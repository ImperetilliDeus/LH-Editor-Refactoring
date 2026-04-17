using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityColor = UnityEngine.Color;
using UDebug = UnityEngine.Debug;
using UnityMesh = UnityEngine.Mesh;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

[AddComponentMenu("LH Editor/Import/DWG Wall Importer")]
public sealed class DwgWallImporter : MonoBehaviour
{
    private const string DefaultImportButtonName = "_ImportButton";

    [Serializable]
    private struct SegmentDefinition
    {
        public Vector3 start;
        public Vector3 end;
        public string layerName;
        public string sourceType;
    }

    [Header("File")]
    [SerializeField] private string cadFilePath = string.Empty;

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
    [SerializeField] private float wallSurfaceOffset = 0.01f;
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
    [Tooltip("이 단어가 포함된 레이어만 벽으로 인식합니다. (예: WALL, 벽체)")]
    [SerializeField] private string targetLayerKeyword = "WALL";

    private UnityMesh cachedCubeMesh;
    private readonly List<SegmentDefinition> segments = new List<SegmentDefinition>();
    private readonly HashSet<string> uniqueSegmentKeys = new HashSet<string>(StringComparer.Ordinal);
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

    // public bool ImportFromPath(string path)
    // {
    //     ResolveReferences();

    //     string resolvedPath = ResolveFilePath(path);
    //     if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
    //     {
    //         UDebug.LogError($"[{nameof(DwgWallImporter)}] CAD file not found: {path}", this);
    //         return false;
    //     }

    //     cadFilePath = resolvedPath;
    //     segments.Clear();
    //     uniqueSegmentKeys.Clear();
    //     warnings.Clear();

    //     CadDocument document;
    //     try
    //     {
    //         document = ReadDocument(resolvedPath);
    //     }
    //     catch (Exception ex)
    //     {
    //         UDebug.LogError($"[{nameof(DwgWallImporter)}] Failed to read CAD file '{resolvedPath}': {ex.Message}", this);
    //         return false;
    //     }

    //     if (document == null)
    //     {
    //         UDebug.LogError($"[{nameof(DwgWallImporter)}] Reader returned no document for '{resolvedPath}'.", this);
    //         return false;
    //     }

    //     ExtractSegments(document, segments);
    //     if (segments.Count == 0)
    //     {
    //         UDebug.LogWarning($"[{nameof(DwgWallImporter)}] No importable wall segments were found in '{resolvedPath}'.", this);
    //         LogWarnings();
    //         return false;
    //     }

    //     Material resolvedWallMaterial = ResolveWallMaterial();
    //     Material resolvedTopMaterial = ResolveTopMaterial();

    //     if (clearExistingWalls)
    //     {
    //         ClearWalls();
    //     }

    //     if (clearExistingRooms)
    //     {
    //         ClearRooms();
    //     }

    //     int createdCount = 0;
    //     for (int i = 0; i < segments.Count; i++)
    //     {
    //         if (TryCreateWall(segments[i], resolvedWallMaterial, resolvedTopMaterial, out GameObject wallObject))
    //         {
    //             createdCount++;
    //             handleManager?.RegisterWall(wallObject);
    //         }
    //     }

    //     handleManager?.RefreshRegisteredWalls();

    //     if (refreshRoomsAfterImport)
    //     {
    //         roomManager?.MarkGraphDirty();
    //         RoomTopologyEvents.RequestRefreshAll();
    //     }

