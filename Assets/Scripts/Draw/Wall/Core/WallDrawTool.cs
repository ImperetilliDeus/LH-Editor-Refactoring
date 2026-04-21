internal sealed class WallDrawTool : IWallTool
{
    private readonly DrawManager owner;

    public WallDrawTool(DrawManager owner)
    {
        this.owner = owner;
    }

    public void Enter()
    {
        owner.SetWallCreationModeActive(true);
        if (owner.IsPreviewWallEnabled())
        {
            owner.EnsurePreviewWallState();
            owner.UpdatePreviewWallState();
        }
    }

    public void Exit()
    {
        owner.ExitWallCreationModeState();
    }

    public void HandleInput(WallToolInputFrame inputFrame)
    {
        if (!inputFrame.IsAvailable || owner.IsHandleInputLocked())
        {
            return;
        }

        if (inputFrame.RightPressedThisFrame)
        {
            owner.ActivateIdleTool();
            return;
        }

        owner.UpdatePreviewWallState();
        if (!inputFrame.PointerOverUI && inputFrame.LeftPressedThisFrame)
        {
            owner.CommitCurrentSegmentState();
        }
    }
}
