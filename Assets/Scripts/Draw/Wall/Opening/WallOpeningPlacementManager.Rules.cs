using UnityEngine;

public partial class WallOpeningPlacementManager
{
    private float ClampOpeningCenterDistance(
        WallOpeningContainer container,
        WallOpening targetOpening,
        float desiredDistance,
        float overrideWidth = -1f)
    {
        if (container == null)
        {
            return desiredDistance;
        }

        float minimumSideWall = MillimetersToUnits(minimumSideWallMillimeters);
        float targetWidth = overrideWidth > 0f ? overrideWidth : (targetOpening != null ? targetOpening.Width : 0f);
        float halfWidth = targetWidth * 0.5f;
        float minDistance = minimumSideWall + halfWidth;
        float maxDistance = container.WallLength - minimumSideWall - halfWidth;

        CollectOpenings(container, cachedOpenings);
        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
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

        if (maxDistance < minDistance)
        {
            float midpoint = (minDistance + maxDistance) * 0.5f;
            minDistance = midpoint;
            maxDistance = midpoint;
        }

        return Mathf.Clamp(desiredDistance, minDistance, maxDistance);
    }

    private float ClampOpeningWidth(WallOpeningContainer container, WallOpening targetOpening, float desiredWidth)
    {
        if (container == null || targetOpening == null)
        {
            return desiredWidth;
        }

        float minimumSideWall = MillimetersToUnits(minimumSideWallMillimeters);
        float leftLimit = minimumSideWall;
        float rightLimit = container.WallLength - minimumSideWall;

        CollectOpenings(container, cachedOpenings);
        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
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

        float maxWidth = Mathf.Max(MinimumWallSegmentLength, Mathf.Min(
            (targetOpening.CenterDistance - leftLimit) * 2f,
            (rightLimit - targetOpening.CenterDistance) * 2f));
        return Mathf.Clamp(desiredWidth, MinimumWallSegmentLength, maxWidth);
    }

    private float ClampOpeningHeight(WallOpeningContainer container, WallOpening targetOpening, float desiredHeight, float bottomY)
    {
        if (container == null)
        {
            return desiredHeight;
        }

        float maxHeight = Mathf.Max(
            MinimumWallSegmentLength,
            container.WallTopY - bottomY);
        return Mathf.Clamp(desiredHeight, MinimumWallSegmentLength, maxHeight);
    }

    private float ClampOpeningBottomY(WallOpeningContainer container, WallOpening targetOpening, float desiredBottomY)
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

    private bool TryParsePositiveMillimeters(string inputText, out float value)
    {
        bool parsed = UnitDisplayUtility.TryParseMillimeters(inputText, out value);
        return parsed && value > 0f;
    }
}
