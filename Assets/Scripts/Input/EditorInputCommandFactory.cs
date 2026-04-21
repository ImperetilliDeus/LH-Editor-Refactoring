using System.Collections.Generic;

public sealed class EditorInputCommandFactory
{
    public IEditorInputCommand CreateCommand(
        EditorMode mode,
        EditorInputFrame inputFrame,
        IReadOnlyDictionary<EditorMode, IEditorModeInputHandler> handlers)
    {
        if (handlers == null)
        {
            return null;
        }

        switch (mode)
        {
            case EditorMode.Default:
                return CreateDispatchCommand(EditorMode.Default, inputFrame, handlers);
            case EditorMode.RoomCreate:
                return CreateDispatchCommand(EditorMode.RoomCreate, inputFrame, handlers);
            case EditorMode.FurniturePlace:
                return CreateDispatchCommand(EditorMode.FurniturePlace, inputFrame, handlers);
            case EditorMode.DetailEdit:
                return CreateDispatchCommand(EditorMode.DetailEdit, inputFrame, handlers);
            default:
                return null;
        }
    }

    private static IEditorInputCommand CreateDispatchCommand(
        EditorMode mode,
        EditorInputFrame inputFrame,
        IReadOnlyDictionary<EditorMode, IEditorModeInputHandler> handlers)
    {
        if (!handlers.TryGetValue(mode, out IEditorModeInputHandler handler) || handler == null)
        {
            return null;
        }

        return new ModeScopedInputCommand(handler, inputFrame);
    }

    private sealed class ModeScopedInputCommand : IEditorInputCommand
    {
        private readonly IEditorModeInputHandler handler;
        private readonly EditorInputFrame inputFrame;

        public ModeScopedInputCommand(IEditorModeInputHandler handler, EditorInputFrame inputFrame)
        {
            this.handler = handler;
            this.inputFrame = inputFrame;
        }

        public void Execute()
        {
            handler.HandleEditorInput(inputFrame);
        }
    }
}
