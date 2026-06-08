using UnityEngine;

public static class PerspectiveHighlightBoundsUtility
{
    private const string HighlightObjectName = "PerspectiveSelectionHighlight";
    private const string WallOverlayObjectName = "PerspectiveSelectionWallOverlay";
    private const string RoomOverlayObjectName = "PerspectiveSelectionRoomOverlay";
    private const string HighlightRootName = "PerspectiveSelectionHighlights";
    private const float BoundsEpsilon = 0.0001f;

    public static bool TryGetTargetBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (target == null)
        {
            return false;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && !IsHighlightTransform(renderer.transform))
            {
                EncapsulateBounds(renderer.bounds, ref bounds, ref hasBounds);
            }
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && !IsHighlightTransform(collider.transform))
            {
                EncapsulateBounds(collider.bounds, ref bounds, ref hasBounds);
            }
        }

        return hasBounds;
    }

    private static bool IsHighlightTransform(Transform candidate)
    {
        while (candidate != null)
        {
            if (candidate.name == HighlightObjectName ||
                candidate.name == WallOverlayObjectName ||
                candidate.name == RoomOverlayObjectName ||
                candidate.name == HighlightRootName)
            {
                return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }

    private static bool EncapsulateBounds(Bounds candidate, ref Bounds bounds, ref bool hasBounds)
    {
        if (candidate.extents.sqrMagnitude <= BoundsEpsilon * BoundsEpsilon)
        {
            return false;
        }

        if (!hasBounds)
        {
            bounds = candidate;
            hasBounds = true;
            return true;
        }

        bounds.Encapsulate(candidate);
        return true;
    }
}
