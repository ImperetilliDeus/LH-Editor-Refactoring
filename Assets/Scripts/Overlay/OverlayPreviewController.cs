using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OverlayPreviewController : MonoBehaviour, IPointerClickHandler, IDragHandler, IScrollHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private RawImage previewImage;
    [SerializeField] private RectTransform viewportRect;
    [SerializeField] private AspectRatioFitter aspectRatioFitter;
    [SerializeField] private TMP_FontAsset tmpFontAsset;
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 8f;
    [SerializeField] private float scrollZoomStep = 0.12f;
    [SerializeField] private bool enableMiddleMousePan = true;

    private DrawingOverlayDocument boundDocument;
    private OverlayCalibrationStep currentStep;
    private float zoomScale = 1f;
    private bool isPanning;
    private Vector2 lastPanPointerPosition;

    private RectTransform overlayRoot;
    private RectTransform gridRoot;
    private RectTransform guideRoot;
    private TMP_Text hintText;
    private PreviewMarker anchorAMarker;
    private PreviewMarker anchorBMarker;
    private PreviewMarker rotationAMarker;
    private PreviewMarker rotationBMarker;
    private PreviewMarker originMarker;
    private Image anchorLine;
    private TMP_Text anchorLineLabel;
    private Image rotationLine;
    private Image crosshairHorizontal;
    private Image crosshairVertical;
    private readonly Image[] gridLines = new Image[24];

    public event Action<Vector2> PixelPointPicked;

    public Texture CurrentTexture => previewImage != null ? previewImage.texture : null;

    private void Awake()
    {
        if (previewImage == null)
        {
            previewImage = GetComponentInChildren<RawImage>(true);
        }

        if (viewportRect == null)
        {
            viewportRect = previewImage != null ? previewImage.rectTransform : transform as RectTransform;
        }

        if (aspectRatioFitter == null && previewImage != null)
        {
            aspectRatioFitter = previewImage.GetComponent<AspectRatioFitter>();
        }

        EnsureViewportMask();
        EnsureOverlayVisuals();
    }

    public void Bind(DrawingOverlayDocument document, Texture texture)
    {
        boundDocument = document;
        if (previewImage != null)
        {
            previewImage.texture = texture;
        }

        if (aspectRatioFitter != null && document != null && document.source != null && document.source.pixelHeight > 0)
        {
            aspectRatioFitter.aspectRatio = (float)document.source.pixelWidth / document.source.pixelHeight;
        }

        ResetViewTransform();
        RefreshVisuals();
    }

    public void Initialize(RawImage resolvedPreviewImage, RectTransform resolvedViewportRect, AspectRatioFitter resolvedAspectRatioFitter, TMP_FontAsset resolvedTmpFontAsset)
    {
        previewImage = resolvedPreviewImage;
        viewportRect = resolvedViewportRect;
        aspectRatioFitter = resolvedAspectRatioFitter;
        tmpFontAsset = resolvedTmpFontAsset;
        EnsureViewportMask();
        EnsureOverlayVisuals();
        ResetViewTransform();
        RefreshVisuals();
    }

    public void SetCalibrationStep(OverlayCalibrationStep step)
    {
        currentStep = step;
        RefreshHint();
    }

    public void RefreshVisuals()
    {
        EnsureOverlayVisuals();
        RefreshGrid();
        RefreshMarkers();
        RefreshLines();
        RefreshCrosshair();
        RefreshHint();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        TryEmitPixelPoint(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isPanning)
        {
            HandlePanDrag(eventData);
            return;
        }

        TryEmitPixelPoint(eventData);
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (viewportRect == null)
        {
            return;
        }

        float delta = eventData.scrollDelta.y;
        if (Mathf.Approximately(delta, 0f))
        {
            return;
        }

        Camera uiCamera = GetUiCamera();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, eventData.position, uiCamera, out Vector2 localPoint))
        {
            return;
        }

        ApplyZoom(delta > 0f ? 1f + scrollZoomStep : 1f - scrollZoomStep, localPoint);
        eventData.Use();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!enableMiddleMousePan || eventData.button != PointerEventData.InputButton.Middle)
        {
            return;
        }

        isPanning = true;
        lastPanPointerPosition = eventData.position;
        eventData.Use();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Middle)
        {
            isPanning = false;
            eventData.Use();
        }
    }

    public bool TryGetPixelCoordinate(PointerEventData eventData, out Vector2 pixelCoordinate)
    {
        pixelCoordinate = Vector2.zero;
        if (boundDocument == null || boundDocument.source == null || viewportRect == null || previewImage == null)
        {
            return false;
        }

        Camera uiCamera = GetUiCamera();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, eventData.position, uiCamera, out Vector2 localPoint))
        {
            return false;
        }

        Rect rect = viewportRect.rect;
        if (!rect.Contains(localPoint))
        {
            return false;
        }

        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);
        pixelCoordinate = new Vector2(
            normalizedX * boundDocument.source.pixelWidth,
            (1f - normalizedY) * boundDocument.source.pixelHeight);
        return true;
    }

    private void TryEmitPixelPoint(PointerEventData eventData)
    {
        if (TryGetPixelCoordinate(eventData, out Vector2 pixelCoordinate))
        {
            PixelPointPicked?.Invoke(pixelCoordinate);
        }
    }

    private void HandlePanDrag(PointerEventData eventData)
    {
        if (viewportRect == null)
        {
            return;
        }

        Vector2 delta = eventData.position - lastPanPointerPosition;
        lastPanPointerPosition = eventData.position;
        viewportRect.anchoredPosition += delta;
        eventData.Use();
    }

    private void ApplyZoom(float zoomFactor, Vector2 localPivot)
    {
        if (viewportRect == null)
        {
            return;
        }

        float previousScale = zoomScale;
        zoomScale = Mathf.Clamp(zoomScale * zoomFactor, minZoom, maxZoom);
        if (Mathf.Approximately(previousScale, zoomScale))
        {
            return;
        }

        Vector2 previousOffset = viewportRect.anchoredPosition;
        float scaleRatio = zoomScale / previousScale;
        viewportRect.localScale = Vector3.one * zoomScale;
        viewportRect.anchoredPosition = previousOffset - localPivot * (scaleRatio - 1f);
    }

    private void ResetViewTransform()
    {
        zoomScale = 1f;
        if (viewportRect != null)
        {
            viewportRect.localScale = Vector3.one;
            viewportRect.anchoredPosition = Vector2.zero;
        }

        isPanning = false;
    }

    private void RefreshGrid()
    {
        if (viewportRect == null)
        {
            return;
        }

        int verticalCount = 12;
        int horizontalCount = 12;
        Rect rect = viewportRect.rect;

        for (int i = 0; i < gridLines.Length; i++)
        {
            if (gridLines[i] == null)
            {
                continue;
            }

            bool isVertical = i < verticalCount;
            int index = isVertical ? i : i - verticalCount;
            int divisions = isVertical ? verticalCount : horizontalCount;
            float t = (index + 1f) / (divisions + 1f);

            RectTransform lineRect = gridLines[i].rectTransform;
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);

            if (isVertical)
            {
                lineRect.sizeDelta = new Vector2(1.5f, rect.height);
                lineRect.anchoredPosition = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, t), 0f);
            }
            else
            {
                lineRect.sizeDelta = new Vector2(rect.width, 1.5f);
                lineRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(rect.yMin, rect.yMax, t));
            }
        }
    }

    private void RefreshMarkers()
    {
        if (boundDocument == null || boundDocument.source == null)
        {
            SetMarkerVisible(anchorAMarker, false);
            SetMarkerVisible(anchorBMarker, false);
            SetMarkerVisible(rotationAMarker, false);
            SetMarkerVisible(rotationBMarker, false);
            SetMarkerVisible(originMarker, false);
            return;
        }

        UpdateMarker(anchorAMarker, boundDocument.calibration.hasAnchorA, boundDocument.calibration.anchorPixelA, "1");
        UpdateMarker(anchorBMarker, boundDocument.calibration.hasAnchorB, boundDocument.calibration.anchorPixelB, "2");
        UpdateMarker(rotationAMarker, boundDocument.calibration.hasRotationPointA, boundDocument.calibration.rotationPixelA, "R1");
        UpdateMarker(rotationBMarker, boundDocument.calibration.hasRotationPointB, boundDocument.calibration.rotationPixelB, "R2");
        UpdateMarker(originMarker, boundDocument.calibration.hasOriginPixel, boundDocument.calibration.originPixel, "O");
    }

    private void RefreshLines()
    {
        if (boundDocument == null || boundDocument.source == null)
        {
            SetLineVisible(anchorLine, anchorLineLabel, false);
            SetLineVisible(rotationLine, null, false);
            return;
        }

        bool hasAnchorLine = boundDocument.calibration.hasAnchorA && boundDocument.calibration.hasAnchorB;
        SetLine(anchorLine, anchorLineLabel, hasAnchorLine, boundDocument.calibration.anchorPixelA, boundDocument.calibration.anchorPixelB, GetAnchorLineLabel());

        bool hasRotationLine = boundDocument.calibration.hasRotationGuide &&
                               boundDocument.calibration.hasRotationPointA &&
                               boundDocument.calibration.hasRotationPointB;
        SetLine(rotationLine, null, hasRotationLine, boundDocument.calibration.rotationPixelA, boundDocument.calibration.rotationPixelB, string.Empty);
    }

    private void RefreshCrosshair()
    {
        if (boundDocument == null || boundDocument.source == null || crosshairHorizontal == null || crosshairVertical == null)
        {
            return;
        }

        Vector2 pivotPixel = boundDocument.calibration.hasOriginPixel
            ? boundDocument.calibration.originPixel
            : DrawingOverlayCalibrationService.GetImageCenterPixel(boundDocument.source);
        Vector2 anchoredPosition = PixelToAnchoredPosition(pivotPixel);

        RectTransform horizontalRect = crosshairHorizontal.rectTransform;
        horizontalRect.anchoredPosition = anchoredPosition;
        horizontalRect.sizeDelta = new Vector2(28f, 2f);

        RectTransform verticalRect = crosshairVertical.rectTransform;
        verticalRect.anchoredPosition = anchoredPosition;
        verticalRect.sizeDelta = new Vector2(2f, 28f);

        bool visible = boundDocument.source.pixelWidth > 0 && boundDocument.source.pixelHeight > 0;
        crosshairHorizontal.enabled = visible;
        crosshairVertical.enabled = visible;
    }

    private void RefreshHint()
    {
        if (hintText == null)
        {
            return;
        }

        hintText.text = currentStep switch
        {
            OverlayCalibrationStep.PickingAnchorA => "기준점 1을 찍으세요",
            OverlayCalibrationStep.PickingAnchorB => "기준점 2를 찍으세요",
            OverlayCalibrationStep.PickingRotationA => "회전 기준선 첫 점",
            OverlayCalibrationStep.PickingRotationB => "회전 기준선 두 번째 점",
            OverlayCalibrationStep.PickingOrigin => "원점 기준점을 찍으세요",
            OverlayCalibrationStep.ReadyToApply => "보정값이 적용되었습니다",
            _ => "기준점 또는 회전 기준선을 선택하세요",
        };
    }

    private void SetLine(Image line, TMP_Text lineLabel, bool visible, Vector2 startPixel, Vector2 endPixel, string labelText)
    {
        if (line == null)
        {
            return;
        }

        line.enabled = visible;
        if (lineLabel != null)
        {
            lineLabel.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        Vector2 start = PixelToAnchoredPosition(startPixel);
        Vector2 end = PixelToAnchoredPosition(endPixel);
        Vector2 delta = end - start;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        RectTransform rect = line.rectTransform;
        rect.anchoredPosition = (start + end) * 0.5f;
        rect.sizeDelta = new Vector2(Mathf.Max(1f, length), 2f);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (lineLabel != null)
        {
            lineLabel.text = labelText;
            lineLabel.rectTransform.anchoredPosition = rect.anchoredPosition + new Vector2(0f, 20f);
        }
    }

    private void SetLineVisible(Image line, TMP_Text label, bool visible)
    {
        if (line != null)
        {
            line.enabled = visible;
        }

        if (label != null)
        {
            label.gameObject.SetActive(visible);
        }
    }

    private void UpdateMarker(PreviewMarker marker, bool visible, Vector2 pixelCoordinate, string labelText)
    {
        if (marker == null || marker.Root == null)
        {
            return;
        }

        marker.Root.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        marker.Label.text = labelText;
        marker.Root.anchoredPosition = PixelToAnchoredPosition(pixelCoordinate);
    }

    private void SetMarkerVisible(PreviewMarker marker, bool visible)
    {
        if (marker != null && marker.Root != null)
        {
            marker.Root.gameObject.SetActive(visible);
        }
    }

    private string GetAnchorLineLabel()
    {
        if (boundDocument == null || !boundDocument.calibration.hasAnchorA || !boundDocument.calibration.hasAnchorB)
        {
            return string.Empty;
        }

        if (boundDocument.calibration.realDistanceMm <= 0f)
        {
            return "기준 길이";
        }

        float meters = MeasurementUnits.MillimetersToMeters(boundDocument.calibration.realDistanceMm);
        return meters.ToString("0.###", CultureInfo.InvariantCulture) + "m";
    }

    private Vector2 PixelToAnchoredPosition(Vector2 pixelCoordinate)
    {
        if (boundDocument == null || boundDocument.source == null || viewportRect == null)
        {
            return Vector2.zero;
        }

        Rect rect = viewportRect.rect;
        float normalizedX = boundDocument.source.pixelWidth > 0 ? pixelCoordinate.x / boundDocument.source.pixelWidth : 0f;
        float normalizedY = boundDocument.source.pixelHeight > 0 ? 1f - (pixelCoordinate.y / boundDocument.source.pixelHeight) : 0f;
        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedX),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedY));
    }

    private void EnsureOverlayVisuals()
    {
        if (viewportRect == null)
        {
            return;
        }

        if (overlayRoot == null)
        {
            overlayRoot = CreateRect("PreviewOverlay", viewportRect);
            overlayRoot.anchorMin = Vector2.zero;
            overlayRoot.anchorMax = Vector2.one;
            overlayRoot.offsetMin = Vector2.zero;
            overlayRoot.offsetMax = Vector2.zero;
        }

        if (gridRoot == null)
        {
            gridRoot = CreateRect("GridRoot", overlayRoot);
            gridRoot.anchorMin = Vector2.zero;
            gridRoot.anchorMax = Vector2.one;
            gridRoot.offsetMin = Vector2.zero;
            gridRoot.offsetMax = Vector2.zero;
        }

        if (guideRoot == null)
        {
            guideRoot = CreateRect("GuideRoot", overlayRoot);
            guideRoot.anchorMin = Vector2.zero;
            guideRoot.anchorMax = Vector2.one;
            guideRoot.offsetMin = Vector2.zero;
            guideRoot.offsetMax = Vector2.zero;
        }

        for (int i = 0; i < gridLines.Length; i++)
        {
            if (gridLines[i] != null)
            {
                continue;
            }

            Image line = CreateImage($"GridLine_{i}", gridRoot, new Color(0.31f, 0.42f, 0.72f, 0.18f));
            line.raycastTarget = false;
            gridLines[i] = line;
        }

        anchorAMarker ??= CreateMarker("AnchorA", guideRoot, new Color32(255, 194, 83, 255));
        anchorBMarker ??= CreateMarker("AnchorB", guideRoot, new Color32(255, 194, 83, 255));
        rotationAMarker ??= CreateMarker("RotationA", guideRoot, new Color32(112, 206, 255, 255));
        rotationBMarker ??= CreateMarker("RotationB", guideRoot, new Color32(112, 206, 255, 255));
        originMarker ??= CreateMarker("Origin", guideRoot, new Color32(175, 216, 255, 255));

        anchorLine ??= CreateImage("AnchorLine", guideRoot, new Color32(255, 194, 83, 255));
        anchorLine.raycastTarget = false;
        rotationLine ??= CreateImage("RotationLine", guideRoot, new Color32(112, 206, 255, 255));
        rotationLine.raycastTarget = false;
        crosshairHorizontal ??= CreateImage("CrosshairH", guideRoot, new Color32(175, 216, 255, 255));
        crosshairVertical ??= CreateImage("CrosshairV", guideRoot, new Color32(175, 216, 255, 255));

        if (anchorLineLabel == null)
        {
            anchorLineLabel = CreateText("AnchorLineLabel", guideRoot, 18, FontStyles.Bold, tmpFontAsset);
            anchorLineLabel.alignment = TextAlignmentOptions.Center;
        }

        if (hintText == null)
        {
            hintText = CreateText("HintText", guideRoot, 18, FontStyles.Bold, tmpFontAsset);
            hintText.color = new Color32(229, 233, 255, 255);
            hintText.alignment = TextAlignmentOptions.TopLeft;
            RectTransform rect = hintText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(280f, 28f);
            rect.anchoredPosition = new Vector2(14f, -14f);
        }
    }

    private void EnsureViewportMask()
    {
        if (viewportRect == null)
        {
            return;
        }

        RectTransform maskRoot = viewportRect.parent as RectTransform;
        if (maskRoot == null)
        {
            maskRoot = viewportRect;
        }

        if (maskRoot.GetComponent<RectMask2D>() == null)
        {
            maskRoot.gameObject.AddComponent<RectMask2D>();
        }
    }

    private static RectTransform CreateRect(string name, RectTransform parent)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, float fontSize, FontStyles style, TMP_FontAsset overrideFont)
    {
        RectTransform rect = CreateRect(name, parent);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = overrideFont != null
            ? overrideFont
            : TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static PreviewMarker CreateMarker(string name, RectTransform parent, Color color)
    {
        RectTransform root = CreateRect(name, parent);
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(26f, 26f);

        Image ring = CreateImage("Ring", root, color);
        ring.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        ring.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ring.rectTransform.sizeDelta = new Vector2(16f, 16f);

        TMP_Text label = CreateText("Label", root, 16f, FontStyles.Bold, null);
        label.alignment = TextAlignmentOptions.Center;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(36f, 20f);
        labelRect.anchoredPosition = new Vector2(0f, -18f);

        root.gameObject.SetActive(false);
        return new PreviewMarker(root, ring, label);
    }

    private Camera GetUiCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private sealed class PreviewMarker
    {
        public PreviewMarker(RectTransform root, Image ring, TMP_Text label)
        {
            Root = root;
            Ring = ring;
            Label = label;
        }

        public RectTransform Root { get; }
        public Image Ring { get; }
        public TMP_Text Label { get; }
    }
}
