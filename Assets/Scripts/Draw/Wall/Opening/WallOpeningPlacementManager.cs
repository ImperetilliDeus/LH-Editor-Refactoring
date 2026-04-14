using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public partial class WallOpeningPlacementManager : MonoBehaviour
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
        public Material wallMaterial;
        public Material wallTopMaterial;
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
    private const float UnitToMillimeters = 100f;

    private readonly List<WallOpening> cachedOpenings = new List<WallOpening>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<Wall> pendingRoomRefreshRemovedWalls = new List<Wall>();
    private readonly HashSet<WallOpeningMarkerUI> markerUIs = new HashSet<WallOpeningMarkerUI>();
    private readonly List<WallOpeningMarkerUI> removedMarkerUIs = new List<WallOpeningMarkerUI>();
    private Mesh cachedCubeMesh;
    private Material cachedDoorMaterial;
    private Material cachedWindowMaterial;
    private UndoRedoManager.OpeningLayoutSnapshot openingDragStartSnapshot;
    private bool hasOpeningDragStartSnapshot;
    private bool isDraggingMarker;
    private WallOpening selectedOpening;
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
    public WallOpening SelectedOpening => selectedOpening;
    public bool IsOpeningDetailMenuVisible =>
        (doorUIController != null && doorUIController.IsMenuVisible) ||
        (windowUIController != null && windowUIController.IsMenuVisible);
    public event System.Action<WallOpening> OpeningSelectionChanged;

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

        if (previewCanvas == null)
        {
            previewCanvas = LayerUtility.FindCanvasByNameOrFirst("_Screen");
        }

        EnsureWallRoot();
        EnsureCachedResources();
        BindButtons();
        doorUIController?.Initialize(this);
        windowUIController?.Initialize(this);
        SetOpeningDetailMenuVisible(false);
        RefreshOpeningDetailInputs(true);
        CacheCameraState();
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
        markerVisualsDirty = true;
    }

    private void OnDestroy()
    {
        UnbindButtons();

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
            selectedOpening = null;
            SetOpeningDetailMenuVisible(false);
            return;
        }

        if (selectedOpening != null &&
            Mouse.current != null &&
            Mouse.current.rightButton.wasPressedThisFrame)
        {
            ClearOpeningSelection();
            return;
        }

        if (!isDraggingMarker && selectedOpening != null && !IsCurrentDetailMenuActive())
        {
            SetOpeningDetailMenuVisible(true);
        }
    }

    public void CreateDoorOnSelectedWall()
    {
        CreateOpeningOnSelectedWall(OpeningPlacementType.Door);
    }

    public void CreateWindowOnSelectedWall()
    {
        CreateOpeningOnSelectedWall(OpeningPlacementType.Window);
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

        Vector3 startPoint = selectedWallComponent.StartPoint;
        Vector3 endPoint = selectedWallComponent.EndPoint;
        Vector3 midpoint = (startPoint + endPoint) * 0.5f;
        midpoint.y = startPoint.y;

        if ((midpoint - startPoint).sqrMagnitude <= MinimumWallSegmentLength * MinimumWallSegmentLength ||
            (endPoint - midpoint).sqrMagnitude <= MinimumWallSegmentLength * MinimumWallSegmentLength)
        {
            return;
        }

        EnsureWallRoot();

        MeshRenderer selectedRenderer = selectedWallComponent.GetComponent<MeshRenderer>();
        Material wallMaterial = selectedRenderer != null ? selectedRenderer.sharedMaterial : null;
        Material topMaterial = selectedWallComponent.GetTopMaterial();
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
            wallMaterial,
            topMaterial);

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
            wallMaterial,
            topMaterial);

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

        if (roomManager != null && createdWalls.Count > 0)
        {
            roomManager.RefreshRoomsForWallReplacement(new[] { removedWall }, createdWalls);
        }

        if (wallSelectionManager != null)
        {
            wallSelectionManager.SetSelectedWall(firstWall);
        }
    }

    public void SelectOpening(WallOpening opening)
    {
        if (selectedOpening == opening)
        {
            SetOpeningDetailMenuVisible(opening != null);
            RefreshOpeningDetailInputs(true);
            MarkMarkerVisualsDirty();
            return;
        }

        selectedOpening = opening;
        SetOpeningDetailMenuVisible(opening != null);
        RefreshOpeningDetailInputs(true);
        MarkMarkerVisualsDirty();
        OpeningSelectionChanged?.Invoke(selectedOpening);
    }

    public void ClearOpeningSelection()
    {
        if (selectedOpening == null)
        {
            SetOpeningDetailMenuVisible(false);
            RefreshOpeningDetailInputs(true);
            MarkMarkerVisualsDirty();
            return;
        }

        selectedOpening = null;
        SetOpeningDetailMenuVisible(false);
        RefreshOpeningDetailInputs(true);
        MarkMarkerVisualsDirty();
        OpeningSelectionChanged?.Invoke(null);
    }

    public void DeleteSelectedOpening()
    {
        if (!CanEditOpenings() || selectedOpening == null)
        {
            return;
        }

        WallOpening openingToDelete = selectedOpening;
        WallOpeningContainer container = openingToDelete.Container;
        if (container == null)
        {
            ClearOpeningSelection();
            Destroy(openingToDelete.gameObject);
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = CaptureLayoutSnapshot(container);
        bool hasRemainingOpenings = HasOtherOpenings(container, openingToDelete);
        ClearOpeningSelection();
        openingToDelete.transform.SetParent(null, false);
        openingToDelete.gameObject.SetActive(false);
        Destroy(openingToDelete.gameObject);
        GameObject restoredWall = hasRemainingOpenings ? null : RestoreContainerIfEmpty(container);
        UndoRedoManager.OpeningLayoutSnapshot afterSnapshot;

        if (restoredWall != null)
        {
            afterSnapshot = CaptureLayoutSnapshot(restoredWall.GetComponent<Wall>());
            if (wallSelectionManager != null)
            {
                wallSelectionManager.SetSelectedWallPreservingOpeningSelection(restoredWall);
            }
        }
        else
        {
            RebuildContainer(container);
            RefreshSelectedWallForContainer(container, container.WallLength * 0.5f);
            afterSnapshot = CaptureLayoutSnapshot(container);
        }

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, afterSnapshot);
        }
    }

    public void RegisterMarkerUI(WallOpeningMarkerUI markerUI)
    {
        if (markerUI == null)
        {
            return;
        }

        markerUIs.Add(markerUI);
        MarkMarkerVisualsDirty();
    }

    public void UnregisterMarkerUI(WallOpeningMarkerUI markerUI)
    {
        if (markerUI == null)
        {
            return;
        }

        markerUIs.Remove(markerUI);
        MarkMarkerVisualsDirty();
    }

    public void MarkMarkerVisualsDirty()
    {
        markerVisualsDirty = true;
    }

    public void BeginMarkerDrag(WallOpening opening)
    {
        if (!CanEditOpenings() || opening == null)
        {
            return;
        }

        selectedOpening = opening;
        isDraggingMarker = true;
        SetOpeningDetailMenuVisible(true);
        RefreshOpeningDetailInputs(true);
        openingDragStartSnapshot = CaptureLayoutSnapshot(opening.Container);
        hasOpeningDragStartSnapshot = true;
    }

    public void DragMarker(WallOpening opening, Vector2 screenPosition)
    {
        if (!CanEditOpenings() || opening == null)
        {
            return;
        }

        WallOpeningContainer container = opening.Container;
        if (container == null || mainCamera == null)
        {
            return;
        }

        Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, container.WallPlaneY, 0f));
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (!dragPlane.Raycast(ray, out float enter))
        {
            return;
        }

        Vector3 point = ray.GetPoint(enter);
        Vector3 direction = container.WallDirection;
        float projectedDistance = Vector3.Dot(point - container.WallStart, direction);
        float clampedDistance = ClampOpeningCenterDistance(container, opening, projectedDistance);
        opening.SetCenterDistance(clampedDistance);
        RebuildContainer(container);
    }

    public void EndMarkerDrag(WallOpening opening)
    {
        if (opening == null)
        {
            isDraggingMarker = false;
            return;
        }

        isDraggingMarker = false;
        RebuildContainer(opening.Container);
        RefreshSelectedWallForContainer(opening.Container, opening.CenterDistance);

        if (hasOpeningDragStartSnapshot && undoRedoManager != null)
        {
            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = CaptureLayoutSnapshot(opening.Container);
            undoRedoManager.RecordOpeningLayoutChange(openingDragStartSnapshot, afterSnapshot);
        }

        hasOpeningDragStartSnapshot = false;
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

    private void CreateOpeningOnSelectedWall(OpeningPlacementType type)
    {
        if (!CanEditOpenings())
        {
            return;
        }

        Wall selectedWall = GetSelectedWallComponent();
        if (selectedWall == null)
        {
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = CaptureLayoutSnapshot(selectedWall);

        Transform containerTransform = GetOrCreateOpeningContainer(selectedWall);
        WallOpeningContainer container = containerTransform != null ? containerTransform.GetComponent<WallOpeningContainer>() : null;
        if (container == null)
        {
            return;
        }

        float openingWidth = MillimetersToUnits(type == OpeningPlacementType.Door ? defaultDoorWidthMillimeters : defaultWindowWidthMillimeters);
        float openingHeight = MillimetersToUnits(type == OpeningPlacementType.Door ? defaultDoorHeightMillimeters : defaultWindowHeightMillimeters);
        float defaultDepthMillimeters = type == OpeningPlacementType.Door
            ? defaultDoorDepthMillimeters
            : defaultWindowDepthMillimeters;
        float defaultBottomOffsetMillimeters = type == OpeningPlacementType.Door
            ? this.defaultDoorBottomOffsetMillimeters
            : defaultWindowBottomOffsetMillimeters;

        float openingDepth = Mathf.Min(MillimetersToUnits(defaultDepthMillimeters), container.WallThickness);
        float bottomOffset = MillimetersToUnits(defaultBottomOffsetMillimeters);
        float minimumSideWall = MillimetersToUnits(minimumSideWallMillimeters);

        if (container.WallLength < openingWidth + minimumSideWall * 2f)
        {
            return;
        }

        float openingBottomY = container.WallBottomY + bottomOffset;

        float maxAllowedHeight = container.WallTopY - openingBottomY;
        openingHeight = Mathf.Min(openingHeight, maxAllowedHeight);
        if (openingHeight <= 0.01f)
        {
            return;
        }

        float centerDistance = ClampOpeningCenterDistance(container, null, container.WallLength * 0.5f, openingWidth);

        GameObject openingObject = new GameObject(type == OpeningPlacementType.Door ? "Door" : "Window");
        openingObject.transform.SetParent(container.transform, false);
        LayerUtility.ApplyLayer(
            openingObject,
            type == OpeningPlacementType.Door ? LayerUtility.DoorLayerName : LayerUtility.WindowLayerName,
            false);
        WallOpening opening = openingObject.AddComponent<WallOpening>();
        opening.Initialize(
            this,
            container,
            type,
            type == OpeningPlacementType.Door ? GetCurrentDoorTypeKey() : string.Empty,
            type == OpeningPlacementType.Window ? GetCurrentWindowTypeKey() : string.Empty,
            false,
            false,
            centerDistance,
            openingWidth,
            openingHeight,
            openingDepth,
            openingBottomY);

        SelectOpening(opening);
        RebuildContainer(container);
        RefreshSelectedWallForContainer(container, opening.CenterDistance);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, CaptureLayoutSnapshot(container));
        }
    }

    public UndoRedoManager.OpeningLayoutSnapshot CaptureLayoutSnapshot(Wall wall)
    {
        if (wall == null)
        {
            return default;
        }

        Transform parent = wall.transform.parent;
        if (parent != null && parent.TryGetComponent(out WallOpeningContainer container))
        {
            return CaptureLayoutSnapshot(container);
        }

        return new UndoRedoManager.OpeningLayoutSnapshot
        {
            hasContainer = false,
            layoutName = wall.name,
            wallSnapshot = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject),
        };
    }

    public UndoRedoManager.OpeningLayoutSnapshot CaptureLayoutSnapshot(WallOpeningContainer container)
    {
        if (container == null)
        {
            return default;
        }

        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));
        UndoRedoManager.OpeningStateSnapshot[] openingSnapshots = new UndoRedoManager.OpeningStateSnapshot[cachedOpenings.Count];
        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
            openingSnapshots[i] = new UndoRedoManager.OpeningStateSnapshot
            {
                type = opening.Type,
                doorTypeKey = opening.DoorTypeKey,
                windowTypeKey = opening.WindowTypeKey,
                doorOpensRight = opening.DoorOpensRight,
                doorVerticalFlip = opening.DoorVerticalFlip,
                centerDistance = opening.CenterDistance,
                width = opening.Width,
                height = opening.Height,
                depth = opening.Depth,
                bottomY = opening.BottomY,
            };
        }

        return new UndoRedoManager.OpeningLayoutSnapshot
        {
            hasContainer = true,
            layoutName = container.name,
            wallStart = container.WallStart,
            wallEnd = container.WallEnd,
            wallThickness = container.WallThickness,
            wallHeight = container.WallHeight,
            centerY = container.CenterY,
            wallMaterial = container.WallMaterial,
            wallTopMaterial = container.WallTopMaterial,
            outerStartVertexId = container.OuterStartVertexId,
            outerEndVertexId = container.OuterEndVertexId,
            suppressOuterStartHandle = container.SuppressOuterStartHandle,
            suppressOuterEndHandle = container.SuppressOuterEndHandle,
            openings = openingSnapshots,
        };
    }

    public void ApplyLayoutSnapshot(UndoRedoManager.OpeningLayoutSnapshot target, UndoRedoManager.OpeningLayoutSnapshot current)
    {
        RemoveLayout(current);
        RemoveLayout(target);

        if (!target.hasContainer)
        {
            if (target.wallSnapshot.wallObject == null && string.IsNullOrEmpty(target.wallSnapshot.name))
            {
                return;
            }

            GameObject restoredWall = CreateRestoredWall(target.wallSnapshot);
            if (restoredWall != null && handleManager != null)
            {
                handleManager.RegisterWall(restoredWall);
            }

            return;
        }

        int openingCount = target.openings != null ? target.openings.Length : 0;
        if (openingCount == 0)
        {
            GameObject restoredWall = CreateRestoredWall(BuildWallSnapshotFromContainer(target));
            if (restoredWall != null && handleManager != null)
            {
                handleManager.RegisterWall(restoredWall);
            }

            return;
        }

        GameObject containerObject = new GameObject(target.layoutName);
        containerObject.transform.SetParent(wallRoot, false);
        containerObject.transform.position = Vector3.zero;
        containerObject.transform.rotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;

        WallOpeningContainer container = containerObject.AddComponent<WallOpeningContainer>();
        container.Initialize(
            target.wallStart,
            target.wallEnd,
            target.wallThickness,
            target.wallHeight,
            target.centerY,
            target.wallMaterial,
            target.wallTopMaterial,
            target.outerStartVertexId,
            target.outerEndVertexId,
            target.suppressOuterStartHandle,
            target.suppressOuterEndHandle);

        for (int i = 0; i < openingCount; i++)
        {
            UndoRedoManager.OpeningStateSnapshot openingSnapshot = target.openings[i];
            GameObject openingObject = new GameObject(openingSnapshot.type == OpeningPlacementType.Door ? "Door" : "Window");
            openingObject.transform.SetParent(container.transform, false);
            LayerUtility.ApplyLayer(
                openingObject,
                openingSnapshot.type == OpeningPlacementType.Door ? LayerUtility.DoorLayerName : LayerUtility.WindowLayerName,
                false);
            WallOpening opening = openingObject.AddComponent<WallOpening>();
            opening.Initialize(
                this,
                container,
                openingSnapshot.type,
                openingSnapshot.doorTypeKey,
                openingSnapshot.windowTypeKey,
                openingSnapshot.doorOpensRight,
                openingSnapshot.doorVerticalFlip,
                openingSnapshot.centerDistance,
                openingSnapshot.width,
                openingSnapshot.height,
                openingSnapshot.depth,
                openingSnapshot.bottomY);
        }

        RebuildContainer(container);
    }

    public void RebuildOpeningContainer(WallOpeningContainer container)
    {
        RebuildContainer(container);
    }

    public void SelectPreferredWallForContainer(WallOpeningContainer container, float preferredDistance)
    {
        RefreshSelectedWallForContainer(container, preferredDistance);
    }

    private void RebuildContainer(WallOpeningContainer container)
    {
        if (container == null)
        {
            return;
        }

        List<Wall> removedWalls = new List<Wall>();
        if (pendingRoomRefreshRemovedWalls.Count > 0)
        {
            removedWalls.AddRange(pendingRoomRefreshRemovedWalls);
            pendingRoomRefreshRemovedWalls.Clear();
        }

        WallHierarchyUtility.CollectWalls(container.transform, cachedWalls, true);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall != null)
            {
                removedWalls.Add(wall);
            }
        }

        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));

        ClearGeneratedContainerVisuals(container, cachedWalls);

        if (cachedOpenings.Count == 0)
        {
            CreateWallSegment(
                container.transform,
                $"{container.name}_Segment_Full",
                container.WallStart,
                container.WallEnd,
                container.WallThickness,
                container.WallHeight,
                container.CenterY,
                container.OuterStartVertexId,
                container.OuterEndVertexId,
                container.SuppressOuterStartHandle,
                container.SuppressOuterEndHandle,
                container.WallMaterial);

            if (roomManager != null && removedWalls.Count > 0)
            {
                WallHierarchyUtility.CollectWalls(container.transform, cachedWalls, true);
                roomManager.RefreshRoomsForWallReplacement(removedWalls, cachedWalls);
            }

            MarkMarkerVisualsDirty();
            return;
        }

        Vector3 startPoint = container.WallStart;
        Vector3 direction = container.WallDirection;
        float currentDistance = 0f;

        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
            if (opening == null)
            {
                continue;
            }

            float halfWidth = opening.Width * 0.5f;
            float openingStartDistance = opening.CenterDistance - halfWidth;
            float openingEndDistance = opening.CenterDistance + halfWidth;

            Vector3 segmentStart = startPoint + direction * currentDistance;
            Vector3 segmentEnd = startPoint + direction * openingStartDistance;
            CreateWallSegment(
                container.transform,
                $"{container.name}_Segment_{i * 2}",
                segmentStart,
                segmentEnd,
                container.WallThickness,
                container.WallHeight,
                container.CenterY,
                currentDistance <= 0.001f ? container.OuterStartVertexId : 0,
                0,
                currentDistance > 0.001f || container.SuppressOuterStartHandle,
                true,
                container.WallMaterial);

            UpdateOpeningVisual(container, opening, i);
            currentDistance = openingEndDistance;
        }

        Vector3 lastSegmentStart = startPoint + direction * currentDistance;
        Vector3 lastSegmentEnd = container.WallEnd;
        CreateWallSegment(
            container.transform,
            $"{container.name}_Segment_End",
            lastSegmentStart,
            lastSegmentEnd,
            container.WallThickness,
            container.WallHeight,
            container.CenterY,
            0,
            container.OuterEndVertexId,
            true,
            container.SuppressOuterEndHandle,
            container.WallMaterial);

        if (roomManager != null && removedWalls.Count > 0)
        {
            WallHierarchyUtility.CollectWalls(container.transform, cachedWalls, true);
            roomManager.RefreshRoomsForWallReplacement(removedWalls, cachedWalls);
        }

        MarkMarkerVisualsDirty();
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
            if (wall == null)
            {
                continue;
            }

            Vector3 midpoint = (wall.StartPoint + wall.EndPoint) * 0.5f;
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

    private void UpdateOpeningVisual(WallOpeningContainer container, WallOpening opening, int index)
    {
        Vector3 openingCenter = container.WallStart + container.WallDirection * opening.CenterDistance;
        opening.transform.position = new Vector3(
            openingCenter.x,
            opening.BottomY + opening.Height * 0.5f,
            openingCenter.z);
        opening.transform.rotation = Quaternion.LookRotation(container.WallDirection, Vector3.up);
        opening.transform.localScale = new Vector3(opening.Depth, opening.Height, opening.Width);
        opening.name = opening.Type == OpeningPlacementType.Door ? $"Door_{index}" : $"Window_{index}";

        MeshFilter meshFilter = opening.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = opening.gameObject.AddComponent<MeshFilter>();
        }

        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = GetCubeMesh();
        }

        BoxCollider collider = opening.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = opening.gameObject.AddComponent<BoxCollider>();
        }

        MeshRenderer renderer = opening.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = opening.gameObject.AddComponent<MeshRenderer>();
        }

        renderer.sharedMaterial = GetOpeningMaterial(opening.Type);

        CreateFillerSegment(container.transform, $"{opening.name}_BottomFill", container, opening, opening.BottomY - container.WallBottomY, container.WallBottomY);
        CreateFillerSegment(
            container.transform,
            $"{opening.name}_TopFill",
            container,
            opening,
            container.WallTopY - (opening.BottomY + opening.Height),
            opening.BottomY + opening.Height);

        opening.EnsureMarker(
            previewCanvas,
            mainCamera,
            GetMarkerPrefab(opening),
            GetMarkerScaleMultiplier(opening));
    }

    private void EnsureCachedResources()
    {
        if (cachedCubeMesh != null)
        {
            return;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter cubeFilter = cube.GetComponent<MeshFilter>();
        if (cubeFilter != null)
        {
            cachedCubeMesh = cubeFilter.sharedMesh;
        }

        Destroy(cube);
    }

    private Mesh GetCubeMesh()
    {
        EnsureCachedResources();
        return cachedCubeMesh;
    }

    private Material GetOpeningMaterial(OpeningPlacementType type)
    {
        Material cached = type == OpeningPlacementType.Door ? cachedDoorMaterial : cachedWindowMaterial;
        if (cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.color = type == OpeningPlacementType.Door
            ? new Color(0.5f, 0.28f, 0.12f, 0.75f)
            : new Color(0.35f, 0.75f, 1f, 0.55f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (type == OpeningPlacementType.Door)
        {
            cachedDoorMaterial = material;
        }
        else
        {
            cachedWindowMaterial = material;
        }

        return material;
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

        Transform[] children = container.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == container.transform)
            {
                continue;
            }

            if (child.GetComponent<WallOpening>() != null)
            {
                continue;
            }

            if (child.GetComponent<Wall>() != null)
            {
                continue;
            }

            if (child.name.StartsWith("MarkerStart") || child.name.StartsWith("MarkerEnd"))
            {
                continue;
            }

            if (child.GetComponent<WallOpeningMarkerUI>() != null)
            {
                continue;
            }

            if (child.GetComponent<MeshRenderer>() != null || child.name.Contains("Fill"))
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void RemoveLayout(UndoRedoManager.OpeningLayoutSnapshot snapshot)
    {
        if (wallRoot == null)
        {
            return;
        }

        if (snapshot.hasContainer)
        {
            Transform container = wallRoot.Find(snapshot.layoutName);
            if (container != null)
            {
                WallHierarchyUtility.CollectWalls(container, cachedWalls);
                for (int i = 0; i < cachedWalls.Count; i++)
                {
                    if (cachedWalls[i] == null)
                    {
                        continue;
                    }

                    cachedWalls[i].ClearLengthDisplay(wallLengthDisplay);
                    if (handleManager != null)
                    {
                        handleManager.UnregisterWall(cachedWalls[i].gameObject);
                    }
                }

                Destroy(container.gameObject);
            }

            return;
        }

        Wall wall = FindMatchingStandaloneWall(snapshot.wallSnapshot);
        if (wall == null)
        {
            return;
        }

        if (handleManager != null)
        {
            handleManager.UnregisterWall(wall.gameObject);
        }

        wall.ClearLengthDisplay(wallLengthDisplay);
        Destroy(wall.gameObject);
    }

    private Wall FindMatchingStandaloneWall(UndoRedoManager.WallStateSnapshot snapshot)
    {
        if (wallRoot == null)
        {
            return null;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (wall.transform.parent != wallRoot)
            {
                continue;
            }

            if (wall.name == snapshot.name &&
                (wall.StartPoint - snapshot.startPoint).sqrMagnitude <= 0.0001f &&
                (wall.EndPoint - snapshot.endPoint).sqrMagnitude <= 0.0001f)
            {
                return wall;
            }
        }

        return null;
    }

    private bool HasOtherOpenings(WallOpeningContainer container, WallOpening excludedOpening)
    {
        if (container == null)
        {
            return false;
        }

        WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening != null && opening != excludedOpening)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject RestoreContainerIfEmpty(WallOpeningContainer container)
    {
        if (container == null)
        {
            return null;
        }

        CollectOpenings(container, cachedOpenings);
        if (cachedOpenings.Count > 0)
        {
            return null;
        }

        UndoRedoManager.WallStateSnapshot wallSnapshot = BuildWallSnapshotFromContainer(container);
        RemoveLayout(CaptureLayoutSnapshot(container));
        GameObject restoredWall = CreateRestoredWall(wallSnapshot);
        if (restoredWall != null && handleManager != null)
        {
            handleManager.RegisterWall(restoredWall);
        }

        if (roomManager != null)
        {
            roomManager.RefreshAllRooms();
        }

        MarkMarkerVisualsDirty();
        return restoredWall;
    }

    private UndoRedoManager.WallStateSnapshot BuildWallSnapshotFromContainer(WallOpeningContainer container)
    {
        return BuildWallSnapshotFromContainer(new UndoRedoManager.OpeningLayoutSnapshot
        {
            layoutName = container != null ? container.name : "Wall",
            wallStart = container != null ? container.WallStart : Vector3.zero,
            wallEnd = container != null ? container.WallEnd : Vector3.right,
            wallThickness = container != null ? container.WallThickness : 0.1f,
            wallHeight = container != null ? container.WallHeight : 1f,
            centerY = container != null ? container.CenterY : 0.5f,
            wallMaterial = container != null ? container.WallMaterial : null,
            wallTopMaterial = container != null ? container.WallTopMaterial : null,
            outerStartVertexId = container != null ? container.OuterStartVertexId : 0,
            outerEndVertexId = container != null ? container.OuterEndVertexId : 0,
            suppressOuterStartHandle = container != null && container.SuppressOuterStartHandle,
            suppressOuterEndHandle = container != null && container.SuppressOuterEndHandle,
        });
    }

    private UndoRedoManager.WallStateSnapshot BuildWallSnapshotFromContainer(UndoRedoManager.OpeningLayoutSnapshot snapshot)
    {
        Vector3 center = (snapshot.wallStart + snapshot.wallEnd) * 0.5f;
        Vector3 direction = snapshot.wallEnd - snapshot.wallStart;
        direction.y = 0f;
        float length = direction.magnitude;
        Quaternion rotation = length > MinimumWallSegmentLength
            ? Quaternion.LookRotation(direction / length, Vector3.up)
            : Quaternion.identity;

        return new UndoRedoManager.WallStateSnapshot
        {
            name = snapshot.layoutName,
            position = new Vector3(center.x, snapshot.centerY, center.z),
            rotation = rotation,
            scale = new Vector3(snapshot.wallThickness, snapshot.wallHeight, Mathf.Max(length, MinimumWallSegmentLength)),
            sharedMaterial = snapshot.wallMaterial,
            topMaterial = snapshot.wallTopMaterial,
            startPoint = snapshot.wallStart,
            endPoint = snapshot.wallEnd,
            startVertexId = snapshot.outerStartVertexId,
            endVertexId = snapshot.outerEndVertexId,
            suppressStartHandle = snapshot.suppressOuterStartHandle,
            suppressEndHandle = snapshot.suppressOuterEndHandle,
        };
    }

    private GameObject CreateRestoredWall(UndoRedoManager.WallStateSnapshot snapshot)
    {
        if (wallRoot == null)
        {
            return null;
        }

        GameObject wallObject = CreateCubeObject(snapshot.name, wallRoot, true);
        wallObject.transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
        wallObject.transform.localScale = snapshot.scale;

        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = snapshot.sharedMaterial;
        }

        Wall wall = wallObject.AddComponent<Wall>();
        wall.Initialize(snapshot.startPoint, snapshot.endPoint);
        wall.SetVertexIds(snapshot.startVertexId, snapshot.endVertexId);
        wall.SetHandleSuppressed(snapshot.suppressStartHandle, snapshot.suppressEndHandle);
        wall.SetSplitPointFlags(snapshot.startSplitPoint, snapshot.endSplitPoint);
        wall.SetTopMaterial(snapshot.topMaterial);
        wall.SetTopFaceOffset(0.01f);
        wall.RefreshLengthDisplay(wallLengthDisplay, false);
        return wallObject;
    }

    private Transform GetOrCreateOpeningContainer(Wall selectedWall)
    {
        if (selectedWall == null)
        {
            return null;
        }

        Transform existingParent = selectedWall.transform.parent;
        if (existingParent != null && existingParent.TryGetComponent(out WallOpeningContainer existingContainer))
        {
            return existingContainer.transform;
        }

        WallGeometryData geometry = CaptureGeometry(selectedWall);
        GameObject containerObject = new GameObject(selectedWall.name);
        containerObject.transform.SetParent(wallRoot, false);
        containerObject.transform.position = Vector3.zero;
        containerObject.transform.rotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;
        LayerUtility.ApplyLayer(containerObject, LayerUtility.WallLayerName, false);

        WallOpeningContainer container = containerObject.AddComponent<WallOpeningContainer>();
        container.Initialize(
            geometry.wallStart,
            geometry.wallEnd,
            geometry.wallThickness,
            geometry.wallHeight,
            geometry.centerY,
            geometry.wallMaterial,
            geometry.wallTopMaterial,
            geometry.outerStartVertexId,
            geometry.outerEndVertexId,
            selectedWall.SuppressStartHandle,
            selectedWall.SuppressEndHandle);

        if (handleManager != null)
        {
            handleManager.UnregisterWall(selectedWall.gameObject);
        }

        pendingRoomRefreshRemovedWalls.Add(selectedWall);

        selectedWall.ClearLengthDisplay(wallLengthDisplay);
        Destroy(selectedWall.gameObject);
        return container.transform;
    }

    private WallGeometryData CaptureGeometry(Wall wall)
    {
        Vector3 wallStart = wall.StartPoint;
        Vector3 wallEnd = wall.EndPoint;
        Vector3 wallDirection = wallEnd - wallStart;
        wallDirection.y = 0f;
        float wallLength = wallDirection.magnitude;
        if (wallLength > MinimumWallSegmentLength)
        {
            wallDirection /= wallLength;
        }

        MeshRenderer wallRenderer = wall.GetComponent<MeshRenderer>();
        float wallHeight = wall.transform.localScale.y;
        return new WallGeometryData
        {
            wallStart = wallStart,
            wallEnd = wallEnd,
            wallDirection = wallDirection,
            wallLength = wallLength,
            wallHeight = wallHeight,
            wallThickness = wall.transform.localScale.x,
            centerY = wall.transform.position.y,
            outerStartVertexId = wall.StartVertexId,
            outerEndVertexId = wall.EndVertexId,
            wallMaterial = wallRenderer != null ? wallRenderer.sharedMaterial : null,
            wallTopMaterial = wall.GetTopMaterial(),
        };
    }

    private void CreateWallSegment(
        Transform parent,
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
        Material wallMaterial)
    {
        Vector3 direction = endPoint - startPoint;
        direction.y = 0f;
        if (direction.magnitude < MinimumWallSegmentLength)
        {
            return;
        }

        GameObject wallObject = CreateCubeObject(segmentName, parent, true);
        wallObject.name = segmentName;
        LayerUtility.ApplyLayer(wallObject, LayerUtility.WallLayerName, false);

        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null && wallMaterial != null)
        {
            renderer.sharedMaterial = wallMaterial;
        }

        Wall wallComponent = wallObject.AddComponent<Wall>();
        wallComponent.SetTopMaterial(parent.TryGetComponent(out WallOpeningContainer container) ? container.WallTopMaterial : null);
        wallComponent.SetTopFaceOffset(0.01f);
        bool applied = wallComponent.TryApplyGeometryAndRefresh(
            startPoint,
            endPoint,
            thickness,
            height,
            centerY,
            MinimumWallSegmentLength,
            wallLengthDisplay,
            false);

        if (!applied)
        {
            Destroy(wallObject);
            return;
        }

        wallComponent.SetVertexIds(startVertexId, endVertexId);
        wallComponent.SetHandleSuppressed(suppressStartHandle, suppressEndHandle);

        if (handleManager != null)
        {
            handleManager.RegisterWall(wallObject);
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
        Material wallMaterial,
        Material topMaterial)
    {
        Vector3 direction = endPoint - startPoint;
        direction.y = 0f;
        if (direction.magnitude < MinimumWallSegmentLength)
        {
            return null;
        }

        GameObject wallObject = CreateCubeObject(segmentName, wallRoot, true);
        wallObject.name = segmentName;
        LayerUtility.ApplyLayer(wallObject, LayerUtility.WallLayerName, false);

        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null && wallMaterial != null)
        {
            renderer.sharedMaterial = wallMaterial;
        }

        Wall wallComponent = wallObject.AddComponent<Wall>();
        wallComponent.SetTopMaterial(topMaterial);
        wallComponent.SetTopFaceOffset(0.01f);
        bool applied = wallComponent.TryApplyGeometryAndRefresh(
            startPoint,
            endPoint,
            thickness,
            height,
            centerY,
            MinimumWallSegmentLength,
            wallLengthDisplay,
            false);

        if (!applied)
        {
            Destroy(wallObject);
            return null;
        }

        wallComponent.SetVertexIds(startVertexId, endVertexId);
        wallComponent.SetHandleSuppressed(suppressStartHandle, suppressEndHandle);
        wallComponent.SetSplitPointFlags(startSplitPoint, endSplitPoint);
        handleManager?.RegisterWall(wallObject);
        return wallObject;
    }

    private void CreateFillerSegment(
        Transform parent,
        string fillerName,
        WallOpeningContainer container,
        WallOpening opening,
        float segmentHeight,
        float segmentBottomY)
    {
        if (segmentHeight <= 0.01f)
        {
            return;
        }

        Vector3 openingCenter = container.WallStart + container.WallDirection * opening.CenterDistance;
        GameObject filler = CreateCubeObject(fillerName, parent, false);
        filler.name = fillerName;
        LayerUtility.ApplyLayer(filler, LayerUtility.WallLayerName, false);
        filler.transform.position = new Vector3(
            openingCenter.x,
            segmentBottomY + segmentHeight * 0.5f,
            openingCenter.z);
        filler.transform.rotation = Quaternion.LookRotation(container.WallDirection, Vector3.up);
        filler.transform.localScale = new Vector3(container.WallThickness, segmentHeight, opening.Width);

        MeshRenderer renderer = filler.GetComponent<MeshRenderer>();
        if (renderer != null && container.WallMaterial != null)
        {
            renderer.sharedMaterial = container.WallMaterial;
        }

        WallTopFaceVisual topFaceVisual = filler.GetComponent<WallTopFaceVisual>();
        if (topFaceVisual == null)
        {
            topFaceVisual = filler.AddComponent<WallTopFaceVisual>();
        }

        topFaceVisual.SetTopMaterial(container.WallTopMaterial);
        topFaceVisual.SetWorldOffset(0.01f);
    }

    private GameObject CreateCubeObject(string objectName, Transform parent, bool withCollider)
    {
        GameObject cubeObject = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        cubeObject.transform.SetParent(parent, true);

        MeshFilter meshFilter = cubeObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = GetCubeMesh();
        }

        if (withCollider)
        {
            cubeObject.AddComponent<BoxCollider>();
        }

        return cubeObject;
    }

    private float ClampOpeningCenterDistance(
        WallOpeningContainer container,
        WallOpening targetOpening,
        float desiredDistance,
        float overrideWidth = -1f)
    {
        if (container == null)
        {
            return desiredDistance;
        }

        float minimumSideWall = MillimetersToUnits(minimumSideWallMillimeters);
        float targetWidth = overrideWidth > 0f ? overrideWidth : (targetOpening != null ? targetOpening.Width : 0f);
        float halfWidth = targetWidth * 0.5f;
        float minDistance = minimumSideWall + halfWidth;
        float maxDistance = container.WallLength - minimumSideWall - halfWidth;

        CollectOpenings(container, cachedOpenings);
        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
            if (opening == null || opening == targetOpening)
            {
                continue;
            }

            float clearance = minimumSideWall + halfWidth + opening.Width * 0.5f;
            if (opening.CenterDistance <= desiredDistance)
            {
                minDistance = Mathf.Max(minDistance, opening.CenterDistance + clearance);
            }
            else
            {
                maxDistance = Mathf.Min(maxDistance, opening.CenterDistance - clearance);
            }
        }

        if (maxDistance < minDistance)
        {
            float midpoint = (minDistance + maxDistance) * 0.5f;
            minDistance = midpoint;
            maxDistance = midpoint;
        }

        return Mathf.Clamp(desiredDistance, minDistance, maxDistance);
    }

    private float ClampOpeningWidth(WallOpeningContainer container, WallOpening targetOpening, float desiredWidth)
    {
        if (container == null || targetOpening == null)
        {
            return desiredWidth;
        }

        float minimumSideWall = MillimetersToUnits(minimumSideWallMillimeters);
        float leftLimit = minimumSideWall;
        float rightLimit = container.WallLength - minimumSideWall;

        CollectOpenings(container, cachedOpenings);
        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
            if (opening == null || opening == targetOpening)
            {
                continue;
            }

            float neighborHalfWidth = opening.Width * 0.5f;
            if (opening.CenterDistance < targetOpening.CenterDistance)
            {
                leftLimit = Mathf.Max(leftLimit, opening.CenterDistance + neighborHalfWidth + minimumSideWall);
            }
            else
            {
                rightLimit = Mathf.Min(rightLimit, opening.CenterDistance - neighborHalfWidth - minimumSideWall);
            }
        }

        float maxWidth = Mathf.Max(MinimumWallSegmentLength, Mathf.Min(
            (targetOpening.CenterDistance - leftLimit) * 2f,
            (rightLimit - targetOpening.CenterDistance) * 2f));
        return Mathf.Clamp(desiredWidth, MinimumWallSegmentLength, maxWidth);
    }

    private float ClampOpeningHeight(WallOpeningContainer container, WallOpening targetOpening, float desiredHeight, float bottomY)
    {
        if (container == null)
        {
            return desiredHeight;
        }

        float maxHeight = Mathf.Max(
            MinimumWallSegmentLength,
            container.WallTopY - bottomY);
        return Mathf.Clamp(desiredHeight, MinimumWallSegmentLength, maxHeight);
    }

    private float ClampOpeningBottomY(WallOpeningContainer container, WallOpening targetOpening, float desiredBottomY)
    {
        if (container == null || targetOpening == null)
        {
            return desiredBottomY;
        }

        float minBottomY = container.WallBottomY;
        float maxBottomY = container.WallTopY - targetOpening.Height;
        if (maxBottomY < minBottomY)
        {
            maxBottomY = minBottomY;
        }

        return Mathf.Clamp(desiredBottomY, minBottomY, maxBottomY);
    }

    public GameObject GetMarkerPrefab(WallOpening opening)
    {
        if (opening == null)
        {
            return null;
        }

        MarkerVariantDefinition variant = GetMarkerVariantDefinition(opening);
        if (opening.Type == OpeningPlacementType.Door)
        {
            return variant != null && variant.MarkerPrefab != null ? variant.MarkerPrefab : doorMarkerPrefab;
        }

        return variant != null && variant.MarkerPrefab != null ? variant.MarkerPrefab : windowMarkerPrefab;
    }

    public Vector2 GetMarkerScaleMultiplier(WallOpening opening)
    {
        if (opening == null)
        {
            return Vector2.one;
        }

        MarkerVariantDefinition variant = GetMarkerVariantDefinition(opening);
        if (variant != null)
        {
            return variant.ScaleMultiplier;
        }

        return opening.Type == OpeningPlacementType.Door
            ? doorMarkerScaleMultiplier
            : windowMarkerScaleMultiplier;
    }

    public MarkerPlacementMode GetMarkerPlacementMode(WallOpening opening)
    {
        if (opening == null)
        {
            return MarkerPlacementMode.OffsetFromOpening;
        }

        MarkerVariantDefinition variant = GetMarkerVariantDefinition(opening);
        if (variant != null)
        {
            return variant.PlacementMode;
        }

        return opening.Type == OpeningPlacementType.Door
            ? doorMarkerPlacementMode
            : windowMarkerPlacementMode;
    }

    private MarkerVariantDefinition GetMarkerVariantDefinition(WallOpening opening)
    {
        if (opening == null)
        {
            return null;
        }

        if (opening.Type == OpeningPlacementType.Door)
        {
            return FindMarkerVariant(doorMarkerVariants, opening.DoorTypeKey);
        }

        return FindMarkerVariant(windowMarkerVariants, opening.WindowTypeKey);
    }

    private static T FindMarkerVariant<T>(List<T> variants, string typeKey) where T : MarkerVariantDefinition
    {
        if (variants == null || string.IsNullOrWhiteSpace(typeKey))
        {
            return null;
        }

        for (int i = 0; i < variants.Count; i++)
        {
            T variant = variants[i];
            if (variant == null || string.IsNullOrWhiteSpace(variant.TypeName))
            {
                continue;
            }

            if (string.Equals(variant.TypeName, typeKey, System.StringComparison.Ordinal))
            {
                return variant;
            }
        }

        return null;
    }

    private static void ValidateMarkerVariants<T>(List<T> variants) where T : MarkerVariantDefinition
    {
        if (variants == null)
        {
            return;
        }

        for (int i = 0; i < variants.Count; i++)
        {
            T variant = variants[i];
            if (variant == null)
            {
                continue;
            }

            variant.ClampValues();
        }
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        Transform wallRootTransform = LayerUtility.FindTransformByName("Walls", true);
        if (wallRootTransform != null)
        {
            wallRoot = wallRootTransform;
        }
    }

    private void ResolveReferences()
    {
        if (handleManager == null)
        {
            handleManager = FindFirstObjectByType<HandleManager>();
        }

        if (wallLengthDisplay == null)
        {
            wallLengthDisplay = FindFirstObjectByType<WallLengthDisplay>();
        }

        if (undoRedoManager == null)
        {
            undoRedoManager = FindFirstObjectByType<UndoRedoManager>();
        }

        if (modeManager == null)
        {
            modeManager = FindFirstObjectByType<ModeManager>();
        }

        if (wallSelectionManager == null)
        {
            wallSelectionManager = FindFirstObjectByType<WallSelectionManager>();
        }

        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }
    }

    private float MillimetersToUnits(float millimeters)
    {
        return millimeters / UnitToMillimeters;
    }

    public float UnitsToMillimeters(float units)
    {
        return units * UnitToMillimeters;
    }

    public float GetSelectedOpeningBottomOffsetMillimeters(float fallbackValue, OpeningPlacementType requiredType)
    {
        if (selectedOpening == null || selectedOpening.Type != requiredType || selectedOpening.Container == null)
        {
            return fallbackValue;
        }

        return UnitsToMillimeters(selectedOpening.BottomY - selectedOpening.Container.WallBottomY);
    }

    private bool TryParsePositiveMillimeters(string inputText, out float value)
    {
        bool parsed = TryParseMillimeters(inputText, out value);
        return parsed && value > 0f;
    }

    private bool TryParseMillimeters(string inputText, out float value)
    {
        return UnitDisplayUtility.TryParseMillimeters(inputText, out value);
    }

}
