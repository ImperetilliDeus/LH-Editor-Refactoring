using UnityEngine;

public static class BoundsXZ
{
    public static bool ContainsPoint(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x &&
               point.x <= bounds.max.x &&
               point.z >= bounds.min.z &&
               point.z <= bounds.max.z;
    }

    // Center first, then the four XZ corners at the bounds' center height.
    public static Vector3[] GetFootprintPoints(Bounds bounds)
    {
        float y = bounds.center.y;
        return new[]
        {
            new Vector3(bounds.center.x, y, bounds.center.z),
            new Vector3(bounds.min.x, y, bounds.min.z),
            new Vector3(bounds.min.x, y, bounds.max.z),
            new Vector3(bounds.max.x, y, bounds.min.z),
            new Vector3(bounds.max.x, y, bounds.max.z),
        };
    }
}
