using System.Collections.Generic;

internal sealed class WallSelectionMoveCommand : IEditorCommand
{
    private readonly List<UndoRedoManager.WallStateChangeRecord> wallChanges;
    private readonly List<UndoRedoManager.OpeningLayoutChangeRecord> openingChanges;

    public WallSelectionMoveCommand(
        List<UndoRedoManager.WallStateChangeRecord> wallChanges,
        List<UndoRedoManager.OpeningLayoutChangeRecord> openingChanges)
    {
        this.wallChanges = wallChanges != null
            ? new List<UndoRedoManager.WallStateChangeRecord>(wallChanges)
            : new List<UndoRedoManager.WallStateChangeRecord>();
        this.openingChanges = openingChanges != null
            ? new List<UndoRedoManager.OpeningLayoutChangeRecord>(openingChanges)
            : new List<UndoRedoManager.OpeningLayoutChangeRecord>();
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
        if (wallChanges.Count > 0)
        {
            context.ApplyWallStateChanges(wallChanges, useAfterState);
        }

        for (int i = 0; i < openingChanges.Count; i++)
        {
            UndoRedoManager.OpeningLayoutChangeRecord change = openingChanges[i];
            UndoRedoManager.OpeningLayoutSnapshot target = useAfterState ? change.after : change.before;
            UndoRedoManager.OpeningLayoutSnapshot current = useAfterState ? change.before : change.after;
            context.ApplyOpeningLayoutSnapshot(target, current);
        }
    }
}
