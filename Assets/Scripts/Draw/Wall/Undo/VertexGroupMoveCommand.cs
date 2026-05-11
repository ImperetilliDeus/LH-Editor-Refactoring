using System.Collections.Generic;

public sealed class VertexGroupMoveCommand : IEditorCommand
{
    public VertexGroupMoveCommand(List<UndoRedoManager.WallStateChangeRecord> wallChanges) { }
    public void Execute(UndoRedoManager context) { }
    public void Undo(UndoRedoManager context) { }
    public void Redo(UndoRedoManager context) { }
}
