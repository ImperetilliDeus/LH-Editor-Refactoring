using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionUndoRecorder
{
    public void FinalizeMove(
        bool isDraggingWall,
        UndoRedoManager undoRedoManager,
        WallSelectionDragState dragState,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        GameObject selectedWall,
        Vector3 moveStartWallPosition,
        Quaternion moveStartWallRotation,
        Vector3 moveStartWallScale)
    {
        if (!isDraggingWall || undoRedoManager == null)
        {
            return;
        }

        List<UndoRedoManager.OpeningLayoutChangeRecord> openingChanges =
            BuildMoveOpeningChangeRecords(dragState, wallOpeningPlacementManager);
        List<UndoRedoManager.WallStateChangeRecord> wallChanges =
            BuildMoveWallStateChangeRecords(dragState, selectedWall, moveStartWallPosition, moveStartWallRotation, moveStartWallScale);

        if (wallChanges.Count == 0 && openingChanges.Count == 0)
        {
            return;
        }

        undoRedoManager.ExecuteCommand(
            new WallSelectionMoveCommand(wallChanges, openingChanges),
            alreadyExecuted: true);
    }

    private static List<UndoRedoManager.OpeningLayoutChangeRecord> BuildMoveOpeningChangeRecords(
        WallSelectionDragState dragState,
        WallOpeningPlacementManager wallOpeningPlacementManager)
    {
        List<UndoRedoManager.OpeningLayoutChangeRecord> results = new List<UndoRedoManager.OpeningLayoutChangeRecord>();
        if (dragState == null || wallOpeningPlacementManager == null)
        {
            return results;
        }

        if (dragState.SelectedOpeningContainer != null && dragState.HasMoveStartOpeningLayoutSnapshot)
        {
            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot =
                wallOpeningPlacementManager.CaptureLayoutSnapshot(dragState.SelectedOpeningContainer);
            if (UndoRedoManager.OpeningLayoutSnapshot.HasMeaningfulDelta(dragState.MoveStartOpeningLayoutSnapshot, afterSnapshot))
            {
                results.Add(new UndoRedoManager.OpeningLayoutChangeRecord
                {
                    before = dragState.MoveStartOpeningLayoutSnapshot,
                    after = afterSnapshot,
                });
            }
        }

        foreach (KeyValuePair<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot> pair in dragState.MoveStartConnectedOpeningSnapshots)
        {
            if (pair.Key == null)
            {
                continue;
            }

            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(pair.Key);
            if (!UndoRedoManager.OpeningLayoutSnapshot.HasMeaningfulDelta(pair.Value, afterSnapshot))
            {
                continue;
            }

            results.Add(new UndoRedoManager.OpeningLayoutChangeRecord
            {
                before = pair.Value,
                after = afterSnapshot,
            });
        }

        return results;
    }

    private static List<UndoRedoManager.WallStateChangeRecord> BuildMoveWallStateChangeRecords(
        WallSelectionDragState dragState,
        GameObject selectedWall,
        Vector3 moveStartWallPosition,
        Quaternion moveStartWallRotation,
        Vector3 moveStartWallScale)
    {
        List<UndoRedoManager.WallStateChangeRecord> results = new List<UndoRedoManager.WallStateChangeRecord>();
        if (dragState == null)
        {
            return results;
        }

        if (dragState.MoveStartSnapshots.Count > 0)
        {
            foreach (KeyValuePair<GameObject, UndoRedoManager.WallStateSnapshot> pair in dragState.MoveStartSnapshots)
            {
                GameObject wallObject = pair.Key;
                if (wallObject == null)
                {
                    continue;
                }

                UndoRedoManager.WallStateSnapshot startSnapshot = pair.Value;
                UndoRedoManager.WallStateSnapshot endSnapshot = UndoRedoManager.WallStateSnapshot.Capture(wallObject);
                if (!UndoRedoManager.WallStateSnapshot.HasMeaningfulDelta(startSnapshot, endSnapshot))
                {
                    continue;
                }

                results.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = startSnapshot,
                    after = endSnapshot,
                });
            }

            return results;
        }

        if (selectedWall == null)
        {
            return results;
        }

        UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(
            selectedWall,
            moveStartWallPosition,
            moveStartWallRotation,
            moveStartWallScale);
        UndoRedoManager.WallStateSnapshot after = UndoRedoManager.WallStateSnapshot.Capture(selectedWall);
        if (!UndoRedoManager.WallStateSnapshot.HasMeaningfulDelta(before, after))
        {
            return results;
        }

        results.Add(new UndoRedoManager.WallStateChangeRecord
        {
            before = before,
            after = after,
        });
        return results;
    }
}
