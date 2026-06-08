using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SceneReferenceRegistry : MonoBehaviour
{
    public static SceneReferenceRegistry Instance { get; private set; }

    [Header("Core")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private DrawManager drawManager;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private SnapManager snapManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private UndoRedoManager undoRedoManager;
    [SerializeField] private TopViewRenderManager topViewRenderManager;
    [SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
    [SerializeField] private RoomWallAuthoringPanelController roomWallAuthoringPanelController;
    [SerializeField] private RoomHandleManager roomHandleManager;
    [SerializeField] private DrawingOverlayManager drawingOverlayManager;
    [SerializeField] private OverlayCalibrationPanelController overlayCalibrationPanelController;

    [Header("Scene Roots")]
    [SerializeField] private Transform wallRoot;
    [SerializeField] private GameObject grid;
    [SerializeField] private Button importButton;

    [Header("Canvas")]
    [SerializeField] private Canvas defaultCanvas;
    [SerializeField] private Canvas handleCanvas;
    [SerializeField] private Canvas wallUiCanvas;

    private void Awake()
    {
        RegisterInstance();
    }

    private void OnEnable()
    {
        RegisterInstance();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void RegisterInstance()
    {
        if (Instance == null || Instance == this)
        {
            Instance = this;
        }
    }

    private static SceneReferenceRegistry ResolveInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        SceneReferenceRegistry registry = FindFirstObjectByType<SceneReferenceRegistry>(FindObjectsInactive.Include);
        if (registry != null)
        {
            registry.RegisterInstance();
        }

        return Instance;
    }

    public static bool TryResolve<T>(out T reference) where T : Object
    {
        SceneReferenceRegistry registry = ResolveInstance();
        if (registry == null)
        {
            reference = null;
            return false;
        }

        reference = registry.Resolve<T>();
        return reference != null;
    }

    public static bool TryResolveTransform(string objectName, out Transform reference)
    {
        reference = null;
        SceneReferenceRegistry registry = ResolveInstance();
        if (registry == null || string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        if (objectName == LayerUtility.DefaultGridName)
        {
            reference = registry.grid != null ? registry.grid.transform : null;
        }
        else if (objectName == LayerUtility.DefaultImportButtonName)
        {
            reference = registry.importButton != null ? registry.importButton.transform : null;
        }

        return reference != null;
    }

    public static bool TryResolveWallRoot(out Transform reference)
    {
        reference = null;
        SceneReferenceRegistry registry = ResolveInstance();
        if (registry == null)
        {
            return false;
        }

        reference = registry.wallRoot;
        return reference != null;
    }

    public static bool TryResolveCanvas(string canvasName, out Canvas reference)
    {
        reference = null;
        SceneReferenceRegistry registry = ResolveInstance();
        if (registry == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(canvasName) || canvasName == LayerUtility.DefaultCanvasName)
        {
            reference = registry.defaultCanvas;
        }
        else if (canvasName == LayerUtility.DefaultHandleCanvasName)
        {
            reference = registry.handleCanvas;
        }
        else if (canvasName == LayerUtility.DefaultWallUICanvasName)
        {
            reference = registry.wallUiCanvas;
        }

        return reference != null;
    }

    private T Resolve<T>() where T : Object
    {
        if (typeof(T) == typeof(Camera))
        {
            return mainCamera as T;
        }

        if (typeof(T) == typeof(ModeManager))
        {
            return modeManager as T;
        }

        if (typeof(T) == typeof(RoomManager))
        {
            return roomManager as T;
        }

        if (typeof(T) == typeof(DrawManager))
        {
            return drawManager as T;
        }

        if (typeof(T) == typeof(HandleManager))
        {
            return handleManager as T;
        }

        if (typeof(T) == typeof(SnapManager))
        {
            return snapManager as T;
        }

        if (typeof(T) == typeof(WallSelectionManager))
        {
            return wallSelectionManager as T;
        }

        if (typeof(T) == typeof(WallOpeningPlacementManager))
        {
            return wallOpeningPlacementManager as T;
        }

        if (typeof(T) == typeof(WallLengthDisplay))
        {
            return wallLengthDisplay as T;
        }

        if (typeof(T) == typeof(UndoRedoManager))
        {
            return undoRedoManager as T;
        }

        if (typeof(T) == typeof(TopViewRenderManager))
        {
            return topViewRenderManager as T;
        }

        if (typeof(T) == typeof(RoomAuthoringPanelManager))
        {
            return roomAuthoringPanelManager as T;
        }

        if (typeof(T) == typeof(RoomWallAuthoringPanelController))
        {
            return roomWallAuthoringPanelController as T;
        }

        if (typeof(T) == typeof(RoomHandleManager))
        {
            return roomHandleManager as T;
        }

        if (typeof(T) == typeof(DrawingOverlayManager))
        {
            return drawingOverlayManager as T;
        }

        if (typeof(T) == typeof(OverlayCalibrationPanelController))
        {
            return overlayCalibrationPanelController as T;
        }

        if (typeof(T) == typeof(Canvas))
        {
            return defaultCanvas as T;
        }

        if (typeof(T) == typeof(Transform))
        {
            return wallRoot as T;
        }

        if (typeof(T) == typeof(GameObject))
        {
            return grid as T;
        }

        if (typeof(T) == typeof(Button))
        {
            return importButton as T;
        }

        return null;
    }
}
