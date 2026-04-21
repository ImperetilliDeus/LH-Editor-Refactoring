internal interface IWallTool
{
    void Enter();
    void Exit();
    WallToolRequest HandleInput(WallToolInputFrame inputFrame);
}
