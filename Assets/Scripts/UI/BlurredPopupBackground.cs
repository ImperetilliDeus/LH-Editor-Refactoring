using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class BlurredPopupBackground : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Camera captureCamera;
    [SerializeField] private RawImage targetImage;
    [SerializeField] private Material blurMaterial;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image colorOverlayImage;

    [Header("Capture")]
    [SerializeField] private int textureWidth = 960;
    [SerializeField] private int textureHeight = 540;
    [SerializeField] private bool matchScreenAspect = true;
    [SerializeField] [Range(0.1f, 1f)] private float renderScale = 0.5f;
    [SerializeField] private bool captureOnEnable = true;
    [SerializeField] private bool captureEveryFrame = true;
    [SerializeField] private bool cropToThisRect = true;
    [SerializeField] private bool copyBackgroundColor = true;
    [SerializeField] private bool useColorOverlay = true;
    [SerializeField] private bool useSrgbOverlayInBlurMaterial = true;

    [Header("Appearance")]
    [SerializeField] [Range(0f, 1f)] private float blurMix = 0.72f;
    [SerializeField] [Range(0f, 1f)] private float overlayOpacityMultiplier = 0.65f;
    [SerializeField] [Range(0f, 2f)] private float saturation = 1.08f;

    private RenderTexture renderTexture;
    private Material runtimeBlurMaterial;

    private void Awake()
    {
        ResolveReferences();
        Configure();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Configure();

        if (captureOnEnable)
        {
            CaptureNow();
        }
    }

    private void LateUpdate()
    {
        if (!captureEveryFrame)
        {
            return;
        }

        Configure();
        CaptureNow();
    }

    private void OnDisable()
    {
        if (captureCamera != null)
        {
            captureCamera.enabled = false;
        }
    }

    private void OnDestroy()
    {
        ReleaseRenderTexture();
        ReleaseRuntimeMaterial();
    }

    public void CaptureNow()
    {
        if (sourceCamera == null || captureCamera == null)
        {
            return;
        }

        CopyCameraSettings(sourceCamera, captureCamera);
        ApplyUvCrop();
        captureCamera.enabled = false;
        captureCamera.Render();
    }

    public void ConfigureForTests(Camera source, Camera capture, RawImage image, int width, int height)
    {
        sourceCamera = source;
        captureCamera = capture;
        targetImage = image;
        textureWidth = width;
        textureHeight = height;
        matchScreenAspect = false;

        Configure();
    }

    private void ResolveReferences()
    {
        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }

        if (targetImage == null)
        {
            targetImage = GetComponent<RawImage>();
        }

        if (targetImage == null)
        {
            targetImage = GetComponentInChildren<RawImage>(true);
        }

        if (targetImage == null)
        {
            targetImage = CreateTargetImage();
        }

        if (useColorOverlay && colorOverlayImage == null)
        {
            colorOverlayImage = FindColorOverlay();
        }

        if (useColorOverlay && colorOverlayImage == null)
        {
            colorOverlayImage = CreateColorOverlay();
        }

        if (targetImage != null)
        {
            targetImage.material = GetBlurMaterial();
            SyncVisualColors();
        }

        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        if (captureCamera == null)
        {
            captureCamera = GetComponentInChildren<Camera>(true);
        }
    }

    private void Configure()
    {
        StretchTargetImage();
        StretchColorOverlay();
        EnsureRenderTexture();

        if (targetImage != null)
        {
            targetImage.texture = renderTexture;
            SyncVisualColors();
            ApplyUvCrop();
        }

        if (captureCamera != null)
        {
            captureCamera.enabled = false;
            captureCamera.targetTexture = renderTexture;
        }
    }

    private RawImage CreateTargetImage()
    {
        GameObject imageObject = new GameObject("BlurredCapture", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.layer = gameObject.layer;
        imageObject.transform.SetParent(transform, false);
        RawImage image = imageObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    private Image FindColorOverlay()
    {
        Transform overlayTransform = transform.Find("ColorOverlay");
        return overlayTransform != null ? overlayTransform.GetComponent<Image>() : null;
    }

    private Image CreateColorOverlay()
    {
        GameObject imageObject = new GameObject("ColorOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = gameObject.layer;
        imageObject.transform.SetParent(transform, false);
        Image image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private void SyncVisualColors()
    {
        if (targetImage != null)
        {
            targetImage.color = Color.white;

            Material material = targetImage.material;
            if (material != null)
            {
                if (material.HasProperty("_TintColor"))
                {
                    material.SetColor("_TintColor", Color.clear);
                }

                if (material.HasProperty("_UseSrgbOverlay"))
                {
                    material.SetFloat("_UseSrgbOverlay", useSrgbOverlayInBlurMaterial ? 1f : 0f);
                }

                if (material.HasProperty("_OverlayColor"))
                {
                    Color overlayColor = copyBackgroundColor && backgroundImage != null
                        ? backgroundImage.color
                        : Color.clear;
                    overlayColor.a *= overlayOpacityMultiplier;
                    material.SetColor("_OverlayColor", overlayColor);
                }

                if (material.HasProperty("_BlurMix"))
                {
                    material.SetFloat("_BlurMix", blurMix);
                }

                if (material.HasProperty("_Saturation"))
                {
                    material.SetFloat("_Saturation", saturation);
                }
            }
        }

        if (!useColorOverlay || colorOverlayImage == null)
        {
            return;
        }

        colorOverlayImage.enabled = copyBackgroundColor && !useSrgbOverlayInBlurMaterial;
        Color colorOverlay = copyBackgroundColor && backgroundImage != null
            ? backgroundImage.color
            : Color.clear;
        colorOverlay.a *= overlayOpacityMultiplier;
        colorOverlayImage.color = colorOverlay;
    }

    private Material GetBlurMaterial()
    {
        if (runtimeBlurMaterial != null)
        {
            return runtimeBlurMaterial;
        }

        if (blurMaterial != null)
        {
            runtimeBlurMaterial = new Material(blurMaterial)
            {
                name = $"{blurMaterial.name} Instance"
            };
            return runtimeBlurMaterial;
        }

        Shader shader = Shader.Find("UI/GaussianBlur");
        if (shader == null)
        {
            return null;
        }

        runtimeBlurMaterial = new Material(shader)
        {
            name = "Runtime UI Gaussian Blur"
        };
        return runtimeBlurMaterial;
    }

    private void ReleaseRuntimeMaterial()
    {
        if (runtimeBlurMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeBlurMaterial);
        }
        else
        {
            DestroyImmediate(runtimeBlurMaterial);
        }

        runtimeBlurMaterial = null;
    }

    private void StretchTargetImage()
    {
        if (targetImage == null)
        {
            return;
        }

        RectTransform rectTransform = targetImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        if (targetImage.transform.parent == transform)
        {
            rectTransform.SetAsFirstSibling();
        }
    }

    private void StretchColorOverlay()
    {
        if (!useColorOverlay || colorOverlayImage == null)
        {
            return;
        }

        RectTransform rectTransform = colorOverlayImage.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        if (colorOverlayImage.transform.parent == transform)
        {
            int blurIndex = targetImage != null && targetImage.transform.parent == transform
                ? targetImage.transform.GetSiblingIndex()
                : -1;
            colorOverlayImage.transform.SetSiblingIndex(Mathf.Max(0, blurIndex + 1));
        }
    }

    private void ApplyUvCrop()
    {
        if (targetImage == null)
        {
            return;
        }

        if (!cropToThisRect)
        {
            targetImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        RectTransform rectTransform = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[2]);
        float screenWidth = Mathf.Max(1f, Screen.width);
        float screenHeight = Mathf.Max(1f, Screen.height);

        float xMin = Mathf.Clamp01(bottomLeft.x / screenWidth);
        float yMin = Mathf.Clamp01(bottomLeft.y / screenHeight);
        float xMax = Mathf.Clamp01(topRight.x / screenWidth);
        float yMax = Mathf.Clamp01(topRight.y / screenHeight);

        targetImage.uvRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void EnsureRenderTexture()
    {
        int width = Mathf.Max(1, matchScreenAspect ? Mathf.RoundToInt(Screen.width * renderScale) : textureWidth);
        int height = Mathf.Max(1, matchScreenAspect ? Mathf.RoundToInt(Screen.height * renderScale) : textureHeight);

        if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
        {
            return;
        }

        ReleaseRenderTexture();
        renderTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            name = "Blurred Popup Background",
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        renderTexture.Create();
    }

    private void ReleaseRenderTexture()
    {
        if (renderTexture == null)
        {
            return;
        }

        if (targetImage != null && targetImage.texture == renderTexture)
        {
            targetImage.texture = null;
        }

        if (captureCamera != null && captureCamera.targetTexture == renderTexture)
        {
            captureCamera.targetTexture = null;
        }

        renderTexture.Release();
        if (Application.isPlaying)
        {
            Destroy(renderTexture);
        }
        else
        {
            DestroyImmediate(renderTexture);
        }

        renderTexture = null;
    }

    private static void CopyCameraSettings(Camera source, Camera target)
    {
        RenderTexture targetTexture = target.targetTexture;
        target.CopyFrom(source);
        target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        target.targetTexture = targetTexture;
        target.enabled = false;
    }
}
