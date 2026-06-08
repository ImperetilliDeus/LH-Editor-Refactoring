using System.Collections.Generic;
using UnityEngine;

public enum SceneHierarchyTreeRowKind
{
    Room,
    Wall
}

public sealed class SceneHierarchyTreeRow
{
    public SceneHierarchyTreeRow(
        SceneHierarchyTreeRowKind kind,
        int depth,
        string displayName,
        Wall representativeWall,
        Room room = null)
    {
        Kind = kind;
        Depth = depth;
        DisplayName = displayName;
        RepresentativeWall = representativeWall;
        Room = room;
    }

    public SceneHierarchyTreeRowKind Kind { get; }
    public int Depth { get; }
    public string DisplayName { get; }
    public Wall RepresentativeWall { get; }
    public Room Room { get; }
}

public static class SceneHierarchyTreeModel
{
    public static List<SceneHierarchyTreeRow> BuildRows(Transform wallRoot, IEnumerable<Room> rooms)
    {
        List<LogicalWall> logicalWalls = CollectLogicalWalls(wallRoot);
        List<SceneHierarchyTreeRow> rows = new List<SceneHierarchyTreeRow>();
        HashSet<LogicalWall> assignedLogicalWalls = new HashSet<LogicalWall>();

        if (rooms != null)
        {
            foreach (Room room in rooms)
            {
                if (room == null)
                {
                    continue;
                }

                rows.Add(new SceneHierarchyTreeRow(
                    SceneHierarchyTreeRowKind.Room,
                    0,
                    GetRoomDisplayName(room),
                    null,
                    room));

                HashSet<string> roomWallIds = CreateWallIdSet(room.EffectiveWallIds);
                for (int i = 0; i < logicalWalls.Count; i++)
                {
                    LogicalWall logicalWall = logicalWalls[i];
                    if (!logicalWall.HasAnyId(roomWallIds))
                    {
                        continue;
                    }

                    rows.Add(CreateWallRow(logicalWall, 1));
                    assignedLogicalWalls.Add(logicalWall);
                }
            }
        }

        for (int i = 0; i < logicalWalls.Count; i++)
        {
            LogicalWall logicalWall = logicalWalls[i];
            if (assignedLogicalWalls.Contains(logicalWall))
            {
                continue;
            }

            rows.Add(CreateWallRow(logicalWall, 0));
        }

        return rows;
    }

    private static List<LogicalWall> CollectLogicalWalls(Transform wallRoot)
    {
        List<Wall> walls = new List<Wall>();
        WallHierarchyUtility.CollectWalls(wallRoot, walls);

        List<LogicalWall> logicalWalls = new List<LogicalWall>();
        Dictionary<WallOpeningContainer, LogicalWall> containerWalls = new Dictionary<WallOpeningContainer, LogicalWall>();

        for (int i = 0; i < walls.Count; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
            if (container != null)
            {
                if (!containerWalls.TryGetValue(container, out LogicalWall logicalWall))
                {
                    logicalWall = new LogicalWall(container.gameObject.name, wall);
                    containerWalls.Add(container, logicalWall);
                    logicalWalls.Add(logicalWall);
                }
                else
                {
                    logicalWall.AddWall(wall);
                }

                continue;
            }

            if (WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                continue;
            }

            logicalWalls.Add(new LogicalWall(wall.gameObject.name, wall));
        }

        return logicalWalls;
    }

    private static SceneHierarchyTreeRow CreateWallRow(LogicalWall logicalWall, int depth)
    {
        return new SceneHierarchyTreeRow(
            SceneHierarchyTreeRowKind.Wall,
            depth,
            logicalWall.DisplayName,
            logicalWall.RepresentativeWall);
    }

    private static string GetRoomDisplayName(Room room)
    {
        if (!string.IsNullOrWhiteSpace(room.RoomName))
        {
            return $"Room ({room.RoomName})";
        }

        return room.gameObject.name;
    }

    private static HashSet<string> CreateWallIdSet(IReadOnlyList<string> wallIds)
    {
        HashSet<string> results = new HashSet<string>();
        if (wallIds == null)
        {
            return results;
        }

        for (int i = 0; i < wallIds.Count; i++)
        {
            string id = wallIds[i];
            if (!string.IsNullOrWhiteSpace(id))
            {
                results.Add(id);
            }
        }

        return results;
    }

    private sealed class LogicalWall
    {
        private readonly List<Wall> walls = new List<Wall>();

        public LogicalWall(string displayName, Wall wall)
        {
            DisplayName = displayName;
            AddWall(wall);
        }

        public string DisplayName { get; }
        public Wall RepresentativeWall { get; private set; }

        public void AddWall(Wall wall)
        {
            if (wall == null)
            {
                return;
            }

            walls.Add(wall);
            if (RepresentativeWall == null || WallHierarchyUtility.IsHiddenOpeningBaseSegment(RepresentativeWall))
            {
                RepresentativeWall = wall;
            }
        }

        public bool HasAnyId(HashSet<string> wallIds)
        {
            if (wallIds == null || wallIds.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < walls.Count; i++)
            {
                Wall wall = walls[i];
                if (wall != null && wall.Data != null && wallIds.Contains(wall.Data.id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
