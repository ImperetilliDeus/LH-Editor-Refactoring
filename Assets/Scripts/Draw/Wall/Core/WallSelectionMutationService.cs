using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionMutationService
{
    public bool TryDeleteSelectedWalls(
        List<GameObject> selectedWalls,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        RoomManager roomManager,
        UndoRedoManager undoRedoManager)
    {
        if (wallOpeningPlacementManager == null || selectedWalls == null || selectedWalls.Count == 0)
        {
            return false;
        }

        List<UndoRedoManager.OpeningLayoutSnapshot> deletedLayouts = new List<UndoRedoManager.OpeningLayoutSnapshot>();
        HashSet<string> processedLayoutKeys = new HashSet<string>();
        HashSet<Wall> affectedWalls = new HashSet<Wall>();

        for (int i = 0; i < selectedWalls.Count; i++)
        {
            GameObject wallObject = selectedWalls[i];
            if (wallObject == null || !wallObject.TryGetComponent(out Wall wall))
            {
                continue;
            }

            UndoRedoManager.OpeningLayoutSnapshot snapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(wall);
            string key = snapshot.hasContainer
                ? $"container:{snapshot.layoutName}"
                : $"wall:{wallObject.GetInstanceID()}";
            if (!processedLayoutKeys.Add(key))
            {
                continue;
            }

            deletedLayouts.Add(snapshot);
            CollectAffectedWalls(wall, snapshot, affectedWalls);
        }

        List<Room> affectedRooms = CollectAffectedRooms(roomManager, affectedWalls);
        undoRedoManager?.RecordDeletedLayouts(deletedLayouts, affectedRooms);

        for (int i = 0; i < affectedRooms.Count; i++)
        {
            if (affectedRooms[i] != null)
            {
                roomManager.DeleteRoom(affectedRooms[i]);
            }
        }

        for (int i = 0; i < deletedLayouts.Count; i++)
        {
            wallOpeningPlacementManager.ApplyLayoutSnapshot(default, deletedLayouts[i]);
        }

        return deletedLayouts.Count > 0 || affectedRooms.Count > 0;
    }

    private static void CollectAffectedWalls(
        Wall wall,
        UndoRedoManager.OpeningLayoutSnapshot snapshot,
        HashSet<Wall> affectedWalls)
    {
        if (wall == null)
        {
            return;
        }

        if (!snapshot.hasContainer)
        {
            affectedWalls.Add(wall);
            return;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return;
        }

        Wall[] containerWalls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < containerWalls.Length; i++)
        {
            if (containerWalls[i] != null)
            {
                affectedWalls.Add(containerWalls[i]);
            }
        }
    }

    private static List<Room> CollectAffectedRooms(RoomManager roomManager, HashSet<Wall> affectedWalls)
    {
        List<Room> affectedRooms = new List<Room>();
        if (roomManager == null || affectedWalls == null || affectedWalls.Count == 0)
        {
            return affectedRooms;
        }

        List<Room> rooms = roomManager.GetAllRooms();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null || room.WallSet == null)
            {
                continue;
            }

            foreach (Wall wall in affectedWalls)
            {
                if (wall != null && room.WallSet.Contains(wall))
                {
                    affectedRooms.Add(room);
                    break;
                }
            }
        }

        return affectedRooms;
    }
}
