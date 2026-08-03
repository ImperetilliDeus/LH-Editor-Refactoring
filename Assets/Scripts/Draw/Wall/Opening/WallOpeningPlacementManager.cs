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

    private struct ContainerPreviewSegment
    {
        public Vector3 startPoint;
        public Vector3 endPoint;
        public int startVertexId;
        public int endVertexId;
        public bool suppressStartHandle;
        public bool suppressEndHandle;
        public bool startSplitPoint;
        public bool endSplitPoint;
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
    private readonly List<ContainerPreviewSegment> cachedPreviewSegments = new List<ContainerPreviewSegment>();
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

        ResolveOpeningUIControllers();

        LayerUtility.ResolveCanvasByNameOrFirst(ref previewCanvas, LayerUtility.DefaultCanvasName);

        CreateDeactivatedWallsHolder();
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

    private void ResolveOpeningUIControllers()
    {
        if (doorUIController == null)
        {
            doorUIController = GetComponentInChildren<DoorOpeningUIController>(true);
        }

        if (windowUIController == null)
        {
            windowUIController = GetComponentInChildren<WindowOpeningUIController>(true);
        }
    }

    private void CreateDeactivatedWallsHolder()
    {
        // This holder keeps drag-deactivated walls alive for HandleManager while hiding them from child scans.
        GameObject tempHolder = new GameObject("DeactivatedWallHolder");
        tempHolder.transform.SetParent(transform, false);
        tempHolder.SetActive(false);
        deactivatedWallsHolder = tempHolder.transform;
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
        bool isDraggingMarker = markerDragState.IsDraggingMarker;
        bool shouldRefreshMarkerVisuals = markerVisualsDirty || HasCameraStateChanged();
        if (!isDraggingMarker && HasMarkerGeometryChanged())
        {
            shouldRefreshMarkerVisuals = true;
        }

        if (shouldRefreshMarkerVisuals)
        {
            RefreshOpeningMarkerVisuals();
            markerVisualsDirty = false;
            if (!isDraggingMarker)
            {
                lastMarkerGeometryHash = CalculateMarkerGeometryHash();
            }
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
