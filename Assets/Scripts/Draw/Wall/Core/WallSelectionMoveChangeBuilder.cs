using System.Collections.Generic;
using UnityEngine;

internal static class WallSelectionMoveChangeBuilder
{
    public static List<UndoRedoManager.OpeningLayoutChangeRecord> BuildOpeningChangeRecords(
        WallSelectionDragState currentDragState,
        WallOpeningPlacementManager currentWallOpeningPlacementManager)
    {
        List<UndoRedoManager.OpeningLayoutChangeRecord> results = new List<UndoRedoManager.OpeningLayoutChangeRecord>();
        if (currentDragState == null || currentWallOpeningPlacementManager == null)
        {
            return results;
        }

        if (currentDragState.SelectedOpeningContainer != null && currentDragState.HasMoveStartOpeningLayoutSnapshot)
        {
            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot =
                currentWallOpeningPlacementManager.CaptureLayoutSnapshot(currentDragState.SelectedOpeningContainer);
            if (UndoRedoManager.OpeningLayoutSnapshot.HasMeaningfulDelta(currentDragState.MoveStartOpeningLayoutSnapshot, afterSnapshot))
            {
                results.Add(new UndoRedoManager.OpeningLayoutChangeRecord
                {
                    before = currentDragState.MoveStartOpeningLayoutSnapshot,
                    after = afterSnapshot,
                });
            }
        }

        foreach (KeyValuePair<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot> pair in currentDragState.MoveStartConnectedOpeningSnapshots)
        {
            if (pair.Key == null)
            {
                continue;
            }

            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = currentWallOpeningPlacementManager.CaptureLayoutSnapshot(pair.Key);
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

    public static List<UndoRedoManager.WallStateChangeRecord> BuildWallStateChangeRecords(
        WallSelectionDragState currentDragState,
        GameObject selectedWall,
        Vector3 startWallPosition,
        Quaternion startWallRotation,
        Vector3 startWallScale)
    {
        List<UndoRedoManager.WallStateChangeRecord> results = new List<UndoRedoManager.WallStateChangeRecord>();
        if (currentDragState == null)
        {
            return results;
        }

        if (currentDragState.MoveStartSnapshots.Count > 0)
        {
            foreach (KeyValuePair<GameObject, UndoRedoManager.WallStateSnapshot> pair in currentDragState.MoveStartSnapshots)
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
            startWallPosition,
            startWallRotation,
            startWallScale);
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
