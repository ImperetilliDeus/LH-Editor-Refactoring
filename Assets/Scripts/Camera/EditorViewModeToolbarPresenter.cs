using UnityEngine;
using UnityEngine.UI;

public sealed class EditorViewModeToolbarPresenter : MonoBehaviour
{
    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private Button topButton;
    [SerializeField] private Button perspectiveButton;
    [SerializeField] private Image topButtonBackground;
    [SerializeField] private Image perspectiveButtonBackground;
    [SerializeField] private Image topIcon;
    [SerializeField] private Image perspectiveIcon;
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color inactiveColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color activeIconColor = Color.black;
    [SerializeField] private Color inactiveIconColor = new Color(0f, 0f, 0f, 0.45f);

    private void Awake()
    {
        LayerUtility.ResolveObject(ref viewModeManager);

        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged += HandleViewModeChanged;
        }

        Refresh();
    }

    private void OnDestroy()
    {
        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        }
    }

    public void Refresh()
    {
        if (viewModeManager == null)
        {
            return;
        }

        bool topActive = viewModeManager.CurrentViewMode == EditorViewMode.Top;

        SetColor(ResolveBackground(topButtonBackground, topButton), topActive ? activeColor : inactiveColor);
        SetColor(ResolveBackground(perspectiveButtonBackground, perspectiveButton), topActive ? inactiveColor : activeColor);
        SetColor(topIcon, topActive ? activeIconColor : inactiveIconColor);
        SetColor(perspectiveIcon, topActive ? inactiveIconColor : activeIconColor);
    }

    private void HandleViewModeChanged(EditorViewMode viewMode)
    {
        Refresh();
    }

    private static Image ResolveBackground(Image explicitBackground, Button button)
    {
        if (explicitBackground != null)
        {
            return explicitBackground;
        }

        return button != null ? button.targetGraphic as Image : null;
    }

    private static void SetColor(Image target, Color color)
    {
        if (target != null)
        {
            target.color = color;
        }
    }
}
