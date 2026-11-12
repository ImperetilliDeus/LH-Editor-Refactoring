using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FurnitureDefectPanelController : MonoBehaviour
{
    private const string DefaultFurnitureMenuRootName = "_FurnishMenu";
    private const string DefaultActionButtonsName = "_FurnishActionButtons";
    private const string DefaultLocationDropdownName = "_EditFurnishLoc";
    private const string DefaultDefectScrollViewName = "_DefectScrollView";
    private const string DefaultToggleTemplateName = "LayerToggleTemplate";
    private const string FurnitureDefectTogglePrefabPath = "Assets/Prefabs/UIPrefab/FurnitureDefectToggleTemplate.prefab";
    private const string FurnitureActionButtonsPrefabPath = "Assets/Prefabs/UIPrefab/FurnitureActionButtons.prefab";
    private const string LayerScrollViewPrefabPath = "Assets/Prefabs/UIPrefab/LayerScrollView.prefab";

    private static readonly LhDefectLocationCodeOption[] FixedLocationOptions =
    {
        new LhDefectLocationCodeOption { code = "2", displayName = "벽" },
        new LhDefectLocationCodeOption { code = "1", displayName = "천장" },
        new LhDefectLocationCodeOption { code = "3", displayName = "바닥" },
    };

    [Header("References")]
    [SerializeField] private FurnitureMenuController furnitureMenuController;
    [SerializeField] private FurniturePlacementManager placementManager;
    [SerializeField] private LhDefectCodeService defectCodeService;
    [SerializeField] private TMP_Dropdown locationDropdown;
    [SerializeField] private Dropdown legacyLocationDropdown;
    [SerializeField] private ScrollRect defectScrollView;
    [SerializeField] private Transform defectToggleContainer;
    [SerializeField] private Toggle defectToggleTemplate;
    [SerializeField] private RectTransform actionButtonsRoot;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private bool enableDefectDebugLogs = true;

    [Header("Lookup")]
    [SerializeField] private string furnitureMenuRootName = DefaultFurnitureMenuRootName;
    [SerializeField] private string locationDropdownName = DefaultLocationDropdownName;
    [SerializeField] private string defectScrollViewName = DefaultDefectScrollViewName;
    [SerializeField] private string toggleTemplateName = DefaultToggleTemplateName;

    private readonly List<LhDefectLocationCodeOption> locationOptions = new List<LhDefectLocationCodeOption>();
    private readonly List<Transform> destroyBuffer = new List<Transform>();
    private readonly Dictionary<string, string> selectedLocationByItemKey = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<FurnitureDefectCatalogEntry>> selectedDefectsByItemKey = new Dictionary<string, List<FurnitureDefectCatalogEntry>>(StringComparer.Ordinal);
    private readonly HashSet<string> visibleDefectKeys = new HashSet<string>(StringComparer.Ordinal);

    private FurnitureCatalogItem selectedItem;
    private FurnitureInstance selectedFurniture;
    private bool suppressLocationCallback;

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
        RefreshPanel();
    }

    private void Start()
    {
        ResolveReferences();
        BindEvents();
        RefreshPanel();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref furnitureMenuController);
        LayerUtility.ResolveObject(ref placementManager);
        LayerUtility.ResolveObject(ref defectCodeService);

        if (locationDropdown == null && legacyLocationDropdown == null)
        {
            Transform locationTransform = LayerUtility.FindTransformByName(GetLocationDropdownName(), true);
            if (locationTransform != null)
            {
                ResolveDropdownComponents(locationTransform, ref locationDropdown, ref legacyLocationDropdown);
            }
        }

        if (defectScrollView == null)
        {
            Transform scrollTransform = LayerUtility.FindTransformByName(GetDefectScrollViewName(), true);
            if (scrollTransform != null)
            {
                defectScrollView = scrollTransform.GetComponent<ScrollRect>();
            }
        }

        if (defectToggleContainer == null && defectScrollView != null)
        {
            defectToggleContainer = defectScrollView.content;
        }

#if UNITY_EDITOR
        if (defectToggleTemplate == null)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FurnitureDefectTogglePrefabPath);
            defectToggleTemplate = prefab != null ? prefab.GetComponent<Toggle>() : null;
        }
