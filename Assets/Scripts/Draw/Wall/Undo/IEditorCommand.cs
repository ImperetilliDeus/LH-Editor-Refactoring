public interface IEditorCommand
{
    void Execute(UndoRedoManager context);
    void Undo(UndoRedoManager context);
    void Redo(UndoRedoManager context);
}
