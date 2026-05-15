using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public partial class HandleManager : MonoBehaviour, IEditorModeInputHandler
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
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;

    [Header("Handle UI")]
    [SerializeField] private Vector2 handleSize = new Vector2(14f, 14f);
    [SerializeField] private Color handleColor = new Color(0.16f, 0.66f, 1f, 1f);
    [SerializeField] private Color splitPointHandleColor = new Color(1f, 0.32f, 0.32f, 1f);
    [SerializeField] private Color activeHandleColor = new Color(1f, 0.65f, 0.12f, 1f);
    [SerializeField] private Color activeSplitPointHandleColor = new Color(1f, 0.86f, 0.2f, 1f);
    [SerializeField] private Color snappedHandleColor = new Color(0.28f, 1f, 0.28f, 1f);

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

    private sealed class LinearChainElement
    {
        public Wall wall;
        public WallOpeningContainer container;
        public int startVertexId;
        public int endVertexId;
        public Vector3 startPoint;
        public Vector3 endPoint;

        public bool IsContainer => container != null;
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
    private readonly HashSet<WallOpeningContainer> affectedOpeningContainers = new HashSet<WallOpeningContainer>();
    private readonly HashSet<GameObject> affectedWallObjects = new HashSet<GameObject>();
    private readonly List<Wall> affectedWallComponents = new List<Wall>();
    private readonly List<Wall> splitChainWalls = new List<Wall>();
    private readonly List<int> splitChainVertexIds = new List<int>();
    private readonly List<Vector3> splitChainPoints = new List<Vector3>();
    private readonly List<LinearChainElement> splitChainCandidates = new List<LinearChainElement>();
    private readonly List<LinearChainElement> splitChainElements = new List<LinearChainElement>();
    private readonly List<LinearChainElement> connectedChainElements = new List<LinearChainElement>();
    private readonly List<int> removedWallEntryKeys = new List<int>();
    private readonly List<UndoRedoManager.WallStateChangeRecord> dragStateChangeRecords = new List<UndoRedoManager.WallStateChangeRecord>();

    private IEditorInputProvider inputProvider;
    private int nextVertexId = 1;
    private int suppressActiveDragCancellationDepth;
    private Sprite circularHandleSprite;
    private bool handleLayoutDirty = true;
    private bool handlePositionsDirty = true;
    private bool suppressWallRenaming;
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

        inputProvider = EditorInputManager.Instance.InputProvider;
        ResolveReferences();

        EnsureCanvas();
        RefreshDragPlane();
        RegisterExistingWalls();
        RefreshAllGroupWorldPoints();
        CacheCameraState();
        BindModeEvents();
        SyncModeState();
        EditorInputManager.Instance.RegisterGlobalHandler(this);
        ValidateConfiguration();
    }

    private void OnValidate()
    {
        handleSize.x = Mathf.Max(4f, handleSize.x);
        handleSize.y = Mathf.Max(4f, handleSize.y);
        minimumWallLength = Mathf.Max(0.01f, minimumWallLength);
        clickAllowanceSensitivityPixels = Mathf.Max(0f, clickAllowanceSensitivityPixels);
        endpointMergeThreshold = Mathf.Max(0.01f, endpointMergeThreshold);
    }

    private void Update()
    {
        if (!isDefaultModeActive || mainCamera == null)
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
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        if (!isDefaultModeActive || mainCamera == null || !inputFrame.IsPointerAvailable)
        {
            return;
        }

        HandleDraggingInput(PointerInputFrameUtility.BuildPointerFrame(inputFrame));
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
        NormalizeWallNames();
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

        if (suppressActiveDragCancellationDepth == 0 &&
            draggingGroup != null &&
            GroupContainsWall(draggingGroup, wallObject))
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
        NormalizeWallNames();
        MarkHandleLayoutDirty();
        WallHierarchyChanged?.Invoke();
    }

    public void BeginTransientDragRebuild()
    {
        suppressActiveDragCancellationDepth++;
    }

    public void EndTransientDragRebuild()
    {
        suppressActiveDragCancellationDepth = Mathf.Max(0, suppressActiveDragCancellationDepth - 1);
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
        NormalizeWallNames();
        RefreshWallEndCaps();
        MarkHandleLayoutDirty();
        SetHandlesVisible(isDefaultModeActive);
    }

    public void RebuildRegisteredWallsFromHierarchy()
    {
        CancelInteractionState();
        wallEntries.Clear();
        groupsByVertexId.Clear();
        vertexGroups.Clear();
        RegisterExistingWalls();
        RefreshWallEndCaps();
        RefreshAllGroupWorldPoints();
        UpdateHandlePositions();
        handleLayoutDirty = false;
        handlePositionsDirty = false;
        SetHandlesVisible(isDefaultModeActive);
        WallHierarchyChanged?.Invoke();
    }

    public void RefreshHandleVisuals()
    {
        if (CleanupDestroyedWalls())
        {
            handleLayoutDirty = true;
        }

        RefreshWallEndCaps();
        MarkHandleLayoutDirty();
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
            hasTaggedEndpoint |= isTaggedSplitPoint;
        }

        return hasTaggedEndpoint;
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

    private bool TryGetMouseWorldPoint(Vector2 pointerScreenPosition, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (!hasDragPlane)
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

    private void CollectDragWallSegmentSnapCandidates(Vector3 aroundPoint, List<SnapManager.WallSnapSegment> segments, VertexGroup draggingSource)
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

        snapManager.CollectNearbyWallSegmentSnapCandidates(
            aroundPoint,
            dragPlaneHeight,
            minimumWallLength,
            segments,
            wallRoot,
            wall => wall != null && draggingSource != null && GroupContainsWall(draggingSource, wall.gameObject));
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

        suppressWallRenaming = true;
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

        suppressWallRenaming = false;
        NormalizeWallNames();
    }

    private void NormalizeWallNames()
    {
        if (suppressWallRenaming)
        {
            return;
        }

        WallNamingUtility.NormalizeWallNames(wallRoot);
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
        LayerUtility.ResolveObject(ref wallOpeningPlacementManager);
    }

    private void RefreshWallEndCaps()
    {
        foreach (KeyValuePair<int, WallHandleEntry> pair in wallEntries)
        {
            Wall wall = pair.Value?.wallComponent;
            if (wall != null)
            {
                wall.RefreshEndCapVisuals();
            }
        }
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(mainCamera != null, $"{nameof(HandleManager)} requires {nameof(mainCamera)}.", this);
        Debug.Assert(modeManager != null, $"{nameof(HandleManager)} requires {nameof(modeManager)}.", this);
    }

    private bool IsVertexInContainer(WallOpeningContainer container, int vertexId)
    {
        if (container == null || vertexId <= 0)
        {
            return false;
        }

        if (container.OuterStartVertexId == vertexId || container.OuterEndVertexId == vertexId)
        {
            return true;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>();
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null && walls[i].ContainsVertexId(vertexId))
            {
                return true;
            }
        }
        return false;
    }
}
