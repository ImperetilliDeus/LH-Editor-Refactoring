using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

internal sealed class WallToolRuntime : IWallToolContext
{
    private const float MinimumWallLength = 0.01f;

    private readonly Camera mainCamera;
    private readonly GameObject grid;
    private readonly Transform wallRoot;
    private readonly SnapManager snapManager;
    private readonly WallLengthDisplay wallLengthDisplay;
    private readonly HandleManager handleManager;
    private readonly WallSelectionManager wallSelectionManager;
    private readonly UndoRedoManager undoRedoManager;
    private readonly IEditorInputProvider inputProvider;
    private readonly bool enablePreviewWall;
    private readonly float wallHeight;
    private readonly float wallThickness;
    private readonly float wallSurfaceOffset;
    private readonly Material defaultWallMaterial;
    private readonly Material wallTopMaterial;
    private readonly List<RaycastResult> uiRaycastResults;

    private readonly List<Vector3> handleSnapCandidates = new List<Vector3>();
    private readonly List<SnapManager.WallSnapSegment> wallSegmentSnapCandidates = new List<SnapManager.WallSnapSegment>();

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
    private bool ownsWallMaterial;
    private Mesh cachedCubeMesh;

    public WallToolRuntime(
        Camera mainCamera,
        GameObject grid,
        Transform wallRoot,
        SnapManager snapManager,
        WallLengthDisplay wallLengthDisplay,
        HandleManager handleManager,
        WallSelectionManager wallSelectionManager,
        UndoRedoManager undoRedoManager,
        IEditorInputProvider inputProvider,
        bool enablePreviewWall,
        float wallHeight,
        float wallThickness,
        float wallSurfaceOffset,
        Color previewColor,
        Material defaultWallMaterial,
        Color wallColor,
        Material wallTopMaterial,
        List<RaycastResult> uiRaycastResults)
    {
        this.mainCamera = mainCamera;
        this.grid = grid;
        this.wallRoot = wallRoot;
        this.snapManager = snapManager;
        this.wallLengthDisplay = wallLengthDisplay;
        this.handleManager = handleManager;
        this.wallSelectionManager = wallSelectionManager;
        this.undoRedoManager = undoRedoManager;
        this.inputProvider = inputProvider;
        this.enablePreviewWall = enablePreviewWall;
        this.wallHeight = wallHeight;
        this.wallThickness = wallThickness;
        this.wallSurfaceOffset = wallSurfaceOffset;
        this.defaultWallMaterial = defaultWallMaterial;
        this.wallTopMaterial = wallTopMaterial;
        this.uiRaycastResults = uiRaycastResults ?? new List<RaycastResult>();

        RefreshDrawingPlane();
        EnsureCachedResources();
        previewMaterial = CreateWallMaterial(previewColor, true);
        wallMaterial = defaultWallMaterial != null ? defaultWallMaterial : CreateWallMaterial(wallColor, false);
        ownsWallMaterial = defaultWallMaterial == null && wallMaterial != null;
    }

    public bool IsWallCreationMode => isWallCreationMode;
    public GameObject PreviewWall => previewWall;

    public void Dispose()
    {
        ClearPreviewWallDisplay();

        if (previewWall != null)
        {
            DestroyObject(previewWall);
            previewWall = null;
        }

        if (previewMaterial != null)
        {
            DestroyObject(previewMaterial);
            previewMaterial = null;
        }

        if (ownsWallMaterial && wallMaterial != null)
        {
            DestroyObject(wallMaterial);
            wallMaterial = null;
        }
    }

    public void DisablePreviewWall()
    {
        if (previewWall == null)
        {
            return;
        }

        ClearPreviewWallDisplay();
        Object.DestroyImmediate(previewWall);
        previewWall = null;
    }

