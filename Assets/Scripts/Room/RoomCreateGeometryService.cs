using System.Collections.Generic;
using UnityEngine;

public static class RoomCreateGeometryService
{
    private const float BoundsHeight = 0.01f;
    private const float Epsilon = 0.0001f;

    public static bool TryBuildRectanglePolygon(
        Vector3 startPoint,
        Vector3 endPoint,
        float minimumRoomWidth,
        float minimumRoomHeight,
        out List<Vector3> polygonVertices,
        out Bounds bounds)
    {
        polygonVertices = new List<Vector3>();
        float minX = Mathf.Min(startPoint.x, endPoint.x);
        float maxX = Mathf.Max(startPoint.x, endPoint.x);
        float minZ = Mathf.Min(startPoint.z, endPoint.z);
        float maxZ = Mathf.Max(startPoint.z, endPoint.z);
        float width = maxX - minX;
        float height = maxZ - minZ;
        float y = startPoint.y;

        bounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
            new Vector3(width, BoundsHeight, height));

        if (width < minimumRoomWidth || height < minimumRoomHeight)
        {
            return false;
        }

        polygonVertices.Add(new Vector3(minX, y, minZ));
        polygonVertices.Add(new Vector3(maxX, y, minZ));
        polygonVertices.Add(new Vector3(maxX, y, maxZ));
        polygonVertices.Add(new Vector3(minX, y, maxZ));
        return true;
    }

    public static bool TryGetAxisAlignedRoomBounds(
        IReadOnlyList<Vector3> vertices,
        float minimumRoomWidth,
        float minimumRoomHeight,
        out Bounds bounds)
    {
        bounds = default;
        if (vertices == null || vertices.Count != 4)
        {
            return false;
        }

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        float y = vertices[0].y;

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];
            bool horizontal = Mathf.Abs(current.z - next.z) <= Epsilon;
            bool vertical = Mathf.Abs(current.x - next.x) <= Epsilon;
            if (!horizontal && !vertical)
            {
                return false;
            }

            minX = Mathf.Min(minX, current.x);
            maxX = Mathf.Max(maxX, current.x);
            minZ = Mathf.Min(minZ, current.z);
            maxZ = Mathf.Max(maxZ, current.z);
        }

        bounds = new Bounds(
            new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
            new Vector3(maxX - minX, BoundsHeight, maxZ - minZ));
        return bounds.size.x >= minimumRoomWidth && bounds.size.z >= minimumRoomHeight;
    }

    public static bool BoundsContainBoundsXZ(Bounds outer, Bounds inner)
    {
        return inner.min.x >= outer.min.x - Epsilon &&
               inner.max.x <= outer.max.x + Epsilon &&
               inner.min.z >= outer.min.z - Epsilon &&
               inner.max.z <= outer.max.z + Epsilon;
    }

    public static bool AreBoundsNearlyEqual(Bounds left, Bounds right)
    {
        return Mathf.Abs(left.min.x - right.min.x) <= Epsilon &&
               Mathf.Abs(left.max.x - right.max.x) <= Epsilon &&
               Mathf.Abs(left.min.z - right.min.z) <= Epsilon &&
               Mathf.Abs(left.max.z - right.max.z) <= Epsilon;
    }

    public static List<Bounds> BuildSplitBounds(Bounds outerBounds, Bounds innerBounds, float minimumRoomWidth, float minimumRoomHeight)
    {
        List<Bounds> results = new List<Bounds>();
        TryAddSplitBounds(results, outerBounds.min.x, outerBounds.max.x, outerBounds.min.z, innerBounds.min.z, innerBounds.center.y, minimumRoomWidth, minimumRoomHeight);
        TryAddSplitBounds(results, outerBounds.min.x, outerBounds.max.x, innerBounds.max.z, outerBounds.max.z, innerBounds.center.y, minimumRoomWidth, minimumRoomHeight);
        TryAddSplitBounds(results, outerBounds.min.x, innerBounds.min.x, innerBounds.min.z, innerBounds.max.z, innerBounds.center.y, minimumRoomWidth, minimumRoomHeight);
        TryAddSplitBounds(results, innerBounds.max.x, outerBounds.max.x, innerBounds.min.z, innerBounds.max.z, innerBounds.center.y, minimumRoomWidth, minimumRoomHeight);
        TryAddSplitBounds(results, innerBounds.min.x, innerBounds.max.x, innerBounds.min.z, innerBounds.max.z, innerBounds.center.y, minimumRoomWidth, minimumRoomHeight);
        return results;
    }

    public static List<Vector3> BuildPolygonFromBounds(Bounds bounds, float y)
    {
        return new List<Vector3>
        {
            new Vector3(bounds.min.x, y, bounds.min.z),
            new Vector3(bounds.max.x, y, bounds.min.z),
            new Vector3(bounds.max.x, y, bounds.max.z),
            new Vector3(bounds.min.x, y, bounds.max.z),
        };
    }

    public static bool ContainsPointXZ(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x &&
               point.x <= bounds.max.x &&
               point.z >= bounds.min.z &&
               point.z <= bounds.max.z;
    }

    public static bool SegmentIntersectsBoundsXZ(Bounds bounds, Vector3 start, Vector3 end)
    {
        if (ContainsPointXZ(bounds, start) || ContainsPointXZ(bounds, end))
        {
            return true;
        }

        Vector2 a = new Vector2(start.x, start.z);
        Vector2 b = new Vector2(end.x, end.z);
        Vector2 rectMin = new Vector2(bounds.min.x, bounds.min.z);
        Vector2 rectMax = new Vector2(bounds.max.x, bounds.max.z);

        Vector2 topLeft = new Vector2(rectMin.x, rectMax.y);
        Vector2 topRight = rectMax;
        Vector2 bottomLeft = rectMin;
        Vector2 bottomRight = new Vector2(rectMax.x, rectMin.y);

        return SegmentsIntersect2D(a, b, bottomLeft, topLeft) ||
               SegmentsIntersect2D(a, b, topLeft, topRight) ||
               SegmentsIntersect2D(a, b, topRight, bottomRight) ||
               SegmentsIntersect2D(a, b, bottomRight, bottomLeft);
    }

    public static bool IsPointInsidePolygonXZ(Vector3 point, IReadOnlyList<Vector3> polygon)
    {
        bool inside = false;
        int count = polygon != null ? polygon.Count : 0;
        if (count < 3)
        {
            return false;
        }

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector3 pi = polygon[i];
            Vector3 pj = polygon[j];
            bool intersects = ((pi.z > point.z) != (pj.z > point.z)) &&
                              (point.x < (pj.x - pi.x) * (point.z - pi.z) / ((pj.z - pi.z) + 0.000001f) + pi.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static float CalculateSignedAreaXZ(IReadOnlyList<Vector3> polygon)
    {
        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 a = polygon[i];
            Vector3 b = polygon[(i + 1) % polygon.Count];
            area += (a.x * b.z) - (b.x * a.z);
        }

        return area * 0.5f;
    }

    private static void TryAddSplitBounds(
        List<Bounds> results,
        float minX,
        float maxX,
        float minZ,
        float maxZ,
        float y,
        float minimumRoomWidth,
        float minimumRoomHeight)
    {
        float width = maxX - minX;
        float height = maxZ - minZ;
        if (width < minimumRoomWidth || height < minimumRoomHeight)
        {
            return;
        }

        results.Add(new Bounds(
            new Vector3((minX + maxX) * 0.5f, y, (minZ + maxZ) * 0.5f),
            new Vector3(width, BoundsHeight, height)));
    }

    private static bool SegmentsIntersect2D(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        float o1 = Orientation(a1, a2, b1);
        float o2 = Orientation(a1, a2, b2);
        float o3 = Orientation(b1, b2, a1);
        float o4 = Orientation(b1, b2, a2);

        if (o1 * o2 < 0f && o3 * o4 < 0f)
        {
            return true;
        }

        return Mathf.Approximately(o1, 0f) && OnSegment(a1, b1, a2) ||
               Mathf.Approximately(o2, 0f) && OnSegment(a1, b2, a2) ||
               Mathf.Approximately(o3, 0f) && OnSegment(b1, a1, b2) ||
               Mathf.Approximately(o4, 0f) && OnSegment(b1, a2, b2);
    }

    private static float Orientation(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool OnSegment(Vector2 a, Vector2 point, Vector2 b)
    {
        return point.x >= Mathf.Min(a.x, b.x) - Epsilon &&
               point.x <= Mathf.Max(a.x, b.x) + Epsilon &&
               point.y >= Mathf.Min(a.y, b.y) - Epsilon &&
               point.y <= Mathf.Max(a.y, b.y) + Epsilon;
    }
}
