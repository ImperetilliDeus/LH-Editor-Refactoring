#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class DwgWallImportPopupBuilder
{
    private const string LegacyFontPath = "Assets/Fonts/SUIT-ttf/SUIT-Regular.ttf";
    private const string TmpFontPath = "Assets/Fonts/SUIT-ttf/SUIT-Bold SDF.asset";

    public static void CreatePopupForImporter(DwgWallImporter importer)
    {
        if (importer == null)
        {
            return;
        }

        Font legacyFont = AssetDatabase.LoadAssetAtPath<Font>(LegacyFontPath);
        TMP_FontAsset tmpFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
        if (legacyFont == null)
        {
            legacyFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        Canvas canvas = LayerUtility.FindCanvasByNameOrFirst("_Screen");
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("_Screen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Import Popup Canvas");
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform existing = LayerUtility.FindChildByName(canvas.transform, "DWGImportSettingsPopup");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        GameObject root = CreateUiObject("DWGImportSettingsPopup", canvas.transform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        root.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.78f);

        GameObject panel = CreateUiObject("Panel", rootRect);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 660f);
        panelRect.anchoredPosition = Vector2.zero;
        panel.AddComponent<Image>().color = new Color32(29, 34, 46, 250);

        GameObject accent = CreateUiObject("Accent", panelRect);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.sizeDelta = new Vector2(0f, 6f);
        accentRect.anchoredPosition = Vector2.zero;
        accent.AddComponent<Image>().color = new Color32(90, 164, 255, 255);

        TMP_Text title = CreateTmpText("Title", panelRect, "DWG Import Settings", 30, FontStyles.Bold, tmpFont, Color.white);
        SetTopLeft(title.rectTransform, new Vector2(28f, -22f), new Vector2(420f, 38f));

        TMP_Text subtitle = CreateTmpText("Subtitle", panelRect, "Set the import scale, filter layers, and choose exactly what to bring in.", 16, FontStyles.Normal, tmpFont, new Color32(184, 194, 214, 255));
        SetTopLeft(subtitle.rectTransform, new Vector2(28f, -62f), new Vector2(720f, 26f));

        Text pathLabel = CreateText("PathLabel", panelRect, "Selected File", 14, FontStyle.Bold, TextAnchor.MiddleLeft, new Color32(137, 149, 173, 255), legacyFont);
        SetTopLeft(pathLabel.rectTransform, new Vector2(28f, -104f), new Vector2(220f, 20f));

        Text pathValue = CreateText("PathValue", panelRect, "No file selected", 15, FontStyle.Normal, TextAnchor.UpperLeft, new Color32(236, 240, 248, 255), legacyFont);
        pathValue.horizontalOverflow = HorizontalWrapMode.Wrap;
        SetTopLeft(pathValue.rectTransform, new Vector2(28f, -128f), new Vector2(764f, 44f));

        GameObject scaleCard = CreateCard("ScaleCard", panelRect, new Vector2(28f, -188f), new Vector2(230f, 102f));
        Text scaleLabel = CreateText("ScaleLabel", scaleCard.transform as RectTransform, "CAD Unit To World Scale", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, legacyFont);
        SetTopLeft(scaleLabel.rectTransform, new Vector2(16f, -14f), new Vector2(198f, 22f));
        InputField scaleInput = CreateInput("ScaleInput", scaleCard.transform as RectTransform, "100", legacyFont);
        SetTopLeft(scaleInput.transform as RectTransform, new Vector2(16f, -48f), new Vector2(198f, 40f));

        GameObject searchCard = CreateCard("SearchCard", panelRect, new Vector2(274f, -188f), new Vector2(230f, 102f));
        Text searchLabel = CreateText("LayerSearchLabel", searchCard.transform as RectTransform, "Layer Search", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, legacyFont);
        SetTopLeft(searchLabel.rectTransform, new Vector2(16f, -14f), new Vector2(198f, 22f));
        InputField searchInput = CreateInput("LayerSearchInput", searchCard.transform as RectTransform, "Search layers", legacyFont);
        SetTopLeft(searchInput.transform as RectTransform, new Vector2(16f, -48f), new Vector2(198f, 40f));

        GameObject infoCard = CreateCard("InfoCard", panelRect, new Vector2(520f, -188f), new Vector2(272f, 102f));
        TMP_Text infoTitle = CreateTmpText("InfoTitle", infoCard.transform as RectTransform, "Selection Rules", 16, FontStyles.Bold, tmpFont, Color.white);
        SetTopLeft(infoTitle.rectTransform, new Vector2(16f, -14f), new Vector2(180f, 22f));
        Text infoBody = CreateText("InfoBody", infoCard.transform as RectTransform, "Search only filters the visible list. Select All and Clear All apply to the currently visible layers.", 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color32(188, 197, 216, 255), legacyFont);
        infoBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        SetTopLeft(infoBody.rectTransform, new Vector2(16f, -42f), new Vector2(240f, 42f));

        TMP_Text sectionTitle = CreateTmpText("LayerSectionTitle", panelRect, "Detected Layers", 20, FontStyles.Bold, tmpFont, Color.white);
        SetTopLeft(sectionTitle.rectTransform, new Vector2(28f, -312f), new Vector2(220f, 26f));

        Text sectionHint = CreateText("LayerSectionHint", panelRect, "Choose one or more layers to import.", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color32(161, 172, 194, 255), legacyFont);
        SetTopLeft(sectionHint.rectTransform, new Vector2(28f, -342f), new Vector2(300f, 20f));

        Button selectAllButton = CreateButton("SelectAllLayersButton", panelRect, "Select All", new Vector2(430f, -330f), new Vector2(112f, 36f), new Color32(70, 117, 171, 255), legacyFont);
        Button clearAllButton = CreateButton("ClearAllLayersButton", panelRect, "Clear All", new Vector2(554f, -330f), new Vector2(112f, 36f), new Color32(91, 100, 122, 255), legacyFont);
        Button cancelButton = CreateButton("CancelButton", panelRect, "Cancel", new Vector2(680f, -330f), new Vector2(112f, 36f), new Color32(91, 100, 122, 255), legacyFont);

        GameObject layerScroll = CreateUiObject("LayerScrollView", panelRect);
        RectTransform layerScrollRect = layerScroll.GetComponent<RectTransform>();
        SetTopLeft(layerScrollRect, new Vector2(28f, -374f), new Vector2(764f, 210f));
        layerScroll.AddComponent<Image>().color = new Color32(23, 27, 36, 255);
        ScrollRect scrollRect = layerScroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        GameObject viewport = CreateUiObject("Viewport", layerScrollRect);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUiObject("LayerToggleContainer", viewportRect);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);
        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        Toggle toggleTemplate = CreateToggleTemplate(contentRect, legacyFont);
        toggleTemplate.gameObject.SetActive(false);

        Button importButton = CreateButton("ImportButton", panelRect, "Import", new Vector2(652f, -598f), new Vector2(140f, 42f), new Color32(90, 164, 255, 255), legacyFont);

        root.SetActive(false);

        AssignReferences(
            importer,
            canvas,
            root,
            pathValue,
            scaleInput,
            searchInput,
            contentRect,
            toggleTemplate,
            selectAllButton,
            clearAllButton,
            cancelButton,
            importButton);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
    }

    private static void AssignReferences(
        DwgWallImporter importer,
        Canvas canvas,
        GameObject root,
        Text pathValue,
        InputField scaleInput,
        InputField searchInput,
        RectTransform toggleContainer,
        Toggle toggleTemplate,
        Button selectAllButton,
        Button clearAllButton,
        Button cancelButton,
        Button importButton)
    {
        SerializedObject serializedObject = new SerializedObject(importer);
        serializedObject.FindProperty("importSettingsPopupCanvas").objectReferenceValue = canvas;
        serializedObject.FindProperty("importSettingsPopupPrefab").objectReferenceValue = null;
        serializedObject.FindProperty("importSettingsPopupRoot").objectReferenceValue = root;
        serializedObject.FindProperty("popupSelectedPathText").objectReferenceValue = pathValue;
        serializedObject.FindProperty("popupCadScaleInputField").objectReferenceValue = scaleInput;
        serializedObject.FindProperty("popupLayerSearchInputField").objectReferenceValue = searchInput;
        serializedObject.FindProperty("popupLayerToggleContainer").objectReferenceValue = toggleContainer;
        serializedObject.FindProperty("popupLayerTogglePrefab").objectReferenceValue = toggleTemplate;
        serializedObject.FindProperty("popupSelectAllLayersButton").objectReferenceValue = selectAllButton;
        serializedObject.FindProperty("popupClearAllLayersButton").objectReferenceValue = clearAllButton;
        serializedObject.FindProperty("popupCancelButton").objectReferenceValue = cancelButton;
        serializedObject.FindProperty("popupConfirmButton").objectReferenceValue = importButton;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(importer);
    }

    private static GameObject CreateCard(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject card = CreateUiObject(name, parent);
        RectTransform rect = card.GetComponent<RectTransform>();
        SetTopLeft(rect, anchoredPosition, size);
        card.AddComponent<Image>().color = new Color32(40, 46, 58, 255);
        return card;
    }

    private static Toggle CreateToggleTemplate(RectTransform parent, Font font)
    {
        GameObject toggleObject = CreateUiObject("LayerToggleTemplate", parent);
        RectTransform toggleRect = toggleObject.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(0.5f, 1f);
        toggleRect.sizeDelta = new Vector2(0f, 32f);
        LayoutElement layoutElement = toggleObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = 32f;
        layoutElement.preferredHeight = 32f;
        HorizontalLayoutGroup rowLayout = toggleObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = false;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        rowLayout.spacing = 8f;
        rowLayout.padding = new RectOffset(8, 8, 4, 4);
        Image rowBackground = toggleObject.AddComponent<Image>();
        rowBackground.color = new Color32(38, 44, 56, 255);
        Toggle toggle = toggleObject.AddComponent<Toggle>();
        toggle.targetGraphic = rowBackground;

        GameObject background = CreateUiObject("Background", toggleRect);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.sizeDelta = new Vector2(18f, 18f);
        LayoutElement backgroundLayout = background.AddComponent<LayoutElement>();
        backgroundLayout.minWidth = 18f;
        backgroundLayout.preferredWidth = 18f;
        backgroundLayout.minHeight = 18f;
        backgroundLayout.preferredHeight = 18f;
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color32(232, 237, 246, 255);

        GameObject checkmark = CreateUiObject("Checkmark", backgroundRect);
        RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
        Stretch(checkmarkRect);
        checkmarkRect.offsetMin = new Vector2(4f, 4f);
        checkmarkRect.offsetMax = new Vector2(-4f, -4f);
        Image checkmarkImage = checkmark.AddComponent<Image>();
        checkmarkImage.color = new Color32(90, 164, 255, 255);
        toggle.graphic = checkmarkImage;

        Text label = CreateText("Label", toggleRect, "Layer", 15, FontStyle.Normal, TextAnchor.MiddleLeft, new Color32(239, 242, 248, 255), font);
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.minHeight = 20f;
        return toggle;
    }

    private static Button CreateButton(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, Color color, Font font)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        SetTopLeft(rect, anchoredPosition, size);
        buttonObject.AddComponent<Image>().color = color;
        Button button = buttonObject.AddComponent<Button>();

        Text label = CreateText("Label", rect, text, 16, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, font);
        Stretch(label.rectTransform);
        return button;
    }

    private static InputField CreateInput(string name, RectTransform parent, string placeholder, Font font)
    {
        GameObject inputObject = CreateUiObject(name, parent);
        inputObject.AddComponent<Image>().color = new Color32(246, 247, 250, 255);
        InputField inputField = inputObject.AddComponent<InputField>();
        inputField.lineType = InputField.LineType.SingleLine;

        RectTransform rect = inputObject.GetComponent<RectTransform>();
        Text text = CreateText("Text", rect, string.Empty, 18, FontStyle.Normal, TextAnchor.MiddleLeft, new Color32(18, 22, 30, 255), font);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(12f, 6f);
        text.rectTransform.offsetMax = new Vector2(-12f, -6f);

        Text hint = CreateText("Placeholder", rect, placeholder, 18, FontStyle.Italic, TextAnchor.MiddleLeft, new Color32(138, 144, 157, 255), font);
        Stretch(hint.rectTransform);
        hint.rectTransform.offsetMin = new Vector2(12f, 6f);
        hint.rectTransform.offsetMax = new Vector2(-12f, -6f);

        inputField.textComponent = text;
        inputField.placeholder = hint;
        return inputField;
    }

    private static Text CreateText(string name, RectTransform parent, string value, int fontSize, FontStyle style, TextAnchor anchor, Color color, Font font)
    {
        GameObject textObject = CreateUiObject(name, parent);
        Text text = textObject.AddComponent<Text>();
        text.font = font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        return text;
    }

    private static TMP_Text CreateTmpText(string name, RectTransform parent, string value, float fontSize, FontStyles style, TMP_FontAsset font, Color color)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        return text;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
#endif
