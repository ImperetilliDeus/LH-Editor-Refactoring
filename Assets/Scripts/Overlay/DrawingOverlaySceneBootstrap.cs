using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Paroxe.PdfRenderer;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class DrawingOverlaySceneBootstrap
{
    private const string PanelRootName = "DrawingOverlayCalibrationPanel";
    private const string ManagerRootName = "DrawingOverlayManager";
    private const string ImportControllerRootName = "DrawingOverlayImportController";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureOverlaySystem()
    {
        EventSystemBootstrap.EnsureEventSystemExists();

        DrawingOverlayManager manager = UnityEngine.Object.FindFirstObjectByType<DrawingOverlayManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            manager = new GameObject(ManagerRootName).AddComponent<DrawingOverlayManager>();
        }

        OverlayCalibrationPanelController existingPanel =
            UnityEngine.Object.FindFirstObjectByType<OverlayCalibrationPanelController>(FindObjectsInactive.Include);
        if (existingPanel != null)
        {
            existingPanel.Close();
        }

        ModeManager modeManager = null;
        LayerUtility.ResolveObject(ref modeManager);

        manager.Initialize(
            modeManager,
            FindGridObject(),
            existingPanel);

        DrawingOverlayImportController importController =
            UnityEngine.Object.FindFirstObjectByType<DrawingOverlayImportController>(FindObjectsInactive.Include);
        if (importController == null)
        {
            importController = new GameObject(ImportControllerRootName).AddComponent<DrawingOverlayImportController>();
        }

        importController.Initialize(FindImportButton(), manager);
    }

    internal static OverlayCalibrationPanelController EnsurePanel(DrawingOverlayManager manager)
    {
        if (manager == null)
        {
            return null;
        }

        Canvas canvas = manager.ParentCanvas != null
            ? manager.ParentCanvas
            : LayerUtility.FindCanvasByNameOrFirst(LayerUtility.DefaultCanvasName);

        OverlayCalibrationPanelController panel =
            UnityEngine.Object.FindFirstObjectByType<OverlayCalibrationPanelController>(FindObjectsInactive.Include);
        if (panel == null)
        {
            if (canvas == null)
            {
                return null;
            }

            if (manager.CalibrationPanelPrefab != null)
            {
                panel = UnityEngine.Object.Instantiate(manager.CalibrationPanelPrefab, canvas.transform);
                panel.name = manager.CalibrationPanelPrefab.name;
            }
            else
            {
                panel = CreatePanel(canvas, manager);
            }
        }

        ConfigurePanelCanvas(panel, canvas);
        panel.Close();
        manager.SetCalibrationPanel(panel);
        return panel;
    }

    private static OverlayCalibrationPanelController CreatePanel(Canvas canvas, DrawingOverlayManager manager)
    {
        TMP_FontAsset tmpFont = manager != null ? manager.UiTmpFont : null;
        Font legacyFont = manager != null && manager.UiLegacyFont != null
            ? manager.UiLegacyFont
            : Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject root = CreateUIObject(PanelRootName, canvas.transform as RectTransform);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(760f, 700f);
        rootRect.anchoredPosition = Vector2.zero;

        Image rootImage = root.AddComponent<Image>();
        rootImage.color = new Color32(35, 36, 42, 244);
        CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
        root.AddComponent<UIDragManager>();

        CreateButton("_DragButton", rootRect, string.Empty, new Color(0f, 0f, 0f, 0f), tmpFont, out RectTransform dragRect);
        dragRect.anchorMin = new Vector2(0f, 1f);
        dragRect.anchorMax = new Vector2(1f, 1f);
        dragRect.pivot = new Vector2(0.5f, 1f);
        dragRect.sizeDelta = new Vector2(-32f, 56f);
        dragRect.anchoredPosition = new Vector2(0f, -12f);

        TMP_Text titleText = CreateTmpText("Title", rootRect, "Overlay Calibration", 28, FontStyles.Bold, tmpFont);
        SetTopLeft(titleText.rectTransform, new Vector2(28f, -20f), new Vector2(340f, 44f));

        TMP_Text statusText = CreateTmpText("StatusText", rootRect, "Pick two anchors on the drawing, then enter the real-world distance.", 18, FontStyles.Normal, tmpFont);
        statusText.color = new Color32(204, 213, 255, 255);
        SetTopLeft(statusText.rectTransform, new Vector2(28f, -62f), new Vector2(460f, 34f));

        TMP_Text scaleValue = CreateMetric(rootRect, "Scale", "-", new Vector2(415f, -28f), new Vector2(415f, -54f), tmpFont);
        TMP_Text rotValue = CreateMetric(rootRect, "Total Rot", "-", new Vector2(535f, -28f), new Vector2(535f, -54f), tmpFont);
        TMP_Text offXValue = CreateMetric(rootRect, "Offset X", "-", new Vector2(665f, -28f), new Vector2(665f, -54f), tmpFont);
        TMP_Text offYValue = CreateMetric(rootRect, "Offset Y", "-", new Vector2(665f, -92f), new Vector2(665f, -118f), tmpFont);

        GameObject previewFrame = CreateUIObject("PreviewFrame", rootRect);
        RectTransform previewFrameRect = previewFrame.GetComponent<RectTransform>();
        previewFrameRect.anchorMin = new Vector2(0f, 1f);
        previewFrameRect.anchorMax = new Vector2(1f, 1f);
        previewFrameRect.pivot = new Vector2(0.5f, 1f);
        previewFrameRect.sizeDelta = new Vector2(-34f, 430f);
        previewFrameRect.anchoredPosition = new Vector2(0f, -112f);
        previewFrame.AddComponent<Image>().color = new Color32(19, 21, 29, 255);

        GameObject previewImageObject = CreateUIObject("PreviewImage", previewFrameRect);
        RectTransform previewRect = previewImageObject.GetComponent<RectTransform>();
        previewRect.anchorMin = previewRect.anchorMax = previewRect.pivot = new Vector2(0.5f, 0.5f);
        previewRect.sizeDelta = new Vector2(660f, 390f);
        RawImage rawImage = previewImageObject.AddComponent<RawImage>();
        rawImage.color = new Color(1f, 1f, 1f, 0.9f);
        AspectRatioFitter aspectRatioFitter = previewImageObject.AddComponent<AspectRatioFitter>();
        aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        OverlayPreviewController previewController = previewImageObject.AddComponent<OverlayPreviewController>();
        previewController.Initialize(rawImage, previewRect, aspectRatioFitter, tmpFont);

        Button pickAnchorAButton = CreateButton("PickAnchorAButton", rootRect, "Pick Anchor 1", new Color32(67, 72, 98, 255), tmpFont, out RectTransform pickAnchorARect);
        SetBottomStretchLeft(pickAnchorARect, new Vector2(28f, 122f), new Vector2(-40f, 52f));

        Button pickAnchorBButton = CreateButton("PickAnchorBButton", rootRect, "Pick Anchor 2", new Color32(67, 72, 98, 255), tmpFont, out RectTransform pickAnchorBRect);
        SetBottomStretchRight(pickAnchorBRect, new Vector2(-28f, 122f), new Vector2(-40f, 52f));

        TMP_Text distanceLabel = CreateTmpText("DistanceLabel", rootRect, "Anchor Distance (m)", 19, FontStyles.Normal, tmpFont);
        SetBottomLeft(distanceLabel.rectTransform, new Vector2(28f, 84f), new Vector2(180f, 28f));
        InputField distanceInput = CreateInputField("DistanceInput", rootRect, "3", legacyFont, out RectTransform distanceRect);
        SetBottomLeft(distanceRect, new Vector2(170f, 62f), new Vector2(190f, 48f));

        Button applyButton = CreateButton("ApplyScaleButton", rootRect, "Apply Scale", new Color32(67, 72, 98, 255), tmpFont, out RectTransform applyRect);
        SetBottomStretchRight(applyRect, new Vector2(-28f, 62f), new Vector2(-40f, 48f));

        TMP_Text rotationLabel = CreateTmpText("FineRotationLabel", rootRect, "Fine Rotation (deg)", 19, FontStyles.Normal, tmpFont);
        SetBottomLeft(rotationLabel.rectTransform, new Vector2(28f, 28f), new Vector2(180f, 28f));
        Slider rotationSlider = CreateSlider("FineRotationSlider", rootRect, out RectTransform sliderRect);
        SetBottomLeft(sliderRect, new Vector2(170f, 10f), new Vector2(210f, 40f));

        InputField rotationInput = CreateInputField("FineRotationInput", rootRect, "0.0", legacyFont, out RectTransform rotationInputRect);
        SetBottomLeft(rotationInputRect, new Vector2(400f, 8f), new Vector2(72f, 44f));

        Button originButton = CreateButton("PickOriginButton", rootRect, "Pick Origin", new Color32(67, 72, 98, 255), tmpFont, out RectTransform originRect);
        SetBottomStretchRight(originRect, new Vector2(-28f, 8f), new Vector2(-40f, 44f));

        Button rotationGuideButton = CreateButton("PickRotationGuideButton", rootRect, "Pick Rotation Guide", new Color32(67, 72, 98, 255), tmpFont, out RectTransform rotationGuideRect);
        SetBottomStretchRight(rotationGuideRect, new Vector2(-28f, 58f), new Vector2(-40f, 44f));

        Button resetButton = CreateButton("ResetButton", rootRect, "Reset", new Color32(67, 72, 98, 255), tmpFont, out RectTransform resetRect);
        SetBottomStretchRight(resetRect, new Vector2(-28f, -42f), new Vector2(-40f, 44f));

        Button completeButton = CreateButton("CompleteButton", rootRect, "Done", new Color32(95, 132, 255, 255), tmpFont, out RectTransform completeRect);
        SetBottomLeft(completeRect, new Vector2(28f, -42f), new Vector2(180f, 44f));

        OverlayCalibrationPanelController controller = root.AddComponent<OverlayCalibrationPanelController>();
        controller.Initialize(
            canvasGroup,
            previewController,
            pickAnchorAButton,
            pickAnchorBButton,
            applyButton,
            resetButton,
            completeButton,
            originButton,
            rotationGuideButton,
            rotationSlider,
            distanceInput,
            rotationInput,
            scaleValue,
            rotValue,
            offXValue,
            offYValue,
            statusText);
        controller.Close();
        return controller;
    }

    private static TMP_Text CreateMetric(RectTransform parent, string label, string value, Vector2 labelPos, Vector2 valuePos, TMP_FontAsset font)
    {
        TMP_Text labelText = CreateTmpText(label.Replace(" ", string.Empty) + "_Label", parent, label, 15, FontStyles.Bold, font);
        labelText.color = new Color32(213, 218, 244, 255);
        SetTopLeft(labelText.rectTransform, labelPos, new Vector2(150f, 22f));

        TMP_Text valueText = CreateTmpText(label.Replace(" ", string.Empty) + "_Value", parent, value, 28, FontStyles.Bold, font);
        SetTopLeft(valueText.rectTransform, valuePos, new Vector2(120f, 34f));
        return valueText;
    }

    private static Button FindImportButton()
    {
        Transform buttonTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultImportButtonName, true);
        return buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
    }

    private static GameObject FindGridObject()
    {
        Transform gridTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultGridName, true);
        return gridTransform != null ? gridTransform.gameObject : null;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        return target;
    }

    private static TMP_Text CreateTmpText(string name, RectTransform parent, string text, float fontSize, FontStyles style, TMP_FontAsset font)
    {
        GameObject obj = CreateUIObject(name, parent);
        TMP_Text label = obj.AddComponent<TextMeshProUGUI>();
        label.font = font != null ? font : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        return label;
    }

    private static Button CreateButton(string name, RectTransform parent, string text, Color backgroundColor, TMP_FontAsset font, out RectTransform rectTransform)
    {
        GameObject obj = CreateUIObject(name, parent);
        rectTransform = obj.GetComponent<RectTransform>();
        Image image = obj.AddComponent<Image>();
        image.color = backgroundColor;
        Button button = obj.AddComponent<Button>();

        if (!string.IsNullOrEmpty(text))
        {
            TMP_Text label = CreateTmpText("Label", rectTransform, text, 22, FontStyles.Bold, font);
            label.alignment = TextAlignmentOptions.Center;
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        return button;
    }

    private static InputField CreateInputField(string name, RectTransform parent, string initialValue, Font font, out RectTransform rectTransform)
    {
        GameObject obj = CreateUIObject(name, parent);
        rectTransform = obj.GetComponent<RectTransform>();
        obj.AddComponent<Image>().color = new Color32(24, 26, 36, 255);
        InputField inputField = obj.AddComponent<InputField>();

        Text text = CreateLegacyText("Text", rectTransform, font, initialValue, Color.white);
        Text placeholder = CreateLegacyText("Placeholder", rectTransform, font, initialValue, new Color(1f, 1f, 1f, 0.4f));
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.text = initialValue;
        return inputField;
    }

    private static Text CreateLegacyText(string name, RectTransform parent, Font font, string textValue, Color color)
    {
        GameObject obj = CreateUIObject(name, parent);
        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = textValue;
        text.color = color;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 6f);
        rect.offsetMax = new Vector2(-10f, -6f);
        return text;
    }

    private static Slider CreateSlider(string name, RectTransform parent, out RectTransform rectTransform)
    {
        GameObject root = CreateUIObject(name, parent);
        rectTransform = root.GetComponent<RectTransform>();

        Image bg = CreateUIObject("Background", rectTransform).AddComponent<Image>();
        bg.color = new Color32(138, 141, 152, 255);
        RectTransform bgRect = bg.rectTransform;
        bgRect.anchorMin = new Vector2(0f, 0.5f);
        bgRect.anchorMax = new Vector2(1f, 0.5f);
        bgRect.sizeDelta = new Vector2(0f, 8f);

        RectTransform fillArea = CreateUIObject("FillArea", rectTransform).GetComponent<RectTransform>();
        fillArea.anchorMin = Vector2.zero;
        fillArea.anchorMax = Vector2.one;
        fillArea.offsetMin = new Vector2(10f, 10f);
        fillArea.offsetMax = new Vector2(-10f, -10f);

        Image fill = CreateUIObject("Fill", fillArea).AddComponent<Image>();
        fill.color = new Color32(144, 185, 255, 255);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;

        RectTransform handleArea = CreateUIObject("HandleArea", rectTransform).GetComponent<RectTransform>();
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        Image handle = CreateUIObject("Handle", handleArea).AddComponent<Image>();
        handle.color = new Color32(155, 193, 255, 255);
        handle.rectTransform.sizeDelta = new Vector2(12f, 30f);

        Slider slider = root.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = -5f;
        slider.maxValue = 5f;
        return slider;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void SetBottomLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void SetBottomStretchLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void SetBottomStretchRight(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void ConfigurePanelCanvas(OverlayCalibrationPanelController panel, Canvas parentCanvas)
    {
        if (panel == null)
        {
            return;
        }

        Canvas panelCanvas = panel.GetComponent<Canvas>();
        if (panelCanvas != null)
        {
            panelCanvas.renderMode = parentCanvas != null ? parentCanvas.renderMode : RenderMode.ScreenSpaceOverlay;
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = parentCanvas != null
                ? Mathf.Max(parentCanvas.sortingOrder + 25, panelCanvas.sortingOrder)
                : Mathf.Max(30, panelCanvas.sortingOrder);
            panelCanvas.worldCamera = panelCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : parentCanvas != null ? parentCanvas.worldCamera : null;
        }

        if (panel.GetComponent<GraphicRaycaster>() == null)
        {
            panel.gameObject.AddComponent<GraphicRaycaster>();
        }

        int uiLayer = parentCanvas != null ? parentCanvas.gameObject.layer : LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
        {
            SetLayerRecursively(panel.gameObject, uiLayer);
        }
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.layer = layer;
        Transform transform = target.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            SetLayerRecursively(transform.GetChild(i).gameObject, layer);
        }
    }

    private static class EventSystemBootstrap
    {
        public static void EnsureEventSystemExists()
        {
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            SceneManager.MoveGameObjectToScene(eventSystemObject, SceneManager.GetActiveScene());
        }
    }
}

