using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using System.Collections.Generic;
using UnityEngine.UI;

public partial class WallSelectionManager : MonoBehaviour, IEditorModeInputHandler
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private DrawManager drawManager;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;
    [SerializeField] private RoomManager roomManager;

    [Header("Selection Visual")]
    [SerializeField] private Canvas wallSelectionCanvas;
    [SerializeField] private Color wallUINormalColor = Color.clear;
    [SerializeField] private Color wallUISelectedColor = Color.clear;
    [SerializeField] private float wallUIThicknessPixels = 16f;

    [Header("Preview Wall UI")]
    [SerializeField, Min(0f)] private float previewWallUIEndCapMinSize = 3f;
    [SerializeField, Min(0f)] private float previewWallUIEndCapPadding = 1f;
    [SerializeField, Min(0f)] private float previewWallUIEndCapSizeMultiplier = 0.6f;

    [Header("Selection UI")]
    [FormerlySerializedAs("dragUIObject")]
    [SerializeField] private GameObject selectUIObject;
    [SerializeField] private Button deleteSelectionButton;

    private IEditorInputProvider inputProvider;
    private readonly WallSelectionState selectionState = new WallSelectionState();
    private readonly List<Transform> logicalWallRoots = new List<Transform>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private readonly WallSelectionPresentationController presentationController = new WallSelectionPresentationController();
    private bool isShuttingDown;
    private EditorInputFrame lastInputFrame;

    public GameObject SelectedWall => selectionState.SelectedWall;
    public Canvas WallSelectionCanvas => wallSelectionCanvas;
    public Camera SelectionCamera => mainCamera;
    public Color WallUINormalColor => wallUINormalColor;
    public Color WallUISelectedColor => wallUISelectedColor;
    public float WallUIThicknessPixels => wallUIThicknessPixels;
    public float PreviewWallUIEndCapMinSize => previewWallUIEndCapMinSize;
    public float PreviewWallUIEndCapPadding => previewWallUIEndCapPadding;
    public float PreviewWallUIEndCapSizeMultiplier => previewWallUIEndCapSizeMultiplier;
    public bool IsWallUIInteractionEnabled => modeManager != null &&
                                              modeManager.IsMode(EditorMode.DetailEdit) &&
                                              (wallOpeningPlacementManager == null || !wallOpeningPlacementManager.IsOpeningDetailMenuVisible);

    public event Action<GameObject> SelectionChanged;
    public event Action SelectionSetChanged;

    public void SetSelectedWall(GameObject wall)
    {
        SelectWall(wall, false);
    }

    public bool IsSelected(WallOpeningContainer container)
    {
        return selectionState.IsContainerSelected(container);
    }

    public WallOpeningContainer GetSelectedContainer()
    {
        return selectionState.SelectedContainer;
    }

    public void SetSelectedWallPreservingOpeningSelection(GameObject wall)
    {
        SelectWall(wall, true);
    }

    public void GetSelectedWalls(List<GameObject> result)
    {
        selectionState.GetSelectedWalls(result);
    }

    public void ClearSelectionForSceneReset()
    {
        selectionState.ClearAll();
        RefreshSelectionVisuals();
    }

    public void HandleWallUIClick(GameObject wallObject)
    {
        if (wallObject == null || !IsWallUIInteractionEnabled)
        {
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
            ClearDeleteButtonSelection();
            return;
        }

        if (!selectionState.HasSelection)
        {
            return;
        }

        DeleteSelectedWalls();
        ClearDeleteButtonSelection();
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

        inputProvider = EditorInputManager.Instance.InputProvider;
        ResolveReferences();

        EnsureWallRoot();
        PrepareSelectUI();
        EnsureSelectionCanvas();
        BindHandleEvents();
        BindSelectionUiRefreshEvents();
        BindUIEvents();
        EditorInputManager.Instance.RegisterGlobalHandler(this);
    }

    private void OnValidate()
    {
        wallUIThicknessPixels = Mathf.Max(4f, wallUIThicknessPixels);
    }

    private void Update()
    {
        if (isShuttingDown)
        {
            return;
        }

        EditorMode currentMode = modeManager != null ? modeManager.CurrentMode : EditorMode.Default;
        if (currentMode == EditorMode.Default || currentMode == EditorMode.DetailEdit)
        {
            if (ShouldRefreshSelectionUiPositions())
            {
                RefreshWallSelectionUIPositions();
            }
        }
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        lastInputFrame = inputFrame;
        if (IsPerspectiveViewActive())
        {
            return;
        }

        EditorMode currentMode = modeManager != null ? modeManager.CurrentMode : EditorMode.Default;
        EditorPointerFrame pointerFrame = PointerInputFrameUtility.BuildPointerFrame(inputFrame);

        if (currentMode == EditorMode.DetailEdit)
        {
            if (!pointerFrame.IsAvailable)
            {
                return;
            }

            UpdateDetailEditMode(pointerFrame);
            return;
        }

        if (currentMode != EditorMode.Default)
        {
            ClearAllSelectionState();
            return;
        }

        HandleDefaultModeInput(pointerFrame);
    }

    // Default mode never selects walls, but a left press on a wall is still consumed so it cannot start wall drawing.
    public bool IsIdleLeftPressOnWall()
    {
        EditorPointerFrame pointerFrame = PointerInputFrameUtility.BuildPointerFrame(lastInputFrame);
        if (!pointerFrame.IsAvailable ||
            !pointerFrame.LeftPressedThisFrame ||
            pointerFrame.MiddlePressed ||
            (modeManager != null && !modeManager.IsMode(EditorMode.Default)) ||
            mainCamera == null ||
            IsPointerOverUI(pointerFrame.ScreenPosition) ||
            (drawManager != null && drawManager.IsWallCreationMode) ||
            (handleManager != null && (handleManager.IsDraggingHandle || handleManager.IsPointerOverHandle(pointerFrame.ScreenPosition))))
        {
            return false;
        }

        return TryGetWallFromMouseRay(mainCamera, wallRoot, pointerFrame.ScreenPosition, out _);
    }

    private void HandleDefaultModeInput(EditorPointerFrame pointerFrame)
    {
        if (!pointerFrame.IsAvailable || pointerFrame.MiddlePressed)
        {
            return;
        }

        SetSelectUIVisible(false);

        if (selectionState.SelectedWall != null &&
            pointerFrame.RightPressedThisFrame &&
            (drawManager == null || !drawManager.IsWallCreationMode))
        {
            ClearSingleSelection();
        }
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
        RefreshWallSelectionUIPositions();

        if (pointerFrame.MiddlePressed)
        {
            return;
        }

        if (pointerFrame.RightPressedThisFrame)
        {
            ClearAllSelectionState();
            return;
        }

        if (pointerFrame.LeftPressedThisFrame)
        {
            TrySelectWallUnderPointer(pointerFrame);
        }
    }

    private void TrySelectWallUnderPointer(EditorPointerFrame pointerFrame)
    {
        if (IsPointerOverUI(pointerFrame.ScreenPosition) ||
            (handleManager != null && handleManager.IsPointerOverHandle(pointerFrame.ScreenPosition)))
        {
            return;
        }

        if (TryGetWallFromMouseRay(mainCamera, wallRoot, pointerFrame.ScreenPosition, out GameObject hitWall))
        {
            SelectWall(hitWall, false);
        }
    }

    private void SelectWall(GameObject wall, bool preserveOpeningSelection)
    {
        if (wall == null)
        {
            return;
        }

        if (wall.TryGetComponent(out WallOpeningContainer container))
        {
            selectionState.SetPrimarySelection(container);
            RefreshSelectionVisuals();
            return;
        }

        if (!preserveOpeningSelection &&
            wallOpeningPlacementManager != null &&
            wallOpeningPlacementManager.IsOpeningDetailMenuVisible)
        {
            wallOpeningPlacementManager.ClearOpeningSelection();
        }

        if (wall == selectionState.SelectedWall)
        {
            UpdateSelectUIVisibility();
            return;
        }

        selectionState.ClearAll();
        selectionState.SelectedWall = wall;
        RefreshSelectionVisuals();
    }

    private void ClearSingleSelection()
    {
        selectionState.ClearPrimarySelection();
        RefreshSelectionVisuals();
    }

    private void ClearAllSelectionState()
    {
        selectionState.ClearAll();
        RefreshSelectionVisuals();
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        wallRoot = LayerUtility.FindWallRoot(true);
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref drawManager);
        LayerUtility.ResolveObject(ref handleManager);
        LayerUtility.ResolveObject(ref wallLengthDisplay);
        LayerUtility.ResolveObject(ref modeManager);
        LayerUtility.ResolveObject(ref viewModeManager);
        LayerUtility.ResolveObject(ref wallOpeningPlacementManager);
        LayerUtility.ResolveObject(ref roomManager);
    }

    private bool IsPerspectiveViewActive()
    {
        return viewModeManager != null && viewModeManager.CurrentViewMode == EditorViewMode.Perspective3D;
    }

    private void OnDestroy()
    {
        isShuttingDown = true;

        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterGlobalHandler(this);
        }

        UnbindHandleEvents();
        UnbindSelectionUiRefreshEvents();
        UnbindUIEvents();
        ClearAllSelectionState();
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
        selectionUiPositionsDirty = true;
        RefreshSelectionVisuals();
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
        List<GameObject> selectedWalls = new List<GameObject>();
        GetSelectedWalls(selectedWalls);
        if (!TryDeleteSelectedWalls(selectedWalls))
        {
            return;
        }

        ClearAllSelectionState();
    }

    private void ClearDeleteButtonSelection()
    {
        if (deleteSelectionButton == null || EventSystem.current == null)
        {
            return;
        }

        GameObject deleteButtonObject = deleteSelectionButton.gameObject;
        if (EventSystem.current.currentSelectedGameObject == deleteButtonObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private bool TryDeleteSelectedWalls(List<GameObject> selectedWalls)
    {
        if (wallOpeningPlacementManager == null || selectedWalls == null || selectedWalls.Count == 0)
        {
            return false;
        }

        List<OpeningLayoutSnapshot> deletedLayouts = new List<OpeningLayoutSnapshot>();
        HashSet<string> processedLayoutKeys = new HashSet<string>();
        HashSet<Wall> affectedWalls = new HashSet<Wall>();

        for (int i = 0; i < selectedWalls.Count; i++)
        {
            GameObject wallObject = selectedWalls[i];
            if (wallObject == null || !wallObject.TryGetComponent(out Wall wall))
            {
                continue;
            }

            OpeningLayoutSnapshot snapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(wall);
            string key = snapshot.hasContainer
                ? $"container:{snapshot.layoutName}"
                : $"wall:{wallObject.GetInstanceID()}";
            if (!processedLayoutKeys.Add(key))
            {
                continue;
            }

            deletedLayouts.Add(snapshot);
            CollectAffectedWallsForDeletion(wall, snapshot, affectedWalls);
        }

        List<Room> affectedRooms = CollectAffectedRoomsForDeletion(affectedWalls);

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

        return deletedLayouts.Count > 0 || affectedRooms.Count > 0;
    }

    private static void CollectAffectedWallsForDeletion(
        Wall wall,
        OpeningLayoutSnapshot snapshot,
        HashSet<Wall> affectedWalls)
    {
        if (wall == null)
        {
            return;
        }

        if (!snapshot.hasContainer)
        {
            affectedWalls.Add(wall);
            return;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return;
        }

        Wall[] containerWalls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < containerWalls.Length; i++)
        {
            if (containerWalls[i] != null)
            {
                affectedWalls.Add(containerWalls[i]);
            }
        }
    }

    private List<Room> CollectAffectedRoomsForDeletion(HashSet<Wall> affectedWalls)
    {
        List<Room> affectedRooms = new List<Room>();
        if (roomManager == null || affectedWalls == null || affectedWalls.Count == 0)
        {
            return affectedRooms;
        }

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

        return affectedRooms;
    }

    private bool TryGetWallFromMouseRay(
        Camera camera,
        Transform currentWallRoot,
        Vector2 pointerScreenPosition,
        out GameObject wall)
    {
        wall = null;
        if (camera == null)
        {
            return false;
        }

        Ray ray = EditorScreenCoordinateUtility.ScreenPointToRay(camera, pointerScreenPosition);
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

        GameObject wallObject = ResolveWallObject(hitObject, currentWallRoot);
        if (wallObject == null)
        {
            return false;
        }

        wall = wallObject;
        return true;
    }

    private bool ShouldDisplaySelectionProxyInternal(Wall wall)
    {
        if (wall == null)
        {
            return false;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        return container == null || GetRepresentativeWallForContainerInternal(container) == wall;
    }

    private Wall GetRepresentativeWallForContainerInternal(WallOpeningContainer container)
    {
        if (container == null)
        {
            return null;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        Wall representative = null;
        float bestLengthSqr = float.MinValue;

        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null || WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                continue;
            }

            float lengthSqr = (wall.Data.endPoint - wall.Data.startPoint).sqrMagnitude;
            if (lengthSqr <= bestLengthSqr)
            {
                continue;
            }

            bestLengthSqr = lengthSqr;
            representative = wall;
        }

        return representative;
    }

    private bool IsWallOrContainerSelectedInternal(Wall wall)
    {
        if (wall == null)
        {
            return false;
        }

        if (selectionState.IsSelected(wall.gameObject))
        {
            return true;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        return selectionState.SelectedWall != null && selectionState.SelectedWall.transform.IsChildOf(container.transform);
    }

    private bool IsWallObject(GameObject candidate, Transform currentWallRoot)
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

        if (currentWallRoot == null)
        {
            return true;
        }

        return candidate.transform.IsChildOf(currentWallRoot);
    }

    private GameObject ResolveWallObject(GameObject candidate, Transform currentWallRoot)
    {
        if (candidate == null)
        {
            return null;
        }

        Wall wall = candidate.GetComponentInParent<Wall>();
        if (wall != null && IsWallObject(wall.gameObject, currentWallRoot))
        {
            return wall.gameObject;
        }

        return IsWallObject(candidate, currentWallRoot) ? candidate : null;
    }
}
