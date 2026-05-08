using System.Collections.Generic;
using UnityEngine;

public partial class WallOpeningPlacementManager
{
    public struct OpeningTypeOption
    {
        public string Key;
        public string DisplayName;
    }

    private void ResolveOpeningTypeCatalog()
    {
        if (openingTypeCatalog != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(openingTypeCatalogResourcePath))
        {
            return;
        }

        openingTypeCatalog = Resources.Load<OpeningTypeCatalog>(openingTypeCatalogResourcePath);
    }

    private void RefreshOpeningTypeCatalogCache()
    {
        doorTypeOptions.Clear();
        windowTypeOptions.Clear();

        if (openingTypeCatalog == null)
        {
            return;
        }

        IReadOnlyList<OpeningTypeCatalogItem> items = openingTypeCatalog.Items;
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            OpeningTypeCatalogItem item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.TypeKey))
            {
                continue;
            }

            OpeningTypeOption option = new OpeningTypeOption
            {
                Key = item.TypeKey,
                DisplayName = item.DisplayName,
            };

            if (item.OpeningType == OpeningPlacementType.Door)
            {
                doorTypeOptions.Add(option);
            }
            else
            {
                windowTypeOptions.Add(option);
            }
        }
    }

    private void ApplyOpeningTypeOptionsToUI()
    {
        doorUIController?.SetTypeOptions(doorTypeOptions);
        windowUIController?.SetTypeOptions(windowTypeOptions);
    }

    private void InitializeOpeningTypeOptions()
    {
        ResolveOpeningTypeCatalog();
        RefreshOpeningTypeCatalogCache();
        ApplyOpeningTypeOptionsToUI();
    }

    private bool TryGetOpeningTypeDefinition(
        OpeningPlacementType openingType,
        string typeKey,
        out OpeningTypeCatalogItem definition)
    {
        definition = null;
        if (openingTypeCatalog == null || string.IsNullOrWhiteSpace(typeKey))
        {
            return false;
        }

        IReadOnlyList<OpeningTypeCatalogItem> items = openingTypeCatalog.Items;
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

            if (string.Equals(item.TypeKey, typeKey, System.StringComparison.Ordinal) ||
                string.Equals(item.DisplayName, typeKey, System.StringComparison.Ordinal))
            {
                definition = item;
                return true;
            }
        }

        return false;
    }

    private bool TryGetOpeningTypeDefinition(WallOpening opening, out OpeningTypeCatalogItem definition)
    {
        definition = null;
        if (opening == null)
        {
            return false;
        }

        string typeKey = opening.Type == OpeningPlacementType.Door
            ? opening.DoorTypeKey
            : opening.WindowTypeKey;

        return TryGetOpeningTypeDefinition(opening.Type, typeKey, out definition);
    }
}
