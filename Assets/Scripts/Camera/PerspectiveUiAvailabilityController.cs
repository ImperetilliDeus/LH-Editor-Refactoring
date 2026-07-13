using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class PerspectiveUiAvailabilityController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EditorViewModeManager viewModeManager;

    [Header("Perspective Visibility")]
    [SerializeField] private GameObject[] hiddenInPerspective;
    [SerializeField] private GameObject[] visibleInPerspective;

    [Header("Perspective Interaction")]
    [SerializeField] private Selectable[] disabledInPerspective;

    private readonly Dictionary<GameObject, bool> activeStateCache = new Dictionary<GameObject, bool>();
    private readonly Dictionary<Selectable, bool> interactableStateCache = new Dictionary<Selectable, bool>();

    private bool eventsBound;

    private void OnEnable()
    {
        ResolveReferences();
        BindEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindEvents();
        RestoreTopState();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        RestoreTopState();
    }

    public void Refresh()
    {
        if (viewModeManager == null)
        {
            return;
        }

        Apply(viewModeManager.CurrentViewMode);
    }

    public void SetReferencesForTests(
        EditorViewModeManager viewModeManager,
        GameObject[] hiddenInPerspective,
        GameObject[] visibleInPerspective,
        Selectable[] disabledInPerspective)
    {
        UnbindEvents();
        RestoreTopState();

        this.viewModeManager = viewModeManager;
        this.hiddenInPerspective = hiddenInPerspective;
        this.visibleInPerspective = visibleInPerspective;
        this.disabledInPerspective = disabledInPerspective;

        if (isActiveAndEnabled)
        {
            BindEvents();
            Refresh();
        }
    }

    private void ResolveReferences()
    {
        if (viewModeManager == null)
        {
            LayerUtility.ResolveObject(ref viewModeManager);
        }
    }

    private void BindEvents()
    {
        if (eventsBound || viewModeManager == null)
        {
            return;
        }

        viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        viewModeManager.ViewModeChanged += HandleViewModeChanged;
        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound || viewModeManager == null)
        {
            eventsBound = false;
            return;
        }

        viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        eventsBound = false;
    }

    private void HandleViewModeChanged(EditorViewMode viewMode)
    {
        Apply(viewMode);
    }

    private void Apply(EditorViewMode viewMode)
    {
        if (viewMode == EditorViewMode.Perspective3D)
        {
            ApplyPerspectiveState();
        }
        else
        {
            RestoreTopState();
        }
    }

    private void ApplyPerspectiveState()
    {
        CacheActiveStates(hiddenInPerspective);
        CacheActiveStates(visibleInPerspective);
        CacheInteractableStates(disabledInPerspective);

        SetActive(hiddenInPerspective, false);
        SetActive(visibleInPerspective, true);
        SetInteractable(disabledInPerspective, false);
    }

    private void RestoreTopState()
    {
        foreach (KeyValuePair<GameObject, bool> pair in activeStateCache)
        {
            if (pair.Key != null)
            {
                pair.Key.SetActive(pair.Value);
            }
        }

        foreach (KeyValuePair<Selectable, bool> pair in interactableStateCache)
        {
            if (pair.Key != null)
            {
                pair.Key.interactable = pair.Value;
            }
        }

        activeStateCache.Clear();
        interactableStateCache.Clear();
    }

    private void CacheActiveStates(GameObject[] targets)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            GameObject target = targets[i];
            if (target != null && !activeStateCache.ContainsKey(target))
            {
                activeStateCache.Add(target, target.activeSelf);
            }
        }
    }

    private void CacheInteractableStates(Selectable[] targets)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            Selectable target = targets[i];
            if (target != null && !interactableStateCache.ContainsKey(target))
            {
                interactableStateCache.Add(target, target.interactable);
            }
        }
    }

    private static void SetActive(GameObject[] targets, bool active)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].SetActive(active);
            }
        }
    }

    private static void SetInteractable(Selectable[] targets, bool interactable)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                targets[i].interactable = interactable;
            }
        }
    }
}
