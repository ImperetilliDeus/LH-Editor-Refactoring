using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FurnitureMenuController : MonoBehaviour
{
    private const string DefaultFurnitureMenuRootName = "_FurnishMenu";
    private const string DefaultFurnitureButtonName = "_Button_EditFurnish";
    private const string DefaultScrollViewName = "Scroll View";

    [Header("References")]
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private FurniturePlacementManager placementManager;
    [SerializeField] private FurnitureCatalog catalog;
    [SerializeField] private UiReferenceSettings uiReferenceSettings;
    [SerializeField] private GameObject furnitureMenuRoot;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Button buttonTemplate;

    [Header("Build")]
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private Vector2 buttonSize = new Vector2(120f, 120f);

    private bool built;
    private bool eventsBound;
    private static readonly Dictionary<Texture2D, Sprite> ThumbnailSpriteCache = new Dictionary<Texture2D, Sprite>();

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void Initialize()
    {
        ResolveReferences();
        BindEvents();
        UpdateMenuVisibility();

        if (rebuildOnStart)
        {
            RebuildMenu();
        }
    }

    public void SetReferencesForTests(
        ModeManager modeManager,
        EditorViewModeManager viewModeManager,
        GameObject furnitureMenuRoot)
    {
        UnbindEvents();

        this.modeManager = modeManager;
        this.viewModeManager = viewModeManager;
        this.furnitureMenuRoot = furnitureMenuRoot;

        BindEvents();
        UpdateMenuVisibility();
    }

    public void RebuildMenu()
    {
        ResolveReferences();
        if (contentRoot == null || catalog == null)
        {
            return;
        }

        ClearExistingButtons();
        for (int i = 0; i < catalog.Items.Count; i++)
        {
            FurnitureCatalogItem item = catalog.Items[i];
            if (item == null || item.prefab == null)
            {
                continue;
            }

            CreateButton(item);
        }

        built = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    private void BindEvents()
    {
        if (eventsBound)
        {
            return;
        }

        bool boundAnyEvent = false;
        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
            modeManager.ModeChanged += HandleModeChanged;
            boundAnyEvent = true;
        }

        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
            viewModeManager.ViewModeChanged += HandleViewModeChanged;
            boundAnyEvent = true;
        }

        eventsBound = boundAnyEvent;
    }

    private void UnbindEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
        }

        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        }

        eventsBound = false;
    }

    private void HandleModeChanged(EditorMode mode)
    {
        UpdateMenuVisibility();
        if (mode == EditorMode.FurniturePlace && rebuildOnStart && !built)
        {
            RebuildMenu();
        }
    }

    private void HandleViewModeChanged(EditorViewMode viewMode)
    {
        UpdateMenuVisibility();
    }

    public void RefreshVisibility()
    {
        UpdateMenuVisibility();
    }

    private void UpdateMenuVisibility()
    {
        if (furnitureMenuRoot == null)
        {
            return;
        }

        if (furnitureMenuRoot.TryGetComponent(out Button _))
        {
            return;
        }

        bool visible = modeManager != null &&
                       modeManager.IsMode(EditorMode.FurniturePlace) &&
                       IsTopViewActive();
        if (furnitureMenuRoot.activeSelf != visible)
        {
            furnitureMenuRoot.SetActive(visible);
        }
    }

    private bool IsTopViewActive()
    {
        return viewModeManager == null || viewModeManager.CurrentViewMode == EditorViewMode.Top;
    }

    private void CreateButton(FurnitureCatalogItem item)
    {
        Button button = InstantiateButton();
        if (button == null)
        {
            return;
        }

        button.name = string.IsNullOrWhiteSpace(item.displayName)
            ? $"Furniture_{item.prefab.name}"
            : $"Furniture_{item.displayName}";

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (placementManager != null)
            {
                placementManager.BeginPlacement(item);
            }
        });

        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect != null)
        {
            buttonRect.sizeDelta = buttonSize;
        }

        ApplyButtonVisuals(button, item);
    }

    private Button InstantiateButton()
    {
        if (contentRoot == null)
        {
            return null;
        }

        if (buttonTemplate != null)
        {
            Button clone = Instantiate(buttonTemplate, contentRoot);
            clone.gameObject.SetActive(true);
            return clone;
        }

        GameObject buttonObject = new GameObject("FurnitureButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(contentRoot, false);
        Image background = buttonObject.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.92f);

        GameObject rawObject = new GameObject("Thumbnail", typeof(RectTransform), typeof(RawImage));
        rawObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rawRect = rawObject.GetComponent<RectTransform>();
        rawRect.anchorMin = Vector2.zero;
        rawRect.anchorMax = Vector2.one;
        rawRect.offsetMin = new Vector2(8f, 28f);
        rawRect.offsetMax = new Vector2(-8f, -8f);

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 8f);
        labelRect.sizeDelta = new Vector2(0f, 22f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 18f;
        label.color = new Color(0.12f, 0.12f, 0.12f, 1f);

        return buttonObject.GetComponent<Button>();
    }

    private static void ApplyButtonVisuals(Button button, FurnitureCatalogItem item)
    {
        RawImage rawImage = button.GetComponentInChildren<RawImage>(true);
        if (rawImage != null)
        {
            rawImage.texture = item.thumbnail;
            rawImage.color = item.thumbnail != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
        }
        else
        {
            Image image = ResolveThumbnailImage(button);
            if (image != null)
            {
                image.sprite = GetOrCreateThumbnailSprite(item.thumbnail);
                image.color = item.thumbnail != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
                image.preserveAspect = true;
            }
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = string.IsNullOrWhiteSpace(item.displayName) ? item.prefab.name : item.displayName;
        }
    }

    private static Image ResolveThumbnailImage(Button button)
    {
        if (button == null)
        {
            return null;
        }

        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image current = images[i];
            if (current == null)
            {
                continue;
            }

            if (current.gameObject == button.gameObject && button.targetGraphic == current)
            {
                return current;
            }

            if (current.gameObject != button.gameObject)
            {
                return current;
            }
        }

        return button.targetGraphic as Image;
    }

    private static Sprite GetOrCreateThumbnailSprite(Texture2D texture)
    {
        if (texture == null)
        {
            return null;
        }

        if (ThumbnailSpriteCache.TryGetValue(texture, out Sprite cachedSprite) && cachedSprite != null)
        {
            return cachedSprite;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
        sprite.name = $"{texture.name}_FurnitureThumb";
        ThumbnailSpriteCache[texture] = sprite;
        return sprite;
    }

    private void ClearExistingButtons()
    {
        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (buttonTemplate != null && child == buttonTemplate.transform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            LayerUtility.ResolveObject(ref modeManager);
        }

        if (viewModeManager == null)
        {
            LayerUtility.ResolveObject(ref viewModeManager);
        }

        if (placementManager == null)
        {
            LayerUtility.ResolveObject(ref placementManager);
        }

        if (furnitureMenuRoot == null)
        {
            Transform target = LayerUtility.FindTransformByName(GetFurnitureMenuRootName(), true);
            if (target != null)
            {
                furnitureMenuRoot = target.gameObject;
            }
        }

        if (scrollRect == null)
        {
            ScrollRect[] scrollRects = FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < scrollRects.Length; i++)
            {
                if (scrollRects[i] != null && scrollRects[i].name == GetScrollViewName())
                {
                    scrollRect = scrollRects[i];
                    break;
                }
            }
        }

        if (contentRoot == null)
        {
            contentRoot = scrollRect != null ? scrollRect.content : null;
        }

        if (buttonTemplate == null && contentRoot != null)
        {
            Button[] buttons = contentRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button currentButton = buttons[i];
                if (currentButton == null)
                {
                    continue;
                }

                if (currentButton.name == GetFurnitureButtonName())
                {
                    continue;
                }

                buttonTemplate = currentButton;
                break;
            }
        }
    }

    private string GetFurnitureMenuRootName()
    {
        return uiReferenceSettings != null && !string.IsNullOrWhiteSpace(uiReferenceSettings.furnitureMenuRootName)
            ? uiReferenceSettings.furnitureMenuRootName
            : DefaultFurnitureMenuRootName;
    }

    private string GetFurnitureButtonName()
    {
        return uiReferenceSettings != null && !string.IsNullOrWhiteSpace(uiReferenceSettings.furnitureButtonName)
            ? uiReferenceSettings.furnitureButtonName
            : DefaultFurnitureButtonName;
    }

    private string GetScrollViewName()
    {
        return uiReferenceSettings != null && !string.IsNullOrWhiteSpace(uiReferenceSettings.furnitureScrollViewName)
            ? uiReferenceSettings.furnitureScrollViewName
            : DefaultScrollViewName;
    }
}
