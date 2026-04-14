using UnityEngine;

public class WallOpeningMarkerUI : MonoBehaviour
{
    [SerializeField] private WallOpening ownerOpening;
    [SerializeField] private WallOpeningPlacementManager placementManager;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform startAnchor;
    [SerializeField] private Transform endAnchor;
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private Vector2 scaleMultiplier = Vector2.one;

    private RectTransform markerRootRect;
    private RectTransform markerContentRect;
    private Vector2 baseSize;
    private Vector3 baseScale = Vector3.one;
    private Coroutine deferredRefreshRoutine;

    public void Initialize(
        WallOpening opening,
        WallOpeningPlacementManager manager,
        Canvas canvas,
        Camera camera,
        Transform start,
        Transform end,
        GameObject prefab,
        Vector2 markerScaleMultiplier)
    {
        ownerOpening = opening;
        placementManager = manager;
        targetCanvas = canvas;
        worldCamera = camera;
        startAnchor = start;
        endAnchor = end;
        Vector2 nextScaleMultiplier = new Vector2(
            Mathf.Max(0.01f, markerScaleMultiplier.x),
            Mathf.Max(0.01f, markerScaleMultiplier.y));
        bool prefabChanged = markerPrefab != prefab;
        bool scaleChanged = scaleMultiplier != nextScaleMultiplier;
        markerPrefab = prefab;
        scaleMultiplier = nextScaleMultiplier;
        EnsureUI();
        if (prefabChanged || scaleChanged)
        {
            RebuildMarkerContent();
        }

        placementManager?.RegisterMarkerUI(this);
        RefreshVisual();
        ScheduleDeferredRefresh();
    }

    private void OnDestroy()
    {
        if (deferredRefreshRoutine != null)
        {
            StopCoroutine(deferredRefreshRoutine);
            deferredRefreshRoutine = null;
        }

        placementManager?.UnregisterMarkerUI(this);
        if (markerRootRect != null)
        {
            Destroy(markerRootRect.gameObject);
        }
    }

    private void ScheduleDeferredRefresh()
    {
        if (!isActiveAndEnabled)
        {
            placementManager?.MarkMarkerVisualsDirty();
            return;
        }

        if (deferredRefreshRoutine != null)
        {
            StopCoroutine(deferredRefreshRoutine);
        }

        deferredRefreshRoutine = StartCoroutine(DeferredRefreshRoutine());
    }

    private System.Collections.IEnumerator DeferredRefreshRoutine()
    {
        Canvas.ForceUpdateCanvases();
        RefreshVisual();

        yield return null;

        Canvas.ForceUpdateCanvases();
        RefreshVisual();
        deferredRefreshRoutine = null;
    }

    private void EnsureUI()
    {
        if (targetCanvas == null)
        {
            return;
        }

        if (markerRootRect == null)
        {
            GameObject rootGO = new GameObject("OpeningMarkerUI", typeof(RectTransform));
            markerRootRect = rootGO.GetComponent<RectTransform>();
            markerRootRect.SetParent(targetCanvas.transform, false);
            markerRootRect.anchorMin = new Vector2(0.5f, 0.5f);
            markerRootRect.anchorMax = new Vector2(0.5f, 0.5f);
            markerRootRect.pivot = new Vector2(0.5f, 0.5f);
            WallOpeningMarkerInput input = rootGO.AddComponent<WallOpeningMarkerInput>();
            input.Initialize(ownerOpening, placementManager);
        }

        ApplyCurrentLayer();
        if (markerContentRect == null)
        {
            RebuildMarkerContent();
        }
    }

