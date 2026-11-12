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
    private const float BoundaryDistanceTolerance = 0.05f;
    private const float BoundaryProjectionTolerance = 0.05f;
    private const float BoundaryDirectionDotTolerance = 0.999f;

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
                AddRoomWallRows(rows, room, logicalWalls, roomWallIds, assignedLogicalWalls);
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

    private static void AddRoomWallRows(
        List<SceneHierarchyTreeRow> rows,
        Room room,
        List<LogicalWall> logicalWalls,
        HashSet<string> roomWallIds,
        HashSet<LogicalWall> assignedLogicalWalls)
    {
        List<LogicalWall> roomLogicalWalls = new List<LogicalWall>();
        for (int i = 0; i < logicalWalls.Count; i++)
        {
            LogicalWall logicalWall = logicalWalls[i];
            if (logicalWall.HasAnyId(roomWallIds))
            {
                roomLogicalWalls.Add(logicalWall);
            }
        }

        if (roomLogicalWalls.Count == 0)
        {
            return;
        }

        HashSet<LogicalWall> groupedLogicalWalls = new HashSet<LogicalWall>();
        List<Vector3> vertices = new List<Vector3>();
        if (room != null && room.TryGetOrderedVertices(vertices) && vertices.Count >= 3)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 edgeStart = vertices[i];
                Vector3 edgeEnd = vertices[(i + 1) % vertices.Count];
                List<LogicalWall> edgeWalls = new List<LogicalWall>();

                for (int wallIndex = 0; wallIndex < roomLogicalWalls.Count; wallIndex++)
                {
                    LogicalWall logicalWall = roomLogicalWalls[wallIndex];
                    if (groupedLogicalWalls.Contains(logicalWall) ||
                        !logicalWall.LiesOnBoundaryEdge(edgeStart, edgeEnd))
                    {
                        continue;
                    }

                    edgeWalls.Add(logicalWall);
                }

                if (edgeWalls.Count == 0)
                {
                    continue;
                }

                edgeWalls.Sort((left, right) => CompareAlongEdge(left, right, edgeStart, edgeEnd));
                rows.Add(CreateGroupedWallRow(edgeWalls, 1));
                for (int wallIndex = 0; wallIndex < edgeWalls.Count; wallIndex++)
                {
                    LogicalWall logicalWall = edgeWalls[wallIndex];
                    groupedLogicalWalls.Add(logicalWall);
                    assignedLogicalWalls.Add(logicalWall);
                }
            }
        }

        for (int i = 0; i < roomLogicalWalls.Count; i++)
        {
            LogicalWall logicalWall = roomLogicalWalls[i];
            if (groupedLogicalWalls.Contains(logicalWall))
            {
                continue;
            }

            rows.Add(CreateWallRow(logicalWall, 1));
            assignedLogicalWalls.Add(logicalWall);
        }
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

    private static SceneHierarchyTreeRow CreateGroupedWallRow(List<LogicalWall> logicalWalls, int depth)
    {
        if (logicalWalls == null || logicalWalls.Count == 0)
        {
            return null;
        }

        if (logicalWalls.Count == 1)
        {
            return CreateWallRow(logicalWalls[0], depth);
        }

        List<string> names = new List<string>(logicalWalls.Count);
        for (int i = 0; i < logicalWalls.Count; i++)
        {
            names.Add(logicalWalls[i].DisplayName);
        }

        return new SceneHierarchyTreeRow(
            SceneHierarchyTreeRowKind.Wall,
            depth,
            string.Join(" + ", names),
            logicalWalls[0].RepresentativeWall);
    }

    private static int CompareAlongEdge(LogicalWall left, LogicalWall right, Vector3 edgeStart, Vector3 edgeEnd)
    {
        float leftDistance = left.GetMidpointDistanceAlongEdge(edgeStart, edgeEnd);
        float rightDistance = right.GetMidpointDistanceAlongEdge(edgeStart, edgeEnd);
        return leftDistance.CompareTo(rightDistance);
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
                if (WallHierarchyUtility.HasAnyLogicalWallId(wall, wallIds))
                {
                    return true;
                }
            }

            return false;
        }

        public bool LiesOnBoundaryEdge(Vector3 edgeStart, Vector3 edgeEnd)
        {
            for (int i = 0; i < walls.Count; i++)
            {
                if (WallLiesOnBoundaryEdge(walls[i], edgeStart, edgeEnd))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetMidpointDistanceAlongEdge(Vector3 edgeStart, Vector3 edgeEnd)
        {
            Wall wall = RepresentativeWall;
            if (wall == null || wall.Data == null)
            {
                return 0f;
            }

            Vector3 edgeDirection = edgeEnd - edgeStart;
            edgeDirection.y = 0f;
            float edgeLength = edgeDirection.magnitude;
            if (edgeLength <= 0.0001f)
            {
                return 0f;
            }

            edgeDirection /= edgeLength;
            Vector3 midpoint = (wall.Data.startPoint + wall.Data.endPoint) * 0.5f;
            Vector3 offset = midpoint - edgeStart;
            offset.y = 0f;
            return Vector3.Dot(offset, edgeDirection);
        }

        private static bool WallLiesOnBoundaryEdge(Wall wall, Vector3 edgeStart, Vector3 edgeEnd)
        {
            if (wall == null || wall.Data == null)
            {
                return false;
            }

            Vector3 edgeDirection = edgeEnd - edgeStart;
            edgeDirection.y = 0f;
            float edgeLength = edgeDirection.magnitude;
            if (edgeLength <= 0.0001f)
            {
                return false;
            }

            edgeDirection /= edgeLength;
            Vector3 wallStart = wall.Data.startPoint;
            Vector3 wallEnd = wall.Data.endPoint;
            Vector3 wallDirection = wallEnd - wallStart;
            wallDirection.y = 0f;
            float wallLength = wallDirection.magnitude;
            if (wallLength <= 0.0001f)
            {
                return false;
            }

            wallDirection /= wallLength;
            if (Mathf.Abs(Vector3.Dot(edgeDirection, wallDirection)) < BoundaryDirectionDotTolerance)
            {
                return false;
            }

            if (DistanceToLineXZ(wallStart, edgeStart, edgeDirection) > BoundaryDistanceTolerance ||
                DistanceToLineXZ(wallEnd, edgeStart, edgeDirection) > BoundaryDistanceTolerance)
            {
                return false;
            }

            Vector3 startOffset = wallStart - edgeStart;
            startOffset.y = 0f;
            Vector3 endOffset = wallEnd - edgeStart;
            endOffset.y = 0f;
            float startDistance = Vector3.Dot(startOffset, edgeDirection);
            float endDistance = Vector3.Dot(endOffset, edgeDirection);
            float minDistance = Mathf.Min(startDistance, endDistance);
            float maxDistance = Mathf.Max(startDistance, endDistance);
            return maxDistance >= -BoundaryProjectionTolerance &&
                minDistance <= edgeLength + BoundaryProjectionTolerance;
        }

        private static float DistanceToLineXZ(Vector3 point, Vector3 lineStart, Vector3 lineDirection)
        {
            Vector3 offset = point - lineStart;
            offset.y = 0f;
            Vector3 projected = lineDirection * Vector3.Dot(offset, lineDirection);
            return (offset - projected).magnitude;
        }
    }
}
