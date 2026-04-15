using System.Collections.Generic;
using UnityEngine;

public static class RoomPolygonValidationUtility
{
    public static bool IsValidPolygon(IReadOnlyList<Vector3> vertices, float minimumEdgeLength = 0.1f, float minimumArea = 0.0001f)
    {
        return vertices != null &&
               vertices.Count >= 3 &&
               HasValidArea(vertices, minimumArea) &&
               HasMinimumEdgeLength(vertices, minimumEdgeLength) &&
               IsSimplePolygon(vertices);
    }

    public static bool HasMinimumEdgeLength(IReadOnlyList<Vector3> vertices, float minimumEdgeLength)
    {
        if (vertices == null || vertices.Count < 2)
        {
            return false;
        }

        float minimumEdgeLengthSqr = minimumEdgeLength * minimumEdgeLength;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];
            if ((next - current).sqrMagnitude < minimumEdgeLengthSqr)
            {
                return false;
            }
        }

        return true;
    }

    public static bool HasValidArea(IReadOnlyList<Vector3> vertices, float minimumArea)
    {
        return Mathf.Abs(CalculateSignedArea(vertices)) > minimumArea;
    }

    public static bool IsSimplePolygon(IReadOnlyList<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return false;
        }

        int edgeCount = vertices.Count;
        for (int firstEdgeIndex = 0; firstEdgeIndex < edgeCount; firstEdgeIndex++)
        {
            Vector3 firstStart = vertices[firstEdgeIndex];
            Vector3 firstEnd = vertices[(firstEdgeIndex + 1) % edgeCount];

            for (int secondEdgeIndex = firstEdgeIndex + 1; secondEdgeIndex < edgeCount; secondEdgeIndex++)
            {
                int firstNextIndex = (firstEdgeIndex + 1) % edgeCount;
                int secondNextIndex = (secondEdgeIndex + 1) % edgeCount;
                bool sharesVertex =
                    firstEdgeIndex == secondEdgeIndex ||
                    firstEdgeIndex == secondNextIndex ||
                    firstNextIndex == secondEdgeIndex ||
                    firstNextIndex == secondNextIndex;

                if (sharesVertex)
                {
                    continue;
                }

                Vector3 secondStart = vertices[secondEdgeIndex];
                Vector3 secondEnd = vertices[secondNextIndex];
                if (SegmentsIntersectXZ(firstStart, firstEnd, secondStart, secondEnd))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static float CalculateSignedArea(IReadOnlyList<Vector3> vertices)
    {
        float area = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];
            area += current.x * next.z - next.x * current.z;
        }

        return area * 0.5f;
    }

    private static bool SegmentsIntersectXZ(Vector3 firstStart, Vector3 firstEnd, Vector3 secondStart, Vector3 secondEnd)
    {
        Vector2 a = new Vector2(firstStart.x, firstStart.z);
        Vector2 b = new Vector2(firstEnd.x, firstEnd.z);
        Vector2 c = new Vector2(secondStart.x, secondStart.z);
        Vector2 d = new Vector2(secondEnd.x, secondEnd.z);

        float abWithC = Cross(a, b, c);
        float abWithD = Cross(a, b, d);
        float cdWithA = Cross(c, d, a);
        float cdWithB = Cross(c, d, b);

        if (Mathf.Approximately(abWithC, 0f) && OnSegment(a, b, c))
        {
            return true;
        }

        if (Mathf.Approximately(abWithD, 0f) && OnSegment(a, b, d))
        {
            return true;
        }

        if (Mathf.Approximately(cdWithA, 0f) && OnSegment(c, d, a))
        {
            return true;
        }

        if (Mathf.Approximately(cdWithB, 0f) && OnSegment(c, d, b))
        {
            return true;
        }

        return (abWithC > 0f) != (abWithD > 0f) &&
               (cdWithA > 0f) != (cdWithB > 0f);
    }

    private static float Cross(Vector2 origin, Vector2 target, Vector2 point)
    {
        return (target.x - origin.x) * (point.y - origin.y) -
               (target.y - origin.y) * (point.x - origin.x);
    }

    private static bool OnSegment(Vector2 start, Vector2 end, Vector2 point)
    {
        const float epsilon = 0.0001f;
        return point.x >= Mathf.Min(start.x, end.x) - epsilon &&
               point.x <= Mathf.Max(start.x, end.x) + epsilon &&
               point.y >= Mathf.Min(start.y, end.y) - epsilon &&
               point.y <= Mathf.Max(start.y, end.y) + epsilon;
    }
}
