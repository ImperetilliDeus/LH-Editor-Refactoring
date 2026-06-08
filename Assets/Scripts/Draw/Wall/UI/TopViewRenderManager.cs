using System.Collections.Generic;
using UnityEngine;

public partial class TopViewRenderManager : MonoBehaviour
{
    private const string DefaultCanvasName = LayerUtility.DefaultCanvasName;
    private const string DefaultContentRootName = "TopPlanContent";
    private const string LegacyContentRootName = "_TopPlanContent";

    [Header("References")]
    [SerializeField] private Camera topViewCamera;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private DrawManager drawManager;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
    [SerializeField] private RoomWallAuthoringPanelController roomWallAuthoringPanelController;
    [SerializeField] private RoomHandleManager roomHandleManager;
    [SerializeField] private ModeManager modeManager;

    [Header("Visibility")]
    [SerializeField] private bool showOnlyInDetailEdit = false;

    [Header("Preview")]
    [SerializeField, Min(0.01f)] private float previewWallThicknessMultiplier = 0.55f;

    [Header("Colors")]
    [SerializeField] private Color floorColor = new Color(0.34f, 0.86f, 0.58f, 0.22f);
    [SerializeField] private Color selectedFloorColor = new Color(0.28f, 0.6f, 1f, 0.35f);
    [SerializeField] private Color wallColor = new Color(0.12f, 0.12f, 0.12f, 0.92f);
    [SerializeField] private Color selectedWallColor = new Color(1f, 0.64f, 0.12f, 1f);
    [SerializeField] private Color authoringHoveredWallColor = new Color(0.44f, 1f, 0.78f, 1f);
    [SerializeField] private Color authoringSelectedWallColor = new Color(1f, 0.84f, 0.22f, 1f);
    [SerializeField] private Color previewWallColor = new Color(0.2f, 0.8f, 1f, 0.45f);
    [SerializeField] private Color doorColor = new Color(0.58f, 0.30f, 0.12f, 0.95f);
    [SerializeField] private Color selectedDoorColor = new Color(1f, 0.68f, 0.22f, 1f);
    [SerializeField] private Color windowColor = new Color(0.22f, 0.62f, 0.95f, 0.95f);
    [SerializeField] private Color selectedWindowColor = new Color(0.42f, 0.84f, 1f, 1f);
    [SerializeField] private Color virtualBoundaryColor = new Color(0.1f, 0.82f, 1f, 0.95f);
    [SerializeField] private float virtualBoundaryThickness = 2f;
    [SerializeField] private float virtualBoundaryDashLength = 14f;
    [SerializeField] private float virtualBoundaryGapLength = 10f;

    private readonly List<Vector2> cachedPolygonPoints = new List<Vector2>();
    private readonly List<GameObject> cachedSelectedWalls = new List<GameObject>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<TopPlanPolygonBatchGraphic.PolygonData> cachedFloorPolygons = new List<TopPlanPolygonBatchGraphic.PolygonData>();
    private readonly List<TopPlanSegmentBatchGraphic.SegmentData> cachedWallSegments = new List<TopPlanSegmentBatchGraphic.SegmentData>();
    private readonly List<TopPlanSegmentBatchGraphic.SegmentData> cachedOpeningSegments = new List<TopPlanSegmentBatchGraphic.SegmentData>();
    private readonly List<TopPlanSegmentBatchGraphic.SegmentData> cachedVirtualBoundarySegments = new List<TopPlanSegmentBatchGraphic.SegmentData>();

    private TopPlanPolygonBatchGraphic floorBatchGraphic;
    private TopPlanSegmentBatchGraphic wallBatchGraphic;
    private TopPlanSegmentBatchGraphic openingBatchGraphic;
    private TopPlanSegmentBatchGraphic virtualBoundaryBatchGraphic;

    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private float lastCameraOrthoSize;
    private bool visualsDirty = true;
    private bool isTopViewVisible = true;
    private Room highlightedRoom;
    public Camera TopViewCamera => topViewCamera;
    public Canvas TargetCanvas => targetCanvas;
    public RectTransform ContentRoot => contentRoot;

    private void Reset()
    {
        topViewCamera = Camera.main;
        ResolveReferences();
        targetCanvas = LayerUtility.FindCanvasByNameOrFirst(DefaultCanvasName);
    }

    private void Awake()
    {
        if (topViewCamera == null)
        {
            topViewCamera = Camera.main;
        }

        ResolveReferences();

        EnsureWallRoot();
        EnsureCanvas();
        BindEvents();
        BindVisualEvents();
        CacheCameraState();
        SyncVisibilityState();
        RefreshAllVisuals();
        ValidateConfiguration();
    }

