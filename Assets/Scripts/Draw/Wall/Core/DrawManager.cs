using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class DrawManager : MonoBehaviour, IEditorModeInputHandler
{
    private const string DefaultWallMaterialPath = "Assets/Prefabs/Furniture/Models/Materials/Wall/W001.mat";

    [SerializeField] private Camera _mainCamera;
    [SerializeField] private GameObject _grid;
    [SerializeField] private Transform _wallRoot;
    [SerializeField] private SnapManager _snapManager;
    [SerializeField] private WallLengthDisplay _wallLengthDisplay;
    [SerializeField] private HandleManager _handleManager;
    [SerializeField] private WallSelectionManager _wallSelectionManager;
    [SerializeField] private UndoRedoManager _undoRedoManager;
    [SerializeField] private ModeManager _modeManager;

    [Header("Input")]
    [SerializeField] private float doubleClickThreshold = 0.25f;

    [Header("Wall Size")]
    [SerializeField] private float _wallHeight = 22f;
    [SerializeField] private float _wallThickness = 1.5f;
    [SerializeField] private float _wallSurfaceOffset = 0.01f;

    [Header("Preview")]
    [SerializeField] private bool _enablePreviewWall = true;
    [SerializeField] private Color _previewColor = new Color(0.2f, 0.8f, 1f, 0.45f);
    [SerializeField] private Material _wallMaterial;
    [SerializeField] private Color _wallColor = new Color(0.78f, 0.78f, 0.78f, 1f);
    [SerializeField] private Material _wallTopMaterial;

    private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();
    private IEditorInputProvider _inputProvider;
    private WallToolRuntime _toolRuntime;
    private WallToolController _toolController;
    private bool _isDefaultModeActive = true;

    public bool IsWallCreationMode => _toolRuntime != null && _toolRuntime.IsWallCreationMode;
    public GameObject PreviewWall => _toolRuntime != null ? _toolRuntime.PreviewWall : null;

    private void Reset()
    {
        _mainCamera = Camera.main;
        ResolveReferences();
        ApplyDefaultWallMaterialIfMissing();
    }

    private void Awake()
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        _inputProvider = EditorInputManager.Instance.InputProvider;
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
        _wallHeight = Mathf.Max(0.1f, _wallHeight);
        _wallThickness = Mathf.Max(0.1f, _wallThickness);
        _wallSurfaceOffset = Mathf.Max(0f, _wallSurfaceOffset);
        ApplyDefaultWallMaterialIfMissing();

        if (!_enablePreviewWall && _toolRuntime != null)
        {
            _toolRuntime.DisablePreviewWall();
        }
    }

    private void OnDestroy()
    {
        UnbindModeEvents();
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterHandler(EditorMode.Default, this);
        }

        _toolRuntime?.Dispose();
        _toolRuntime = null;
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        if (!_isDefaultModeActive || _mainCamera == null || _inputProvider == null || !inputFrame.IsPointerAvailable)
        {
            return;
        }

        _toolController?.HandleInput(BuildToolInputFrame(inputFrame));
    }

    private void BindModeEvents()
    {
        if (_modeManager == null)
        {
            return;
        }

        _modeManager.ModeChanged -= HandleModeChanged;
        _modeManager.ModeChanged += HandleModeChanged;
    }

    private void UnbindModeEvents()
    {
        if (_modeManager == null)
        {
            return;
        }

        _modeManager.ModeChanged -= HandleModeChanged;
    }

    private void SyncModeState()
    {
        HandleModeChanged(_modeManager != null ? _modeManager.CurrentMode : EditorMode.Default);
    }

    private void HandleModeChanged(EditorMode mode)
    {
        bool shouldBeActive = mode == EditorMode.Default;
        if (!shouldBeActive && IsWallCreationMode)
        {
            _toolController?.ActivateEditTool();
        }

        _isDefaultModeActive = shouldBeActive;
        enabled = shouldBeActive;
    }

    private void EnsureWallRoot()
    {
        if (_wallRoot != null)
        {
            return;
        }

        Transform wallRootTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        if (wallRootTransform == null)
        {
            wallRootTransform = new GameObject(LayerUtility.DefaultWallRootName).transform;
        }

        _wallRoot = wallRootTransform;
    }

    private void ResolveReferences()
    {
        if (_snapManager == null)
        {
            LayerUtility.ResolveObject(ref _snapManager);
        }

        if (_wallLengthDisplay == null)
        {
            LayerUtility.ResolveObject(ref _wallLengthDisplay);
        }

        if (_handleManager == null)
        {
            LayerUtility.ResolveObject(ref _handleManager);
        }

        if (_wallSelectionManager == null)
        {
            LayerUtility.ResolveObject(ref _wallSelectionManager);
        }

        if (_undoRedoManager == null)
        {
            LayerUtility.ResolveObject(ref _undoRedoManager);
        }

        if (_modeManager == null)
        {
            LayerUtility.ResolveObject(ref _modeManager);
        }
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(_mainCamera != null, $"{nameof(DrawManager)} requires a Main Camera.", this);
        Debug.Assert(_modeManager != null, $"{nameof(DrawManager)} requires a Mode Manager.", this);
        Debug.Assert(_handleManager != null, $"{nameof(DrawManager)} requires a Handle Manager.", this);
    }

    private void InitializeToolRuntime()
    {
        _toolRuntime = new WallToolRuntime(
            _mainCamera,
            _grid,
            _wallRoot,
            _snapManager,
            _wallLengthDisplay,
            _handleManager,
            _wallSelectionManager,
            _undoRedoManager,
            _inputProvider,
            _enablePreviewWall,
            _wallHeight,
            _wallThickness,
            _wallSurfaceOffset,
            _previewColor,
            _wallMaterial,
            _wallColor,
            _wallTopMaterial,
            _uiRaycastResults);
    }

    private void ApplyDefaultWallMaterialIfMissing()
    {
#if UNITY_EDITOR
        if (_wallMaterial == null)
        {
            _wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultWallMaterialPath);
        }
#endif
    }

    private void InitializeToolController()
    {
        _toolController = new WallToolController(_toolRuntime, doubleClickThreshold);
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
