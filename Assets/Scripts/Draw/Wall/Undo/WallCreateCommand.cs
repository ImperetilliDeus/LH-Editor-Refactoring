using UnityEngine;

internal sealed class WallCreateCommand : IEditorCommand
{
    private GameObject wallObject;
    private readonly UndoRedoManager.WallStateSnapshot snapshot;

    public WallCreateCommand(GameObject createdWall)
    {
        wallObject = createdWall;
        snapshot = UndoRedoManager.WallStateSnapshot.Capture(createdWall);
    }

    public void Execute(UndoRedoManager context)
    {
        Redo(context);
    }

    public void Undo(UndoRedoManager context)
    {
        if (wallObject == null)
        {
            return;
        }

        context.UnregisterWallVisuals(wallObject);
        Object.Destroy(wallObject);
        wallObject = null;
    }

    public void Redo(UndoRedoManager context)
    {
        if (wallObject != null)
        {
            return;
        }

        wallObject = context.CreateWallFromSnapshot(snapshot);
        context.RegisterWallVisuals(wallObject);
    }
}
