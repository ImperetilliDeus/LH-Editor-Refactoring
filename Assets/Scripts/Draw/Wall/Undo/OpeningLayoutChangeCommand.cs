public sealed class OpeningLayoutChangeCommand : IEditorCommand
{
    public OpeningLayoutChangeCommand(UndoRedoManager.OpeningLayoutSnapshot before, UndoRedoManager.OpeningLayoutSnapshot after) { }
    public void Execute(UndoRedoManager context) { }
    public void Undo(UndoRedoManager context) { }
    public void Redo(UndoRedoManager context) { }
}
