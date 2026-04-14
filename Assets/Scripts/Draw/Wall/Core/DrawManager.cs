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
        if (mainCamera == null || Mouse.current == null)
        {
            return;
        }

        if (modeManager != null && !modeManager.IsMode(EditorMode.Default))
        {
            if (isWallCreationMode)
            {
                ExitWallCreationMode();
            }

            return;
        }

        if (!isWallCreationMode && handleManager != null)
        {
            handleManager.ClearPreviewSnappedHandle();
        }

        bool isHandleInputLocked = handleManager != null && handleManager.IsDraggingHandle;
        if (isHandleInputLocked)
        {
            return;
        }

        bool isPointerOverUI = IsPointerOverUI();

        if (Mouse.current.rightButton.wasPressedThisFrame && isWallCreationMode)
        {
            ExitWallCreationMode();
            return;
        }

        if (isWallCreationMode)
        {
            UpdatePreviewWall();

            if (!isPointerOverUI && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CommitCurrentSegment();
            }
            return;
        }

        if (!isPointerOverUI && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (wallSelectionManager != null && wallSelectionManager.TryConsumeIdleLeftPress())
            {
                return;
            }

            TryEnterWallCreationMode();
        }
    }

    private void TryEnterWallCreationMode()
    {
        float currentTime = Time.unscaledTime;
        bool isDoubleClick = lastLeftClickTime >= 0f && currentTime - lastLeftClickTime <= doubleClickThreshold;
        lastLeftClickTime = currentTime;

        if (!isDoubleClick)
        {
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 startPoint))
        {
            return;
        }

        isWallCreationMode = true;
        currentSegmentStart = startPoint;

        if (enablePreviewWall)
        {
            EnsurePreviewWall();
            UpdatePreviewWall();
        }
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        Transform wallRootTransform = LayerUtility.FindTransformByName("Walls", true);
        if (wallRootTransform == null)
        {
            wallRootTransform = new GameObject("Walls").transform;
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
                if (handleManager != null)
                {
                    handleManager.CollectSnapPoints(handleSnapCandidates);
                }

                bool hasHandleCandidate = false;
                Vector3 handleCandidatePoint = worldPoint;
                if (snapManager != null && handleSnapCandidates.Count > 0)
                {
                    hasHandleCandidate = snapManager.TryGetClosestHandleSnapPoint(worldPoint, handleSnapCandidates, mainCamera, out handleCandidatePoint);
                }

                CollectWallSegmentSnapCandidates(wallSegmentSnapCandidates);
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

    private void CollectWallSegmentSnapCandidates(List<SnapManager.WallSnapSegment> segments)
    {
        if (segments == null)
        {
            return;
        }

        segments.Clear();
        if (wallRoot == null)
        {
            return;
        }

        Transform previewTransform = previewWall != null ? previewWall.transform : null;
        float planeY = drawingPlaneHeight;
        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);

        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null || wall.transform == previewTransform)
            {
                continue;
            }

            if (!wall.TryGetSnapSegment(planeY, MinimumWallLength, out SnapManager.WallSnapSegment segment))
            {
                continue;
            }

            segments.Add(segment);
        }
    }

}
