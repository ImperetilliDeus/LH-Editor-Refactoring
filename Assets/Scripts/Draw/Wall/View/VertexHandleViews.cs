using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Screen-space vertex handle images shown by HandleManager. Canvas and camera are passed per call because the host
// can replace them after construction (scene wiring, tests).
public sealed class VertexHandleViews
{
    private Sprite circularSprite;

    public static Canvas ResolveCanvas(Canvas current, string fallbackCanvasName)
    {
        if (current != null)
        {
            return current;
        }

        Canvas handleCanvas = LayerUtility.FindCanvasByName(LayerUtility.DefaultHandleCanvasName);
        if (handleCanvas != null)
        {
            return handleCanvas;
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == fallbackCanvasName)
            {
                return canvas;
            }
        }

        GameObject canvasObject = new GameObject(fallbackCanvasName);
        Canvas createdCanvas = canvasObject.AddComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        return createdCanvas;
    }

    public RectTransform Create(Canvas canvas, string handleName, Vector2 size, bool active, out Image image)
    {
        GameObject handleObject = new GameObject(handleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleObject.SetActive(active);
        RectTransform handleRect = handleObject.GetComponent<RectTransform>();
        handleRect.SetParent(canvas.transform, false);
        handleRect.SetAsLastSibling();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = size;

        image = handleObject.GetComponent<Image>();
        if (circularSprite == null)
        {
            circularSprite = CreateCircularSprite(64);
        }

        image.sprite = circularSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        image.raycastTarget = false;

        return handleRect;
    }

    // Shows the handle at worldPoint when allowed and in front of the camera; hides it otherwise.
    public static void Place(RectTransform handleRect, Vector3 worldPoint, bool allowedVisible, Camera camera, Canvas canvas)
    {
        if (handleRect == null || camera == null || !HandleGeometryUtility.IsValidWorldPoint(worldPoint))
        {
            if (handleRect != null)
            {
                handleRect.gameObject.SetActive(false);
            }

            return;
        }

        Vector3 screenPosition = EditorScreenCoordinateUtility.ToUnityScreenPoint(
            camera,
            camera.WorldToScreenPoint(worldPoint));
        bool visible = allowedVisible &&
                       screenPosition.z > 0f &&
                       IsValidScreenPoint(screenPosition);
        handleRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        RectTransform canvasRect = handleRect.parent as RectTransform;
        if (canvasRect == null)
        {
            handleRect.position = screenPosition;
            return;
        }

        handleRect.anchoredPosition = EditorScreenCoordinateUtility.ScreenPointToAnchoredPosition(
            canvasRect,
            canvas,
            screenPosition,
            camera);
        handleRect.SetAsLastSibling();
    }

    public static bool ContainsScreenPoint(RectTransform rect, Canvas canvas, Vector2 screenPoint)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy)
        {
            return false;
        }

        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, uiCamera);
    }

    public static void Destroy(RectTransform handleRect)
    {
        DestroyObject(handleRect != null ? handleRect.gameObject : null, null);
    }

    // Destroys the given handles plus any leftover children of the canvas whose name starts with namePrefix.
    public static void DestroyAll(IReadOnlyList<RectTransform> handleRects, Canvas canvas, string namePrefix)
    {
        HashSet<GameObject> destroyedObjects = new HashSet<GameObject>();
        for (int i = 0; i < handleRects.Count; i++)
        {
            DestroyObject(handleRects[i] != null ? handleRects[i].gameObject : null, destroyedObjects);
        }

        if (canvas == null)
        {
            return;
        }

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child != null && child.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
            {
                DestroyObject(child.gameObject, destroyedObjects);
            }
        }
    }

    public void ReleaseSprite()
    {
        if (circularSprite == null)
        {
            return;
        }

        Texture2D spriteTexture = circularSprite.texture;
        Object.Destroy(circularSprite);
        circularSprite = null;

        if (spriteTexture != null)
        {
            Object.Destroy(spriteTexture);
        }
    }

    private static void DestroyObject(GameObject handleObject, HashSet<GameObject> destroyedObjects)
    {
        if (handleObject == null)
        {
            return;
        }

        if (destroyedObjects != null && !destroyedObjects.Add(handleObject))
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(handleObject);
        }
        else
        {
            Object.DestroyImmediate(handleObject);
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

    private static Sprite CreateCircularSprite(int size)
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
}
