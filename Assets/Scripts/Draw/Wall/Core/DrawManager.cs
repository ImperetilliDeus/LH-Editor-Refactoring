using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DrawManager : MonoBehaviour, IEditorModeInputHandler
{
    private const string DefaultWallMaterialPath = "Assets/Prefabs/Furniture/Models/Materials/Wall/W001.mat";

    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject grid;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private SnapManager snapManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private UndoRedoManager undoRedoManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private ModeManager modeManager;

    [Header("Input")]
    [SerializeField] private float doubleClickThreshold = 0.25f;

    [Header("Wall Size")]
    [SerializeField] private float wallHeight = 22f;
    [SerializeField] private float wallThickness = 1.5f;
    [SerializeField] private float wallSurfaceOffset = 0.01f;

    [Header("Preview")]
    [SerializeField] private bool enablePreviewWall = true;
    [SerializeField] private Color previewColor = new Color(0.2f, 0.8f, 1f, 0.45f);
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Color wallColor = new Color(0.78f, 0.78f, 0.78f, 1f);
    [SerializeField] private Material wallTopMaterial;

    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private IEditorInputProvider inputProvider;
    private WallToolRuntime toolRuntime;
    private WallToolController toolController;
    private bool isDefaultModeActive = true;

    public bool IsWallCreationMode => toolRuntime != null && toolRuntime.IsWallCreationMode;
    public GameObject PreviewWall => toolRuntime != null ? toolRuntime.PreviewWall : null;

    private void Reset()
    {
        mainCamera = Camera.main;
        ResolveReferences();
        ApplyDefaultWallMaterialIfMissing();
    }

    private void Awake()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        inputProvider = EditorInputManager.Instance.InputProvider;
        ResolveReferences();
        EnsureWallRoot();
        InitializeToolRuntime();
        InitializeToolController();
        BindModeEvents();
        SyncModeState();
        EditorInputManager.Instance.RegisterHandler(EditorMode.Default, this);
        ValidateConfiguration();
    }

    private void OnValidate()
    {
        doubleClickThreshold = Mathf.Max(0.05f, doubleClickThreshold);
        wallHeight = Mathf.Max(0.1f, wallHeight);
        wallThickness = Mathf.Max(0.1f, wallThickness);
        wallSurfaceOffset = Mathf.Max(0f, wallSurfaceOffset);
        ApplyDefaultWallMaterialIfMissing();

        if (!enablePreviewWall && toolRuntime != null)
        {
            toolRuntime.DisablePreviewWall();
        }
    }

    private void OnDestroy()
    {
        UnbindModeEvents();
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterHandler(EditorMode.Default, this);
        }

        toolRuntime?.Dispose();
        toolRuntime = null;
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        if (!isDefaultModeActive || mainCamera == null || inputProvider == null || !inputFrame.IsPointerAvailable)
        {
            return;
        }

        toolController?.HandleInput(BuildToolInputFrame(inputFrame));
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
        bool shouldBeActive = mode == EditorMode.Default;
        if (!shouldBeActive && IsWallCreationMode)
        {
            toolController?.ActivateEditTool();
        }

        isDefaultModeActive = shouldBeActive;
        enabled = shouldBeActive;
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        Transform wallRootTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        if (wallRootTransform == null)
        {
            wallRootTransform = new GameObject(LayerUtility.DefaultWallRootName).transform;
        }

        wallRoot = wallRootTransform;
    }

    private void ResolveReferences()
    {
        if (snapManager == null)
        {
            snapManager = FindFirstObjectByType<SnapManager>();
        }

        if (wallLengthDisplay == null)
        {
            wallLengthDisplay = FindFirstObjectByType<WallLengthDisplay>();
        }

        if (handleManager == null)
        {
            handleManager = FindFirstObjectByType<HandleManager>();
        }

        if (wallSelectionManager == null)
        {
            wallSelectionManager = FindFirstObjectByType<WallSelectionManager>();
        }

        if (undoRedoManager == null)
        {
            undoRedoManager = FindFirstObjectByType<UndoRedoManager>();
        }

        if (modeManager == null)
        {
            modeManager = FindFirstObjectByType<ModeManager>();
        }
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(mainCamera != null, $"{nameof(DrawManager)} requires {nameof(mainCamera)}.", this);
        Debug.Assert(modeManager != null, $"{nameof(DrawManager)} requires {nameof(modeManager)}.", this);
        Debug.Assert(handleManager != null, $"{nameof(DrawManager)} requires {nameof(handleManager)}.", this);
    }

    private void InitializeToolRuntime()
    {
        toolRuntime = new WallToolRuntime(
            mainCamera,
            grid,
            wallRoot,
            snapManager,
            wallLengthDisplay,
            handleManager,
            wallSelectionManager,
            undoRedoManager,
            inputProvider,
            enablePreviewWall,
            wallHeight,
            wallThickness,
            wallSurfaceOffset,
            previewColor,
            wallMaterial,
            wallColor,
            wallTopMaterial,
            uiRaycastResults);
    }

    private void ApplyDefaultWallMaterialIfMissing()
    {
#if UNITY_EDITOR
        if (wallMaterial == null)
        {
            wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultWallMaterialPath);
        }
#endif
    }

    private void InitializeToolController()
    {
        toolController = new WallToolController(toolRuntime, doubleClickThreshold);
    }

    private static WallToolInputFrame BuildToolInputFrame(EditorInputFrame inputFrame)
    {
        if (!inputFrame.IsPointerAvailable)
        {
            return WallToolInputFrame.Unavailable;
        }

        return new WallToolInputFrame(
            inputFrame.PointerScreenPosition,
            inputFrame.LeftPressedThisFrame,
            inputFrame.LeftReleasedThisFrame,
            inputFrame.LeftPressed,
            inputFrame.RightPressedThisFrame,
            inputFrame.DeletePressedThisFrame,
            inputFrame.PointerOverUI);
    }
}
