using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class WallPropertyInputManager : MonoBehaviour
{
    private const float MinimumWallLength = 0.01f;

    private enum MultiSelectionField
    {
        Length,
        Height,
        Thickness,
        AddOpenings,
    }

    private enum LengthAnchorMode
    {
        LeftFixed,
        RightFixed,
    }

    [Header("References")]
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private UndoRedoManager undoRedoManager;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;

    [Header("Inputs")]
    [SerializeField] private InputField wallLengthInputField;
    [SerializeField] private InputField wallHeightInputField;
    [SerializeField] private InputField wallThicknessInputField;
    [SerializeField] private GameObject addOpeningsTarget;

    [Header("Multi Selection")]
    [SerializeField] private List<MultiSelectionField> disabledFieldsForMultiSelection = new List<MultiSelectionField>();

    [Header("Length Anchor")]
    [SerializeField] private Button leftLengthAnchorButton;
    [SerializeField] private Button rightLengthAnchorButton;
    [SerializeField] private LengthAnchorMode lengthAnchorMode = LengthAnchorMode.LeftFixed;

    private readonly List<Wall> resizeAffectedWalls = new List<Wall>();
    private readonly List<UndoRedoManager.WallStateChangeRecord> resizeStateRecords = new List<UndoRedoManager.WallStateChangeRecord>();
    private readonly List<GameObject> selectedWallObjects = new List<GameObject>();
    private readonly List<Wall> selectedWallComponents = new List<Wall>();
    private readonly HashSet<WallOpeningContainer> selectedOpeningContainers = new HashSet<WallOpeningContainer>();
    private readonly List<TopViewRenderManager> topViewRenderManagers = new List<TopViewRenderManager>();
    private bool suppressInputCallback;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();

        BindAnchorButtons();
        EnsureWallRoot();
        BindStateEvents();
        UpdateInputFieldValues(true);
    }

    private void OnDestroy()
    {
        UnbindStateEvents();
        UnbindAnchorButtons();
    }

    public void ApplySelectedWallLengthFromInput(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText) && wallLengthInputField != null)
        {
            inputText = wallLengthInputField.text;
        }

        if (!IsFieldEnabledForCurrentSelection(MultiSelectionField.Length))
        {
            UpdateInputFieldValues(true);
            return;
        }

        GameObject selectedWall = GetSelectedWall();
        if (selectedWall == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (!TryParseMillimeterInput(inputText, out float targetLengthMillimeters))
        {
            UpdateInputFieldValues(true);
            return;
        }

        float targetLengthUnits = targetLengthMillimeters / 100f;
        if (targetLengthUnits < MinimumWallLength)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (IsMultiSelectionActive())
        {
            GetSelectedWallComponents(selectedWallComponents);
            List<UndoRedoManager.WallStateChangeRecord> multiRecords = new List<UndoRedoManager.WallStateChangeRecord>();
            for (int i = 0; i < selectedWallComponents.Count; i++)
            {
                ApplyWallLengthToWall(selectedWallComponents[i], targetLengthUnits, multiRecords);
            }

            RecordAndRefresh(multiRecords);
            return;
        }

        Wall selectedWallComponent = selectedWall.GetComponent<Wall>();
        if (selectedWallComponent == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (TryApplyContainerLengthFromSelectedWall(selectedWallComponent, targetLengthUnits))
        {
            return;
        }

        Vector3 startPoint = selectedWallComponent.StartPoint;
        Vector3 currentEndPoint = selectedWallComponent.EndPoint;
        Vector3 direction = currentEndPoint - startPoint;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.000001f)
        {
            UpdateInputFieldValues(true);
            return;
        }

        direction.Normalize();
        bool keepsStartFixed = lengthAnchorMode == LengthAnchorMode.LeftFixed;
        Vector3 fixedPoint = keepsStartFixed ? startPoint : currentEndPoint;
        Vector3 targetStartPoint = keepsStartFixed ? startPoint : currentEndPoint - direction * targetLengthUnits;
        Vector3 targetEndPoint = keepsStartFixed ? startPoint + direction * targetLengthUnits : currentEndPoint;
        targetStartPoint.y = startPoint.y;
        targetEndPoint.y = startPoint.y;

        List<UndoRedoManager.WallStateChangeRecord> records = new List<UndoRedoManager.WallStateChangeRecord>();
        int movedVertexId = keepsStartFixed ? selectedWallComponent.EndVertexId : selectedWallComponent.StartVertexId;
        Vector3 movedPoint = keepsStartFixed ? targetEndPoint : targetStartPoint;

        if (movedVertexId > 0)
        {
            CollectWallsSharingVertex(movedVertexId, resizeAffectedWalls);

            for (int i = 0; i < resizeAffectedWalls.Count; i++)
            {
                Wall wall = resizeAffectedWalls[i];
                if (wall == null)
                {
                    continue;
                }

                records.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject),
                });
            }

            WallGeometryService.ApplyVertexMove(
                resizeAffectedWalls,
                movedVertexId,
                movedPoint,
                startPoint.y,
                MinimumWallLength,
                wallLengthDisplay);

            for (int i = records.Count - 1; i >= 0; i--)
            {
                UndoRedoManager.WallStateChangeRecord record = records[i];
                if (record.before.wallObject == null)
                {
                    records.RemoveAt(i);
                    continue;
                }

                record.after = UndoRedoManager.WallStateSnapshot.Capture(record.before.wallObject);
                records[i] = record;
            }
        }
        else
        {
            UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(selectedWall);
            bool applied = selectedWallComponent.TryApplyCurrentProfileAndRefresh(
                targetStartPoint,
                targetEndPoint,
                MinimumWallLength,
                wallLengthDisplay,
                false);

            if (applied)
            {
                records.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = before,
                    after = UndoRedoManager.WallStateSnapshot.Capture(selectedWall),
                });
            }
        }

        RecordAndRefresh(records);
    }

    public void ApplySelectedWallLengthFromCurrentInput()
    {
        if (wallLengthInputField == null)
        {
            return;
        }

        ApplySelectedWallLengthFromInput(wallLengthInputField.text);
    }

    public void SetLeftLengthAnchor()
    {
        lengthAnchorMode = LengthAnchorMode.LeftFixed;
        UpdateAnchorButtonState();
    }

    public void SetRightLengthAnchor()
    {
        lengthAnchorMode = LengthAnchorMode.RightFixed;
        UpdateAnchorButtonState();
    }

    public void ApplySelectedWallHeightFromInput(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText) && wallHeightInputField != null)
        {
            inputText = wallHeightInputField.text;
        }

        if (!IsFieldEnabledForCurrentSelection(MultiSelectionField.Height))
        {
            UpdateInputFieldValues(true);
            return;
        }

        GameObject selectedWall = GetSelectedWall();
        if (selectedWall == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (!TryParseMillimeterInput(inputText, out float targetHeightMillimeters))
        {
            UpdateInputFieldValues(true);
            return;
        }

        float targetHeightUnits = targetHeightMillimeters / 100f;
        if (targetHeightUnits < MinimumWallLength)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (IsMultiSelectionActive())
        {
            GetSelectedWallComponents(selectedWallComponents);
            List<UndoRedoManager.WallStateChangeRecord> multiRecords = new List<UndoRedoManager.WallStateChangeRecord>();
            selectedOpeningContainers.Clear();
            for (int i = 0; i < selectedWallComponents.Count; i++)
            {
                Wall wall = selectedWallComponents[i];
                if (wall == null)
                {
                    continue;
                }

                WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
                if (container != null)
                {
                    if (!selectedOpeningContainers.Add(container))
                    {
                        continue;
                    }

                    ApplyContainerHeightForMultiSelection(container, targetHeightUnits);
                    continue;
                }

                GameObject wallObject = wall.gameObject;
                UndoRedoManager.WallStateSnapshot multiBefore = UndoRedoManager.WallStateSnapshot.Capture(wallObject);
                Transform multiWallTransform = wallObject.transform;
                Vector3 multiScale = multiWallTransform.localScale;
                float multiBottomY = multiWallTransform.position.y - multiScale.y * 0.5f;
                multiScale.y = targetHeightUnits;
                multiWallTransform.localScale = multiScale;

                Vector3 multiPosition = multiWallTransform.position;
                multiPosition.y = multiBottomY + targetHeightUnits * 0.5f;
                multiWallTransform.position = multiPosition;

                wall.SyncEndpointsFromTransform(wall.StartPoint.y);
                wall.RefreshLengthDisplay(wallLengthDisplay, false);
                multiRecords.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = multiBefore,
                    after = UndoRedoManager.WallStateSnapshot.Capture(wallObject),
                });
            }

            RecordAndRefresh(multiRecords);
            return;
        }

        Wall selectedWallComponent = selectedWall.GetComponent<Wall>();
        if (selectedWallComponent == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (TryApplyContainerHeightFromSelectedWall(selectedWallComponent, targetHeightUnits))
        {
            return;
        }

        UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(selectedWall);
        Transform wallTransform = selectedWall.transform;
        Vector3 scale = wallTransform.localScale;
        float bottomY = wallTransform.position.y - scale.y * 0.5f;

        scale.y = targetHeightUnits;
        wallTransform.localScale = scale;

        Vector3 position = wallTransform.position;
        position.y = bottomY + targetHeightUnits * 0.5f;
        wallTransform.position = position;

        selectedWallComponent.SyncEndpointsFromTransform(selectedWallComponent.StartPoint.y);
        selectedWallComponent.RefreshLengthDisplay(wallLengthDisplay, false);

        RecordAndRefresh(new List<UndoRedoManager.WallStateChangeRecord>
        {
            new UndoRedoManager.WallStateChangeRecord
            {
                before = before,
                after = UndoRedoManager.WallStateSnapshot.Capture(selectedWall),
            }
        });
    }

    public void ApplySelectedWallHeightFromCurrentInput()
    {
        if (wallHeightInputField == null)
        {
            return;
        }

        ApplySelectedWallHeightFromInput(wallHeightInputField.text);
    }

    public void ApplySelectedWallThicknessFromInput(string inputText)
    {
        if (string.IsNullOrWhiteSpace(inputText) && wallThicknessInputField != null)
        {
            inputText = wallThicknessInputField.text;
        }

        if (!IsFieldEnabledForCurrentSelection(MultiSelectionField.Thickness))
        {
            UpdateInputFieldValues(true);
            return;
        }

        GameObject selectedWall = GetSelectedWall();
        if (selectedWall == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (!TryParseMillimeterInput(inputText, out float targetThicknessMillimeters))
        {
            UpdateInputFieldValues(true);
            return;
        }

        float targetThicknessUnits = targetThicknessMillimeters / 100f;
        if (targetThicknessUnits < MinimumWallLength)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (IsMultiSelectionActive())
        {
            GetSelectedWallComponents(selectedWallComponents);
            List<UndoRedoManager.WallStateChangeRecord> multiRecords = new List<UndoRedoManager.WallStateChangeRecord>();
            selectedOpeningContainers.Clear();
            for (int i = 0; i < selectedWallComponents.Count; i++)
            {
                Wall wall = selectedWallComponents[i];
                if (wall == null)
                {
                    continue;
                }

                WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
                if (container != null)
                {
                    if (!selectedOpeningContainers.Add(container))
                    {
                        continue;
                    }

                    ApplyContainerThicknessForMultiSelection(container, targetThicknessUnits);
                    continue;
                }

                GameObject wallObject = wall.gameObject;
                UndoRedoManager.WallStateSnapshot multiBefore = UndoRedoManager.WallStateSnapshot.Capture(wallObject);
                Vector3 multiScale = wallObject.transform.localScale;
                multiScale.x = targetThicknessUnits;
                wallObject.transform.localScale = multiScale;

                wall.SyncEndpointsFromTransform(wall.StartPoint.y);
                wall.RefreshLengthDisplay(wallLengthDisplay, false);
                multiRecords.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = multiBefore,
                    after = UndoRedoManager.WallStateSnapshot.Capture(wallObject),
                });
            }

            RecordAndRefresh(multiRecords);
            return;
        }

        Wall selectedWallComponent = selectedWall.GetComponent<Wall>();
        if (selectedWallComponent == null)
        {
            UpdateInputFieldValues(true);
            return;
        }

        if (TryApplyContainerThicknessFromSelectedWall(selectedWallComponent, targetThicknessUnits))
        {
            return;
        }

        UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(selectedWall);
        Vector3 scale = selectedWall.transform.localScale;
        scale.x = targetThicknessUnits;
        selectedWall.transform.localScale = scale;

        selectedWallComponent.SyncEndpointsFromTransform(selectedWallComponent.StartPoint.y);
        selectedWallComponent.RefreshLengthDisplay(wallLengthDisplay, false);

        RecordAndRefresh(new List<UndoRedoManager.WallStateChangeRecord>
        {
            new UndoRedoManager.WallStateChangeRecord
            {
                before = before,
                after = UndoRedoManager.WallStateSnapshot.Capture(selectedWall),
            }
        });
    }

    public void ApplySelectedWallThicknessFromCurrentInput()
    {
        if (wallThicknessInputField == null)
        {
            return;
        }

        ApplySelectedWallThicknessFromInput(wallThicknessInputField.text);
    }

    private void RecordAndRefresh(List<UndoRedoManager.WallStateChangeRecord> records)
    {
        GameObject selectedWall = GetSelectedWall();
        if (records != null && records.Count > 0 && undoRedoManager != null)
        {
            undoRedoManager.RecordMoveConnectedWalls(records);
        }

        if (handleManager != null)
        {
            handleManager.RefreshRegisteredWalls();
        }

        if (roomManager != null)
        {
            roomManager.RefreshAllRooms();
        }

        MarkTopViewDirty();
        UpdateInputFieldValues(true);

        if (selectedWall != null && wallSelectionManager != null)
        {
            wallSelectionManager.SetSelectedWall(selectedWall);
        }
    }

    private bool TryParseMillimeterInput(string inputText, out float millimeters)
    {
        millimeters = 0f;

        if (suppressInputCallback || string.IsNullOrWhiteSpace(inputText))
        {
            return false;
        }

        if (!UnitDisplayUtility.TryParseMillimeters(inputText, out millimeters))
        {
            return false;
        }

        return millimeters > 0f;
    }

    private void CollectWallsSharingVertex(int vertexId, List<Wall> result)
    {
        if (result == null)
        {
            return;
        }

        result.Clear();
        if (wallRoot == null || vertexId <= 0)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, result);
        for (int i = result.Count - 1; i >= 0; i--)
        {
            Wall wall = result[i];
            if (wall == null || !wall.ContainsVertexId(vertexId))
            {
                result.RemoveAt(i);
            }
        }
    }

    private GameObject GetSelectedWall()
    {
        if (wallSelectionManager == null)
        {
            return null;
        }

        if (wallSelectionManager.SelectedWall != null)
        {
            return wallSelectionManager.SelectedWall;
        }

        wallSelectionManager.GetSelectedWalls(selectedWallObjects);
        for (int i = 0; i < selectedWallObjects.Count; i++)
        {
            if (selectedWallObjects[i] != null)
            {
                return selectedWallObjects[i];
            }
        }

        return null;
    }

    private void GetSelectedWallComponents(List<Wall> result)
    {
        if (result == null)
        {
            return;
        }

        result.Clear();
        if (wallSelectionManager == null)
        {
            return;
        }

        wallSelectionManager.GetSelectedWalls(selectedWallObjects);
        for (int i = 0; i < selectedWallObjects.Count; i++)
        {
            GameObject wallObject = selectedWallObjects[i];
            if (wallObject == null || !wallObject.TryGetComponent(out Wall wall))
            {
                continue;
            }

            result.Add(wall);
        }
    }

    private bool IsFieldEnabledForCurrentSelection(MultiSelectionField field)
    {
        if (wallSelectionManager == null || !wallSelectionManager.HasMultiWallSelection)
        {
            return true;
        }

        return !disabledFieldsForMultiSelection.Contains(field);
    }

    private bool IsMultiSelectionActive()
    {
        return wallSelectionManager != null && wallSelectionManager.HasMultiWallSelection;
    }

    private float GetDisplayedLengthUnits(Wall wall)
    {
        if (wall == null)
        {
            return 0f;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        return container != null ? container.WallLength : wall.Length;
    }

    private float GetDisplayedHeightUnits(GameObject selectedWall)
    {
        if (selectedWall == null)
        {
            return 0f;
        }

        WallOpeningContainer container = selectedWall.GetComponentInParent<WallOpeningContainer>();
        return container != null ? container.WallHeight : selectedWall.transform.localScale.y;
    }

    private float GetDisplayedThicknessUnits(GameObject selectedWall)
    {
        if (selectedWall == null)
        {
            return 0f;
        }

        WallOpeningContainer container = selectedWall.GetComponentInParent<WallOpeningContainer>();
        return container != null ? container.WallThickness : selectedWall.transform.localScale.x;
    }

    private bool TryApplyContainerLengthFromSelectedWall(Wall selectedWallComponent, float targetLengthUnits)
    {
        if (selectedWallComponent == null || wallOpeningPlacementManager == null)
        {
            return false;
        }

        WallOpeningContainer container = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        bool keepsStartFixed = lengthAnchorMode == LengthAnchorMode.LeftFixed;

        Vector3 direction = container.WallDirection;
        Vector3 oldStart = container.WallStart;
        Vector3 oldEnd = container.WallEnd;
        Vector3 newStart = oldStart;
        Vector3 newEnd = oldEnd;
        float openingShift = 0f;

        if (keepsStartFixed)
        {
            newEnd = oldStart + direction * targetLengthUnits;
        }
        else
        {
            newStart = oldEnd - direction * targetLengthUnits;
            openingShift = Vector3.Dot(oldStart - newStart, direction);
        }

        WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
        float minimumSideWallUnits = wallOpeningPlacementManager.MinimumSideWallUnits;
        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            float nextCenterDistance = opening.CenterDistance + openingShift;
            float halfWidth = opening.Width * 0.5f;
            if (nextCenterDistance - halfWidth < minimumSideWallUnits ||
                nextCenterDistance + halfWidth > targetLengthUnits - minimumSideWallUnits)
            {
                UpdateInputFieldValues(true);
                return true;
            }
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
        resizeStateRecords.Clear();

        int movedVertexId = keepsStartFixed ? container.OuterEndVertexId : container.OuterStartVertexId;
        Vector3 movedPoint = keepsStartFixed ? newEnd : newStart;
        CollectWallsSharingVertex(movedVertexId, resizeAffectedWalls);
        for (int i = resizeAffectedWalls.Count - 1; i >= 0; i--)
        {
            Wall wall = resizeAffectedWalls[i];
            if (wall == null)
            {
                resizeAffectedWalls.RemoveAt(i);
                continue;
            }

            if (wall.GetComponentInParent<WallOpeningContainer>() == container)
            {
                resizeAffectedWalls.RemoveAt(i);
                continue;
            }

            resizeStateRecords.Add(new UndoRedoManager.WallStateChangeRecord
            {
                before = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject),
            });
        }

        container.SetWallSpan(newStart, newEnd);

        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            opening.SetCenterDistance(opening.CenterDistance + openingShift);
        }

        wallOpeningPlacementManager.RebuildOpeningContainer(container);
        wallOpeningPlacementManager.SelectPreferredWallForContainer(container, targetLengthUnits * 0.5f);
        if (resizeAffectedWalls.Count > 0)
        {
            WallGeometryService.ApplyVertexMove(
                resizeAffectedWalls,
                movedVertexId,
                movedPoint,
                movedPoint.y,
                MinimumWallLength,
                wallLengthDisplay);
        }

        if (undoRedoManager != null)
        {
            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, afterSnapshot);

            for (int i = 0; i < resizeStateRecords.Count; i++)
            {
                UndoRedoManager.WallStateChangeRecord record = resizeStateRecords[i];
                if (record.before.wallObject == null)
                {
                    continue;
                }

                record.after = UndoRedoManager.WallStateSnapshot.Capture(record.before.wallObject);
                resizeStateRecords[i] = record;
            }

            undoRedoManager.RecordMoveConnectedWalls(resizeStateRecords);
        }

        if (handleManager != null)
        {
            handleManager.RefreshRegisteredWalls();
        }

        if (roomManager != null)
        {
            roomManager.RefreshAllRooms();
        }

        MarkTopViewDirty();
        UpdateInputFieldValues(true);

        return true;
    }

    private void ApplyWallLengthToWall(Wall selectedWallComponent, float targetLengthUnits, List<UndoRedoManager.WallStateChangeRecord> records)
    {
        if (selectedWallComponent == null || records == null)
        {
            return;
        }

        Vector3 startPoint = selectedWallComponent.StartPoint;
        Vector3 currentEndPoint = selectedWallComponent.EndPoint;
        Vector3 direction = currentEndPoint - startPoint;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        direction.Normalize();
        bool keepsStartFixed = lengthAnchorMode == LengthAnchorMode.LeftFixed;
        Vector3 targetStartPoint = keepsStartFixed ? startPoint : currentEndPoint - direction * targetLengthUnits;
        Vector3 targetEndPoint = keepsStartFixed ? startPoint + direction * targetLengthUnits : currentEndPoint;
        targetStartPoint.y = startPoint.y;
        targetEndPoint.y = startPoint.y;

        GameObject wallObject = selectedWallComponent.gameObject;
        int movedVertexId = keepsStartFixed ? selectedWallComponent.EndVertexId : selectedWallComponent.StartVertexId;
        Vector3 movedPoint = keepsStartFixed ? targetEndPoint : targetStartPoint;

        if (movedVertexId > 0)
        {
            List<UndoRedoManager.WallStateChangeRecord> localRecords = new List<UndoRedoManager.WallStateChangeRecord>();
            CollectWallsSharingVertex(movedVertexId, resizeAffectedWalls);
            for (int i = 0; i < resizeAffectedWalls.Count; i++)
            {
                Wall wall = resizeAffectedWalls[i];
                if (wall == null)
                {
                    continue;
                }

                localRecords.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject),
                });
            }

            WallGeometryService.ApplyVertexMove(
                resizeAffectedWalls,
                movedVertexId,
                movedPoint,
                startPoint.y,
                MinimumWallLength,
                wallLengthDisplay);

            for (int i = 0; i < localRecords.Count; i++)
            {
                UndoRedoManager.WallStateChangeRecord record = localRecords[i];
                if (record.before.wallObject == null)
                {
                    continue;
                }

                record.after = UndoRedoManager.WallStateSnapshot.Capture(record.before.wallObject);
                localRecords[i] = record;
            }

            records.AddRange(localRecords);
            return;
        }

        UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(wallObject);
        bool applied = selectedWallComponent.TryApplyCurrentProfileAndRefresh(
            targetStartPoint,
            targetEndPoint,
            MinimumWallLength,
            wallLengthDisplay,
            false);
        if (!applied)
        {
            return;
        }

        records.Add(new UndoRedoManager.WallStateChangeRecord
        {
            before = before,
            after = UndoRedoManager.WallStateSnapshot.Capture(wallObject),
        });
    }

    private bool TryApplyContainerHeightFromSelectedWall(Wall selectedWallComponent, float targetHeightUnits)
    {
        if (selectedWallComponent == null || wallOpeningPlacementManager == null)
        {
            return false;
        }

        WallOpeningContainer container = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
        container.SetWallHeightKeepingBottom(targetHeightUnits);
        wallOpeningPlacementManager.RebuildOpeningContainer(container);
        wallOpeningPlacementManager.SelectPreferredWallForContainer(container, container.WallLength * 0.5f);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, wallOpeningPlacementManager.CaptureLayoutSnapshot(container));
        }

        if (handleManager != null)
        {
            handleManager.RefreshRegisteredWalls();
        }

        MarkTopViewDirty();
        UpdateInputFieldValues(true);

        return true;
    }

    private void ApplyContainerHeightForMultiSelection(WallOpeningContainer container, float targetHeightUnits)
    {
        if (container == null || wallOpeningPlacementManager == null)
        {
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
        container.SetWallHeightKeepingBottom(targetHeightUnits);
        wallOpeningPlacementManager.RebuildOpeningContainer(container);
        wallOpeningPlacementManager.SelectPreferredWallForContainer(container, container.WallLength * 0.5f);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, wallOpeningPlacementManager.CaptureLayoutSnapshot(container));
        }

        if (roomManager != null)
        {
            roomManager.RefreshAllRooms();
        }

        MarkTopViewDirty();
    }

    private bool TryApplyContainerThicknessFromSelectedWall(Wall selectedWallComponent, float targetThicknessUnits)
    {
        if (selectedWallComponent == null || wallOpeningPlacementManager == null)
        {
            return false;
        }

        WallOpeningContainer container = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
        container.SetWallThickness(targetThicknessUnits);
        wallOpeningPlacementManager.RebuildOpeningContainer(container);
        wallOpeningPlacementManager.SelectPreferredWallForContainer(container, container.WallLength * 0.5f);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, wallOpeningPlacementManager.CaptureLayoutSnapshot(container));
        }

        if (handleManager != null)
        {
            handleManager.RefreshRegisteredWalls();
        }

        if (roomManager != null)
        {
            roomManager.RefreshAllRooms();
        }

        MarkTopViewDirty();
        UpdateInputFieldValues(true);

        return true;
    }

    private void ApplyContainerThicknessForMultiSelection(WallOpeningContainer container, float targetThicknessUnits)
    {
        if (container == null || wallOpeningPlacementManager == null)
        {
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
        container.SetWallThickness(targetThicknessUnits);
        wallOpeningPlacementManager.RebuildOpeningContainer(container);
        wallOpeningPlacementManager.SelectPreferredWallForContainer(container, container.WallLength * 0.5f);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, wallOpeningPlacementManager.CaptureLayoutSnapshot(container));
        }

        if (roomManager != null)
        {
            roomManager.RefreshAllRooms();
        }

        MarkTopViewDirty();
    }

    private void EnsureWallRoot()
    {
        LayerUtility.ResolveTransformByName(ref wallRoot, "Walls", true);
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveObject(ref handleManager);
        LayerUtility.ResolveObject(ref wallLengthDisplay);
        LayerUtility.ResolveObject(ref undoRedoManager);
        LayerUtility.ResolveObject(ref modeManager);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref wallOpeningPlacementManager);
    }

    private void MarkTopViewDirty()
    {
        if (topViewRenderManagers.Count == 0)
        {
            TopViewRenderManager[] managers = FindObjectsByType<TopViewRenderManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null)
                {
                    topViewRenderManagers.Add(managers[i]);
                }
            }
        }

        for (int i = topViewRenderManagers.Count - 1; i >= 0; i--)
        {
            TopViewRenderManager manager = topViewRenderManagers[i];
            if (manager == null)
            {
                topViewRenderManagers.RemoveAt(i);
                continue;
            }

            manager.MarkDirty();
        }
    }
}
