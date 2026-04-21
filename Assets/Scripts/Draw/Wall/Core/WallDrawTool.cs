internal sealed class WallDrawTool : IWallTool
{
    private readonly IWallToolContext context;

    public WallDrawTool(IWallToolContext context)
    {
        this.context = context;
    }

    public void Enter()
    {
        context.SetWallCreationModeActive(true);
        if (context.IsPreviewWallEnabled())
        {
            context.EnsurePreviewWallState();
            context.UpdatePreviewWallState();
        }
    }

    public void Exit()
    {
        context.ExitWallCreationModeState();
    }

    public WallToolRequest HandleInput(WallToolInputFrame inputFrame)
    {
        if (!inputFrame.IsAvailable || context.IsHandleInputLocked())
        {
            return WallToolRequest.None;
        }

        if (inputFrame.RightPressedThisFrame)
        {
            return WallToolRequest.ActivateIdle;
        }

        context.UpdatePreviewWallState();
        if (!inputFrame.PointerOverUI && inputFrame.LeftPressedThisFrame)
        {
            context.CommitCurrentSegmentState();
        }

        return WallToolRequest.None;
    }
}
