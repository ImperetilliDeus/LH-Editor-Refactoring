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
        CollectOpenings(container, cachedOpenings);
        return OpeningLayoutService.ClampCenterDistance(
            container,
            targetOpening,
            cachedOpenings,
            desiredDistance,
            minimumSideWall,
            overrideWidth);
    }

    private float ClampOpeningWidth(WallOpeningContainer container, WallOpening targetOpening, float desiredWidth)
    {
        if (container == null || targetOpening == null)
        {
            return desiredWidth;
        }

        CollectOpenings(container, cachedOpenings);
        TryGetOpeningTypeDefinition(targetOpening, out OpeningTypeCatalogItem definition);
        return OpeningLayoutService.ClampWidth(
            container,
            targetOpening,
            cachedOpenings,
            desiredWidth,
            MillimetersToUnits(minimumSideWallMillimeters),
            MinimumWallSegmentLength,
            definition);
    }

    private float ClampOpeningHeight(WallOpeningContainer container, WallOpening targetOpening, float desiredHeight, float bottomY)
    {
        if (container == null)
        {
            return desiredHeight;
        }

        TryGetOpeningTypeDefinition(targetOpening, out OpeningTypeCatalogItem definition);
        return OpeningLayoutService.ClampHeight(
            container,
            desiredHeight,
            bottomY,
            MinimumWallSegmentLength,
            definition);
    }

    private float ClampOpeningBottomY(WallOpeningContainer container, WallOpening targetOpening, float desiredBottomY)
    {
        if (container == null || targetOpening == null)
        {
            return desiredBottomY;
        }

        return OpeningLayoutService.ClampBottomY(container, targetOpening, desiredBottomY);
    }

    private bool TryParsePositiveMillimeters(string inputText, out float value)
    {
        bool parsed = UnitDisplayUtility.TryParseMillimeters(inputText, out value);
        return parsed && value > 0f;
    }
}
