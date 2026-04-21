internal sealed class WallDeleteTool : IWallTool
{
    private readonly IWallToolContext context;

    public WallDeleteTool(IWallToolContext context)
    {
        this.context = context;
    }

    public void Enter()
    {
        context.DeleteCurrentSelection();
    }

    public void Exit()
    {
    }

    public WallToolRequest HandleInput(WallToolInputFrame inputFrame)
    {
        return WallToolRequest.ActivateEdit;
    }
}
