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

            if (IsPreviewWall(wall))
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

    public static bool IsHiddenOpeningBaseSegment(Wall wall)
    {
        if (wall == null || wall.GetComponentInParent<WallOpeningContainer>() == null)
        {
            return false;
        }

        MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
        return renderer != null && !renderer.enabled;
    }

    public static bool IsPreviewWall(Wall wall)
    {
        return wall != null && IsPreviewWall(wall.transform);
    }

    public static bool IsPreviewWall(Transform wallTransform)
    {
        if (wallTransform == null)
        {
            return false;
        }

        Transform exportRoot = wallTransform;
        WallOpeningContainer container = wallTransform.GetComponentInParent<WallOpeningContainer>();
        if (container != null)
        {
            exportRoot = container.transform;
        }

        return string.Equals(exportRoot.name, "WallPreview", System.StringComparison.OrdinalIgnoreCase) ||
               string.Equals(wallTransform.name, "WallPreview", System.StringComparison.OrdinalIgnoreCase);
    }
}
