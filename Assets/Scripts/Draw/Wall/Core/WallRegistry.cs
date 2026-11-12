using System.Collections.Generic;
using UnityEngine;

public static class WallRegistry
{
    private static readonly HashSet<Wall> activeWalls = new HashSet<Wall>();
    private static int changeEventSuppressionDepth;
    private static bool registryChangedWhileSuppressed;

    public static event System.Action RegistryChanged;

    public static void Register(Wall wall)
    {
        if (wall == null || WallHierarchyUtility.IsPreviewWall(wall))
        {
            return;
        }

        activeWalls.Add(wall);
        NotifyRegistryChanged();
    }

    public static void Unregister(Wall wall)
    {
        if (wall == null)
        {
            return;
        }

        activeWalls.Remove(wall);
        NotifyRegistryChanged();
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

        NotifyRegistryChanged();
    }

    public static void BeginChangeEventBatch()
    {
        changeEventSuppressionDepth++;
    }

    public static void EndChangeEventBatch()
    {
        if (changeEventSuppressionDepth <= 0)
        {
            return;
        }

        changeEventSuppressionDepth--;
        if (changeEventSuppressionDepth == 0 && registryChangedWhileSuppressed)
        {
            registryChangedWhileSuppressed = false;
            RegistryChanged?.Invoke();
        }
    }

    private static void NotifyRegistryChanged()
    {
        if (changeEventSuppressionDepth > 0)
        {
            registryChangedWhileSuppressed = true;
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
