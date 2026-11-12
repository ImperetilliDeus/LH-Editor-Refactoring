using System.Collections.Generic;
using UnityEngine;

// Single rule for which room a furniture instance belongs to, shared by export, save and load:
// keep the current room while the furniture is still inside it, otherwise take the first room that contains it.
public static class FurnitureRoomResolver
{
    public static Room Resolve(FurnitureInstance instance, IReadOnlyList<Room> rooms)
    {
        if (instance == null || rooms == null || rooms.Count == 0)
        {
            return null;
        }

        Bounds bounds = instance.CalculateWorldBounds();
        Vector3 position = instance.transform.position;
        Room currentRoom = instance.CurrentRoom;
        if (IsFurnitureInsideRoom(currentRoom, bounds, position))
        {
            return currentRoom;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (IsFurnitureInsideRoom(room, bounds, position))
            {
                return room;
            }
        }

        return null;
    }

    public static bool IsFurnitureInsideRoom(Room room, Bounds bounds, Vector3 worldPosition)
    {
        if (room == null)
        {
            return false;
        }

        List<Vector3> vertices = new List<Vector3>();
        if (!room.TryGetOrderedVertices(vertices))
        {
            return false;
        }

        return PolygonXZ.ContainsBoundsFootprint(bounds, vertices) || PolygonXZ.ContainsPoint(worldPosition, vertices);
    }
}
