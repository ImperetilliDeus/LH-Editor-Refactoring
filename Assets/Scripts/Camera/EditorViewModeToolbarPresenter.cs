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

    private bool eventsBound;

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
        LayerUtility.ResolveObject(ref viewModeManager);
        BindEvents();
        Refresh();
    }

    public void SetReferencesForTests(
        EditorViewModeManager viewModeManager,
        Button topButton,
        Button perspectiveButton,
        Image topButtonBackground,
        Image perspectiveButtonBackground,
        Color activeColor,
        Color inactiveColor)
    {
        UnbindEvents();

        this.viewModeManager = viewModeManager;
        this.topButton = topButton;
        this.perspectiveButton = perspectiveButton;
        this.topButtonBackground = topButtonBackground;
        this.perspectiveButtonBackground = perspectiveButtonBackground;
        this.activeColor = activeColor;
        this.inactiveColor = inactiveColor;

        BindEvents();
        Refresh();
    }

    private void BindEvents()
    {
        if (eventsBound || viewModeManager == null)
        {
            return;
        }

        viewModeManager.ViewModeChanged += HandleViewModeChanged;
        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound || viewModeManager == null)
        {
            return;
        }

        viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        eventsBound = false;
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
