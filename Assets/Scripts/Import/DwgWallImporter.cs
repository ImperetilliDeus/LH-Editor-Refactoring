using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityColor = UnityEngine.Color;
using UDebug = UnityEngine.Debug;
using UnityMesh = UnityEngine.Mesh;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

[AddComponentMenu("LH Editor/Import/DWG Wall Importer")]
public sealed class DwgWallImporter : MonoBehaviour
{
    private const string DefaultTargetLayerKeyword = "WALL";

    [Header("File")]
    [SerializeField] private string cadFilePath = string.Empty;
    [SerializeField, HideInInspector] private string importerOwnershipId = string.Empty;

    [Header("References")]
    [SerializeField] private Transform wallRoot;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private Button importButton;

    [Header("Import Settings Popup")]
    [SerializeField] private Canvas importSettingsPopupCanvas;
    [SerializeField] private GameObject importSettingsPopupPrefab;
    [SerializeField] private GameObject importSettingsPopupRoot;
    [SerializeField] private Text popupSelectedPathText;
    [SerializeField] private InputField popupCadScaleInputField;
    [SerializeField] private InputField popupLayerSearchInputField;
    [SerializeField] private Transform popupLayerToggleContainer;
    [SerializeField] private Toggle popupLayerTogglePrefab;
    [SerializeField] private Button popupSelectAllLayersButton;
    [SerializeField] private Button popupClearAllLayersButton;
    [SerializeField] private Button popupCancelButton;
    [SerializeField] private Button popupConfirmButton;

    [Header("Wall Size")]
    [SerializeField] private float wallHeight = 22f;
    [SerializeField] private float wallThickness = 1.5f;
    [SerializeField] private float wallSurfaceOffset = Wall.DefaultTopFaceOffset;
    [SerializeField] private float minimumWallLength = 0.01f;

    [Header("CAD Mapping")]
    [SerializeField] private float cadUnitToWorldScale = 0.01f;
    [SerializeField] private bool invertCadY = false;
    [SerializeField] private float drawingPlaneY = 0f;
    [SerializeField] private Vector3 importOffset = Vector3.zero;
    [SerializeField] private bool autoCenterImportAtOrigin = true;

    [Header("Filter")]
    [SerializeField] private string[] includedLayers = Array.Empty<string>();
    [SerializeField] private string[] excludedLayers = Array.Empty<string>();
    [SerializeField] private bool includeInvisibleEntities;
    [SerializeField] private bool deduplicateSegments = true;
    [SerializeField] private float deduplicateTolerance = 0.001f;

    [Header("Import Behavior")]
    [SerializeField] private bool clearExistingWalls = true;
    [SerializeField] private bool clearExistingRooms = true;
    [SerializeField] private bool refreshRoomsAfterImport = true;

    [Header("Visual")]
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material wallTopMaterial;
    [SerializeField] private UnityColor fallbackWallColor = new UnityColor(0.78f, 0.78f, 0.78f, 1f);

    [Header("Import Filter")]
    [Tooltip("Only layers containing this keyword are treated as walls.")]
    [SerializeField] private string targetLayerKeyword = DefaultTargetLayerKeyword;

    private UnityMesh cachedCubeMesh;
    private readonly List<CadWallSegment> segments = new List<CadWallSegment>();
    private readonly List<string> warnings = new List<string>();
    private readonly DwgWallImportPopupState popupState = new DwgWallImportPopupState();
    private readonly DwgWallImportPopupController popupController = new DwgWallImportPopupController();
    private readonly DwgWallImportPopupValidationService popupValidationService = new DwgWallImportPopupValidationService();
    private readonly DwgWallImportExecutionBuilder executionBuilder = new DwgWallImportExecutionBuilder();
    private readonly DwgWallImportProcessingService processingService = new DwgWallImportProcessingService();
    private readonly DwgWallImportApplyService applyService = new DwgWallImportApplyService();

    public string CadFilePath
    {
        get => cadFilePath;
        set => cadFilePath = value ?? string.Empty;
    }

    [ContextMenu("Import Configured CAD File")]
    public void ImportFromConfiguredFile()
    {
        ImportFromPath(cadFilePath);
    }

