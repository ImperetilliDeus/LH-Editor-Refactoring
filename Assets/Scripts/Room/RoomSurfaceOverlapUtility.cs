using System.Collections.Generic;
using UnityEngine;

public static class RoomSurfaceOverlapUtility
{
    private const float Epsilon = 0.0001f;

    public static Dictionary<Room, List<List<Vector3>>> BuildNonOverlappingAxisAlignedRegions(IReadOnlyList<Room> rooms)
    {
        Dictionary<Room, List<List<Vector3>>> results = new Dictionary<Room, List<List<Vector3>>>();
        if (rooms == null || rooms.Count == 0)
        {
            return results;
        }

        List<RoomRect> rects = new List<RoomRect>();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null || !TryGetAxisAlignedRoomRect(room, i, out RoomRect rect))
            {
                continue;
            }

            rects.Add(rect);
        }

        for (int i = 0; i < rects.Count; i++)
        {
            RoomRect target = rects[i];
            List<RoomRect> remaining = new List<RoomRect> { target };

            for (int j = 0; j < rects.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                RoomRect blocker = rects[j];
                if (!ShouldBlock(target, blocker))
                {
                    continue;
                }

                SubtractRect(remaining, blocker);
                if (remaining.Count == 0)
                {
                    break;
                }
            }

            if (remaining.Count == 1 && AreSameRect(remaining[0], target))
            {
                continue;
            }

            List<List<Vector3>> roomRegions = new List<List<Vector3>>(remaining.Count);
            for (int j = 0; j < remaining.Count; j++)
            {
                roomRegions.Add(BuildPolygon(remaining[j], target.Y));
            }

            results[target.Room] = roomRegions;
        }

        return results;
    }

    private static bool TryGetAxisAlignedRoomRect(Room room, int index, out RoomRect rect)
    {
        rect = default;
        List<Vector3> vertices = new List<Vector3>();
        if (room == null || !room.TryGetOrderedVertices(vertices))
        {
            return false;
        }

        if (!RoomCreateGeometryService.TryGetAxisAlignedRoomBounds(vertices, 0f, 0f, out Bounds bounds))
        {
            return false;
        }

        rect = new RoomRect
        {
            Room = room,
            Index = index,
            MinX = bounds.min.x,
            MaxX = bounds.max.x,
            MinZ = bounds.min.z,
            MaxZ = bounds.max.z,
            Y = vertices[0].y,
            Area = Mathf.Abs(RoomCreateGeometryService.CalculateSignedAreaXZ(vertices)),
        };
        return rect.Width > Epsilon && rect.Height > Epsilon;
    }

    private static bool ShouldBlock(RoomRect target, RoomRect blocker)
    {
        if (!Overlaps(target, blocker))
        {
            return false;
        }

        if (blocker.Area < target.Area - Epsilon)
        {
            return true;
        }

        if (Mathf.Abs(blocker.Area - target.Area) <= Epsilon)
        {
            return blocker.Index > target.Index;
        }

        return false;
    }

    private static void SubtractRect(List<RoomRect> regions, RoomRect blocker)
    {
        for (int i = regions.Count - 1; i >= 0; i--)
        {
            RoomRect region = regions[i];
            if (!TryGetIntersection(region, blocker, out RoomRect overlap))
            {
                continue;
            }

            regions.RemoveAt(i);
            AddIfValid(regions, region.MinX, region.MaxX, region.MinZ, overlap.MinZ, region);
            AddIfValid(regions, region.MinX, region.MaxX, overlap.MaxZ, region.MaxZ, region);
            AddIfValid(regions, region.MinX, overlap.MinX, overlap.MinZ, overlap.MaxZ, region);
            AddIfValid(regions, overlap.MaxX, region.MaxX, overlap.MinZ, overlap.MaxZ, region);
        }
    }

    private static void AddIfValid(List<RoomRect> regions, float minX, float maxX, float minZ, float maxZ, RoomRect source)
    {
        if (maxX - minX <= Epsilon || maxZ - minZ <= Epsilon)
        {
            return;
        }

        regions.Add(new RoomRect
        {
            Room = source.Room,
            Index = source.Index,
            MinX = minX,
            MaxX = maxX,
            MinZ = minZ,
            MaxZ = maxZ,
            Y = source.Y,
            Area = (maxX - minX) * (maxZ - minZ),
        });
    }

    private static bool Overlaps(RoomRect left, RoomRect right)
    {
        return left.MinX < right.MaxX - Epsilon &&
               left.MaxX > right.MinX + Epsilon &&
               left.MinZ < right.MaxZ - Epsilon &&
               left.MaxZ > right.MinZ + Epsilon;
    }

    private static bool TryGetIntersection(RoomRect left, RoomRect right, out RoomRect overlap)
    {
        overlap = default;
        if (!Overlaps(left, right))
        {
            return false;
        }

        overlap = new RoomRect
        {
            Room = left.Room,
            Index = left.Index,
            MinX = Mathf.Max(left.MinX, right.MinX),
            MaxX = Mathf.Min(left.MaxX, right.MaxX),
            MinZ = Mathf.Max(left.MinZ, right.MinZ),
            MaxZ = Mathf.Min(left.MaxZ, right.MaxZ),
            Y = left.Y,
        };
        overlap.Area = overlap.Width * overlap.Height;
        return overlap.Width > Epsilon && overlap.Height > Epsilon;
    }

    private static bool AreSameRect(RoomRect left, RoomRect right)
    {
        return Mathf.Abs(left.MinX - right.MinX) <= Epsilon &&
               Mathf.Abs(left.MaxX - right.MaxX) <= Epsilon &&
               Mathf.Abs(left.MinZ - right.MinZ) <= Epsilon &&
               Mathf.Abs(left.MaxZ - right.MaxZ) <= Epsilon;
    }

    private static List<Vector3> BuildPolygon(RoomRect rect, float y)
    {
        return new List<Vector3>
        {
            new Vector3(rect.MinX, y, rect.MinZ),
            new Vector3(rect.MaxX, y, rect.MinZ),
            new Vector3(rect.MaxX, y, rect.MaxZ),
            new Vector3(rect.MinX, y, rect.MaxZ),
        };
    }

    private struct RoomRect
    {
        public Room Room;
        public int Index;
        public float MinX;
        public float MaxX;
        public float MinZ;
        public float MaxZ;
        public float Y;
        public float Area;
        public float Width => MaxX - MinX;
        public float Height => MaxZ - MinZ;
    }
}
