using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OpeningTypeCatalog", menuName = "LH/Opening Type Catalog")]
public class OpeningTypeCatalog : ScriptableObject
{
    [SerializeField] private List<OpeningTypeCatalogItem> items = new List<OpeningTypeCatalogItem>();
    public IReadOnlyList<OpeningTypeCatalogItem> Items => items;
}

[System.Serializable]
public class OpeningTypeCatalogItem
{
    [SerializeField] private WallOpeningPlacementManager.OpeningPlacementType openingType;
    [SerializeField] private string typeKey;
    [SerializeField] private string displayName;

    public WallOpeningPlacementManager.OpeningPlacementType OpeningType => openingType;
    public string TypeKey => typeKey;
    public string DisplayName => displayName;
}
