using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DrawingOverlayToolbarController : MonoBehaviour
{
    [SerializeField] private DrawingOverlayManager manager;
    [SerializeField] private GameObject collapsedRoot;
    [SerializeField] private GameObject expandedRoot;
    [SerializeField] private Slider opacitySlider;
    [SerializeField] private TMP_Text collapsedOpacityText;
    [SerializeField] private TMP_Text expandedOpacityText;
    [SerializeField] private Button expandButton;
    [SerializeField] private Button collapseButton;
    [SerializeField] private Button visibilityButton;
    [SerializeField] private Button lockButton;

    private bool collapsed = true;
    private bool suppressSliderCallback;

    public void Initialize(
        DrawingOverlayManager resolvedManager,
        GameObject resolvedCollapsedRoot,
        GameObject resolvedExpandedRoot,
        Slider resolvedOpacitySlider,
        TMP_Text resolvedCollapsedOpacityText,
        TMP_Text resolvedExpandedOpacityText,
        Button resolvedExpandButton,
        Button resolvedCollapseButton,
        Button resolvedVisibilityButton,
        Button resolvedLockButton)
    {
        Unbind();

        manager = resolvedManager;
        collapsedRoot = resolvedCollapsedRoot;
        expandedRoot = resolvedExpandedRoot;
        opacitySlider = resolvedOpacitySlider;
        collapsedOpacityText = resolvedCollapsedOpacityText;
        expandedOpacityText = resolvedExpandedOpacityText;
        expandButton = resolvedExpandButton;
        collapseButton = resolvedCollapseButton;
        visibilityButton = resolvedVisibilityButton;
        lockButton = resolvedLockButton;

        Bind();
        ConfigureSlider();
        SetCollapsed(true);
        SyncFromManager();
    }

    public void SetCollapsed(bool value)
    {
        collapsed = value;
        if (collapsedRoot != null)
        {
            collapsedRoot.SetActive(collapsed);
        }

        if (expandedRoot != null)
        {
            expandedRoot.SetActive(!collapsed);
        }
    }

    public void SetManager(DrawingOverlayManager resolvedManager)
    {
        if (manager == resolvedManager)
        {
            SyncFromManager();
            return;
        }

        Unbind();
        manager = resolvedManager;
        Bind();
        SyncFromManager();
    }

    public void RefreshVisibility()
    {
        SyncFromManager();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    private void Bind()
    {
        if (expandButton != null)
        {
            expandButton.onClick.AddListener(Expand);
        }

        if (collapseButton != null)
        {
            collapseButton.onClick.AddListener(Collapse);
        }

        if (visibilityButton != null)
        {
            visibilityButton.onClick.AddListener(ToggleVisibility);
        }

        if (lockButton != null)
        {
            lockButton.onClick.AddListener(ToggleLock);
        }

        if (opacitySlider != null)
        {
            opacitySlider.onValueChanged.AddListener(HandleOpacitySliderChanged);
        }

        if (manager != null)
        {
            manager.ActiveOverlayChanged += HandleActiveOverlayChanged;
        }
    }

    private void Unbind()
    {
        if (expandButton != null)
        {
            expandButton.onClick.RemoveListener(Expand);
        }

        if (collapseButton != null)
        {
            collapseButton.onClick.RemoveListener(Collapse);
        }

        if (visibilityButton != null)
        {
            visibilityButton.onClick.RemoveListener(ToggleVisibility);
        }

        if (lockButton != null)
        {
            lockButton.onClick.RemoveListener(ToggleLock);
        }

        if (opacitySlider != null)
        {
            opacitySlider.onValueChanged.RemoveListener(HandleOpacitySliderChanged);
        }

        if (manager != null)
        {
            manager.ActiveOverlayChanged -= HandleActiveOverlayChanged;
        }
    }

    private void ConfigureSlider()
    {
        if (opacitySlider == null)
        {
            return;
        }

        opacitySlider.minValue = 0f;
        opacitySlider.maxValue = 1f;
        opacitySlider.wholeNumbers = false;
    }

    private void Expand()
    {
        SetCollapsed(false);
    }

    private void Collapse()
    {
        SetCollapsed(true);
    }

    private void ToggleVisibility()
    {
        if (manager == null)
        {
            return;
        }

        manager.SetOverlayVisible(!manager.OverlayVisible);
    }

    private void ToggleLock()
    {
        if (manager == null)
        {
            return;
        }

        manager.SetOverlayLocked(!manager.OverlayLocked);
    }

    private void HandleOpacitySliderChanged(float value)
    {
        if (suppressSliderCallback)
        {
            return;
        }

        manager?.SetOpacity(value);
        UpdateOpacityLabels(value);
    }

    private void HandleActiveOverlayChanged(DrawingOverlayDocument document)
    {
        SyncFromManager();
    }

    private void SyncFromManager()
    {
        bool shouldShow = manager != null && manager.HasAppliedOverlay && !manager.IsCalibrating;
        gameObject.SetActive(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        float opacity = manager.Opacity;
        if (opacitySlider != null)
        {
            suppressSliderCallback = true;
            opacitySlider.SetValueWithoutNotify(opacity);
            suppressSliderCallback = false;
        }

        UpdateOpacityLabels(opacity);
        UpdateToggleLabels();
    }

    private void UpdateOpacityLabels(float opacity)
    {
        int percent = Mathf.RoundToInt(Mathf.Clamp01(opacity) * 100f);
        if (collapsedOpacityText != null)
        {
            collapsedOpacityText.text = $"\uB3C4\uBA74 {percent}%";
        }

        if (expandedOpacityText != null)
        {
            expandedOpacityText.text = $"{percent}%";
        }
    }

    private void UpdateToggleLabels()
    {
        SetButtonLabel(visibilityButton, manager != null && manager.OverlayVisible ? "\uC228\uAE40" : "\uD45C\uC2DC");
        SetButtonLabel(lockButton, manager != null && manager.OverlayLocked ? "\uC7A0\uAE08" : "\uD574\uC81C");
    }

    private static void SetButtonLabel(Button button, string text)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
        }
    }
}
