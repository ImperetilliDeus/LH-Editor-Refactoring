using UnityEngine;

internal sealed class WallToolController
{
    private readonly IWallToolContext context;
    private readonly IWallTool editTool;
    private readonly IWallTool drawTool;
    private readonly IWallTool deleteTool;
    private readonly float doubleClickThreshold;

    private IWallTool activeTool;
    private float lastLeftClickTime = -1f;

    public WallToolController(IWallToolContext context, float doubleClickThreshold)
    {
        this.context = context;
        this.doubleClickThreshold = Mathf.Max(0.05f, doubleClickThreshold);

        editTool = new WallEditTool(context);
        drawTool = new WallDrawTool(context);
        deleteTool = new WallDeleteTool(context);
        SetActiveTool(editTool);
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
            case WallToolRequest.ActivateEdit:
                SetActiveTool(editTool);
                break;
            case WallToolRequest.ActivateDraw:
                TryActivateDrawTool();
                break;
            case WallToolRequest.ActivateDelete:
                ActivateDeleteTool();
                break;
        }
    }

    public void ActivateEditTool()
    {
        SetActiveTool(editTool);
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

    private void ActivateDeleteTool()
    {
        SetActiveTool(deleteTool);
        SetActiveTool(editTool);
    }
}
