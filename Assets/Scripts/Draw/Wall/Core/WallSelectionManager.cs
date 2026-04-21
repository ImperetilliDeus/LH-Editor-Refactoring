using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System.Collections.Generic;
using UnityEngine.UI;

public partial class WallSelectionManager : MonoBehaviour
{
    private enum SelectionModifierKey
    {
        None,
        Ctrl,
        Shift,
        Alt,
    }

    private const float MinimumWallLength = 0.01f;

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject grid;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private DrawManager drawManager;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private SnapManager snapManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private UndoRedoManager undoRedoManager;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;
    [SerializeField] private RoomManager roomManager;

    [Header("Selection Visual")]
    [SerializeField] private Canvas wallSelectionCanvas;
    [SerializeField] private Color wallUINormalColor = new Color(1f, 0.62f, 0.12f, 0.04f);
    [SerializeField] private Color wallUISelectedColor = new Color(1f, 0.62f, 0.12f, 0.28f);
    [SerializeField] private float wallUIThicknessPixels = 16f;
    [SerializeField] private float multiSelectDragThresholdPixels = 6f;
    [SerializeField] private float multiSelectBoxHeight = 1f;
    [SerializeField] private Color multiSelectBoxColor = new Color(1f, 0.62f, 0.12f, 0.14f);
    [SerializeField] private SelectionModifierKey multiSelectModifierKey = SelectionModifierKey.Ctrl;

    [Header("Wall Drag")]
    [SerializeField] private float dragStartThresholdPixels = 6f;
    [SerializeField] private bool snapDraggedWallToGrid = true;
    [SerializeField] private float connectedEndpointThreshold = 0.35f;

    [Header("Selection UI")]
    [FormerlySerializedAs("dragUIObject")]
    [SerializeField] private GameObject selectUIObject;
    [SerializeField] private Button deleteSelectionButton;

    private IEditorInputProvider inputProvider;
    private Plane dragPlane;
    private float dragPlaneHeight;
    private bool hasDragPlane;
    private Bounds gridBounds;
    private bool hasGridBounds;

    private GameObject selectedWall;
    private readonly HashSet<GameObject> detailSelectedWalls = new HashSet<GameObject>();
    private readonly HashSet<GameObject> multiSelectWallsInBox = new HashSet<GameObject>();
    private GameObject multiSelectBoxObject;
    private BoxCollider multiSelectBoxCollider;
    private Material multiSelectBoxMaterial;
    private bool pendingMultiSelectDrag;
    private bool isMultiSelecting;
    private bool addToSelectionOnDragStart;
    private Vector2 multiSelectStartMousePosition;
    private Vector3 multiSelectStartWorldPoint;

    private bool pendingWallDrag;
    private bool isDraggingWall;
    private Vector2 pendingStartMousePosition;
    private Vector3 dragStartPoint;
    private Vector3 selectedWallStartPosition;
    private Vector3 moveStartWallPosition;
    private Quaternion moveStartWallRotation;
    private Vector3 moveStartWallScale;
    private readonly List<Vector3> moveSnapCandidates = new List<Vector3>();
    private readonly Dictionary<GameObject, UndoRedoManager.WallStateSnapshot> moveStartSnapshots = new Dictionary<GameObject, UndoRedoManager.WallStateSnapshot>();
    private readonly Dictionary<GameObject, WallGeometryService.WallEndpointState> moveStartEndpointSnapshots = new Dictionary<GameObject, WallGeometryService.WallEndpointState>();
    private readonly List<Wall> dragAffectedWalls = new List<Wall>();
    private readonly List<WallOpeningContainer> dragAffectedOpeningContainers = new List<WallOpeningContainer>();
    private readonly Dictionary<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot> moveStartConnectedOpeningSnapshots = new Dictionary<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<Wall> rootWallsCache = new List<Wall>();
    private readonly List<UndoRedoManager.WallStateChangeRecord> moveStateChangeRecords = new List<UndoRedoManager.WallStateChangeRecord>();
    private readonly List<UndoRedoManager.OpeningLayoutChangeRecord> moveOpeningChangeRecords = new List<UndoRedoManager.OpeningLayoutChangeRecord>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private readonly HashSet<WallOpeningContainer> processedSelectionUIContainers = new HashSet<WallOpeningContainer>();
    private Mesh cachedCubeMesh;
    private Vector3 dragSelectedStartPoint;
    private Vector3 dragSelectedEndPoint;
    private int dragSelectedStartVertexId;
    private int dragSelectedEndVertexId;
    private WallOpeningContainer selectedOpeningContainer;
    private Vector3 moveStartContainerPosition;
    private Vector3 moveStartContainerWallStart;
    private Vector3 moveStartContainerWallEnd;
    private UndoRedoManager.OpeningLayoutSnapshot moveStartOpeningLayoutSnapshot;
    private bool hasMoveStartOpeningLayoutSnapshot;
    private bool rootWallsCacheDirty = true;

    public bool IsDraggingWall => isDraggingWall;
    public GameObject SelectedWall => selectedWall;
    public Canvas WallSelectionCanvas => wallSelectionCanvas;
    public Camera SelectionCamera => mainCamera;
    public Color WallUINormalColor => wallUINormalColor;
    public Color WallUISelectedColor => wallUISelectedColor;
    public float WallUIThicknessPixels => wallUIThicknessPixels;
    public bool IsWallUIInteractionEnabled => modeManager != null &&
                                              modeManager.IsMode(EditorMode.DetailEdit) &&
                                              (wallOpeningPlacementManager == null || !wallOpeningPlacementManager.IsOpeningDetailMenuVisible);
    public int SelectedWallCount => (selectedWall != null ? 1 : 0) + detailSelectedWalls.Count;
    public bool HasMultiWallSelection => SelectedWallCount > 1;

    public event Action<GameObject> SelectionChanged;
    public event Action SelectionSetChanged;

    private GameObject lastNotifiedSelectedWall;

    public void SetSelectedWall(GameObject wall)
    {
        SelectWall(wall, false);
    }

    public void SetSelectedWallPreservingOpeningSelection(GameObject wall)
    {
        SelectWall(wall, true);
    }

    public void GetSelectedWalls(List<GameObject> result)
    {
        if (result == null)
        {
            return;
        }

        result.Clear();
        if (selectedWall != null)
        {
            result.Add(selectedWall);
        }

        foreach (GameObject wall in detailSelectedWalls)
        {
            if (wall != null && wall != selectedWall)
            {
                result.Add(wall);
            }
        }
    }

