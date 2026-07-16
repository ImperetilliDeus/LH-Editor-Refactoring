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
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private Vector3 modelLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 modelLocalEulerAngles = Vector3.zero;
    [SerializeField] private Vector3 modelScaleMultiplier = Vector3.one;
    [SerializeField] private Vector3 referenceSize = Vector3.one;
    [SerializeField] private bool fitDepth = true;
    [SerializeField] private bool fitHeight = true;
    [SerializeField] private bool fitWidth = true;
    [SerializeField] private bool useParametricModel;
    [SerializeField] private string parametricProfileKey;
    [SerializeField] private Vector3 authoredSize = Vector3.one;
    [SerializeField] private bool usesBlenderLocalAxes;

    public WallOpeningPlacementManager.OpeningPlacementType OpeningType => openingType;
    public string TypeKey => typeKey;
    public string DisplayName => displayName;
    public GameObject ModelPrefab => modelPrefab;
    public Vector3 ModelLocalPosition => modelLocalPosition;
    public Vector3 ModelLocalEulerAngles => modelLocalEulerAngles;
    public Vector3 ModelScaleMultiplier => modelScaleMultiplier;
    public Vector3 ReferenceSize => referenceSize;
    public bool FitDepth => fitDepth;
    public bool FitHeight => fitHeight;
    public bool FitWidth => fitWidth;
    public bool UseParametricModel => useParametricModel;
    public string ParametricProfileKey => parametricProfileKey;
    public Vector3 AuthoredSize => authoredSize;
    public bool UsesBlenderLocalAxes => usesBlenderLocalAxes;
}
