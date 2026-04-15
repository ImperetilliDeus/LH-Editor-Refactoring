using System.Collections.Generic;
using UnityEngine;

public static class VirtualBoundaryUtility
{
    private const float DirectionDotThreshold = 0.995f;
    private const float Epsilon = 0.0001f;

    public static bool TryBuildRectangleOutlineFromRect(
        Vector3 startCorner,
        Vector3 endCorner,
        float minimumBoundaryLength,
        List<(Vector3 start, Vector3 end)> segments,
        out Bounds previewBounds)
    {
        float minX = Mathf.Min(startCorner.x, endCorner.x);
        float maxX = Mathf.Max(startCorner.x, endCorner.x);
        float minZ = Mathf.Min(startCorner.z, endCorner.z);
        float maxZ = Mathf.Max(startCorner.z, endCorner.z);
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        float y = startCorner.y;

        previewBounds = new Bounds(
            new Vector3(centerX, y, centerZ),
            new Vector3(Mathf.Max(0.01f, sizeX), 0.02f, Mathf.Max(0.01f, sizeZ)));

        if (segments != null)
        {
            segments.Clear();
        }

        if (sizeX < minimumBoundaryLength || sizeZ < minimumBoundaryLength)
        {
            return false;
        }

        if (segments != null)
        {
            Vector3 bottomLeft = new Vector3(minX, y, minZ);
            Vector3 bottomRight = new Vector3(maxX, y, minZ);
            Vector3 topRight = new Vector3(maxX, y, maxZ);
            Vector3 topLeft = new Vector3(minX, y, maxZ);

            segments.Add((bottomLeft, bottomRight));
            segments.Add((bottomRight, topRight));
            segments.Add((topRight, topLeft));
            segments.Add((topLeft, bottomLeft));
        }

        return true;
    }

}