    //     LogWarnings();
    //     UDebug.Log($"[{nameof(DwgWallImporter)}] Imported {createdCount} wall segments from '{resolvedPath}'.", this);
    //     return createdCount > 0;
    // }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
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
        wallHeight = Mathf.Max(0.1f, wallHeight);
        wallThickness = Mathf.Max(0.01f, wallThickness);
        wallSurfaceOffset = Mathf.Max(0f, wallSurfaceOffset);
        minimumWallLength = Mathf.Max(0.001f, minimumWallLength);
        cadUnitToWorldScale = Mathf.Max(0.000001f, cadUnitToWorldScale);
        deduplicateTolerance = Mathf.Max(0.000001f, deduplicateTolerance);
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveTransformByName(ref wallRoot, "Walls", true);
        LayerUtility.ResolveObject(ref handleManager);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref wallLengthDisplay);
        importButton = ResolveButton(importButton, DefaultImportButtonName);
        LayerUtility.ResolveCanvasByNameOrFirst(ref importSettingsPopupCanvas, "_Screen");
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

        EnsureEventSystemExists();

        Canvas canvas = importSettingsPopupCanvas != null
            ? importSettingsPopupCanvas
            : LayerUtility.FindCanvasByNameOrFirst("_Screen");
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("DWGImportPopupCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (importSettingsPopupPrefab != null)
        {
            importSettingsPopupRoot = Instantiate(importSettingsPopupPrefab, canvas.transform);
            importSettingsPopupRoot.name = importSettingsPopupPrefab.name;
            ownsRuntimeImportSettingsPopup = true;
            ResolvePopupReferencesFromRoot();
            BindPopupButtons();
            importSettingsPopupRoot.SetActive(false);
            return;
        }

        GameObject overlayObject = CreatePopupObject("DWGImportSettingsPopup", canvas.transform);
        importSettingsPopupRoot = overlayObject;
        ownsRuntimeImportSettingsPopup = true;
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        StretchRect(overlayRect);

        Image overlayImage = overlayObject.AddComponent<Image>();
        overlayImage.color = new UnityColor(0f, 0f, 0f, 0.55f);

        GameObject panelObject = CreatePopupObject("Panel", overlayRect);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 280f);
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color32(34, 37, 48, 245);

        CreatePopupText("Title", panelRect, "DWG Import Settings", 24, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Vector2(24f, -24f), new Vector2(472f, 30f), UnityColor.white);

        CreatePopupText("PathLabel", panelRect, "Selected File", 14, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Vector2(24f, -62f), new Vector2(472f, 18f), new Color32(180, 188, 214, 255));
        popupSelectedPathText = CreatePopupText("PathValue", panelRect, string.Empty, 13, FontStyle.Normal, TextAnchor.UpperLeft,
            new Vector2(24f, -84f), new Vector2(472f, 36f), new Color32(226, 230, 241, 255));
        popupSelectedPathText.horizontalOverflow = HorizontalWrapMode.Wrap;

        CreatePopupText("ScaleLabel", panelRect, "CAD Unit To World Scale", 16, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Vector2(24f, -126f), new Vector2(220f, 24f), UnityColor.white);
        popupCadScaleInputField = CreatePopupInputField("ScaleInput", panelRect,
            new Vector2(24f, -154f), new Vector2(220f, 40f), "100");

        CreatePopupText("LayerLabel", panelRect, "Layer Search", 16, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Vector2(268f, -126f), new Vector2(228f, 24f), UnityColor.white);
        popupLayerSearchInputField = CreatePopupInputField("LayerSearchInput", panelRect,
            new Vector2(268f, -154f), new Vector2(228f, 40f), "Search layers");

        GameObject layerListLabel = CreatePopupObject("LayerListLabel", panelRect);
        RectTransform layerListLabelRect = layerListLabel.GetComponent<RectTransform>();
        layerListLabelRect.anchorMin = new Vector2(0f, 1f);
        layerListLabelRect.anchorMax = new Vector2(0f, 1f);
        layerListLabelRect.pivot = new Vector2(0f, 1f);
        layerListLabelRect.anchoredPosition = new Vector2(24f, -202f);
        layerListLabelRect.sizeDelta = new Vector2(220f, 20f);
        Text layerListLabelText = layerListLabel.AddComponent<Text>();
        layerListLabelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        layerListLabelText.text = "Detected Layers";
        layerListLabelText.fontSize = 15;
        layerListLabelText.fontStyle = FontStyle.Bold;
        layerListLabelText.alignment = TextAnchor.MiddleLeft;
        layerListLabelText.color = new Color32(213, 218, 244, 255);

        GameObject layerScrollObject = CreatePopupObject("LayerScrollView", panelRect);
        RectTransform layerScrollRect = layerScrollObject.GetComponent<RectTransform>();
        layerScrollRect.anchorMin = new Vector2(0f, 1f);
        layerScrollRect.anchorMax = new Vector2(0f, 1f);
        layerScrollRect.pivot = new Vector2(0f, 1f);
        layerScrollRect.anchoredPosition = new Vector2(24f, -226f);
        layerScrollRect.sizeDelta = new Vector2(340f, 88f);
        Image layerScrollImage = layerScrollObject.AddComponent<Image>();
        layerScrollImage.color = new Color32(24, 27, 36, 255);
        ScrollRect layerScrollView = layerScrollObject.AddComponent<ScrollRect>();

        GameObject viewportObject = CreatePopupObject("Viewport", layerScrollRect);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        StretchRect(viewportRect);
        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color32(24, 27, 36, 0);
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreatePopupObject("LayerToggleContainer", viewportRect);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup layoutGroup = contentObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.spacing = 4f;
        layoutGroup.padding = new RectOffset(8, 8, 8, 8);
        ContentSizeFitter contentSizeFitter = contentObject.AddComponent<ContentSizeFitter>();
        contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        layerScrollView.viewport = viewportRect;
        layerScrollView.content = contentRect;
        layerScrollView.horizontal = false;
        layerScrollView.vertical = true;

        popupLayerToggleContainer = contentRect;
        popupLayerTogglePrefab = CreatePopupLayerToggleTemplate(contentRect);
        popupLayerTogglePrefab.gameObject.SetActive(false);

        popupSelectAllLayersButton = CreatePopupButton("SelectAllLayersButton", panelRect, "Select All",
            new Vector2(376f, -226f), new Vector2(110f, 40f), new Color32(70, 117, 171, 255));

        popupClearAllLayersButton = CreatePopupButton("ClearAllLayersButton", panelRect, "Clear All",
            new Vector2(498f, -226f), new Vector2(110f, 40f), new Color32(91, 100, 122, 255));

        popupCancelButton = CreatePopupButton("CancelButton", panelRect, "Cancel",
            new Vector2(376f, -176f), new Vector2(110f, 40f), new Color32(90, 97, 119, 255));

        popupConfirmButton = CreatePopupButton("ImportButton", panelRect, "Import",
            new Vector2(498f, -176f), new Vector2(110f, 40f), new Color32(95, 132, 255, 255));

        importSettingsPopupRoot.SetActive(false);
        BindPopupButtons();
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

        targetLayerKeyword = string.Empty;
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

    private static GameObject CreatePopupObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Text CreatePopupText(
        string name,
        RectTransform parent,
        string text,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment,
        Vector2 anchoredPosition,
        Vector2 size,
        UnityColor color)
    {
        GameObject obj = CreatePopupObject(name, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text label = obj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        return label;
    }

    private static InputField CreatePopupInputField(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, string placeholder)
    {
        GameObject inputObject = CreatePopupObject(name, parent);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 1f);
        inputRect.anchorMax = new Vector2(0f, 1f);
        inputRect.pivot = new Vector2(0f, 1f);
        inputRect.anchoredPosition = anchoredPosition;
        inputRect.sizeDelta = size;

        Image background = inputObject.AddComponent<Image>();
        background.color = new Color32(246, 247, 250, 255);

        InputField inputField = inputObject.AddComponent<InputField>();
        inputField.lineType = InputField.LineType.SingleLine;

        Text textComponent = CreatePopupText("Text", inputRect, string.Empty, 18, FontStyle.Normal, TextAnchor.MiddleLeft,
            new Vector2(12f, -7f), new Vector2(size.x - 24f, size.y - 14f), new Color32(22, 25, 33, 255));
        textComponent.supportRichText = false;
        inputField.textComponent = textComponent;

        Text placeholderText = CreatePopupText("Placeholder", inputRect, placeholder, 18, FontStyle.Italic, TextAnchor.MiddleLeft,
            new Vector2(12f, -7f), new Vector2(size.x - 24f, size.y - 14f), new Color32(130, 136, 152, 255));
        placeholderText.supportRichText = false;
        inputField.placeholder = placeholderText;
        return inputField;
    }

    private static Button CreatePopupButton(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, UnityColor color)
    {
        GameObject buttonObject = CreatePopupObject(name, parent);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0f, 1f);
        buttonRect.anchorMax = new Vector2(0f, 1f);
        buttonRect.pivot = new Vector2(0f, 1f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = color;

        Button button = buttonObject.AddComponent<Button>();
        Text label = CreatePopupText("Label", buttonRect, text, 18, FontStyle.Bold, TextAnchor.MiddleCenter,
            Vector2.zero, size, UnityColor.white);
        RectTransform labelRect = label.rectTransform;
        StretchRect(labelRect);
        return button;
    }

    private static Toggle CreatePopupLayerToggleTemplate(RectTransform parent)
    {
        GameObject toggleObject = CreatePopupObject("LayerToggleTemplate", parent);
        RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(0.5f, 1f);
        toggleRect.sizeDelta = new Vector2(0f, 28f);
        LayoutElement layoutElement = toggleObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 28f;
        layoutElement.preferredHeight = 28f;
        HorizontalLayoutGroup rowLayout = toggleObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.spacing = 8f;
        rowLayout.padding = new RectOffset(8, 8, 4, 4);

        Toggle toggle = toggleObject.AddComponent<Toggle>();
        Image rowBackground = toggleObject.AddComponent<Image>();
        rowBackground.color = new Color32(38, 44, 56, 255);
        toggle.targetGraphic = rowBackground;

        GameObject backgroundObject = CreatePopupObject("Background", toggleRect);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = new Vector2(20f, 20f);
        LayoutElement backgroundLayout = backgroundObject.AddComponent<LayoutElement>();
        backgroundLayout.minWidth = 20f;
        backgroundLayout.preferredWidth = 20f;
        backgroundLayout.minHeight = 20f;
        backgroundLayout.preferredHeight = 20f;
        Image backgroundImage = backgroundObject.AddComponent<Image>();
        backgroundImage.color = new Color32(240, 242, 248, 255);

        GameObject checkmarkObject = CreatePopupObject("Checkmark", backgroundRect);
        RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
        StretchRect(checkmarkRect);
        checkmarkRect.offsetMin = new Vector2(4f, 4f);
        checkmarkRect.offsetMax = new Vector2(-4f, -4f);
        Image checkmarkImage = checkmarkObject.AddComponent<Image>();
        checkmarkImage.color = new Color32(95, 132, 255, 255);

        Text label = CreatePopupText("Label", toggleRect, "Layer", 16, FontStyle.Normal, TextAnchor.MiddleLeft,
            Vector2.zero, new Vector2(280f, 24f), new Color32(235, 239, 248, 255));
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.minHeight = 20f;

        toggle.graphic = checkmarkImage;
        toggle.isOn = true;

        return toggle;
    }

    private void LoadAvailableLayersForPopup(string path)
    {
        popupAvailableLayers.Clear();
        string resolvedPath = ResolveFilePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return;
        }

        try
        {
            CadDocument document = ReadDocument(resolvedPath);
            if (document == null)
            {
                return;
            }

            HashSet<string> uniqueLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CollectLayerNamesFromEntities(document.Entities, uniqueLayers);

            if (document.Layers != null)
            {
                foreach (var layer in document.Layers)
                {
                    if (layer == null || string.IsNullOrWhiteSpace(layer.Name))
                    {
                        continue;
                    }

                    uniqueLayers.Add(layer.Name);
                }
            }

            popupAvailableLayers.AddRange(uniqueLayers);
            popupAvailableLayers.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            UDebug.LogWarning($"[{nameof(DwgWallImporter)}] Failed to read layer list for popup: {ex.Message}", this);
        }
    }

    private void CollectLayerNamesFromEntities(IEnumerable<Entity> entities, HashSet<string> results)
    {
        if (entities == null || results == null)
        {
            return;
        }

        foreach (Entity entity in entities)
        {
            if (entity == null)
            {
                continue;
            }

            string layerName = GetLayerName(entity);
            if (!string.IsNullOrWhiteSpace(layerName))
            {
                results.Add(layerName);
            }

            if (entity is Insert insert && insert.Block != null)
            {
                CollectLayerNamesFromEntities(insert.Block.Entities, results);
            }
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

            bool isIncluded = includedLayers == null || includedLayers.Length == 0
                ? string.IsNullOrWhiteSpace(targetLayerKeyword) || layerName.IndexOf(targetLayerKeyword, StringComparison.OrdinalIgnoreCase) >= 0
                : Array.Exists(includedLayers, item => string.Equals(item, layerName, StringComparison.OrdinalIgnoreCase));
            toggle.isOn = isIncluded;
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
        // [1단계] 시작 확인
        UDebug.Log($"[1/6] 임포트 프로세스 시작: {path}", this);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            UDebug.LogError("[DwgWallImporter] 파일 경로가 비어있거나 존재하지 않습니다.", this);
            return;
        }

        CadDocument cadDocument = null;
        try
        {
            UDebug.Log($"[2/6] 파일 읽기 시도 중... (파일 크기에 따라 몇 초 정도 걸릴 수 있습니다)", this);
            
            // DXF 파일과 DWG 파일을 구분하여 읽기
            string ext = Path.GetExtension(path).ToLower();
            if (ext == ".dxf")
            {
                using (DxfReader reader = new DxfReader(path))
                {
                    cadDocument = reader.Read();
                }
            }
            else
            {
                using (DwgReader reader = new DwgReader(path))
                {
                    cadDocument = reader.Read();
                }
            }
            
            UDebug.Log($"[3/6] 파일 읽기 성공! (도면 객체 총 개수: {cadDocument?.Entities?.Count})", this);
        }
        catch (Exception ex)
        {
            // 에러가 발생하면 무조건 빨간색으로 표시!
            UDebug.LogError($"[DwgWallImporter] 🚨 파일 읽기 실패!\n이유: {ex.Message}\n위치: {ex.StackTrace}", this);
            return;
        }

        if (cadDocument == null || cadDocument.Entities == null)
        {
            UDebug.LogError("[DwgWallImporter] 도면을 읽었으나 데이터가 비어있습니다.", this);
            return;
        }

        UDebug.Log($"[4/6] 벽체 데이터 추출 시작... (필터: {targetLayerKeyword})", this);
        List<SegmentDefinition> segments = new List<SegmentDefinition>();
        UDebug.Log($"[{nameof(DwgWallImporter)}] Popup-selected layers: {(includedLayers != null ? includedLayers.Length : 0)}", this);
        
        try 
        {
            uniqueSegmentKeys.Clear();
            ExtractSegments(cadDocument, segments);
        }
        catch (Exception ex)
        {
            UDebug.LogError($"[DwgWallImporter] 🚨 벽체 추출 중 에러 발생!\n이유: {ex.Message}\n위치: {ex.StackTrace}", this);
            return;
        }

        UDebug.Log($"[5/6] 추출 완료! 최종적으로 찾은 벽체 선분 개수: {segments.Count}", this);

        if (autoCenterImportAtOrigin)
        {
            CenterSegmentsAtOrigin(segments);
        }

        UDebug.Log($"[{nameof(DwgWallImporter)}] Extracted wall segments after filtering: {segments.Count}", this);

        if (segments.Count == 0)
        {
            UDebug.LogWarning($"[DwgWallImporter] '{path}'에서 변환할 벽체를 찾지 못했습니다.", this);
            
            StringBuilder debugInfo = new StringBuilder();
            debugInfo.AppendLine("=== 🔍 도면에 존재하는 실제 레이어 목록 ===");
            foreach (var layer in cadDocument.Layers)
            {
                debugInfo.AppendLine($"- {layer.Name}");
            }
            debugInfo.AppendLine("=========================================");
            UDebug.Log(debugInfo.ToString(), this);
            return;
        }

        UDebug.Log($"[6/6] 유니티 3D 벽체 생성 로직으로 데이터 전달 중...", this);

        ResolveReferences();

        string resolvedPath = ResolveFilePath(path);
        
        // 💡 주의: 이 아래에는 기존에 작성하셨던 유니티 벽 생성 로직 (HandleManager, RoomManager 호출 등)이 와야 합니다!
            Material resolvedWallMaterial = ResolveWallMaterial();
            Material resolvedTopMaterial = ResolveTopMaterial();

            if (clearExistingWalls)
            {
                ClearWalls();
            }

            if (clearExistingRooms)
            {
                ClearRooms();
            }

            int createdCount = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                if (TryCreateWall(segments[i], resolvedWallMaterial, resolvedTopMaterial, out GameObject wallObject))
                {
                    createdCount++;
                    handleManager?.RegisterWall(wallObject);
                }
            }

            handleManager?.RefreshRegisteredWalls();

            if (refreshRoomsAfterImport)
            {
                roomManager?.MarkGraphDirty();
                RoomTopologyEvents.RequestRefreshAll();
            }

            LogWarnings();
            UDebug.Log($"[{nameof(DwgWallImporter)}] Imported {createdCount} wall segments from '{resolvedPath}'.", this);
            //return createdCount > 0;
    }

