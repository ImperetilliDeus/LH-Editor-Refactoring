using System.Collections.Generic;

internal sealed class MoveConnectedWallsCommand : IEditorCommand
{
    private readonly List<UndoRedoManager.WallStateChangeRecord> records;

    public MoveConnectedWallsCommand(List<UndoRedoManager.WallStateChangeRecord> records)
    {
        this.records = records != null
            ? new List<UndoRedoManager.WallStateChangeRecord>(records)
            : new List<UndoRedoManager.WallStateChangeRecord>();
    }

    public void Execute(UndoRedoManager context)
    {
        Redo(context);
    }

    public void Undo(UndoRedoManager context)
    {
        context.ApplyWallStateChanges(records, false);
    }

    public void Redo(UndoRedoManager context)
    {
        context.ApplyWallStateChanges(records, true);
    }
}
