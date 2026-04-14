using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TopPlanPolygonBatchGraphic : MaskableGraphic
{
    public sealed class PolygonData
    {
        public readonly List<Vector2> points = new List<Vector2>();
        public Color color;

        public PolygonData()
        {
        }

        public PolygonData(IReadOnlyList<Vector2> sourcePoints, Color sourceColor)
        {
            color = sourceColor;
            if (sourcePoints == null)
            {
                return;
            }

            for (int i = 0; i < sourcePoints.Count; i++)
            {
                points.Add(sourcePoints[i]);
            }
        }
    }

    private const float PolygonEpsilon = 0.0001f;
    private const float PolygonEpsilonSqr = 0.00000001f;

    private readonly List<PolygonData> polygons = new List<PolygonData>();
    private readonly List<Vector2> triangulationVertices = new List<Vector2>();
    private readonly List<int> polygonIndices = new List<int>();
    private readonly List<int> triangulationIndices = new List<int>();

    public void SetPolygons(IReadOnlyList<PolygonData> values)
    {
        polygons.Clear();
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                PolygonData source = values[i];
                if (source == null)
                {
                    continue;
                }

                PolygonData copy = new PolygonData();
                copy.color = source.color;
                copy.points.AddRange(source.points);
                polygons.Add(copy);
            }
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int vertexOffset = 0;

        for (int polygonIndex = 0; polygonIndex < polygons.Count; polygonIndex++)
        {
            PolygonData polygon = polygons[polygonIndex];
            if (polygon == null || polygon.points.Count < 3)
            {
                continue;
            }

            triangulationVertices.Clear();
            triangulationVertices.AddRange(polygon.points);
            SanitizePolygonVertices(triangulationVertices);
            if (triangulationVertices.Count < 3)
            {
                continue;
            }

            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = polygon.color;

            for (int i = 0; i < triangulationVertices.Count; i++)
            {
                vertex.position = triangulationVertices[i];
                vh.AddVert(vertex);
            }

            triangulationIndices.Clear();
            if (!TryTriangulatePolygon(triangulationVertices, triangulationIndices))
            {
                for (int i = 1; i < triangulationVertices.Count - 1; i++)
                {
                    vh.AddTriangle(vertexOffset, vertexOffset + i, vertexOffset + i + 1);
                }
            }
            else
            {
                for (int i = 0; i < triangulationIndices.Count; i += 3)
                {
                    vh.AddTriangle(
                        vertexOffset + triangulationIndices[i],
                        vertexOffset + triangulationIndices[i + 1],
                        vertexOffset + triangulationIndices[i + 2]);
                }
            }

            vertexOffset += triangulationVertices.Count;
        }
    }

    private static void SanitizePolygonVertices(List<Vector2> points)
    {
        if (points == null || points.Count < 3)
        {
            return;
        }

        RemoveSequentialDuplicateVertices(points);
        RemoveCollinearVertices(points);
        RemoveSequentialDuplicateVertices(points);
        EnsureCounterClockwiseWinding(points);
    }

    private static void RemoveSequentialDuplicateVertices(List<Vector2> points)
    {
        if (points == null || points.Count < 2)
        {
            return;
        }

        bool removedAny = true;
        while (removedAny && points.Count >= 2)
        {
            removedAny = false;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];
                if ((current - next).sqrMagnitude > PolygonEpsilonSqr)
                {
                    continue;
                }

                points.RemoveAt(i);
                removedAny = true;
            }
        }
    }

    private static void RemoveCollinearVertices(List<Vector2> points)
    {
        if (points == null || points.Count < 3)
        {
            return;
        }

        bool removedAny = true;
        while (removedAny && points.Count >= 3)
        {
            removedAny = false;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                Vector2 previous = points[(i - 1 + points.Count) % points.Count];
                Vector2 current = points[i];
                Vector2 next = points[(i + 1) % points.Count];

                Vector2 a = current - previous;
                Vector2 b = next - current;
                if (a.sqrMagnitude <= PolygonEpsilonSqr || b.sqrMagnitude <= PolygonEpsilonSqr)
                {
                    points.RemoveAt(i);
                    removedAny = true;
                    continue;
                }

                float cross = a.x * b.y - a.y * b.x;
                if (Mathf.Abs(cross) > PolygonEpsilon)
                {
                    continue;
                }

                points.RemoveAt(i);
                removedAny = true;
            }
        }
    }

    private static void EnsureCounterClockwiseWinding(List<Vector2> points)
    {
        if (points == null || points.Count < 3)
        {
            return;
        }

        float signedArea2 = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[(i + 1) % points.Count];
            signedArea2 += (a.x * b.y) - (b.x * a.y);
        }

        if (signedArea2 < 0f)
        {
            points.Reverse();
        }
    }

    private bool TryTriangulatePolygon(List<Vector2> points, List<int> triangles)
    {
        if (points == null || triangles == null)
        {
            return false;
        }

        SanitizePolygonVertices(points);
        if (points.Count < 3)
        {
            return false;
        }

        triangles.Clear();
        polygonIndices.Clear();
        for (int i = 0; i < points.Count; i++)
        {
            polygonIndices.Add(i);
        }

        int safetyLimit = points.Count * points.Count;
        while (polygonIndices.Count > 3 && safetyLimit-- > 0)
        {
            bool clippedEar = false;
            for (int i = 0; i < polygonIndices.Count; i++)
            {
                int previousIndex = polygonIndices[(i - 1 + polygonIndices.Count) % polygonIndices.Count];
                int currentIndex = polygonIndices[i];
                int nextIndex = polygonIndices[(i + 1) % polygonIndices.Count];

                Vector2 previous = points[previousIndex];
                Vector2 current = points[currentIndex];
                Vector2 next = points[nextIndex];

                if (!IsConvexCorner(previous, current, next))
                {
                    continue;
                }

                if (IsDiagonalIntersectingPolygon(points, polygonIndices, previousIndex, nextIndex))
                {
                    continue;
                }

                if (ContainsAnyVertexInTriangle(points, polygonIndices, previousIndex, currentIndex, nextIndex))
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

    private static bool IsConvexCorner(Vector2 previous, Vector2 current, Vector2 next)
    {
        Vector2 a = current - previous;
        Vector2 b = next - current;
        float cross = a.x * b.y - a.y * b.x;
        return cross > PolygonEpsilon;
    }

    private static bool IsDiagonalIntersectingPolygon(List<Vector2> points, List<int> indices, int aIndex, int bIndex)
    {
        Vector2 diagonalStart = points[aIndex];
        Vector2 diagonalEnd = points[bIndex];

        for (int i = 0; i < indices.Count; i++)
        {
            int edgeStartIndex = indices[i];
            int edgeEndIndex = indices[(i + 1) % indices.Count];
            if (edgeStartIndex == aIndex || edgeStartIndex == bIndex || edgeEndIndex == aIndex || edgeEndIndex == bIndex)
            {
                continue;
            }

            if (SegmentsIntersect(diagonalStart, diagonalEnd, points[edgeStartIndex], points[edgeEndIndex]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAnyVertexInTriangle(List<Vector2> points, List<int> indices, int aIndex, int bIndex, int cIndex)
    {
        for (int i = 0; i < indices.Count; i++)
        {
            int pointIndex = indices[i];
            if (pointIndex == aIndex || pointIndex == bIndex || pointIndex == cIndex)
            {
                continue;
            }

            if (IsPointStrictlyInTriangle(points[pointIndex], points[aIndex], points[bIndex], points[cIndex]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointStrictlyInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float area1 = Cross(point, a, b);
        float area2 = Cross(point, b, c);
        float area3 = Cross(point, c, a);

        if (Mathf.Abs(area1) <= PolygonEpsilon || Mathf.Abs(area2) <= PolygonEpsilon || Mathf.Abs(area3) <= PolygonEpsilon)
        {
            return false;
        }

        bool hasNegative = area1 < -PolygonEpsilon || area2 < -PolygonEpsilon || area3 < -PolygonEpsilon;
        bool hasPositive = area1 > PolygonEpsilon || area2 > PolygonEpsilon || area3 > PolygonEpsilon;
        return !(hasNegative && hasPositive);
    }

    private static float Cross(Vector2 origin, Vector2 left, Vector2 right)
    {
        Vector2 a = left - origin;
        Vector2 b = right - origin;
        return a.x * b.y - a.y * b.x;
    }

    private static bool SegmentsIntersect(Vector2 aStart, Vector2 aEnd, Vector2 bStart, Vector2 bEnd)
    {
        float o1 = Cross(aStart, aEnd, bStart);
        float o2 = Cross(aStart, aEnd, bEnd);
        float o3 = Cross(bStart, bEnd, aStart);
        float o4 = Cross(bStart, bEnd, aEnd);

        if (o1 * o2 < -PolygonEpsilon && o3 * o4 < -PolygonEpsilon)
        {
            return true;
        }

        return Mathf.Abs(o1) <= PolygonEpsilon && IsPointOnSegment(aStart, bStart, aEnd) ||
               Mathf.Abs(o2) <= PolygonEpsilon && IsPointOnSegment(aStart, bEnd, aEnd) ||
               Mathf.Abs(o3) <= PolygonEpsilon && IsPointOnSegment(bStart, aStart, bEnd) ||
               Mathf.Abs(o4) <= PolygonEpsilon && IsPointOnSegment(bStart, aEnd, bEnd);
    }

    private static bool IsPointOnSegment(Vector2 segmentStart, Vector2 point, Vector2 segmentEnd)
    {
        return point.x >= Mathf.Min(segmentStart.x, segmentEnd.x) - PolygonEpsilon &&
               point.x <= Mathf.Max(segmentStart.x, segmentEnd.x) + PolygonEpsilon &&
               point.y >= Mathf.Min(segmentStart.y, segmentEnd.y) - PolygonEpsilon &&
               point.y <= Mathf.Max(segmentStart.y, segmentEnd.y) + PolygonEpsilon;
    }
}