    private void RebuildMarkerContent()
    {
        if (markerRootRect == null)
        {
            return;
        }

        if (markerContentRect != null)
        {
            Destroy(markerContentRect.gameObject);
            markerContentRect = null;
        }

        GameObject markerGO;
        if (markerPrefab != null)
        {
            markerGO = Instantiate(markerPrefab, markerRootRect);
            markerGO.name = markerPrefab.name;
            ApplyLayerRecursively(markerGO);
        }
        else
        {
            markerGO = new GameObject("OpeningMarkerUI", typeof(RectTransform));
            markerGO.transform.SetParent(markerRootRect, false);
            ApplyLayerRecursively(markerGO);
        }

        markerContentRect = markerGO.GetComponent<RectTransform>();
        if (markerContentRect == null)
        {
            markerContentRect = markerGO.AddComponent<RectTransform>();
        }

        markerContentRect.SetParent(markerRootRect, false);
        markerContentRect.anchorMin = new Vector2(0f, 0f);
        markerContentRect.anchorMax = new Vector2(1f, 1f);
        markerContentRect.pivot = new Vector2(0.5f, 0.5f);
        markerContentRect.anchoredPosition = Vector2.zero;

        baseSize = markerContentRect.sizeDelta;
        baseScale = markerContentRect.localScale;
        if (baseSize.x <= 0f)
        {
            baseSize.x = 100f;
        }
        if (baseSize.y <= 0f)
        {
            baseSize.y = 100f;
        }
    }

    private void ApplyCurrentLayer()
    {
        if (markerRootRect == null)
        {
            return;
        }

        ApplyLayerRecursively(markerRootRect.gameObject);
    }

    private void ApplyLayerRecursively(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        LayerUtility.ApplyLayer(
            target,
            ownerOpening != null && ownerOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door
                ? LayerUtility.DoorUILayerName
                : LayerUtility.WindowUILayerName,
            true);
    }

    public void RefreshVisual()
    {
        EnsureUI();
        if (markerRootRect == null || markerContentRect == null || targetCanvas == null || startAnchor == null || endAnchor == null)
        {
            return;
        }

        Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : (targetCanvas.worldCamera != null ? targetCanvas.worldCamera : worldCamera);
        Camera sourceCamera = worldCamera != null ? worldCamera : Camera.main;
        if (sourceCamera == null)
        {
            return;
        }

        if (uiCamera == null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = sourceCamera;
        }

        Vector3 startScreen = sourceCamera.WorldToScreenPoint(startAnchor.position);
        Vector3 endScreen = sourceCamera.WorldToScreenPoint(endAnchor.position);
        bool visible = startScreen.z > 0f && endScreen.z > 0f;
        markerRootRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Vector2 startPoint = startScreen;
        Vector2 endPoint = endScreen;
        Vector2 midpoint = (startPoint + endPoint) * 0.5f;
        Vector2 delta = endPoint - startPoint;
        float width = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        float scaledWidth = width * scaleMultiplier.x;
        float scaledHeight = scaledWidth * scaleMultiplier.y;

        WallOpeningPlacementManager.MarkerPlacementMode placementMode =
            placementManager != null
                ? placementManager.GetMarkerPlacementMode(ownerOpening)
                : WallOpeningPlacementManager.MarkerPlacementMode.OffsetFromOpening;

        if (placementMode == WallOpeningPlacementManager.MarkerPlacementMode.OffsetFromOpening)
        {
            Vector2 normal = width > 0.0001f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;
            float offsetDirection = ownerOpening != null &&
                                    ownerOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door &&
                                    ownerOpening.DoorVerticalFlip
                ? -1f
                : 1f;
            midpoint += normal * (scaledHeight * 0.5f * offsetDirection);
        }

        RectTransform canvasRect = targetCanvas.transform as RectTransform;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, midpoint, uiCamera, out Vector2 localPoint))
        {
            markerRootRect.anchoredPosition = localPoint;
        }
        else
        {
            return;
        }

        markerRootRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        markerRootRect.sizeDelta = new Vector2(scaledWidth, scaledHeight);
        markerRootRect.localScale = Vector3.one;

        markerContentRect.sizeDelta = Vector2.zero;
        Vector3 markerScale = baseScale;
        if (ownerOpening != null && ownerOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door)
        {
            markerScale.x = Mathf.Abs(markerScale.x) * (ownerOpening.DoorOpensRight ? -1f : 1f);
            markerScale.y = Mathf.Abs(markerScale.y) * (ownerOpening.DoorVerticalFlip ? -1f : 1f);
        }

        markerContentRect.localScale = markerScale;
    }
}
