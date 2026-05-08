using System.Collections.Generic;
using UnityEngine;

public partial class WallOpeningPlacementManager
{
    public GameObject GetMarkerPrefab(WallOpening opening)
    {
        if (opening == null)
        {
            return null;
        }

        if (TryGetOpeningTypeDefinition(opening, out OpeningTypeCatalogItem definition) &&
            definition.MarkerPrefab != null)
        {
            return definition.MarkerPrefab;
        }

        MarkerVariantDefinition variant = GetMarkerVariantDefinition(opening);
        if (opening.Type == OpeningPlacementType.Door)
        {
            return variant != null && variant.MarkerPrefab != null ? variant.MarkerPrefab : doorMarkerPrefab;
        }

        return variant != null && variant.MarkerPrefab != null ? variant.MarkerPrefab : windowMarkerPrefab;
    }

    public Vector2 GetMarkerScaleMultiplier(WallOpening opening)
    {
        if (opening == null)
        {
            return Vector2.one;
        }

        if (TryGetOpeningTypeDefinition(opening, out OpeningTypeCatalogItem definition))
        {
            return definition.MarkerScaleMultiplier;
        }

        MarkerVariantDefinition variant = GetMarkerVariantDefinition(opening);
        if (variant != null)
        {
            return variant.ScaleMultiplier;
        }

        return opening.Type == OpeningPlacementType.Door
            ? doorMarkerScaleMultiplier
            : windowMarkerScaleMultiplier;
    }

    public MarkerPlacementMode GetMarkerPlacementMode(WallOpening opening)
    {
        if (opening == null)
        {
            return MarkerPlacementMode.OffsetFromOpening;
        }

        if (TryGetOpeningTypeDefinition(opening, out OpeningTypeCatalogItem definition))
        {
            return definition.MarkerPlacementMode;
        }

        MarkerVariantDefinition variant = GetMarkerVariantDefinition(opening);
        if (variant != null)
        {
            return variant.PlacementMode;
        }

        return opening.Type == OpeningPlacementType.Door
            ? doorMarkerPlacementMode
            : windowMarkerPlacementMode;
    }

    private MarkerVariantDefinition GetMarkerVariantDefinition(WallOpening opening)
    {
        if (opening == null)
        {
            return null;
        }

        if (opening.Type == OpeningPlacementType.Door)
        {
            return FindMarkerVariant(doorMarkerVariants, opening.DoorTypeKey);
        }

        return FindMarkerVariant(windowMarkerVariants, opening.WindowTypeKey);
    }

    private static T FindMarkerVariant<T>(List<T> variants, string typeKey) where T : MarkerVariantDefinition
    {
        if (variants == null || string.IsNullOrWhiteSpace(typeKey))
        {
            return null;
        }

        for (int i = 0; i < variants.Count; i++)
        {
            T variant = variants[i];
            if (variant == null || string.IsNullOrWhiteSpace(variant.TypeName))
            {
                continue;
            }

            if (string.Equals(variant.TypeName, typeKey, System.StringComparison.Ordinal))
            {
                return variant;
            }
        }

        return null;
    }

    private static void ValidateMarkerVariants<T>(List<T> variants) where T : MarkerVariantDefinition
    {
        if (variants == null)
        {
            return;
        }

        for (int i = 0; i < variants.Count; i++)
        {
            T variant = variants[i];
            if (variant == null)
            {
                continue;
            }

            variant.ClampValues();
        }
    }
}