internal static class PdfThumbnailLoader
{
    public static bool TryLoadFirstPageThumbnail(string path, int maxPixelSize, out Texture2D texture, out string error)
    {
        texture = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = "The PDF file could not be found.";
            return false;
        }

        if (TryRenderWithParoxe(path, maxPixelSize, out texture, out error))
        {
            return true;
        }

        string paroxeError = error;

        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            Guid guid = typeof(IShellItemImageFactory).GUID;
            NativePdfThumbnail.SHCreateItemFromParsingName(path, IntPtr.Zero, ref guid, out IShellItemImageFactory imageFactory);

            NativePdfThumbnail.SIZE size = new NativePdfThumbnail.SIZE
            {
                cx = maxPixelSize,
                cy = maxPixelSize,
            };

            imageFactory.GetImage(
                size,
                NativePdfThumbnail.SIIGBF.ResizeToFit |
                NativePdfThumbnail.SIIGBF.BiggerSizeOk |
                NativePdfThumbnail.SIIGBF.ThumbnailOnly,
                out hBitmap);

            if (hBitmap == IntPtr.Zero)
            {
                error = "Windows Shell did not return a thumbnail image.";
                return false;
            }

            texture = NativePdfThumbnail.CreateTextureFromHBitmap(hBitmap, out error);
            if (texture == null && !string.IsNullOrEmpty(paroxeError))
            {
                error = $"Paroxe render failed: {paroxeError}\n{error}";
            }

