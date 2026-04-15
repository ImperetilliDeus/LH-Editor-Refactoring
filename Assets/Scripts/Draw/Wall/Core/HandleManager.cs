using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

public partial class HandleManager : MonoBehaviour
{
    private const string HandleCanvasName = "HandleCanvas";

    public event Action WallHierarchyChanged;

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject grid;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private SnapManager snapManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private UndoRedoManager undoRedoManager;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private RoomManager roomManager;

    [Header("Handle UI")]
    [SerializeField] private Vector2 handleSize = new Vector2(14f, 14f);
    [SerializeField] private Color handleColor = new Color(0.16f, 0.66f, 1f, 1f);
    [SerializeField] private Color splitPointHandleColor = new Color(1f, 0.32f, 0.32f, 1f);
    [SerializeField] private Color activeHandleColor = new Color(1f, 0.65f, 0.12f, 1f);
    [SerializeField] private Color activeSplitPointHandleColor = new Color(1f, 0.86f, 0.2f, 1f);
    [SerializeField] private Color snappedHandleColor = new Color(0.28f, 1f, 0.28f, 1f);
    [SerializeField] private int handleCanvasSortingOrder = 200;

    [Header("Drag")]
    [SerializeField] private float minimumWallLength = 0.01f;
    [FormerlySerializedAs("dragStartThresholdPixels")]
    [SerializeField]
    [InspectorName("Ŭ�� ��� ���� (px)")]
    private float clickAllowanceSensitivityPixels = 6f;

    [Header("Merge")]
    [SerializeField] private float endpointMergeThreshold = 0.12f;

    private class WallHandleEntry
    {
        public GameObject wall;
        public Wall wallComponent;
    }

    private class EndpointRef
    {
        public WallHandleEntry entry;
        public bool isStart;
    }

    private class VertexGroup
    {
        public int vertexId;
        public RectTransform handleRect;
        public Image image;
        public readonly List<EndpointRef> endpoints = new List<EndpointRef>();
        public Vector3 worldPoint;
    }

    private readonly Dictionary<int, WallHandleEntry> wallEntries = new Dictionary<int, WallHandleEntry>();
    private readonly Dictionary<int, VertexGroup> groupsByVertexId = new Dictionary<int, VertexGroup>();
    private readonly List<VertexGroup> vertexGroups = new List<VertexGroup>();
    private readonly List<Wall> cachedWalls = new List<Wall>();

    private Plane dragPlane;
    private bool hasDragPlane;
    private float dragPlaneHeight;

    private VertexGroup pendingGroup;
    private VertexGroup draggingGroup;
    private VertexGroup previewSnappedGroup;
    private Vector2 pendingStartMousePosition;
    private Vector3 dragAnchorPoint;
    private readonly Dictionary<GameObject, UndoRedoManager.WallStateSnapshot> dragStartStates = new Dictionary<GameObject, UndoRedoManager.WallStateSnapshot>();
    private readonly List<Vector3> dragSnapCandidates = new List<Vector3>();
    private readonly List<SnapManager.WallSnapSegment> dragWallSegmentSnapCandidates = new List<SnapManager.WallSnapSegment>();
    private readonly List<Transform> containerChildren = new List<Transform>();
    private readonly HashSet<GameObject> affectedWallObjects = new HashSet<GameObject>();
    private readonly List<Wall> affectedWallComponents = new List<Wall>();
    private readonly List<Wall> splitChainWalls = new List<Wall>();
    private readonly List<int> splitChainVertexIds = new List<int>();
    private readonly List<Vector3> splitChainPoints = new List<Vector3>();
    private readonly List<int> removedWallEntryKeys = new List<int>();
    private readonly List<UndoRedoManager.WallStateChangeRecord> dragStateChangeRecords = new List<UndoRedoManager.WallStateChangeRecord>();
    private readonly List<TopViewRenderManager> topViewRenderManagers = new List<TopViewRenderManager>();

    private int nextVertexId = 1;
    private Sprite circularHandleSprite;
    private bool handleLayoutDirty = true;
    private bool handlePositionsDirty = true;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private float lastCameraOrthoSize;
    private bool isDefaultModeActive = true;

    public bool IsDraggingHandle => draggingGroup != null;

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

