using UnityEngine;

public class WallOpening : MonoBehaviour
{
    private const float MinimumModelBoundsSize = 0.0001f;

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

    public void ApplyModelPrefab(GameObject prefab, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScaleMultiplier)
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

        bool instanceParentChanged = activeModelInstance != null && activeModelInstance.transform.parent != modelRotationRoot;
        bool prefabChanged = activeModelPrefab != prefab || activeModelInstance == null || instanceParentChanged;
        if (prefabChanged)
        {
            if (activeModelInstance != null)
            {
                Destroy(activeModelInstance);
            }

            activeModelInstance = Instantiate(prefab, modelRotationRoot);
            activeModelInstance.name = prefab.name;
            activeModelPrefab = prefab;
        }

        modelRoot.localPosition = localPosition;
        modelRoot.localRotation = Quaternion.identity;
        modelRoot.localScale = Vector3.one;

        modelScaleRoot.localPosition = Vector3.zero;
        modelScaleRoot.localRotation = Quaternion.identity;
        modelScaleRoot.localScale = Vector3.one;

        modelRotationRoot.localPosition = Vector3.zero;
        modelRotationRoot.localRotation = Quaternion.Euler(localEulerAngles);
        modelRotationRoot.localScale = Vector3.one;

        modelScaleRoot.localScale = CalculateNormalizedModelScale(localScaleMultiplier);
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

        if (modelScaleRoot == null || modelScaleRoot.parent != modelRoot)
        {
            Transform existing = modelRoot.Find("ModelScaleRoot");
            modelScaleRoot = existing != null ? existing : new GameObject("ModelScaleRoot").transform;
            modelScaleRoot.SetParent(modelRoot, false);
        }

        if (modelRotationRoot == null || modelRotationRoot.parent != modelScaleRoot)
        {
            Transform existing = modelScaleRoot.Find("ModelRotationRoot");
            modelRotationRoot = existing != null ? existing : new GameObject("ModelRotationRoot").transform;
            modelRotationRoot.SetParent(modelScaleRoot, false);
        }
    }

    private Vector3 CalculateNormalizedModelScale(Vector3 localScaleMultiplier)
    {
        Vector3 clampedMultiplier = new Vector3(
            Mathf.Max(MinimumModelBoundsSize, localScaleMultiplier.x),
            Mathf.Max(MinimumModelBoundsSize, localScaleMultiplier.y),
            Mathf.Max(MinimumModelBoundsSize, localScaleMultiplier.z));

        if (!TryCalculateModelBoundsInOpeningSpace(out Bounds bounds))
        {
            return clampedMultiplier;
        }

        Vector3 size = bounds.size;
        return new Vector3(
            NormalizeModelScaleAxis(clampedMultiplier.x, size.x),
            NormalizeModelScaleAxis(clampedMultiplier.y, size.y),
            NormalizeModelScaleAxis(clampedMultiplier.z, size.z));
    }

    private static float NormalizeModelScaleAxis(float multiplier, float size)
    {
        return size <= MinimumModelBoundsSize
            ? multiplier
            : multiplier / size;
    }

    private bool TryCalculateModelBoundsInOpeningSpace(out Bounds bounds)
    {
        bounds = default;
        if (activeModelInstance == null)
        {
            return false;
        }

        bool hasBounds = false;
        Renderer[] renderers = activeModelInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            EncapsulateWorldBounds(renderer.bounds, ref hasBounds, ref bounds);
        }

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = activeModelInstance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            EncapsulateWorldBounds(collider.bounds, ref hasBounds, ref bounds);
        }

        return hasBounds;
    }

    private void EncapsulateWorldBounds(Bounds worldBounds, ref bool hasBounds, ref Bounds localBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;

        for (int x = 0; x < 2; x++)
        {
            float cornerX = x == 0 ? min.x : max.x;
            for (int y = 0; y < 2; y++)
            {
                float cornerY = y == 0 ? min.y : max.y;
                for (int z = 0; z < 2; z++)
                {
                    float cornerZ = z == 0 ? min.z : max.z;
                    Vector3 localPoint = transform.InverseTransformPoint(new Vector3(cornerX, cornerY, cornerZ));
                    if (!hasBounds)
                    {
                        localBounds = new Bounds(localPoint, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(localPoint);
                    }
                }
            }
        }
    }
}
