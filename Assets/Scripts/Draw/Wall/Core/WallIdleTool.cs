internal sealed class WallIdleTool : IWallTool
{
    private readonly DrawManager owner;

    public WallIdleTool(DrawManager owner)
    {
        this.owner = owner;
    }

    public void Enter()
    {
        owner.SetWallCreationModeActive(false);
        owner.HandleManagerRef?.ClearPreviewSnappedHandle();
    }

    public void Exit()
    {
    }

    public void HandleInput(WallToolInputFrame inputFrame)
    {
        if (!inputFrame.IsAvailable || owner.IsHandleInputLocked())
        {
            return;
        }

        if (inputFrame.PointerOverUI || !inputFrame.LeftPressedThisFrame)
        {
            return;
        }

        if (owner.TryConsumeIdleSelectionPress())
        {
            return;
        }

        owner.TryActivateDrawTool();
    }
}
