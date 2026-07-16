using UnityEngine;

public class WallVisualizer : MonoBehaviour
{
    private const string StartCapObjectName = "WallStartCap";
    private const string EndCapObjectName = "WallEndCap";
    private const float MinimumExtension = 0.0001f;
    private const float DefaultObservedMinimumLength = 0.0001f;

    [SerializeField] private float topFaceWorldOffset = Wall.DefaultTopFaceOffset;

    private Transform startCapTransform;
    private Transform endCapTransform;
    private MeshRenderer startCapRenderer;
    private MeshRenderer endCapRenderer;
    private MeshFilter startCapFilter;
    private MeshFilter endCapFilter;
    private Wall observedWall;
    private WallData observedData;

    private void OnEnable()
    {
        AttachToWall();
        RefreshFromData(DefaultObservedMinimumLength);
    }

    private void OnDisable()
    {
        DetachFromData();
    }

    public void RefreshFromData(float minimumLength)
    {
        AttachToWall();
        if (observedData == null)
        {
            return;
        }

        TryApplyWallData(observedData, minimumLength);
    }

    public bool TryApplyWallData(WallData wallData, float minimumLength)
    {
        if (wallData == null)
        {
            return false;
        }

        if (!TryGetFlatGeometry(wallData.startPoint, wallData.endPoint, minimumLength, out Vector3 flatDirection, out float length))
        {
            return false;
        }

        Vector3 midpoint = (wallData.startPoint + wallData.endPoint) * 0.5f;
        midpoint.y = wallData.centerY;

        transform.SetPositionAndRotation(
            midpoint,
            Quaternion.LookRotation(flatDirection.normalized, Vector3.up));
        transform.localScale = new Vector3(wallData.thickness, wallData.height, length);

        ApplyWallTextureTiling();
        RefreshTopFaceVisual();
        RefreshEndCapVisuals();
        return true;
    }

    public Material GetTopMaterial()
    {
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        return topFaceVisual != null ? topFaceVisual.TopMaterial : null;
    }

    public void SetTopMaterial(Material material)
    {
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        if (topFaceVisual == null)
        {
            topFaceVisual = gameObject.AddComponent<WallTopFaceVisual>();
        }

        topFaceVisual.SetTopMaterial(material);
        RefreshEndCapVisuals();
    }

    public void SetTopFaceOffset(float offset)
    {
        topFaceWorldOffset = Mathf.Max(0f, offset);
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        if (topFaceVisual == null)
        {
            topFaceVisual = gameObject.AddComponent<WallTopFaceVisual>();
        }

        topFaceVisual.SetWorldOffset(topFaceWorldOffset);
        RefreshEndCapVisuals();
    }

    public void RefreshTopFaceVisual()
    {
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        if (topFaceVisual != null)
        {
            topFaceVisual.Refresh();
        }
    }

    public void RefreshEndCapVisuals()
    {
        AttachToWall();
        if (observedWall == null)
        {
            return;
        }

        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
        Material sharedMaterial = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
        bool visible = sharedMaterial != null;
        ApplyWallTextureTiling();

        EnsureEndCap(ref startCapTransform, ref startCapFilter, ref startCapRenderer, StartCapObjectName);
        EnsureEndCap(ref endCapTransform, ref endCapFilter, ref endCapRenderer, EndCapObjectName);

        if (startCapRenderer != null)
        {
            startCapRenderer.sharedMaterial = sharedMaterial;
            startCapRenderer.gameObject.SetActive(visible && !observedWall.SuppressStartHandle);
        }

        if (endCapRenderer != null)
        {
            endCapRenderer.sharedMaterial = sharedMaterial;
            endCapRenderer.gameObject.SetActive(visible && !observedWall.SuppressEndHandle);
        }

        float length = Mathf.Max(0.0001f, transform.localScale.z);
        float startExtension = observedWall.SuppressStartHandle ? 0f : observedWall.CalculateEndpointExtension(true);
        float endExtension = observedWall.SuppressEndHandle ? 0f : observedWall.CalculateEndpointExtension(false);

        ApplyCapVisual(startCapTransform, startCapRenderer, startExtension, length, -0.5f, visible && !observedWall.SuppressStartHandle);
        ApplyCapVisual(endCapTransform, endCapRenderer, endExtension, length, 0.5f, visible && !observedWall.SuppressEndHandle);
    }

    private void AttachToWall()
    {
        Wall wall = GetComponent<Wall>();
        if (observedWall == wall && observedData == wall?.Data)
        {
            return;
        }

        DetachFromData();
        observedWall = wall;
        observedData = wall != null ? wall.Data : null;
        if (observedData != null)
        {
            observedData.Changed += HandleWallDataChanged;
        }
    }

    private void DetachFromData()
    {
        if (observedData != null)
        {
            observedData.Changed -= HandleWallDataChanged;
        }

        observedData = null;
        observedWall = null;
    }

    private void HandleWallDataChanged()
    {
        RefreshFromData(DefaultObservedMinimumLength);
    }

    private void ApplyCapVisual(
        Transform capTransform,
        MeshRenderer capRenderer,
        float extension,
        float wallLength,
        float localZ,
        bool shouldBeVisible)
    {
        if (capTransform == null)
        {
            return;
        }

        bool hasExtension = extension > MinimumExtension;
        if (capRenderer != null)
        {
            capRenderer.gameObject.SetActive(shouldBeVisible && hasExtension);
        }

        if (!shouldBeVisible || !hasExtension)
        {
            return;
        }

        float capDepthRatio = (extension * 2f) / wallLength;
        capTransform.localPosition = new Vector3(0f, 0f, localZ);
        capTransform.localRotation = Quaternion.identity;
        capTransform.localScale = new Vector3(1f, 1f, capDepthRatio);

        WallTopFaceVisual capTopVisual = capTransform.GetComponent<WallTopFaceVisual>();
        if (capTopVisual == null)
        {
            capTopVisual = capTransform.gameObject.AddComponent<WallTopFaceVisual>();
        }

        capTopVisual.SetTopMaterial(GetTopMaterial());
        capTopVisual.SetWorldOffset(topFaceWorldOffset);
        WallTextureTilingUtility.ApplyCapTiling(capRenderer, capTransform.lossyScale);
    }

    private void ApplyWallTextureTiling()
    {
        WallTextureTilingUtility.ApplyWallTiling(GetComponent<MeshRenderer>(), transform.localScale);
    }

    private void EnsureEndCap(
        ref Transform capTransform,
        ref MeshFilter meshFilter,
        ref MeshRenderer meshRenderer,
        string objectName)
    {
        if (capTransform == null)
        {
            Transform existing = transform.Find(objectName);
            capTransform = existing != null ? existing : new GameObject(objectName).transform;
            capTransform.SetParent(transform, false);
            capTransform.gameObject.layer = gameObject.layer;
        }

        if (meshFilter == null)
        {
            meshFilter = capTransform.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = capTransform.gameObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = capTransform.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = capTransform.gameObject.AddComponent<MeshRenderer>();
            }
        }

        meshFilter.sharedMesh = WallMeshReferenceUtility.GetSharedCubeMesh();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        meshRenderer.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.Object;
    }

    private static bool TryGetFlatGeometry(Vector3 start, Vector3 end, float minimumLength, out Vector3 flatDirection, out float length)
    {
        flatDirection = end - start;
        flatDirection.y = 0f;
        length = flatDirection.magnitude;
        return length >= minimumLength;
    }
}