        EnsureCanvas();
        RefreshDragPlane();
        RegisterExistingWalls();
        RefreshAllGroupWorldPoints();
        CacheCameraState();
        BindModeEvents();
        SyncModeState();
        ValidateConfiguration();
    }

    private void OnValidate()
    {
        handleSize.x = Mathf.Max(4f, handleSize.x);
        handleSize.y = Mathf.Max(4f, handleSize.y);
        minimumWallLength = Mathf.Max(0.01f, minimumWallLength);
        clickAllowanceSensitivityPixels = Mathf.Max(0f, clickAllowanceSensitivityPixels);
        endpointMergeThreshold = Mathf.Max(0.01f, endpointMergeThreshold);
        handleCanvasSortingOrder = Mathf.Max(short.MinValue, Mathf.Min(short.MaxValue, handleCanvasSortingOrder));
    }

    private void Update()
    {
        if (!isDefaultModeActive || mainCamera == null || Mouse.current == null)
        {
            return;
        }

        bool removedDestroyedWalls = CleanupDestroyedWalls();
        if (removedDestroyedWalls)
        {
            handleLayoutDirty = true;
        }

        bool cameraChanged = HasCameraStateChanged();
        if (cameraChanged)
        {
            handlePositionsDirty = true;
        }

        if (handleLayoutDirty)
        {
            RefreshAllGroupWorldPoints();
            UpdateHandlePositions();
            handleLayoutDirty = false;
            handlePositionsDirty = false;
        }
        else if (handlePositionsDirty)
        {
            UpdateHandlePositions();
            handlePositionsDirty = false;
        }

        CacheCameraState();
        HandleDraggingInput();
    }

    public void RegisterWall(GameObject wallObject)
    {
        if (wallObject == null)
        {
            return;
        }

        int key = wallObject.GetInstanceID();
        if (wallEntries.ContainsKey(key))
        {
            return;
        }

        Wall wallComponent = wallObject.GetComponent<Wall>();
        if (wallComponent == null)
        {
            return;
        }

        EnsureWallVertexIds(wallComponent);

        WallHandleEntry entry = new WallHandleEntry
        {
            wall = wallObject,
            wallComponent = wallComponent,
        };

        wallEntries[key] = entry;
        AddEntryToVertexGroup(entry, true);
        AddEntryToVertexGroup(entry, false);
        MarkHandleLayoutDirty();
        WallHierarchyChanged?.Invoke();
    }

    public void UnregisterWall(GameObject wallObject)
    {
        if (wallObject == null)
        {
            return;
        }

        int key = wallObject.GetInstanceID();
        if (!wallEntries.TryGetValue(key, out WallHandleEntry entry))
        {
            return;
        }

        if (draggingGroup != null && GroupContainsWall(draggingGroup, wallObject))
        {
            draggingGroup = null;
            dragStartStates.Clear();
        }

        if (pendingGroup != null && GroupContainsWall(pendingGroup, wallObject))
        {
            pendingGroup = null;
        }

        RemoveEntryFromAllGroups(entry);
        wallEntries.Remove(key);
        MarkHandleLayoutDirty();
        WallHierarchyChanged?.Invoke();
    }

    public void CollectSnapPoints(List<Vector3> points, GameObject ignoreWall = null)
    {
        if (points == null)
        {
            return;
        }

        points.Clear();

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null)
            {
                continue;
            }

            if (ignoreWall != null && GroupContainsWall(group, ignoreWall))
            {
                continue;
            }

            points.Add(group.worldPoint);
        }
    }

    public void UpdatePreviewSnappedHandle(Vector3 snappedPoint, bool snappedByHandlePoint)
    {
        if (IsDraggingHandle)
        {
            return;
        }

        if (!snappedByHandlePoint)
        {
            ClearPreviewSnappedHandle();
            return;
        }

        float thresholdSqr = endpointMergeThreshold * endpointMergeThreshold;
        VertexGroup target = FindClosestGroupByPoint(snappedPoint, thresholdSqr);
        if (target == previewSnappedGroup)
        {
            if (previewSnappedGroup != null)
            {
                SetGroupColor(previewSnappedGroup, GetSnappedColor(previewSnappedGroup));
            }
            return;
        }

        if (previewSnappedGroup != null)
        {
            SetGroupColor(previewSnappedGroup, GetBaseColor(previewSnappedGroup));
        }

        previewSnappedGroup = target;
        if (previewSnappedGroup != null)
        {
            SetGroupColor(previewSnappedGroup, GetSnappedColor(previewSnappedGroup));
        }
    }

    public void ClearPreviewSnappedHandle()
    {
        if (previewSnappedGroup == null)
        {
            return;
        }

        if (previewSnappedGroup != draggingGroup)
        {
            SetGroupColor(previewSnappedGroup, GetBaseColor(previewSnappedGroup));
        }

        previewSnappedGroup = null;
    }

    public void RefreshRegisteredWalls()
    {
        if (CleanupDestroyedWalls())
        {
            handleLayoutDirty = true;
        }

        RebuildGroupsFromEntries();
        MarkHandleLayoutDirty();
    }

    public void RefreshHandleVisuals()
    {
        if (CleanupDestroyedWalls())
        {
            handleLayoutDirty = true;
        }

        MarkHandleLayoutDirty();
    }

    private void HandleDraggingInput()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryBeginPendingDrag(mousePosition);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            EndHandleDrag();
            return;
        }

        if (pendingGroup != null && draggingGroup == null && Mouse.current.leftButton.isPressed)
        {
            float thresholdSqr = clickAllowanceSensitivityPixels * clickAllowanceSensitivityPixels;
            float movedSqr = (mousePosition - pendingStartMousePosition).sqrMagnitude;
            if (movedSqr >= thresholdSqr)
            {
                BeginHandleDragFromPending();
            }
        }

        if (draggingGroup == null)
        {
            return;
        }

        if (!Mouse.current.leftButton.isPressed)
        {
            EndHandleDrag();
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 dragPoint))
        {
            return;
        }

        Vector3 snappedPoint = dragPoint;
        bool snapped = false;
        if (IsSplitPointGroup(draggingGroup) && TryGetSplitPointDragSegment(draggingGroup, out Vector3 segmentStart, out Vector3 segmentEnd))
        {
            snappedPoint = ConstrainPointToSegment(dragPoint, segmentStart, segmentEnd);
            SetGroupColor(draggingGroup, GetActiveColor(draggingGroup));
        }
        else
        {
            dragSnapCandidates.Clear();
            for (int i = 0; i < vertexGroups.Count; i++)
            {
                VertexGroup group = vertexGroups[i];
                if (group == null || group == draggingGroup)
                {
                    continue;
                }

                dragSnapCandidates.Add(group.worldPoint);
            }

            CollectDragWallSegmentSnapCandidates(dragWallSegmentSnapCandidates, draggingGroup);

            snappedPoint = snapManager != null
                ? snapManager.GetSnappedHandleDragPoint(dragPoint, dragAnchorPoint, dragSnapCandidates, mainCamera, dragWallSegmentSnapCandidates, out _, out _)
                : dragPoint;

            snapped = (new Vector2(snappedPoint.x - dragPoint.x, snappedPoint.z - dragPoint.z)).sqrMagnitude > 0.000001f;
            SetGroupColor(draggingGroup, snapped ? GetSnappedColor(draggingGroup) : GetActiveColor(draggingGroup));
        }

        ApplyVertexDrag(draggingGroup, snappedPoint);
        UpdateHandlePositions();
        handlePositionsDirty = false;
    }

    private void TryBeginPendingDrag(Vector2 mousePosition)
    {
        pendingGroup = null;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null || group.handleRect == null)
            {
                continue;
            }

            if (ContainsScreenPoint(group.handleRect, mousePosition))
            {
                pendingGroup = group;
                pendingStartMousePosition = mousePosition;
                return;
            }
        }
    }

    private void BeginHandleDragFromPending()
    {
        if (pendingGroup == null)
        {
            return;
        }

        ClearPreviewSnappedHandle();

        draggingGroup = pendingGroup;
        pendingGroup = null;
        dragAnchorPoint = draggingGroup.worldPoint;

        dragStartStates.Clear();
        CollectAffectedWallsForGroup(draggingGroup, affectedWallObjects, affectedWallComponents);
        for (int i = 0; i < affectedWallComponents.Count; i++)
        {
            Wall wall = affectedWallComponents[i];
            if (wall == null || dragStartStates.ContainsKey(wall.gameObject))
            {
                continue;
            }

            dragStartStates[wall.gameObject] = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject);
        }

        SetGroupColor(draggingGroup, GetActiveColor(draggingGroup));
    }

    private void EndHandleDrag()
    {
        if (draggingGroup == null)
        {
            pendingGroup = null;
            return;
        }

        draggingGroup = TryMergeDraggedGroupToNearby(draggingGroup);

        if (undoRedoManager != null)
        {
            dragStateChangeRecords.Clear();

            foreach (KeyValuePair<GameObject, UndoRedoManager.WallStateSnapshot> pair in dragStartStates)
            {
                GameObject wallObject = pair.Key;
                if (wallObject == null)
                {
                    continue;
                }

                UndoRedoManager.WallStateSnapshot before = pair.Value;
                UndoRedoManager.WallStateSnapshot after = UndoRedoManager.WallStateSnapshot.Capture(wallObject);

                if (!UndoRedoManager.WallStateSnapshot.HasMeaningfulDelta(before, after))
                {
                    continue;
                }

                dragStateChangeRecords.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = before,
                    after = after,
                });
            }

            undoRedoManager.RecordMoveVertexGroup(draggingGroup.vertexId, dragStateChangeRecords);
        }

        SetGroupColor(draggingGroup, GetBaseColor(draggingGroup));
        draggingGroup = null;
        pendingGroup = null;
        dragStartStates.Clear();

        RefreshAllGroupWorldPoints();
        roomManager?.RefreshAllRooms();
        MarkTopViewDirty();
        handlePositionsDirty = true;
    }

    private void ApplyVertexDrag(VertexGroup group, Vector3 newPoint)
    {
        if (group == null)
        {
            return;
        }

        newPoint.y = dragPlaneHeight;

        if (TryApplyOpeningContainerEndpointDrag(group, newPoint))
        {
            RefreshAllGroupWorldPoints();
            roomManager?.RefreshAllRooms();
            MarkTopViewDirty();
            MarkHandlePositionsDirty();
            return;
        }

        CollectAffectedWallsForGroup(group, affectedWallObjects, affectedWallComponents);

        bool appliedSplitChain = false;
        Vector3 appliedPoint = newPoint;
        if (!IsSplitPointGroup(group))
        {
            appliedSplitChain = TryApplySplitPointChainEndpointDrag(group.vertexId, newPoint, out appliedPoint);
        }

        if (!appliedSplitChain)
        {
            WallGeometryService.ApplyVertexMove(affectedWallComponents, group.vertexId, newPoint, dragPlaneHeight, minimumWallLength, wallLengthDisplay);
        }

        RefreshAllGroupWorldPoints();
        group.worldPoint = appliedSplitChain ? appliedPoint : group.worldPoint;
        roomManager?.RefreshAllRooms();
        MarkTopViewDirty();
        MarkHandlePositionsDirty();
    }

    private void MarkTopViewDirty()
    {
        if (topViewRenderManagers.Count == 0)
        {
            TopViewRenderManager[] managers = FindObjectsByType<TopViewRenderManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null)
                {
                    topViewRenderManagers.Add(managers[i]);
                }
            }
        }

        for (int i = topViewRenderManagers.Count - 1; i >= 0; i--)
        {
            TopViewRenderManager manager = topViewRenderManagers[i];
            if (manager == null)
            {
                topViewRenderManagers.RemoveAt(i);
                continue;
            }

            manager.MarkDirty();
        }
    }

    private bool TryApplyOpeningContainerEndpointDrag(VertexGroup group, Vector3 newPoint)
    {
        if (group == null || group.endpoints.Count == 0)
        {
            return false;
        }

        WallOpeningContainer container = null;
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            Wall sourceWall = group.endpoints[i]?.entry?.wallComponent;
            if (sourceWall == null)
            {
                continue;
            }

            WallOpeningContainer candidateContainer = sourceWall.GetComponentInParent<WallOpeningContainer>();
            if (candidateContainer == null)
            {
                continue;
            }

            bool isOuterVertex = group.vertexId > 0 &&
                (group.vertexId == candidateContainer.OuterStartVertexId || group.vertexId == candidateContainer.OuterEndVertexId);
            if (!isOuterVertex)
            {
                continue;
            }

            container = candidateContainer;
            break;
        }

        if (container == null)
        {
            return false;
        }

        bool isDraggingStart = group.vertexId > 0 && group.vertexId == container.OuterStartVertexId;
        bool isDraggingEnd = group.vertexId > 0 && group.vertexId == container.OuterEndVertexId;
        if (!isDraggingStart && !isDraggingEnd)
        {
            return false;
        }

        if (!TryGetContainerOuterWalls(container, out Wall startWall, out Wall endWall))
        {
            return false;
        }

        Vector3 oldStart = startWall.StartPoint;
        Vector3 oldEnd = endWall.EndPoint;
        oldStart.y = dragPlaneHeight;
        oldEnd.y = dragPlaneHeight;

        Vector3 fixedPoint = isDraggingStart ? oldEnd : oldStart;
        Vector3 movedPoint = newPoint;
        Vector3 oldDirection = oldEnd - oldStart;
        oldDirection.y = 0f;
        float oldLength = oldDirection.magnitude;
        if (oldLength < minimumWallLength)
        {
            return false;
        }

        oldDirection /= oldLength;

        Vector3 newDirection = isDraggingStart ? (fixedPoint - movedPoint) : (movedPoint - fixedPoint);
        newDirection.y = 0f;
        float newLength = newDirection.magnitude;
        if (newLength < minimumWallLength)
        {
            return false;
        }

        newDirection /= newLength;
        Vector3 newStart = isDraggingStart ? movedPoint : fixedPoint;
        Vector3 newEnd = isDraggingStart ? fixedPoint : movedPoint;
        container.SetWallSpan(newStart, newEnd);

        containerChildren.Clear();
        for (int i = 0; i < container.transform.childCount; i++)
        {
            Transform child = container.transform.GetChild(i);
            if (child != null)
            {
                containerChildren.Add(child);
            }
        }

        for (int i = 0; i < containerChildren.Count; i++)
        {
            Transform child = containerChildren[i];
            if (child == null)
            {
                continue;
            }

            Vector3 childCenter = child.position;
            float projectedCenter = Vector3.Dot(childCenter - oldStart, oldDirection);
            float distanceFromStart = projectedCenter;
            float distanceFromEnd = oldLength - projectedCenter;
            float newProjectedCenter = isDraggingStart
                ? newLength - distanceFromEnd
                : distanceFromStart;

            Vector3 nextCenter = newStart + newDirection * newProjectedCenter;
            nextCenter.y = childCenter.y;
            child.position = nextCenter;
            child.rotation = Quaternion.LookRotation(newDirection, Vector3.up);

            if (child.TryGetComponent(out WallOpening opening))
            {
                opening.SetCenterDistance(Vector3.Dot(nextCenter - newStart, newDirection));
            }

            if (child.TryGetComponent(out Wall wall))
            {
                Vector3 wallStart = wall.StartPoint;
                Vector3 wallEnd = wall.EndPoint;
                Vector3 nextStart = ResolveContainerEndpoint(
                    wallStart,
                    wall.StartVertexId,
                    isDraggingStart,
                    container,
                    oldStart,
                    oldEnd,
                    newStart,
                    newEnd,
                    oldDirection,
                    newDirection);
                Vector3 nextEnd = ResolveContainerEndpoint(
                    wallEnd,
                    wall.EndVertexId,
                    isDraggingStart,
                    container,
                    oldStart,
                    oldEnd,
                    newStart,
                    newEnd,
                    oldDirection,
                    newDirection);
                wall.TryApplyCurrentProfileAndRefresh(nextStart, nextEnd, minimumWallLength, wallLengthDisplay, false);
            }
        }

        affectedWallComponents.Clear();
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            Wall wall = endpointRef?.entry?.wallComponent;
            if (wall == null || wall.GetComponentInParent<WallOpeningContainer>() == container)
            {
                continue;
            }

            affectedWallComponents.Add(wall);
        }

        if (affectedWallComponents.Count > 0)
        {
            WallGeometryService.ApplyVertexMove(
                affectedWallComponents,
                group.vertexId,
                newPoint,
                dragPlaneHeight,
                minimumWallLength,
                wallLengthDisplay);
        }

        return true;
    }

    private bool TryGetContainerOuterWalls(WallOpeningContainer container, out Wall startWall, out Wall endWall)
    {
        startWall = null;
        endWall = null;
        if (container == null)
        {
            return false;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            if (startWall == null && wall.StartVertexId == container.OuterStartVertexId)
            {
                startWall = wall;
            }

            if (endWall == null && wall.EndVertexId == container.OuterEndVertexId)
            {
                endWall = wall;
            }
        }

        return startWall != null && endWall != null;
    }

    private Vector3 ResolveContainerEndpoint(
        Vector3 oldPoint,
        int endpointVertexId,
        bool isDraggingStart,
        WallOpeningContainer container,
        Vector3 oldStart,
        Vector3 oldEnd,
        Vector3 newStart,
        Vector3 newEnd,
        Vector3 oldDirection,
        Vector3 newDirection)
    {
        oldPoint.y = dragPlaneHeight;

        if (container != null)
        {
            if (isDraggingStart && endpointVertexId == container.OuterStartVertexId)
            {
                return newStart;
            }

            if (!isDraggingStart && endpointVertexId == container.OuterEndVertexId)
            {
                return newEnd;
            }
        }

        if (isDraggingStart)
        {
            float distanceFromEnd = Vector3.Dot(oldEnd - oldPoint, oldDirection);
            Vector3 resolved = newEnd - newDirection * distanceFromEnd;
            resolved.y = dragPlaneHeight;
            return resolved;
        }

        float distanceFromStart = Vector3.Dot(oldPoint - oldStart, oldDirection);
        Vector3 next = newStart + newDirection * distanceFromStart;
        next.y = dragPlaneHeight;
        return next;
    }

    private void EnsureWallVertexIds(Wall wall)
    {
        if (wall == null)
        {
            return;
        }

        Vector3 startPoint = wall.StartPoint;
        Vector3 endPoint = wall.EndPoint;

        int startId = wall.StartVertexId;
        int endId = wall.EndVertexId;

        if (!wall.SuppressStartHandle && startId <= 0)
        {
            startId = FindNearestVertexId(startPoint);
            if (startId <= 0)
            {
                startId = AllocateVertexId();
            }
        }

        if (!wall.SuppressEndHandle && endId <= 0)
        {
            endId = FindNearestVertexId(endPoint);
            if (endId <= 0 || endId == startId)
            {
                endId = AllocateVertexId();
            }
        }

        wall.SetVertexIds(startId, endId);
    }

    private int FindNearestVertexId(Vector3 point)
    {
        float thresholdSqr = endpointMergeThreshold * endpointMergeThreshold;
        int foundId = -1;
        float closestSqr = thresholdSqr;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            float distanceSqr = (new Vector2(group.worldPoint.x - point.x, group.worldPoint.z - point.z)).sqrMagnitude;
            if (distanceSqr > closestSqr)
            {
                continue;
            }

            closestSqr = distanceSqr;
            foundId = group.vertexId;
        }

        return foundId;
    }

    private int AllocateVertexId()
    {
        while (groupsByVertexId.ContainsKey(nextVertexId))
        {
            nextVertexId++;
        }

        return nextVertexId++;
    }

    private void AddEntryToVertexGroup(WallHandleEntry entry, bool isStart)
    {
        if (entry?.wallComponent == null)
        {
            return;
        }

        if ((isStart && entry.wallComponent.SuppressStartHandle) ||
            (!isStart && entry.wallComponent.SuppressEndHandle))
        {
            return;
        }

        int vertexId = isStart ? entry.wallComponent.StartVertexId : entry.wallComponent.EndVertexId;
        if (vertexId <= 0)
        {
            return;
        }

        Vector3 point = isStart ? entry.wallComponent.StartPoint : entry.wallComponent.EndPoint;

        VertexGroup group = GetOrCreateGroup(vertexId, point);
        group.endpoints.Add(new EndpointRef
        {
            entry = entry,
            isStart = isStart,
        });

        UpdateGroupWorldPoint(group);
        SetGroupColor(group, GetBaseColor(group));
    }

    private VertexGroup GetOrCreateGroup(int vertexId, Vector3 initialPoint)
    {
        if (groupsByVertexId.TryGetValue(vertexId, out VertexGroup existing))
        {
            return existing;
        }

        EnsureCanvas();
        RectTransform rect = CreateHandleRect($"Handle_Vertex_{vertexId}", out Image image);

        VertexGroup group = new VertexGroup
        {
            vertexId = vertexId,
            handleRect = rect,
            image = image,
            worldPoint = initialPoint,
        };

        groupsByVertexId[vertexId] = group;
        vertexGroups.Add(group);
        SetGroupColor(group, GetBaseColor(group));

        return group;
    }

    private void RemoveEntryFromAllGroups(WallHandleEntry entry)
    {
        for (int i = vertexGroups.Count - 1; i >= 0; i--)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null)
            {
                continue;
            }

            for (int j = group.endpoints.Count - 1; j >= 0; j--)
            {
                if (group.endpoints[j].entry == entry)
                {
                    group.endpoints.RemoveAt(j);
                }
            }

            if (group.endpoints.Count > 0)
            {
                UpdateGroupWorldPoint(group);
                SetGroupColor(group, GetBaseColor(group));
                continue;
            }

            if (group.handleRect != null)
            {
                Destroy(group.handleRect.gameObject);
            }

            groupsByVertexId.Remove(group.vertexId);
            vertexGroups.RemoveAt(i);
        }
    }

    private void UpdateGroupWorldPoint(VertexGroup group)
    {
        if (group == null || group.endpoints.Count == 0)
        {
            return;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            if (endpointRef?.entry?.wallComponent == null)
            {
                continue;
            }

            Vector3 point = endpointRef.isStart
                ? endpointRef.entry.wallComponent.StartPoint
                : endpointRef.entry.wallComponent.EndPoint;

            sum += point;
            count++;
        }

        if (count == 0)
        {
            return;
        }

        group.worldPoint = sum / count;
        group.worldPoint.y = dragPlaneHeight;
    }

    private void RefreshAllGroupWorldPoints()
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            UpdateGroupWorldPoint(vertexGroups[i]);
        }
    }

    private VertexGroup TryMergeDraggedGroupToNearby(VertexGroup source)
    {
        if (source == null)
        {
            return null;
        }

        float thresholdSqr = endpointMergeThreshold * endpointMergeThreshold;
        VertexGroup target = null;
        float closestSqr = thresholdSqr;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup candidate = vertexGroups[i];
            if (candidate == null || candidate == source)
            {
                continue;
            }

            float distanceSqr = (new Vector2(candidate.worldPoint.x - source.worldPoint.x, candidate.worldPoint.z - source.worldPoint.z)).sqrMagnitude;
            if (distanceSqr > closestSqr)
            {
                continue;
            }

            closestSqr = distanceSqr;
            target = candidate;
        }

        if (target == null)
        {
            return source;
        }

        int oldVertexId = source.vertexId;
        int newVertexId = target.vertexId;

        for (int i = 0; i < source.endpoints.Count; i++)
        {
            EndpointRef endpointRef = source.endpoints[i];
            if (endpointRef?.entry?.wallComponent == null)
            {
                continue;
            }

            Wall wall = endpointRef.entry.wallComponent;
            if (wall.StartVertexId == oldVertexId)
            {
                wall.StartVertexId = newVertexId;
            }

            if (wall.EndVertexId == oldVertexId)
            {
                wall.EndVertexId = newVertexId;
            }
        }

        RemoveGroupById(oldVertexId);
        RebuildGroupsFromEntries();
        if (groupsByVertexId.TryGetValue(newVertexId, out VertexGroup rebuiltTarget))
        {
            return rebuiltTarget;
        }

        return source;
    }

    private VertexGroup FindClosestGroupByPoint(Vector3 point, float thresholdSqr)
    {
        VertexGroup found = null;
        float closestSqr = thresholdSqr;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null)
            {
                continue;
            }

            float dx = group.worldPoint.x - point.x;
            float dz = group.worldPoint.z - point.z;
            float distanceSqr = dx * dx + dz * dz;
            if (distanceSqr > closestSqr)
            {
                continue;
            }

            closestSqr = distanceSqr;
            found = group;
        }

        return found;
    }

    private void RebuildGroupsFromEntries()
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            if (vertexGroups[i]?.handleRect != null)
            {
                Destroy(vertexGroups[i].handleRect.gameObject);
            }
        }

        groupsByVertexId.Clear();
        vertexGroups.Clear();

        foreach (KeyValuePair<int, WallHandleEntry> pair in wallEntries)
        {
            WallHandleEntry entry = pair.Value;
            if (entry?.wallComponent == null)
            {
                continue;
            }

            AddEntryToVertexGroup(entry, true);
            AddEntryToVertexGroup(entry, false);
        }

        RefreshAllGroupWorldPoints();
    }

    private void RemoveGroupById(int vertexId)
    {
        if (!groupsByVertexId.TryGetValue(vertexId, out VertexGroup group))
        {
            return;
        }

        if (group.handleRect != null)
        {
            Destroy(group.handleRect.gameObject);
        }

        groupsByVertexId.Remove(vertexId);
        vertexGroups.Remove(group);
    }

    private bool GroupContainsWall(VertexGroup group, GameObject wallObject)
    {
        if (group == null || wallObject == null)
        {
            return false;
        }

        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            if (endpointRef?.entry?.wall == wallObject)
            {
                return true;
            }
        }

        return false;
    }

    private void CollectAffectedWallsForGroup(VertexGroup group, HashSet<GameObject> wallObjects, List<Wall> walls)
    {
        if (wallObjects == null || walls == null)
        {
            return;
        }

        wallObjects.Clear();
        walls.Clear();

        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            if (endpointRef?.entry?.wall != null)
            {
                wallObjects.Add(endpointRef.entry.wall);
            }
        }

        if (!IsSplitPointGroup(group) && TryBuildSplitChainFromEndpoint(group.vertexId, splitChainWalls, splitChainVertexIds, splitChainPoints))
        {
            for (int i = 0; i < splitChainWalls.Count; i++)
            {
                Wall chainWall = splitChainWalls[i];
                if (chainWall != null)
                {
                    wallObjects.Add(chainWall.gameObject);
                }
            }
        }

        foreach (GameObject wallObject in wallObjects)
        {
            if (wallObject == null)
            {
                continue;
            }

            Wall wall = wallObject.GetComponent<Wall>();
            if (wall != null)
            {
                walls.Add(wall);
            }
        }
    }

    private bool IsSplitPointGroup(VertexGroup group)
    {
        if (group == null || group.endpoints.Count == 0)
        {
            return false;
        }

        bool hasTaggedEndpoint = false;
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            Wall wall = endpointRef?.entry?.wallComponent;
            if (wall == null)
            {
                continue;
            }

            bool isTaggedSplitPoint = endpointRef.isStart ? wall.IsStartSplitPoint : wall.IsEndSplitPoint;
            if (!isTaggedSplitPoint)
            {
                return false;
            }

            hasTaggedEndpoint = true;
        }

        return hasTaggedEndpoint;
    }

    private bool TryApplySplitPointChainEndpointDrag(int draggedVertexId, Vector3 draggedPoint, out Vector3 appliedDraggedPoint)
    {
        appliedDraggedPoint = draggedPoint;
        if (!TryBuildSplitChainFromEndpoint(draggedVertexId, splitChainWalls, splitChainVertexIds, splitChainPoints))
        {
            return false;
        }

        if (splitChainWalls.Count == 0 || splitChainPoints.Count != splitChainWalls.Count + 1)
        {
            return false;
        }

        float originalTotalLength = 0f;
        float minimumRequiredTotalLength = 0f;
        List<float> originalSegmentLengths = new List<float>(splitChainWalls.Count);
        for (int i = 0; i < splitChainWalls.Count; i++)
        {
            float segmentLength = Vector3.Distance(splitChainPoints[i], splitChainPoints[i + 1]);
            originalTotalLength += segmentLength;
            originalSegmentLengths.Add(segmentLength);
        }

        if (originalTotalLength <= 0.0001f)
        {
            return false;
        }

        for (int i = 0; i < splitChainWalls.Count; i++)
        {
            float segmentLength = originalSegmentLengths[i];
            if (segmentLength <= 0.0001f)
            {
                return false;
            }

            float segmentRatio = segmentLength / originalTotalLength;
            minimumRequiredTotalLength = Mathf.Max(minimumRequiredTotalLength, minimumWallLength / segmentRatio);
        }

        Vector3 terminalPoint = splitChainPoints[splitChainPoints.Count - 1];
        terminalPoint.y = dragPlaneHeight;
        draggedPoint.y = dragPlaneHeight;

        Vector3 draggedToTerminal = draggedPoint - terminalPoint;
        draggedToTerminal.y = 0f;
        float newTotalLength = draggedToTerminal.magnitude;
        if (newTotalLength <= 0.0001f)
        {
            Vector3 fallbackDirection = splitChainPoints[0] - terminalPoint;
            fallbackDirection.y = 0f;
            if (fallbackDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            draggedToTerminal = fallbackDirection.normalized * minimumRequiredTotalLength;
            newTotalLength = minimumRequiredTotalLength;
        }
        else if (newTotalLength < minimumRequiredTotalLength)
        {
            draggedToTerminal = draggedToTerminal.normalized * minimumRequiredTotalLength;
            newTotalLength = minimumRequiredTotalLength;
        }

        Vector3 adjustedDraggedPoint = terminalPoint + draggedToTerminal;
        appliedDraggedPoint = adjustedDraggedPoint;
        Vector3 direction = (terminalPoint - adjustedDraggedPoint).normalized;

        splitChainPoints[0] = adjustedDraggedPoint;
        float accumulatedDistance = 0f;
        for (int i = 1; i < splitChainPoints.Count - 1; i++)
        {
            float originalSegmentLength = originalSegmentLengths[i - 1];
            float segmentRatio = originalSegmentLength / originalTotalLength;
            accumulatedDistance += newTotalLength * segmentRatio;
            Vector3 point = adjustedDraggedPoint + direction * accumulatedDistance;
            point.y = dragPlaneHeight;
            splitChainPoints[i] = point;
        }

        splitChainPoints[splitChainPoints.Count - 1] = terminalPoint;

        for (int i = 0; i < splitChainWalls.Count; i++)
        {
            Wall wall = splitChainWalls[i];
            if (wall == null)
            {
                continue;
            }

            int firstVertexId = splitChainVertexIds[i];
            int secondVertexId = splitChainVertexIds[i + 1];
            Vector3 firstPoint = splitChainPoints[i];
            Vector3 secondPoint = splitChainPoints[i + 1];

            Vector3 startPoint;
            Vector3 endPoint;
            if (wall.StartVertexId == firstVertexId && wall.EndVertexId == secondVertexId)
            {
                startPoint = firstPoint;
                endPoint = secondPoint;
            }
            else if (wall.StartVertexId == secondVertexId && wall.EndVertexId == firstVertexId)
            {
                startPoint = secondPoint;
                endPoint = firstPoint;
            }
            else
            {
                startPoint = wall.StartVertexId == firstVertexId ? firstPoint : secondPoint;
                endPoint = wall.EndVertexId == secondVertexId ? secondPoint : firstPoint;
            }

            WallGeometryService.ApplyWallEndpoints(wall, startPoint, endPoint, minimumWallLength, wallLengthDisplay, false);
        }

        return true;
    }

    private bool TryBuildSplitChainFromEndpoint(int draggedVertexId, List<Wall> orderedWalls, List<int> orderedVertexIds, List<Vector3> orderedPoints)
    {
        if (orderedWalls == null || orderedVertexIds == null || orderedPoints == null)
        {
            return false;
        }

        orderedWalls.Clear();
        orderedVertexIds.Clear();
        orderedPoints.Clear();

        GetWallsConnectedToVertex(draggedVertexId, cachedWalls, null);
        if (cachedWalls.Count != 1)
        {
            return false;
        }

        Wall currentWall = cachedWalls[0];
        int currentVertexId = draggedVertexId;
        int nextVertexId = currentWall.GetOppositeVertexId(currentVertexId);
        if (nextVertexId <= 0 || !currentWall.IsSplitPointVertex(nextVertexId))
        {
            return false;
        }

        orderedVertexIds.Add(currentVertexId);
        orderedPoints.Add(GetWallPointForVertex(currentWall, currentVertexId));

        HashSet<Wall> visitedWalls = new HashSet<Wall>();
        while (currentWall != null)
        {
            if (!visitedWalls.Add(currentWall))
            {
                return false;
            }

            nextVertexId = currentWall.GetOppositeVertexId(currentVertexId);
            if (nextVertexId <= 0)
            {
                return false;
            }

            orderedWalls.Add(currentWall);
            orderedVertexIds.Add(nextVertexId);
            orderedPoints.Add(GetWallPointForVertex(currentWall, nextVertexId));

            if (!currentWall.IsSplitPointVertex(nextVertexId))
            {
                return orderedWalls.Count > 1;
            }

            GetWallsConnectedToVertex(nextVertexId, cachedWalls, currentWall);
            if (cachedWalls.Count != 1)
            {
                return false;
            }

            currentVertexId = nextVertexId;
            currentWall = cachedWalls[0];
        }

        return false;
    }

    private void GetWallsConnectedToVertex(int vertexId, List<Wall> results, Wall ignoredWall)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        foreach (KeyValuePair<int, WallHandleEntry> pair in wallEntries)
        {
            Wall wall = pair.Value?.wallComponent;
            if (wall == null || wall == ignoredWall || !wall.ContainsVertexId(vertexId))
            {
                continue;
            }

            results.Add(wall);
        }
    }

    private static Vector3 GetWallPointForVertex(Wall wall, int vertexId)
    {
        if (wall == null)
        {
            return Vector3.zero;
        }

        return wall.StartVertexId == vertexId ? wall.StartPoint : wall.EndPoint;
    }

    private bool TryGetSplitPointDragSegment(VertexGroup group, out Vector3 segmentStart, out Vector3 segmentEnd)
    {
        segmentStart = Vector3.zero;
        segmentEnd = Vector3.zero;
        if (!IsSplitPointGroup(group))
        {
            return false;
        }

        const float uniquePointThresholdSqr = 0.0001f;
        List<Vector3> uniqueOppositePoints = new List<Vector3>();
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            Wall wall = endpointRef?.entry?.wallComponent;
            if (wall == null)
            {
                continue;
            }

            Vector3 oppositePoint = endpointRef.isStart ? wall.EndPoint : wall.StartPoint;
            oppositePoint.y = dragPlaneHeight;

            bool alreadyAdded = false;
            for (int j = 0; j < uniqueOppositePoints.Count; j++)
            {
                if ((uniqueOppositePoints[j] - oppositePoint).sqrMagnitude <= uniquePointThresholdSqr)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                uniqueOppositePoints.Add(oppositePoint);
            }
        }

        if (uniqueOppositePoints.Count < 2)
        {
            return false;
        }

        float maxDistanceSqr = -1f;
        for (int i = 0; i < uniqueOppositePoints.Count - 1; i++)
        {
            for (int j = i + 1; j < uniqueOppositePoints.Count; j++)
            {
                float distanceSqr = (uniqueOppositePoints[i] - uniqueOppositePoints[j]).sqrMagnitude;
                if (distanceSqr <= maxDistanceSqr)
                {
                    continue;
                }

                maxDistanceSqr = distanceSqr;
                segmentStart = uniqueOppositePoints[i];
                segmentEnd = uniqueOppositePoints[j];
            }
        }

        return maxDistanceSqr > minimumWallLength * minimumWallLength;
    }

    private Vector3 ConstrainPointToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 direction = segmentEnd - segmentStart;
        direction.y = 0f;
        float length = direction.magnitude;
        if (length <= 0.0001f)
        {
            point = segmentStart;
            point.y = dragPlaneHeight;
            return point;
        }

        direction /= length;
        float minDistance = Mathf.Min(minimumWallLength, length * 0.5f);
        float maxDistance = Mathf.Max(minDistance, length - minimumWallLength);
        float projectedDistance = Vector3.Dot(point - segmentStart, direction);
        float clampedDistance = Mathf.Clamp(projectedDistance, minDistance, maxDistance);
        Vector3 constrainedPoint = segmentStart + direction * clampedDistance;
        constrainedPoint.y = dragPlaneHeight;
        return constrainedPoint;
    }

    private Color GetBaseColor(VertexGroup group)
    {
        return IsSplitPointGroup(group) ? splitPointHandleColor : handleColor;
    }

    private Color GetActiveColor(VertexGroup group)
    {
        return IsSplitPointGroup(group) ? activeSplitPointHandleColor : activeHandleColor;
    }

    private Color GetSnappedColor(VertexGroup group)
    {
        return IsSplitPointGroup(group) ? activeSplitPointHandleColor : snappedHandleColor;
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (!hasDragPlane)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!dragPlane.Raycast(ray, out float enter))
        {
            return false;
        }

        worldPoint = ray.GetPoint(enter);
        worldPoint.y = dragPlaneHeight;
        return true;
    }

    private void CollectDragWallSegmentSnapCandidates(List<SnapManager.WallSnapSegment> segments, VertexGroup draggingSource)
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

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
            {
                continue;
            }

            GameObject childObject = wall.gameObject;
            if (draggingSource != null && GroupContainsWall(draggingSource, childObject))
            {
                continue;
            }

            if (!wall.TryGetSnapSegment(dragPlaneHeight, minimumWallLength, out SnapManager.WallSnapSegment segment))
            {
                continue;
            }

            segments.Add(segment);
        }
    }

    private bool CleanupDestroyedWalls()
    {
        removedWallEntryKeys.Clear();

        foreach (KeyValuePair<int, WallHandleEntry> pair in wallEntries)
        {
            WallHandleEntry entry = pair.Value;
            if (entry != null && entry.wall != null)
            {
                continue;
            }

            removedWallEntryKeys.Add(pair.Key);
        }

        if (removedWallEntryKeys.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < removedWallEntryKeys.Count; i++)
        {
            wallEntries.Remove(removedWallEntryKeys[i]);
        }

        RebuildGroupsFromEntries();
        WallHierarchyChanged?.Invoke();
        return true;
    }

    private void CancelInteractionState()
    {
        if (draggingGroup != null)
        {
            SetGroupColor(draggingGroup, GetBaseColor(draggingGroup));
        }

        if (previewSnappedGroup != null && previewSnappedGroup != draggingGroup)
        {
            SetGroupColor(previewSnappedGroup, GetBaseColor(previewSnappedGroup));
        }

        pendingGroup = null;
        draggingGroup = null;
        previewSnappedGroup = null;
        dragStartStates.Clear();
    }

    private void RefreshDragPlane()
    {
        hasDragPlane = false;
        float planeY = 0f;

        if (grid != null)
        {
            if (grid.TryGetComponent(out Collider gridCollider))
            {
                planeY = gridCollider.bounds.center.y;
                hasDragPlane = true;
            }
            else if (grid.TryGetComponent(out Renderer gridRenderer))
            {
                planeY = gridRenderer.bounds.center.y;
                hasDragPlane = true;
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

        dragPlaneHeight = planeY;
        dragPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
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
        isDefaultModeActive = mode == EditorMode.Default;
        SetHandlesVisible(isDefaultModeActive);
        if (!isDefaultModeActive)
        {
            CancelInteractionState();
        }

        enabled = isDefaultModeActive;
    }

    private void RegisterExistingWalls()
    {
        if (wallRoot == null)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls, true);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            RegisterWall(wall.gameObject);
        }
    }

    private void MarkHandleLayoutDirty()
    {
        handleLayoutDirty = true;
        handlePositionsDirty = true;
    }

    private void MarkHandlePositionsDirty()
    {
        handlePositionsDirty = true;
    }

    private void CacheCameraState()
    {
        if (mainCamera == null)
        {
            return;
        }

        Transform cameraTransform = mainCamera.transform;
        lastCameraPosition = cameraTransform.position;
        lastCameraRotation = cameraTransform.rotation;
        lastCameraOrthoSize = mainCamera.orthographicSize;
    }

    private bool HasCameraStateChanged()
    {
        if (mainCamera == null)
        {
            return false;
        }

        Transform cameraTransform = mainCamera.transform;
        return cameraTransform.position != lastCameraPosition ||
               cameraTransform.rotation != lastCameraRotation ||
               !Mathf.Approximately(mainCamera.orthographicSize, lastCameraOrthoSize);
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref snapManager);
        LayerUtility.ResolveObject(ref wallLengthDisplay);
        LayerUtility.ResolveObject(ref undoRedoManager);
        LayerUtility.ResolveObject(ref modeManager);
        LayerUtility.ResolveObject(ref roomManager);
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(mainCamera != null, $"{nameof(HandleManager)} requires {nameof(mainCamera)}.", this);
        Debug.Assert(modeManager != null, $"{nameof(HandleManager)} requires {nameof(modeManager)}.", this);
    }
}