    public bool IsPointerOverUI()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        return inputProvider != null && inputProvider.IsPointerOverUI(EventSystem.current, uiRaycastResults);
    }

    public bool IsHandleInputLocked()
    {
        return handleManager != null && handleManager.IsDraggingHandle;
    }

    public void ClearPreviewSnappedHandle()
    {
        handleManager?.ClearPreviewSnappedHandle();
    }

    public bool TryConsumeEditSelectionPress()
    {
        return wallSelectionManager != null && wallSelectionManager.TryConsumeIdleLeftPress();
    }

    public void DeleteCurrentSelection()
    {
        wallSelectionManager?.DeleteCurrentSelection();
    }

    public bool TryPrepareWallCreationStart()
    {
        if (!TryGetMouseWorldPoint(out Vector3 startPoint))
        {
            return false;
        }

        currentSegmentStart = startPoint;
        return true;
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

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (!hasDrawingPlane)
        {
            return false;
        }

        if (inputProvider == null || !inputProvider.TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        Ray mouseRay = mainCamera.ScreenPointToRay(pointerScreenPosition);
        if (!drawingPlane.Raycast(mouseRay, out float enter))
        {
            if (isWallCreationMode)
            {
                handleManager?.ClearPreviewSnappedHandle();
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

        if (snapManager == null)
        {
            return true;
        }

        Vector3 anchorPoint = isWallCreationMode ? currentSegmentStart : worldPoint;
        if (isWallCreationMode)
        {
            handleSnapCandidates.Clear();
            snapManager.CollectNearbyHandleSnapCandidates(worldPoint, handleSnapCandidates, wallRoot);

            bool hasHandleCandidate = false;
            Vector3 handleCandidatePoint = worldPoint;
            if (handleSnapCandidates.Count > 0)
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

            handleManager?.UpdatePreviewSnappedHandle(handleCandidatePoint, hasHandleCandidate);
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

        return true;
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

    private void CommitCurrentSegment()
    {
        if (!TryGetMouseWorldPoint(out Vector3 endPoint))
        {
            return;
        }

        if (!TryBuildWallSegment(currentSegmentStart, endPoint, false, out GameObject wallSegment))
        {
            return;
        }

        wallSegment.name = $"Wall_{wallSequence++:000}";
        handleManager?.RegisterWall(wallSegment);
        undoRedoManager?.RecordWallCreated(wallSegment);

        currentSegmentStart = endPoint;
        UpdatePreviewWall();
    }

    private void UpdatePreviewWall()
    {
        if (!enablePreviewWall)
        {
            return;
        }

        EnsurePreviewWall();

        if (!TryGetMouseWorldPoint(out Vector3 currentPoint))
        {
            previewWall.SetActive(false);
            EditorVisualEvents.RequestTopViewRefresh();
            return;
        }

        if (!TryBuildWallSegment(currentSegmentStart, currentPoint, true, out _))
        {
            ClearPreviewWallDisplay();
            previewWall.SetActive(false);
            EditorVisualEvents.RequestTopViewRefresh();
            return;
        }

        SetPreviewWorldRenderersEnabled(previewWall, false);
        previewWall.SetActive(true);
        EditorVisualEvents.RequestTopViewRefresh();
    }

    private void ExitWallCreationMode()
    {
        isWallCreationMode = false;
        handleManager?.ClearPreviewSnappedHandle();
        ClearPreviewWallDisplay();
        if (previewWall != null)
        {
            previewWall.SetActive(false);
            EditorVisualEvents.RequestTopViewRefresh();
        }
    }

    private bool TryBuildWallSegment(Vector3 startPoint, Vector3 endPoint, bool isPreview, out GameObject wallObject)
    {
        wallObject = isPreview ? previewWall : CreateWallObject();

        Wall wallComponent = wallObject.GetComponent<Wall>();
        bool applied = wallComponent != null &&
            wallComponent.TryApplyGeometryAndRefresh(
                startPoint,
                endPoint,
                wallThickness,
                wallHeight,
                drawingPlaneHeight + wallHeight * 0.5f + wallSurfaceOffset,
                MinimumWallLength,
                wallLengthDisplay,
                isPreview);

        if (applied)
        {
            return true;
        }

        if (!isPreview && wallObject != null)
        {
            wallComponent?.ClearLengthDisplay(wallLengthDisplay);
            handleManager?.UnregisterWall(wallObject);
            DestroyObject(wallObject);
        }
        else if (isPreview)
        {
            ClearPreviewWallDisplay();
        }

        return false;
    }

    private void EnsurePreviewWall()
    {
        if (!enablePreviewWall || previewWall != null)
        {
            return;
        }

        previewWall = CreateWallObject();
        previewWall.name = "WallPreview";

        Collider previewCollider = previewWall.GetComponent<Collider>();
        if (previewCollider != null)
        {
            DestroyObject(previewCollider);
        }

        MeshRenderer previewRenderer = previewWall.GetComponent<MeshRenderer>();
        if (previewRenderer != null && previewMaterial != null)
        {
            previewRenderer.sharedMaterial = previewMaterial;
        }

        Wall previewWallComponent = previewWall.GetComponent<Wall>();
        if (previewWallComponent != null)
        {
            previewWallComponent.SetTopMaterial(previewMaterial);
            previewWallComponent.SetTopFaceOffset(Wall.DefaultTopFaceOffset);
        }

        SetPreviewWorldRenderersEnabled(previewWall, false);
        previewWall.SetActive(false);
    }

    private static void SetPreviewWorldRenderersEnabled(GameObject previewObject, bool enabled)
    {
        if (previewObject == null)
        {
            return;
        }

        MeshRenderer[] renderers = previewObject.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = enabled;
            }
        }
    }

    private void ClearPreviewWallDisplay()
    {
        if (previewWall == null)
        {
            return;
        }

        Wall previewComponent = previewWall.GetComponent<Wall>();
        previewComponent?.ClearLengthDisplay(wallLengthDisplay);
    }

    private GameObject CreateWallObject()
    {
        EnsureCachedResources();
        return WallObjectFactory.CreateWallObject(
            "Wall",
            wallRoot,
            cachedCubeMesh,
            new WallVisualState
            {
                wallMaterial = wallMaterial,
                topMaterial = wallTopMaterial,
                topFaceOffset = Wall.DefaultTopFaceOffset,
            });
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

        DestroyObject(cube);
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }

    private static Material CreateWallMaterial(Color color, bool transparent)
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
        SetMaterialColor(material, color);

        if (!transparent)
        {
            return material;
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        SetMaterialColor(material, color);

        return material;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }
}
