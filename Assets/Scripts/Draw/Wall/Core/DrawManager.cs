using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public partial class DrawManager : MonoBehaviour, IWallToolContext
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject grid;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private SnapManager snapManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private UndoRedoManager undoRedoManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private ModeManager modeManager;

    [Header("Input")]
    [SerializeField] private float doubleClickThreshold = 0.25f;

    [Header("Wall Size")]
    [SerializeField] private float wallHeight = 22f;
    [SerializeField] private float wallThickness = 1.5f;
    [SerializeField] private float wallSurfaceOffset = 0.01f;

    [Header("Preview")]
    [SerializeField] private bool enablePreviewWall = true;
    [SerializeField] private Color previewColor = new Color(0.2f, 0.8f, 1f, 0.45f);
    [SerializeField] private Color wallColor = new Color(0.78f, 0.78f, 0.78f, 1f);
    [SerializeField] private Material wallTopMaterial;

    private const float MinimumWallLength = 0.01f;

    private Plane drawingPlane;
    private Bounds gridBounds;
    private bool hasDrawingPlane;
    private bool hasGridBounds;
    private bool isWallCreationMode;
    private float drawingPlaneHeight;
    private int wallSequence;
    private Vector3 currentSegmentStart;
    private GameObject previewWall;
    private Material previewMaterial;
    private Material wallMaterial;
    private Mesh cachedCubeMesh;
    private readonly List<Vector3> handleSnapCandidates = new List<Vector3>();
    private readonly List<SnapManager.WallSnapSegment> wallSegmentSnapCandidates = new List<SnapManager.WallSnapSegment>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private IEditorInputProvider inputProvider;
    private WallToolController toolController;
    private bool isDefaultModeActive = true;

    public bool IsWallCreationMode => isWallCreationMode;
    public GameObject PreviewWall => previewWall;

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

        RefreshDrawingPlane();
        EnsureWallRoot();
        EnsureCachedResources();
        previewMaterial = CreateWallMaterial(previewColor, true);
        wallMaterial = CreateWallMaterial(wallColor, false);
        InitializeToolController();
        BindModeEvents();
        SyncModeState();
        ValidateConfiguration();
    }

    private void OnValidate()
    {
        doubleClickThreshold = Mathf.Max(0.05f, doubleClickThreshold);
        wallHeight = Mathf.Max(0.1f, wallHeight);
        wallThickness = Mathf.Max(0.1f, wallThickness);
        wallSurfaceOffset = Mathf.Max(0f, wallSurfaceOffset);

        if (!enablePreviewWall && previewWall != null)
        {
            ClearPreviewWallDisplay();
            DestroyImmediate(previewWall);
            previewWall = null;
        }
    }

    private void Update()
    {
        if (!isDefaultModeActive || mainCamera == null || inputProvider == null || !inputProvider.IsPointerAvailable)
        {
            return;
        }

        toolController?.HandleInput(BuildToolInputFrame());
    }

    private void BindModeEvents()
    {
        if (modeManager == null)
        {
            return;
        }

        modeManager.ModeChanged -= HandleModeChanged;
        modeManager.ModeChanged += HandleModeChanged;
    }

    private void UnbindModeEvents()
    {
        if (modeManager == null)
        {
            return;
        }

        modeManager.ModeChanged -= HandleModeChanged;
    }

    private void SyncModeState()
    {
        HandleModeChanged(modeManager != null ? modeManager.CurrentMode : EditorMode.Default);
    }

    private void HandleModeChanged(EditorMode mode)
    {
        bool shouldBeActive = mode == EditorMode.Default;
        if (!shouldBeActive && isWallCreationMode)
        {
            toolController?.ActivateIdleTool();
        }

        isDefaultModeActive = shouldBeActive;
        enabled = shouldBeActive;
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        Transform wallRootTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        if (wallRootTransform == null)
        {
            wallRootTransform = new GameObject(LayerUtility.DefaultWallRootName).transform;
        }

        wallRoot = wallRootTransform;
    }

    private void RefreshDrawingPlane()
    {
        hasDrawingPlane = false;
        hasGridBounds = false;
        float planeY = 0f;

        if (grid != null)
        {
            if (grid.TryGetComponent(out Collider gridCollider))
            {
                planeY = gridCollider.bounds.center.y;
                hasDrawingPlane = true;
                gridBounds = gridCollider.bounds;
                hasGridBounds = true;
            }
            else if (grid.TryGetComponent(out Renderer gridRenderer))
            {
                planeY = gridRenderer.bounds.center.y;
                hasDrawingPlane = true;
                gridBounds = gridRenderer.bounds;
                hasGridBounds = true;
            }
            else
            {
                planeY = grid.transform.position.y;
                hasDrawingPlane = true;
            }
        }

        if (!hasDrawingPlane)
        {
            planeY = 0f;
            hasDrawingPlane = true;
        }

        drawingPlaneHeight = planeY;
        drawingPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (!hasDrawingPlane)
        {
            return false;
        }

        if (!inputProvider.TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        Ray mouseRay = mainCamera.ScreenPointToRay(pointerScreenPosition);
        if (!drawingPlane.Raycast(mouseRay, out float enter))
        {
            if (isWallCreationMode && handleManager != null)
            {
                handleManager.ClearPreviewSnappedHandle();
            }

            return false;
        }

        worldPoint = mouseRay.GetPoint(enter);
        worldPoint.y = drawingPlaneHeight;

        if (hasGridBounds)
        {
            worldPoint.x = Mathf.Clamp(worldPoint.x, gridBounds.min.x, gridBounds.max.x);
            worldPoint.z = Mathf.Clamp(worldPoint.z, gridBounds.min.z, gridBounds.max.z);
        }

        if (snapManager != null)
        {
            Vector3 anchorPoint = isWallCreationMode ? currentSegmentStart : worldPoint;
            if (isWallCreationMode)
            {
                handleSnapCandidates.Clear();
                if (snapManager != null)
                {
                    snapManager.CollectNearbyHandleSnapCandidates(worldPoint, handleSnapCandidates, wallRoot);
                }

                bool hasHandleCandidate = false;
                Vector3 handleCandidatePoint = worldPoint;
                if (snapManager != null && handleSnapCandidates.Count > 0)
                {
                    hasHandleCandidate = snapManager.TryGetClosestHandleSnapPoint(worldPoint, handleSnapCandidates, mainCamera, out handleCandidatePoint);
                }

                CollectWallSegmentSnapCandidates(worldPoint, wallSegmentSnapCandidates);
                worldPoint = snapManager.GetSnappedWallDrawPoint(
                    worldPoint,
                    anchorPoint,
                    handleSnapCandidates,
                    mainCamera,
                    wallSegmentSnapCandidates,
                    out _,
                    out _);

                if (handleManager != null)
                {
                    handleManager.UpdatePreviewSnappedHandle(handleCandidatePoint, hasHandleCandidate);
                }
            }
            else
            {
                worldPoint = snapManager.GetSnappedPoint(worldPoint, anchorPoint);
            }

            if (hasGridBounds)
            {
                worldPoint.x = Mathf.Clamp(worldPoint.x, gridBounds.min.x, gridBounds.max.x);
                worldPoint.z = Mathf.Clamp(worldPoint.z, gridBounds.min.z, gridBounds.max.z);
            }
        }

        return true;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return inputProvider != null && inputProvider.IsPointerOverUI(EventSystem.current, uiRaycastResults);
    }

    private void ResolveReferences()
    {
        if (snapManager == null)
        {
            snapManager = FindFirstObjectByType<SnapManager>();
        }

        if (wallLengthDisplay == null)
        {
            wallLengthDisplay = FindFirstObjectByType<WallLengthDisplay>();
        }

        if (handleManager == null)
        {
            handleManager = FindFirstObjectByType<HandleManager>();
        }

        if (wallSelectionManager == null)
        {
            wallSelectionManager = FindFirstObjectByType<WallSelectionManager>();
        }

        if (undoRedoManager == null)
        {
            undoRedoManager = FindFirstObjectByType<UndoRedoManager>();
        }

        if (modeManager == null)
        {
            modeManager = FindFirstObjectByType<ModeManager>();
        }
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(mainCamera != null, $"{nameof(DrawManager)} requires {nameof(mainCamera)}.", this);
        Debug.Assert(modeManager != null, $"{nameof(DrawManager)} requires {nameof(modeManager)}.", this);
        Debug.Assert(handleManager != null, $"{nameof(DrawManager)} requires {nameof(handleManager)}.", this);
    }

    private void CollectWallSegmentSnapCandidates(Vector3 aroundPoint, List<SnapManager.WallSnapSegment> segments)
    {
        if (segments == null)
        {
            return;
        }

        segments.Clear();
        if (wallRoot == null || snapManager == null)
        {
            return;
        }

        Transform previewTransform = previewWall != null ? previewWall.transform : null;
        snapManager.CollectNearbyWallSegmentSnapCandidates(
            aroundPoint,
            drawingPlaneHeight,
            MinimumWallLength,
            segments,
            wallRoot,
            wall => wall != null && wall.transform == previewTransform);
    }

    private void InitializeToolController()
    {
        toolController = new WallToolController(this, doubleClickThreshold);
    }

    private WallToolInputFrame BuildToolInputFrame()
    {
        if (!PointerInputFrameUtility.TryBuildPointerFrame(inputProvider, out EditorPointerFrame pointerFrame))
        {
            return WallToolInputFrame.Unavailable;
        }

        return new WallToolInputFrame(
            pointerFrame.ScreenPosition,
            pointerFrame.LeftPressedThisFrame,
            pointerFrame.LeftReleasedThisFrame,
            pointerFrame.LeftPressed,
            pointerFrame.RightPressedThisFrame,
            IsPointerOverUI());
    }

    private bool TryPrepareWallCreationStartInternal()
    {
        if (!TryGetMouseWorldPoint(out Vector3 startPoint))
        {
            return false;
        }

        currentSegmentStart = startPoint;
        return true;
    }

    public bool IsHandleInputLocked()
    {
        return handleManager != null && handleManager.IsDraggingHandle;
    }

    public void ClearPreviewSnappedHandle()
    {
        handleManager?.ClearPreviewSnappedHandle();
    }

    public bool TryConsumeIdleSelectionPress()
    {
        return wallSelectionManager != null && wallSelectionManager.TryConsumeIdleLeftPress();
    }

    public bool TryPrepareWallCreationStart()
    {
        return TryPrepareWallCreationStartInternal();
    }

    public void SetWallCreationModeActive(bool value)
    {
        isWallCreationMode = value;
    }

    public bool IsPreviewWallEnabled()
    {
        return enablePreviewWall;
    }

    public void EnsurePreviewWallState()
    {
        EnsurePreviewWall();
    }

    public void UpdatePreviewWallState()
    {
        UpdatePreviewWall();
    }

    public void CommitCurrentSegmentState()
    {
        CommitCurrentSegment();
    }

    public void ExitWallCreationModeState()
    {
        ExitWallCreationMode();
    }
}
