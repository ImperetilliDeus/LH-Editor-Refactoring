using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class WallOpeningPlacementManager : MonoBehaviour, IEditorModeInputHandler
{
    public enum OpeningPlacementType
    {
        Door,
        Window,
    }

    private struct WallGeometryData
    {
        public Vector3 wallStart;
        public Vector3 wallEnd;
        public Vector3 wallDirection;
        public float wallLength;
        public float wallHeight;
        public float wallThickness;
        public float centerY;
        public int outerStartVertexId;
        public int outerEndVertexId;
        public bool outerStartSplitPoint;
        public bool outerEndSplitPoint;
        public WallVisualState visualState;
    }

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private UndoRedoManager undoRedoManager;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private Canvas previewCanvas;
    [SerializeField] private Button addDoorButton;
    [SerializeField] private Button addWindowButton;
    [SerializeField] private Button addSplitPointButton;
    [SerializeField] private DoorOpeningUIController doorUIController;
    [SerializeField] private WindowOpeningUIController windowUIController;

    [Header("Type Catalog")]
    [SerializeField] private OpeningTypeCatalog openingTypeCatalog;
    [SerializeField] private string openingTypeCatalogResourcePath = "OpeningTypeCatalog";

    [Header("Door Defaults")]
    [SerializeField] private float defaultDoorWidthMillimeters = 900f;
    [SerializeField] private float defaultDoorHeightMillimeters = 2100f;
    [SerializeField] private float defaultDoorDepthMillimeters = 50f;
    [SerializeField] private float defaultDoorBottomOffsetMillimeters = 50f;

    [Header("Window Defaults")]
    [SerializeField] private float defaultWindowWidthMillimeters = 900f;
    [SerializeField] private float defaultWindowHeightMillimeters = 1200f;
    [SerializeField] private float defaultWindowDepthMillimeters = 50f;
    [SerializeField] private float defaultWindowBottomOffsetMillimeters = 50f;

    [Header("Shared Constraints")]
    [SerializeField] private float minimumSideWallMillimeters = 100f;

    private const float MinimumWallSegmentLength = 0.01f;

    private readonly List<WallOpening> cachedOpenings = new List<WallOpening>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<Wall> pendingRoomRefreshRemovedWalls = new List<Wall>();
    private readonly List<OpeningTypeOption> doorTypeOptions = new List<OpeningTypeOption>();
    private readonly List<OpeningTypeOption> windowTypeOptions = new List<OpeningTypeOption>();
    private readonly HashSet<WallOpeningMarkerUI> markerUIs = new HashSet<WallOpeningMarkerUI>();
    private readonly List<WallOpeningMarkerUI> removedMarkerUIs = new List<WallOpeningMarkerUI>();
    private readonly WallOpeningPresentationController presentationController = new WallOpeningPresentationController();
    private readonly WallOpeningMarkerDragState markerDragState = new WallOpeningMarkerDragState();
    private readonly WallOpeningMarkerDragController markerDragController = new WallOpeningMarkerDragController();
    private readonly WallOpeningLayoutRebuildController layoutRebuildController = new WallOpeningLayoutRebuildController();
    private readonly WallOpeningGeometryFactory geometryFactory = new WallOpeningGeometryFactory();
    private readonly OpeningVisualResources visualResources = new OpeningVisualResources();
    private readonly WallOpeningSelectionState selectionState = new WallOpeningSelectionState();
    private Transform deactivatedWallsHolder;
    private bool markerVisualsDirty = true;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private float lastCameraOrthoSize;
    private int lastScreenWidth;
    private int lastScreenHeight;
    private float lastCanvasScaleFactor;
    private Camera lastPreviewCanvasWorldCamera;
    private int lastMarkerGeometryHash;

    public float MinimumSideWallUnits => MillimetersToUnits(minimumSideWallMillimeters);
    public WallOpening SelectedOpening => selectionState.SelectedOpening;
    public bool IsOpeningDetailMenuVisible =>
        (doorUIController != null && doorUIController.IsMenuVisible) ||
        (windowUIController != null && windowUIController.IsMenuVisible);
    public event System.Action<WallOpening> OpeningSelectionChanged
    {
        add => selectionState.SelectionChanged += value;
        remove => selectionState.SelectionChanged -= value;
    }

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

        if (doorUIController == null)
        {
            doorUIController = GetComponentInChildren<DoorOpeningUIController>(true);
        }

        if (windowUIController == null)
        {
            windowUIController = GetComponentInChildren<WindowOpeningUIController>(true);
        }

        LayerUtility.ResolveCanvasByNameOrFirst(ref previewCanvas, LayerUtility.DefaultCanvasName);

        // Create a holder for objects that are deactivated during a drag.
        // This prevents them from being found by GetComponentsInChildren while keeping them
        // alive for the HandleManager's drag operation.
        GameObject tempHolder = new GameObject("DeactivatedWallHolder");
        tempHolder.transform.SetParent(transform, false);
        tempHolder.SetActive(false);
        deactivatedWallsHolder = tempHolder.transform;
        EnsureWallRoot();
        EnsureCachedResources();
        BindButtons();
        BindVisualEvents();
        InitializeOpeningTypeOptions();
        doorUIController?.Initialize(this);
        windowUIController?.Initialize(this);
        SetOpeningDetailMenuVisible(false);
        RefreshOpeningDetailInputs(true);
        CacheCameraState();
        EditorInputManager.Instance.RegisterHandler(EditorMode.DetailEdit, this);
    }

    private void OnValidate()
    {
        ResolveOpeningTypeCatalog();
        RefreshOpeningTypeCatalogCache();
        ApplyOpeningTypeOptionsToUI();
        markerVisualsDirty = true;
    }

    private void OnDestroy()
    {
        UnbindButtons();
        UnbindVisualEvents();
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterHandler(EditorMode.DetailEdit, this);
        }

        visualResources.DestroyCachedMaterials(Destroy);

        if (deactivatedWallsHolder != null)
        {
            Destroy(deactivatedWallsHolder.gameObject);
        }
    }

    private void Update()
    {
        if (markerVisualsDirty || HasCameraStateChanged() || HasMarkerGeometryChanged())
        {
            RefreshOpeningMarkerVisuals();
            markerVisualsDirty = false;
            lastMarkerGeometryHash = CalculateMarkerGeometryHash();
        }

        CacheCameraState();

        if (modeManager != null && !modeManager.IsMode(EditorMode.DetailEdit))
        {
            selectionState.ClearSelectedOpening();
            SetOpeningDetailMenuVisible(false);
            return;
        }

        if (!markerDragState.IsDraggingMarker && SelectedOpening != null && !IsCurrentDetailMenuActive())
        {
            SetOpeningDetailMenuVisible(true);
        }
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        if (modeManager != null && !modeManager.IsMode(EditorMode.DetailEdit))
        {
            return;
        }

        if (SelectedOpening != null && inputFrame.RightPressedThisFrame)
        {
            ClearOpeningSelection();
        }
    }

    public void SplitSelectedWall()
    {
        if (!CanEditOpenings())
        {
            return;
        }

        Wall selectedWallComponent = GetSelectedWallComponent();
        if (selectedWallComponent == null || wallRoot == null)
        {
            return;
        }

        WallOpeningContainer selectedContainer = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (selectedContainer != null)
        {
            SplitContainerWallSegment(selectedContainer, selectedWallComponent);
            return;
        }

        Vector3 startPoint = selectedWallComponent.Data.startPoint;
        Vector3 endPoint = selectedWallComponent.Data.endPoint;
        Vector3 midpoint = (startPoint + endPoint) * 0.5f;
        midpoint.y = startPoint.y;

        if ((midpoint - startPoint).sqrMagnitude <= MinimumWallSegmentLength * MinimumWallSegmentLength ||
            (endPoint - midpoint).sqrMagnitude <= MinimumWallSegmentLength * MinimumWallSegmentLength)
        {
            return;
        }

        EnsureWallRoot();

        WallVisualState visualState = WallVisualState.Capture(selectedWallComponent.gameObject);
        float thickness = selectedWallComponent.transform.localScale.x;
        float height = selectedWallComponent.transform.localScale.y;
        float centerY = selectedWallComponent.transform.position.y;

        GameObject firstWall = CreateStandaloneWallSegment(
            $"{selectedWallComponent.name}_A",
            startPoint,
            midpoint,
            thickness,
            height,
            centerY,
            selectedWallComponent.StartVertexId,
            0,
            selectedWallComponent.SuppressStartHandle,
            false,
            selectedWallComponent.IsStartSplitPoint,
            true,
            visualState);

        if (firstWall == null)
        {
            return;
        }

        GameObject secondWall = CreateStandaloneWallSegment(
            $"{selectedWallComponent.name}_B",
            midpoint,
            endPoint,
            thickness,
            height,
            centerY,
            0,
            selectedWallComponent.EndVertexId,
            false,
            selectedWallComponent.SuppressEndHandle,
            true,
            selectedWallComponent.IsEndSplitPoint,
            visualState);

        if (secondWall == null)
        {
            DestroyCreatedStandaloneSplitWall(firstWall);
            return;
        }

        List<Wall> createdWalls = CollectCreatedStandaloneSplitWalls(firstWall, secondWall);

        if (handleManager != null)
        {
            handleManager.UnregisterWall(selectedWallComponent.gameObject);
        }

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordWallSplit(selectedWallComponent.gameObject, firstWall, secondWall);
        }

        selectedWallComponent.ClearLengthDisplay(wallLengthDisplay);
        Wall removedWall = selectedWallComponent;
        Destroy(selectedWallComponent.gameObject);

        if (createdWalls.Count > 0)
        {
            RoomTopologyEvents.RequestRefreshForWallReplacement(new[] { removedWall }, createdWalls);
        }

        StartCoroutine(RefreshWallRegistryAfterSplit(false));

        if (wallSelectionManager != null)
        {
            wallSelectionManager.SetSelectedWall(firstWall);
        }
    }

    private IEnumerator RefreshWallRegistryAfterSplit(bool isDragging)
    {
        if (isDragging) yield return null; // Only defer if dragging, otherwise refresh immediately
        handleManager?.RefreshRegisteredWalls();
    }

    private static List<Wall> CollectCreatedStandaloneSplitWalls(GameObject firstWall, GameObject secondWall)
    {
        Wall firstWallComponent = firstWall.GetComponent<Wall>();
        Wall secondWallComponent = secondWall.GetComponent<Wall>();
        List<Wall> createdWalls = new List<Wall>();
        if (firstWallComponent != null)
        {
            createdWalls.Add(firstWallComponent);
        }

        if (secondWallComponent != null)
        {
            createdWalls.Add(secondWallComponent);
        }

        return createdWalls;
    }

    private void DestroyCreatedStandaloneSplitWall(GameObject wallObject)
    {
        if (handleManager != null)
        {
            handleManager.UnregisterWall(wallObject);
        }

        Destroy(wallObject);
    }

    public void ApplyContainerSpanFromExternalDrag(
        WallOpeningContainer container,
        Vector3 newStart,
        Vector3 newEnd,
        UndoRedoManager.OpeningLayoutSnapshot baselineSnapshot)
    {
        if (container == null || !baselineSnapshot.hasContainer)
        {
            return;
        }

        container.SetOuterSplitPointFlags(baselineSnapshot.outerStartSplitPoint, baselineSnapshot.outerEndSplitPoint);
        container.SetHandleSuppression(baselineSnapshot.suppressOuterStartHandle, baselineSnapshot.suppressOuterEndHandle);

        float oldLength = Vector3.Distance(baselineSnapshot.wallStart, baselineSnapshot.wallEnd);
        float newLength = Vector3.Distance(newStart, newEnd);
        if (oldLength <= MinimumWallSegmentLength || newLength <= MinimumWallSegmentLength)
        {
            return;
        }

        bool movedStart = (newStart - baselineSnapshot.wallStart).sqrMagnitude > 0.000001f;
        bool movedEnd = (newEnd - baselineSnapshot.wallEnd).sqrMagnitude > 0.000001f;
        Vector3 startDelta = newStart - baselineSnapshot.wallStart;
        Vector3 endDelta = newEnd - baselineSnapshot.wallEnd;
        bool translated = movedStart && movedEnd && (startDelta - endDelta).sqrMagnitude <= 0.000001f;

        if (movedStart && !movedEnd &&
            TryConstrainContainerOuterSplitPointDrag(container, container.OuterStartVertexId, newStart, out Vector3 constrainedStart))
        {
            newStart = constrainedStart;
        }

        if (!movedStart && movedEnd &&
            TryConstrainContainerOuterSplitPointDrag(container, container.OuterEndVertexId, newEnd, out Vector3 constrainedEnd))
        {
            newEnd = constrainedEnd;
        }

        newLength = Vector3.Distance(newStart, newEnd);
        if (newLength <= MinimumWallSegmentLength)
        {
            return;
        }

        container.SetWallSpan(newStart, newEnd);

        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));

        int openingCount = Mathf.Min(
            baselineSnapshot.openings != null ? baselineSnapshot.openings.Length : 0,
            cachedOpenings.Count);
        for (int i = 0; i < openingCount; i++)
        {
            WallOpening opening = cachedOpenings[i];
            UndoRedoManager.OpeningStateSnapshot openingSnapshot = baselineSnapshot.openings[i];
            float nextCenterDistance = ResolveOpeningCenterDistanceAfterSpanChange(
                openingSnapshot,
                oldLength,
                newLength,
                movedStart,
                movedEnd,
                translated);

            opening.SetCenterDistance(ClampOpeningCenterDistance(container, opening, nextCenterDistance, opening.Width));
        }

        RebuildContainer(container, true);

        if (wallSelectionManager != null && wallSelectionManager.SelectedWall == null)
        {
            // After clearing selection during drag, re-select the appropriate segment
            bool startDragged = (newStart - baselineSnapshot.wallStart).sqrMagnitude > 0.001f;
            float preferredDistance = startDragged ? 0f : newLength;
            RefreshSelectedWallForContainer(container, preferredDistance);
        }
    }

    private static float ResolveOpeningCenterDistanceAfterSpanChange(
        UndoRedoManager.OpeningStateSnapshot openingSnapshot,
        float oldLength,
        float newLength,
        bool movedStart,
        bool movedEnd,
        bool translated)
    {
        if (translated || (!movedStart && !movedEnd))
        {
            return openingSnapshot.centerDistance;
        }

        if (movedStart && !movedEnd)
        {
            float distanceFromEnd = oldLength - openingSnapshot.centerDistance;
            return newLength - distanceFromEnd;
        }

        if (!movedStart && movedEnd)
        {
            return openingSnapshot.centerDistance;
        }

        return oldLength > 0.001f ? (openingSnapshot.centerDistance / oldLength) * newLength : newLength * 0.5f;
    }

    internal bool TryConstrainContainerOuterSplitPointDrag(
        WallOpeningContainer container,
        int draggedVertexId,
        Vector3 desiredPoint,
        out Vector3 constrainedPoint)
    {
        constrainedPoint = desiredPoint;
        if (container == null)
        {
            return false;
        }

        if (!TryResolveOuterSplitDragTarget(container, draggedVertexId, out bool draggingStart, out bool draggingEnd))
        {
            return false;
        }

        CollectOpenings(container, cachedOpenings);
        if (cachedOpenings.Count == 0)
        {
            return true;
        }

        float effectiveSideWall = Mathf.Max(MillimetersToUnits(minimumSideWallMillimeters), MinimumWallSegmentLength);
        float minimumRequiredLength = MinimumWallSegmentLength;
        float maxStartDistance = float.PositiveInfinity;
        float minEndDistance = float.NegativeInfinity;

        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
            if (opening == null)
            {
                continue;
            }

            ApplyOuterSplitOpeningConstraint(
                opening,
                draggingStart,
                container.WallLength,
                effectiveSideWall,
                ref minimumRequiredLength,
                ref maxStartDistance,
                ref minEndDistance);
        }

        if (!TryResolveOuterSplitMinimumLengthPoint(
            desiredPoint,
            container.WallStart,
            container.WallEnd,
            draggingStart,
            minimumRequiredLength,
            out constrainedPoint))
        {
            return true;
        }

        Vector3 wallDirection = container.WallDirection;
        float desiredDistance = Vector3.Dot(constrainedPoint - container.WallStart, wallDirection);
        desiredDistance = ClampOuterSplitConstrainedDistance(
            desiredDistance,
            draggingStart,
            draggingEnd,
            maxStartDistance,
            minEndDistance);

        constrainedPoint = container.WallStart + wallDirection * desiredDistance;
        constrainedPoint.y = desiredPoint.y;
        return true;
    }

    private static bool TryResolveOuterSplitDragTarget(
        WallOpeningContainer container,
        int draggedVertexId,
        out bool draggingStart,
        out bool draggingEnd)
    {
        draggingStart = draggedVertexId == container.OuterStartVertexId;
        draggingEnd = draggedVertexId == container.OuterEndVertexId;
        return draggingStart || draggingEnd;
    }

    private static bool TryResolveOuterSplitMinimumLengthPoint(
        Vector3 desiredPoint,
        Vector3 wallStart,
        Vector3 wallEnd,
        bool draggingStart,
        float minimumRequiredLength,
        out Vector3 constrainedPoint)
    {
        constrainedPoint = desiredPoint;

        Vector3 anchorPoint = draggingStart ? wallEnd : wallStart;
        Vector3 desiredOffset = desiredPoint - anchorPoint;
        desiredOffset.y = 0f;
        float desiredLength = desiredOffset.magnitude;
        if (desiredLength >= minimumRequiredLength - 0.0001f)
        {
            return false;
        }

        Vector3 fallbackDirection = draggingStart
            ? wallStart - wallEnd
            : wallEnd - wallStart;
        fallbackDirection.y = 0f;

        Vector3 direction = desiredLength > 0.0001f
            ? desiredOffset / desiredLength
            : (fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector3.right);

        constrainedPoint = anchorPoint + direction * minimumRequiredLength;
        constrainedPoint.y = desiredPoint.y;
        return true;
    }

    private static float ClampOuterSplitConstrainedDistance(
        float desiredDistance,
        bool draggingStart,
        bool draggingEnd,
        float maxStartDistance,
        float minEndDistance)
    {
        if (draggingStart && maxStartDistance != float.PositiveInfinity)
        {
            return Mathf.Min(desiredDistance, maxStartDistance);
        }

        if (draggingEnd && minEndDistance != float.NegativeInfinity)
        {
            return Mathf.Max(desiredDistance, minEndDistance);
        }

        return desiredDistance;
    }

    private static void ApplyOuterSplitOpeningConstraint(
        WallOpening opening,
        bool draggingStart,
        float wallLength,
        float effectiveSideWall,
        ref float minimumRequiredLength,
        ref float maxStartDistance,
        ref float minEndDistance)
    {
        float halfWidth = opening.Width * 0.5f;
        if (draggingStart)
        {
            float distanceFromEnd = wallLength - opening.CenterDistance;
            minimumRequiredLength = Mathf.Max(minimumRequiredLength, distanceFromEnd + halfWidth + effectiveSideWall);
            maxStartDistance = Mathf.Min(maxStartDistance, opening.CenterDistance - halfWidth - effectiveSideWall);
            return;
        }

        minimumRequiredLength = Mathf.Max(minimumRequiredLength, opening.CenterDistance + halfWidth + effectiveSideWall);
        minEndDistance = Mathf.Max(minEndDistance, opening.CenterDistance + halfWidth + effectiveSideWall);
    }

    private void RebuildContainer(WallOpeningContainer container, bool isDragging = false)
    {
        if (isDragging)
        {
            handleManager?.BeginTransientDragRebuild();
        }

        try
        {
            if (!isDragging)
            {
                ClearDeactivatedWallsHolder();
            }

            layoutRebuildController.RebuildContainer(
                container: container,
                pendingRoomRefreshRemovedWalls: pendingRoomRefreshRemovedWalls,
                cachedWalls: cachedWalls,
                cachedOpenings: cachedOpenings,
                collectOpenings: CollectOpenings,
                clearGeneratedContainerVisuals: ClearGeneratedContainerVisuals,
                getSegmentsRoot: GetOrCreateSegmentsRoot,
                createWallSegment: CreateWallSegment,
                updateOpeningVisual: UpdateOpeningVisual,
                collectWalls: (targetTransform, result, includeInactive) => WallHierarchyUtility.CollectWalls(targetTransform, result, includeInactive),
                requestRefreshForWallReplacement: RoomTopologyEvents.RequestRefreshForWallReplacement,
                markMarkerVisualsDirty: MarkMarkerVisualsDirty,
                isDragging: isDragging);
        }
        finally
        {
            if (isDragging)
            {
                handleManager?.EndTransientDragRebuild();
            }
        }
    }

    private void ClearDeactivatedWallsHolder()
    {
        if (deactivatedWallsHolder == null)
        {
            return;
        }

        for (int i = deactivatedWallsHolder.childCount - 1; i >= 0; i--)
        {
            Transform child = deactivatedWallsHolder.GetChild(i);
            if (child == null)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void RefreshSelectedWallForContainer(WallOpeningContainer container, float preferredDistance)
    {
        if (container == null || wallSelectionManager == null)
        {
            return;
        }

        GameObject preferredWall = FindClosestSegmentToDistance(container, preferredDistance);
        if (preferredWall != null)
        {
            wallSelectionManager.SetSelectedWallPreservingOpeningSelection(preferredWall);
        }
    }

    private GameObject FindClosestSegmentToDistance(WallOpeningContainer container, float preferredDistance)
    {
        if (container == null)
        {
            return null;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        GameObject bestWall = null;
        float bestDelta = float.MaxValue;

        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (!CanUseSegmentForSelectionRefresh(wall))
            {
                continue;
            }

            float delta = CalculateSelectionSegmentDistanceDelta(
                wall,
                container.WallStart,
                container.WallDirection,
                preferredDistance);
            if (delta >= bestDelta)
            {
                continue;
            }

            bestDelta = delta;
            bestWall = wall.gameObject;
        }

        return bestWall;
    }

    private static float CalculateSelectionSegmentDistanceDelta(
        Wall wall,
        Vector3 wallStart,
        Vector3 wallDirection,
        float preferredDistance)
    {
        Vector3 midpoint = (wall.Data.startPoint + wall.Data.endPoint) * 0.5f;
        float distanceAlongWall = Vector3.Dot(midpoint - wallStart, wallDirection);
        return Mathf.Abs(distanceAlongWall - preferredDistance);
    }

    private static bool CanUseSegmentForSelectionRefresh(Wall wall)
    {
        return wall != null && !WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall);
    }

    private void CollectOpenings(WallOpeningContainer container, List<WallOpening> results)
    {
        results.Clear();
        if (container == null)
        {
            return;
        }

        WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
        for (int i = 0; i < openings.Length; i++)
        {
            if (openings[i] != null)
            {
                results.Add(openings[i]);
            }
        }
    }

    private void ClearGeneratedContainerVisuals(WallOpeningContainer container, List<Wall> wallsInContainer = null, bool isDragging = false)
    {
        if (container == null)
        {
            return;
        }

        List<Wall> walls = wallsInContainer;
        if (walls == null)
        {
            WallHierarchyUtility.CollectWalls(container.transform, cachedWalls);
            walls = cachedWalls;
        }

        for (int i = 0; i < walls.Count; i++)
        {
            if (walls[i] == null)
            {
                continue;
            }

            ClearSelectedGeneratedWallDuringDrag(walls[i], isDragging);

            walls[i].ClearLengthDisplay(wallLengthDisplay);

            if (isDragging)
            {
                MoveWallToDeactivatedHolderForDrag(walls[i]);
            }
            else
            {
                DestroyGeneratedWall(walls[i]);
            }
        }

        for (int i = container.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = container.transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (ShouldPreserveContainerChildDuringVisualClear(child))
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void ClearSelectedGeneratedWallDuringDrag(Wall wall, bool isDragging)
    {
        if (isDragging && wallSelectionManager != null && wallSelectionManager.SelectedWall == wall.gameObject)
        {
            wallSelectionManager.SetSelectedWallPreservingOpeningSelection(null);
        }
    }

    private static bool ShouldPreserveContainerChildDuringVisualClear(Transform child)
    {
        return child.GetComponent<WallOpening>() != null
            || child.GetComponentInChildren<WallOpening>(true) != null
            || child.name == SegmentGroupName;
    }

    private void MoveWallToDeactivatedHolderForDrag(Wall wall)
    {
        if (handleManager != null)
        {
            handleManager.UnregisterWall(wall.gameObject);
        }

        WallSelectionUIProxy selectionProxy = wall.GetComponent<WallSelectionUIProxy>();
        if (selectionProxy != null)
        {
            selectionProxy.DestroyUI();
            Destroy(selectionProxy);
        }

        wall.transform.SetParent(deactivatedWallsHolder, false);
    }

    private void DestroyGeneratedWall(Wall wall)
    {
        if (handleManager != null)
        {
            handleManager.UnregisterWall(wall.gameObject);
        }

        Destroy(wall.gameObject);
    }

    private GameObject CreateStandaloneWallSegment(
        string segmentName,
        Vector3 startPoint,
        Vector3 endPoint,
        float thickness,
        float height,
        float centerY,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint,
        WallVisualState visualState,
        bool isDragging = false)
    {
        return geometryFactory.CreateStandaloneWallSegment(
            wallRoot,
            GetCubeMesh(),
            wallLengthDisplay,
            handleManager,
            Destroy,
            segmentName,
            startPoint,
            endPoint,
            thickness,
            height,
            centerY,
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            startSplitPoint,
            endSplitPoint,
            visualState, // No change here
            MinimumWallSegmentLength,
            isDragging); // Pass isDragging
    }

    private void CreateFillerSegment(
        Transform parent,
        string fillerName,
        WallOpeningContainer container,
        WallOpening opening,
        float segmentHeight,
        float segmentBottomY)
    {
        geometryFactory.CreateFillerSegment(
            parent,
            GetCubeMesh(),
            fillerName,
            container,
            opening,
            segmentHeight,
            segmentBottomY);
    }

    private GameObject CreateCubeObject(string objectName, Transform parent, bool withCollider)
    {
        return geometryFactory.CreateCubeObject(GetCubeMesh(), objectName, parent, withCollider);
    }

    private void EnsureWallRoot()
    {
        LayerUtility.ResolveWallRoot(ref wallRoot, true);
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref handleManager);
        LayerUtility.ResolveObject(ref wallLengthDisplay);
        LayerUtility.ResolveObject(ref undoRedoManager);
        LayerUtility.ResolveObject(ref modeManager);
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveObject(ref roomManager);
    }

    private float MillimetersToUnits(float millimeters)
    {
        return MeasurementUnits.MillimetersToUnits(millimeters);
    }

    public float UnitsToMillimeters(float units)
    {
        return MeasurementUnits.UnitsToMillimeters(units);
    }

    public float GetSelectedOpeningBottomOffsetMillimeters(float fallbackValue, OpeningPlacementType requiredType)
    {
        if (!CanUseSelectedOpeningBottomOffset(requiredType))
        {
            return fallbackValue;
        }

        return UnitsToMillimeters(SelectedOpening.BottomY - SelectedOpening.Container.WallBottomY);
    }

    private bool CanUseSelectedOpeningBottomOffset(OpeningPlacementType requiredType)
    {
        return SelectedOpening != null
            && SelectedOpening.Type == requiredType
            && SelectedOpening.Container != null;
    }

}
