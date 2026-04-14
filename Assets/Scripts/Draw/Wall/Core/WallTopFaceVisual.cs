using UnityEngine;

public class WallTopFaceVisual : MonoBehaviour
{
    private const string OverlayObjectName = "WallTopFaceOverlay";
    private const float MinimumHeight = 0.0001f;

    [SerializeField] private Material topMaterial;
    [SerializeField] private float worldOffset = 0.01f;

    private static Mesh sharedTopFaceMesh;

    private Transform overlayTransform;
    private MeshRenderer overlayRenderer;
    private MeshFilter overlayFilter;

    public Material TopMaterial => topMaterial;

    public void SetTopMaterial(Material material)
    {
        topMaterial = material;
        Refresh();
    }

    public void SetWorldOffset(float offset)
    {
        worldOffset = Mathf.Max(0f, offset);
        Refresh();
    }

    public void Refresh()
    {
        EnsureOverlay();
        if (overlayTransform == null || overlayRenderer == null)
        {
            return;
        }

        bool visible = topMaterial != null;
        if (overlayRenderer.gameObject.activeSelf != visible)
        {
            overlayRenderer.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        overlayRenderer.sharedMaterial = topMaterial;

        float wallHeight = Mathf.Max(MinimumHeight, transform.localScale.y);
        overlayTransform.localPosition = new Vector3(0f, 0.5f + (worldOffset / wallHeight), 0f);
        overlayTransform.localRotation = Quaternion.identity;
        overlayTransform.localScale = Vector3.one;
    }

    private void Awake()
    {
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnValidate()
    {
        worldOffset = Mathf.Max(0f, worldOffset);
        Refresh();
    }

    private void EnsureOverlay()
    {
        if (overlayTransform == null)
        {
            Transform existing = transform.Find(OverlayObjectName);
            overlayTransform = existing != null ? existing : new GameObject(OverlayObjectName).transform;
            overlayTransform.SetParent(transform, false);
            overlayTransform.gameObject.layer = gameObject.layer;
        }

        if (overlayFilter == null)
        {
            overlayFilter = overlayTransform.GetComponent<MeshFilter>();
            if (overlayFilter == null)
            {
                overlayFilter = overlayTransform.gameObject.AddComponent<MeshFilter>();
            }
        }

        if (overlayRenderer == null)
        {
            overlayRenderer = overlayTransform.GetComponent<MeshRenderer>();
            if (overlayRenderer == null)
            {
                overlayRenderer = overlayTransform.gameObject.AddComponent<MeshRenderer>();
            }
        }

        overlayFilter.sharedMesh = GetSharedTopFaceMesh();
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        overlayRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        overlayRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private static Mesh GetSharedTopFaceMesh()
    {
        if (sharedTopFaceMesh != null)
        {
            return sharedTopFaceMesh;
        }

        Mesh mesh = new Mesh
        {
            name = "WallTopFaceMesh",
        };

        mesh.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, -0.5f),
        };
        mesh.normals = new[]
        {
            Vector3.up,
            Vector3.up,
            Vector3.up,
            Vector3.up,
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f),
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();

        sharedTopFaceMesh = mesh;
        return sharedTopFaceMesh;
    }
}
