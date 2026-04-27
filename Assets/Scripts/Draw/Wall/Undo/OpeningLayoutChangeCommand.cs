internal sealed class OpeningLayoutChangeCommand : IEditorCommand
{
    private readonly UndoRedoManager.OpeningLayoutSnapshot before;
    private readonly UndoRedoManager.OpeningLayoutSnapshot after;

    public OpeningLayoutChangeCommand(
        UndoRedoManager.OpeningLayoutSnapshot before,
        UndoRedoManager.OpeningLayoutSnapshot after)
    {
        this.before = before;
        this.after = after;
    }

    public void Execute(UndoRedoManager context)
    {
        Redo(context);
    }

    public void Undo(UndoRedoManager context)
    {
        context.ApplyOpeningLayoutSnapshot(before, after);
    }

    public void Redo(UndoRedoManager context)
    {
        context.ApplyOpeningLayoutSnapshot(after, before);
    }
}
