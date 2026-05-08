using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OpeningTypeCatalog", menuName = "LH/Opening Type Catalog")]
public class OpeningTypeCatalog : ScriptableObject
{
    [SerializeField] private List<OpeningTypeCatalogItem> items = new List<OpeningTypeCatalogItem>();

    public IReadOnlyList<OpeningTypeCatalogItem> Items => items;

    private void OnValidate()
    {
        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            items[i]?.ClampValues();
        }
    }
}

[Serializable]
public class OpeningTypeCatalogItem
{
    [SerializeField] private WallOpeningPlacementManager.OpeningPlacementType openingType;
    [SerializeField] private string typeKey = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Vector2 markerScaleMultiplier = Vector2.one;
    [SerializeField] private WallOpeningPlacementManager.MarkerPlacementMode markerPlacementMode =
        WallOpeningPlacementManager.MarkerPlacementMode.OffsetFromOpening;
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 modelLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 modelScaleMultiplier = Vector3.one;

    public WallOpeningPlacementManager.OpeningPlacementType OpeningType => openingType;
    public string TypeKey => typeKey ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? TypeKey : displayName;
    public GameObject MarkerPrefab => markerPrefab;
    public Vector2 MarkerScaleMultiplier => markerScaleMultiplier;
    public WallOpeningPlacementManager.MarkerPlacementMode MarkerPlacementMode => markerPlacementMode;
    public GameObject ModelPrefab => modelPrefab;
    public Vector3 ModelLocalPosition => modelLocalPosition;
    public Vector3 ModelLocalEulerAngles => modelLocalEulerAngles;
    public Vector3 ModelScaleMultiplier => modelScaleMultiplier;

    public void ClampValues()
    {
        markerScaleMultiplier = new Vector2(
            Mathf.Max(0.01f, markerScaleMultiplier.x),
            Mathf.Max(0.01f, markerScaleMultiplier.y));
        modelScaleMultiplier = new Vector3(
            Mathf.Max(0.01f, modelScaleMultiplier.x),
            Mathf.Max(0.01f, modelScaleMultiplier.y),
            Mathf.Max(0.01f, modelScaleMultiplier.z));
    }
}