    private void OnDestroy()
    {
        if (wallBatchGraphic != null)
        {
            wallBatchGraphic.SegmentClicked -= HandleTopPlanWallSegmentClicked;
        }

        if (openingBatchGraphic != null)
        {
            openingBatchGraphic.SegmentClicked -= HandleTopPlanOpeningSegmentClicked;
        }

        UnbindEvents();
        UnbindVisualEvents();
        ClearPolygonBatchGraphic(ref floorBatchGraphic);
        ClearBatchGraphic(ref wallBatchGraphic);
        ClearBatchGraphic(ref openingBatchGraphic);
        ClearBatchGraphic(ref virtualBoundaryBatchGraphic);
    }

    private void Update()
    {
        if (!isTopViewVisible)
        {
            return;
        }

        if (drawManager != null && drawManager.PreviewWall != null && drawManager.PreviewWall.activeInHierarchy)
        {
            visualsDirty = true;
        }

        if (visualsDirty || HasCameraStateChanged())
        {
            RefreshAllVisuals();
            visualsDirty = false;
        }

        CacheCameraState();
    }

    public void MarkDirty()
    {
        visualsDirty = true;
    }

    private void HandleTopPlanWallSegmentClicked(TopPlanSegmentBatchGraphic.SegmentData segment)
    {
        if (!IsRoomWallAuthoringInteractionEnabled() ||
            roomWallAuthoringPanelController == null ||
            segment.wall == null)
        {
            return;
        }

        if (roomWallAuthoringPanelController.TryToggleWallSelectionFromWall(segment.wall))
        {
            MarkDirty();
        }
    }

    private void HandleTopPlanOpeningSegmentClicked(TopPlanSegmentBatchGraphic.SegmentData segment)
    {
        if (!IsOpeningInteractionEnabled() ||
            wallOpeningPlacementManager == null ||
            segment.opening == null)
        {
            return;
        }

        wallOpeningPlacementManager.SelectOpening(segment.opening);
        MarkDirty();
    }

    private void BindEvents()
    {
        if (handleManager != null)
        {
            handleManager.WallHierarchyChanged += MarkDirty;
        }

        if (wallSelectionManager != null)
        {
            wallSelectionManager.SelectionChanged += HandleWallSelectionChanged;
            wallSelectionManager.SelectionSetChanged += HandleWallSelectionSetChanged;
        }

        if (wallOpeningPlacementManager != null)
        {
            wallOpeningPlacementManager.OpeningSelectionChanged += HandleOpeningSelectionChanged;
        }

        if (roomManager != null)
        {
            roomManager.RoomsChanged += MarkDirty;
        }

        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged += HandleSelectedRoomChanged;
        }

        if (roomWallAuthoringPanelController != null)
        {
            roomWallAuthoringPanelController.HighlightStateChanged += MarkDirty;
        }

        if (roomHandleManager != null)
        {
            roomHandleManager.FocusedRoomChanged += HandleFocusedRoomChanged;
        }

        VirtualBoundary.BoundariesChanged += MarkDirty;

