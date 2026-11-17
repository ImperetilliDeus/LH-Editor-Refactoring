using System;
using System.Collections.Generic;
using UnityEngine;

public static class PolygonXZ
{
    public static bool ContainsPoint(Vector3 point, IReadOnlyList<Vector3> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        bool inside = false;
        float x = point.x;
        float z = point.z;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector3 pi = polygon[i];
            Vector3 pj = polygon[j];
            float deltaZ = pj.z - pi.z;
            bool intersects = Mathf.Abs(deltaZ) > GeometryTolerance.PolygonEdgeDeltaEpsilon &&
                              ((pi.z > z) != (pj.z > z)) &&
                              (x < (pj.x - pi.x) * (z - pi.z) / deltaZ + pi.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public static bool ContainsBoundsFootprint(Bounds bounds, IReadOnlyList<Vector3> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        Vector3[] testPoints = BoundsXZ.GetFootprintPoints(bounds);
        for (int i = 0; i < testPoints.Length; i++)
        {
            if (!ContainsPoint(testPoints[i], polygon))
            {
                return false;
            }
        }

        return true;
    }

    // Positive for counter-clockwise winding when viewed with +X right and +Z up.
    public static float SignedArea(IReadOnlyList<Vector3> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 current = polygon[i];
            Vector3 next = polygon[(i + 1) % polygon.Count];
            area += current.x * next.z - next.x * current.z;
        }

        return area * 0.5f;
    }

    // The outline is treated as closed. Duplicates are measured in 3D, as the callers' vertices share a height.
    public static void RemoveSequentialDuplicateVertices(List<Vector3> vertices)
    {
        RemoveSequentialDuplicates(vertices, (a, b) => (a - b).sqrMagnitude);
    }

    public static void RemoveSequentialDuplicateVertices(List<Vector2> points)
    {
        RemoveSequentialDuplicates(points, (a, b) => (a - b).sqrMagnitude);
    }

    // Removes vertices that sit on a straight XZ line or next to a degenerate edge.
    public static void RemoveCollinearVertices(List<Vector3> vertices)
    {
        RemoveCollinear(vertices, vertex => new Vector2(vertex.x, vertex.z));
    }

    // Vector2 points are read as (x, z).
    public static void RemoveCollinearVertices(List<Vector2> points)
    {
        RemoveCollinear(points, point => point);
    }

    private static void RemoveSequentialDuplicates<T>(List<T> vertices, Func<T, T, float> sqrDistance)
    {
        if (vertices == null)
        {
            return;
        }

        bool removedAny = true;
        while (removedAny && vertices.Count >= 2)
        {
            removedAny = false;
            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                T current = vertices[i];
                T next = vertices[(i + 1) % vertices.Count];
                if (sqrDistance(current, next) > GeometryTolerance.PolygonVertexEpsilonSqr)
                {
                    continue;
                }

                vertices.RemoveAt(i);
                removedAny = true;
            }
        }
    }

    private static void RemoveCollinear<T>(List<T> vertices, Func<T, Vector2> toPlane)
    {
        if (vertices == null)
        {
            return;
        }

        bool removedAny = true;
        while (removedAny && vertices.Count >= 3)
        {
            removedAny = false;
            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                Vector2 previous = toPlane(vertices[(i - 1 + vertices.Count) % vertices.Count]);
                Vector2 current = toPlane(vertices[i]);
                Vector2 next = toPlane(vertices[(i + 1) % vertices.Count]);

                Vector2 a = current - previous;
                Vector2 b = next - current;
                if (a.sqrMagnitude <= GeometryTolerance.PolygonVertexEpsilonSqr || b.sqrMagnitude <= GeometryTolerance.PolygonVertexEpsilonSqr)
                {
                    vertices.RemoveAt(i);
                    removedAny = true;
                    continue;
                }

                float cross = a.x * b.y - a.y * b.x;
                if (Mathf.Abs(cross) > GeometryTolerance.PolygonVertexEpsilon)
                {
                    continue;
                }

                vertices.RemoveAt(i);
                removedAny = true;
            }
        }
    }
}
