public partial class TopViewRenderManager
{
    private void BindVisualEvents()
    {
        EditorVisualEvents.TopViewRefreshRequested -= HandleTopViewRefreshRequested;
        EditorVisualEvents.TopViewRefreshRequested += HandleTopViewRefreshRequested;
    }

    private void UnbindVisualEvents()
    {
        EditorVisualEvents.TopViewRefreshRequested -= HandleTopViewRefreshRequested;
    }

    private void HandleTopViewRefreshRequested()
    {
        MarkDirty();
    }
}
