using System.Collections.Generic;
using UnityEngine;

public static class WallRegistry
{
    private static readonly HashSet<Wall> activeWalls = new HashSet<Wall>();
    public static event System.Action RegistryChanged;

    public static void Register(Wall wall)
    {
        if (wall == null || WallHierarchyUtility.IsPreviewWall(wall))
        {
            return;
        }

        activeWalls.Add(wall);
        RegistryChanged?.Invoke();
    }

    public static void Unregister(Wall wall)
    {
        if (wall == null)
        {
            return;
        }

        activeWalls.Remove(wall);
        RegistryChanged?.Invoke();
    }

    public static void NotifyWallChanged(Wall wall)
    {
        if (wall == null)
        {
            return;
        }

        if (WallHierarchyUtility.IsPreviewWall(wall))
        {
            activeWalls.Remove(wall);
            return;
        }

        RegistryChanged?.Invoke();
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

            if (WallHierarchyUtility.IsPreviewWall(wall))
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
