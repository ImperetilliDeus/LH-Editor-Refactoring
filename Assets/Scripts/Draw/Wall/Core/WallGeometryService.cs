using System.Collections.Generic;
using UnityEngine;

public static class WallGeometryService
{
    public static bool ApplyWallEndpoints(Wall wall, Vector3 startPoint, Vector3 endPoint, float minimumWallLength, WallLengthDisplay wallLengthDisplay, bool isPreview)
    {
        if (wall == null)
        {
            return false;
        }

        WallData wallData = wall.Data;
        wallData.startPoint = startPoint;
        wallData.endPoint = endPoint;
        SyncWallDataFromTransformProfile(wallData, wall.transform);
        return ApplyWallData(wall, wallData, minimumWallLength, wallLengthDisplay, isPreview);
    }

    public static bool ApplyWallData(Wall wall, WallData wallData, float minimumWallLength, WallLengthDisplay wallLengthDisplay, bool isPreview)
    {
        if (wall == null || wallData == null)
        {
            return false;
        }

        wall.CopyDataFrom(wallData);
        bool applied = wall.UpdateView(minimumWallLength);
        if (!applied)
        {
            wall.ClearLengthDisplay(wallLengthDisplay);
            return false;
        }

        wall.RefreshLengthDisplay(wallLengthDisplay, isPreview);
        return true;
    }

    public static void ApplyVertexMove(IEnumerable<Wall> walls, int vertexId, Vector3 newPoint, float planeY, float minimumWallLength, WallLengthDisplay wallLengthDisplay)
    {
        if (walls == null)
        {
            return;
        }

        newPoint.y = planeY;
        foreach (Wall wall in walls)
        {
            if (wall == null)
            {
                continue;
            }

            Vector3 startPoint = wall.Data.startPoint;
            Vector3 endPoint = wall.Data.endPoint;

            if (wall.StartVertexId == vertexId)
            {
                startPoint = newPoint;
            }

            if (wall.EndVertexId == vertexId)
            {
                endPoint = newPoint;
            }

            ApplyWallEndpoints(wall, startPoint, endPoint, minimumWallLength, wallLengthDisplay, false);
        }
    }

    private static void SyncWallDataFromTransformProfile(WallData wallData, Transform wallTransform)
    {
        if (wallData == null || wallTransform == null)
        {
            return;
        }

        wallData.thickness = wallTransform.localScale.x;
        wallData.height = wallTransform.localScale.y;
        wallData.centerY = wallTransform.position.y;
    }
}
