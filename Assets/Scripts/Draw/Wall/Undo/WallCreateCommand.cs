using UnityEngine;

public sealed class WallCreateCommand : IEditorCommand
{
    public WallCreateCommand(GameObject wallObject) { }
    public void Execute(UndoRedoManager context) { }
    public void Undo(UndoRedoManager context) { }
    public void Redo(UndoRedoManager context) { }
}
