using UnityEngine;

public sealed class WallSplitCommand : IEditorCommand
{
    public WallSplitCommand(GameObject originalWall, GameObject firstSplitWall, GameObject secondSplitWall) { }
    public void Execute(UndoRedoManager context) { }
    public void Undo(UndoRedoManager context) { }
    public void Redo(UndoRedoManager context) { }
}
