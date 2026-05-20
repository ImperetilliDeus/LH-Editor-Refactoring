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

    private bool[] topViewRootActiveStates;
    private bool hasCachedTopViewRootStates;
    private bool buttonsBound;
    private bool warnedMissingTopButton;
    private bool warnedMissingPerspectiveButton;

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

        hasCachedTopViewRootStates = false;
        topViewRootActiveStates = null;
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
        else
        {
            WarnMissingOptionalButton(ref warnedMissingTopButton, nameof(topButton));
        }

        if (perspectiveButton != null)
        {
            perspectiveButton.onClick.AddListener(SetPerspectiveView);
        }
        else
        {
            WarnMissingOptionalButton(ref warnedMissingPerspectiveButton, nameof(perspectiveButton));
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

        if (!active)
        {
            if (!hasCachedTopViewRootStates)
            {
                CacheTopViewRootActiveStates();
            }
        }

        foreach (GameObject root in topViewOnlyRoots)
        {
            if (root != null)
            {
                root.SetActive(active ? GetCachedTopViewRootActiveState(root) : false);
            }
            else
            {
                Debug.LogWarning($"{nameof(EditorViewModeManager)} has a missing top-view-only root reference.", this);
            }
        }

        if (active)
        {
            hasCachedTopViewRootStates = false;
        }
    }

    private void CacheTopViewRootActiveStates()
    {
        if (topViewRootActiveStates == null || topViewRootActiveStates.Length != topViewOnlyRoots.Length)
        {
            topViewRootActiveStates = new bool[topViewOnlyRoots.Length];
        }

        for (int i = 0; i < topViewOnlyRoots.Length; i++)
        {
            GameObject root = topViewOnlyRoots[i];
            topViewRootActiveStates[i] = root != null && root.activeSelf;
        }

        hasCachedTopViewRootStates = true;
    }

    private bool GetCachedTopViewRootActiveState(GameObject root)
    {
        if (!hasCachedTopViewRootStates || topViewRootActiveStates == null)
        {
            return root != null && root.activeSelf;
        }

        for (int i = 0; i < topViewOnlyRoots.Length && i < topViewRootActiveStates.Length; i++)
        {
            if (topViewOnlyRoots[i] == root)
            {
                return topViewRootActiveStates[i];
            }
        }

        return root != null && root.activeSelf;
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

    private void WarnMissingOptionalButton(ref bool warned, string referenceName)
    {
        if (warned)
        {
            return;
        }

        Debug.LogWarning($"{nameof(EditorViewModeManager)} is missing optional {referenceName} reference.", this);
        warned = true;
    }
}
