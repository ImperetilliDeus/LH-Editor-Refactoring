using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSplitCommand : IEditorCommand
{
    private readonly UndoRedoManager.WallStateSnapshot originalSnapshot;
    private readonly UndoRedoManager.WallStateSnapshot firstSplitSnapshot;
    private readonly UndoRedoManager.WallStateSnapshot secondSplitSnapshot;
    private GameObject restoredOriginalWall;
    private GameObject restoredFirstSplitWall;
    private GameObject restoredSecondSplitWall;

    public WallSplitCommand(GameObject originalWall, GameObject firstSplitWall, GameObject secondSplitWall)
    {
        originalSnapshot = UndoRedoManager.WallStateSnapshot.Capture(originalWall);
        firstSplitSnapshot = UndoRedoManager.WallStateSnapshot.Capture(firstSplitWall);
        secondSplitSnapshot = UndoRedoManager.WallStateSnapshot.Capture(secondSplitWall);
        restoredFirstSplitWall = firstSplitWall;
        restoredSecondSplitWall = secondSplitWall;
    }

    public void Execute(UndoRedoManager context)
    {
        Redo(context);
    }

    public void Undo(UndoRedoManager context)
    {
        List<Wall> removedWalls = CollectExistingWalls(restoredFirstSplitWall, restoredSecondSplitWall);

        DeleteWall(context, ref restoredFirstSplitWall);
        DeleteWall(context, ref restoredSecondSplitWall);

        if (restoredOriginalWall == null)
        {
            restoredOriginalWall = context.CreateWallFromSnapshot(originalSnapshot);
            context.RegisterWallVisuals(restoredOriginalWall);
        }

        Wall restoredWallComponent = restoredOriginalWall != null ? restoredOriginalWall.GetComponent<Wall>() : null;
        RoomTopologyEvents.RequestRefreshForWallReplacement(
            removedWalls,
            restoredWallComponent != null ? new[] { restoredWallComponent } : null);
    }

    public void Redo(UndoRedoManager context)
    {
        List<Wall> removedWalls = CollectExistingWalls(restoredOriginalWall);

        DeleteWall(context, ref restoredOriginalWall);

        if (restoredFirstSplitWall == null)
        {
            restoredFirstSplitWall = context.CreateWallFromSnapshot(firstSplitSnapshot);
            context.RegisterWallVisuals(restoredFirstSplitWall);
        }

        if (restoredSecondSplitWall == null)
        {
            restoredSecondSplitWall = context.CreateWallFromSnapshot(secondSplitSnapshot);
            context.RegisterWallVisuals(restoredSecondSplitWall);
        }

        List<Wall> addedWalls = CollectExistingWalls(restoredFirstSplitWall, restoredSecondSplitWall);
        RoomTopologyEvents.RequestRefreshForWallReplacement(removedWalls, addedWalls);
    }

    private static List<Wall> CollectExistingWalls(params GameObject[] wallObjects)
    {
        List<Wall> results = new List<Wall>();
        if (wallObjects == null)
        {
            return results;
        }

        for (int i = 0; i < wallObjects.Length; i++)
        {
            GameObject wallObject = wallObjects[i];
            if (wallObject == null)
            {
                continue;
            }

            Wall wall = wallObject.GetComponent<Wall>();
            if (wall != null)
            {
                results.Add(wall);
            }
        }

        return results;
    }

    private static void DeleteWall(UndoRedoManager context, ref GameObject wallObject)
    {
        if (wallObject == null)
        {
            return;
        }

        context.UnregisterWallVisuals(wallObject);
        Object.Destroy(wallObject);
        wallObject = null;
    }
}
