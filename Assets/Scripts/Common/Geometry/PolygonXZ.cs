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
}
