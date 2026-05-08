using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class WallOpeningPlacementManager : MonoBehaviour, IEditorModeInputHandler
{
    public enum MarkerPlacementMode
    {
        OffsetFromOpening,
        CenterOnOpening,
    }

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
        public WallVisualState visualState;
    }

    [System.Serializable]
    private class MarkerVariantDefinition
    {
        [SerializeField] private string typeName;
        [SerializeField] private GameObject markerPrefab;
        [SerializeField] private Vector2 scaleMultiplier = Vector2.one;
        [SerializeField] private MarkerPlacementMode placementMode = MarkerPlacementMode.OffsetFromOpening;

        public string TypeName => typeName;
        public GameObject MarkerPrefab => markerPrefab;
        public Vector2 ScaleMultiplier => scaleMultiplier;
        public MarkerPlacementMode PlacementMode => placementMode;

        public void ClampValues()
        {
            scaleMultiplier = new Vector2(
                Mathf.Max(0.01f, scaleMultiplier.x),
                Mathf.Max(0.01f, scaleMultiplier.y));
        }
    }

    [System.Serializable]
    private class DoorMarkerVariant : MarkerVariantDefinition
    {
    }

    [System.Serializable]
    private class WindowMarkerVariant : MarkerVariantDefinition
    {
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

    [Header("Marker UI")]
    [SerializeField] private GameObject doorMarkerPrefab;
    [SerializeField] private GameObject windowMarkerPrefab;
    [SerializeField] private Vector2 doorMarkerScaleMultiplier = Vector2.one;
    [SerializeField] private Vector2 windowMarkerScaleMultiplier = Vector2.one;
    [SerializeField] private MarkerPlacementMode doorMarkerPlacementMode = MarkerPlacementMode.OffsetFromOpening;
    [SerializeField] private MarkerPlacementMode windowMarkerPlacementMode = MarkerPlacementMode.OffsetFromOpening;
    [SerializeField] private List<DoorMarkerVariant> doorMarkerVariants = new List<DoorMarkerVariant>();
    [SerializeField] private List<WindowMarkerVariant> windowMarkerVariants = new List<WindowMarkerVariant>();

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
    private Mesh cachedCubeMesh;
    private Material cachedDoorMaterial;
    private Material cachedWindowMaterial;
    private readonly WallOpeningSelectionState selectionState = new WallOpeningSelectionState();
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
        doorMarkerScaleMultiplier = new Vector2(
            Mathf.Max(0.01f, doorMarkerScaleMultiplier.x),
            Mathf.Max(0.01f, doorMarkerScaleMultiplier.y));
        windowMarkerScaleMultiplier = new Vector2(
            Mathf.Max(0.01f, windowMarkerScaleMultiplier.x),
            Mathf.Max(0.01f, windowMarkerScaleMultiplier.y));
        ValidateMarkerVariants(doorMarkerVariants);
        ValidateMarkerVariants(windowMarkerVariants);
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

        if (cachedDoorMaterial != null)
        {
            Destroy(cachedDoorMaterial);
        }

        if (cachedWindowMaterial != null)
        {
            Destroy(cachedWindowMaterial);
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

        if (selectedWallComponent.GetComponentInParent<WallOpeningContainer>() != null)
        {
            Debug.LogWarning("Wall split does not support walls inside opening containers yet.", selectedWallComponent);
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
            if (handleManager != null)
            {
                handleManager.UnregisterWall(firstWall);
            }

            Destroy(firstWall);
            return;
        }

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

        StartCoroutine(RefreshWallRegistryAfterSplit());

        if (wallSelectionManager != null)
        {
            wallSelectionManager.SetSelectedWall(firstWall);
        }
    }

    private IEnumerator RefreshWallRegistryAfterSplit()
    {
        yield return null;
        handleManager?.RefreshRegisteredWalls();
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
            float nextCenterDistance;

            if (translated || (!movedStart && !movedEnd))
            {
                nextCenterDistance = openingSnapshot.centerDistance;
            }
            else if (movedStart && !movedEnd)
            {
                float distanceFromEnd = oldLength - openingSnapshot.centerDistance;
                nextCenterDistance = newLength - distanceFromEnd;
            }
            else if (!movedStart && movedEnd)
            {
                nextCenterDistance = openingSnapshot.centerDistance;
            }
            else
            {
                nextCenterDistance = (openingSnapshot.centerDistance / oldLength) * newLength;
            }

            opening.SetCenterDistance(ClampOpeningCenterDistance(container, opening, nextCenterDistance, opening.Width));
        }

        RebuildContainer(container);
    }

    private void RebuildContainer(WallOpeningContainer container)
    {
        layoutRebuildController.RebuildContainer(
            container,
            pendingRoomRefreshRemovedWalls,
            cachedWalls,
            cachedOpenings,
            CollectOpenings,
            ClearGeneratedContainerVisuals,
            GetOrCreateSegmentsRoot,
            CreateWallSegment,
            UpdateOpeningVisual,
            (targetTransform, result, includeInactive) => WallHierarchyUtility.CollectWalls(targetTransform, result, includeInactive),
            RoomTopologyEvents.RequestRefreshForWallReplacement,
            MarkMarkerVisualsDirty);
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
            if (wall == null || WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                continue;
            }

            Vector3 midpoint = (wall.Data.startPoint + wall.Data.endPoint) * 0.5f;
            float distanceAlongWall = Vector3.Dot(midpoint - container.WallStart, container.WallDirection);
            float delta = Mathf.Abs(distanceAlongWall - preferredDistance);
            if (delta >= bestDelta)
            {
                continue;
            }

            bestDelta = delta;
            bestWall = wall.gameObject;
        }

        return bestWall;
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

    private void ClearGeneratedContainerVisuals(WallOpeningContainer container, List<Wall> wallsInContainer = null)
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

            if (handleManager != null)
            {
                handleManager.UnregisterWall(walls[i].gameObject);
            }

            walls[i].ClearLengthDisplay(wallLengthDisplay);
            Destroy(walls[i].gameObject);
        }

        for (int i = container.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = container.transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponent<WallOpening>() != null)
            {
                continue;
            }

            if (child.GetComponentInChildren<WallOpening>(true) != null)
            {
                continue;
            }

            if (child.name == SegmentGroupName)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
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
        WallVisualState visualState)
    {
        return geometryFactory.CreateStandaloneWallSegment(
            wallRoot,
            cachedCubeMesh,
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
            visualState,
            MinimumWallSegmentLength);
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
        LayerUtility.ResolveTransformByName(ref wallRoot, LayerUtility.DefaultWallRootName, true);
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
        if (SelectedOpening == null || SelectedOpening.Type != requiredType || SelectedOpening.Container == null)
        {
            return fallbackValue;
        }

        return UnitsToMillimeters(SelectedOpening.BottomY - SelectedOpening.Container.WallBottomY);
    }

}
