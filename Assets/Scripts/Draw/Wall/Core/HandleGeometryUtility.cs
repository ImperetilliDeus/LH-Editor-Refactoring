using UnityEngine;

internal static class HandleGeometryUtility
{
    public static bool HasValidWallHandleGeometry(Wall wall, float minimumWallLength)
    {
        if (wall == null || wall.Data == null)
        {
            return false;
        }

        Vector3 startPoint = wall.Data.startPoint;
        Vector3 endPoint = wall.Data.endPoint;
        if (!IsValidWorldPoint(startPoint) || !IsValidWorldPoint(endPoint))
        {
            return false;
        }

        Vector3 delta = endPoint - startPoint;
        delta.y = 0f;
        return delta.sqrMagnitude >= minimumWallLength * minimumWallLength;
    }

    public static bool IsValidWorldPoint(Vector3 point)
    {
        return IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z);
    }

    public static float DistanceSqrXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