// 💡 교체할 ExtractWallSegments 함수
    private void ExtractWallSegments(IEnumerable<Entity> entities, List<SegmentDefinition> segments)
    {
        // 💡 주의: 이 변수 이름은 인스펙터에 있는 스케일 변수명(예: cadUnitToMeterScale)과 일치시켜 주세요.
        float currentScale = cadUnitToWorldScale;

        foreach (Entity entity in entities)
        {
            if (entity is Insert insert && insert.Block != null)
            {
                ExtractWallSegments(insert.Block.Entities, segments);
                continue;
            }

            string layerName = entity.Layer.Name;
            if (!ShouldImportLegacyLayer(layerName))
            {
                continue; 
            }

            // 1. 일반 선 (Line)
            if (entity is Line line)
            {
                AddSegment(segments, line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y, layerName, "Line", currentScale);
            }
            // 2. 신형 폴리선 (LwPolyline)
            else if (entity is LwPolyline lwPolyline)
            {
                for (int i = 0; i < lwPolyline.Vertices.Count - 1; i++)
                {
                    var p1 = lwPolyline.Vertices[i].Location;
                    var p2 = lwPolyline.Vertices[i + 1].Location;
                    AddSegment(segments, p1.X, p1.Y, p2.X, p2.Y, layerName, "LwPolyline", currentScale);
                }
                if (lwPolyline.IsClosed && lwPolyline.Vertices.Count > 2)
                {
                    var pLast = lwPolyline.Vertices[lwPolyline.Vertices.Count - 1].Location;
                    var pFirst = lwPolyline.Vertices[0].Location;
                    AddSegment(segments, pLast.X, pLast.Y, pFirst.X, pFirst.Y, layerName, "LwPolyline_Closed", currentScale);
                }
            }
            // 💡 3. 구형 2D 폴리선 지원 추가! (Polyline2D)
            else if (entity is Polyline2D polyline2d)
            {
                for (int i = 0; i < polyline2d.Vertices.Count - 1; i++)
                {
                    var p1 = polyline2d.Vertices[i].Location;
                    var p2 = polyline2d.Vertices[i + 1].Location;
                    AddSegment(segments, p1.X, p1.Y, p2.X, p2.Y, layerName, "Polyline2D", currentScale);
                }
                if (polyline2d.IsClosed && polyline2d.Vertices.Count > 2)
                {
                    var pLast = polyline2d.Vertices[polyline2d.Vertices.Count - 1].Location;
                    var pFirst = polyline2d.Vertices[0].Location;
                    AddSegment(segments, pLast.X, pLast.Y, pFirst.X, pFirst.Y, layerName, "Polyline2D_Closed", currentScale);
                }
            }
            // 그 외의 객체로 그려졌을 경우 알려줌
            else
            {
                UDebug.LogWarning($"[디버그] 'Wall' 레이어에 변환할 수 없는 객체가 있습니다: {entity.GetType().Name}", this);
            }
        }
    }

    // 💡 교체할 AddSegment 함수 (스케일 체크 로그 추가)
    private void AddSegment(List<SegmentDefinition> segments, double x1, double y1, double x2, double y2, string layer, string type, float scale)
    {
        Vector3 start = new Vector3((float)x1, 0, (float)y1) * scale;
        Vector3 end = new Vector3((float)x2, 0, (float)y2) * scale;

        float distance = Vector3.Distance(start, end);

        // 💡 노이즈 필터링 원인 분석용 디버그 로그
        if (distance < minimumWallLength) 
        {
            UDebug.LogWarning($"[디버그] 선분이 너무 짧아서 무시됨! 계산된 길이: {distance}m (타입: {type}). CAD 도면의 단위(m/mm)와 Inspector의 스케일 설정을 확인하세요.", this);
            return;
        }

        segments.Add(new SegmentDefinition
        {
            start = start,
            end = end,
            layerName = layer,
            sourceType = type
        });
    }

    private string ResolveFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }

    private CadDocument ReadDocument(string path)
    {
        string extension = Path.GetExtension(path);
        if (string.Equals(extension, ".dwg", StringComparison.OrdinalIgnoreCase))
        {
            return DwgReader.Read(path);
        }

        if (string.Equals(extension, ".dxf", StringComparison.OrdinalIgnoreCase))
        {
            return DxfReader.Read(path);
        }

        throw new NotSupportedException($"Unsupported CAD extension '{extension}'. Only .dwg and .dxf are supported.");
    }

    private void ExtractSegments(CadDocument document, List<SegmentDefinition> results)
    {
        if (document == null || results == null)
        {
            return;
        }

        foreach (Entity entity in document.Entities)
        {
            if (!ShouldImportEntity(entity))
            {
                continue;
            }

            switch (entity)
            {
                case Line line:
                    AddSegment(line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y, GetLayerName(entity), nameof(Line), results);
                    break;
                case LwPolyline lwPolyline:
                    ExtractLwPolylineSegments(lwPolyline, results);
                    break;
                case Polyline2D polyline2D:
                    ExtractPolyline2DSegments(polyline2D, results);
                    break;
            }
        }
    }

    private bool ShouldImportEntity(Entity entity)
    {
        if (entity == null)
        {
            return false;
        }

        if (!includeInvisibleEntities && entity.IsInvisible)
        {
            return false;
        }

        string layerName = GetLayerName(entity);
        if (!IsLayerIncluded(layerName))
        {
            return false;
        }

        return entity is Line || entity is LwPolyline || entity is Polyline2D;
    }

    private bool IsLayerIncluded(string layerName)
    {
        if (excludedLayers != null)
        {
            for (int i = 0; i < excludedLayers.Length; i++)
            {
                if (string.Equals(excludedLayers[i], layerName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (includedLayers == null || includedLayers.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < includedLayers.Length; i++)
        {
            if (string.Equals(includedLayers[i], layerName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldImportLegacyLayer(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return false;
        }

        if (excludedLayers != null)
        {
            for (int i = 0; i < excludedLayers.Length; i++)
            {
                if (string.Equals(excludedLayers[i], layerName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (includedLayers != null && includedLayers.Length > 0)
        {
            for (int i = 0; i < includedLayers.Length; i++)
            {
                if (string.Equals(includedLayers[i], layerName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(targetLayerKeyword))
        {
            return layerName.IndexOf(targetLayerKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        return true;
    }

    private void ExtractLwPolylineSegments(LwPolyline polyline, List<SegmentDefinition> results)
    {
        if (polyline == null || polyline.Vertices == null || polyline.Vertices.Count < 2)
        {
            return;
        }

        string layerName = GetLayerName(polyline);
        for (int i = 0; i < polyline.Vertices.Count; i++)
        {
            LwPolyline.Vertex current = polyline.Vertices[i];
            LwPolyline.Vertex next = i + 1 < polyline.Vertices.Count
                ? polyline.Vertices[i + 1]
                : (polyline.IsClosed ? polyline.Vertices[0] : null);

            if (next == null)
            {
                break;
            }

            if (!Mathf.Approximately((float)current.Bulge, 0f))
            {
                warnings.Add($"Skipped bulged LwPolyline segment on layer '{layerName}'.");
                continue;
            }

            AddSegment(current.Location.X, current.Location.Y, next.Location.X, next.Location.Y, layerName, nameof(LwPolyline), results);
        }
    }

    private void ExtractPolyline2DSegments(Polyline2D polyline, List<SegmentDefinition> results)
    {
        if (polyline == null || polyline.Vertices == null || polyline.Vertices.Count < 2)
        {
            return;
        }

        string layerName = GetLayerName(polyline);
        for (int i = 0; i < polyline.Vertices.Count; i++)
        {
            Vertex current = polyline.Vertices[i];
            Vertex next = i + 1 < polyline.Vertices.Count
                ? polyline.Vertices[i + 1]
                : (polyline.IsClosed ? polyline.Vertices[0] : null);

            if (next == null)
            {
                break;
            }

            if (!Mathf.Approximately((float)current.Bulge, 0f))
            {
                warnings.Add($"Skipped bulged Polyline2D segment on layer '{layerName}'.");
                continue;
            }

            AddSegment(current.Location.X, current.Location.Y, next.Location.X, next.Location.Y, layerName, nameof(Polyline2D), results);
        }
    }

    private void AddSegment(
        double startX,
        double startY,
        double endX,
        double endY,
        string layerName,
        string sourceType,
        List<SegmentDefinition> results)
    {
        Vector3 start = ConvertCadPoint(startX, startY);
        Vector3 end = ConvertCadPoint(endX, endY);

        if ((end - start).sqrMagnitude < minimumWallLength * minimumWallLength)
        {
            return;
        }

        if (deduplicateSegments)
        {
            string key = BuildSegmentKey(start, end);
            if (!uniqueSegmentKeys.Add(key))
            {
                return;
            }
        }

        results.Add(new SegmentDefinition
        {
            start = start,
            end = end,
            layerName = layerName,
            sourceType = sourceType,
        });
    }

    private Vector3 ConvertCadPoint(double x, double y)
    {
        float worldX = (float)x * cadUnitToWorldScale;
        float worldZ = (float)y * cadUnitToWorldScale * (invertCadY ? -1f : 1f);
        return new Vector3(worldX, drawingPlaneY, worldZ) + importOffset;
    }

    private void CenterSegmentsAtOrigin(List<SegmentDefinition> segmentDefinitions)
    {
        if (segmentDefinitions == null || segmentDefinitions.Count == 0)
        {
            return;
        }

        Vector3 min = segmentDefinitions[0].start;
        Vector3 max = segmentDefinitions[0].start;
        ExpandBounds(segmentDefinitions[0].end, ref min, ref max);

        for (int i = 1; i < segmentDefinitions.Count; i++)
        {
            ExpandBounds(segmentDefinitions[i].start, ref min, ref max);
            ExpandBounds(segmentDefinitions[i].end, ref min, ref max);
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
            SegmentDefinition definition = segmentDefinitions[i];
            definition.start -= recenterOffset;
            definition.end -= recenterOffset;
            segmentDefinitions[i] = definition;
        }
    }

    private static void ExpandBounds(Vector3 point, ref Vector3 min, ref Vector3 max)
    {
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
    }

    private string BuildSegmentKey(Vector3 start, Vector3 end)
    {
        if (ComparePoints(start, end) > 0)
        {
            Vector3 temp = start;
            start = end;
            end = temp;
        }

        return $"{Quantize(start.x)}|{Quantize(start.z)}|{Quantize(end.x)}|{Quantize(end.z)}";
    }

    private int ComparePoints(Vector3 left, Vector3 right)
    {
        int xCompare = left.x.CompareTo(right.x);
        if (xCompare != 0)
        {
            return xCompare;
        }

        return left.z.CompareTo(right.z);
    }

    private long Quantize(float value)
    {
        return Convert.ToInt64(Math.Round(value / deduplicateTolerance, MidpointRounding.AwayFromZero));
    }

    private bool TryCreateWall(
        SegmentDefinition segment,
        Material resolvedWallMaterial,
        Material resolvedTopMaterial,
        out GameObject wallObject)
    {
        wallObject = CreateWallObject(resolvedWallMaterial, resolvedTopMaterial);
        wallObject.name = $"{segment.sourceType}_{segment.layerName}";

        Wall wall = wallObject.GetComponent<Wall>();
        if (wall == null)
        {
            DestroySafely(wallObject);
            return false;
        }

        float centerY = drawingPlaneY + wallHeight * 0.5f + wallSurfaceOffset;
        if (!wall.TryApplyGeometryAndRefresh(
                segment.start,
                segment.end,
                wallThickness,
                wallHeight,
                centerY,
                minimumWallLength,
                wallLengthDisplay,
                false))
        {
            DestroySafely(wallObject);
            return false;
        }

        return true;
    }

    private GameObject CreateWallObject(Material resolvedWallMaterial, Material resolvedTopMaterial)
    {
        EnsureWallRoot();
        EnsureCachedResources();

        GameObject wallObject = new GameObject("DWG_Wall", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        wallObject.transform.SetParent(wallRoot, true);
        LayerUtility.ApplyLayer(wallObject, LayerUtility.WallLayerName, false);

        MeshFilter filter = wallObject.GetComponent<MeshFilter>();
        if (filter != null)
        {
            filter.sharedMesh = cachedCubeMesh;
        }

        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null && resolvedWallMaterial != null)
        {
            renderer.sharedMaterial = resolvedWallMaterial;
        }

        Wall wall = wallObject.AddComponent<Wall>();
        wall.SetTopMaterial(resolvedTopMaterial);
        wall.SetTopFaceOffset(0.01f);
        return wallObject;
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        wallRoot = LayerUtility.FindTransformByName("Walls", true);
        if (wallRoot != null)
        {
            return;
        }

        wallRoot = new GameObject("Walls").transform;
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

    private void ClearWalls()
    {
        EnsureWallRoot();
        if (wallRoot == null)
        {
            return;
        }

        List<GameObject> wallObjects = new List<GameObject>();
        Wall[] walls = wallRoot.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
            {
                wallObjects.Add(walls[i].gameObject);
            }
        }

        for (int i = 0; i < wallObjects.Count; i++)
        {
            handleManager?.UnregisterWall(wallObjects[i]);
            DestroySafely(wallObjects[i]);
        }
    }

    private void ClearRooms()
    {
        Room[] rooms = FindObjectsByType<Room>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < rooms.Length; i++)
        {
            if (rooms[i] != null)
            {
                DestroySafely(rooms[i].gameObject);
            }
        }
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

    private string GetLayerName(Entity entity)
    {
        return entity?.Layer != null ? entity.Layer.Name : string.Empty;
    }

    private static string ShowOpenCadFileDialog()
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel("DWG 파일 선택", string.Empty, "dwg,dxf");
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
$dialog.Title = 'DWG 파일 선택'
if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    Write-Output $dialog.FileName
}";
        return "-NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }
}
