using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public partial class DrawManager : MonoBehaviour
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
    private float lastLeftClickTime = -1f;
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
    private IDrawManagerEditorState currentEditorState;
    private DrawManagerIdleState idleEditorState;
    private DrawManagerWallCreationState wallCreationEditorState;
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

        ResolveReferences();

        RefreshDrawingPlane();
        EnsureWallRoot();
        EnsureCachedResources();
        previewMaterial = CreateWallMaterial(previewColor, true);
        wallMaterial = CreateWallMaterial(wallColor, false);
        InitializeEditorStates();
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
        if (!isDefaultModeActive || mainCamera == null || Mouse.current == null)
        {
            return;
        }

        currentEditorState?.Tick();
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
            TransitionToState(idleEditorState);
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

        Ray mouseRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
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

        if (Mouse.current != null && EventSystem.current.IsPointerOverGameObject(Mouse.current.deviceId))
        {
            return true;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero,
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);
        return uiRaycastResults.Count > 0;
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

    private void InitializeEditorStates()
    {
        idleEditorState = new DrawManagerIdleState(this);
        wallCreationEditorState = new DrawManagerWallCreationState(this);
        TransitionToState(idleEditorState);
    }

    private void TransitionToState(IDrawManagerEditorState nextState)
    {
        if (nextState == null || ReferenceEquals(currentEditorState, nextState))
        {
            return;
        }

        currentEditorState?.Exit();
        currentEditorState = nextState;
        currentEditorState.Enter();
    }

    internal void TransitionToIdleState()
    {
        TransitionToState(idleEditorState);
    }

    internal bool IsHandleInputLockedState()
    {
        return handleManager != null && handleManager.IsDraggingHandle;
    }

    internal bool IsPointerOverUIState()
    {
        return IsPointerOverUI();
    }

    internal bool TryStartWallCreationModeState()
    {
        float currentTime = Time.unscaledTime;
        bool isDoubleClick = lastLeftClickTime >= 0f && currentTime - lastLeftClickTime <= doubleClickThreshold;
        lastLeftClickTime = currentTime;

        if (!isDoubleClick || !TryGetMouseWorldPoint(out Vector3 startPoint))
        {
            return false;
        }

        currentSegmentStart = startPoint;
        TransitionToState(wallCreationEditorState);
        return true;
    }

    internal void SetWallCreationModeActive(bool value)
    {
        isWallCreationMode = value;
    }

    internal bool IsPreviewWallEnabled()
    {
        return enablePreviewWall;
    }

    internal void EnsurePreviewWallState()
    {
        EnsurePreviewWall();
    }

    internal void UpdatePreviewWallState()
    {
        UpdatePreviewWall();
    }

    internal void CommitCurrentSegmentState()
    {
        CommitCurrentSegment();
    }

    internal void ExitWallCreationModeState()
    {
        ExitWallCreationMode();
    }

    internal HandleManager HandleManagerRef => handleManager;
    internal WallSelectionManager WallSelectionManagerRef => wallSelectionManager;

}

internal interface IDrawManagerEditorState
{
    void Enter();
    void Exit();
    void Tick();
}

internal sealed class DrawManagerIdleState : IDrawManagerEditorState
{
    private readonly DrawManager owner;

    public DrawManagerIdleState(DrawManager owner)
    {
        this.owner = owner;
    }

    public void Enter()
    {
        owner.SetWallCreationModeActive(false);
        if (owner.HandleManagerRef != null)
        {
            owner.HandleManagerRef.ClearPreviewSnappedHandle();
        }
    }

    public void Exit()
    {
    }

    public void Tick()
    {
        if (owner.IsHandleInputLockedState())
        {
            return;
        }

        if (owner.IsPointerOverUIState() || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (owner.WallSelectionManagerRef != null && owner.WallSelectionManagerRef.TryConsumeIdleLeftPress())
        {
            return;
        }

        owner.TryStartWallCreationModeState();
    }
}

internal sealed class DrawManagerWallCreationState : IDrawManagerEditorState
{
    private readonly DrawManager owner;

    public DrawManagerWallCreationState(DrawManager owner)
    {
        this.owner = owner;
    }

    public void Enter()
    {
        owner.SetWallCreationModeActive(true);
        if (owner.IsPreviewWallEnabled())
        {
            owner.EnsurePreviewWallState();
            owner.UpdatePreviewWallState();
        }
    }

    public void Exit()
    {
        owner.ExitWallCreationModeState();
    }

    public void Tick()
    {
        if (owner.IsHandleInputLockedState())
        {
            return;
        }

        bool isPointerOverUI = owner.IsPointerOverUIState();
        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            owner.TransitionToIdleState();
            return;
        }

        owner.UpdatePreviewWallState();
        if (!isPointerOverUI && Mouse.current.leftButton.wasPressedThisFrame)
        {
            owner.CommitCurrentSegmentState();
        }
    }
}
