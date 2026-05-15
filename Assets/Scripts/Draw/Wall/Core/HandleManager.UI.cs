using UnityEngine;
using UnityEngine.UI;

public partial class HandleManager
{
    public bool IsPointerOverHandle(Vector2 screenPoint)
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null || group.handleRect == null)
            {
                continue;
            }

            if (ContainsScreenPoint(group.handleRect, screenPoint))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateHandlePositions()
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null || group.handleRect == null)
            {
                continue;
            }

            if (group.endpoints.Count == 0 || !IsValidHandleWorldPoint(group.worldPoint))
            {
                group.handleRect.gameObject.SetActive(false);
                continue;
            }

            SetHandleScreenPosition(group.handleRect, group.worldPoint);
        }
    }

    private void SetHandlesVisible(bool visible)
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            if (vertexGroups[i]?.handleRect != null)
            {
                vertexGroups[i].handleRect.gameObject.SetActive(visible);
            }
        }
    }

    private bool ContainsScreenPoint(RectTransform rect, Vector2 screenPoint)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        Camera uiCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, uiCamera);
    }

    private void SetGroupColor(VertexGroup group, Color color)
    {
        if (group == null || group.image == null)
        {
            return;
        }

        group.image.color = color;
    }

    private void SetHandleScreenPosition(RectTransform handleRect, Vector3 worldPoint)
    {
        if (handleRect == null || mainCamera == null || !IsValidHandleWorldPoint(worldPoint))
        {
            if (handleRect != null)
            {
                handleRect.gameObject.SetActive(false);
            }

            return;
        }

        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPoint);
        bool visible = isDefaultModeActive && screenPosition.z > 0f && IsValidScreenPoint(screenPosition);
        handleRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        if (targetCanvas == null || targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            handleRect.position = screenPosition;
            handleRect.SetAsLastSibling();
            return;
        }

        RectTransform canvasRect = targetCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            handleRect.position = screenPosition;
            return;
        }

        Camera uiCamera = targetCanvas.worldCamera != null ? targetCanvas.worldCamera : mainCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPoint))
        {
            handleRect.anchoredPosition = localPoint;
            handleRect.SetAsLastSibling();
        }
    }

    private static bool IsValidScreenPoint(Vector3 screenPosition)
    {
        return !float.IsNaN(screenPosition.x) &&
               !float.IsNaN(screenPosition.y) &&
               !float.IsNaN(screenPosition.z) &&
               !float.IsInfinity(screenPosition.x) &&
               !float.IsInfinity(screenPosition.y) &&
               !float.IsInfinity(screenPosition.z);
    }

    private RectTransform CreateHandleRect(string handleName, out Image image)
    {
        GameObject handleObject = new GameObject(handleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleObject.SetActive(isDefaultModeActive);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.SetParent(targetCanvas.transform, false);
        handleRect.SetAsLastSibling();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = handleSize;

        image = handleObject.GetComponent<Image>();
        if (circularHandleSprite == null)
        {
            circularHandleSprite = CreateCircularSprite(64);
        }

        image.sprite = circularHandleSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;

        return handleRect;
    }

    private Sprite CreateCircularSprite(int size)
    {
        int safeSize = Mathf.Max(8, size);
        Texture2D texture = new Texture2D(safeSize, safeSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color32[] pixels = new Color32[safeSize * safeSize];
        float radius = (safeSize - 1) * 0.5f;
        float radiusSqr = radius * radius;
        float center = radius;

        for (int y = 0; y < safeSize; y++)
        {
            for (int x = 0; x < safeSize; x++)
            {
                float dx = x - center;
                float dy = y - center;
                bool insideCircle = dx * dx + dy * dy <= radiusSqr;
                pixels[y * safeSize + x] = insideCircle ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, safeSize, safeSize),
            new Vector2(0.5f, 0.5f),
            safeSize);
    }

    private void EnsureCanvas()
    {
        if (targetCanvas != null)
        {
            return;
        }

        Canvas handleCanvas = LayerUtility.FindCanvasByName(LayerUtility.DefaultHandleCanvasName);
        if (handleCanvas != null)
        {
            targetCanvas = handleCanvas;
            return;
        }

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == HandleCanvasName)
            {
                targetCanvas = canvas;
                return;
            }
        }

        GameObject canvasObject = new GameObject(HandleCanvasName);
        targetCanvas = canvasObject.AddComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
    }

    private void OnDestroy()
    {
        UnbindModeEvents();
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterGlobalHandler(this);
        }

        previewSnappedGroup = null;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            if (vertexGroups[i]?.handleRect != null)
            {
                Destroy(vertexGroups[i].handleRect.gameObject);
            }
        }

        if (circularHandleSprite != null)
        {
            Texture2D spriteTexture = circularHandleSprite.texture;
            Destroy(circularHandleSprite);
            circularHandleSprite = null;

            if (spriteTexture != null)
            {
                Destroy(spriteTexture);
            }
        }

        vertexGroups.Clear();
        groupsByVertexId.Clear();
        wallEntries.Clear();
    }
}
