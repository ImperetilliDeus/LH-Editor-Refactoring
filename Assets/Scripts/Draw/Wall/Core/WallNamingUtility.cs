using System.Collections.Generic;
using UnityEngine;

public static class WallNamingUtility
{
    private const string WallNamePrefix = "wall";
    private static readonly List<Wall> CachedWalls = new List<Wall>();

    public static void NormalizeWallNames(Transform wallRoot)
    {
        if (wallRoot == null)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, CachedWalls, true);

        int renamableCount = 0;
        for (int i = 0; i < CachedWalls.Count; i++)
        {
            if (ShouldRename(CachedWalls[i]))
            {
                renamableCount++;
            }
        }

        if (renamableCount == 0)
        {
            return;
        }

        int digits = Mathf.Max(2, renamableCount.ToString().Length);
        int sequence = 1;
        for (int i = 0; i < CachedWalls.Count; i++)
        {
            Wall wall = CachedWalls[i];
            if (!ShouldRename(wall))
            {
                continue;
            }

            wall.name = $"{WallNamePrefix}{sequence.ToString().PadLeft(digits, '0')}";
            sequence++;
        }
    }

    private static bool ShouldRename(Wall wall)
    {
        if (wall == null)
        {
            return false;
        }

        return wall.GetComponent<Collider>() != null;
    }
}