    public void HandleWallUIClick(GameObject wallObject)
    {
        if (wallObject == null)
        {
            return;
        }

        if (modeManager != null && modeManager.IsMode(EditorMode.DetailEdit))
        {
            HandleDetailWallClick(wallObject, IsSelectionModifierPressed());
            return;
        }

        SelectWall(wallObject, false);
    }

    public bool IsPointerBlockedByNonWallUI(GameObject allowedWallUIObject = null)
    {
        if (EventSystem.current == null || inputProvider == null || !inputProvider.TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = pointerScreenPosition,
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);
        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            if (allowedWallUIObject != null && (hitObject == allowedWallUIObject || hitObject.transform.IsChildOf(allowedWallUIObject.transform)))
            {
                continue;
            }

            if (LayerUtility.IsLayer(hitObject, LayerUtility.WallUILayerName))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public void DeleteCurrentSelection()
    {
        EditorMode currentMode = modeManager != null ? modeManager.CurrentMode : EditorMode.Default;
        if (currentMode != EditorMode.Default && currentMode != EditorMode.DetailEdit)
        {
            return;
        }

        if (wallOpeningPlacementManager != null && wallOpeningPlacementManager.SelectedOpening != null)
        {
            wallOpeningPlacementManager.DeleteSelectedOpening();
            return;
        }

        if (selectedWall == null && detailSelectedWalls.Count == 0)
        {
            return;
        }

        DeleteSelectedWalls();
    }

    private void Reset()
    {
        mainCamera = Camera.main;
        ResolveReferences();
    }

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        inputProvider = new UnityEditorInputProvider();
        ResolveReferences();

        EnsureWallRoot();
        RefreshDragPlane();
        EnsureCachedResources();
        PrepareSelectUI();
        EnsureMultiSelectBoxVisual();
        EnsureSelectionCanvas();
        BindHandleEvents();
        BindUIEvents();
    }

    private void OnValidate()
    {
        wallUIThicknessPixels = Mathf.Max(4f, wallUIThicknessPixels);
        dragStartThresholdPixels = Mathf.Max(0f, dragStartThresholdPixels);
        multiSelectDragThresholdPixels = Mathf.Max(0f, multiSelectDragThresholdPixels);
        multiSelectBoxHeight = Mathf.Max(0.01f, multiSelectBoxHeight);
        connectedEndpointThreshold = Mathf.Max(0.01f, connectedEndpointThreshold);
    }

    private void Update()
    {
        if (!TryGetPointerFrame(out EditorPointerFrame pointerFrame))
        {
            return;
        }

        EditorMode currentMode = modeManager != null ? modeManager.CurrentMode : EditorMode.Default;
        if (currentMode == EditorMode.DetailEdit)
        {
            UpdateDetailEditMode(pointerFrame);
            return;
        }

        if (detailSelectedWalls.Count > 0)
        {
            ClearAllSelectionState();
        }

        if (currentMode != EditorMode.Default)
        {
            FinalizeMoveIfNeeded();
            ClearAllSelectionState();
            ResetDragState();
            return;
        }

        RefreshWallSelectionUIPositions();

        if (selectedWall != null && pointerFrame.RightPressedThisFrame)
        {
            if (drawManager == null || !drawManager.IsWallCreationMode)
            {
                FinalizeMoveIfNeeded();
                ClearSingleSelection();
                ResetDragState();
                return;
            }
        }

        if (selectedWall == null)
        {
            ClearSingleSelection();
            ResetDragState();

            return;
        }

        SetSelectUIVisible(false);

        if (pendingWallDrag && !pointerFrame.LeftPressed)
        {
            FinalizeMoveIfNeeded();
            ResetDragState();

            return;
        }

        if (drawManager != null && drawManager.IsWallCreationMode)
        {
            FinalizeMoveIfNeeded();
            ResetDragState();

            return;
        }

        if (handleManager != null && handleManager.IsDraggingHandle)
        {
            FinalizeMoveIfNeeded();
            ResetDragState();

            return;
        }

        if (pendingWallDrag && !isDraggingWall)
        {
            Vector2 currentMouse = pointerFrame.ScreenPosition;
            float movedSqr = (currentMouse - pendingStartMousePosition).sqrMagnitude;
            float thresholdSqr = dragStartThresholdPixels * dragStartThresholdPixels;
            if (movedSqr >= thresholdSqr)
            {
                moveStartWallPosition = selectedWall.transform.position;
                moveStartWallRotation = selectedWall.transform.rotation;
                moveStartWallScale = selectedWall.transform.localScale;
                PrepareConnectedWallDrag();
                isDraggingWall = true;
            }
        }

        if (!isDraggingWall)
        {
            return;
        }

        if (!TryGetMouseWorldPoint(pointerFrame.ScreenPosition, out Vector3 currentPoint))
        {
            return;
        }

        Vector3 delta = currentPoint - dragStartPoint;
        Vector3 targetPosition = selectedWallStartPosition + new Vector3(delta.x, 0f, delta.z);

        if (snapDraggedWallToGrid && snapManager != null)
        {
            targetPosition = snapManager.GetSnappedPoint(targetPosition, targetPosition);
        }

        TryApplyHandleSnapToMovedWall(selectedWall.transform, ref targetPosition);

        if (hasGridBounds)
        {
            targetPosition = ClampWallCenterPositionInsideBounds(selectedWall.transform, targetPosition);
        }

        Vector3 targetWallPosition = new Vector3(targetPosition.x, selectedWallStartPosition.y, targetPosition.z);
        Vector3 translationDelta = targetWallPosition - selectedWallStartPosition;
        translationDelta.y = 0f;

        // Keep endpoint data in sync during drag so Room/Handle systems see consistent geometry.
        SyncAllWallComponentEndpoints();
        ApplyConnectedWallDrag(translationDelta, targetWallPosition);
    }

    public bool TryConsumeIdleLeftPress()
    {
        if (!TryGetPointerFrame(out EditorPointerFrame pointerFrame))
        {
            return false;
        }

        return TryConsumeIdleLeftPress(pointerFrame);
    }

