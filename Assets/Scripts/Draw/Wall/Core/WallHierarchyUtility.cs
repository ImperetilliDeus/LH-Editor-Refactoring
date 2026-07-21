using System.Collections.Generic;
using UnityEngine;

public static class WallHierarchyUtility
{
    public static void CollectWalls(Transform root, List<Wall> results, bool includeInactive = false, bool includePreview = false)
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

            if (!includePreview && IsPreviewWall(wall))
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

    public static bool HasAnyLogicalWallId(Wall wall, HashSet<string> wallIds)
    {
        if (wall == null || wallIds == null || wallIds.Count == 0)
        {
            return false;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container != null && !string.IsNullOrWhiteSpace(container.PersistentWallId) && wallIds.Contains(container.PersistentWallId))
        {
            return true;
        }

        if (wall.Data != null && !string.IsNullOrWhiteSpace(wall.Data.id) && wallIds.Contains(wall.Data.id))
        {
            return true;
        }

        if (container == null)
        {
            return false;
        }

        Wall[] containerWalls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < containerWalls.Length; i++)
        {
            Wall containerWall = containerWalls[i];
            if (containerWall != null &&
                containerWall.Data != null &&
                !string.IsNullOrWhiteSpace(containerWall.Data.id) &&
                wallIds.Contains(containerWall.Data.id))
            {
                return true;
            }
        }

        return false;
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
