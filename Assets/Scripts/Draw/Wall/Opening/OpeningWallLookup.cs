using System.Collections.Generic;
using UnityEngine;

internal static class OpeningWallLookup
{
    public static Wall FindMatchingStandaloneWall(
        Transform wallRoot,
        List<Wall> cachedWalls,
        UndoRedoManager.WallStateSnapshot snapshot)
    {
        if (wallRoot == null)
        {
            return null;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (wall.transform.parent != wallRoot)
            {
                continue;
            }

            if (IsMatchingStandaloneWall(wall, snapshot))
            {
                return wall;
            }
        }

        return null;
    }

    public static Wall FindClosestVisibleWallToPoint(
        Transform wallRoot,
        List<Wall> cachedWalls,
        Vector3 point)
    {
        if (wallRoot == null)
        {
            return null;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls, true);
        Wall bestWall = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (!CanSelectReplacementWall(wall))
            {
                continue;
            }

            Vector3 midpoint = (wall.Data.startPoint + wall.Data.endPoint) * 0.5f;
            float distanceSqr = (midpoint - point).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestWall = wall;
        }

        return bestWall;
    }

    private static bool IsMatchingStandaloneWall(Wall wall, UndoRedoManager.WallStateSnapshot snapshot)
    {
        return wall.name == snapshot.name &&
            snapshot.wallData != null &&
            (wall.Data.startPoint - snapshot.wallData.startPoint).sqrMagnitude <= 0.0001f &&
            (wall.Data.endPoint - snapshot.wallData.endPoint).sqrMagnitude <= 0.0001f;
    }

    private static bool CanSelectReplacementWall(Wall wall)
    {
        return wall != null &&
            wall.gameObject.activeInHierarchy &&
            !WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall);
    }
}