    internal bool TryConsumeIdleLeftPress(EditorPointerFrame pointerFrame)
    {
        if (mainCamera == null || !pointerFrame.IsAvailable)
        {
            return false;
        }

        if (modeManager != null && !modeManager.IsMode(EditorMode.Default))
        {
            return false;
        }

        if (IsPointerOverUI(pointerFrame.ScreenPosition))
        {
            return false;
        }

        if (drawManager != null && drawManager.IsWallCreationMode)
        {
            return false;
        }

        if (handleManager != null && handleManager.IsDraggingHandle)
        {
            return false;
        }

        Vector2 mousePosition = pointerFrame.ScreenPosition;
        if (handleManager != null && handleManager.IsPointerOverHandle(mousePosition))
        {
            return false;
        }

        if (!TryGetWallFromMouseRay(pointerFrame.ScreenPosition, out GameObject hitWall))
        {
            return false;
        }

            SelectWall(hitWall, false);

        ResetDragState();
        SetSelectUIVisible(false);

        if (TryGetMouseWorldPoint(pointerFrame.ScreenPosition, out Vector3 worldPoint))
        {
            pendingWallDrag = true;
            pendingStartMousePosition = mousePosition;
            dragStartPoint = worldPoint;
            selectedWallStartPosition = selectedWall.transform.position;
        }

        return true;
    }

