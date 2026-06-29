using UnityEngine;

[DisallowMultipleComponent]
public sealed class DrawingOverlayRuntime : MonoBehaviour
{
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private float surfaceOffset = 0.01f;

    private Material runtimeMaterial;
    private Mesh quadMesh;

    public DrawingOverlayDocument Document { get; private set; }
    public Texture2D DisplayTexture { get; private set; }

    private void Awake()
    {
        EnsureComponents();
    }

    private void OnDestroy()
    {
        if (quadMesh != null)
        {
            Destroy(quadMesh);
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    public void SetDocument(DrawingOverlayDocument document, Texture2D displayTexture, float planeY)
    {
        Document = document;
        DisplayTexture = displayTexture;
        EnsureComponents();
        EnsureResources();
        UpdateVisual(planeY);
    }

    public void ClearDocument()
    {
        Document = null;
        DisplayTexture = null;
        gameObject.SetActive(false);
        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = null;
        }

        if (meshFilter != null)
        {
            meshFilter.sharedMesh = null;
        }
    }

    public void UpdateVisual(float planeY)
    {
        if (Document == null || Document.source == null || Document.calibration == null || Document.solved == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (DisplayTexture == null || Document.solved.unitPerPixel <= 0f)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        EnsureResources();

        float widthUnits = Document.source.pixelWidth * Document.solved.unitPerPixel;
        float heightUnits = Document.source.pixelHeight * Document.solved.unitPerPixel;

        Vector2 centerXZ = DrawingOverlayCalibrationService.PixelToWorldXZ(
            DrawingOverlayCalibrationService.GetImageCenterPixel(Document.source),
            Document);

        transform.position = new Vector3(centerXZ.x, planeY + surfaceOffset, centerXZ.y);
        transform.rotation = Quaternion.Euler(90f, Document.solved.totalRotationDeg, 0f);
        transform.localScale = new Vector3(widthUnits, heightUnits, 1f);

        Color color = Color.white;
        color.a = Mathf.Clamp01(Document.calibration.opacity);
        ApplyMaterialState(color, DisplayTexture);
        meshRenderer.sharedMaterial = runtimeMaterial;
        meshFilter.sharedMesh = quadMesh;
    }

    private void EnsureComponents()
    {
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }
    }

    private void EnsureResources()
    {
        if (quadMesh == null)
        {
            quadMesh = new Mesh
            {
                name = "DrawingOverlayQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                },
                triangles = new[]
                {
                    0, 2, 1,
                    2, 3, 1,
                }
            };
            quadMesh.RecalculateBounds();
        }

        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }

            runtimeMaterial = new Material(shader)
            {
                name = "DrawingOverlayRuntimeMaterial",
            };

            if (runtimeMaterial.HasProperty("_Surface"))
            {
                runtimeMaterial.SetFloat("_Surface", 1f);
            }

            if (runtimeMaterial.HasProperty("_Blend"))
            {
                runtimeMaterial.SetFloat("_Blend", 0f);
            }

            if (runtimeMaterial.HasProperty("_SrcBlend"))
            {
                runtimeMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (runtimeMaterial.HasProperty("_DstBlend"))
            {
                runtimeMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (runtimeMaterial.HasProperty("_ZWrite"))
            {
                runtimeMaterial.SetFloat("_ZWrite", 0f);
            }

            if (runtimeMaterial.HasProperty("_Cull"))
            {
                runtimeMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }

            runtimeMaterial.SetOverrideTag("RenderType", "Transparent");
            runtimeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            runtimeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private void ApplyMaterialState(Color color, Texture texture)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        runtimeMaterial.color = color;
        runtimeMaterial.mainTexture = texture;

        if (runtimeMaterial.HasProperty(ColorPropertyId))
        {
            runtimeMaterial.SetColor(ColorPropertyId, color);
        }

        if (runtimeMaterial.HasProperty(BaseColorPropertyId))
        {
            runtimeMaterial.SetColor(BaseColorPropertyId, color);
        }

        if (runtimeMaterial.HasProperty(MainTexPropertyId))
        {
            runtimeMaterial.SetTexture(MainTexPropertyId, texture);
        }

        if (runtimeMaterial.HasProperty(BaseMapPropertyId))
        {
            runtimeMaterial.SetTexture(BaseMapPropertyId, texture);
        }
    }
}
