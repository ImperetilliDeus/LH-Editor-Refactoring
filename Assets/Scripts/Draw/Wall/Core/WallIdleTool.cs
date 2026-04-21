internal sealed class WallIdleTool : IWallTool
{
    private readonly IWallToolContext context;

    public WallIdleTool(IWallToolContext context)
    {
        this.context = context;
    }

    public void Enter()
    {
        context.SetWallCreationModeActive(false);
        context.ClearPreviewSnappedHandle();
    }

    public void Exit()
    {
    }

    public WallToolRequest HandleInput(WallToolInputFrame inputFrame)
    {
        if (!inputFrame.IsAvailable || context.IsHandleInputLocked())
        {
            return WallToolRequest.None;
        }

        if (inputFrame.PointerOverUI || !inputFrame.LeftPressedThisFrame)
        {
            return WallToolRequest.None;
        }

        if (context.TryConsumeIdleSelectionPress())
        {
            return WallToolRequest.None;
        }

        return WallToolRequest.ActivateDraw;
    }
}
