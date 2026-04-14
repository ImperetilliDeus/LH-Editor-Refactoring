using System.Collections.Generic;
using UnityEngine;

public struct RoomGeometry
{
    public Vector3 Center;
    public float Area;
    public int WallCount;
}

public static class PolygonUtility
{
    private const float PolygonEpsilon = 0.0001f;
    private const float PolygonEpsilonSqr = 0.00000001f;

    public static List<Vector3> CreateSanitizedPolygonCopy(IReadOnlyList<Vector3> source)
    {
        List<Vector3> results = new List<Vector3>();
        CopySanitizedVertices(source, results);
        return results;
    }

    public static void CopySanitizedVertices(IReadOnlyList<Vector3> source, List<Vector3> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i]);
        }

        SanitizePolygonVertices(destination);
    }

    public static void SanitizePolygonVertices(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return;
        }

        RemoveSequentialDuplicateVertices(vertices);
        RemoveCollinearVertices(vertices);
        RemoveSequentialDuplicateVertices(vertices);
        EnsureCounterClockwiseWinding(vertices);
    }

    public static bool TryTriangulatePolygon(List<Vector3> vertices, List<int> triangles, List<int> polygonIndices)
    {
        if (vertices == null || triangles == null || polygonIndices == null)
        {
            return false;
        }

        SanitizePolygonVertices(vertices);
        if (vertices.Count < 3)
        {
            return false;
        }

        triangles.Clear();
        polygonIndices.Clear();
        for (int i = 0; i < vertices.Count; i++)
        {
            polygonIndices.Add(i);
        }

        int safetyLimit = vertices.Count * vertices.Count;
        while (polygonIndices.Count > 3 && safetyLimit-- > 0)
        {
            bool clippedEar = false;
            for (int i = 0; i < polygonIndices.Count; i++)
            {
                int previousIndex = polygonIndices[(i - 1 + polygonIndices.Count) % polygonIndices.Count];
                int currentIndex = polygonIndices[i];
                int nextIndex = polygonIndices[(i + 1) % polygonIndices.Count];

                Vector3 previous = vertices[previousIndex];
                Vector3 current = vertices[currentIndex];
                Vector3 next = vertices[nextIndex];

                if (!IsConvexCorner(previous, current, next))
                {
                    continue;
                }

                if (IsDiagonalIntersectingPolygon(vertices, polygonIndices, previousIndex, nextIndex))
                {
                    continue;
                }

                if (ContainsAnyVertexInTriangle(vertices, polygonIndices, previousIndex, currentIndex, nextIndex))
                {
                    continue;
                }

                triangles.Add(previousIndex);
                triangles.Add(currentIndex);
                triangles.Add(nextIndex);
                polygonIndices.RemoveAt(i);
                clippedEar = true;
                break;
            }

            if (!clippedEar)
            {
                triangles.Clear();
                return false;
            }
        }

        if (polygonIndices.Count != 3)
        {
            triangles.Clear();
            return false;
        }

        triangles.Add(polygonIndices[0]);
        triangles.Add(polygonIndices[1]);
        triangles.Add(polygonIndices[2]);
        return true;
    }

    public static RoomGeometry CalculateGeometry(IReadOnlyList<Vector3> vertices)
    {
        if (vertices == null || vertices.Count == 0)
        {
            return new RoomGeometry();
        }

        float signedAreaTwice = 0f;
        float centroidX = 0f;
        float centroidZ = 0f;

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];
            float cross = current.x * next.z - next.x * current.z;
            signedAreaTwice += cross;
            centroidX += (current.x + next.x) * cross;
            centroidZ += (current.z + next.z) * cross;
        }

        float area = Mathf.Abs(signedAreaTwice) * 0.5f;
        Vector3 center;
        if (Mathf.Abs(signedAreaTwice) <= 0.000001f)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                sum += vertices[i];
            }

            center = sum / vertices.Count;
        }
        else
        {
            float factor = 1f / (3f * signedAreaTwice);
            center = new Vector3(centroidX * factor, vertices[0].y, centroidZ * factor);
        }

        return new RoomGeometry
        {
            Center = center,
            Area = area,
            WallCount = vertices.Count,
        };
    }

    public static bool ArePolygonsEquivalent(IReadOnlyList<Vector3> left, IReadOnlyList<Vector3> right, float epsilon = 0.01f)
    {
        if (left == null || right == null || left.Count != right.Count)
        {
            return false;
        }

        if (left.Count == 0)
        {
            return true;
        }

        float epsilonSqr = epsilon * epsilon;
        for (int offset = 0; offset < right.Count; offset++)
        {
            bool matchesForward = true;
            bool matchesReverse = true;
            for (int i = 0; i < left.Count; i++)
            {
                if ((left[i] - right[(offset + i) % right.Count]).sqrMagnitude > epsilonSqr)
                {
                    matchesForward = false;
                }

                int reverseIndex = offset - i;
                if (reverseIndex < 0)
                {
                    reverseIndex += right.Count;
                }

                if ((left[i] - right[reverseIndex]).sqrMagnitude > epsilonSqr)
                {
                    matchesReverse = false;
                }

                if (!matchesForward && !matchesReverse)
                {
                    break;
                }
            }

            if (matchesForward || matchesReverse)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureCounterClockwiseWinding(List<Vector3> vertices)
    {
        float signedArea2 = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 a = vertices[i];
            Vector3 b = vertices[(i + 1) % vertices.Count];
            signedArea2 += (a.x * b.z) - (b.x * a.z);
        }

        if (signedArea2 < 0f)
        {
            vertices.Reverse();
        }
    }

    private static void RemoveSequentialDuplicateVertices(List<Vector3> vertices)
    {
        bool removedAny = true;
        while (removedAny && vertices.Count >= 2)
        {
            removedAny = false;
            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                Vector3 current = vertices[i];
                Vector3 next = vertices[(i + 1) % vertices.Count];
                if ((current - next).sqrMagnitude > PolygonEpsilonSqr)
                {
                    continue;
                }

                vertices.RemoveAt(i);
                removedAny = true;
            }
        }
    }

    private static void RemoveCollinearVertices(List<Vector3> vertices)
    {
        bool removedAny = true;
        while (removedAny && vertices.Count >= 3)
        {
            removedAny = false;
            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                Vector3 previous = vertices[(i - 1 + vertices.Count) % vertices.Count];
                Vector3 current = vertices[i];
                Vector3 next = vertices[(i + 1) % vertices.Count];

                Vector2 a = new Vector2(current.x - previous.x, current.z - previous.z);
                Vector2 b = new Vector2(next.x - current.x, next.z - current.z);
                if (a.sqrMagnitude <= PolygonEpsilonSqr || b.sqrMagnitude <= PolygonEpsilonSqr)
                {
                    vertices.RemoveAt(i);
                    removedAny = true;
                    continue;
                }

                float cross = a.x * b.y - a.y * b.x;
                if (Mathf.Abs(cross) > PolygonEpsilon)
                {
                    continue;
                }

                vertices.RemoveAt(i);
                removedAny = true;
            }
        }
    }

    private static bool IsConvexCorner(Vector3 previous, Vector3 current, Vector3 next)
    {
        Vector2 a = new Vector2(current.x - previous.x, current.z - previous.z);
        Vector2 b = new Vector2(next.x - current.x, next.z - current.z);
        float cross = a.x * b.y - a.y * b.x;
        return cross > PolygonEpsilon;
    }

    private static bool IsDiagonalIntersectingPolygon(List<Vector3> vertices, List<int> polygonIndices, int aIndex, int bIndex)
    {
        Vector3 diagonalStart = vertices[aIndex];
        Vector3 diagonalEnd = vertices[bIndex];

        for (int i = 0; i < polygonIndices.Count; i++)
        {
            int edgeStartIndex = polygonIndices[i];
            int edgeEndIndex = polygonIndices[(i + 1) % polygonIndices.Count];
            if (edgeStartIndex == aIndex || edgeStartIndex == bIndex || edgeEndIndex == aIndex || edgeEndIndex == bIndex)
            {
                continue;
            }

            if (SegmentsIntersectXZ(diagonalStart, diagonalEnd, vertices[edgeStartIndex], vertices[edgeEndIndex]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAnyVertexInTriangle(List<Vector3> vertices, List<int> polygonIndices, int aIndex, int bIndex, int cIndex)
    {
        for (int i = 0; i < polygonIndices.Count; i++)
        {
            int pointIndex = polygonIndices[i];
            if (pointIndex == aIndex || pointIndex == bIndex || pointIndex == cIndex)
            {
                continue;
            }

            if (IsPointStrictlyInsideTriangleXZ(vertices[pointIndex], vertices[aIndex], vertices[bIndex], vertices[cIndex]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointStrictlyInsideTriangleXZ(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        float area = CrossXZ(a, b, c);
        float area1 = CrossXZ(point, a, b);
        float area2 = CrossXZ(point, b, c);
        float area3 = CrossXZ(point, c, a);

        if (Mathf.Abs(area) <= PolygonEpsilon)
        {
            return false;
        }

        if (Mathf.Abs(area1) <= PolygonEpsilon || Mathf.Abs(area2) <= PolygonEpsilon || Mathf.Abs(area3) <= PolygonEpsilon)
        {
            return false;
        }

        bool hasNegative = area1 < -PolygonEpsilon || area2 < -PolygonEpsilon || area3 < -PolygonEpsilon;
        bool hasPositive = area1 > PolygonEpsilon || area2 > PolygonEpsilon || area3 > PolygonEpsilon;
        return !(hasNegative && hasPositive);
    }

    private static float CrossXZ(Vector3 origin, Vector3 left, Vector3 right)
    {
        Vector2 a = new Vector2(left.x - origin.x, left.z - origin.z);
        Vector2 b = new Vector2(right.x - origin.x, right.z - origin.z);
        return a.x * b.y - a.y * b.x;
    }

    private static bool SegmentsIntersectXZ(Vector3 aStart, Vector3 aEnd, Vector3 bStart, Vector3 bEnd)
    {
        float o1 = CrossXZ(aStart, aEnd, bStart);
        float o2 = CrossXZ(aStart, aEnd, bEnd);
        float o3 = CrossXZ(bStart, bEnd, aStart);
        float o4 = CrossXZ(bStart, bEnd, aEnd);

        if (o1 * o2 < -PolygonEpsilon && o3 * o4 < -PolygonEpsilon)
        {
            return true;
        }

        return Mathf.Abs(o1) <= PolygonEpsilon && IsPointOnSegmentXZ(aStart, bStart, aEnd) ||
               Mathf.Abs(o2) <= PolygonEpsilon && IsPointOnSegmentXZ(aStart, bEnd, aEnd) ||
               Mathf.Abs(o3) <= PolygonEpsilon && IsPointOnSegmentXZ(bStart, aStart, bEnd) ||
               Mathf.Abs(o4) <= PolygonEpsilon && IsPointOnSegmentXZ(bStart, aEnd, bEnd);
    }

    private static bool IsPointOnSegmentXZ(Vector3 segmentStart, Vector3 point, Vector3 segmentEnd)
    {
        return point.x >= Mathf.Min(segmentStart.x, segmentEnd.x) - PolygonEpsilon &&
               point.x <= Mathf.Max(segmentStart.x, segmentEnd.x) + PolygonEpsilon &&
               point.z >= Mathf.Min(segmentStart.z, segmentEnd.z) - PolygonEpsilon &&
               point.z <= Mathf.Max(segmentStart.z, segmentEnd.z) + PolygonEpsilon;
    }
}
