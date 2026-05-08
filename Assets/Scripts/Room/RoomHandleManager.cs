using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class RoomHandleManager : MonoBehaviour, IEditorModeInputHandler
{
    private const string HandleCanvasName = "RoomHandleCanvas";
    private const float PolygonAreaEpsilon = 0.0001f;

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject grid;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private SnapManager snapManager;
    [SerializeField] private HandleManager wallHandleManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private UndoRedoManager undoRedoManager;

    [Header("Handle UI")]
    [SerializeField] private Vector2 handleSize = new Vector2(16f, 16f);
    [SerializeField] private Color handleColor = new Color(0.2f, 1f, 0.55f, 1f);
    [SerializeField] private Color activeHandleColor = new Color(1f, 0.82f, 0.2f, 1f);

    [Header("Visibility")]
    [SerializeField] private bool showHandlesOnlyForFocusedRoom = true;

    [Header("Drag")]
    [SerializeField] private float minimumRoomEdgeLength = 0.1f;
    [SerializeField] private float wallSnapDistance = 10f;

    private sealed class HandleGroup
    {
        public Room room;
        public int vertexIndex;
        public Vector3 worldPoint;
        public RectTransform rect;
        public Image image;
    }

    private readonly List<HandleGroup> handleGroups = new List<HandleGroup>();
    private readonly List<HandleGroup> pooledHandleGroups = new List<HandleGroup>();
    private readonly List<Vector3> snapCandidates = new List<Vector3>();
    private readonly List<SnapManager.WallSnapSegment> wallSegmentSnapCandidates = new List<SnapManager.WallSnapSegment>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<Vector3> dragOriginalVertices = new List<Vector3>();
    private readonly List<Room> cachedRooms = new List<Room>();

    private Plane dragPlane;
    private Bounds gridBounds;
    private bool hasDragPlane;
    private bool hasGridBounds;
    private bool handlesDirty = true;
    private bool handlePositionsDirty = true;
    private HandleGroup draggingGroup;
    private Sprite circularHandleSprite;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private float lastCameraOrthoSize;
    private Room focusedRoom;
    private IEditorInputProvider inputProvider;

    public bool IsDraggingHandle => draggingGroup != null;
    public Room FocusedRoom => focusedRoom;
    public event System.Action<Room> FocusedRoomChanged;

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
        EnsureCanvas();
        RefreshDragPlane();
        BindEvents();
        CacheCameraState();
        SyncModeState();
        EditorInputManager.Instance.RegisterGlobalHandler(this);
        ValidateConfiguration();
    }

    private void Update()
    {
        if (mainCamera == null || inputProvider == null)
        {
            return;
        }

        if (HasCameraStateChanged())
        {
            handlePositionsDirty = true;
        }

        if (handlesDirty)
        {
            RebuildHandles();
            handlesDirty = false;
            handlePositionsDirty = true;
        }

        if (handlePositionsDirty)
        {
            UpdateHandleWorldPoints();
            UpdateHandlePositions();
            handlePositionsDirty = false;
        }

        CacheCameraState();
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        if (mainCamera == null || inputProvider == null || !inputFrame.IsPointerAvailable)
        {
            return;
        }

        if (modeManager != null && !modeManager.IsMode(EditorMode.RoomCreate))
        {
            return;
        }

        HandleDragInput(inputFrame);
    }

    public void MarkDirty()
    {
        handlesDirty = true;
        handlePositionsDirty = true;
    }

    public void SetFocusedRoom(Room room)
    {
        if (focusedRoom == room)
        {
            return;
        }

        focusedRoom = room;
        MarkDirty();
        FocusedRoomChanged?.Invoke(focusedRoom);
    }

    public void ClearFocusedRoom()
    {
        SetFocusedRoom(null);
    }

    public bool IsPointerOverHandle(Vector2 screenPoint)
    {
        for (int i = 0; i < handleGroups.Count; i++)
        {
            HandleGroup group = handleGroups[i];
            if (group == null || group.rect == null || !group.rect.gameObject.activeInHierarchy)
            {
                continue;
            }

            Camera uiCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(group.rect, screenPoint, uiCamera))
            {
                return true;
            }
        }

        return false;
    }

    public void CollectSnapPoints(List<Vector3> points, Room ignoreRoom = null)
    {
        if (points == null || roomManager == null)
        {
            return;
        }

        cachedRooms.Clear();
        cachedRooms.AddRange(roomManager.GetAllRooms());
        for (int i = 0; i < cachedRooms.Count; i++)
        {
            Room room = cachedRooms[i];
            if (room == null || room == ignoreRoom || room.ManualBoundaryVertices == null)
            {
                continue;
            }

            for (int j = 0; j < room.ManualBoundaryVertices.Count; j++)
            {
                points.Add(room.ManualBoundaryVertices[j]);
            }
        }
    }

    private void BindEvents()
    {
        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleRoomsChanged;
            roomManager.RoomsChanged += HandleRoomsChanged;
        }

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
            modeManager.ModeChanged += HandleModeChanged;
        }
    }

    private void UnbindEvents()
    {
        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleRoomsChanged;
        }

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
        }
    }

    private void HandleRoomsChanged()
    {
        if (focusedRoom != null)
        {
            cachedRooms.Clear();
            cachedRooms.AddRange(roomManager != null ? roomManager.GetAllRooms() : new List<Room>());
            if (!cachedRooms.Contains(focusedRoom))
            {
                focusedRoom = null;
            }
        }

        if (draggingGroup == null)
        {
            handlesDirty = true;
        }

        handlePositionsDirty = true;
    }

    private void HandleModeChanged(EditorMode mode)
    {
        bool visible = mode == EditorMode.RoomCreate;
        SetHandlesVisible(visible);
        enabled = visible;

        if (!visible)
        {
            draggingGroup = null;
            dragOriginalVertices.Clear();
        }
    }

    private void HandleDragInput(EditorInputFrame inputFrame)
    {
        Vector2 mousePosition = inputFrame.PointerScreenPosition;
        if (!inputFrame.IsPointerAvailable)
        {
            return;
        }

        if (draggingGroup == null)
        {
            if (!inputFrame.LeftPressedThisFrame)
            {
                return;
            }

            if (!TryFindHandleAtScreenPoint(mousePosition, out HandleGroup selectedGroup))
            {
                return;
            }

            BeginDrag(selectedGroup);
            return;
        }

        if (inputFrame.LeftPressed)
        {
            UpdateDraggingGroup();
        }

        if (inputFrame.LeftReleasedThisFrame)
        {
            EndDrag();
        }
    }

    private bool TryFindHandleAtScreenPoint(Vector2 mousePosition, out HandleGroup selectedGroup)
    {
        selectedGroup = null;
        for (int i = 0; i < handleGroups.Count; i++)
        {
            HandleGroup group = handleGroups[i];
            if (group == null || group.rect == null || !group.rect.gameObject.activeInHierarchy)
            {
                continue;
            }

            Camera uiCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint(group.rect, mousePosition, uiCamera))
            {
                continue;
            }

            selectedGroup = group;
            return true;
        }

        return false;
    }

    private void BeginDrag(HandleGroup group)
    {
        draggingGroup = group;
        dragOriginalVertices.Clear();

        if (group?.image != null)
        {
            group.image.color = activeHandleColor;
        }

        if (group?.room?.ManualBoundaryVertices == null)
        {
            return;
        }

        for (int i = 0; i < group.room.ManualBoundaryVertices.Count; i++)
        {
            dragOriginalVertices.Add(group.room.ManualBoundaryVertices[i]);
        }
    }

    private void UpdateDraggingGroup()
    {
        if (draggingGroup?.room == null || dragOriginalVertices.Count < 3)
        {
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 snappedPoint))
        {
            return;
        }

        List<Vector3> updatedVertices = BuildDraggedPolygon(dragOriginalVertices, draggingGroup.vertexIndex, snappedPoint);
        if (updatedVertices == null || updatedVertices.Count < 3)
        {
            return;
        }

        if (!RoomPolygonValidationUtility.IsValidPolygon(updatedVertices, minimumRoomEdgeLength, PolygonAreaEpsilon))
        {
            return;
        }

        if (roomManager != null && roomManager.UpdateRoomPolygon(draggingGroup.room, updatedVertices))
        {
            draggingGroup.worldPoint = snappedPoint;
            handlePositionsDirty = true;
        }
    }

    private void EndDrag()
    {
        Room draggedRoom = draggingGroup != null ? draggingGroup.room : null;
        if (draggingGroup == null)
        {
            return;
        }

        if (draggingGroup.image != null)
        {
            draggingGroup.image.color = handleColor;
        }

        if (draggingGroup.room != null && dragOriginalVertices.Count >= 3)
        {
            List<Vector3> finalVertices = Room.CreateSanitizedPolygonCopy(draggingGroup.room.ManualBoundaryVertices);
            if (!RoomPolygonValidationUtility.IsValidPolygon(finalVertices, minimumRoomEdgeLength, PolygonAreaEpsilon))
            {
                roomManager?.UpdateRoomPolygon(draggingGroup.room, dragOriginalVertices);
                finalVertices = Room.CreateSanitizedPolygonCopy(dragOriginalVertices);
            }

            if (undoRedoManager != null && draggedRoom != null)
            {
                undoRedoManager.RecordRoomPolygonChanged(draggedRoom, dragOriginalVertices, finalVertices);
            }
        }

        draggingGroup = null;
        dragOriginalVertices.Clear();
        handlesDirty = true;
        handlePositionsDirty = true;
    }

    private List<Vector3> BuildDraggedPolygon(List<Vector3> sourceVertices, int vertexIndex, Vector3 snappedPoint)
    {
        List<Vector3> vertices = new List<Vector3>(sourceVertices);
        if (vertexIndex < 0 || vertexIndex >= vertices.Count)
        {
            return vertices;
        }

        vertices[vertexIndex] = snappedPoint;
        return vertices;
    }

    private void RebuildHandles()
    {
        EnsureCanvas();
        for (int i = 0; i < handleGroups.Count; i++)
        {
            ReleaseHandleGroup(handleGroups[i]);
        }

        handleGroups.Clear();
        if (roomManager == null)
        {
            return;
        }

        List<Room> rooms = roomManager.GetAllRooms();
        if (showHandlesOnlyForFocusedRoom)
        {
            rooms.Clear();
            if (focusedRoom != null)
            {
                rooms.Add(focusedRoom);
            }
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null || room.ManualBoundaryVertices == null || room.ManualBoundaryVertices.Count < 3)
            {
                continue;
            }

            for (int vertexIndex = 0; vertexIndex < room.ManualBoundaryVertices.Count; vertexIndex++)
            {
                HandleGroup group = AcquireHandleGroup($"RoomHandle_{handleGroups.Count:000}");
                group.room = room;
                group.vertexIndex = vertexIndex;
                group.worldPoint = room.ManualBoundaryVertices[vertexIndex];
                group.image.color = handleColor;

                handleGroups.Add(group);
            }
        }
    }

    private void UpdateHandleWorldPoints()
    {
        for (int i = 0; i < handleGroups.Count; i++)
        {
            HandleGroup group = handleGroups[i];
            if (group?.room == null || group.vertexIndex < 0 || group.vertexIndex >= group.room.ManualBoundaryVertices.Count)
            {
                continue;
            }

            group.worldPoint = group.room.ManualBoundaryVertices[group.vertexIndex];
        }
    }

    private void UpdateHandlePositions()
    {
        for (int i = 0; i < handleGroups.Count; i++)
        {
            HandleGroup group = handleGroups[i];
            if (group == null || group.rect == null)
            {
                continue;
            }

            SetHandleScreenPosition(group.rect, group.worldPoint);
        }
    }

    private void SetHandlesVisible(bool visible)
    {
        for (int i = 0; i < handleGroups.Count; i++)
        {
            if (handleGroups[i]?.rect != null)
            {
                handleGroups[i].rect.gameObject.SetActive(visible);
            }
        }
    }

    private void SetHandleScreenPosition(RectTransform handleRect, Vector3 worldPoint)
    {
        if (handleRect == null)
        {
            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPoint);
        bool visible = screenPosition.z > 0f;
        handleRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            handleRect.position = screenPosition;
            return;
        }

        RectTransform canvasRect = targetCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            handleRect.position = screenPosition;
            return;
        }

        Camera uiCamera = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : mainCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
        {
            handleRect.anchoredPosition = localPoint;
        }
    }

    private HandleGroup AcquireHandleGroup(string handleName)
    {
        HandleGroup group;
        if (pooledHandleGroups.Count > 0)
        {
            int lastIndex = pooledHandleGroups.Count - 1;
            group = pooledHandleGroups[lastIndex];
            pooledHandleGroups.RemoveAt(lastIndex);
        }
        else
        {
            GameObject handleObject = new GameObject(handleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.SetParent(targetCanvas.transform, false);
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = handleSize;

            Image image = handleObject.GetComponent<Image>();
            group = new HandleGroup
            {
                rect = handleRect,
                image = image,
            };
        }

        group.room = null;
        group.vertexIndex = -1;
        group.worldPoint = Vector3.zero;
        group.rect.name = handleName;
        group.rect.SetParent(targetCanvas.transform, false);
        group.rect.sizeDelta = handleSize;
        group.rect.gameObject.SetActive(true);

        if (circularHandleSprite == null)
        {
            circularHandleSprite = CreateCircularSprite(64);
        }

        group.image.sprite = circularHandleSprite;
        group.image.type = Image.Type.Simple;
        group.image.preserveAspect = true;
        group.image.color = handleColor;
        group.image.raycastTarget = false;
        return group;
    }

    private void ReleaseHandleGroup(HandleGroup group)
    {
        if (group?.rect == null)
        {
            return;
        }

        group.room = null;
        group.vertexIndex = -1;
        group.worldPoint = Vector3.zero;
        group.image.color = handleColor;
        group.rect.gameObject.SetActive(false);
        pooledHandleGroups.Add(group);
    }

    private Sprite CreateCircularSprite(int size)
    {
        int safeSize = Mathf.Max(8, size);
        Texture2D texture = new Texture2D(safeSize, safeSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color32[] pixels = new Color32[safeSize * safeSize];
        float radius = (safeSize - 1) * 0.5f;
        float radiusSqr = radius * radius;
        float center = radius;

        for (int y = 0; y < safeSize; y++)
        {
            for (int x = 0; x < safeSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                bool insideCircle = dx * dx + dy * dy <= radiusSqr;
                pixels[y * safeSize + x] = insideCircle ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, safeSize, safeSize), new Vector2(0.5f, 0.5f), safeSize);
    }

    private void EnsureCanvas()
    {
        if (targetCanvas != null)
        {
            return;
        }

        Canvas handleCanvas = LayerUtility.FindCanvasByName(LayerUtility.DefaultHandleCanvasName);
        if (handleCanvas != null)
        {
            targetCanvas = handleCanvas;
            return;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null || canvases[i].name != HandleCanvasName)
            {
                continue;
            }

            targetCanvas = canvases[i];
            return;
        }

        GameObject canvasObject = new GameObject(HandleCanvasName);
        targetCanvas = canvasObject.AddComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void ResolveReferences()
    {
        if (snapManager == null)
        {
            LayerUtility.ResolveObject(ref snapManager);
        }

        if (wallHandleManager == null)
        {
            LayerUtility.ResolveObject(ref wallHandleManager);
        }

        if (roomManager == null)
        {
            LayerUtility.ResolveObject(ref roomManager);
        }

        if (modeManager == null)
        {
            LayerUtility.ResolveObject(ref modeManager);
        }

        if (undoRedoManager == null)
        {
            LayerUtility.ResolveObject(ref undoRedoManager);
        }
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

        dragPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (!hasDragPlane)
        {
            return false;
        }

        if (inputProvider == null || !inputProvider.TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        Ray mouseRay = mainCamera.ScreenPointToRay(pointerScreenPosition);
        if (!dragPlane.Raycast(mouseRay, out float enter))
        {
            return false;
        }

        worldPoint = mouseRay.GetPoint(enter);
        if (hasGridBounds)
        {
            worldPoint.x = Mathf.Clamp(worldPoint.x, gridBounds.min.x, gridBounds.max.x);
            worldPoint.z = Mathf.Clamp(worldPoint.z, gridBounds.min.z, gridBounds.max.z);
        }

        Vector3 anchorPoint = draggingGroup != null ? draggingGroup.worldPoint : worldPoint;

        if (IsRoomHandleGridSnapActive())
        {
            if (snapManager == null)
            {
                return true;
            }

            worldPoint = snapManager.GetSnappedPoint(worldPoint, anchorPoint);
            return true;
        }

        if (!IsRoomHandleWallSnapActive())
        {
            return true;
        }

        CollectWallSegmentSnapCandidates(wallSegmentSnapCandidates);
        if (TryGetClosestRoomWallSnapPoint(worldPoint, wallSegmentSnapCandidates, out Vector3 wallSnapPoint))
        {
            worldPoint.x = wallSnapPoint.x;
            worldPoint.z = wallSnapPoint.z;
        }

        return true;
    }

    private bool IsRoomHandleGridSnapActive()
    {
        return inputProvider != null &&
               (inputProvider.IsKeyPressed(Key.LeftAlt) || inputProvider.IsKeyPressed(Key.RightAlt));
    }

    private bool IsRoomHandleWallSnapActive()
    {
        return inputProvider != null &&
               (inputProvider.IsKeyPressed(Key.LeftShift) || inputProvider.IsKeyPressed(Key.RightShift));
    }

    private void CollectWallSegmentSnapCandidates(List<SnapManager.WallSnapSegment> segments)
    {
        if (segments == null)
        {
            return;
        }

        segments.Clear();
        WallRegistry.CollectWalls(cachedWalls);

        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (!wall.TryGetSnapSegment(0f, minimumRoomEdgeLength, out SnapManager.WallSnapSegment centerSegment))
            {
                continue;
            }

            float halfThickness = Mathf.Max(0f, wall.transform.localScale.x * 0.5f);
            if (halfThickness <= 0.0001f)
            {
                segments.Add(centerSegment);
                continue;
            }

            Vector3 faceOffset = wall.transform.right * halfThickness;
            segments.Add(new SnapManager.WallSnapSegment
            {
                start = centerSegment.start + faceOffset,
                end = centerSegment.end + faceOffset,
            });
            segments.Add(new SnapManager.WallSnapSegment
            {
                start = centerSegment.start - faceOffset,
                end = centerSegment.end - faceOffset,
            });
        }
    }

    private bool TryGetClosestRoomWallSnapPoint(
        Vector3 worldPoint,
        List<SnapManager.WallSnapSegment> segments,
        out Vector3 snapPoint)
    {
        snapPoint = Vector3.zero;
        if (segments == null || segments.Count == 0)
        {
            return false;
        }

        float closestDistanceSqr = float.MaxValue;
        bool found = false;

        for (int i = 0; i < segments.Count; i++)
        {
            SnapManager.WallSnapSegment segment = segments[i];
            Vector3 candidate = GetClosestPointOnSegmentXZ(worldPoint, segment.start, segment.end);
            float dx = worldPoint.x - candidate.x;
            float dz = worldPoint.z - candidate.z;
            float distanceSqr = dx * dx + dz * dz;
            if (distanceSqr >= closestDistanceSqr)
            {
                continue;
            }

            closestDistanceSqr = distanceSqr;
            snapPoint = candidate;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        if (wallSnapDistance > 0f)
        {
            float maxDistanceSqr = wallSnapDistance * wallSnapDistance;
            return closestDistanceSqr <= maxDistanceSqr || segments.Count > 0;
        }

        return true;
    }

    private static Vector3 GetClosestPointOnSegmentXZ(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 a = new Vector2(segmentStart.x, segmentStart.z);
        Vector2 b = new Vector2(segmentEnd.x, segmentEnd.z);
        Vector2 ab = b - a;
        float abSqrMagnitude = ab.sqrMagnitude;
        if (abSqrMagnitude <= 0.0000001f)
        {
            return new Vector3(segmentStart.x, point.y, segmentStart.z);
        }

        float t = Vector2.Dot(p - a, ab) / abSqrMagnitude;
        t = Mathf.Clamp01(t);
        Vector2 projected = a + ab * t;
        return new Vector3(projected.x, point.y, projected.y);
    }

    private bool HasCameraStateChanged()
    {
        return mainCamera.transform.position != lastCameraPosition ||
               mainCamera.transform.rotation != lastCameraRotation ||
               !Mathf.Approximately(mainCamera.orthographicSize, lastCameraOrthoSize);
    }

    private void CacheCameraState()
    {
        lastCameraPosition = mainCamera.transform.position;
        lastCameraRotation = mainCamera.transform.rotation;
        lastCameraOrthoSize = mainCamera.orthographicSize;
    }

    private void SyncModeState()
    {
        HandleModeChanged(modeManager != null ? modeManager.CurrentMode : EditorMode.Default);
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(mainCamera != null, $"{nameof(RoomHandleManager)} requires {nameof(mainCamera)}.", this);
        Debug.Assert(roomManager != null, $"{nameof(RoomHandleManager)} requires {nameof(roomManager)}.", this);
        Debug.Assert(modeManager != null, $"{nameof(RoomHandleManager)} requires {nameof(modeManager)}.", this);
    }

    private void OnDestroy()
    {
        UnbindEvents();
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterGlobalHandler(this);
        }

        for (int i = 0; i < handleGroups.Count; i++)
        {
            if (handleGroups[i]?.rect != null)
            {
                Destroy(handleGroups[i].rect.gameObject);
            }
        }

        for (int i = 0; i < pooledHandleGroups.Count; i++)
        {
            if (pooledHandleGroups[i]?.rect != null)
            {
                Destroy(pooledHandleGroups[i].rect.gameObject);
            }
        }

        if (circularHandleSprite != null)
        {
            Texture2D texture = circularHandleSprite.texture;
            Destroy(circularHandleSprite);
            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }
}
