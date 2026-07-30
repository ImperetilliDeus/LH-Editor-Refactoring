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

            RebuildContainerLayout(container, isDragging);
        }
        finally
        {
            if (isDragging)
            {
                handleManager?.EndTransientDragRebuild();
            }
        }
    }

    private void RebuildContainerLayout(WallOpeningContainer container, bool isDragging)
    {
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

}
