using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class RoomCreateManager : MonoBehaviour
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
    }

    private void Update()
    {
        if (mainCamera == null || Mouse.current == null)
        {
            return;
        }

        bool isRoomCreateMode = modeManager != null && modeManager.IsMode(EditorMode.RoomCreate);
        if (!isRoomCreateMode)
        {
            CancelCurrentInteraction();
            ClearSelectedRoom();
            return;
        }

        if (wallHandleManager != null && wallHandleManager.IsDraggingHandle)
        {
            return;
        }

        if (roomHandleManager != null && roomHandleManager.IsDraggingHandle)
        {
            return;
        }

        bool isPointerOverUI = IsPointerOverUI();
        bool isPointerOverRoomHandle = roomHandleManager != null && roomHandleManager.IsPointerOverHandle(Mouse.current.position.ReadValue());

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelCurrentInteraction();
            ClearSelectedRoom();
            return;
        }

        if (isDraggingSelectedRoom)
        {
            UpdateSelectedRoomDrag();
            return;
        }

        if (!isDraggingRectangle)
        {
            if (pendingRoomSelection)
            {
                HandlePendingRoomSelection();
                return;
            }

            if (!isPointerOverUI &&
                !isPointerOverRoomHandle &&
                Mouse.current.leftButton.wasPressedThisFrame &&
                TryGetMouseWorldPoint(out Vector3 startPoint))
            {
                pendingSelectedRoom = PickRoomAtWorldPoint(startPoint);
                if (pendingSelectedRoom != null)
                {
                    pendingRoomSelection = true;
                    pendingSelectionStartPoint = startPoint;
                    pendingSelectionStartMousePosition = Mouse.current.position.ReadValue();
                    return;
                }

                BeginRectangleDrag(startPoint);
            }

            return;
        }

        if (Mouse.current.leftButton.isPressed)
        {
            UpdatePreviewWhileDragging();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            CommitDraggedRoom();
        }
    }

    private void UpdatePreviewWhileDragging()
    {
        if (!TryGetMouseWorldPoint(out Vector3 currentPoint))
        {
            HidePreviewObjects();
            return;
        }

        UpdatePreviewFromRectangle(dragStartPoint, currentPoint);
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

    private void HandlePendingRoomSelection()
    {
        if (Mouse.current == null)
        {
            ClearPendingRoomSelection();
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        float thresholdSqr = clickToSelectThresholdPixels * clickToSelectThresholdPixels;
        float movedSqr = (mousePosition - pendingSelectionStartMousePosition).sqrMagnitude;

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            FocusRoomForEditing(pendingSelectedRoom);
            ClearPendingRoomSelection();
            return;
        }

        if (!Mouse.current.leftButton.isPressed)
        {
            ClearPendingRoomSelection();
            return;
        }

        if (movedSqr < thresholdSqr)
        {
            return;
        }

        Room room = pendingSelectedRoom;
        Vector3 startPoint = pendingSelectionStartPoint;
        ClearPendingRoomSelection();
        if (room != null)
        {
            BeginSelectedRoomDrag(room, startPoint);
            return;
        }

        BeginRectangleDrag(startPoint);
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
        polygonVertices = new List<Vector3>();
        float minX = Mathf.Min(startPoint.x, endPoint.x);
        float maxX = Mathf.Max(startPoint.x, endPoint.x);
        float minZ = Mathf.Min(startPoint.z, endPoint.z);
        float maxZ = Mathf.Max(startPoint.z, endPoint.z);
        float width = maxX - minX;
        float height = maxZ - minZ;
        float y = startPoint.y;

        bounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
            new Vector3(width, 0.01f, height));

        if (width < minimumRoomWidth || height < minimumRoomHeight)
        {
            return false;
        }

        polygonVertices.Add(new Vector3(minX, y, minZ));
        polygonVertices.Add(new Vector3(maxX, y, minZ));
        polygonVertices.Add(new Vector3(maxX, y, maxZ));
        polygonVertices.Add(new Vector3(minX, y, maxZ));
        return true;
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
        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }

        if (snapManager == null)
        {
            snapManager = FindFirstObjectByType<SnapManager>();
        }

        if (wallHandleManager == null)
        {
            wallHandleManager = FindFirstObjectByType<HandleManager>();
        }

        if (roomHandleManager == null)
        {
            roomHandleManager = FindFirstObjectByType<RoomHandleManager>();
        }

        if (modeManager == null)
        {
            modeManager = FindFirstObjectByType<ModeManager>();
        }

        if (undoRedoManager == null)
        {
            undoRedoManager = FindFirstObjectByType<UndoRedoManager>();
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

            if (ContainsPointXZ(bounds, wall.StartPoint) ||
                ContainsPointXZ(bounds, wall.EndPoint) ||
                SegmentIntersectsBoundsXZ(bounds, wall.StartPoint, wall.EndPoint))
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

        if (AreBoundsNearlyEqual(innerBounds, outerBounds))
        {
            return false;
        }

        List<Bounds> splitBounds = BuildSplitBounds(outerBounds, innerBounds);
        if (splitBounds.Count == 0)
        {
            return false;
        }

        deletedRooms.Add(containingRoom);
        roomManager.DeleteRoom(containingRoom);

        for (int i = 0; i < splitBounds.Count; i++)
        {
            List<Vector3> polygon = BuildPolygonFromBounds(splitBounds[i], innerBounds.center.y);
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
            if (!TryGetRoomBounds(room, out Bounds roomBounds))
            {
                continue;
            }

            if (!BoundsContainBoundsXZ(roomBounds, targetBounds))
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

            if (!IsPointInsidePolygonXZ(worldPoint, cachedRoomVertices))
            {
                continue;
            }

            float area = Mathf.Abs(CalculateSignedAreaXZ(cachedRoomVertices));
            if (area >= bestArea)
            {
                continue;
            }

            bestArea = area;
            bestRoom = room;
        }

        return bestRoom;
    }

    private bool TryGetRoomBounds(Room room, out Bounds bounds)
    {
        bounds = default;
        if (room == null || room.ManualBoundaryVertices == null || room.ManualBoundaryVertices.Count != 4)
        {
            return false;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        float y = room.ManualBoundaryVertices[0].y;

        for (int i = 0; i < room.ManualBoundaryVertices.Count; i++)
        {
            Vector3 current = room.ManualBoundaryVertices[i];
            Vector3 next = room.ManualBoundaryVertices[(i + 1) % room.ManualBoundaryVertices.Count];
            bool horizontal = Mathf.Abs(current.z - next.z) <= 0.0001f;
            bool vertical = Mathf.Abs(current.x - next.x) <= 0.0001f;
            if (!horizontal && !vertical)
            {
                return false;
            }

            minX = Mathf.Min(minX, current.x);
            maxX = Mathf.Max(maxX, current.x);
            minZ = Mathf.Min(minZ, current.z);
            maxZ = Mathf.Max(maxZ, current.z);
        }

        bounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
            new Vector3(maxX - minX, 0.01f, maxZ - minZ));
        return bounds.size.x >= minimumRoomWidth && bounds.size.z >= minimumRoomHeight;
    }

    private bool BoundsContainBoundsXZ(Bounds outer, Bounds inner)
    {
        const float epsilon = 0.0001f;
        return inner.min.x >= outer.min.x - epsilon &&
               inner.max.x <= outer.max.x + epsilon &&
               inner.min.z >= outer.min.z - epsilon &&
               inner.max.z <= outer.max.z + epsilon;
    }

    private static bool AreBoundsNearlyEqual(Bounds left, Bounds right)
    {
        return Mathf.Abs(left.min.x - right.min.x) <= 0.0001f &&
               Mathf.Abs(left.max.x - right.max.x) <= 0.0001f &&
               Mathf.Abs(left.min.z - right.min.z) <= 0.0001f &&
               Mathf.Abs(left.max.z - right.max.z) <= 0.0001f;
    }

    private List<Bounds> BuildSplitBounds(Bounds outerBounds, Bounds innerBounds)
    {
        List<Bounds> results = new List<Bounds>();
        TryAddSplitBounds(results, outerBounds.min.x, outerBounds.max.x, outerBounds.min.z, innerBounds.min.z, innerBounds.center.y);
        TryAddSplitBounds(results, outerBounds.min.x, outerBounds.max.x, innerBounds.max.z, outerBounds.max.z, innerBounds.center.y);
        TryAddSplitBounds(results, outerBounds.min.x, innerBounds.min.x, innerBounds.min.z, innerBounds.max.z, innerBounds.center.y);
        TryAddSplitBounds(results, innerBounds.max.x, outerBounds.max.x, innerBounds.min.z, innerBounds.max.z, innerBounds.center.y);
        TryAddSplitBounds(results, innerBounds.min.x, innerBounds.max.x, innerBounds.min.z, innerBounds.max.z, innerBounds.center.y);
        return results;
    }

    private void TryAddSplitBounds(List<Bounds> results, float minX, float maxX, float minZ, float maxZ, float y)
    {
        float width = maxX - minX;
        float height = maxZ - minZ;
        if (width < minimumRoomWidth || height < minimumRoomHeight)
        {
            return;
        }

        results.Add(new Bounds(
            new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
            new Vector3(width, 0.01f, height)));
    }

    private static List<Vector3> BuildPolygonFromBounds(Bounds bounds, float y)
    {
        return new List<Vector3>
        {
            new Vector3(bounds.min.x, y, bounds.min.z),
            new Vector3(bounds.max.x, y, bounds.min.z),
            new Vector3(bounds.max.x, y, bounds.max.z),
            new Vector3(bounds.min.x, y, bounds.max.z),
        };
    }

    private static bool ContainsPointXZ(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x &&
               point.x <= bounds.max.x &&
               point.z >= bounds.min.z &&
               point.z <= bounds.max.z;
    }

    private static bool SegmentIntersectsBoundsXZ(Bounds bounds, Vector3 start, Vector3 end)
    {
        if (ContainsPointXZ(bounds, start) || ContainsPointXZ(bounds, end))
        {
            return true;
        }

        Vector2 a = new Vector2(start.x, start.z);
        Vector2 b = new Vector2(end.x, end.z);
        Vector2 rectMin = new Vector2(bounds.min.x, bounds.min.z);
        Vector2 rectMax = new Vector2(bounds.max.x, bounds.max.z);

        Vector2 topLeft = new Vector2(rectMin.x, rectMax.y);
        Vector2 topRight = rectMax;
        Vector2 bottomLeft = rectMin;
        Vector2 bottomRight = new Vector2(rectMax.x, rectMin.y);

        return SegmentsIntersect2D(a, b, bottomLeft, topLeft) ||
               SegmentsIntersect2D(a, b, topLeft, topRight) ||
               SegmentsIntersect2D(a, b, topRight, bottomRight) ||
               SegmentsIntersect2D(a, b, bottomRight, bottomLeft);
    }

    private static bool SegmentsIntersect2D(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float o1 = Orientation(a1, a2, b1);
        float o2 = Orientation(a1, a2, b2);
        float o3 = Orientation(b1, b2, a1);
        float o4 = Orientation(b1, b2, a2);

        if (o1 * o2 < 0f && o3 * o4 < 0f)
        {
            return true;
        }

        return Mathf.Approximately(o1, 0f) && OnSegment(a1, b1, a2) ||
               Mathf.Approximately(o2, 0f) && OnSegment(a1, b2, a2) ||
               Mathf.Approximately(o3, 0f) && OnSegment(b1, a1, b2) ||
               Mathf.Approximately(o4, 0f) && OnSegment(b1, a2, b2);
    }

    private static float Orientation(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool OnSegment(Vector2 a, Vector2 point, Vector2 b)
    {
        return point.x >= Mathf.Min(a.x, b.x) - 0.0001f &&
               point.x <= Mathf.Max(a.x, b.x) + 0.0001f &&
               point.y >= Mathf.Min(a.y, b.y) - 0.0001f &&
               point.y <= Mathf.Max(a.y, b.y) + 0.0001f;
    }

    private static bool IsPointInsidePolygonXZ(Vector3 point, List<Vector3> polygon)
    {
        bool inside = false;
        int count = polygon != null ? polygon.Count : 0;
        if (count < 3)
        {
            return false;
        }

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector3 pi = polygon[i];
            Vector3 pj = polygon[j];
            bool intersects = ((pi.z > point.z) != (pj.z > point.z)) &&
                              (point.x < (pj.x - pi.x) * (point.z - pi.z) / ((pj.z - pi.z) + 0.000001f) + pi.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float CalculateSignedAreaXZ(List<Vector3> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 a = polygon[i];
            Vector3 b = polygon[(i + 1) % polygon.Count];
            area += (a.x * b.z) - (b.x * a.z);
        }

        return area * 0.5f;
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
}
