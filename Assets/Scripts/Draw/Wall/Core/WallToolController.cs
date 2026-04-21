using UnityEngine;

internal sealed class WallToolController
{
    private readonly IWallToolContext context;
    private readonly IWallTool idleTool;
    private readonly IWallTool drawTool;
    private readonly float doubleClickThreshold;

    private IWallTool activeTool;
    private float lastLeftClickTime = -1f;

    public WallToolController(IWallToolContext context, float doubleClickThreshold)
    {
        this.context = context;
        this.doubleClickThreshold = Mathf.Max(0.05f, doubleClickThreshold);

        idleTool = new WallIdleTool(context);
        drawTool = new WallDrawTool(context);
        SetActiveTool(idleTool);
    }

    public void HandleInput(WallToolInputFrame inputFrame)
    {
        if (activeTool == null)
        {
            return;
        }

        WallToolRequest request = activeTool.HandleInput(inputFrame);
        switch (request)
        {
            case WallToolRequest.ActivateIdle:
                SetActiveTool(idleTool);
                break;
            case WallToolRequest.ActivateDraw:
                TryActivateDrawTool();
                break;
        }
    }

    public void ActivateIdleTool()
    {
        SetActiveTool(idleTool);
    }

    private void TryActivateDrawTool()
    {
        float currentTime = Time.unscaledTime;
        bool isDoubleClick = lastLeftClickTime >= 0f && currentTime - lastLeftClickTime <= doubleClickThreshold;
        lastLeftClickTime = currentTime;

        if (!isDoubleClick || !context.TryPrepareWallCreationStart())
        {
            return;
        }

        SetActiveTool(drawTool);
    }

    private void SetActiveTool(IWallTool nextTool)
    {
        if (nextTool == null || ReferenceEquals(activeTool, nextTool))
        {
            return;
        }

        activeTool?.Exit();
        activeTool = nextTool;
        activeTool.Enter();
    }
}