            return texture != null;
        }
        catch (Exception exception)
        {
            error = $"Paroxe render failed: {paroxeError}\n{exception.Message}";
            return false;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
            {
                NativePdfThumbnail.DeleteObject(hBitmap);
            }
        }
    }

    private static bool TryRenderWithParoxe(string path, int maxPixelSize, out Texture2D texture, out string error)
    {
        texture = null;
        error = string.Empty;

        try
        {
            using PDFDocument document = new PDFDocument(path);
            if (!document.IsValid)
            {
                error = "Paroxe could not open the PDF document.";
                return false;
            }

            if (document.GetPageCount() <= 0)
            {
                error = "Paroxe opened the document but found no pages.";
                return false;
            }

            using PDFPage page = document.GetPage(0);
            Vector2Int renderSize = ComputeRenderSize(page.GetPageSize(), maxPixelSize);

            using PDFRenderer renderer = new PDFRenderer();
            texture = renderer.RenderPageToTexture(page, renderSize.x, renderSize.y);
            if (texture == null)
            {
                error = "Paroxe returned a null texture.";
                return false;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static Vector2Int ComputeRenderSize(Vector2 pageSize, int maxPixelSize)
    {
        int safeMax = Mathf.Max(256, maxPixelSize);
        float width = Mathf.Max(1f, pageSize.x);
        float height = Mathf.Max(1f, pageSize.y);
        float scale = Mathf.Min(safeMax / width, safeMax / height);

        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        return new Vector2Int(
            Mathf.Max(1, Mathf.RoundToInt(width * scale)),
            Mathf.Max(1, Mathf.RoundToInt(height * scale)));
    }
}

[ComImport]
[Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
    void GetImage(NativePdfThumbnail.SIZE size, NativePdfThumbnail.SIIGBF flags, out IntPtr phbm);
}

internal static class NativePdfThumbnail
{
    [Flags]
    internal enum SIIGBF
    {
        ResizeToFit = 0x00,
        BiggerSizeOk = 0x01,
        ThumbnailOnly = 0x08,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SIZE
    {
        public int cx;
        public int cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public short bmPlanes;
        public short bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
    }

    private const uint DIB_RGB_COLORS = 0;
    private const uint BI_RGB = 0;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    internal static extern void SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr pbc,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory ppv);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetObject(IntPtr hObject, int cbBuffer, out BITMAP lpvObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(
        IntPtr hdc,
        IntPtr hbmp,
        uint uStartScan,
        uint cScanLines,
        [Out] byte[] lpvBits,
        ref BITMAPINFO lpbmi,
        uint uUsage);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern bool DeleteObject(IntPtr hObject);

    internal static Texture2D CreateTextureFromHBitmap(IntPtr hBitmap, out string error)
    {
        error = string.Empty;
        if (GetObject(hBitmap, Marshal.SizeOf<BITMAP>(), out BITMAP bitmap) == 0)
        {
            error = "Failed to read bitmap metadata.";
            return null;
        }

        int width = bitmap.bmWidth;
        int height = bitmap.bmHeight;
        if (width <= 0 || height <= 0)
        {
            error = "The bitmap returned an invalid size.";
            return null;
        }

        BITMAPINFO info = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
                biSizeImage = (uint)(width * height * 4),
            }
        };

        byte[] bgra = new byte[width * height * 4];
        IntPtr hdc = GetDC(IntPtr.Zero);
        try
        {
            if (GetDIBits(hdc, hBitmap, 0, (uint)height, bgra, ref info, DIB_RGB_COLORS) == 0)
            {
                error = "Failed to copy bitmap pixel data.";
                return null;
            }
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, hdc);
        }

        byte[] rgba = new byte[bgra.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            rgba[i] = bgra[i + 2];
            rgba[i + 1] = bgra[i + 1];
            rgba[i + 2] = bgra[i];
            rgba[i + 3] = bgra[i + 3];
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        texture.LoadRawTextureData(rgba);
        texture.Apply(false, false);
        return texture;
    }
}
