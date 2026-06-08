using UnityEngine;

public static class PerspectiveWallOverlayGeometryUtility
{
    private const float BoundsEpsilon = 0.0001f;
    private const float MinimumHighlightSize = 0.01f;

    public static bool TryBuildCorners(WallData wallData, float boundsPadding, out Vector3[] corners)
    {
        corners = null;
        if (wallData == null)
        {
            return false;
        }

        Vector3 start = wallData.startPoint;
        Vector3 end = wallData.endPoint;
        Vector3 direction = end - start;
        direction.y = 0f;
        if (direction.sqrMagnitude <= BoundsEpsilon * BoundsEpsilon)
        {
            return false;
        }

        direction.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        float halfThickness = Mathf.Max(Mathf.Abs(wallData.thickness), MinimumHighlightSize) * 0.5f + boundsPadding;
        float halfHeight = Mathf.Max(Mathf.Abs(wallData.height), MinimumHighlightSize) * 0.5f + boundsPadding;
        float centerY = wallData.centerY;
        Vector3 startCenter = new Vector3(start.x, centerY, start.z);
        Vector3 endCenter = new Vector3(end.x, centerY, end.z);

        corners = new[]
        {
            startCenter - side * halfThickness + Vector3.down * halfHeight,
            startCenter + side * halfThickness + Vector3.down * halfHeight,
            endCenter + side * halfThickness + Vector3.down * halfHeight,
            endCenter - side * halfThickness + Vector3.down * halfHeight,
            startCenter - side * halfThickness + Vector3.up * halfHeight,
            startCenter + side * halfThickness + Vector3.up * halfHeight,
            endCenter + side * halfThickness + Vector3.up * halfHeight,
            endCenter - side * halfThickness + Vector3.up * halfHeight,
        };
        return true;
    }
}
