using System;
using System.Collections.Generic;
using System.Globalization;
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
    private sealed class PopupState
    {
        public string PendingImportPath { get; set; } = string.Empty;
        public bool OwnsRuntimeImportSettingsPopup { get; set; }

        public void Reset()
        {
            PendingImportPath = string.Empty;
            OwnsRuntimeImportSettingsPopup = false;
        }
    }

    private sealed class PopupController
    {
        private readonly List<Toggle> popupLayerToggles = new List<Toggle>();
        private readonly List<string> popupAvailableLayers = new List<string>();

        public int AvailableLayerCount => popupAvailableLayers.Count;

        public void SetAvailableLayers(IEnumerable<string> layers)
        {
            popupAvailableLayers.Clear();
            if (layers == null)
            {
                return;
            }

            popupAvailableLayers.AddRange(layers);
        }

        public void PopulateLayerToggleList(
            Transform popupLayerToggleContainer,
            Toggle popupLayerTogglePrefab,
            string searchText,
            Func<string, bool> isSelectedByDefault,
            Action<UnityEngine.Object> destroySafely)
        {
            ClearPopupLayerToggles(destroySafely);

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
                LayoutElement layoutElement = toggle.GetComponent<LayoutElement>() ?? toggle.gameObject.AddComponent<LayoutElement>();
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

                toggle.isOn = isSelectedByDefault?.Invoke(layerName) ?? false;
                popupLayerToggles.Add(toggle);
            }

            ApplyPopupLayerSearchFilter(popupLayerToggleContainer, searchText);
        }

        public void ClearPopupLayerToggles(Action<UnityEngine.Object> destroySafely)
        {
            for (int i = 0; i < popupLayerToggles.Count; i++)
            {
                if (popupLayerToggles[i] != null)
                {
                    destroySafely?.Invoke(popupLayerToggles[i].gameObject);
                }
            }

            popupLayerToggles.Clear();
        }

        public string[] GetSelectedLayers()
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

            return selectedLayers.ToArray();
        }

        public void SetPopupLayerSelectionState(bool selected, string searchText)
        {
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

        public void ApplyPopupLayerSearchFilter(Transform popupLayerToggleContainer, string searchText)
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

        public bool HasAnyPopupLayerSelected()
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

        public int CountVisiblePopupLayerToggles()
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

        public void Reset()
        {
            popupLayerToggles.Clear();
            popupAvailableLayers.Clear();
        }
    }

    private readonly struct PopupBindings
    {
        public PopupBindings(
            Text selectedPathText,
            InputField cadScaleInputField,
            InputField layerSearchInputField,
            Transform layerToggleContainer,
            Toggle layerTogglePrefab,
            Button selectAllLayersButton,
            Button clearAllLayersButton,
            Button cancelButton,
            Button confirmButton)
        {
            SelectedPathText = selectedPathText;
            CadScaleInputField = cadScaleInputField;
            LayerSearchInputField = layerSearchInputField;
            LayerToggleContainer = layerToggleContainer;
            LayerTogglePrefab = layerTogglePrefab;
            SelectAllLayersButton = selectAllLayersButton;
            ClearAllLayersButton = clearAllLayersButton;
            CancelButton = cancelButton;
            ConfirmButton = confirmButton;
        }

        public Text SelectedPathText { get; }
        public InputField CadScaleInputField { get; }
        public InputField LayerSearchInputField { get; }
        public Transform LayerToggleContainer { get; }
        public Toggle LayerTogglePrefab { get; }
        public Button SelectAllLayersButton { get; }
        public Button ClearAllLayersButton { get; }
        public Button CancelButton { get; }
        public Button ConfirmButton { get; }
    }

    private const string DefaultTargetLayerKeyword = "WALL";

    [Header("File")]
    [SerializeField] private string cadFilePath = string.Empty;
    [SerializeField, HideInInspector] private string importerOwnershipId = string.Empty;

    [Header("References")]
    [SerializeField] private Transform wallRoot;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private DrawingOverlayManager drawingOverlayManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private Button importButton;

    [Header("Import Settings Popup")]
    [SerializeField] private Canvas importSettingsPopupCanvas;
    [SerializeField] private GameObject importSettingsPopupPrefab;
    [SerializeField] private GameObject importSettingsPopupRoot;
    [SerializeField] private DwgWallImportPopupView importSettingsPopupView;

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
    private readonly PopupState popupState = new PopupState();
    private readonly PopupController popupController = new PopupController();
    private readonly DwgWallImportExecutionBuilder executionBuilder = new DwgWallImportExecutionBuilder();
    private readonly DwgWallImportProcessingService processingService = new DwgWallImportProcessingService();
    private readonly DwgWallImportApplyService applyService = new DwgWallImportApplyService();
    private PopupBindings popupBindings;

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
        LayerUtility.ResolveWallRoot(ref wallRoot, true, true);
        LayerUtility.ResolveObject(ref handleManager);
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref drawingOverlayManager);
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

        BindButton(popupBindings.CancelButton, CloseImportSettingsPopup);
        BindButton(popupBindings.SelectAllLayersButton, HandleSelectAllPopupLayers);
        BindButton(popupBindings.ClearAllLayersButton, HandleClearAllPopupLayers);
        BindButton(popupBindings.ConfirmButton, ConfirmImportSettingsAndImport);
        if (popupBindings.LayerSearchInputField != null)
        {
            popupBindings.LayerSearchInputField.onValueChanged.RemoveListener(HandlePopupLayerSearchChanged);
            popupBindings.LayerSearchInputField.onValueChanged.AddListener(HandlePopupLayerSearchChanged);
        }
    }

    private void UnbindPopupButtons()
    {
        UnbindButton(popupBindings.CancelButton, CloseImportSettingsPopup);
        UnbindButton(popupBindings.SelectAllLayersButton, HandleSelectAllPopupLayers);
        UnbindButton(popupBindings.ClearAllLayersButton, HandleClearAllPopupLayers);
        UnbindButton(popupBindings.ConfirmButton, ConfirmImportSettingsAndImport);

        if (popupBindings.LayerSearchInputField != null)
        {
            popupBindings.LayerSearchInputField.onValueChanged.RemoveListener(HandlePopupLayerSearchChanged);
        }
    }

    private void ResolvePopupReferencesFromRoot()
    {
        if (importSettingsPopupView != null)
        {
            importSettingsPopupView.RefreshReferences();
        }

        popupBindings = ResolvePopupBindings(
            importSettingsPopupRoot,
            importSettingsPopupView);
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
        string pendingImportPath = path ?? string.Empty;
        popupState.PendingImportPath = pendingImportPath;
        LoadAvailableLayers(pendingImportPath);
        EnsureImportSettingsPopup();
        if (importSettingsPopupRoot == null)
        {
            ImportFromPath(pendingImportPath);
            return;
        }

        if (!TryPreparePopup(pendingImportPath))
        {
            ImportFromPath(pendingImportPath);
        }
    }

    private void EnsureImportSettingsPopup()
    {
        if (!EnsurePopup())
        {
            UDebug.LogWarning($"[{nameof(DwgWallImporter)}] No popup root or popup prefab could be resolved. Falling back to direct import.", this);
            return;
        }

        BindPopupButtons();
    }

    private void ConfirmImportSettingsAndImport()
    {
        if (!TryApplyCadScale(out float parsedScale))
        {
            return;
        }

        if (popupBindings.CadScaleInputField != null)
        {
            cadUnitToWorldScale = parsedScale;
        }

        if (!ValidateLayerSelection())
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
        importSettingsPopupView = null;
        popupBindings = default;
        popupController.Reset();
        popupState.Reset();
    }

    private void PopulateLayerToggleList()
    {
        ResolvePopupReferencesFromRoot();
        popupController.PopulateLayerToggleList(
            popupBindings.LayerToggleContainer,
            popupBindings.LayerTogglePrefab,
            popupBindings.LayerSearchInputField != null ? popupBindings.LayerSearchInputField.text : string.Empty,
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
            popupBindings.LayerSearchInputField != null ? popupBindings.LayerSearchInputField.text : string.Empty);
    }

    private void ApplyPopupLayerSearchFilter(string searchText)
    {
        popupController.ApplyPopupLayerSearchFilter(popupBindings.LayerToggleContainer, searchText);
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
        wallMaterial = executionBuilder.ResolveWallMaterial(wallRoot, wallMaterial, fallbackWallColor);
        Material resolvedWallMaterial = wallMaterial;
        wallTopMaterial = executionBuilder.ResolveTopMaterial(wallRoot, wallTopMaterial, resolvedWallMaterial);
        Material resolvedTopMaterial = wallTopMaterial;

        DwgWallImportSceneApplyContext applyContext = executionBuilder.CreateSceneApplyContext(
            importerOwnershipId,
            wallRoot,
            handleManager,
            wallSelectionManager,
            roomManager,
            drawingOverlayManager,
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

    private PopupBindings ResolvePopupBindings(
        GameObject popupRoot,
        DwgWallImportPopupView popupView)
    {
        DwgWallImportPopupView resolvedView = popupView != null
            ? popupView
            : popupRoot != null ? popupRoot.GetComponent<DwgWallImportPopupView>() : null;

        if (resolvedView != null)
        {
            return new PopupBindings(
                resolvedView.SelectedPathText,
                resolvedView.CadScaleInputField,
                resolvedView.LayerSearchInputField,
                resolvedView.LayerToggleContainer,
                resolvedView.LayerTogglePrefab,
                resolvedView.SelectAllLayersButton,
                resolvedView.ClearAllLayersButton,
                resolvedView.CancelButton,
                resolvedView.ConfirmButton);
        }

        if (popupRoot == null)
        {
            return default;
        }

        Transform popupRootTransform = popupRoot.transform;
        return new PopupBindings(
            ResolvePopupComponent<Text>(popupRootTransform, "PathValue"),
            ResolvePopupComponent<InputField>(popupRootTransform, "ScaleInput"),
            ResolvePopupComponent<InputField>(popupRootTransform, "LayerSearchInput"),
            LayerUtility.FindChildByName(popupRootTransform, "LayerToggleContainer"),
            ResolvePopupComponent<Toggle>(popupRootTransform, "LayerToggleTemplate"),
            ResolvePopupComponent<Button>(popupRootTransform, "SelectAllLayersButton"),
            ResolvePopupComponent<Button>(popupRootTransform, "ClearAllLayersButton"),
            ResolvePopupComponent<Button>(popupRootTransform, "CancelButton"),
            ResolvePopupComponent<Button>(popupRootTransform, "ImportButton"));
    }

    private bool EnsurePopup()
    {
        if (importSettingsPopupRoot != null)
        {
            importSettingsPopupView = ResolvePopupView(importSettingsPopupRoot, importSettingsPopupView);
            popupBindings = ResolvePopupBindings(importSettingsPopupRoot, importSettingsPopupView);
            return true;
        }

        if (importSettingsPopupPrefab == null)
        {
            return false;
        }

        Canvas canvas = importSettingsPopupCanvas != null
            ? importSettingsPopupCanvas
            : LayerUtility.FindCanvasByNameOrFirst(LayerUtility.DefaultCanvasName);
        if (canvas == null)
        {
            return false;
        }

        importSettingsPopupRoot = Instantiate(importSettingsPopupPrefab, canvas.transform);
        importSettingsPopupRoot.name = importSettingsPopupPrefab.name;
        popupState.OwnsRuntimeImportSettingsPopup = true;
        importSettingsPopupView = ResolvePopupView(importSettingsPopupRoot, null);
        popupBindings = ResolvePopupBindings(importSettingsPopupRoot, importSettingsPopupView);
        importSettingsPopupRoot.SetActive(false);
        return true;
    }

    private bool TryPreparePopup(string pendingImportPath)
    {
        popupState.PendingImportPath = pendingImportPath ?? string.Empty;
        if (importSettingsPopupRoot == null)
        {
            return false;
        }

        if (popupBindings.CadScaleInputField != null)
        {
            popupBindings.CadScaleInputField.SetTextWithoutNotify(cadUnitToWorldScale.ToString("0.#######", CultureInfo.InvariantCulture));
        }

        popupBindings.LayerSearchInputField?.SetTextWithoutNotify(string.Empty);

        if (popupBindings.SelectedPathText != null)
        {
            popupBindings.SelectedPathText.text = popupState.PendingImportPath;
        }

        PopulateLayerToggleList();

        importSettingsPopupRoot.SetActive(true);
        Canvas.ForceUpdateCanvases();
        if (popupBindings.LayerToggleContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        UDebug.Log(
            $"[{nameof(DwgWallImporter)}] Popup layers detected: {popupController.AvailableLayerCount}, visible toggles: {CountVisiblePopupLayerToggles()}",
            this);
        popupBindings.CadScaleInputField?.ActivateInputField();
        return true;
    }

    private void LoadAvailableLayers(string path)
    {
        List<string> availableLayers = new List<string>();
        string resolvedPath = CadWallImportService.ResolveFilePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !System.IO.File.Exists(resolvedPath))
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

    private bool TryApplyCadScale(out float parsedScale)
    {
        parsedScale = 0f;
        if (popupBindings.CadScaleInputField == null)
        {
            return true;
        }

        string scaleText = popupBindings.CadScaleInputField.text?.Trim();
        if (!float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedScale) ||
            parsedScale <= 0f)
        {
            Debug.LogWarning($"[{nameof(DwgWallImporter)}] Invalid CAD unit scale: '{scaleText}'.", this);
            popupBindings.CadScaleInputField.ActivateInputField();
            return false;
        }

        return true;
    }

    private bool ValidateLayerSelection()
    {
        int visibleLayerToggleCount = CountVisiblePopupLayerToggles();
        if (popupController.AvailableLayerCount == 0 || visibleLayerToggleCount == 0)
        {
            Debug.LogWarning($"[{nameof(DwgWallImporter)}] No visible layer entries are available in the popup. Import was cancelled.", this);
            return false;
        }

        if (!popupController.HasAnyPopupLayerSelected())
        {
            Debug.LogWarning($"[{nameof(DwgWallImporter)}] Select at least one layer before importing.", this);
            return false;
        }

        return true;
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null || callback == null)
        {
            return;
        }

        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    private static void UnbindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null || callback == null)
        {
            return;
        }

        button.onClick.RemoveListener(callback);
    }

    private static T ResolvePopupComponent<T>(Transform root, string childName) where T : Component
    {
        Transform target = LayerUtility.FindChildByName(root, childName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static DwgWallImportPopupView ResolvePopupView(
        GameObject popupRoot,
        DwgWallImportPopupView popupView)
    {
        DwgWallImportPopupView resolvedView = popupView != null ? popupView : popupRoot.GetComponent<DwgWallImportPopupView>();
        if (resolvedView != null)
        {
            resolvedView.RefreshReferences();
        }

        return resolvedView;
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
