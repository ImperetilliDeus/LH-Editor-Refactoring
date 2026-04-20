using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class OverlayCalibrationPanelController : MonoBehaviour
{
    private const bool ShowAdvancedCalibrationControls = false;

    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private OverlayPreviewController previewController;
    [SerializeField] private Button pickAnchorAButton;
    [SerializeField] private Button pickAnchorBButton;
    [SerializeField] private Button applyScaleButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button completeButton;
    [SerializeField] private Button pickOriginButton;
    [SerializeField] private Button pickRotationGuideButton;
    [SerializeField] private Slider fineRotationSlider;
    [SerializeField] private InputField distanceMetersInputField;
    [SerializeField] private InputField fineRotationInputField;
    [SerializeField] private TMP_Text scaleValueText;
    [SerializeField] private TMP_Text rotationValueText;
    [SerializeField] private TMP_Text offsetXValueText;
    [SerializeField] private TMP_Text offsetYValueText;
    [SerializeField] private TMP_Text statusText;

    private DrawingOverlayManager manager;
    private DrawingOverlayDocument document;
    private OverlayCalibrationStep step;

    public Texture2D CurrentTexture { get; private set; }

    private void Awake()
    {
        BindUI();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        UnbindUI();
        if (previewController != null)
        {
            previewController.PixelPointPicked -= HandlePixelPointPicked;
        }
    }

    public void Open(DrawingOverlayManager owner, DrawingOverlayDocument activeDocument, Texture2D texture)
    {
        manager = owner;
        document = activeDocument;
        CurrentTexture = texture;

        if (previewController != null)
        {
            previewController.Bind(document, CurrentTexture);
            previewController.SetCalibrationStep(step);
        }

        ConfigureVisibleControls();
        SyncInputsFromDocument();
        SetStep(OverlayCalibrationStep.Idle);
        SetStatus(GetDefaultStatusMessage());
        SetVisible(true);
    }

    public void Initialize(
        CanvasGroup resolvedCanvasGroup,
        OverlayPreviewController resolvedPreviewController,
        Button resolvedPickAnchorAButton,
        Button resolvedPickAnchorBButton,
        Button resolvedApplyScaleButton,
        Button resolvedResetButton,
        Button resolvedCompleteButton,
        Button resolvedPickOriginButton,
        Button resolvedPickRotationGuideButton,
        Slider resolvedFineRotationSlider,
        InputField resolvedDistanceMetersInputField,
        InputField resolvedFineRotationInputField,
        TMP_Text resolvedScaleValueText,
        TMP_Text resolvedRotationValueText,
        TMP_Text resolvedOffsetXValueText,
        TMP_Text resolvedOffsetYValueText,
        TMP_Text resolvedStatusText)
    {
        UnbindUI();
        if (previewController != null)
        {
            previewController.PixelPointPicked -= HandlePixelPointPicked;
        }

        canvasGroup = resolvedCanvasGroup;
        previewController = resolvedPreviewController;
        pickAnchorAButton = resolvedPickAnchorAButton;
        pickAnchorBButton = resolvedPickAnchorBButton;
        applyScaleButton = resolvedApplyScaleButton;
        resetButton = resolvedResetButton;
        completeButton = resolvedCompleteButton;
        pickOriginButton = resolvedPickOriginButton;
        pickRotationGuideButton = resolvedPickRotationGuideButton;
        fineRotationSlider = resolvedFineRotationSlider;
        distanceMetersInputField = resolvedDistanceMetersInputField;
        fineRotationInputField = resolvedFineRotationInputField;
        scaleValueText = resolvedScaleValueText;
        rotationValueText = resolvedRotationValueText;
        offsetXValueText = resolvedOffsetXValueText;
        offsetYValueText = resolvedOffsetYValueText;
        statusText = resolvedStatusText;

        BindUI();
    }

    public void Close()
    {
        SetVisible(false);
        SetStep(OverlayCalibrationStep.Idle);
    }

    public void ShowStatusOnly(string text)
    {
        SetVisible(true);
        SetStep(OverlayCalibrationStep.Idle);
        SetStatus(text);
        previewController?.RefreshVisuals();
    }

    private void BindUI()
    {
        if (pickAnchorAButton != null)
        {
            pickAnchorAButton.onClick.AddListener(() => SetStep(OverlayCalibrationStep.PickingAnchorA));
        }

        if (pickAnchorBButton != null)
        {
            pickAnchorBButton.onClick.AddListener(() => SetStep(OverlayCalibrationStep.PickingAnchorB));
        }

        if (pickOriginButton != null)
        {
            pickOriginButton.onClick.AddListener(() => SetStep(OverlayCalibrationStep.PickingOrigin));
        }

        if (pickRotationGuideButton != null)
        {
            pickRotationGuideButton.onClick.AddListener(BeginRotationGuidePicking);
        }

        if (applyScaleButton != null)
        {
            applyScaleButton.onClick.AddListener(HandleApplyClicked);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(HandleResetClicked);
        }

        if (completeButton != null)
        {
            completeButton.onClick.AddListener(HandleCompleteClicked);
        }

        if (fineRotationSlider != null)
        {
            fineRotationSlider.onValueChanged.AddListener(HandleFineRotationSliderChanged);
        }

        if (fineRotationInputField != null)
        {
            fineRotationInputField.onEndEdit.AddListener(HandleFineRotationInputChanged);
        }

        if (previewController != null)
        {
            previewController.PixelPointPicked -= HandlePixelPointPicked;
            previewController.PixelPointPicked += HandlePixelPointPicked;
        }
    }

    private void ConfigureVisibleControls()
    {
        SetControlVisible(pickOriginButton, ShowAdvancedCalibrationControls);
        SetControlVisible(pickRotationGuideButton, ShowAdvancedCalibrationControls);
    }

    private void UnbindUI()
    {
        if (pickAnchorAButton != null)
        {
            pickAnchorAButton.onClick.RemoveAllListeners();
        }

        if (pickAnchorBButton != null)
        {
            pickAnchorBButton.onClick.RemoveAllListeners();
        }

        if (pickOriginButton != null)
        {
            pickOriginButton.onClick.RemoveAllListeners();
        }

        if (pickRotationGuideButton != null)
        {
            pickRotationGuideButton.onClick.RemoveAllListeners();
        }

        if (applyScaleButton != null)
        {
            applyScaleButton.onClick.RemoveAllListeners();
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
        }

        if (completeButton != null)
        {
            completeButton.onClick.RemoveAllListeners();
        }

        if (fineRotationSlider != null)
        {
            fineRotationSlider.onValueChanged.RemoveListener(HandleFineRotationSliderChanged);
        }

        if (fineRotationInputField != null)
        {
            fineRotationInputField.onEndEdit.RemoveListener(HandleFineRotationInputChanged);
        }
    }

    private void BeginRotationGuidePicking()
    {
        if (document == null)
        {
            return;
        }

        document.calibration.hasRotationGuide = true;
        SetStep(OverlayCalibrationStep.PickingRotationA);
    }

    private void HandleApplyClicked()
    {
        if (document == null)
        {
            return;
        }

        document.calibration.realDistanceMm = ParseMetersToMillimeters(distanceMetersInputField != null ? distanceMetersInputField.text : string.Empty);
        if (manager != null && manager.ApplyCalibration())
        {
            SetStatus("스케일이 보정되었습니다.");
            SetStep(OverlayCalibrationStep.ReadyToApply);
        }
        else
        {
            SetStatus("기준점 2개와 실제 거리를 확인하세요.");
        }

        RefreshSolvedState();
        previewController?.RefreshVisuals();
    }

    private void HandleResetClicked()
    {
        if (manager == null || document == null)
        {
            return;
        }

        manager.ResetCalibration();
        SyncInputsFromDocument();
        SetStatus("설정을 초기화했습니다.");
        SetStep(OverlayCalibrationStep.Idle);
        previewController?.RefreshVisuals();
    }

    private void HandleCompleteClicked()
    {
        manager?.CompleteCalibration();
    }

    private void HandleFineRotationSliderChanged(float value)
    {
        if (document == null)
        {
            return;
        }

        document.calibration.manualRotationOffsetDeg = value;
        if (fineRotationInputField != null)
        {
            fineRotationInputField.SetTextWithoutNotify(value.ToString("0.0", CultureInfo.InvariantCulture));
        }

        ApplyIfPossibleWithoutStatusNoise();
        previewController?.RefreshVisuals();
    }

    private void HandleFineRotationInputChanged(string value)
    {
        if (document == null)
        {
            return;
        }

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            parsed = document.calibration.manualRotationOffsetDeg;
        }

        document.calibration.manualRotationOffsetDeg = parsed;
        if (fineRotationSlider != null)
        {
            fineRotationSlider.SetValueWithoutNotify(parsed);
        }

        ApplyIfPossibleWithoutStatusNoise();
        previewController?.RefreshVisuals();
    }

    private void HandlePixelPointPicked(Vector2 pixelCoordinate)
    {
        if (document == null)
        {
            return;
        }

        switch (step)
        {
            case OverlayCalibrationStep.PickingAnchorA:
                document.calibration.anchorPixelA = pixelCoordinate;
                document.calibration.hasAnchorA = true;
                SetStatus("기준점 1이 저장되었습니다.");
                SetStep(OverlayCalibrationStep.Idle);
                break;
            case OverlayCalibrationStep.PickingAnchorB:
                document.calibration.anchorPixelB = pixelCoordinate;
                document.calibration.hasAnchorB = true;
                SetStatus("기준점 2가 저장되었습니다.");
                SetStep(OverlayCalibrationStep.Idle);
                break;
            case OverlayCalibrationStep.PickingRotationA:
                document.calibration.rotationPixelA = pixelCoordinate;
                document.calibration.hasRotationPointA = true;
                SetStatus("회전 기준점 1이 저장되었습니다.");
                SetStep(OverlayCalibrationStep.PickingRotationB);
                break;
            case OverlayCalibrationStep.PickingRotationB:
                document.calibration.rotationPixelB = pixelCoordinate;
                document.calibration.hasRotationPointB = true;
                SetStatus("회전 기준점 2가 저장되었습니다.");
                SetStep(OverlayCalibrationStep.Idle);
                ApplyIfPossibleWithoutStatusNoise();
                break;
            case OverlayCalibrationStep.PickingOrigin:
                document.calibration.originPixel = pixelCoordinate;
                document.calibration.originWorldXZ = Vector2.zero;
                document.calibration.hasOriginPixel = true;
                SetStatus("원점 기준점이 저장되었습니다.");
                SetStep(OverlayCalibrationStep.Idle);
                ApplyIfPossibleWithoutStatusNoise();
                break;
        }

        manager?.NotifyDocumentChanged();
        previewController?.RefreshVisuals();
    }

    private void ApplyIfPossibleWithoutStatusNoise()
    {
        if (manager == null || document == null)
        {
            return;
        }

        document.calibration.realDistanceMm = ParseMetersToMillimeters(distanceMetersInputField != null ? distanceMetersInputField.text : string.Empty);
        manager.ApplyCalibration();
        RefreshSolvedState();
        previewController?.RefreshVisuals();
    }

    private void SyncInputsFromDocument()
    {
        if (document == null)
        {
            return;
        }

        if (distanceMetersInputField != null)
        {
            distanceMetersInputField.SetTextWithoutNotify(MeasurementUnits.MillimetersToMeters(document.calibration.realDistanceMm).ToString("0.###", CultureInfo.InvariantCulture));
        }

        if (fineRotationSlider != null)
        {
            fineRotationSlider.SetValueWithoutNotify(document.calibration.manualRotationOffsetDeg);
        }

        if (fineRotationInputField != null)
        {
            fineRotationInputField.SetTextWithoutNotify(document.calibration.manualRotationOffsetDeg.ToString("0.0", CultureInfo.InvariantCulture));
        }

        RefreshSolvedState();
        previewController?.RefreshVisuals();
    }

    private void RefreshSolvedState()
    {
        if (document == null)
        {
            SetMetricText(scaleValueText, "-");
            SetMetricText(rotationValueText, "-");
            SetMetricText(offsetXValueText, "-");
            SetMetricText(offsetYValueText, "-");
            return;
        }

        float scaleMetersPerPixel = MeasurementUnits.MillimetersToMeters(document.solved.mmPerPixel);
        SetMetricText(scaleValueText, scaleMetersPerPixel > 0f ? scaleMetersPerPixel.ToString("0.###", CultureInfo.InvariantCulture) : "-");
        SetMetricText(rotationValueText, document.solved.totalRotationDeg.ToString("0.0", CultureInfo.InvariantCulture) + "°");
        SetMetricText(offsetXValueText, document.solved.worldOffsetXZ.x.ToString("0.##", CultureInfo.InvariantCulture));
        SetMetricText(offsetYValueText, document.solved.worldOffsetXZ.y.ToString("0.##", CultureInfo.InvariantCulture));
    }

    private void SetStep(OverlayCalibrationStep nextStep)
    {
        step = nextStep;
        previewController?.SetCalibrationStep(step);
        if (step == OverlayCalibrationStep.PickingAnchorA)
        {
            SetStatus("미리보기에서 기준점 1을 클릭하세요.");
        }
        else if (step == OverlayCalibrationStep.PickingAnchorB)
        {
            SetStatus("미리보기에서 기준점 2를 클릭하세요.");
        }
        else if (step == OverlayCalibrationStep.PickingRotationA)
        {
            SetStatus("수평/수직 기준선의 첫 점을 클릭하세요.");
        }
        else if (step == OverlayCalibrationStep.PickingRotationB)
        {
            SetStatus("수평/수직 기준선의 두 번째 점을 클릭하세요.");
        }
        else if (step == OverlayCalibrationStep.PickingOrigin)
        {
            SetStatus("월드 원점에 맞출 점을 클릭하세요.");
        }

        previewController?.RefreshVisuals();
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }

    private string GetDefaultStatusMessage()
    {
        if (document == null || document.source == null)
        {
            return "미리보기를 불러온 뒤 기준점을 찍으세요.";
        }

        return document.source.sourceType == OverlaySourceType.PdfPage
            ? "PDF 미리보기를 불러왔습니다. 기준점을 찍으세요."
            : "이미지를 불러왔습니다. 기준점을 찍으세요.";
    }

    private static void SetControlVisible(Behaviour control, bool visible)
    {
        if (control == null)
        {
            return;
        }

        control.gameObject.SetActive(visible);
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        gameObject.SetActive(visible);
    }

    private static void SetMetricText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private static float ParseMetersToMillimeters(string input)
    {
        if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float meters))
        {
            return 0f;
        }

        return Mathf.Max(0f, MeasurementUnits.MetersToMillimeters(meters));
    }
}
