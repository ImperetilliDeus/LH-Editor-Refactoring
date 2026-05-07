using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed partial class RoomCreateManager : MonoBehaviour, IEditorModeInputHandler
{
    private enum RoomCreationShapeMode
    {
        RectangleDrag = 0,
        PolygonDraw = 1,
    }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject grid;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private SnapManager snapManager;
    [SerializeField] private HandleManager wallHandleManager;
    [SerializeField] private RoomHandleManager roomHandleManager;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private UndoRedoManager undoRedoManager;

    [Header("Input")]
    [SerializeField] private RoomCreationShapeMode creationShapeMode = RoomCreationShapeMode.RectangleDrag;
    [SerializeField] private float minimumRoomWidth = 0.1f;
    [SerializeField] private float minimumRoomHeight = 0.1f;
    [SerializeField] private float clickToSelectThresholdPixels = 6f;
    [SerializeField] private float polygonCloseDistance = 0.2f;
    [SerializeField] private float polygonCloseDistancePixels = 18f;
    [SerializeField] private float minimumPolygonEdgeLength = 0.1f;

    [Header("Auto Wall Match")]
    [SerializeField] private float autoWallMatchMaxAngleDegrees = 5f;
    [SerializeField] private float autoWallMatchDistanceThreshold = 0.08f;
    [SerializeField] private float autoWallMatchMinOverlapRatio = 0.6f;
    [SerializeField] private float autoWallMatchMinOverlapLength = 0.1f;
    [SerializeField] private float autoWallBroadPhasePadding = 0.05f;

    [Header("Preview")]
    [SerializeField] private float previewBoxHeight = 0.04f;
    [SerializeField] private Color previewBoxColor = new Color(0.12f, 0.85f, 1f, 0.15f);

    private Plane drawingPlane;
    private Bounds gridBounds;
    private bool hasDrawingPlane;
    private bool hasGridBounds;
    private bool isDraggingRectangle;
    private bool isDrawingPolygon;
    private bool isDraggingSelectedRoom;
    private bool pendingRoomSelection;
    private Vector3 dragStartPoint;
    private Vector3 roomDragStartPoint;
    private Vector3 pendingSelectionStartPoint;
    private Vector2 pendingSelectionStartMousePosition;
    private GameObject previewBoxObject;
    private Material previewBoxMaterial;
    private Mesh cachedCubeMesh;
    private readonly List<VirtualBoundary> previewBoundaries = new List<VirtualBoundary>();
    private readonly List<(Vector3 start, Vector3 end)> previewSegments = new List<(Vector3 start, Vector3 end)>();
    private readonly List<Vector3> snapCandidates = new List<Vector3>();
    private readonly List<SnapManager.WallSnapSegment> wallSegmentSnapCandidates = new List<SnapManager.WallSnapSegment>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<Room> cachedRooms = new List<Room>();
    private readonly List<Vector3> cachedRoomVertices = new List<Vector3>();
    private readonly List<Vector3> cachedDraggedRoomVertices = new List<Vector3>();
    private readonly List<Vector3> polygonDraftVertices = new List<Vector3>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private Room selectedRoom;
    private Room pendingSelectedRoom;
    private bool isRoomCreateModeActive;
    private IEditorInputProvider inputProvider;
    private EditorInputFrame lastInputFrame;
    private Vector3 polygonHoverPoint;
    private bool hasPolygonHoverPoint;

    private void Reset()
    {
        mainCamera = Camera.main;
        ResolveReferences();
    }

    private void Awake()
    {
        inputProvider = EditorInputManager.Instance.InputProvider;
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        ResolveReferences();
        RefreshDrawingPlane();
        EnsurePreviewObjects();
        BindModeEvents();
        SyncModeState();
        EditorInputManager.Instance.RegisterHandler(EditorMode.RoomCreate, this);
        ValidateConfiguration();
    }

    private void CommitDraggedRoom()
    {
        isDraggingRectangle = false;
        if (!TryGetMouseWorldPoint(out Vector3 endPoint))
        {
            HidePreviewObjects();
            return;
        }

        if (!TryBuildRectanglePolygon(dragStartPoint, endPoint, out List<Vector3> polygonVertices, out Bounds roomBounds))
        {
            HidePreviewObjects();
            return;
        }

        List<Room> createdRooms = new List<Room>();
        List<Room> deletedRooms = new List<Room>();

        if (!TrySplitContainingRoom(roomBounds, createdRooms, deletedRooms))
        {
            HashSet<Wall> relatedWalls = CollectWallsMatchingPolygon(polygonVertices);
            Room createdRoom = roomManager != null
                ? roomManager.CreateRoomFromPolygon(polygonVertices, relatedWalls.Count > 0 ? relatedWalls : null)
                : null;

            if (createdRoom != null)
            {
                createdRooms.Add(createdRoom);
            }
        }

        if (undoRedoManager != null)
        {
            if (deletedRooms.Count > 0)
            {
                undoRedoManager.RecordRoomsReplaced(deletedRooms, createdRooms);
            }
            else
            {
                for (int i = 0; i < createdRooms.Count; i++)
                {
                    undoRedoManager.RecordRoomCreated(createdRooms[i]);
                }
            }
        }

        if (createdRooms.Count > 0)
        {
            SetSelectedRoom(createdRooms[createdRooms.Count - 1]);
        }
        HidePreviewObjects();
    }

    private void CommitPolygonRoom(IReadOnlyList<Vector3> polygonVertices)
    {
        isDrawingPolygon = false;
        hasPolygonHoverPoint = false;

        if (polygonVertices == null || polygonVertices.Count < 3)
        {
            polygonDraftVertices.Clear();
            HidePreviewObjects();
            return;
        }

        List<Vector3> finalizedVertices = PolygonUtility.CreateSanitizedPolygonCopy(polygonVertices);
        if (!RoomPolygonValidationUtility.IsValidPolygon(
                finalizedVertices,
                Mathf.Max(minimumPolygonEdgeLength, Mathf.Min(minimumRoomWidth, minimumRoomHeight)),
                0.0001f))
        {
            isDrawingPolygon = true;
            UpdatePreviewFromPolygonDraft();
            return;
        }

        HashSet<Wall> relatedWalls = CollectWallsMatchingPolygon(finalizedVertices);
        Room createdRoom = roomManager != null
            ? roomManager.CreateRoomFromPolygon(finalizedVertices, relatedWalls.Count > 0 ? relatedWalls : null)
            : null;

        if (createdRoom != null)
        {
            undoRedoManager?.RecordRoomCreated(createdRoom);
            SetSelectedRoom(createdRoom);
            polygonDraftVertices.Clear();
            HidePreviewObjects();
            return;
        }

        isDrawingPolygon = true;
        UpdatePreviewFromPolygonDraft();
    }

    private void BeginRectangleDrag(Vector3 startPoint)
    {
        ClearSelectedRoom();
        isDraggingRectangle = true;
        dragStartPoint = startPoint;
        UpdatePreviewFromRectangle(dragStartPoint, dragStartPoint);
    }

    private void BeginPolygonDraw(Vector3 startPoint)
    {
        ClearSelectedRoom();
        isDrawingPolygon = true;
        hasPolygonHoverPoint = false;
        polygonDraftVertices.Clear();
        polygonDraftVertices.Add(startPoint);
        UpdatePreviewFromPolygonDraft();
    }

    private void AppendPolygonVertex(Vector3 point)
    {
        if (polygonDraftVertices.Count == 0)
        {
            BeginPolygonDraw(point);
            return;
        }

        if (ShouldClosePolygon(point))
        {
            TryCompletePolygonDraw();
            return;
        }

        Vector3 lastPoint = polygonDraftVertices[polygonDraftVertices.Count - 1];
        Vector3 delta = point - lastPoint;
        delta.y = 0f;
        if (delta.sqrMagnitude < minimumPolygonEdgeLength * minimumPolygonEdgeLength)
        {
            return;
        }

        polygonDraftVertices.Add(point);
        UpdatePreviewFromPolygonDraft();
    }

    private void TryCompletePolygonDraw()
    {
        if (polygonDraftVertices.Count < 3)
        {
            return;
        }

        CommitPolygonRoom(polygonDraftVertices);
    }

    private void FocusRoomForEditing(Room room)
    {
        SetSelectedRoom(room);
    }

    private void BeginSelectedRoomDrag(Room room, Vector3 startPoint)
    {
        if (room == null || room.ManualBoundaryVertices == null || room.ManualBoundaryVertices.Count < 3)
        {
            return;
        }

        SetSelectedRoom(room);
        cachedDraggedRoomVertices.Clear();
        cachedDraggedRoomVertices.AddRange(room.ManualBoundaryVertices);
        roomDragStartPoint = startPoint;
        isDraggingSelectedRoom = true;
    }

    private void UpdateSelectedRoomDrag()
    {
        if (inputProvider == null || !inputProvider.IsPointerAvailable)
        {
            CancelSelectedRoomDrag();
            return;
        }

        if (!inputProvider.IsPointerButtonPressed(PointerButton.Left))
        {
            EndSelectedRoomDrag();
            return;
        }

        if (selectedRoom == null || cachedDraggedRoomVertices.Count < 3)
        {
            CancelSelectedRoomDrag();
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 currentPoint, roomDragStartPoint, selectedRoom))
        {
            return;
        }

        Vector3 delta = currentPoint - roomDragStartPoint;
        delta.y = 0f;

        List<Vector3> movedVertices = new List<Vector3>(cachedDraggedRoomVertices.Count);
        for (int i = 0; i < cachedDraggedRoomVertices.Count; i++)
        {
            movedVertices.Add(cachedDraggedRoomVertices[i] + delta);
        }

        roomManager?.UpdateRoomPolygon(selectedRoom, movedVertices);
        roomHandleManager?.MarkDirty();
    }

    private void EndSelectedRoomDrag()
    {
        if (undoRedoManager != null &&
            selectedRoom != null &&
            selectedRoom.ManualBoundaryVertices != null &&
            selectedRoom.ManualBoundaryVertices.Count >= 3)
        {
            undoRedoManager.RecordRoomPolygonChanged(
                selectedRoom,
                cachedDraggedRoomVertices,
                selectedRoom.ManualBoundaryVertices);
        }

        isDraggingSelectedRoom = false;
        cachedDraggedRoomVertices.Clear();
        roomHandleManager?.MarkDirty();
    }

    private void CancelSelectedRoomDrag()
    {
        isDraggingSelectedRoom = false;
        cachedDraggedRoomVertices.Clear();
        roomHandleManager?.MarkDirty();
    }

    private void SetSelectedRoom(Room room)
    {
        if (selectedRoom == room)
        {
            roomHandleManager?.SetFocusedRoom(room);
            roomHandleManager?.MarkDirty();
            return;
        }

        selectedRoom = room;

        roomHandleManager?.SetFocusedRoom(room);
        roomHandleManager?.MarkDirty();
    }

    private void ClearSelectedRoom()
    {
        SetSelectedRoom(null);
    }

    private bool TryBuildRectanglePolygon(Vector3 startPoint, Vector3 endPoint, out List<Vector3> polygonVertices, out Bounds bounds)
    {
        return RoomCreateGeometryService.TryBuildRectanglePolygon(
            startPoint,
            endPoint,
            minimumRoomWidth,
            minimumRoomHeight,
            out polygonVertices,
            out bounds);
    }

    private void UpdatePreviewFromRectangle(Vector3 startPoint, Vector3 endPoint)
    {
        EnsurePreviewObjects();
        if (!VirtualBoundaryUtility.TryBuildRectangleOutlineFromRect(
                startPoint,
                endPoint,
                Mathf.Min(minimumRoomWidth, minimumRoomHeight),
                previewSegments,
                out Bounds previewBounds))
        {
            if (previewBoxObject != null)
            {
                previewBoxObject.SetActive(true);
                previewBoxObject.transform.position = previewBounds.center + Vector3.up * (previewBoxHeight * 0.5f);
                previewBoxObject.transform.localScale = new Vector3(
                    Mathf.Max(0.01f, previewBounds.size.x),
                    previewBoxHeight,
                    Mathf.Max(0.01f, previewBounds.size.z));
            }

            SetPreviewBoundaryVisibility(false);
            return;
        }

        if (previewBoxObject != null)
        {
            previewBoxObject.SetActive(true);
            previewBoxObject.transform.position = previewBounds.center + Vector3.up * (previewBoxHeight * 0.5f);
            previewBoxObject.transform.localScale = new Vector3(previewBounds.size.x, previewBoxHeight, previewBounds.size.z);
        }

        EnsurePreviewBoundaryCount(previewSegments.Count);
        for (int i = 0; i < previewBoundaries.Count; i++)
        {
            bool visible = i < previewSegments.Count;
            previewBoundaries[i].gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            previewBoundaries[i].SetEndpoints(previewSegments[i].start, previewSegments[i].end);
        }
    }

    private void UpdatePreviewFromPolygonDraft()
    {
        EnsurePreviewObjects();
        if (previewBoxObject != null)
        {
            previewBoxObject.SetActive(false);
        }

        BuildPolygonPreviewSegments(previewSegments);
        EnsurePreviewBoundaryCount(previewSegments.Count);
        for (int i = 0; i < previewBoundaries.Count; i++)
        {
            bool visible = i < previewSegments.Count;
            previewBoundaries[i].gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            previewBoundaries[i].SetEndpoints(previewSegments[i].start, previewSegments[i].end);
        }
    }

    private void CancelRectangleDrag()
    {
        isDraggingRectangle = false;
        CancelSelectedRoomDrag();
        CancelPolygonDraft();
        ClearPendingRoomSelection();
        HidePreviewObjects();
    }

    private void CancelCurrentInteraction()
    {
        isDraggingRectangle = false;
        CancelSelectedRoomDrag();
        CancelPolygonDraft();
        ClearPendingRoomSelection();
        HidePreviewObjects();
    }

    private void CancelPolygonDraft()
    {
        isDrawingPolygon = false;
        hasPolygonHoverPoint = false;
        polygonDraftVertices.Clear();
    }

    private void ClearPendingRoomSelection()
    {
        pendingRoomSelection = false;
        pendingSelectedRoom = null;
        pendingSelectionStartPoint = Vector3.zero;
        pendingSelectionStartMousePosition = Vector2.zero;
    }

    private void EnsurePreviewObjects()
    {
        EnsureCachedResources();
        if (previewBoxObject != null)
        {
            return;
        }

        previewBoxObject = new GameObject("RoomCreatePreviewBox", typeof(MeshFilter), typeof(MeshRenderer));
        previewBoxObject.transform.SetParent(transform, false);
        MeshFilter meshFilter = previewBoxObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = cachedCubeMesh;
        }

        MeshRenderer meshRenderer = previewBoxObject.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            previewBoxMaterial = CreateTransparentMaterial(previewBoxColor);
            meshRenderer.sharedMaterial = previewBoxMaterial;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        previewBoxObject.SetActive(false);
    }

    private void HidePreviewObjects()
    {
        SetPreviewBoundaryVisibility(false);
        if (previewBoxObject != null)
        {
            previewBoxObject.SetActive(false);
        }
    }

    private void EnsurePreviewBoundaryCount(int count)
    {
        while (previewBoundaries.Count < count)
        {
            GameObject boundaryObject = new GameObject($"RoomCreatePreviewBoundary_{previewBoundaries.Count:000}");
            boundaryObject.transform.SetParent(transform, true);
            VirtualBoundary previewBoundary = boundaryObject.AddComponent<VirtualBoundary>();
            previewBoundary.SetPreviewOnly(true);
            previewBoundary.gameObject.SetActive(false);
            previewBoundaries.Add(previewBoundary);
        }
    }

    private void SetPreviewBoundaryVisibility(bool visible)
    {
        for (int i = 0; i < previewBoundaries.Count; i++)
        {
            if (previewBoundaries[i] != null)
            {
                previewBoundaries[i].gameObject.SetActive(visible);
            }
        }
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

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref snapManager);
        LayerUtility.ResolveObject(ref wallHandleManager);
        LayerUtility.ResolveObject(ref roomHandleManager);
        LayerUtility.ResolveObject(ref modeManager);
        LayerUtility.ResolveObject(ref undoRedoManager);
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
        SetRoomCreateModeActive(modeManager != null && modeManager.IsMode(EditorMode.RoomCreate));
    }

    private void HandleModeChanged(EditorMode mode)
    {
        SetRoomCreateModeActive(mode == EditorMode.RoomCreate);
    }

    private void SetRoomCreateModeActive(bool active)
    {
        if (isRoomCreateModeActive == active)
        {
            return;
        }

        isRoomCreateModeActive = active;
        enabled = active;
        if (active)
        {
            OnEnterRoomCreateMode();
            return;
        }

        OnExitRoomCreateMode();
    }

    private void OnEnterRoomCreateMode()
    {
        RefreshDrawingPlane();
        EnsurePreviewObjects();
    }

    private void OnExitRoomCreateMode()
    {
        CancelCurrentInteraction();
        ClearSelectedRoom();
    }

    public void SetRectangleCreationMode()
    {
        SetCreationShapeMode(RoomCreationShapeMode.RectangleDrag);
    }

    public void SetPolygonCreationMode()
    {
        SetCreationShapeMode(RoomCreationShapeMode.PolygonDraw);
    }

    private void SetCreationShapeMode(RoomCreationShapeMode mode)
    {
        if (creationShapeMode == mode)
        {
            return;
        }

        creationShapeMode = mode;
        CancelCurrentInteraction();
    }

    private bool IsPolygonDrawMode()
    {
        return creationShapeMode == RoomCreationShapeMode.PolygonDraw;
    }

    private bool IsPolygonCreationModifierPressed()
    {
        return inputProvider != null &&
               (inputProvider.IsKeyPressed(Key.LeftShift) || inputProvider.IsKeyPressed(Key.RightShift));
    }

    private bool ShouldClosePolygon(Vector3 point)
    {
        if (polygonDraftVertices.Count < 3)
        {
            return false;
        }

        Vector3 delta = point - polygonDraftVertices[0];
        delta.y = 0f;
        return delta.sqrMagnitude <= polygonCloseDistance * polygonCloseDistance;
    }

    private bool IsPointerNearPolygonStart(Vector2 pointerScreenPosition)
    {
        if (mainCamera == null || polygonDraftVertices.Count < 3)
        {
            return false;
        }

        Vector3 screenPoint = mainCamera.WorldToScreenPoint(polygonDraftVertices[0]);
        if (screenPoint.z <= 0f)
        {
            return false;
        }

        return (new Vector2(screenPoint.x, screenPoint.y) - pointerScreenPosition).sqrMagnitude <=
               polygonCloseDistancePixels * polygonCloseDistancePixels;
    }

    private void UpdatePolygonHoverPoint(Vector3 point)
    {
        polygonHoverPoint = ShouldClosePolygon(point) ? polygonDraftVertices[0] : point;
        hasPolygonHoverPoint = true;
    }

    private void BuildPolygonPreviewSegments(List<(Vector3 start, Vector3 end)> segments)
    {
        if (segments == null)
        {
            return;
        }

        segments.Clear();
        if (polygonDraftVertices.Count == 0)
        {
            return;
        }

        for (int i = 1; i < polygonDraftVertices.Count; i++)
        {
            segments.Add((polygonDraftVertices[i - 1], polygonDraftVertices[i]));
        }

        if (!hasPolygonHoverPoint)
        {
            return;
        }

        Vector3 lastPoint = polygonDraftVertices[polygonDraftVertices.Count - 1];
        Vector3 delta = polygonHoverPoint - lastPoint;
        delta.y = 0f;
        if (delta.sqrMagnitude >= 0.0001f)
        {
            segments.Add((lastPoint, polygonHoverPoint));
        }

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

        drawingPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint, Vector3? snapAnchor = null, Room ignoreRoom = null)
    {
        worldPoint = Vector3.zero;
        if (!hasDrawingPlane)
        {
            return false;
        }

        if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        Ray mouseRay = mainCamera.ScreenPointToRay(pointerScreenPosition);
        if (!drawingPlane.Raycast(mouseRay, out float enter))
        {
            return false;
        }

        worldPoint = mouseRay.GetPoint(enter);
        if (hasGridBounds)
        {
            worldPoint.x = Mathf.Clamp(worldPoint.x, gridBounds.min.x, gridBounds.max.x);
            worldPoint.z = Mathf.Clamp(worldPoint.z, gridBounds.min.z, gridBounds.max.z);
        }

        if (snapManager == null)
        {
            return true;
        }

        snapCandidates.Clear();
        if (wallHandleManager != null)
        {
            wallHandleManager.CollectSnapPoints(snapCandidates);
        }

        roomHandleManager?.CollectSnapPoints(snapCandidates, ignoreRoom);
        if (isDrawingPolygon)
        {
            for (int i = 0; i < polygonDraftVertices.Count; i++)
            {
                snapCandidates.Add(polygonDraftVertices[i]);
            }
        }

        CollectWallSegmentSnapCandidates(wallSegmentSnapCandidates);

        Vector3 anchorPoint = snapAnchor ?? (isDraggingRectangle ? dragStartPoint : worldPoint);
        worldPoint = snapManager.GetSnappedWallDrawPoint(
            worldPoint,
            anchorPoint,
            snapCandidates,
            mainCamera,
            wallSegmentSnapCandidates,
            out _,
            out _);
        return true;
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
            wallRoot = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
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

            if (!wall.TryGetSnapSegment(0f, Mathf.Min(minimumRoomWidth, minimumRoomHeight), out SnapManager.WallSnapSegment segment))
            {
                continue;
            }

            segments.Add(segment);
        }
    }

    private HashSet<Wall> CollectWallsMatchingPolygon(IReadOnlyList<Vector3> polygonVertices)
    {
        HashSet<Wall> results = new HashSet<Wall>();
        if (polygonVertices == null || polygonVertices.Count < 3)
        {
            return results;
        }

        if (wallRoot == null)
        {
            wallRoot = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        }

        if (wallRoot == null)
        {
            return results;
        }

        Bounds polygonBounds = CalculatePolygonBounds(polygonVertices, autoWallBroadPhasePadding);
        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy || wall.Data == null)
            {
                continue;
            }

            Vector3 wallStart = wall.Data.startPoint;
            Vector3 wallEnd = wall.Data.endPoint;
            if (!RoomCreateGeometryService.ContainsPointXZ(polygonBounds, wallStart) &&
                !RoomCreateGeometryService.ContainsPointXZ(polygonBounds, wallEnd) &&
                !RoomCreateGeometryService.SegmentIntersectsBoundsXZ(polygonBounds, wallStart, wallEnd))
            {
                continue;
            }

            if (DoesWallMatchRoomBoundary(wallStart, wallEnd, polygonVertices))
            {
                results.Add(wall);
            }
        }

        return results;
    }

    private bool TrySplitContainingRoom(Bounds innerBounds, List<Room> createdRooms, List<Room> deletedRooms)
    {
        if (roomManager == null)
        {
            return false;
        }

        Room containingRoom = FindSmallestContainingRectRoom(innerBounds, out Bounds outerBounds);
        if (containingRoom == null)
        {
            return false;
        }

        if (RoomCreateGeometryService.AreBoundsNearlyEqual(innerBounds, outerBounds))
        {
            return false;
        }

        List<Bounds> splitBounds = RoomCreateGeometryService.BuildSplitBounds(
            outerBounds,
            innerBounds,
            minimumRoomWidth,
            minimumRoomHeight);
        if (splitBounds.Count == 0)
        {
            return false;
        }

        deletedRooms.Add(containingRoom);
        roomManager.DeleteRoom(containingRoom);

        for (int i = 0; i < splitBounds.Count; i++)
        {
            List<Vector3> polygon = RoomCreateGeometryService.BuildPolygonFromBounds(splitBounds[i], innerBounds.center.y);
            HashSet<Wall> walls = CollectWallsMatchingPolygon(polygon);
            Room room = roomManager.CreateRoomFromPolygon(polygon, walls.Count > 0 ? walls : null);
            if (room != null)
            {
                createdRooms.Add(room);
            }
        }

        return createdRooms.Count > 0;
    }

    private Room FindSmallestContainingRectRoom(Bounds targetBounds, out Bounds containingBounds)
    {
        containingBounds = default;
        if (roomManager == null)
        {
            return null;
        }

        cachedRooms.Clear();
        cachedRooms.AddRange(roomManager.GetAllRooms());

        Room bestRoom = null;
        float bestArea = float.MaxValue;
        for (int i = 0; i < cachedRooms.Count; i++)
        {
            Room room = cachedRooms[i];
            if (!RoomCreateGeometryService.TryGetAxisAlignedRoomBounds(
                    room != null ? room.ManualBoundaryVertices : null,
                    minimumRoomWidth,
                    minimumRoomHeight,
                    out Bounds roomBounds))
            {
                continue;
            }

            if (!RoomCreateGeometryService.BoundsContainBoundsXZ(roomBounds, targetBounds))
            {
                continue;
            }

            float area = roomBounds.size.x * roomBounds.size.z;
            if (area >= bestArea)
            {
                continue;
            }

            bestArea = area;
            bestRoom = room;
            containingBounds = roomBounds;
        }

        return bestRoom;
    }

    private Room PickRoomAtWorldPoint(Vector3 worldPoint)
    {
        if (roomManager == null)
        {
            return null;
        }

        cachedRooms.Clear();
        cachedRooms.AddRange(roomManager.GetAllRooms());

        Room bestRoom = null;
        float bestArea = float.MaxValue;
        for (int i = 0; i < cachedRooms.Count; i++)
        {
            Room room = cachedRooms[i];
            if (room == null || !room.TryGetOrderedVertices(cachedRoomVertices) || cachedRoomVertices.Count < 3)
            {
                continue;
            }

            if (!RoomCreateGeometryService.IsPointInsidePolygonXZ(worldPoint, cachedRoomVertices))
            {
                continue;
            }

            float area = Mathf.Abs(RoomCreateGeometryService.CalculateSignedAreaXZ(cachedRoomVertices));
            if (area >= bestArea)
            {
                continue;
            }

            bestArea = area;
            bestRoom = room;
        }

        return bestRoom;
    }

    private bool DoesWallMatchRoomBoundary(Vector3 wallStart, Vector3 wallEnd, IReadOnlyList<Vector3> polygonVertices)
    {
        Vector2 wallStart2 = new Vector2(wallStart.x, wallStart.z);
        Vector2 wallEnd2 = new Vector2(wallEnd.x, wallEnd.z);
        Vector2 wallVector = wallEnd2 - wallStart2;
        float wallLength = wallVector.magnitude;
        if (wallLength < 0.0001f)
        {
            return false;
        }

        Vector2 wallDirection = wallVector / wallLength;
        float maxAngleCos = Mathf.Cos(Mathf.Deg2Rad * Mathf.Clamp(autoWallMatchMaxAngleDegrees, 0.1f, 45f));

        for (int i = 0; i < polygonVertices.Count; i++)
        {
            Vector3 current = polygonVertices[i];
            Vector3 next = polygonVertices[(i + 1) % polygonVertices.Count];
            Vector2 edgeStart = new Vector2(current.x, current.z);
            Vector2 edgeEnd = new Vector2(next.x, next.z);
            Vector2 edgeVector = edgeEnd - edgeStart;
            float edgeLength = edgeVector.magnitude;
            if (edgeLength < 0.0001f)
            {
                continue;
            }

            Vector2 edgeDirection = edgeVector / edgeLength;
            float alignment = Mathf.Abs(Vector2.Dot(wallDirection, edgeDirection));
            if (alignment < maxAngleCos)
            {
                continue;
            }

            float startDistance = DistancePointToInfiniteLine(wallStart2, edgeStart, edgeDirection);
            float endDistance = DistancePointToInfiniteLine(wallEnd2, edgeStart, edgeDirection);
            float midpointDistance = DistancePointToInfiniteLine((wallStart2 + wallEnd2) * 0.5f, edgeStart, edgeDirection);
            if (midpointDistance > autoWallMatchDistanceThreshold ||
                Mathf.Min(startDistance, endDistance) > autoWallMatchDistanceThreshold * 1.5f)
            {
                continue;
            }

            float wallMin = Vector2.Dot(wallStart2 - edgeStart, edgeDirection);
            float wallMax = Vector2.Dot(wallEnd2 - edgeStart, edgeDirection);
            if (wallMin > wallMax)
            {
                float temp = wallMin;
                wallMin = wallMax;
                wallMax = temp;
            }

            float overlapLength = Mathf.Min(wallMax, edgeLength) - Mathf.Max(wallMin, 0f);
            if (overlapLength <= 0f)
            {
                continue;
            }

            float shorterLength = Mathf.Min(wallLength, edgeLength);
            float overlapRatio = shorterLength > 0.0001f ? overlapLength / shorterLength : 0f;
            if (overlapLength >= autoWallMatchMinOverlapLength &&
                overlapRatio >= autoWallMatchMinOverlapRatio)
            {
                return true;
            }
        }

        return false;
    }

    private static float DistancePointToInfiniteLine(Vector2 point, Vector2 lineOrigin, Vector2 lineDirection)
    {
        Vector2 offset = point - lineOrigin;
        float projected = Vector2.Dot(offset, lineDirection);
        Vector2 closestPoint = lineOrigin + lineDirection * projected;
        return Vector2.Distance(point, closestPoint);
    }

    private static Bounds CalculatePolygonBounds(IReadOnlyList<Vector3> polygonVertices, float padding)
    {
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        float y = polygonVertices[0].y;

        for (int i = 0; i < polygonVertices.Count; i++)
        {
            Vector3 vertex = polygonVertices[i];
            minX = Mathf.Min(minX, vertex.x);
            maxX = Mathf.Max(maxX, vertex.x);
            minZ = Mathf.Min(minZ, vertex.z);
            maxZ = Mathf.Max(maxZ, vertex.z);
        }

        minX -= padding;
        maxX += padding;
        minZ -= padding;
        maxZ += padding;

        return new Bounds(
            new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
            new Vector3(maxX - minX, 0.01f, maxZ - minZ));
    }

    private bool TryGetPointerScreenPosition(out Vector2 pointerScreenPosition)
    {
        if (lastInputFrame.IsPointerAvailable)
        {
            pointerScreenPosition = lastInputFrame.PointerScreenPosition;
            return true;
        }

        if (inputProvider != null && inputProvider.TryGetPointerScreenPosition(out pointerScreenPosition))
        {
            return true;
        }

        pointerScreenPosition = Vector2.zero;
        return false;
    }

    private void OnDestroy()
    {
        UnbindModeEvents();
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterHandler(EditorMode.RoomCreate, this);
        }

        for (int i = 0; i < previewBoundaries.Count; i++)
        {
            if (previewBoundaries[i] != null)
            {
                Destroy(previewBoundaries[i].gameObject);
            }
        }

        previewBoundaries.Clear();

        if (previewBoxObject != null)
        {
            Destroy(previewBoxObject);
        }

        if (previewBoxMaterial != null)
        {
            Destroy(previewBoxMaterial);
        }
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(mainCamera != null, $"{nameof(RoomCreateManager)} requires {nameof(mainCamera)}.", this);
        Debug.Assert(roomManager != null, $"{nameof(RoomCreateManager)} requires {nameof(roomManager)}.", this);
        Debug.Assert(modeManager != null, $"{nameof(RoomCreateManager)} requires {nameof(modeManager)}.", this);
    }
}
