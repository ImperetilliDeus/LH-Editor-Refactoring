internal sealed class WallEditTool : IWallTool
{
    private readonly IWallToolContext context;

    public WallEditTool(IWallToolContext context)
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

        if (inputFrame.DeletePressedThisFrame)
        {
            return WallToolRequest.ActivateDelete;
        }

        if (inputFrame.PointerOverUI || !inputFrame.LeftPressedThisFrame)
        {
            return WallToolRequest.None;
        }

        if (context.TryConsumeEditSelectionPress())
        {
            return WallToolRequest.None;
        }

        return WallToolRequest.ActivateDraw;
    }
}
