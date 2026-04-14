using System.Collections.Generic;
using UnityEngine;

public static class WallRegistry
{
    private static readonly HashSet<Wall> activeWalls = new HashSet<Wall>();

    public static void Register(Wall wall)
    {
        if (wall == null)
        {
            return;
        }

        activeWalls.Add(wall);
    }

    public static void Unregister(Wall wall)
    {
        if (wall == null)
        {
            return;
        }

        activeWalls.Remove(wall);
    }

    public static void CollectWalls(List<Wall> results, Transform root = null)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        foreach (Wall wall in activeWalls)
        {
            if (wall == null)
            {
                continue;
            }

            if (root != null && !wall.transform.IsChildOf(root))
            {
                continue;
            }

            results.Add(wall);
        }
    }
}
