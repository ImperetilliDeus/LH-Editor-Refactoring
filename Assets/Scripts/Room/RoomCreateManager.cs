using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed partial class RoomCreateManager : MonoBehaviour
{
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
    [SerializeField] private float minimumRoomWidth = 0.1f;
    [SerializeField] private float minimumRoomHeight = 0.1f;
    [SerializeField] private float clickToSelectThresholdPixels = 6f;

    [Header("Preview")]
    [SerializeField] private float previewBoxHeight = 0.04f;
    [SerializeField] private Color previewBoxColor = new Color(0.12f, 0.85f, 1f, 0.15f);

    private Plane drawingPlane;
    private Bounds gridBounds;
    private bool hasDrawingPlane;
    private bool hasGridBounds;
    private bool isDraggingRectangle;
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
    private readonly List<(Vector3 start, Vector3 end)> rectangleSegments = new List<(Vector3 start, Vector3 end)>();
    private readonly List<Vector3> snapCandidates = new List<Vector3>();
    private readonly List<SnapManager.WallSnapSegment> wallSegmentSnapCandidates = new List<SnapManager.WallSnapSegment>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<Room> cachedRooms = new List<Room>();
    private readonly List<Vector3> cachedRoomVertices = new List<Vector3>();
    private readonly List<Vector3> cachedDraggedRoomVertices = new List<Vector3>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private Room selectedRoom;
    private Room pendingSelectedRoom;
    private bool isRoomCreateModeActive;

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
        EnsurePreviewObjects();
        BindModeEvents();
        SyncModeState();
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
            HashSet<Wall> relatedWalls = CollectWallsIntersectingBounds(roomBounds);
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

    private void BeginRectangleDrag(Vector3 startPoint)
    {
        ClearSelectedRoom();
        isDraggingRectangle = true;
        dragStartPoint = startPoint;
        UpdatePreviewFromRectangle(dragStartPoint, dragStartPoint);
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
        if (Mouse.current == null)
        {
            CancelSelectedRoomDrag();
            return;
        }

        if (!Mouse.current.leftButton.isPressed)
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
                rectangleSegments,
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

        EnsurePreviewBoundaryCount(rectangleSegments.Count);
        for (int i = 0; i < previewBoundaries.Count; i++)
        {
            bool visible = i < rectangleSegments.Count;
            previewBoundaries[i].gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            previewBoundaries[i].SetEndpoints(rectangleSegments[i].start, rectangleSegments[i].end);
        }
    }

    private void CancelRectangleDrag()
    {
        isDraggingRectangle = false;
        CancelSelectedRoomDrag();
        ClearPendingRoomSelection();
        HidePreviewObjects();
    }

    private void CancelCurrentInteraction()
    {
        isDraggingRectangle = false;
        CancelSelectedRoomDrag();
        ClearPendingRoomSelection();
        HidePreviewObjects();
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

        Ray mouseRay = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
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
            wallRoot = LayerUtility.FindTransformByName("Walls", true);
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

    private HashSet<Wall> CollectWallsIntersectingBounds(Bounds bounds)
    {
        HashSet<Wall> results = new HashSet<Wall>();
        if (wallRoot == null)
        {
            wallRoot = LayerUtility.FindTransformByName("Walls", true);
        }

        if (wallRoot == null)
        {
            return results;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (RoomCreateGeometryService.ContainsPointXZ(bounds, wall.StartPoint) ||
                RoomCreateGeometryService.ContainsPointXZ(bounds, wall.EndPoint) ||
                RoomCreateGeometryService.SegmentIntersectsBoundsXZ(bounds, wall.StartPoint, wall.EndPoint))
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
            HashSet<Wall> walls = CollectWallsIntersectingBounds(splitBounds[i]);
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

    private void OnDestroy()
    {
        UnbindModeEvents();

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
