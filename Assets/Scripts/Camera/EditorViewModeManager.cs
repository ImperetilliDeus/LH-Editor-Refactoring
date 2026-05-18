using System;
using UnityEngine;
using UnityEngine.UI;

public enum EditorViewMode
{
    Top = 0,
    Perspective3D = 1
}

public sealed class EditorViewModeManager : MonoBehaviour
{
    [SerializeField] private EditorViewMode initialViewMode = EditorViewMode.Top;
    [SerializeField] private Camera topCamera;
    [SerializeField] private Camera perspectiveCamera;
    [SerializeField] private Behaviour topCameraManager;
    [SerializeField] private Behaviour perspectiveCameraManager;
    [SerializeField] private GameObject[] topViewOnlyRoots;
    [SerializeField] private Button topButton;
    [SerializeField] private Button perspectiveButton;

    public EditorViewMode CurrentViewMode { get; private set; } = EditorViewMode.Top;

    public event Action<EditorViewMode> ViewModeChanged;

    private bool buttonsBound;

    private void Awake()
    {
        BindButtons();
        CurrentViewMode = initialViewMode;
        ApplyViewMode(initialViewMode);
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void SetTopView()
    {
        SetViewMode(EditorViewMode.Top);
    }

    public void SetPerspectiveView()
    {
        SetViewMode(EditorViewMode.Perspective3D);
    }

    public void SetViewMode(EditorViewMode mode)
    {
        bool changed = CurrentViewMode != mode;
        CurrentViewMode = mode;

        ApplyViewMode(mode);

        if (changed)
        {
            ViewModeChanged?.Invoke(mode);
        }
    }

    public void SetReferencesForTests(
        Camera topCamera,
        Camera perspectiveCamera,
        Behaviour topCameraManager,
        Behaviour perspectiveCameraManager,
        GameObject[] topViewOnlyRoots,
        Button topButton,
        Button perspectiveButton)
    {
        UnbindButtons();

        this.topCamera = topCamera;
        this.perspectiveCamera = perspectiveCamera;
        this.topCameraManager = topCameraManager;
        this.perspectiveCameraManager = perspectiveCameraManager;
        this.topViewOnlyRoots = topViewOnlyRoots;
        this.topButton = topButton;
        this.perspectiveButton = perspectiveButton;

        BindButtons();
    }

    private void BindButtons()
    {
        if (buttonsBound)
        {
            UnbindButtons();
        }

        if (topButton != null)
        {
            topButton.onClick.AddListener(SetTopView);
        }

        if (perspectiveButton != null)
        {
            perspectiveButton.onClick.AddListener(SetPerspectiveView);
        }

        buttonsBound = true;
    }

    private void UnbindButtons()
    {
        if (topButton != null)
        {
            topButton.onClick.RemoveListener(SetTopView);
        }

        if (perspectiveButton != null)
        {
            perspectiveButton.onClick.RemoveListener(SetPerspectiveView);
        }

        buttonsBound = false;
    }

    private void ApplyViewMode(EditorViewMode mode)
    {
        bool topViewEnabled = mode == EditorViewMode.Top;

        SetEnabled(topCamera, topViewEnabled, nameof(topCamera));
        SetEnabled(perspectiveCamera, !topViewEnabled, nameof(perspectiveCamera));
        SetEnabled(topCameraManager, topViewEnabled, nameof(topCameraManager));
        SetEnabled(perspectiveCameraManager, !topViewEnabled, nameof(perspectiveCameraManager));
        SetTopViewOnlyRootsActive(topViewEnabled);
        SetInteractable(topButton, !topViewEnabled);
        SetInteractable(perspectiveButton, topViewEnabled);
    }

    private void SetTopViewOnlyRootsActive(bool active)
    {
        if (topViewOnlyRoots == null)
        {
            Debug.LogWarning($"{nameof(EditorViewModeManager)} is missing top-view-only roots.", this);
            return;
        }

        foreach (GameObject root in topViewOnlyRoots)
        {
            if (root != null)
            {
                root.SetActive(active);
            }
            else
            {
                Debug.LogWarning($"{nameof(EditorViewModeManager)} has a missing top-view-only root reference.", this);
            }
        }
    }

    private void SetEnabled(Behaviour target, bool enabled, string referenceName)
    {
        if (target != null)
        {
            target.enabled = enabled;
        }
        else
        {
            Debug.LogWarning($"{nameof(EditorViewModeManager)} is missing a {referenceName} reference.", this);
        }
    }

    private void SetInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }
}
