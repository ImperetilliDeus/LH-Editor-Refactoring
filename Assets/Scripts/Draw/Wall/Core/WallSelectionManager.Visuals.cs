using UnityEngine;

public partial class WallSelectionManager
{
    private void RefreshSelectionVisuals()
    {
        RefreshSelectedWallLengthLabel();
        RefreshWallSelectionUIStates();
        UpdateSelectUIVisibility();
        NotifySelectionChangedIfNeeded();
        SelectionSetChanged?.Invoke();
    }

    private void RefreshSelectedWallLengthLabel()
    {
        if (wallLengthDisplay == null)
        {
            return;
        }

        Transform selectedTransform = selectionState.SelectedWall != null
            ? selectionState.SelectedWall.transform
            : null;
        wallLengthDisplay.SetSelectedWall(selectedTransform);
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
                       selectionState.HasSelection;
        SetSelectUIVisible(visible);
    }

    private void MarkTopViewDirty()
    {
        EditorVisualEvents.RequestTopViewRefresh();
    }
}
