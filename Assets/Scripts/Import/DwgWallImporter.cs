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
    private const string DefaultImportButtonName = "_ImportButton";
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
    private string pendingImportPath = string.Empty;
    private bool ownsRuntimeImportSettingsPopup;
    private readonly List<Toggle> popupLayerToggles = new List<Toggle>();
    private readonly List<string> popupAvailableLayers = new List<string>();

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
        importButton = ResolveButton(importButton, DefaultImportButtonName);
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
        pendingImportPath = path ?? string.Empty;
        LoadAvailableLayersForPopup(pendingImportPath);
        EnsureImportSettingsPopup();
        if (importSettingsPopupRoot == null)
        {
            ImportFromPath(pendingImportPath);
            return;
        }

        if (popupCadScaleInputField != null)
        {
            popupCadScaleInputField.SetTextWithoutNotify(cadUnitToWorldScale.ToString("0.#######", System.Globalization.CultureInfo.InvariantCulture));
        }

        popupLayerSearchInputField?.SetTextWithoutNotify(string.Empty);

        if (popupSelectedPathText != null)
        {
            popupSelectedPathText.text = pendingImportPath;
        }

        PopulateLayerToggleList();

        importSettingsPopupRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (popupLayerToggleContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
        UDebug.Log($"[{nameof(DwgWallImporter)}] Popup layers detected: {popupAvailableLayers.Count}, toggles created: {popupLayerToggles.Count}, visible toggles: {CountVisiblePopupLayerToggles()}", this);
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
            ownsRuntimeImportSettingsPopup = true;
            ResolvePopupReferencesFromRoot();
            BindPopupButtons();
            importSettingsPopupRoot.SetActive(false);
            return;
        }
    }

    private void ConfirmImportSettingsAndImport()
    {
        if (popupCadScaleInputField != null)
        {
            string scaleText = popupCadScaleInputField.text?.Trim();
            if (!float.TryParse(scaleText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsedScale) ||
                parsedScale <= 0f)
            {
                UDebug.LogWarning($"[{nameof(DwgWallImporter)}] Invalid CAD unit scale: '{scaleText}'.", this);
                popupCadScaleInputField.ActivateInputField();
                return;
            }

            cadUnitToWorldScale = parsedScale;
        }

        if (popupAvailableLayers.Count == 0 || popupLayerToggles.Count == 0 || CountVisiblePopupLayerToggles() == 0)
        {
            UDebug.LogWarning($"[{nameof(DwgWallImporter)}] No visible layer entries are available in the popup. Import was cancelled.", this);
            return;
        }

        if (!HasAnyPopupLayerSelected())
        {
            UDebug.LogWarning($"[{nameof(DwgWallImporter)}] Select at least one layer before importing.", this);
            return;
        }

        ApplySelectedPopupLayersToImportFilter();

        string path = pendingImportPath;
        CloseImportSettingsPopup();
        ImportFromPath(path);
    }

    private void CloseImportSettingsPopup()
    {
        pendingImportPath = string.Empty;
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

        if (ownsRuntimeImportSettingsPopup)
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
        popupLayerToggles.Clear();
        popupAvailableLayers.Clear();
        pendingImportPath = string.Empty;
        ownsRuntimeImportSettingsPopup = false;
    }

    private void LoadAvailableLayersForPopup(string path)
    {
        popupAvailableLayers.Clear();
        string resolvedPath = CadWallImportService.ResolveFilePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return;
        }

        try
        {
            popupAvailableLayers.AddRange(CadWallImportService.LoadAvailableLayers(resolvedPath));
        }
        catch (Exception ex)
        {
            UDebug.LogWarning($"[{nameof(DwgWallImporter)}] Failed to read layer list for popup: {ex.Message}", this);
        }
    }

    private void PopulateLayerToggleList()
    {
        ResolvePopupReferencesFromRoot();
        ClearPopupLayerToggles();

        if (popupLayerToggleContainer == null || popupLayerTogglePrefab == null)
        {
            return;
        }

        for (int i = 0; i < popupAvailableLayers.Count; i++)
        {
            string layerName = popupAvailableLayers[i];
            Toggle toggle = Instantiate(popupLayerTogglePrefab, popupLayerToggleContainer);
            toggle.name = $"LayerToggle_{layerName}";
            toggle.gameObject.SetActive(true);
            toggle.transform.localScale = Vector3.one;
            LayoutElement layoutElement = toggle.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = toggle.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = 28f;
            layoutElement.preferredHeight = 28f;

            if (toggle.transform is RectTransform toggleRect)
            {
                toggleRect.anchorMin = new Vector2(0f, 1f);
                toggleRect.anchorMax = new Vector2(1f, 1f);
                toggleRect.pivot = new Vector2(0.5f, 1f);
                toggleRect.sizeDelta = new Vector2(0f, 28f);
                toggleRect.anchoredPosition = new Vector2(0f, -(i * 36f));
                toggleRect.offsetMin = new Vector2(0f, toggleRect.offsetMin.y);
                toggleRect.offsetMax = new Vector2(0f, toggleRect.offsetMax.y);
            }

            Text label = toggle.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = layerName;
            }

            toggle.isOn = CadWallImportService.ShouldImportLayerByDefault(layerName, CreateImportSettings());
            popupLayerToggles.Add(toggle);
        }

        ApplyPopupLayerSearchFilter(popupLayerSearchInputField != null ? popupLayerSearchInputField.text : string.Empty);

        if (popupLayerToggleContainer is RectTransform containerRect)
        {
            containerRect.sizeDelta = new Vector2(containerRect.sizeDelta.x, Mathf.Max(0f, popupAvailableLayers.Count * 36f + 12f));
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private void ClearPopupLayerToggles()
    {
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            if (popupLayerToggles[i] != null)
            {
                DestroySafely(popupLayerToggles[i].gameObject);
            }
        }

        popupLayerToggles.Clear();
    }

    private void ApplySelectedPopupLayersToImportFilter()
    {
        List<string> selectedLayers = new List<string>();
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            Toggle toggle = popupLayerToggles[i];
            if (toggle == null || !toggle.isOn)
            {
                continue;
            }

            Text label = toggle.GetComponentInChildren<Text>(true);
            if (label != null && !string.IsNullOrWhiteSpace(label.text))
            {
                selectedLayers.Add(label.text);
            }
        }

        includedLayers = selectedLayers.ToArray();
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
        string searchText = popupLayerSearchInputField != null ? popupLayerSearchInputField.text : string.Empty;
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            Toggle toggle = popupLayerToggles[i];
            if (toggle == null || !toggle.gameObject.activeSelf)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                Text label = toggle.GetComponentInChildren<Text>(true);
                string layerName = label != null ? label.text : string.Empty;
                if (layerName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            toggle.isOn = selected;
        }
    }

    private void ApplyPopupLayerSearchFilter(string searchText)
    {
        int visibleCount = 0;
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            Toggle toggle = popupLayerToggles[i];
            if (toggle == null)
            {
                continue;
            }

            Text label = toggle.GetComponentInChildren<Text>(true);
            string layerName = label != null ? label.text : string.Empty;
            bool visible = string.IsNullOrWhiteSpace(searchText) ||
                           layerName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
            toggle.gameObject.SetActive(visible);
            if (visible)
            {
                if (toggle.transform is RectTransform toggleRect)
                {
                    toggleRect.anchoredPosition = new Vector2(0f, -(visibleCount * 36f));
                }

                visibleCount++;
            }
        }

        if (popupLayerToggleContainer is RectTransform rectTransform)
        {
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, Mathf.Max(0f, visibleCount * 36f + 12f));
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    private bool HasAnyPopupLayerSelected()
    {
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            if (popupLayerToggles[i] != null && popupLayerToggles[i].isOn)
            {
                return true;
            }
        }

        return false;
    }

    private int CountVisiblePopupLayerToggles()
    {
        int count = 0;
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            Toggle toggle = popupLayerToggles[i];
            if (toggle != null && toggle.gameObject.activeInHierarchy)
            {
                count++;
            }
        }

        return count;
    }

    public void ImportFromPath(string path)
    {
        UDebug.Log($"[1/6] Import process started: {path}", this);

        if (string.IsNullOrWhiteSpace(path))
        {
            UDebug.LogError("[DwgWallImporter] File path is empty or invalid.", this);
            return;
        }

        string resolvedPath = CadWallImportService.ResolveFilePath(path);
        UDebug.Log($"[1/6] Import started: {resolvedPath}", this);

        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            UDebug.LogError($"[{nameof(DwgWallImporter)}] CAD file not found: {path}", this);
            return;
        }

        cadFilePath = resolvedPath;
        segments.Clear();
        warnings.Clear();

        CadWallImportParseResult parseResult;
        try
        {
            UDebug.Log("[2/6] Reading CAD file...", this);
            parseResult = CadWallImportService.Parse(resolvedPath, CreateImportSettings());
            UDebug.Log($"[3/6] CAD file read succeeded. Layer count: {parseResult.AvailableLayers.Count}", this);
        }
        catch (Exception ex)
        {
            UDebug.LogError($"[DwgWallImporter] Failed to read CAD file.\nReason: {ex.Message}\nStack: {ex.StackTrace}", this);
            return;
        }

        if (parseResult == null)
        {
            UDebug.LogError("[DwgWallImporter] The CAD document is empty.", this);
            return;
        }

        UDebug.Log($"[4/6] Extracting wall data. Keyword filter: {targetLayerKeyword}", this);
        UDebug.Log($"[{nameof(DwgWallImporter)}] Popup-selected layers: {(includedLayers != null ? includedLayers.Length : 0)}", this);

        try
        {
            segments.AddRange(parseResult.Segments);
            warnings.AddRange(parseResult.Warnings);
        }
        catch (Exception ex)
        {
            UDebug.LogError($"[DwgWallImporter] Failed while extracting wall data.\nReason: {ex.Message}\nStack: {ex.StackTrace}", this);
            return;
        }

        UDebug.Log($"[5/6] Extraction complete. Wall segment count: {segments.Count}", this);

        if (autoCenterImportAtOrigin)
        {
            CenterSegmentsAtOrigin(segments);
        }

        UDebug.Log($"[{nameof(DwgWallImporter)}] Extracted wall segments after filtering: {segments.Count}", this);

        if (segments.Count == 0)
        {
            UDebug.LogWarning($"[DwgWallImporter] No wall segments were found in '{path}'.", this);
            
            StringBuilder debugInfo = new StringBuilder();
            debugInfo.AppendLine("=== Layers found in CAD document ===");
            foreach (string layerName in parseResult.AvailableLayers)
            {
                debugInfo.AppendLine($"- {layerName}");
            }
            debugInfo.AppendLine("=========================================");
            UDebug.Log(debugInfo.ToString(), this);
            return;
        }

        UDebug.Log("[6/6] Creating Unity wall objects.", this);

        ResolveReferences();
        EnsureWallRoot();
        EnsureCachedResources();

        // resolvedPath was already normalized above.
        
        Material resolvedWallMaterial = ResolveWallMaterial();
        Material resolvedTopMaterial = ResolveTopMaterial();

        DwgWallImportSceneApplyResult applyResult;
        try
        {
            applyResult = DwgWallImportSceneApplier.Apply(segments, CreateSceneApplyContext(resolvedWallMaterial, resolvedTopMaterial));
        }
        catch (Exception ex)
        {
            UDebug.LogError($"[{nameof(DwgWallImporter)}] Failed while applying imported walls.\nReason: {ex.Message}\nStack: {ex.StackTrace}", this);
            return;
        }

        LogWarnings();
        UDebug.Log(
            $"[{nameof(DwgWallImporter)}] Imported {applyResult.CreatedWallCount} wall segments from '{resolvedPath}'. " +
            $"Removed owned walls: {applyResult.RemovedWallCount}, removed auto rooms: {applyResult.RemovedRoomCount}.",
            this);
    }


    private CadWallImportSettings CreateImportSettings()
    {
        return new CadWallImportSettings
        {
            CadUnitToWorldScale = cadUnitToWorldScale,
            InvertCadY = invertCadY,
            DrawingPlaneY = drawingPlaneY,
            ImportOffset = importOffset,
            MinimumWallLength = minimumWallLength,
            IncludeInvisibleEntities = includeInvisibleEntities,
            DeduplicateSegments = deduplicateSegments,
            DeduplicateTolerance = deduplicateTolerance,
            IncludedLayers = includedLayers ?? Array.Empty<string>(),
            ExcludedLayers = excludedLayers ?? Array.Empty<string>(),
            TargetLayerKeyword = targetLayerKeyword ?? string.Empty,
        };
    }

    private DwgWallImportSceneApplyContext CreateSceneApplyContext(Material resolvedWallMaterial, Material resolvedTopMaterial)
    {
        return new DwgWallImportSceneApplyContext
        {
            ImporterId = importerOwnershipId,
            WallRoot = wallRoot,
            HandleManager = handleManager,
            RoomManager = roomManager,
            WallLengthDisplay = wallLengthDisplay,
            WallMaterial = resolvedWallMaterial,
            TopMaterial = resolvedTopMaterial,
            WallMesh = cachedCubeMesh,
            DrawingPlaneY = drawingPlaneY,
            WallHeight = wallHeight,
            WallThickness = wallThickness,
            WallSurfaceOffset = wallSurfaceOffset,
            MinimumWallLength = minimumWallLength,
            ClearExistingWalls = clearExistingWalls,
            ClearExistingRooms = clearExistingRooms,
            RefreshRoomsAfterImport = refreshRoomsAfterImport,
            DestroyObject = DestroySafely,
        };
    }

    private void CenterSegmentsAtOrigin(List<CadWallSegment> segmentDefinitions)
    {
        if (segmentDefinitions == null || segmentDefinitions.Count == 0)
        {
            return;
        }

        Vector3 min = segmentDefinitions[0].Start;
        Vector3 max = segmentDefinitions[0].Start;
        ExpandBounds(segmentDefinitions[0].End, ref min, ref max);

        for (int i = 1; i < segmentDefinitions.Count; i++)
        {
            ExpandBounds(segmentDefinitions[i].Start, ref min, ref max);
            ExpandBounds(segmentDefinitions[i].End, ref min, ref max);
        }

        Vector3 currentCenter = new Vector3((min.x + max.x) * 0.5f, drawingPlaneY, (min.z + max.z) * 0.5f);
        Vector3 targetCenter = new Vector3(importOffset.x, drawingPlaneY, importOffset.z);
        Vector3 recenterOffset = currentCenter - targetCenter;
        if (Mathf.Abs(recenterOffset.x) <= 0.000001f && Mathf.Abs(recenterOffset.z) <= 0.000001f)
        {
            return;
        }

        for (int i = 0; i < segmentDefinitions.Count; i++)
        {
            CadWallSegment definition = segmentDefinitions[i];
            segmentDefinitions[i] = new CadWallSegment(
                definition.Start - recenterOffset,
                definition.End - recenterOffset,
                definition.LayerName,
                definition.SourceType);
        }
    }

    private static void ExpandBounds(Vector3 point, ref Vector3 min, ref Vector3 max)
    {
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        wallRoot = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        if (wallRoot != null)
        {
            return;
        }

        wallRoot = new GameObject(LayerUtility.DefaultWallRootName).transform;
    }

    private void EnsureCachedResources()
    {
        if (cachedCubeMesh != null)
        {
            return;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter filter = cube.GetComponent<MeshFilter>();
        if (filter != null)
        {
            cachedCubeMesh = filter.sharedMesh;
        }

        DestroySafely(cube);
    }

    private Material ResolveWallMaterial()
    {
        if (wallMaterial != null)
        {
            return wallMaterial;
        }

        if (wallRoot != null)
        {
            MeshRenderer existingRenderer = wallRoot.GetComponentInChildren<MeshRenderer>(true);
            if (existingRenderer != null && existingRenderer.sharedMaterial != null)
            {
                return existingRenderer.sharedMaterial;
            }
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

        wallMaterial = new Material(shader)
        {
            color = fallbackWallColor,
        };
        return wallMaterial;
    }

    private Material ResolveTopMaterial()
    {
        if (wallTopMaterial != null)
        {
            return wallTopMaterial;
        }

        if (wallRoot != null)
        {
            Wall existingWall = wallRoot.GetComponentInChildren<Wall>(true);
            if (existingWall != null)
            {
                WallTopFaceVisual topFace = existingWall.GetComponentInChildren<WallTopFaceVisual>(true);
                if (topFace != null && topFace.TryGetComponent(out MeshRenderer renderer) && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }
        }

        return ResolveWallMaterial();
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

    private void LogWarnings()
    {
        if (warnings.Count == 0)
        {
            return;
        }

        HashSet<string> uniqueWarnings = new HashSet<string>(warnings, StringComparer.Ordinal);
        foreach (string warning in uniqueWarnings)
        {
            UDebug.LogWarning($"[{nameof(DwgWallImporter)}] {warning}", this);
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