#endif

        if (defectToggleTemplate == null)
        {
            Transform searchRoot = defectScrollView != null ? defectScrollView.transform : transform;
            Transform templateTransform = LayerUtility.FindChildByName(searchRoot, GetToggleTemplateName());
            if (templateTransform != null)
            {
                defectToggleTemplate = templateTransform.GetComponent<Toggle>();
            }
        }

#if UNITY_EDITOR
        if (defectToggleTemplate == null)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(LayerScrollViewPrefabPath);
            Transform templateTransform = prefab != null ? LayerUtility.FindChildByName(prefab.transform, GetToggleTemplateName()) : null;
            if (templateTransform != null)
            {
                defectToggleTemplate = templateTransform.GetComponent<Toggle>();
            }
        }
#endif

        if (defectToggleTemplate == null)
        {
            defectToggleTemplate = CreateRuntimeDefectToggleTemplate();
        }

        EnsureActionButtons();
    }

    private void BindEvents()
    {
        if (furnitureMenuController != null)
        {
            furnitureMenuController.SelectedItemChanged -= HandleFurnitureItemChanged;
            furnitureMenuController.SelectedItemChanged += HandleFurnitureItemChanged;
            selectedItem = furnitureMenuController.SelectedItem;
        }

        if (placementManager != null)
        {
            placementManager.SelectedFurnitureChanged -= HandleSelectedFurnitureChanged;
            placementManager.SelectedFurnitureChanged += HandleSelectedFurnitureChanged;
            selectedFurniture = placementManager.SelectedFurniture;
        }

        if (defectCodeService != null && defectCodeService.DefectCodesLoaded != null)
        {
            defectCodeService.DefectCodesLoaded.RemoveListener(HandleDefectCodesLoaded);
            defectCodeService.DefectCodesLoaded.AddListener(HandleDefectCodesLoaded);
        }

        if (locationDropdown != null)
        {
            locationDropdown.onValueChanged.RemoveListener(HandleLocationChanged);
            locationDropdown.onValueChanged.AddListener(HandleLocationChanged);
        }

        if (legacyLocationDropdown != null)
        {
            legacyLocationDropdown.onValueChanged.RemoveListener(HandleLocationChanged);
            legacyLocationDropdown.onValueChanged.AddListener(HandleLocationChanged);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSelection);
            confirmButton.onClick.AddListener(ConfirmSelection);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(CancelSelection);
            cancelButton.onClick.AddListener(CancelSelection);
        }
    }

    private void UnbindEvents()
    {
        if (furnitureMenuController != null)
        {
            furnitureMenuController.SelectedItemChanged -= HandleFurnitureItemChanged;
        }

        if (placementManager != null)
        {
            placementManager.SelectedFurnitureChanged -= HandleSelectedFurnitureChanged;
        }

        if (defectCodeService != null && defectCodeService.DefectCodesLoaded != null)
        {
            defectCodeService.DefectCodesLoaded.RemoveListener(HandleDefectCodesLoaded);
        }

        if (locationDropdown != null)
        {
            locationDropdown.onValueChanged.RemoveListener(HandleLocationChanged);
        }

        if (legacyLocationDropdown != null)
        {
            legacyLocationDropdown.onValueChanged.RemoveListener(HandleLocationChanged);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSelection);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(CancelSelection);
        }
    }

    private void HandleFurnitureItemChanged(FurnitureCatalogItem item)
    {
        selectedItem = item;
        selectedFurniture = null;
        EnsureItemDefaults(item);
        RefreshPanel();
    }

    private void HandleSelectedFurnitureChanged(FurnitureInstance furniture)
    {
        selectedFurniture = furniture;
        if (furniture != null)
        {
            selectedItem = null;
        }

        RefreshPanel();
    }

    private void HandleDefectCodesLoaded(LhDefectMaterialCodeList codes)
    {
        RefreshPanel();
    }

    private void HandleLocationChanged(int optionIndex)
    {
        if (suppressLocationCallback)
        {
            return;
        }

        SetActiveLocationCode(GetSelectedLocationCode());
        RefreshDefectToggles();
    }

    private void ConfirmSelection()
    {
        if (selectedItem == null || placementManager == null)
        {
            return;
        }

        string itemKey = GetItemKey(selectedItem);
        string locationCode = GetActiveLocationCode();
        List<FurnitureDefectCatalogEntry> defects = GetCachedDefects(itemKey);
        placementManager.BeginPlacement(selectedItem, locationCode, defects);
    }

    private void CancelSelection()
    {
        selectedItem = null;
        selectedFurniture = null;
        furnitureMenuController?.ClearSelection();
        RefreshPanel();
    }

    private void RefreshPanel()
    {
        ResolveReferences();
        PopulateLocationDropdown();
        RefreshDefectToggles();
        RefreshActionButtonState();
    }

    private static void ResolveDropdownComponents(
        Transform root,
        ref TMP_Dropdown tmpDropdown,
        ref Dropdown legacyDropdown)
    {
        if (root == null)
        {
            return;
        }

        tmpDropdown = tmpDropdown != null ? tmpDropdown : root.GetComponent<TMP_Dropdown>();
        legacyDropdown = legacyDropdown != null ? legacyDropdown : root.GetComponent<Dropdown>();
        if (tmpDropdown != null || legacyDropdown != null)
        {
            return;
        }

        tmpDropdown = root.GetComponentInChildren<TMP_Dropdown>(true);
        legacyDropdown = root.GetComponentInChildren<Dropdown>(true);
        if (tmpDropdown != null || legacyDropdown != null)
        {
            return;
        }

        tmpDropdown = root.GetComponentInParent<TMP_Dropdown>(true);
        legacyDropdown = root.GetComponentInParent<Dropdown>(true);
    }

    private void PopulateLocationDropdown()
    {
        locationOptions.Clear();
        locationOptions.AddRange(FixedLocationOptions);

        string preferredLocCode = GetActiveLocationCode();
        int selectedIndex = FindLocationIndex(preferredLocCode);
        if (selectedIndex < 0 && locationOptions.Count > 0)
        {
            selectedIndex = 0;
            SetActiveLocationCode(locationOptions[selectedIndex].code);
        }

        suppressLocationCallback = true;
        try
        {
            if (locationDropdown != null)
            {
                List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(locationOptions.Count);
                for (int i = 0; i < locationOptions.Count; i++)
                {
                    options.Add(new TMP_Dropdown.OptionData(locationOptions[i].displayName ?? string.Empty));
                }

                locationDropdown.ClearOptions();
                locationDropdown.AddOptions(options);
                if (selectedIndex >= 0)
                {
                    locationDropdown.SetValueWithoutNotify(selectedIndex);
                }
                locationDropdown.RefreshShownValue();
            }

            if (legacyLocationDropdown != null)
            {
                List<Dropdown.OptionData> options = new List<Dropdown.OptionData>(locationOptions.Count);
                for (int i = 0; i < locationOptions.Count; i++)
                {
                    options.Add(new Dropdown.OptionData(locationOptions[i].displayName ?? string.Empty));
                }

                legacyLocationDropdown.ClearOptions();
                legacyLocationDropdown.AddOptions(options);
                if (selectedIndex >= 0)
                {
                    legacyLocationDropdown.SetValueWithoutNotify(selectedIndex);
                }
                legacyLocationDropdown.RefreshShownValue();
            }
        }
        finally
        {
            suppressLocationCallback = false;
        }

        SetLocationInteractable(HasActiveTarget());
    }

    private void RefreshDefectToggles()
    {
        ClearDefectToggles();
        visibleDefectKeys.Clear();

        if (!HasActiveTarget())
        {
            LogDefectDebug("Skip defect list: no selected furniture item or placed furniture.");
            return;
        }

        if (defectToggleContainer == null)
        {
            LogDefectDebug("Skip defect list: defect toggle container is missing.");
            return;
        }

        if (defectToggleTemplate == null)
        {
            LogDefectDebug("Skip defect list: defect toggle template is missing.");
            return;
        }

        string locationCode = GetSelectedLocationCode();
        LhDefectMaterialCodeList codes = defectCodeService != null ? defectCodeService.LoadedCodes : null;
        if (string.IsNullOrWhiteSpace(locationCode))
        {
            LogDefectDebug("Skip defect list: selected location code is empty.");
            return;
        }

        if (defectCodeService == null)
        {
            LogDefectDebug("Skip defect list: LhDefectCodeService was not found.");
            return;
        }

        if (codes == null || codes.mtrlList == null)
        {
            LogDefectDebug("Skip defect list: defect code list is null.");
            return;
        }

        int candidateCount = 0;
        for (int i = 0; i < codes.mtrlList.Count; i++)
        {
            LhDefectMaterialCode item = codes.mtrlList[i];
            if (item == null ||
                !string.Equals(item.mblLocCd, locationCode, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(item.mblMtrlCd))
            {
                continue;
            }

            candidateCount++;
            string defectKey = BuildDefectKey(item.mblMtrlCd);
            if (!visibleDefectKeys.Add(defectKey))
            {
                continue;
            }

            Toggle toggle = Instantiate(defectToggleTemplate, defectToggleContainer);
            toggle.gameObject.SetActive(true);
            toggle.name = $"DefectToggle_{item.mblMtrlCd}";
            NormalizeDefectToggleLayout(toggle);
            SetToggleText(toggle, !string.IsNullOrWhiteSpace(item.mblMtrlCdNm) ? item.mblMtrlCdNm : item.mblMtrlCd);

            bool isSelected = HasSelectedDefect(item);
            toggle.SetIsOnWithoutNotify(isSelected);
            toggle.onValueChanged.AddListener(value => HandleDefectToggleChanged(item, value));
        }

        if (defectToggleContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        LogDefectDebug(
            $"Defect list refreshed. locationCode={locationCode}, loaded={codes.mtrlList.Count}, " +
            $"matched={candidateCount}, visible={visibleDefectKeys.Count}.");
    }

    private void HandleDefectToggleChanged(LhDefectMaterialCode defect, bool value)
    {
        if (!HasActiveTarget() || defect == null)
        {
            return;
        }

        string locationCode = GetSelectedLocationCode();
        FurnitureDefectCatalogEntry entry = defect.ToFurnitureDefectEntry();
        entry.locCd = string.IsNullOrWhiteSpace(entry.locCd) ? locationCode : entry.locCd;

        if (selectedItem != null)
        {
            string itemKey = GetItemKey(selectedItem);
            List<FurnitureDefectCatalogEntry> defects = GetCachedDefects(itemKey);
            SetDefect(defects, entry, value);
            selectedDefectsByItemKey[itemKey] = CloneDefects(defects);
            selectedLocationByItemKey[itemKey] = locationCode;
            return;
        }

        if (selectedFurniture != null)
        {
            string roomCode = ResolveRoomCode(selectedFurniture.CurrentRoom);
            if (!string.IsNullOrWhiteSpace(roomCode))
            {
                entry.mntnCd = roomCode;
            }

            List<FurnitureDefectCatalogEntry> defects = CloneDefects(selectedFurniture.ExportDefects);
            SetDefect(defects, entry, value);
            selectedFurniture.SetFurnishLocationCode(locationCode);
            selectedFurniture.SetExportDefects(defects);
        }
    }

    private bool HasSelectedDefect(LhDefectMaterialCode defect)
    {
        if (defect == null)
        {
            return false;
        }

        IReadOnlyList<FurnitureDefectCatalogEntry> defects = selectedItem != null
            ? GetCachedDefects(GetItemKey(selectedItem))
            : selectedFurniture != null ? selectedFurniture.ExportDefects : null;
        if (defects == null)
        {
            return false;
        }

        for (int i = 0; i < defects.Count; i++)
        {
            if (MatchesDefect(defects[i], defect))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureItemDefaults(FurnitureCatalogItem item)
    {
        if (item == null)
        {
            return;
        }

        string itemKey = GetItemKey(item);
        if (!selectedDefectsByItemKey.ContainsKey(itemKey))
        {
            selectedDefectsByItemKey[itemKey] = CloneDefects(item.defects);
        }

        if (!selectedLocationByItemKey.ContainsKey(itemKey))
        {
            selectedLocationByItemKey[itemKey] = ResolveLocationCode(selectedDefectsByItemKey[itemKey]);
        }
    }

    private string GetActiveLocationCode()
    {
        if (selectedItem != null)
        {
            string itemKey = GetItemKey(selectedItem);
            return selectedLocationByItemKey.TryGetValue(itemKey, out string code) ? code : string.Empty;
        }

        return selectedFurniture != null ? selectedFurniture.FurnishLocationCode : string.Empty;
    }

    private void SetActiveLocationCode(string locationCode)
    {
        if (selectedItem != null)
        {
            selectedLocationByItemKey[GetItemKey(selectedItem)] = locationCode ?? string.Empty;
        }

        if (selectedFurniture != null)
        {
            selectedFurniture.SetFurnishLocationCode(locationCode);
        }
    }

    private List<FurnitureDefectCatalogEntry> GetCachedDefects(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
        {
            return new List<FurnitureDefectCatalogEntry>();
        }

        if (!selectedDefectsByItemKey.TryGetValue(itemKey, out List<FurnitureDefectCatalogEntry> defects) || defects == null)
        {
            defects = new List<FurnitureDefectCatalogEntry>();
            selectedDefectsByItemKey[itemKey] = defects;
        }

        return defects;
    }

    private static void SetDefect(List<FurnitureDefectCatalogEntry> defects, FurnitureDefectCatalogEntry entry, bool selected)
    {
        if (defects == null || entry == null)
        {
            return;
        }

        for (int i = defects.Count - 1; i >= 0; i--)
        {
            FurnitureDefectCatalogEntry current = defects[i];
            if (current != null &&
                string.Equals(current.locCd, entry.locCd, StringComparison.Ordinal) &&
                string.Equals(current.mtrlCd, entry.mtrlCd, StringComparison.Ordinal))
            {
                defects.RemoveAt(i);
            }
        }

        if (selected)
        {
            defects.Add(new FurnitureDefectCatalogEntry
            {
                mntnCd = entry.mntnCd ?? string.Empty,
                locCd = entry.locCd ?? string.Empty,
                mtrlCd = entry.mtrlCd ?? string.Empty,
            });
        }
    }

    private static List<FurnitureDefectCatalogEntry> CloneDefects(IEnumerable<FurnitureDefectCatalogEntry> source)
    {
        List<FurnitureDefectCatalogEntry> results = new List<FurnitureDefectCatalogEntry>();
        if (source == null)
        {
            return results;
        }

        foreach (FurnitureDefectCatalogEntry entry in source)
        {
            if (entry == null)
            {
                continue;
            }

            results.Add(new FurnitureDefectCatalogEntry
            {
                mntnCd = entry.mntnCd ?? string.Empty,
                locCd = entry.locCd ?? string.Empty,
                mtrlCd = entry.mtrlCd ?? string.Empty,
            });
        }

        return results;
    }

    private static bool MatchesDefect(FurnitureDefectCatalogEntry entry, LhDefectMaterialCode defect)
    {
        return entry != null &&
               defect != null &&
               string.Equals(entry.locCd, defect.mblLocCd, StringComparison.Ordinal) &&
               string.Equals(entry.mtrlCd, defect.mblMtrlCd, StringComparison.Ordinal);
    }

    private static string ResolveLocationCode(IEnumerable<FurnitureDefectCatalogEntry> defects)
    {
        if (defects == null)
        {
            return string.Empty;
        }

        foreach (FurnitureDefectCatalogEntry defect in defects)
        {
            if (defect != null && !string.IsNullOrWhiteSpace(defect.locCd))
            {
                return defect.locCd;
            }
        }

        return string.Empty;
    }

    private static string ResolveRoomCode(Room room)
    {
        if (room == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(room.RoomCode))
        {
            return room.RoomCode;
        }

        return RoomTypeCatalog.TryGetCode(room.RoomTypeKey, out int resolvedCode)
            ? resolvedCode.ToString()
            : string.Empty;
    }

    private static string GetItemKey(FurnitureCatalogItem item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.code))
        {
            return item.code;
        }

        if (!string.IsNullOrWhiteSpace(item.exportCode))
        {
            return item.exportCode;
        }

        if (!string.IsNullOrWhiteSpace(item.nativeCode))
        {
            return item.nativeCode;
        }

        return item.prefab != null ? item.prefab.name : item.displayName ?? string.Empty;
    }

    private string BuildDefectKey(string materialCode)
    {
        return $"{GetSelectedLocationCode()}|{materialCode ?? string.Empty}";
    }

    private bool HasActiveTarget()
    {
        return selectedItem != null || selectedFurniture != null;
    }

    private int FindLocationIndex(string locationCode)
    {
        if (string.IsNullOrWhiteSpace(locationCode))
        {
            return -1;
        }

        for (int i = 0; i < locationOptions.Count; i++)
        {
            if (string.Equals(locationOptions[i].code, locationCode, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private string GetSelectedLocationCode()
    {
        int index = locationDropdown != null
            ? locationDropdown.value
            : legacyLocationDropdown != null ? legacyLocationDropdown.value : -1;
        return index >= 0 && index < locationOptions.Count ? locationOptions[index].code : string.Empty;
    }

    private void SetLocationInteractable(bool value)
    {
        if (locationDropdown != null)
        {
            locationDropdown.interactable = value;
        }

        if (legacyLocationDropdown != null)
        {
            legacyLocationDropdown.interactable = value;
        }
    }

    private void RefreshActionButtonState()
    {
        bool canConfirm = selectedItem != null && placementManager != null;
        if (confirmButton != null)
        {
            confirmButton.interactable = canConfirm;
        }

        if (cancelButton != null)
        {
            cancelButton.interactable = HasActiveTarget();
        }
    }

    private void EnsureActionButtons()
    {
        if (confirmButton != null && cancelButton != null)
        {
            return;
        }

        if (actionButtonsRoot != null)
        {
            ResolveActionButtonsFromRoot(actionButtonsRoot);
            return;
        }

        Transform menuRoot = LayerUtility.FindTransformByName(GetFurnitureMenuRootName(), true);
        RectTransform menuRect = menuRoot as RectTransform;
        if (menuRect == null)
        {
            return;
        }

        RectTransform actionRoot = null;
#if UNITY_EDITOR
        if (actionRoot == null)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FurnitureActionButtonsPrefabPath);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, menuRect);
                instance.name = DefaultActionButtonsName;
                actionRoot = instance.transform as RectTransform;
                ApplyActionRootLayout(actionRoot);
                actionButtonsRoot = actionRoot;
            }
        }
#endif

        if (actionRoot == null)
        {
            GameObject actionObject = new GameObject(DefaultActionButtonsName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            actionRoot = actionObject.GetComponent<RectTransform>();
            actionRoot.SetParent(menuRect, false);
            ApplyActionRootLayout(actionRoot);

            HorizontalLayoutGroup layout = actionObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            actionButtonsRoot = actionRoot;
        }

        ResolveActionButtonsFromRoot(actionRoot);
    }

    private void ResolveActionButtonsFromRoot(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        if (confirmButton == null)
        {
            confirmButton = FindOrCreateActionButton(root, "_Button_FurnishConfirm", "확인", new Color32(75, 110, 255, 255));
        }

        if (cancelButton == null)
        {
            cancelButton = FindOrCreateActionButton(root, "_Button_FurnishCancel", "취소", new Color32(76, 82, 96, 255));
        }
    }

    private static void ApplyActionRootLayout(RectTransform actionRoot)
    {
        if (actionRoot == null)
        {
            return;
        }

        actionRoot.anchorMin = new Vector2(0f, 0f);
        actionRoot.anchorMax = new Vector2(1f, 0f);
        actionRoot.pivot = new Vector2(0.5f, 0f);
        actionRoot.anchoredPosition = new Vector2(0f, 14f);
        actionRoot.sizeDelta = new Vector2(-32f, 42f);
    }

    private static Button FindOrCreateActionButton(RectTransform parent, string name, string label, Color color)
    {
        Transform existing = LayerUtility.FindChildByName(parent, name);
        Button button = existing != null ? existing.GetComponent<Button>() : null;
        if (button != null)
        {
            return button;
        }

        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelObject.GetComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 18f;
        text.color = Color.white;

        return buttonObject.GetComponent<Button>();
    }

    private static void NormalizeDefectToggleLayout(Toggle toggle)
    {
        RectTransform toggleRect = toggle != null ? toggle.transform as RectTransform : null;
        if (toggleRect == null)
        {
            return;
        }

        toggleRect.anchorMin = new Vector2(0f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(0.5f, 1f);
        toggleRect.sizeDelta = new Vector2(0f, 28f);

        LayoutElement layoutElement = toggle.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = toggle.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.minHeight = 28f;
        layoutElement.preferredHeight = 28f;
        layoutElement.flexibleWidth = 1f;

        Text legacyText = toggle.GetComponentInChildren<Text>(true);
        RectTransform labelRect = legacyText != null ? legacyText.transform as RectTransform : null;
        if (labelRect == null)
        {
            TMP_Text tmpText = toggle.GetComponentInChildren<TMP_Text>(true);
            labelRect = tmpText != null ? tmpText.transform as RectTransform : null;
        }

        if (labelRect != null)
        {
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = new Vector2(16f, 0f);
            labelRect.sizeDelta = new Vector2(-48f, -4f);
        }
    }

    private Toggle CreateRuntimeDefectToggleTemplate()
    {
        GameObject templateObject = new GameObject(
            "RuntimeFurnitureDefectToggleTemplate",
            typeof(RectTransform),
            typeof(Image),
            typeof(Toggle),
            typeof(LayoutElement));
        templateObject.transform.SetParent(transform, false);
        templateObject.SetActive(false);

        RectTransform templateRect = templateObject.GetComponent<RectTransform>();
        templateRect.anchorMin = new Vector2(0f, 1f);
        templateRect.anchorMax = new Vector2(1f, 1f);
        templateRect.pivot = new Vector2(0.5f, 1f);
        templateRect.sizeDelta = new Vector2(0f, 40f);

        Image targetImage = templateObject.GetComponent<Image>();
        targetImage.color = new Color(0.09f, 0.11f, 0.14f, 0f);

        LayoutElement layoutElement = templateObject.GetComponent<LayoutElement>();
        layoutElement.minHeight = 40f;
        layoutElement.preferredHeight = 40f;
        layoutElement.flexibleWidth = 1f;

        GameObject backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(templateObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.anchoredPosition = new Vector2(12f, 0f);
        backgroundRect.sizeDelta = new Vector2(18f, 18f);
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.87f, 0.9f, 0.95f, 1f);

        GameObject checkmarkObject = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkmarkObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform checkmarkRect = checkmarkObject.GetComponent<RectTransform>();
        checkmarkRect.anchorMin = Vector2.zero;
        checkmarkRect.anchorMax = Vector2.one;
        checkmarkRect.offsetMin = new Vector2(3f, 3f);
        checkmarkRect.offsetMax = new Vector2(-3f, -3f);
        Image checkmarkImage = checkmarkObject.GetComponent<Image>();
        checkmarkImage.color = new Color(0.2f, 0.45f, 1f, 1f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(templateObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(40f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = string.Empty;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = new Color(0.9f, 0.93f, 0.98f, 1f);
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 14f;

        Toggle toggle = templateObject.GetComponent<Toggle>();
        toggle.targetGraphic = targetImage;
        toggle.graphic = checkmarkImage;
        toggle.SetIsOnWithoutNotify(false);
        return toggle;
    }

    private void LogDefectDebug(string message)
    {
        if (enableDefectDebugLogs)
        {
            Debug.Log($"[FurnitureDefectPanel] {message}", this);
        }
    }

    private void SetToggleText(Toggle toggle, string text)
    {
        Text legacyText = toggle != null ? toggle.GetComponentInChildren<Text>(true) : null;
        if (legacyText != null)
        {
            legacyText.text = text ?? string.Empty;
            legacyText.color = new Color(0.9f, 0.93f, 0.98f, 1f);
            legacyText.resizeTextForBestFit = true;
            legacyText.resizeTextMinSize = 10;
            legacyText.resizeTextMaxSize = 14;
            return;
        }

        TMP_Text tmpText = toggle != null ? toggle.GetComponentInChildren<TMP_Text>(true) : null;
        if (tmpText != null)
        {
            tmpText.text = text ?? string.Empty;
            tmpText.color = new Color(0.9f, 0.93f, 0.98f, 1f);
            tmpText.enableAutoSizing = true;
            tmpText.fontSizeMin = 10f;
            tmpText.fontSizeMax = 14f;
        }
    }

    private void ClearDefectToggles()
    {
        if (defectToggleContainer == null)
        {
            return;
        }

        destroyBuffer.Clear();
        for (int i = 0; i < defectToggleContainer.childCount; i++)
        {
            Transform child = defectToggleContainer.GetChild(i);
            if (child == null || child == defectToggleTemplate?.transform)
            {
                continue;
            }

            destroyBuffer.Add(child);
        }

        for (int i = 0; i < destroyBuffer.Count; i++)
        {
            Destroy(destroyBuffer[i].gameObject);
        }
    }

    private string GetFurnitureMenuRootName() => string.IsNullOrWhiteSpace(furnitureMenuRootName) ? DefaultFurnitureMenuRootName : furnitureMenuRootName;
    private string GetLocationDropdownName() => string.IsNullOrWhiteSpace(locationDropdownName) ? DefaultLocationDropdownName : locationDropdownName;
    private string GetDefectScrollViewName() => string.IsNullOrWhiteSpace(defectScrollViewName) ? DefaultDefectScrollViewName : defectScrollViewName;
    private string GetToggleTemplateName() => string.IsNullOrWhiteSpace(toggleTemplateName) ? DefaultToggleTemplateName : toggleTemplateName;
}
