using System.Collections.Generic;

public sealed class WallSelectionMoveCommand : IEditorCommand
{
    public WallSelectionMoveCommand(
        List<UndoRedoManager.WallStateChangeRecord> wallChanges,
        List<UndoRedoManager.OpeningLayoutChangeRecord> openingChanges) { }
    public void Execute(UndoRedoManager context) { }
    public void Undo(UndoRedoManager context) { }
    public void Redo(UndoRedoManager context) { }
}
