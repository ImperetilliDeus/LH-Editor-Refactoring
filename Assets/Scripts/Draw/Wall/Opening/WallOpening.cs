using UnityEngine;

public class WallOpening : MonoBehaviour
{
    private const float MinimumModelBoundsSize = 0.0001f;
    private static readonly Quaternion ModelToOpeningRotation = Quaternion.Euler(0f, -90f, 0f);

    [SerializeField] private WallOpeningPlacementManager.OpeningPlacementType type;
    [SerializeField] private string doorTypeKey;
    [SerializeField] private string windowTypeKey;
    [SerializeField] private bool doorOpensRight;
    [SerializeField] private bool doorVerticalFlip;
    [SerializeField] private float centerDistance;
    [SerializeField] private float width;
    [SerializeField] private float height;
    [SerializeField] private float depth;
    [SerializeField] private float bottomY;
    [SerializeField] private WallOpeningContainer container;
    [SerializeField] private WallOpeningPlacementManager placementManager;

    private Transform startAnchor;
    private Transform endAnchor;
    private WallOpeningMarkerUI markerUI;
    private Transform modelRoot;
    private Transform modelScaleRoot;
    private Transform modelRotationRoot;
    private GameObject activeModelInstance;
    private GameObject activeModelPrefab;

    public WallOpeningPlacementManager.OpeningPlacementType Type => type;
    public string DoorTypeKey => doorTypeKey;
    public string WindowTypeKey => windowTypeKey;
    public bool DoorOpensRight => doorOpensRight;
    public bool DoorVerticalFlip => doorVerticalFlip;
    public float CenterDistance => centerDistance;
    public float Width => width;
    public float Height => height;
    public float Depth => depth;
    public float BottomY => bottomY;
    public WallOpeningContainer Container => container;

    public void Initialize(
        WallOpeningPlacementManager manager,
        WallOpeningContainer ownerContainer,
        WallOpeningPlacementManager.OpeningPlacementType openingType,
        string openingDoorTypeKey,
        string openingWindowTypeKey,
        bool openingDoorOpensRight,
        bool openingDoorVerticalFlip,
        float openingCenterDistance,
        float openingWidth,
        float openingHeight,
        float openingDepth,
        float openingBottomY)
    {
        placementManager = manager;
        container = ownerContainer;
        type = openingType;
        doorTypeKey = openingType == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? openingDoorTypeKey ?? string.Empty
            : string.Empty;
        windowTypeKey = openingType == WallOpeningPlacementManager.OpeningPlacementType.Window
            ? openingWindowTypeKey ?? string.Empty
            : string.Empty;
        doorOpensRight = openingType == WallOpeningPlacementManager.OpeningPlacementType.Door && openingDoorOpensRight;
        doorVerticalFlip = openingType == WallOpeningPlacementManager.OpeningPlacementType.Door && openingDoorVerticalFlip;
        centerDistance = openingCenterDistance;
        width = openingWidth;
        height = openingHeight;
        depth = openingDepth;
        bottomY = openingBottomY;
    }

    public void SetCenterDistance(float value)
    {
        centerDistance = value;
    }

    public void SetWidth(float value)
    {
        width = value;
    }

    public void SetHeight(float value)
    {
        height = value;
    }

    public void SetDepth(float value)
    {
        depth = value;
    }

    public void SetBottomY(float value)
    {
        bottomY = value;
    }

    public void SetDoorTypeKey(string value)
    {
        doorTypeKey = type == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? value ?? string.Empty
            : string.Empty;
    }

    public void SetWindowTypeKey(string value)
    {
        windowTypeKey = type == WallOpeningPlacementManager.OpeningPlacementType.Window
            ? value ?? string.Empty
            : string.Empty;
    }

    public void SetDoorOpensRight(bool value)
    {
        doorOpensRight = type == WallOpeningPlacementManager.OpeningPlacementType.Door && value;
    }

    public void SetDoorVerticalFlip(bool value)
    {
        doorVerticalFlip = type == WallOpeningPlacementManager.OpeningPlacementType.Door && value;
    }

    public void EnsureMarker(Canvas canvas, Camera worldCamera)
    {
        EnsureAnchors();

        if (markerUI == null)
        {
            markerUI = GetComponent<WallOpeningMarkerUI>();
            if (markerUI == null)
            {
                markerUI = gameObject.AddComponent<WallOpeningMarkerUI>();
            }
        }

        markerUI.Initialize(this, placementManager, canvas, worldCamera, startAnchor, endAnchor);
    }

    private void EnsureAnchors()
    {
        if (startAnchor == null)
        {
            Transform existing = transform.Find("MarkerStart");
            startAnchor = existing != null ? existing : new GameObject("MarkerStart").transform;
            startAnchor.SetParent(transform, false);
        }

        if (endAnchor == null)
        {
            Transform existing = transform.Find("MarkerEnd");
            endAnchor = existing != null ? existing : new GameObject("MarkerEnd").transform;
            endAnchor.SetParent(transform, false);
        }

        startAnchor.localPosition = new Vector3(0f, 0f, -0.5f);
        endAnchor.localPosition = new Vector3(0f, 0f, 0.5f);
    }