        if (modeManager != null)
        {
            modeManager.ModeChanged += HandleModeChanged;
        }
    }

    private void UnbindEvents()
    {
        if (handleManager != null)
        {
            handleManager.WallHierarchyChanged -= MarkDirty;
        }

        if (wallSelectionManager != null)
        {
            wallSelectionManager.SelectionChanged -= HandleWallSelectionChanged;
            wallSelectionManager.SelectionSetChanged -= HandleWallSelectionSetChanged;
        }

        if (wallOpeningPlacementManager != null)
        {
            wallOpeningPlacementManager.OpeningSelectionChanged -= HandleOpeningSelectionChanged;
        }

        if (roomManager != null)
        {
            roomManager.RoomsChanged -= MarkDirty;
        }

        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged -= HandleSelectedRoomChanged;
        }

        if (roomWallAuthoringPanelController != null)
        {
            roomWallAuthoringPanelController.HighlightStateChanged -= MarkDirty;
        }

        if (roomHandleManager != null)
        {
            roomHandleManager.FocusedRoomChanged -= HandleFocusedRoomChanged;
        }

        VirtualBoundary.BoundariesChanged -= MarkDirty;

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
        }
    }

    private void HandleWallSelectionChanged(GameObject selectedWall)
    {
        MarkDirty();
    }

    private void HandleOpeningSelectionChanged(WallOpening selectedOpening)
    {
        MarkDirty();
    }

    private void HandleWallSelectionSetChanged()
    {
        MarkDirty();
    }

    private void HandleModeChanged(EditorMode mode)
    {
        isTopViewVisible = !showOnlyInDetailEdit || mode == EditorMode.DetailEdit;
        if (contentRoot != null && contentRoot.gameObject.activeSelf != isTopViewVisible)
        {
            contentRoot.gameObject.SetActive(isTopViewVisible);
        }

        enabled = isTopViewVisible;
        MarkDirty();
    }

    private void HandleSelectedRoomChanged(Room room)
    {
        highlightedRoom = room ?? (roomHandleManager != null ? roomHandleManager.FocusedRoom : null);
        MarkDirty();
    }

    private void HandleFocusedRoomChanged(Room room)
    {
        if (highlightedRoom != null)
        {
            return;
        }

        highlightedRoom = room;
        MarkDirty();
    }

    private void ResolveReferences()
    {
        if (drawManager == null)
        {
            LayerUtility.ResolveObject(ref drawManager);
        }

        if (handleManager == null)
        {
            LayerUtility.ResolveObject(ref handleManager);
        }

        if (wallSelectionManager == null)
        {
            LayerUtility.ResolveObject(ref wallSelectionManager);
        }

        if (wallOpeningPlacementManager == null)
        {
            LayerUtility.ResolveObject(ref wallOpeningPlacementManager);
        }

        if (roomManager == null)
        {
            LayerUtility.ResolveObject(ref roomManager);
        }

        if (roomAuthoringPanelManager == null)
        {
            LayerUtility.ResolveObject(ref roomAuthoringPanelManager);
        }

        if (roomWallAuthoringPanelController == null)
        {
            LayerUtility.ResolveObject(ref roomWallAuthoringPanelController);
        }

        if (roomHandleManager == null)
        {
            LayerUtility.ResolveObject(ref roomHandleManager);
        }

        if (modeManager == null)
        {
            LayerUtility.ResolveObject(ref modeManager);
        }
    }

    private void SyncVisibilityState()
    {
        HandleModeChanged(modeManager != null ? modeManager.CurrentMode : EditorMode.DetailEdit);
        highlightedRoom = roomAuthoringPanelManager != null
            ? roomAuthoringPanelManager.SelectedRoom
            : roomHandleManager != null ? roomHandleManager.FocusedRoom : null;
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(topViewCamera != null, $"{nameof(TopViewRenderManager)} requires {nameof(topViewCamera)}.", this);
        Debug.Assert(contentRoot != null, $"{nameof(TopViewRenderManager)} requires {nameof(contentRoot)}.", this);
        Debug.Assert(roomManager != null, $"{nameof(TopViewRenderManager)} requires {nameof(roomManager)}.", this);
    }

    private bool IsRoomWallAuthoringInteractionEnabled()
    {
        return modeManager != null &&
               modeManager.CurrentMode == EditorMode.RoomCreate &&
               roomWallAuthoringPanelController != null &&
               roomWallAuthoringPanelController.HasSelectedRoomForAuthoring;
    }

    private bool IsOpeningInteractionEnabled()
    {
        return modeManager != null &&
               modeManager.CurrentMode == EditorMode.DetailEdit &&
               wallOpeningPlacementManager != null;
    }

    private void EnsureCanvas()
    {
        if (targetCanvas == null)
        {
            targetCanvas = LayerUtility.FindCanvasByNameOrFirst(DefaultCanvasName);
        }

        if (targetCanvas == null)
        {
            return;
        }

        targetCanvas.pixelPerfect = false;

        if (contentRoot == null)
        {
            Transform existing = targetCanvas.transform.Find(DefaultContentRootName);
            if (existing != null)
            {
                contentRoot = existing as RectTransform;
            }
            else
            {
                existing = targetCanvas.transform.Find(LegacyContentRootName);
                if (existing != null)
                {
                    contentRoot = existing as RectTransform;
                }
            }
        }

        if (contentRoot == null)
        {
            GameObject contentObject = new GameObject(DefaultContentRootName, typeof(RectTransform));
            contentObject.transform.SetParent(targetCanvas.transform, false);
            contentRoot = contentObject.GetComponent<RectTransform>();
        }

        LayerUtility.ApplyLayer(contentRoot.gameObject, LayerUtility.TopPlanUILayerName, true);
        NormalizeRectTransform(contentRoot, true);
        contentRoot.SetAsLastSibling();
        WallSelectionCanvasOrderingUtility.PlaceBelowSelectableControls(contentRoot, targetCanvas.transform);
    }

    private void NormalizeRectTransform(RectTransform rectTransform, bool stretchToParent)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.anchoredPosition3D = Vector3.zero;

        if (!stretchToParent)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void EnsureWallRoot()
    {
        LayerUtility.ResolveWallRoot(ref wallRoot, true);
    }

    private void CacheCameraState()
    {
        if (topViewCamera == null)
        {
            return;
        }

        Transform cameraTransform = topViewCamera.transform;
        lastCameraPosition = cameraTransform.position;
        lastCameraRotation = cameraTransform.rotation;
        lastCameraOrthoSize = topViewCamera.orthographicSize;
    }

    private bool HasCameraStateChanged()
    {
        if (topViewCamera == null)
        {
            return false;
        }

        Transform cameraTransform = topViewCamera.transform;
        return cameraTransform.position != lastCameraPosition ||
               cameraTransform.rotation != lastCameraRotation ||
               !Mathf.Approximately(topViewCamera.orthographicSize, lastCameraOrthoSize);
    }
}
