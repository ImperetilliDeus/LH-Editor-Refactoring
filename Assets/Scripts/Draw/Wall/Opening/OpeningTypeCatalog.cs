using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OpeningTypeCatalog", menuName = "LH/Opening Type Catalog")]
public class OpeningTypeCatalog : ScriptableObject
{
    [SerializeField] private List<OpeningTypeCatalogItem> items = new List<OpeningTypeCatalogItem>();
    public IReadOnlyList<OpeningTypeCatalogItem> Items => items;
}

public enum OpeningModelLayoutKind
{
    Auto = 0,
    DoorLike = 1,
    WindowLike = 2,
    BalconyLike = 3,
}

public enum OpeningCollisionPolicy
{
    Auto = 0,
    PassThrough = 1,
    BlockCharacter = 2,
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
    [SerializeField] private Vector3 minSize = Vector3.zero;
    [SerializeField] private Vector3 maxSize = Vector3.zero;
    [SerializeField] private OpeningModelLayoutKind modelLayoutKind = OpeningModelLayoutKind.Auto;
    [SerializeField] private OpeningCollisionPolicy collisionPolicy = OpeningCollisionPolicy.Auto;
    [SerializeField] private bool selectable = true;

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
    public Vector3 MinSize => minSize;
    public Vector3 MaxSize => maxSize;
    public OpeningModelLayoutKind ModelLayoutKind => modelLayoutKind;
    public OpeningCollisionPolicy CollisionPolicy => collisionPolicy;
    public bool Selectable => selectable;

    public float ClampWidth(float value)
    {
        return ClampAxis(value, minSize.x, maxSize.x);
    }

    public float ClampHeight(float value)
    {
        return ClampAxis(value, minSize.y, maxSize.y);
    }

    public float ClampDepth(float value)
    {
        return ClampAxis(value, minSize.z, maxSize.z);
    }

    private static float ClampAxis(float value, float min, float max)
    {
        float result = value;
        if (min > 0f)
        {
            result = Mathf.Max(result, min);
        }

        if (max > 0f)
        {
            result = Mathf.Min(result, max);
        }

        return result;
    }
}
