using UnityEngine;

public partial class WallSelectionManager
{
    private void RefreshSelectionVisuals()
    {
        RefreshWallSelectionUIStates();
        UpdateSelectUIVisibility();
        NotifySelectionChangedIfNeeded();
        SelectionSetChanged?.Invoke();
    }

    private void NotifySelectionChangedIfNeeded()
    {
        if (!selectionState.TryGetSelectionChanged(out GameObject currentSelection))
        {
            return;
        }

        SelectionChanged?.Invoke(currentSelection);
    }

    private void UpdateSelectUIVisibility()
    {
        bool visible = modeManager != null &&
                       modeManager.IsMode(EditorMode.DetailEdit) &&
                       (wallOpeningPlacementManager == null || !wallOpeningPlacementManager.IsOpeningDetailMenuVisible) &&
                       selectionState.SelectedWallCount > 0;
        SetSelectUIVisible(visible);
    }

    private void MarkTopViewDirty()
    {
        EditorVisualEvents.RequestTopViewRefresh();
    }
}