    public void ApplyModelPrefab(
        GameObject prefab,
        Vector3 localPosition,
        Vector3 localEulerAngles,
        Vector3 localScaleMultiplier,
        Vector3 openingSize,
        Vector3 modelTargetSize,
        Vector3 referenceSize,
        bool fitDepth,
        bool fitHeight,
        bool fitWidth)
    {
        if (prefab == null)
        {
            ClearModelPrefab();
            return;
        }

        EnsureModelHierarchy();
        if (modelRoot == null || modelScaleRoot == null || modelRotationRoot == null)
        {
            return;
        }

        bool instanceParentChanged = activeModelInstance != null && activeModelInstance.transform.parent != modelScaleRoot;
        bool prefabChanged = activeModelPrefab != prefab || activeModelInstance == null || instanceParentChanged;
        if (prefabChanged)
        {
            if (activeModelInstance != null)
            {
                Destroy(activeModelInstance);
            }

            activeModelInstance = Instantiate(prefab, modelScaleRoot);
            activeModelInstance.name = prefab.name;
            activeModelPrefab = prefab;
        }

        NormalizePrefabPivotRotations(activeModelInstance != null ? activeModelInstance.transform : null);

        modelRoot.localPosition = localPosition;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = CalculateInverseOpeningScale(openingSize);

        modelRotationRoot.localPosition = Vector3.zero;
        modelRotationRoot.localRotation = Quaternion.Euler(localEulerAngles) * ModelToOpeningRotation;
        modelRotationRoot.localScale = Vector3.one;

        modelScaleRoot.localPosition = Vector3.zero;
        modelScaleRoot.localRotation = Quaternion.identity;
        modelScaleRoot.localScale = Vector3.one;

        modelScaleRoot.localScale = CalculateReferenceModelScale(
            localScaleMultiplier,
            modelTargetSize,
            referenceSize,
            fitDepth,
            fitHeight,
            fitWidth);
        LayerUtility.ApplyLayer(
            modelRoot.gameObject,
            type == WallOpeningPlacementManager.OpeningPlacementType.Door
                ? LayerUtility.DoorLayerName
                : LayerUtility.WindowLayerName,
            true);
    }

    public void ClearModelPrefab()
    {
        if (activeModelInstance != null)
        {
            Destroy(activeModelInstance);
        }

        activeModelInstance = null;
        activeModelPrefab = null;
        if (modelRoot != null)
        {
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.localScale = Vector3.one;
        }

        if (modelScaleRoot != null)
        {
            modelScaleRoot.localPosition = Vector3.zero;
            modelScaleRoot.localRotation = Quaternion.identity;
            modelScaleRoot.localScale = Vector3.one;
        }

        if (modelRotationRoot != null)
        {
            modelRotationRoot.localPosition = Vector3.zero;
            modelRotationRoot.localRotation = Quaternion.identity;
            modelRotationRoot.localScale = Vector3.one;
        }
    }

    private void EnsureModelHierarchy()
    {
        if (modelRoot == null)
        {
            Transform existing = transform.Find("ModelRoot");
            modelRoot = existing != null ? existing : new GameObject("ModelRoot").transform;
            modelRoot.SetParent(transform, false);
        }

        Transform legacyScaleRoot = modelRoot.Find("ModelScaleRoot");
        if (legacyScaleRoot != null)
        {
            if (modelScaleRoot == legacyScaleRoot)
            {
                modelScaleRoot = null;
            }

            if (modelRotationRoot != null && modelRotationRoot.IsChildOf(legacyScaleRoot))
            {
                modelRotationRoot = null;
            }

            DestroyModelHierarchyObject(legacyScaleRoot.gameObject);
        }

        if (modelRotationRoot == null || modelRotationRoot.parent != modelRoot)
        {
            Transform existing = modelRoot.Find("ModelRotationRoot");
            modelRotationRoot = existing != null ? existing : new GameObject("ModelRotationRoot").transform;
            modelRotationRoot.SetParent(modelRoot, false);
        }

        if (modelScaleRoot == null || modelScaleRoot.parent != modelRotationRoot)
        {
            Transform existing = modelRotationRoot.Find("ModelScaleRoot");
            modelScaleRoot = existing != null ? existing : new GameObject("ModelScaleRoot").transform;
            modelScaleRoot.SetParent(modelRotationRoot, false);
        }
    }

    private static void DestroyModelHierarchyObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static void NormalizePrefabPivotRotations(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == "Pivot")
            {
                child.localRotation = Quaternion.identity;
            }
        }
    }

    private static Vector3 CalculateReferenceModelScale(
        Vector3 localScaleMultiplier,
        Vector3 modelTargetSize,
        Vector3 referenceSize,
        bool fitDepth,
        bool fitHeight,
        bool fitWidth)
    {
        Vector3 clampedMultiplier = new Vector3(
            Mathf.Max(MinimumModelBoundsSize, localScaleMultiplier.x),
            Mathf.Max(MinimumModelBoundsSize, localScaleMultiplier.y),
            Mathf.Max(MinimumModelBoundsSize, localScaleMultiplier.z));

        Vector3 clampedReferenceSize = new Vector3(
            Mathf.Max(MinimumModelBoundsSize, referenceSize.x),
            Mathf.Max(MinimumModelBoundsSize, referenceSize.y),
            Mathf.Max(MinimumModelBoundsSize, referenceSize.z));

        return new Vector3(
            fitWidth ? clampedMultiplier.x * modelTargetSize.x / clampedReferenceSize.x : clampedMultiplier.x,
            fitHeight ? clampedMultiplier.y * modelTargetSize.y / clampedReferenceSize.y : clampedMultiplier.y,
            fitDepth ? clampedMultiplier.z * modelTargetSize.z / clampedReferenceSize.z : clampedMultiplier.z);
    }

    private static Vector3 CalculateInverseOpeningScale(Vector3 targetSize)
    {
        return new Vector3(
            InverseScaleAxis(targetSize.x),
            InverseScaleAxis(targetSize.y),
            InverseScaleAxis(targetSize.z));
    }

    private static float InverseScaleAxis(float value)
    {
        return Mathf.Abs(value) > MinimumModelBoundsSize ? 1f / value : 1f;
    }
}
