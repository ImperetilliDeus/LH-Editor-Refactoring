using UnityEngine;

public class WallVisual : MonoBehaviour
{
    private const string StartCapObjectName = "WallStartCap";
    private const string EndCapObjectName = "WallEndCap";
    private const float MinimumExtension = 0.0001f;

    [SerializeField] private float topFaceWorldOffset = Wall.DefaultTopFaceOffset;

    private static Mesh sharedCubeMesh;

    private Transform startCapTransform;
    private Transform endCapTransform;
    private MeshRenderer startCapRenderer;
    private MeshRenderer endCapRenderer;
    private MeshFilter startCapFilter;
    private MeshFilter endCapFilter;

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
        if (!TryGetComponent(out Wall wall))
        {
            return;
        }

        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
        Material sharedMaterial = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
        bool visible = sharedMaterial != null;

        EnsureEndCap(ref startCapTransform, ref startCapFilter, ref startCapRenderer, StartCapObjectName);
        EnsureEndCap(ref endCapTransform, ref endCapFilter, ref endCapRenderer, EndCapObjectName);

        if (startCapRenderer != null)
        {
            startCapRenderer.sharedMaterial = sharedMaterial;
            startCapRenderer.gameObject.SetActive(visible && !wall.SuppressStartHandle);
        }

        if (endCapRenderer != null)
        {
            endCapRenderer.sharedMaterial = sharedMaterial;
            endCapRenderer.gameObject.SetActive(visible && !wall.SuppressEndHandle);
        }

        float length = Mathf.Max(0.0001f, transform.localScale.z);
        float startExtension = wall.SuppressStartHandle ? 0f : wall.CalculateEndpointExtension(true);
        float endExtension = wall.SuppressEndHandle ? 0f : wall.CalculateEndpointExtension(false);

        ApplyCapVisual(startCapTransform, startCapRenderer, startExtension, length, -0.5f, visible && !wall.SuppressStartHandle);
        ApplyCapVisual(endCapTransform, endCapRenderer, endExtension, length, 0.5f, visible && !wall.SuppressEndHandle);
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

        meshFilter.sharedMesh = GetSharedCubeMesh();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        meshRenderer.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.Object;
    }

    private static Mesh GetSharedCubeMesh()
    {
        if (sharedCubeMesh != null)
        {
            return sharedCubeMesh;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter meshFilter = cube.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            sharedCubeMesh = meshFilter.sharedMesh;
        }

        if (Application.isPlaying)
        {
            Destroy(cube);
        }
        else
        {
            DestroyImmediate(cube);
        }

        return sharedCubeMesh;
    }

    private static bool TryGetFlatGeometry(Vector3 start, Vector3 end, float minimumLength, out Vector3 flatDirection, out float length)
    {
        flatDirection = end - start;
        flatDirection.y = 0f;
        length = flatDirection.magnitude;
        return length >= minimumLength;
    }
}
