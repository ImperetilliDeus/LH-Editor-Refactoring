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
        if (lastNotifiedSelectedWall == selectedWall)
        {
            return;
        }

        lastNotifiedSelectedWall = selectedWall;
        SelectionChanged?.Invoke(selectedWall);
    }

    private void UpdateSelectUIVisibility()
    {
        bool visible = modeManager != null &&
                       modeManager.IsMode(EditorMode.DetailEdit) &&
                       (wallOpeningPlacementManager == null || !wallOpeningPlacementManager.IsOpeningDetailMenuVisible) &&
                       (selectedWall != null || detailSelectedWalls.Count > 0);
        SetSelectUIVisible(visible);
    }

    private void MarkTopViewDirty()
    {
        EditorVisualEvents.RequestTopViewRefresh();
    }
}
