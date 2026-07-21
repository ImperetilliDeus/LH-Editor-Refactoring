using System.Collections.Generic;
using UnityEngine;

internal static class OpeningLayoutService
{
    public static float ClampCenterDistance(
        WallOpeningContainer container,
        WallOpening targetOpening,
        IReadOnlyList<WallOpening> openings,
        float desiredDistance,
        float minimumSideWall,
        float overrideWidth = -1f)
    {
        if (container == null)
        {
            return desiredDistance;
        }

        float targetWidth = overrideWidth > 0f ? overrideWidth : (targetOpening != null ? targetOpening.Width : 0f);
        float halfWidth = targetWidth * 0.5f;
        float minDistance = minimumSideWall + halfWidth;
        float maxDistance = container.WallLength - minimumSideWall - halfWidth;

        if (openings != null)
        {
            for (int i = 0; i < openings.Count; i++)
            {
                WallOpening opening = openings[i];
                if (opening == null || opening == targetOpening)
                {
                    continue;
                }

                float clearance = minimumSideWall + halfWidth + opening.Width * 0.5f;
                if (opening.CenterDistance <= desiredDistance)
                {
                    minDistance = Mathf.Max(minDistance, opening.CenterDistance + clearance);
                }
                else
                {
                    maxDistance = Mathf.Min(maxDistance, opening.CenterDistance - clearance);
                }
            }
        }

        if (maxDistance < minDistance)
        {
            float midpoint = (minDistance + maxDistance) * 0.5f;
            minDistance = midpoint;
            maxDistance = midpoint;
        }

        return Mathf.Clamp(desiredDistance, minDistance, maxDistance);
    }

    public static float ClampWidth(
        WallOpeningContainer container,
        WallOpening targetOpening,
        IReadOnlyList<WallOpening> openings,
        float desiredWidth,
        float minimumSideWall,
        float minimumWallSegmentLength,
        OpeningTypeCatalogItem definition)
    {
        if (container == null || targetOpening == null)
        {
            return desiredWidth;
        }

        float leftLimit = minimumSideWall;
        float rightLimit = container.WallLength - minimumSideWall;

        if (openings != null)
        {
            for (int i = 0; i < openings.Count; i++)
            {
                WallOpening opening = openings[i];
                if (opening == null || opening == targetOpening)
                {
                    continue;
                }

                float neighborHalfWidth = opening.Width * 0.5f;
                if (opening.CenterDistance < targetOpening.CenterDistance)
                {
                    leftLimit = Mathf.Max(leftLimit, opening.CenterDistance + neighborHalfWidth + minimumSideWall);
                }
                else
                {
                    rightLimit = Mathf.Min(rightLimit, opening.CenterDistance - neighborHalfWidth - minimumSideWall);
                }
            }
        }

        float maxWidth = Mathf.Max(minimumWallSegmentLength, Mathf.Min(
            (targetOpening.CenterDistance - leftLimit) * 2f,
            (rightLimit - targetOpening.CenterDistance) * 2f));
        float clampedWidth = Mathf.Clamp(desiredWidth, minimumWallSegmentLength, maxWidth);
        if (definition != null)
        {
            clampedWidth = definition.ClampWidth(clampedWidth);
            clampedWidth = Mathf.Clamp(clampedWidth, minimumWallSegmentLength, maxWidth);
        }

        return clampedWidth;
    }

    public static float ClampHeight(
        WallOpeningContainer container,
        float desiredHeight,
        float bottomY,
        float minimumWallSegmentLength,
        OpeningTypeCatalogItem definition)
    {
        if (container == null)
        {
            return desiredHeight;
        }

        float maxHeight = Mathf.Max(
            minimumWallSegmentLength,
            container.WallTopY - bottomY);
        float clampedHeight = Mathf.Clamp(desiredHeight, minimumWallSegmentLength, maxHeight);
        if (definition != null)
        {
            clampedHeight = definition.ClampHeight(clampedHeight);
            clampedHeight = Mathf.Clamp(clampedHeight, minimumWallSegmentLength, maxHeight);
        }

        return clampedHeight;
    }

    public static float ClampBottomY(WallOpeningContainer container, WallOpening targetOpening, float desiredBottomY)
    {
        if (container == null || targetOpening == null)
        {
            return desiredBottomY;
        }

        float minBottomY = container.WallBottomY;
        float maxBottomY = container.WallTopY - targetOpening.Height;
        if (maxBottomY < minBottomY)
        {
            maxBottomY = minBottomY;
        }

        return Mathf.Clamp(desiredBottomY, minBottomY, maxBottomY);
    }
}
