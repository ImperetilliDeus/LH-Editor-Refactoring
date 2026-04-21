using System.Collections.Generic;

internal sealed class VertexGroupMoveCommand : IEditorCommand
{
    private readonly List<UndoRedoManager.WallStateChangeRecord> wallChanges;

    public VertexGroupMoveCommand(int vertexId, List<UndoRedoManager.WallStateChangeRecord> wallChanges)
    {
        this.wallChanges = wallChanges != null
            ? new List<UndoRedoManager.WallStateChangeRecord>(wallChanges)
            : new List<UndoRedoManager.WallStateChangeRecord>();
    }

    public void Execute(UndoRedoManager context)
    {
        Apply(context, true);
    }

    public void Undo(UndoRedoManager context)
    {
        Apply(context, false);
    }

    public void Redo(UndoRedoManager context)
    {
        Apply(context, true);
    }

    private void Apply(UndoRedoManager context, bool useAfterState)
    {
        if (wallChanges.Count == 0)
        {
            return;
        }

        context.ApplyWallStateChanges(wallChanges, useAfterState);
    }
}
