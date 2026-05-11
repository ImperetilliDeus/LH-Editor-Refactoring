using System.Collections.Generic;
using UnityEngine;

public static class WallNamingUtility
{
    private const string WallNamePrefix = "wall";
    private const string ContainerSegmentName = "Segment";
    private static readonly List<Wall> CachedWalls = new List<Wall>();
    private static readonly List<Transform> CachedRoots = new List<Transform>();

    public static void NormalizeWallNames(Transform wallRoot)
    {
        if (wallRoot == null)
        {
            return;
        }

        CollectRenamableRoots(wallRoot, CachedRoots);
        WallHierarchyUtility.CollectWalls(wallRoot, CachedWalls, true);

        for (int i = 0; i < CachedWalls.Count; i++)
        {
            Wall wall = CachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (IsGeneratedContainerSegment(wall))
            {
                wall.name = ContainerSegmentName;
            }
        }

        int renamableCount = CachedRoots.Count;
        if (renamableCount == 0)
        {
            return;
        }

        int digits = Mathf.Max(2, renamableCount.ToString().Length);
        for (int i = 0; i < CachedRoots.Count; i++)
        {
            Transform root = CachedRoots[i];
            root.name = $"{WallNamePrefix}{(i + 1).ToString().PadLeft(digits, '0')}";
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

    private static bool IsGeneratedContainerSegment(Wall wall)
    {
        return wall != null && wall.GetComponentInParent<WallOpeningContainer>() != null;
    }

    private static void CollectRenamableRoots(Transform wallRoot, List<Transform> results)
    {
        results.Clear();
        if (wallRoot == null)
        {
            return;
        }

        for (int i = 0; i < wallRoot.childCount; i++)
        {
            Transform child = wallRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.GetComponent<WallOpeningContainer>() != null)
            {
                results.Add(child);
                continue;
            }

            Wall wall = child.GetComponent<Wall>();
            if (ShouldRename(wall))
            {
                results.Add(child);
            }
        }
    }
}
