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

        ApplyCenterDistanceNeighborLimits(
            openings,
            targetOpening,
            desiredDistance,
            minimumSideWall,
            halfWidth,
            ref minDistance,
            ref maxDistance);

        if (maxDistance < minDistance)
        {
            float midpoint = (minDistance + maxDistance) * 0.5f;
            minDistance = midpoint;
            maxDistance = midpoint;
        }

        return Mathf.Clamp(desiredDistance, minDistance, maxDistance);
    }

    private static void ApplyCenterDistanceNeighborLimits(
        IReadOnlyList<WallOpening> openings,
        WallOpening targetOpening,
        float desiredDistance,
        float minimumSideWall,
        float halfWidth,
        ref float minDistance,
        ref float maxDistance)
    {
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

        ApplyWidthNeighborLimits(
            openings,
            targetOpening,
            minimumSideWall,
            ref leftLimit,
            ref rightLimit);

        float maxWidth = Mathf.Max(minimumWallSegmentLength, Mathf.Min(
            (targetOpening.CenterDistance - leftLimit) * 2f,
            (rightLimit - targetOpening.CenterDistance) * 2f));
        float clampedWidth = Mathf.Clamp(desiredWidth, minimumWallSegmentLength, maxWidth);
        return ApplyWidthDefinitionConstraint(clampedWidth, minimumWallSegmentLength, maxWidth, definition);
    }

    private static void ApplyWidthNeighborLimits(
        IReadOnlyList<WallOpening> openings,
        WallOpening targetOpening,
        float minimumSideWall,
        ref float leftLimit,
        ref float rightLimit)
    {
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
        return ApplyHeightDefinitionConstraint(clampedHeight, minimumWallSegmentLength, maxHeight, definition);
    }

    private static float ApplyWidthDefinitionConstraint(
        float clampedWidth,
        float minimumWallSegmentLength,
        float maxWidth,
        OpeningTypeCatalogItem definition)
    {
        if (definition == null)
        {
            return clampedWidth;
        }

        clampedWidth = definition.ClampWidth(clampedWidth);
        return Mathf.Clamp(clampedWidth, minimumWallSegmentLength, maxWidth);
    }

    private static float ApplyHeightDefinitionConstraint(
        float clampedHeight,
        float minimumWallSegmentLength,
        float maxHeight,
        OpeningTypeCatalogItem definition)
    {
        if (definition == null)
        {
            return clampedHeight;
        }

        clampedHeight = definition.ClampHeight(clampedHeight);
        return Mathf.Clamp(clampedHeight, minimumWallSegmentLength, maxHeight);
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

    public static bool TryResolveNewOpeningSpan(
        WallOpeningContainer container,
        IReadOnlyList<WallOpening> sortedOpenings,
        float desiredWidth,
        float minimumSideWall,
        float minimumWallSegmentLength,
        out float centerDistance,
        out float resolvedWidth)
    {
        centerDistance = 0f;
        resolvedWidth = 0f;
        if (container == null)
        {
            return false;
        }

        float wallLength = container.WallLength;
        if (wallLength <= minimumSideWall * 2f + minimumWallSegmentLength)
        {
            return false;
        }

        float preferredCenter = wallLength * 0.5f;
        float bestLeftLimit = 0f;
        float bestRightLimit = 0f;
        float bestScore = float.MaxValue;
        bool foundGap = false;
        float leftLimit = minimumSideWall;
        int openingCount = sortedOpenings != null ? sortedOpenings.Count : 0;

        for (int i = 0; i <= openingCount; i++)
        {
            if (!TryResolveOpeningGapRightLimit(
                    sortedOpenings,
                    i,
                    openingCount,
                    wallLength,
                    minimumSideWall,
                    out float rightLimit))
            {
                continue;
            }

            float availableWidth = rightLimit - leftLimit;
            if (availableWidth >= minimumWallSegmentLength)
            {
                TryUpdateBestOpeningGap(
                    leftLimit,
                    rightLimit,
                    preferredCenter,
                    ref bestScore,
                    ref bestLeftLimit,
                    ref bestRightLimit,
                    ref foundGap);
            }

            UpdateOpeningGapLeftLimit(sortedOpenings, i, openingCount, minimumSideWall, ref leftLimit);
        }

        if (!foundGap)
        {
            return false;
        }

        float maxWidth = bestRightLimit - bestLeftLimit;
        resolvedWidth = Mathf.Min(Mathf.Max(desiredWidth, minimumWallSegmentLength), maxWidth);
        centerDistance = (bestLeftLimit + bestRightLimit) * 0.5f;
        return resolvedWidth >= minimumWallSegmentLength;
    }

    private static bool TryResolveOpeningGapRightLimit(
        IReadOnlyList<WallOpening> sortedOpenings,
        int index,
        int openingCount,
        float wallLength,
        float minimumSideWall,
        out float rightLimit)
    {
        rightLimit = wallLength - minimumSideWall;
        if (index >= openingCount)
        {
            return true;
        }

        WallOpening nextOpening = sortedOpenings[index];
        if (nextOpening == null)
        {
            return false;
        }

        rightLimit = nextOpening.CenterDistance - nextOpening.Width * 0.5f - minimumSideWall;
        return true;
    }

    private static void UpdateOpeningGapLeftLimit(
        IReadOnlyList<WallOpening> sortedOpenings,
        int index,
        int openingCount,
        float minimumSideWall,
        ref float leftLimit)
    {
        if (index >= openingCount)
        {
            return;
        }

        WallOpening previousOpening = sortedOpenings[index];
        if (previousOpening == null)
        {
            return;
        }

        leftLimit = previousOpening.CenterDistance + previousOpening.Width * 0.5f + minimumSideWall;
    }

    private static void TryUpdateBestOpeningGap(
        float leftLimit,
        float rightLimit,
        float preferredCenter,
        ref float bestScore,
        ref float bestLeftLimit,
        ref float bestRightLimit,
        ref bool foundGap)
    {
        float gapCenter = (leftLimit + rightLimit) * 0.5f;
        float score = Mathf.Abs(gapCenter - preferredCenter);
        if (score >= bestScore)
        {
            return;
        }

        bestScore = score;
        bestLeftLimit = leftLimit;
        bestRightLimit = rightLimit;
        foundGap = true;
    }
}