    private bool TryGetWallFromMouseRay(Vector2 pointerScreenPosition, out GameObject wall)
    {
        wall = null;

        Ray ray = mainCamera.ScreenPointToRay(pointerScreenPosition);
        int wallMask = LayerUtility.GetMaskOrDefault(LayerUtility.WallLayerName);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, wallMask))
        {
            return false;
        }

        GameObject hitObject = hitInfo.collider != null ? hitInfo.collider.gameObject : null;
        if (hitObject == null)
        {
            return false;
        }

        GameObject wallObject = ResolveWallObject(hitObject);
        if (wallObject == null)
        {
            return false;
        }

        wall = wallObject;
        return true;
    }

    private bool IsWallObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (LayerUtility.TryGetLayer(LayerUtility.WallLayerName, out int wallLayer) &&
            candidate.layer != wallLayer)
        {
            return false;
        }

        if (wallRoot == null)
        {
            return true;
        }

        return candidate.transform.IsChildOf(wallRoot);
    }

    private GameObject ResolveWallObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        Wall wall = candidate.GetComponentInParent<Wall>();
        if (wall != null && IsWallObject(wall.gameObject))
        {
            return wall.gameObject;
        }

        if (IsWallObject(candidate))
        {
            return candidate;
        }

        return null;
    }

    private bool IsPointerOverUI(Vector2 pointerScreenPosition)
    {
        if (EventSystem.current == null || inputProvider == null)
        {
            return false;
        }

        return inputProvider.IsPointerOverUI(EventSystem.current, uiRaycastResults);
    }

    private void UpdateDetailEditMode(EditorPointerFrame pointerFrame)
    {
        FinalizeMoveIfNeeded();
        ResetDragState();
        RefreshWallSelectionUIPositions();

        if (pointerFrame.RightPressedThisFrame)
        {
            ClearAllSelectionState();
            return;
        }

        if (pointerFrame.LeftPressedThisFrame)
        {
            TryBeginDetailSelection(pointerFrame);
        }

        if (pendingMultiSelectDrag && pointerFrame.LeftPressed)
        {
            Vector2 currentMouse = pointerFrame.ScreenPosition;
            float movedSqr = (currentMouse - multiSelectStartMousePosition).sqrMagnitude;
            if (movedSqr >= multiSelectDragThresholdPixels * multiSelectDragThresholdPixels)
            {
                pendingMultiSelectDrag = false;
                isMultiSelecting = true;
                UpdateMultiSelectDrag(pointerFrame);
            }
        }

        if (isMultiSelecting && pointerFrame.LeftPressed)
        {
            UpdateMultiSelectDrag(pointerFrame);
        }

        if ((pendingMultiSelectDrag || isMultiSelecting) && pointerFrame.LeftReleasedThisFrame)
        {
            FinishMultiSelectDrag();
        }
    }

    private void TryBeginDetailSelection(EditorPointerFrame pointerFrame)
    {
        if (IsPointerOverUI(pointerFrame.ScreenPosition))
        {
            return;
        }

        bool isModifierPressed = IsSelectionModifierPressed();
        if (TryGetWallFromMouseRay(pointerFrame.ScreenPosition, out GameObject hitWall))
        {
            HandleDetailWallClick(hitWall, isModifierPressed);
            return;
        }

        if (!TryGetMouseWorldPoint(pointerFrame.ScreenPosition, out Vector3 worldPoint))
        {
            return;
        }

        addToSelectionOnDragStart = isModifierPressed;
        pendingMultiSelectDrag = true;
        isMultiSelecting = false;
        multiSelectStartMousePosition = pointerFrame.ScreenPosition;
        multiSelectStartWorldPoint = worldPoint;
        ShowMultiSelectBox();
        UpdateMultiSelectBox(multiSelectStartWorldPoint, multiSelectStartWorldPoint);
    }

    private void HandleDetailWallClick(GameObject wallObject, bool isModifierPressed)
    {
        CancelMultiSelectDrag();

        if (!isModifierPressed)
        {
            detailSelectedWalls.Clear();
            SelectWall(wallObject, false);
            return;
        }

        if (wallObject == selectedWall)
        {
            selectedWall = null;
            RefreshSelectionVisuals();
            return;
        }

        if (detailSelectedWalls.Contains(wallObject))
        {
            detailSelectedWalls.Remove(wallObject);
        }
        else
        {
            detailSelectedWalls.Add(wallObject);
        }

        RefreshSelectionVisuals();
    }

    private void UpdateMultiSelectDrag(EditorPointerFrame pointerFrame)
    {
        if (!TryGetMouseWorldPoint(pointerFrame.ScreenPosition, out Vector3 currentWorldPoint))
        {
            return;
        }

        UpdateMultiSelectBox(multiSelectStartWorldPoint, currentWorldPoint);
        UpdateWallsFromMultiSelectBox(addToSelectionOnDragStart);
    }

    private void FinishMultiSelectDrag()
    {
        bool hadDrag = isMultiSelecting;
        pendingMultiSelectDrag = false;
        isMultiSelecting = false;
        HideMultiSelectBox();

        if (!hadDrag)
        {
            return;
        }

        UpdateSelectUIVisibility();
    }

    private void CancelMultiSelectDrag()
    {
        pendingMultiSelectDrag = false;
        isMultiSelecting = false;
        HideMultiSelectBox();
    }

    private void UpdateWallsFromMultiSelectBox(bool additive)
    {
        if (wallOpeningPlacementManager != null && wallOpeningPlacementManager.SelectedOpening != null)
        {
            wallOpeningPlacementManager.ClearOpeningSelection();
        }

        multiSelectWallsInBox.Clear();
        if (multiSelectBoxCollider != null && wallRoot != null)
        {
            Bounds bounds = multiSelectBoxCollider.bounds;
            List<Wall> walls = GetRootWalls();
            processedSelectionUIContainers.Clear();
            for (int i = 0; i < walls.Count; i++)
            {
                Wall wall = walls[i];
                if (wall == null || !wall.gameObject.activeInHierarchy)
                {
                    continue;
                }

                WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
                if (container != null)
                {
                    if (!processedSelectionUIContainers.Add(container))
                    {
                        continue;
                    }

                    if (TryGetSelectableWallFromContainerInBounds(container, bounds, out GameObject representativeWall))
                    {
                        multiSelectWallsInBox.Add(representativeWall);
                    }

                    continue;
                }

                if (ContainsPointXZ(bounds, wall.Data.startPoint) && ContainsPointXZ(bounds, wall.Data.endPoint))
                {
                    multiSelectWallsInBox.Add(wall.gameObject);
                }
            }
        }

        if (!additive)
        {
            detailSelectedWalls.Clear();
            selectedWall = null;
        }

        foreach (GameObject wallObject in multiSelectWallsInBox)
        {
            if (selectedWall == null)
            {
                selectedWall = wallObject;
            }
            else if (wallObject != selectedWall)
            {
                detailSelectedWalls.Add(wallObject);
            }
        }

        RefreshSelectionVisuals();
    }

    private bool TryGetSelectableWallFromContainerInBounds(WallOpeningContainer container, Bounds bounds, out GameObject representativeWall)
    {
        representativeWall = null;
        if (container == null)
        {
            return false;
        }

        Wall[] containerWalls = container.GetComponentsInChildren<Wall>(true);
        Wall bestWall = null;
        float bestLength = float.MinValue;

        for (int i = 0; i < containerWalls.Length; i++)
        {
            Wall wall = containerWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!ContainsPointXZ(bounds, wall.Data.startPoint) || !ContainsPointXZ(bounds, wall.Data.endPoint))
            {
                return false;
            }

            float length = (wall.Data.endPoint - wall.Data.startPoint).sqrMagnitude;
            if (length <= bestLength)
            {
                continue;
            }

            bestLength = length;
            bestWall = wall;
        }

        if (bestWall == null)
        {
            return false;
        }

        representativeWall = bestWall.gameObject;
        return true;
    }

    private bool ContainsPointXZ(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x &&
               point.x <= bounds.max.x &&
               point.z >= bounds.min.z &&
               point.z <= bounds.max.z;
    }

    private bool IsSelectionModifierPressed()
    {
        switch (multiSelectModifierKey)
        {
            case SelectionModifierKey.None:
                return false;
            case SelectionModifierKey.Ctrl:
                return IsAnyKeyPressed(Key.LeftCtrl, Key.RightCtrl);
            case SelectionModifierKey.Shift:
                return IsAnyKeyPressed(Key.LeftShift, Key.RightShift);
            case SelectionModifierKey.Alt:
                return IsAnyKeyPressed(Key.LeftAlt, Key.RightAlt);
            default:
                return false;
        }
    }

    private bool IsAnyKeyPressed(Key firstKey, Key secondKey)
    {
        return inputProvider != null &&
               (inputProvider.IsKeyPressed(firstKey) || inputProvider.IsKeyPressed(secondKey));
    }

    private void SelectWall(GameObject wall, bool preserveOpeningSelection)
    {
        if (wall == null)
        {
            return;
        }

        if (!preserveOpeningSelection &&
            wallOpeningPlacementManager != null &&
            wallOpeningPlacementManager.IsOpeningDetailMenuVisible)
        {
            wallOpeningPlacementManager.ClearOpeningSelection();
        }

        if (wall == selectedWall)
        {
            UpdateSelectUIVisibility();
            return;
        }

        detailSelectedWalls.Clear();
        selectedWall = wall;
        RefreshSelectionVisuals();
    }

    private void ClearSingleSelection()
    {
        selectedWall = null;
        RefreshSelectionVisuals();
    }

    private void ClearAllSelectionState()
    {
        selectedWall = null;
        detailSelectedWalls.Clear();
        RefreshSelectionVisuals();
        CancelMultiSelectDrag();
    }

    private bool TryGetMouseWorldPoint(Vector2 pointerScreenPosition, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (!hasDragPlane || mainCamera == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(pointerScreenPosition);
        if (!dragPlane.Raycast(ray, out float enter))
        {
            return false;
        }

        worldPoint = ray.GetPoint(enter);
        worldPoint.y = dragPlaneHeight;
        return true;
    }

    private void SyncWallComponentEndpoints(Transform wallTransform)
    {
        WallGeometryService.SyncWallFromTransform(wallTransform, dragPlaneHeight);
    }

    private void PrepareConnectedWallDrag()
    {
        moveStartSnapshots.Clear();
        moveStartEndpointSnapshots.Clear();
        dragAffectedWalls.Clear();
        dragAffectedOpeningContainers.Clear();
        moveStartConnectedOpeningSnapshots.Clear();
        selectedOpeningContainer = null;
        hasMoveStartOpeningLayoutSnapshot = false;

        // Ensure all Wall endpoints reflect the latest transforms before connectivity checks.
        SyncAllWallComponentEndpoints();

        if (selectedWall == null)
        {
            return;
        }

        Wall selectedWallComponent = selectedWall.GetComponent<Wall>();
        if (selectedWallComponent == null)
        {
            return;
        }

        dragSelectedStartPoint = selectedWallComponent.Data.startPoint;
        dragSelectedEndPoint = selectedWallComponent.Data.endPoint;
        dragSelectedStartPoint.y = dragPlaneHeight;
        dragSelectedEndPoint.y = dragPlaneHeight;

        dragSelectedStartVertexId = selectedWallComponent.StartVertexId;
        dragSelectedEndVertexId = selectedWallComponent.EndVertexId;

        WallOpeningContainer openingContainer = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (openingContainer != null)
        {
            selectedOpeningContainer = openingContainer;
            moveStartContainerPosition = openingContainer.transform.position;
            moveStartContainerWallStart = openingContainer.WallStart;
            moveStartContainerWallEnd = openingContainer.WallEnd;
            dragSelectedStartPoint = openingContainer.WallStart;
            dragSelectedEndPoint = openingContainer.WallEnd;
            dragSelectedStartPoint.y = dragPlaneHeight;
            dragSelectedEndPoint.y = dragPlaneHeight;
            dragSelectedStartVertexId = openingContainer.OuterStartVertexId;
            dragSelectedEndVertexId = openingContainer.OuterEndVertexId;

            if (wallOpeningPlacementManager != null)
            {
                moveStartOpeningLayoutSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(openingContainer);
                hasMoveStartOpeningLayoutSnapshot = true;
            }

            if (wallRoot != null)
            {
                List<Wall> walls = GetRootWalls();
                for (int i = 0; i < walls.Count; i++)
                {
                    Wall wall = walls[i];
                    if (wall == null || wall.GetComponentInParent<WallOpeningContainer>() == openingContainer)
                    {
                        continue;
                    }

                    bool sharesStartVertex = dragSelectedStartVertexId > 0 &&
                        (wall.StartVertexId == dragSelectedStartVertexId || wall.EndVertexId == dragSelectedStartVertexId);
                    bool sharesEndVertex = dragSelectedEndVertexId > 0 &&
                        (wall.StartVertexId == dragSelectedEndVertexId || wall.EndVertexId == dragSelectedEndVertexId);
                    bool sharesByProximity =
                        WallGeometryService.IsNearXZ(wall.Data.startPoint, dragSelectedStartPoint, connectedEndpointThreshold) ||
                        WallGeometryService.IsNearXZ(wall.Data.startPoint, dragSelectedEndPoint, connectedEndpointThreshold) ||
                        WallGeometryService.IsNearXZ(wall.Data.endPoint, dragSelectedStartPoint, connectedEndpointThreshold) ||
                        WallGeometryService.IsNearXZ(wall.Data.endPoint, dragSelectedEndPoint, connectedEndpointThreshold);

                    if (!sharesStartVertex && !sharesEndVertex && !sharesByProximity)
                    {
                        continue;
                    }

                    dragAffectedWalls.Add(wall);
                    moveStartSnapshots[wall.gameObject] = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject);
                    moveStartEndpointSnapshots[wall.gameObject] = new WallGeometryService.WallEndpointState
                    {
                        start = wall.Data.startPoint,
                        end = wall.Data.endPoint,
                    };
                }
            }

            WallHierarchyUtility.CollectWalls(openingContainer.transform, cachedWalls, true);
            for (int i = 0; i < cachedWalls.Count; i++)
            {
                Wall wall = cachedWalls[i];
                if (wall == null)
                {
                    continue;
                }

                moveStartEndpointSnapshots[wall.gameObject] = new WallGeometryService.WallEndpointState
                {
                    start = wall.Data.startPoint,
                    end = wall.Data.endPoint,
                };
            }

            return;
        }

        if (wallRoot == null)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            bool sharesStartVertex = dragSelectedStartVertexId > 0 &&
                (wall.StartVertexId == dragSelectedStartVertexId || wall.EndVertexId == dragSelectedStartVertexId);
            bool sharesEndVertex = dragSelectedEndVertexId > 0 &&
                (wall.StartVertexId == dragSelectedEndVertexId || wall.EndVertexId == dragSelectedEndVertexId);
            bool sharesByProximity =
                WallGeometryService.IsNearXZ(wall.Data.startPoint, dragSelectedStartPoint, connectedEndpointThreshold) ||
                WallGeometryService.IsNearXZ(wall.Data.startPoint, dragSelectedEndPoint, connectedEndpointThreshold) ||
                WallGeometryService.IsNearXZ(wall.Data.endPoint, dragSelectedStartPoint, connectedEndpointThreshold) ||
                WallGeometryService.IsNearXZ(wall.Data.endPoint, dragSelectedEndPoint, connectedEndpointThreshold);

            if (!sharesStartVertex && !sharesEndVertex && !sharesByProximity && wall.gameObject != selectedWall)
            {
                continue;
            }

            WallOpeningContainer connectedContainer = wall.GetComponentInParent<WallOpeningContainer>();
            if (connectedContainer != null)
            {
                if (!dragAffectedOpeningContainers.Contains(connectedContainer))
                {
                    dragAffectedOpeningContainers.Add(connectedContainer);
                    if (wallOpeningPlacementManager != null)
                    {
                        moveStartConnectedOpeningSnapshots[connectedContainer] =
                            wallOpeningPlacementManager.CaptureLayoutSnapshot(connectedContainer);
                    }
                }

                continue;
            }

            dragAffectedWalls.Add(wall);
            moveStartSnapshots[wall.gameObject] = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject);
            moveStartEndpointSnapshots[wall.gameObject] = new WallGeometryService.WallEndpointState
            {
                start = wall.Data.startPoint,
                end = wall.Data.endPoint,
            };
        }
    }

    private void ApplyConnectedWallDrag(Vector3 translationDelta, Vector3 selectedTargetPosition)
    {
        if (selectedWall == null)
        {
            return;
        }

        if (selectedOpeningContainer != null)
        {
            ApplyOpeningContainerDrag(translationDelta);
            return;
        }

        if (dragAffectedWalls.Count == 0)
        {
            selectedWall.transform.position = selectedTargetPosition;
            SyncWallComponentEndpoints(selectedWall.transform);
            handleManager?.RefreshHandleVisuals();
            RoomTopologyEvents.RequestRefreshAll();
            MarkTopViewDirty();
            return;
        }

        Vector3 movedStartPoint = dragSelectedStartPoint + translationDelta;
        Vector3 movedEndPoint = dragSelectedEndPoint + translationDelta;
        movedStartPoint.y = dragPlaneHeight;
        movedEndPoint.y = dragPlaneHeight;

        WallGeometryService.ConnectedWallMoveContext moveContext = new WallGeometryService.ConnectedWallMoveContext
        {
            selectedStartPoint = dragSelectedStartPoint,
            selectedEndPoint = dragSelectedEndPoint,
            movedStartPoint = movedStartPoint,
            movedEndPoint = movedEndPoint,
            selectedStartVertexId = dragSelectedStartVertexId,
            selectedEndVertexId = dragSelectedEndVertexId,
            endpointThreshold = connectedEndpointThreshold,
            minimumWallLength = MinimumWallLength,
        };

        WallGeometryService.ApplyConnectedWallMove(dragAffectedWalls, moveStartEndpointSnapshots, moveContext, wallLengthDisplay);

        if (wallOpeningPlacementManager != null)
        {
            for (int i = 0; i < dragAffectedOpeningContainers.Count; i++)
            {
                WallOpeningContainer container = dragAffectedOpeningContainers[i];
                if (container == null ||
                    !moveStartConnectedOpeningSnapshots.TryGetValue(container, out UndoRedoManager.OpeningLayoutSnapshot snapshot))
                {
                    continue;
                }

                Vector3 nextStart = WallGeometryService.ResolveDraggedEndpoint(container.OuterStartVertexId, snapshot.wallStart, moveContext);
                Vector3 nextEnd = WallGeometryService.ResolveDraggedEndpoint(container.OuterEndVertexId, snapshot.wallEnd, moveContext);
                nextStart.y = dragPlaneHeight;
                nextEnd.y = dragPlaneHeight;
                wallOpeningPlacementManager.ApplyContainerSpanFromExternalDrag(container, nextStart, nextEnd, snapshot);
            }
        }

        handleManager?.RefreshHandleVisuals();
        RoomTopologyEvents.RequestRefreshAll();
        MarkTopViewDirty();
    }

    private void ApplyOpeningContainerDrag(Vector3 translationDelta)
    {
        if (selectedOpeningContainer == null)
        {
            return;
        }

        selectedOpeningContainer.transform.position = moveStartContainerPosition + translationDelta;
        selectedOpeningContainer.SetWallSpan(moveStartContainerWallStart + translationDelta, moveStartContainerWallEnd + translationDelta);

        WallHierarchyUtility.CollectWalls(selectedOpeningContainer.transform, cachedWalls, true);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (!moveStartEndpointSnapshots.TryGetValue(wall.gameObject, out WallGeometryService.WallEndpointState state))
            {
                continue;
            }

            wall.CopyDataFrom(new WallData(
                state.start + translationDelta,
                state.end + translationDelta,
                wall.Data.thickness,
                wall.Data.height,
                wall.Data.centerY));
            wall.RefreshLengthDisplay(wallLengthDisplay, false);
        }

        if (dragAffectedWalls.Count == 0)
        {
            handleManager?.RefreshHandleVisuals();
            RoomTopologyEvents.RequestRefreshAll();
            MarkTopViewDirty();
            return;
        }

        Vector3 movedStartPoint = dragSelectedStartPoint + translationDelta;
        Vector3 movedEndPoint = dragSelectedEndPoint + translationDelta;
        movedStartPoint.y = dragPlaneHeight;
        movedEndPoint.y = dragPlaneHeight;

        WallGeometryService.ConnectedWallMoveContext moveContext = new WallGeometryService.ConnectedWallMoveContext
        {
            selectedStartPoint = dragSelectedStartPoint,
            selectedEndPoint = dragSelectedEndPoint,
            movedStartPoint = movedStartPoint,
            movedEndPoint = movedEndPoint,
            selectedStartVertexId = dragSelectedStartVertexId,
            selectedEndVertexId = dragSelectedEndVertexId,
            endpointThreshold = connectedEndpointThreshold,
            minimumWallLength = MinimumWallLength,
        };

        WallGeometryService.ApplyConnectedWallMove(dragAffectedWalls, moveStartEndpointSnapshots, moveContext, wallLengthDisplay);
        handleManager?.RefreshHandleVisuals();
        RoomTopologyEvents.RequestRefreshAll();
        MarkTopViewDirty();
    }

    private void SyncAllWallComponentEndpoints()
    {
        WallGeometryService.SyncWallsFromTransform(wallRoot, dragPlaneHeight);
    }

    private Vector3 ClampWallCenterPositionInsideBounds(Transform wallTransform, Vector3 centerPosition)
    {
        if (wallTransform == null)
        {
            return centerPosition;
        }

        Wall wall = wallTransform.GetComponent<Wall>();
        if (wall == null)
        {
            return centerPosition;
        }

        wall.GetEndpointsForCenterPosition(centerPosition, dragPlaneHeight, out Vector3 start, out Vector3 end);

        float minX = Mathf.Min(start.x, end.x);
        float maxX = Mathf.Max(start.x, end.x);
        float minZ = Mathf.Min(start.z, end.z);
        float maxZ = Mathf.Max(start.z, end.z);

        Vector3 offset = Vector3.zero;

        if (minX < gridBounds.min.x)
        {
            offset.x += gridBounds.min.x - minX;
        }
        else if (maxX > gridBounds.max.x)
        {
            offset.x += gridBounds.max.x - maxX;
        }

        if (minZ < gridBounds.min.z)
        {
            offset.z += gridBounds.min.z - minZ;
        }
        else if (maxZ > gridBounds.max.z)
        {
            offset.z += gridBounds.max.z - maxZ;
        }

        return centerPosition + offset;
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        Transform wallRootTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        if (wallRootTransform != null)
        {
            wallRoot = wallRootTransform;
        }
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref drawManager);
        LayerUtility.ResolveObject(ref handleManager);
        LayerUtility.ResolveObject(ref snapManager);
        LayerUtility.ResolveObject(ref wallLengthDisplay);
        LayerUtility.ResolveObject(ref undoRedoManager);
        LayerUtility.ResolveObject(ref modeManager);
        LayerUtility.ResolveObject(ref wallOpeningPlacementManager);
        LayerUtility.ResolveObject(ref roomManager);
    }

    private bool TryGetPointerFrame(out EditorPointerFrame pointerFrame)
    {
        return PointerInputFrameUtility.TryBuildPointerFrame(inputProvider, out pointerFrame);
    }

    private void RefreshDragPlane()
    {
        hasDragPlane = false;
        hasGridBounds = false;
        float planeY = 0f;

        if (grid != null)
        {
            if (grid.TryGetComponent(out Collider gridCollider))
            {
                planeY = gridCollider.bounds.center.y;
                hasDragPlane = true;
                gridBounds = gridCollider.bounds;
                hasGridBounds = true;
            }
            else if (grid.TryGetComponent(out Renderer gridRenderer))
            {
                planeY = gridRenderer.bounds.center.y;
                hasDragPlane = true;
                gridBounds = gridRenderer.bounds;
                hasGridBounds = true;
            }
            else
            {
                planeY = grid.transform.position.y;
                hasDragPlane = true;
            }
        }

        if (!hasDragPlane)
        {
            planeY = 0f;
            hasDragPlane = true;
        }

        dragPlaneHeight = planeY;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
    }

    private void EnsureMultiSelectBoxVisual()
    {
        if (multiSelectBoxObject != null)
        {
            return;
        }

        EnsureCachedResources();
        multiSelectBoxObject = new GameObject("WallMultiSelectBox", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        multiSelectBoxObject.name = "WallMultiSelectBox";
        multiSelectBoxObject.layer = 2;
        multiSelectBoxObject.transform.SetParent(transform, false);

        MeshFilter filter = multiSelectBoxObject.GetComponent<MeshFilter>();
        if (filter != null)
        {
            filter.sharedMesh = cachedCubeMesh;
        }

        multiSelectBoxCollider = multiSelectBoxObject.GetComponent<BoxCollider>();
        if (multiSelectBoxCollider != null)
        {
            multiSelectBoxCollider.isTrigger = true;
        }

        MeshRenderer renderer = multiSelectBoxObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            multiSelectBoxMaterial = CreateTransparentMaterial(multiSelectBoxColor);
            renderer.sharedMaterial = multiSelectBoxMaterial;
        }

        multiSelectBoxObject.SetActive(false);
    }

    private void UpdateMultiSelectBox(Vector3 startPoint, Vector3 endPoint)
    {
        EnsureMultiSelectBoxVisual();
        if (multiSelectBoxObject == null)
        {
            return;
        }

        Vector3 center = (startPoint + endPoint) * 0.5f;
        center.y = dragPlaneHeight + multiSelectBoxHeight * 0.5f;

        multiSelectBoxObject.transform.position = center;
        multiSelectBoxObject.transform.rotation = Quaternion.identity;
        multiSelectBoxObject.transform.localScale = new Vector3(
            Mathf.Max(0.01f, Mathf.Abs(endPoint.x - startPoint.x)),
            Mathf.Max(0.01f, multiSelectBoxHeight),
            Mathf.Max(0.01f, Mathf.Abs(endPoint.z - startPoint.z)));
    }

    private void ShowMultiSelectBox()
    {
        EnsureMultiSelectBoxVisual();
        if (multiSelectBoxObject != null)
        {
            multiSelectBoxObject.SetActive(true);
        }
    }

    private void HideMultiSelectBox()
    {
        if (multiSelectBoxObject != null)
        {
            multiSelectBoxObject.SetActive(false);
        }
    }

    private Material CreateTransparentMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            color = color,
        };

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return material;
    }

    private void EnsureCachedResources()
    {
        if (cachedCubeMesh != null)
        {
            return;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter filter = cube.GetComponent<MeshFilter>();
        if (filter != null)
        {
            cachedCubeMesh = filter.sharedMesh;
        }

        Destroy(cube);
    }

    private void ResetDragState()
    {
        pendingWallDrag = false;
        isDraggingWall = false;
        moveStartSnapshots.Clear();
        moveStartEndpointSnapshots.Clear();
        dragAffectedWalls.Clear();
        dragAffectedOpeningContainers.Clear();
        moveStartConnectedOpeningSnapshots.Clear();
        dragSelectedStartVertexId = 0;
        dragSelectedEndVertexId = 0;
        selectedOpeningContainer = null;
        hasMoveStartOpeningLayoutSnapshot = false;
    }

    private void TryApplyHandleSnapToMovedWall(Transform wallTransform, ref Vector3 targetCenterPosition)
    {
        if (wallTransform == null || handleManager == null || snapManager == null)
        {
            return;
        }

        moveSnapCandidates.Clear();
        snapManager.CollectNearbyHandleSnapCandidates(
            targetCenterPosition,
            moveSnapCandidates,
            wallRoot,
            wallTransform.GetComponent<Wall>());
        if (moveSnapCandidates.Count == 0)
        {
            return;
        }

        Wall wall = wallTransform.GetComponent<Wall>();
        if (wall == null)
        {
            return;
        }

        wall.GetEndpointsForCenterPosition(targetCenterPosition, dragPlaneHeight, out Vector3 startPoint, out Vector3 endPoint);

        bool hasStartSnap = snapManager.TryGetClosestHandleSnapPoint(startPoint, moveSnapCandidates, mainCamera, out Vector3 startSnapPoint);
        bool hasEndSnap = snapManager.TryGetClosestHandleSnapPoint(endPoint, moveSnapCandidates, mainCamera, out Vector3 endSnapPoint);

        if (!hasStartSnap && !hasEndSnap)
        {
            return;
        }

        Vector3 bestOffset = Vector3.zero;
        float bestOffsetSqr = float.MaxValue;

        if (hasStartSnap)
        {
            Vector3 startOffset = new Vector3(startSnapPoint.x - startPoint.x, 0f, startSnapPoint.z - startPoint.z);
            float startOffsetSqr = startOffset.sqrMagnitude;
            if (startOffsetSqr < bestOffsetSqr)
            {
                bestOffset = startOffset;
                bestOffsetSqr = startOffsetSqr;
            }
        }

        if (hasEndSnap)
        {
            Vector3 endOffset = new Vector3(endSnapPoint.x - endPoint.x, 0f, endSnapPoint.z - endPoint.z);
            float endOffsetSqr = endOffset.sqrMagnitude;
            if (endOffsetSqr < bestOffsetSqr)
            {
                bestOffset = endOffset;
            }
        }

        targetCenterPosition += bestOffset;
    }

    private void FinalizeMoveIfNeeded()
    {
        if (!isDraggingWall || undoRedoManager == null)
        {
            return;
        }

        BuildMoveOpeningChangeRecords();
        BuildMoveWallStateChangeRecords();

        if (moveStateChangeRecords.Count > 0 || moveOpeningChangeRecords.Count > 0)
        {
            undoRedoManager.ExecuteCommand(
                new WallSelectionMoveCommand(moveStateChangeRecords, moveOpeningChangeRecords),
                alreadyExecuted: true);
        }
    }

    private void BuildMoveOpeningChangeRecords()
    {
        moveOpeningChangeRecords.Clear();
        if (wallOpeningPlacementManager == null)
        {
            return;
        }

        if (selectedOpeningContainer != null && hasMoveStartOpeningLayoutSnapshot)
        {
            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(selectedOpeningContainer);
            if (UndoRedoManager.OpeningLayoutSnapshot.HasMeaningfulDelta(moveStartOpeningLayoutSnapshot, afterSnapshot))
            {
                moveOpeningChangeRecords.Add(new UndoRedoManager.OpeningLayoutChangeRecord
                {
                    before = moveStartOpeningLayoutSnapshot,
                    after = afterSnapshot,
                });
            }
        }

        foreach (KeyValuePair<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot> pair in moveStartConnectedOpeningSnapshots)
        {
            if (pair.Key == null)
            {
                continue;
            }

            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(pair.Key);
            if (!UndoRedoManager.OpeningLayoutSnapshot.HasMeaningfulDelta(pair.Value, afterSnapshot))
            {
                continue;
            }

            moveOpeningChangeRecords.Add(new UndoRedoManager.OpeningLayoutChangeRecord
            {
                before = pair.Value,
                after = afterSnapshot,
            });
        }
    }

    private void BuildMoveWallStateChangeRecords()
    {
        moveStateChangeRecords.Clear();

        if (moveStartSnapshots.Count > 0)
        {
            foreach (KeyValuePair<GameObject, UndoRedoManager.WallStateSnapshot> pair in moveStartSnapshots)
            {
                GameObject wallObject = pair.Key;
                if (wallObject == null)
                {
                    continue;
                }

                UndoRedoManager.WallStateSnapshot startSnapshot = pair.Value;
                UndoRedoManager.WallStateSnapshot endSnapshot = UndoRedoManager.WallStateSnapshot.Capture(wallObject);
                if (!UndoRedoManager.WallStateSnapshot.HasMeaningfulDelta(startSnapshot, endSnapshot))
                {
                    continue;
                }

                moveStateChangeRecords.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = startSnapshot,
                    after = endSnapshot,
                });
            }

            return;
        }

        if (selectedWall == null)
        {
            return;
        }

        UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(
            selectedWall,
            moveStartWallPosition,
            moveStartWallRotation,
            moveStartWallScale);
        UndoRedoManager.WallStateSnapshot after = UndoRedoManager.WallStateSnapshot.Capture(selectedWall);
        if (!UndoRedoManager.WallStateSnapshot.HasMeaningfulDelta(before, after))
        {
            return;
        }

        moveStateChangeRecords.Add(new UndoRedoManager.WallStateChangeRecord
        {
            before = before,
            after = after,
        });
    }

    private void OnDestroy()
    {
        UnbindHandleEvents();
        UnbindUIEvents();
        ClearAllSelectionState();

        if (multiSelectBoxObject != null)
        {
            Destroy(multiSelectBoxObject);
        }

        if (multiSelectBoxMaterial != null)
        {
            Destroy(multiSelectBoxMaterial);
            multiSelectBoxMaterial = null;
        }
    }

    private void BindHandleEvents()
    {
        if (handleManager == null)
        {
            return;
        }

        handleManager.WallHierarchyChanged -= HandleWallHierarchyChanged;
        handleManager.WallHierarchyChanged += HandleWallHierarchyChanged;
    }

    private void UnbindHandleEvents()
    {
        if (handleManager == null)
        {
            return;
        }

        handleManager.WallHierarchyChanged -= HandleWallHierarchyChanged;
    }

    private void HandleWallHierarchyChanged()
    {
        rootWallsCacheDirty = true;
    }

    private void BindUIEvents()
    {
        if (deleteSelectionButton == null)
        {
            return;
        }

        deleteSelectionButton.onClick.RemoveListener(DeleteCurrentSelection);
        deleteSelectionButton.onClick.AddListener(DeleteCurrentSelection);
    }

    private void UnbindUIEvents()
    {
        if (deleteSelectionButton == null)
        {
            return;
        }

        deleteSelectionButton.onClick.RemoveListener(DeleteCurrentSelection);
    }

    private void DeleteSelectedWalls()
    {
        if (wallOpeningPlacementManager == null)
        {
            return;
        }

        List<GameObject> selectedWalls = new List<GameObject>();
        GetSelectedWalls(selectedWalls);
        if (selectedWalls.Count == 0)
        {
            return;
        }

        List<UndoRedoManager.OpeningLayoutSnapshot> deletedLayouts = new List<UndoRedoManager.OpeningLayoutSnapshot>();
        HashSet<string> processedLayoutKeys = new HashSet<string>();
        HashSet<Wall> affectedWalls = new HashSet<Wall>();

        for (int i = 0; i < selectedWalls.Count; i++)
        {
            GameObject wallObject = selectedWalls[i];
            if (wallObject == null || !wallObject.TryGetComponent(out Wall wall))
            {
                continue;
            }

            UndoRedoManager.OpeningLayoutSnapshot snapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(wall);
            string key = snapshot.hasContainer
                ? $"container:{snapshot.layoutName}"
                : $"wall:{wallObject.GetInstanceID()}";
            if (!processedLayoutKeys.Add(key))
            {
                continue;
            }

            deletedLayouts.Add(snapshot);

            if (snapshot.hasContainer)
            {
                WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
                if (container != null)
                {
                    Wall[] containerWalls = container.GetComponentsInChildren<Wall>(true);
                    for (int j = 0; j < containerWalls.Length; j++)
                    {
                        if (containerWalls[j] != null)
                        {
                            affectedWalls.Add(containerWalls[j]);
                        }
                    }
                }
            }
            else
            {
                affectedWalls.Add(wall);
            }
        }

        List<Room> affectedRooms = new List<Room>();
        if (roomManager != null && affectedWalls.Count > 0)
        {
            List<Room> rooms = roomManager.GetAllRooms();
            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (room == null || room.WallSet == null)
                {
                    continue;
                }

                foreach (Wall wall in affectedWalls)
                {
                    if (wall != null && room.WallSet.Contains(wall))
                    {
                        affectedRooms.Add(room);
                        break;
                    }
                }
            }
        }

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordDeletedLayouts(deletedLayouts, affectedRooms);
        }

        for (int i = 0; i < affectedRooms.Count; i++)
        {
            if (affectedRooms[i] != null)
            {
                roomManager.DeleteRoom(affectedRooms[i]);
            }
        }

        for (int i = 0; i < deletedLayouts.Count; i++)
        {
            wallOpeningPlacementManager.ApplyLayoutSnapshot(default, deletedLayouts[i]);
        }

        ClearAllSelectionState();
        ResetDragState();
    }

    private List<Wall> GetRootWalls()
    {
        if (wallRoot == null)
        {
            rootWallsCache.Clear();
            return rootWallsCache;
        }

        if (!rootWallsCacheDirty)
        {
            return rootWallsCache;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, rootWallsCache);
        rootWallsCacheDirty = false;
        return rootWallsCache;
    }
}
