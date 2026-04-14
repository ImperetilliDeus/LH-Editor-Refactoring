using System.Collections.Generic;
using UnityEngine;

public static class WallHierarchyUtility
{
    public static void CollectWalls(Transform root, List<Wall> results, bool includeInactive = false)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        if (root == null)
        {
            return;
        }

        Wall[] walls = root.GetComponentsInChildren<Wall>(includeInactive);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            if (!includeInactive && !wall.gameObject.activeInHierarchy)
            {
                continue;
            }

            results.Add(wall);
        }
    }
}
