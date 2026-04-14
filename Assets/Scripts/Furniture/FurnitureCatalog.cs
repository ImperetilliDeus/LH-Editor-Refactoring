using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FurnitureCatalog", menuName = "LH/Furniture Catalog")]
public class FurnitureCatalog : ScriptableObject
{
    [SerializeField] private List<FurnitureCatalogItem> items = new List<FurnitureCatalogItem>();

    public IReadOnlyList<FurnitureCatalogItem> Items => items;
}

[Serializable]
public class FurnitureCatalogItem
{
    public string code;
    public string displayName;
    public GameObject prefab;
    public Texture2D thumbnail;
    public Vector3 placementOffset;
    public Vector3 defaultEulerAngles;
    public Vector3 boundsSize = Vector3.one;
}