    public void OpenFileDialogAndImport()
    {
        string path = ShowOpenCadFileDialog();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        ShowImportSettingsPopup(path);
    }

    private void Reset()
    {
        EnsureImporterOwnershipId();
        ResolveReferences();
    }

    private void Awake()
    {
        EnsureImporterOwnershipId();
        ResolveReferences();
        BindImportButton();
        BindPopupButtons();
    }

    private void OnDestroy()
    {
        UnbindImportButton();
        UnbindPopupButtons();
        DestroyImportSettingsPopup();
    }

    private void OnValidate()
    {
        EnsureImporterOwnershipId();
        wallHeight = Mathf.Max(0.1f, wallHeight);
        wallThickness = Mathf.Max(0.01f, wallThickness);
        wallSurfaceOffset = Mathf.Max(0f, wallSurfaceOffset);
        minimumWallLength = Mathf.Max(0.001f, minimumWallLength);
        cadUnitToWorldScale = Mathf.Max(0.000001f, cadUnitToWorldScale);
        deduplicateTolerance = Mathf.Max(0.000001f, deduplicateTolerance);
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveTransformByName(ref wallRoot, LayerUtility.DefaultWallRootName, true);
        LayerUtility.ResolveObject(ref handleManager);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref wallLengthDisplay);
        importButton = ResolveButton(importButton, LayerUtility.DefaultImportButtonName);
        LayerUtility.ResolveCanvasByNameOrFirst(ref importSettingsPopupCanvas, LayerUtility.DefaultCanvasName);
    }

    private void EnsureImporterOwnershipId()
    {
        if (!string.IsNullOrWhiteSpace(importerOwnershipId))
        {
            return;
        }

        importerOwnershipId = Guid.NewGuid().ToString("N");
    }

    private void BindImportButton()
    {
        if (importButton == null)
        {
            return;
        }

        importButton.onClick.RemoveListener(OpenFileDialogAndImport);
        importButton.onClick.AddListener(OpenFileDialogAndImport);
    }

    private void UnbindImportButton()
    {
        if (importButton == null)
        {
            return;
        }

        importButton.onClick.RemoveListener(OpenFileDialogAndImport);
    }

    private void BindPopupButtons()
    {
        ResolvePopupReferencesFromRoot();

        if (popupCancelButton != null)
        {
            popupCancelButton.onClick.RemoveListener(CloseImportSettingsPopup);
            popupCancelButton.onClick.AddListener(CloseImportSettingsPopup);
        }

        if (popupSelectAllLayersButton != null)
        {
            popupSelectAllLayersButton.onClick.RemoveListener(HandleSelectAllPopupLayers);
            popupSelectAllLayersButton.onClick.AddListener(HandleSelectAllPopupLayers);
        }

        if (popupClearAllLayersButton != null)
        {
            popupClearAllLayersButton.onClick.RemoveListener(HandleClearAllPopupLayers);
            popupClearAllLayersButton.onClick.AddListener(HandleClearAllPopupLayers);
        }

        if (popupLayerSearchInputField != null)
        {
            popupLayerSearchInputField.onValueChanged.RemoveListener(HandlePopupLayerSearchChanged);
            popupLayerSearchInputField.onValueChanged.AddListener(HandlePopupLayerSearchChanged);
        }

        if (popupConfirmButton != null)
        {
            popupConfirmButton.onClick.RemoveListener(ConfirmImportSettingsAndImport);
            popupConfirmButton.onClick.AddListener(ConfirmImportSettingsAndImport);
        }
    }

    private void UnbindPopupButtons()
    {
        if (popupCancelButton != null)
        {
            popupCancelButton.onClick.RemoveListener(CloseImportSettingsPopup);
        }

        if (popupSelectAllLayersButton != null)
        {
            popupSelectAllLayersButton.onClick.RemoveListener(HandleSelectAllPopupLayers);
        }

        if (popupClearAllLayersButton != null)
        {
            popupClearAllLayersButton.onClick.RemoveListener(HandleClearAllPopupLayers);
        }

        if (popupLayerSearchInputField != null)
        {
            popupLayerSearchInputField.onValueChanged.RemoveListener(HandlePopupLayerSearchChanged);
        }

        if (popupConfirmButton != null)
        {
            popupConfirmButton.onClick.RemoveListener(ConfirmImportSettingsAndImport);
        }
    }

    private void ResolvePopupReferencesFromRoot()
    {
        if (importSettingsPopupRoot == null)
        {
            return;
        }

        Transform popupRootTransform = importSettingsPopupRoot.transform;
        if (popupSelectedPathText == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "PathValue");
            popupSelectedPathText = target != null ? target.GetComponent<Text>() : null;
        }

        if (popupCadScaleInputField == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "ScaleInput");
            popupCadScaleInputField = target != null ? target.GetComponent<InputField>() : null;
        }

        if (popupLayerSearchInputField == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "LayerSearchInput");
            popupLayerSearchInputField = target != null ? target.GetComponent<InputField>() : null;
        }

        if (popupLayerToggleContainer == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "LayerToggleContainer");
            popupLayerToggleContainer = target;
        }

        if (popupLayerTogglePrefab == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "LayerToggleTemplate");
            popupLayerTogglePrefab = target != null ? target.GetComponent<Toggle>() : null;
        }

        if (popupCancelButton == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "CancelButton");
            popupCancelButton = target != null ? target.GetComponent<Button>() : null;
        }

        if (popupSelectAllLayersButton == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "SelectAllLayersButton");
            popupSelectAllLayersButton = target != null ? target.GetComponent<Button>() : null;
        }

        if (popupClearAllLayersButton == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "ClearAllLayersButton");
            popupClearAllLayersButton = target != null ? target.GetComponent<Button>() : null;
        }

        if (popupConfirmButton == null)
        {
            Transform target = LayerUtility.FindChildByName(popupRootTransform, "ImportButton");
            popupConfirmButton = target != null ? target.GetComponent<Button>() : null;
        }
    }

    private static Button ResolveButton(Button currentButton, string objectName)
    {
        if (currentButton != null || string.IsNullOrWhiteSpace(objectName))
        {
            return currentButton;
        }

        Transform target = LayerUtility.FindTransformByName(objectName, true);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private void ShowImportSettingsPopup(string path)
    {
        popupState.PendingImportPath = path ?? string.Empty;
        LoadAvailableLayersForPopup(popupState.PendingImportPath);
        EnsureImportSettingsPopup();
        if (importSettingsPopupRoot == null)
        {
            ImportFromPath(popupState.PendingImportPath);
            return;
        }

        if (popupCadScaleInputField != null)
        {
            popupCadScaleInputField.SetTextWithoutNotify(cadUnitToWorldScale.ToString("0.#######", System.Globalization.CultureInfo.InvariantCulture));
        }

        popupLayerSearchInputField?.SetTextWithoutNotify(string.Empty);

        if (popupSelectedPathText != null)
        {
            popupSelectedPathText.text = popupState.PendingImportPath;
        }

        PopulateLayerToggleList();

        importSettingsPopupRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (popupLayerToggleContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
        UDebug.Log($"[{nameof(DwgWallImporter)}] Popup layers detected: {popupController.AvailableLayerCount}, visible toggles: {CountVisiblePopupLayerToggles()}", this);
        popupCadScaleInputField?.ActivateInputField();
    }

    private void EnsureImportSettingsPopup()
    {
        if (importSettingsPopupRoot != null)
        {
            BindPopupButtons();
            return;
        }

        if (importSettingsPopupPrefab != null)
        {
            Canvas canvas = importSettingsPopupCanvas != null
                ? importSettingsPopupCanvas
                : LayerUtility.FindCanvasByNameOrFirst(LayerUtility.DefaultCanvasName);
            if (canvas == null)
            {
                UDebug.LogWarning($"[{nameof(DwgWallImporter)}] No canvas was found for the import settings popup. Falling back to direct import.", this);
                return;
            }

            importSettingsPopupRoot = Instantiate(importSettingsPopupPrefab, canvas.transform);
            importSettingsPopupRoot.name = importSettingsPopupPrefab.name;
            popupState.OwnsRuntimeImportSettingsPopup = true;
            ResolvePopupReferencesFromRoot();
            BindPopupButtons();
            importSettingsPopupRoot.SetActive(false);
            return;
        }
    }

    private void ConfirmImportSettingsAndImport()
    {
        if (!popupValidationService.TryApplyCadScale(popupCadScaleInputField, this, out float parsedScale))
        {
            return;
        }

        if (popupCadScaleInputField != null)
        {
            cadUnitToWorldScale = parsedScale;
        }

        if (!popupValidationService.ValidateLayerSelection(popupController, CountVisiblePopupLayerToggles(), this))
        {
            return;
        }

        ApplySelectedPopupLayersToImportFilter();

        string path = popupState.PendingImportPath;
        CloseImportSettingsPopup();
        ImportFromPath(path);
    }

    private void CloseImportSettingsPopup()
    {
        popupState.PendingImportPath = string.Empty;
        if (importSettingsPopupRoot != null)
        {
            importSettingsPopupRoot.SetActive(false);
        }
    }

    private void DestroyImportSettingsPopup()
    {
        if (importSettingsPopupRoot == null)
        {
            return;
        }

        if (popupState.OwnsRuntimeImportSettingsPopup)
        {
            DestroySafely(importSettingsPopupRoot);
        }
        else
        {
            importSettingsPopupRoot.SetActive(false);
        }

        importSettingsPopupRoot = null;
        popupSelectedPathText = null;
        popupCadScaleInputField = null;
        popupLayerSearchInputField = null;
        popupLayerToggleContainer = null;
        popupLayerTogglePrefab = null;
        popupSelectAllLayersButton = null;
        popupClearAllLayersButton = null;
        popupCancelButton = null;
        popupConfirmButton = null;
        popupController.Reset();
        popupState.Reset();
    }

    private void LoadAvailableLayersForPopup(string path)
    {
        List<string> availableLayers = new List<string>();
        string resolvedPath = CadWallImportService.ResolveFilePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            popupController.SetAvailableLayers(availableLayers);
            return;
        }

        try
        {
            availableLayers.AddRange(CadWallImportService.LoadAvailableLayers(resolvedPath));
        }
        catch (Exception ex)
        {
            UDebug.LogWarning($"[{nameof(DwgWallImporter)}] Failed to read layer list for popup: {ex.Message}", this);
        }

        popupController.SetAvailableLayers(availableLayers);
    }

    private void PopulateLayerToggleList()
    {
        ResolvePopupReferencesFromRoot();
        popupController.PopulateLayerToggleList(
            popupLayerToggleContainer,
            popupLayerTogglePrefab,
            popupLayerSearchInputField != null ? popupLayerSearchInputField.text : string.Empty,
            layerName => CadWallImportService.ShouldImportLayerByDefault(layerName, CreateImportSettings()),
            DestroySafely);
    }

    private void ClearPopupLayerToggles()
    {
        popupController.ClearPopupLayerToggles(DestroySafely);
    }

    private void ApplySelectedPopupLayersToImportFilter()
    {
        includedLayers = popupController.GetSelectedLayers();
    }

    private void HandlePopupLayerSearchChanged(string searchText)
    {
        ApplyPopupLayerSearchFilter(searchText);
    }

    private void HandleSelectAllPopupLayers()
    {
        SetPopupLayerSelectionState(true);
    }

    private void HandleClearAllPopupLayers()
    {
        SetPopupLayerSelectionState(false);
    }

    private void SetPopupLayerSelectionState(bool selected)
    {
        popupController.SetPopupLayerSelectionState(
            selected,
            popupLayerSearchInputField != null ? popupLayerSearchInputField.text : string.Empty);
    }

    private void ApplyPopupLayerSearchFilter(string searchText)
    {
        popupController.ApplyPopupLayerSearchFilter(popupLayerToggleContainer, searchText);
    }

    private bool HasAnyPopupLayerSelected()
    {
        return popupController.HasAnyPopupLayerSelected();
    }

    private int CountVisiblePopupLayerToggles()
    {
        return popupController.CountVisiblePopupLayerToggles();
    }

    public void ImportFromPath(string path)
    {
        UDebug.Log($"[1/6] Import process started: {path}", this);

        if (!processingService.TryResolveImportPath(path, this, out string resolvedPath))
        {
            return;
        }

        UDebug.Log($"[1/6] Import started: {resolvedPath}", this);

        cadFilePath = resolvedPath;

        if (!processingService.TryParse(resolvedPath, CreateImportSettings(), this, out CadWallImportParseResult parseResult))
        {
            return;
        }

        if (parseResult == null)
        {
            UDebug.LogError("[DwgWallImporter] The CAD document is empty.", this);
            return;
        }

        UDebug.Log($"[4/6] Extracting wall data. Keyword filter: {targetLayerKeyword}", this);
        UDebug.Log($"[{nameof(DwgWallImporter)}] Popup-selected layers: {(includedLayers != null ? includedLayers.Length : 0)}", this);

        processingService.PopulateSegmentsAndWarnings(parseResult, segments, warnings);

        UDebug.Log($"[5/6] Extraction complete. Wall segment count: {segments.Count}", this);

        if (autoCenterImportAtOrigin)
        {
            processingService.CenterSegmentsAtOrigin(segments, drawingPlaneY, importOffset);
        }

        UDebug.Log($"[{nameof(DwgWallImporter)}] Extracted wall segments after filtering: {segments.Count}", this);

        if (segments.Count == 0)
        {
            UDebug.LogWarning($"[DwgWallImporter] No wall segments were found in '{path}'.", this);
            UDebug.Log(processingService.BuildAvailableLayerDebugInfo(parseResult.AvailableLayers), this);
            return;
        }

        UDebug.Log("[6/6] Creating Unity wall objects.", this);

        ResolveReferences();
        wallRoot = executionBuilder.EnsureWallRoot(wallRoot);
        cachedCubeMesh = executionBuilder.EnsureCachedCubeMesh(cachedCubeMesh, DestroySafely);

        // resolvedPath was already normalized above.
        wallMaterial = executionBuilder.ResolveWallMaterial(wallRoot, wallMaterial, fallbackWallColor);
        Material resolvedWallMaterial = wallMaterial;
        wallTopMaterial = executionBuilder.ResolveTopMaterial(wallRoot, wallTopMaterial, resolvedWallMaterial);
        Material resolvedTopMaterial = wallTopMaterial;

        DwgWallImportSceneApplyContext applyContext = executionBuilder.CreateSceneApplyContext(
            importerOwnershipId,
            wallRoot,
            handleManager,
            roomManager,
            wallLengthDisplay,
            resolvedWallMaterial,
            resolvedTopMaterial,
            cachedCubeMesh,
            drawingPlaneY,
            wallHeight,
            wallThickness,
            wallSurfaceOffset,
            minimumWallLength,
            clearExistingWalls,
            clearExistingRooms,
            refreshRoomsAfterImport,
            DestroySafely);
        if (!applyService.TryApply(segments, applyContext, this, out DwgWallImportSceneApplyResult applyResult))
        {
            return;
        }

        applyService.LogWarnings(warnings, this);
        applyService.LogImportSummary(resolvedPath, applyResult, this);
    }


    private CadWallImportSettings CreateImportSettings()
    {
        return executionBuilder.CreateImportSettings(
            cadUnitToWorldScale,
            invertCadY,
            drawingPlaneY,
            importOffset,
            minimumWallLength,
            includeInvisibleEntities,
            deduplicateSegments,
            deduplicateTolerance,
            includedLayers,
            excludedLayers,
            targetLayerKeyword);
    }

    private void DestroySafely(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static string ShowOpenCadFileDialog()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("Select DWG/DXF File", string.Empty, "dwg,dxf");
#else
        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            UnityEngine.Debug.LogWarning($"[{nameof(DwgWallImporter)}] Runtime file dialog is only implemented for Windows Player.");
            return string.Empty;
        }

        try
        {
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildPowerShellArguments(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            return string.Empty;
        }
#endif
    }

    private static string BuildPowerShellArguments()
    {
        string script = @"
Add-Type -AssemblyName System.Windows.Forms
$dialog = New-Object System.Windows.Forms.OpenFileDialog
$dialog.Filter = 'CAD Files (*.dwg;*.dxf)|*.dwg;*.dxf|DWG Files (*.dwg)|*.dwg|DXF Files (*.dxf)|*.dxf|All Files (*.*)|*.*'
$dialog.Multiselect = $false
$dialog.Title = 'Select DWG/DXF File'
if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    Write-Output $dialog.FileName
}";
        return "-NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }
}
