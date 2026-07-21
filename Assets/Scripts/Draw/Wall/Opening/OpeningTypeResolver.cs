using System;
using System.Collections.Generic;

internal static class OpeningTypeResolver
{
    public static bool TryFindDefinition(
        OpeningTypeCatalog catalog,
        WallOpeningPlacementManager.OpeningPlacementType openingType,
        string typeKey,
        out OpeningTypeCatalogItem definition)
    {
        definition = null;
        if (catalog == null || string.IsNullOrWhiteSpace(typeKey))
        {
            return false;
        }

        IReadOnlyList<OpeningTypeCatalogItem> items = catalog.Items;
        if (items == null)
        {
            return false;
        }

        for (int i = 0; i < items.Count; i++)
        {
            OpeningTypeCatalogItem item = items[i];
            if (item == null || item.OpeningType != openingType)
            {
                continue;
            }

            if (string.Equals(item.TypeKey, typeKey, StringComparison.Ordinal) ||
                string.Equals(item.DisplayName, typeKey, StringComparison.Ordinal))
            {
                definition = item;
                return true;
            }
        }

        return false;
    }

    public static string ResolveTypeKey(WallOpening opening)
    {
        if (opening == null)
        {
            return string.Empty;
        }

        return opening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? opening.DoorTypeKey
            : opening.WindowTypeKey;
    }
}
