using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System.Collections.Generic;
using UnityEngine.UI;

public partial class WallSelectionManager : MonoBehaviour, IEditorModeInputHandler
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

    private readonly WallSelectionState selectionState = new WallSelectionState();
    private GameObject multiSelectBoxObject;
    private BoxCollider multiSelectBoxCollider;
    private Material multiSelectBoxMaterial;

    private bool pendingWallDrag;
    private bool isDraggingWall;
    private Vector2 pendingStartMousePosition;
    private Vector3 dragStartPoint;
    private Vector3 selectedWallStartPosition;
    private Vector3 moveStartWallPosition;
    private Quaternion moveStartWallRotation;
    private Vector3 moveStartWallScale;
    private readonly List<Vector3> moveSnapCandidates = new List<Vector3>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<Wall> rootWallsCache = new List<Wall>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private readonly WallSelectionPresentationController presentationController = new WallSelectionPresentationController();
    private readonly WallSelectionQueryService queryService = new WallSelectionQueryService();
    private readonly WallSelectionDragController dragController = new WallSelectionDragController();
    private readonly WallSelectionDragState dragState = new WallSelectionDragState();
    private readonly WallSelectionInputController inputController = new WallSelectionInputController();
    private readonly WallSelectionInputState inputState = new WallSelectionInputState();
    private readonly WallSelectionMutationService mutationService = new WallSelectionMutationService();
    private readonly WallSelectionUndoRecorder undoRecorder = new WallSelectionUndoRecorder();
    private Mesh cachedCubeMesh;
    private bool rootWallsCacheDirty = true;
    private bool isShuttingDown;
    private EditorInputFrame lastInputFrame;

    public bool IsDraggingWall => isDraggingWall;
    public GameObject SelectedWall => selectionState.SelectedWall;
    public Canvas WallSelectionCanvas => wallSelectionCanvas;
    public Camera SelectionCamera => mainCamera;
    public Color WallUINormalColor => wallUINormalColor;
    public Color WallUISelectedColor => wallUISelectedColor;
    public float WallUIThicknessPixels => wallUIThicknessPixels;
    public bool IsWallUIInteractionEnabled => modeManager != null &&
                                              modeManager.IsMode(EditorMode.DetailEdit) &&
                                              (wallOpeningPlacementManager == null || !wallOpeningPlacementManager.IsOpeningDetailMenuVisible);
    public int SelectedWallCount => selectionState.SelectedWallCount;
    public bool HasMultiWallSelection => SelectedWallCount > 1;

    public event Action<GameObject> SelectionChanged;
    public event Action SelectionSetChanged;

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
        selectionState.GetSelectedWalls(result);
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

        if (selectionState.SelectedWallCount == 0)
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

        inputProvider = EditorInputManager.Instance.InputProvider;
        ResolveReferences();

        EnsureWallRoot();
        RefreshDragPlane();
        EnsureCachedResources();
        PrepareSelectUI();
        EnsureMultiSelectBoxVisual();
        EnsureSelectionCanvas();
        BindHandleEvents();
        BindUIEvents();
        EditorInputManager.Instance.RegisterGlobalHandler(this);
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
        if (isShuttingDown)
        {
            return;
        }

        EditorMode currentMode = modeManager != null ? modeManager.CurrentMode : EditorMode.Default;
        if (currentMode == EditorMode.Default || currentMode == EditorMode.DetailEdit)
        {
            RefreshWallSelectionUIPositions();
        }
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        lastInputFrame = inputFrame;
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

        if (selectionState.DetailSelectedWalls.Count > 0)
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

        HandleDefaultModeInput(pointerFrame);
    }

    public bool TryConsumeIdleLeftPress()
    {
        EditorPointerFrame pointerFrame = PointerInputFrameUtility.BuildPointerFrame(lastInputFrame);
        if (!pointerFrame.IsAvailable)
        {
            return false;
        }

        return TryConsumeIdleLeftPress(pointerFrame);
    }

    private void HandleDefaultModeInput(EditorPointerFrame pointerFrame)
    {
        inputController.HandleDefaultModeInput(
            pointerFrame,
            selectionState.SelectedWall,
            drawManager != null && drawManager.IsWallCreationMode,
            handleManager != null && handleManager.IsDraggingHandle,
            pendingWallDrag,
            isDraggingWall,
            dragStartThresholdPixels,
            pendingStartMousePosition,
            dragStartPoint,
            selectedWallStartPosition,
            snapDraggedWallToGrid,
            screenPosition =>
            {
                bool success = TryGetMouseWorldPoint(screenPosition, out Vector3 point);
                return (success, point);
            },
            targetPosition => snapManager != null ? snapManager.GetSnappedPoint(targetPosition, targetPosition) : targetPosition,
            targetPosition =>
            {
                if (selectionState.SelectedWall == null)
                {
                    return targetPosition;
                }

                TryApplyHandleSnapToMovedWall(selectionState.SelectedWall.transform, ref targetPosition);
                return hasGridBounds
                    ? ClampWallCenterPositionInsideBounds(selectionState.SelectedWall.transform, targetPosition)
                    : targetPosition;
            },
            FinalizeMoveIfNeeded,
            ClearSingleSelection,
            ResetDragState,
            SetSelectUIVisible,
            () =>
            {
                moveStartWallPosition = selectionState.SelectedWall.transform.position;
                moveStartWallRotation = selectionState.SelectedWall.transform.rotation;
                moveStartWallScale = selectionState.SelectedWall.transform.localScale;
            },
            PrepareConnectedWallDrag,
            ApplyConnectedWallDrag,
            () => isDraggingWall = true);
    }

    internal bool TryConsumeIdleLeftPress(EditorPointerFrame pointerFrame)
    {
        return inputController.TryConsumeIdleLeftPress(
            pointerFrame,
            modeManager == null || modeManager.IsMode(EditorMode.Default),
            mainCamera == null || IsPointerOverUI(pointerFrame.ScreenPosition),
            drawManager != null && drawManager.IsWallCreationMode,
            handleManager != null && handleManager.IsDraggingHandle,
            handleManager != null && handleManager.IsPointerOverHandle(pointerFrame.ScreenPosition),
            screenPosition =>
            {
                bool success = queryService.TryGetWallFromMouseRay(mainCamera, wallRoot, screenPosition, out GameObject wall);
                return (success, wall);
            },
            screenPosition =>
            {
                bool success = TryGetMouseWorldPoint(screenPosition, out Vector3 point);
                return (success, point);
            },
            wall => SelectWall(wall, false),
            ResetDragState,
            SetSelectUIVisible,
            (mousePosition, worldPoint) =>
            {
                pendingWallDrag = true;
                pendingStartMousePosition = mousePosition;
                dragStartPoint = worldPoint;
                selectedWallStartPosition = selectionState.SelectedWall.transform.position;
            });
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
        inputController.UpdateDetailEditMode(
            pointerFrame,
            inputState,
            multiSelectDragThresholdPixels,
            FinalizeMoveIfNeeded,
            ResetDragState,
            RefreshWallSelectionUIPositions,
            ClearAllSelectionState,
            TryBeginDetailSelection,
            UpdateMultiSelectDrag,
            FinishMultiSelectDrag);
    }

    private void TryBeginDetailSelection(EditorPointerFrame pointerFrame)
    {
        if (IsPointerOverUI(pointerFrame.ScreenPosition))
        {
            return;
        }

        bool isModifierPressed = IsSelectionModifierPressed();
        if (queryService.TryGetWallFromMouseRay(mainCamera, wallRoot, pointerFrame.ScreenPosition, out GameObject hitWall))
        {
            HandleDetailWallClick(hitWall, isModifierPressed);
            return;
        }

        if (!TryGetMouseWorldPoint(pointerFrame.ScreenPosition, out Vector3 worldPoint))
        {
            return;
        }

        inputController.BeginDetailSelectionBox(
            inputState,
            pointerFrame,
            isModifierPressed,
            worldPoint,
            ShowMultiSelectBox,
            UpdateMultiSelectBox);
    }

    private void HandleDetailWallClick(GameObject wallObject, bool isModifierPressed)
    {
        CancelMultiSelectDrag();

        if (!isModifierPressed)
        {
            selectionState.ClearDetailSelection();
            SelectWall(wallObject, false);
            return;
        }

        if (wallObject == selectionState.SelectedWall)
        {
            selectionState.ClearPrimarySelection();
            RefreshSelectionVisuals();
            return;
        }

        selectionState.ToggleDetailSelection(wallObject);
        RefreshSelectionVisuals();
    }

    private void UpdateMultiSelectDrag(EditorPointerFrame pointerFrame)
    {
        inputController.UpdateMultiSelectDrag(
            inputState,
            pointerFrame,
            screenPosition =>
            {
                bool success = TryGetMouseWorldPoint(screenPosition, out Vector3 worldPoint);
                return (success, worldPoint);
            },
            UpdateMultiSelectBox,
            UpdateWallsFromMultiSelectBox);
    }

    private void FinishMultiSelectDrag()
    {
        inputController.FinishMultiSelectDrag(inputState, HideMultiSelectBox, UpdateSelectUIVisibility);
    }

    private void CancelMultiSelectDrag()
    {
        inputController.CancelMultiSelectDrag(inputState, HideMultiSelectBox);
    }

    private void UpdateWallsFromMultiSelectBox(bool additive)
    {
        if (wallOpeningPlacementManager != null && wallOpeningPlacementManager.SelectedOpening != null)
        {
            wallOpeningPlacementManager.ClearOpeningSelection();
        }

        IReadOnlyCollection<GameObject> wallsInBounds = queryService.CollectWallsInSelectionBounds(
            multiSelectBoxCollider,
            wallRoot,
            GetRootWalls());

        if (!additive)
        {
            selectionState.ClearAll();
        }

        selectionState.ApplyMultiSelection(wallsInBounds, additive);

        RefreshSelectionVisuals();
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

        if (wall == selectionState.SelectedWall)
        {
            UpdateSelectUIVisibility();
            return;
        }

        selectionState.ClearDetailSelection();
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
        dragController.PrepareDrag(
            dragState,
            selectionState.SelectedWall,
            dragPlaneHeight,
            connectedEndpointThreshold,
            wallRoot,
            GetRootWalls(),
            cachedWalls,
            wallOpeningPlacementManager,
            SyncAllWallComponentEndpoints);
    }

    private void ApplyConnectedWallDrag(Vector3 translationDelta, Vector3 selectedTargetPosition)
    {
        dragController.ApplyDrag(
            dragState,
            selectionState.SelectedWall,
            translationDelta,
            selectedTargetPosition,
            dragPlaneHeight,
            connectedEndpointThreshold,
            MinimumWallLength,
            wallLengthDisplay,
            handleManager,
            wallOpeningPlacementManager,
            cachedWalls,
            SyncWallComponentEndpoints,
            MarkTopViewDirty);
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

        wallRoot = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
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

    private void RefreshDragPlane()
    {
        float planeY = 0f;
        Bounds bounds = default;
        hasGridBounds = false;

        if (grid != null)
        {
            if (grid.TryGetComponent(out Collider gridCollider))
            {
                planeY = gridCollider.bounds.center.y;
                bounds = gridCollider.bounds;
                hasGridBounds = true;
            }
            else if (grid.TryGetComponent(out Renderer gridRenderer))
            {
                planeY = gridRenderer.bounds.center.y;
                bounds = gridRenderer.bounds;
                hasGridBounds = true;
            }
            else
            {
                planeY = grid.transform.position.y;
            }
        }

        dragPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
        dragPlaneHeight = planeY;
        gridBounds = bounds;
        hasDragPlane = true;
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
        dragState.Reset();
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
        undoRecorder.FinalizeMove(
            isDraggingWall,
            undoRedoManager,
            dragState,
            wallOpeningPlacementManager,
            selectionState.SelectedWall,
            moveStartWallPosition,
            moveStartWallRotation,
            moveStartWallScale);
    }

    private void OnDestroy()
    {
        isShuttingDown = true;

        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterGlobalHandler(this);
        }

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
        List<GameObject> selectedWalls = new List<GameObject>();
        GetSelectedWalls(selectedWalls);
        if (!mutationService.TryDeleteSelectedWalls(
                selectedWalls,
                wallOpeningPlacementManager,
                roomManager,
                undoRedoManager))
        {
            return;
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
