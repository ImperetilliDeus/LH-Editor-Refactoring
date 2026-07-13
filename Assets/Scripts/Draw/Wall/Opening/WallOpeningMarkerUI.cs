using UnityEngine;

public class WallOpeningMarkerUI : MonoBehaviour
{
    private const float MarkerThickness = 3f;
    private const float SelectedMarkerThickness = 5f;
    private const float MarkerHitPadding = 18f;
    private static readonly Color DoorMarkerColor = new Color32(99, 58, 29, 255);
    private static readonly Color WindowMarkerColor = new Color32(70, 150, 214, 255);
    private static readonly Color SelectedMarkerColor = new Color32(255, 190, 92, 255);

    [SerializeField] private WallOpening ownerOpening;
    [SerializeField] private WallOpeningPlacementManager placementManager;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Transform startAnchor;
    [SerializeField] private Transform endAnchor;

    private RectTransform markerRootRect;
    private RectTransform markerContentRect;
    private TopPlanSegmentBatchGraphic markerGraphic;
    private Coroutine deferredRefreshRoutine;

    public void Initialize(
        WallOpening opening,
        WallOpeningPlacementManager manager,
        Canvas canvas,
        Camera camera,
        Transform start,
        Transform end)
    {
        ownerOpening = opening;
        placementManager = manager;
        targetCanvas = canvas;
        worldCamera = camera;
        startAnchor = start;
        endAnchor = end;
        EnsureUI();
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
        if (markerContentRect == null || markerGraphic == null)
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
        markerGraphic = null;

        GameObject markerGO = new GameObject(
            "OpeningMarkerLine",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TopPlanSegmentBatchGraphic));
        markerGO.transform.SetParent(markerRootRect, false);
        ApplyLayerRecursively(markerGO);

        markerContentRect = markerGO.GetComponent<RectTransform>();
        markerContentRect.SetParent(markerRootRect, false);
        markerContentRect.anchorMin = new Vector2(0f, 0f);
        markerContentRect.anchorMax = new Vector2(1f, 1f);
        markerContentRect.pivot = new Vector2(0.5f, 0.5f);
        markerContentRect.anchoredPosition = Vector2.zero;
        markerContentRect.offsetMin = Vector2.zero;
        markerContentRect.offsetMax = Vector2.zero;

        markerGraphic = markerGO.GetComponent<TopPlanSegmentBatchGraphic>();
        markerGraphic.raycastTarget = false;
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

        Vector3 startScreen = EditorScreenCoordinateUtility.ToUnityScreenPoint(
            sourceCamera,
            sourceCamera.WorldToScreenPoint(startAnchor.position));
        Vector3 endScreen = EditorScreenCoordinateUtility.ToUnityScreenPoint(
            sourceCamera,
            sourceCamera.WorldToScreenPoint(endAnchor.position));
        bool visible = startScreen.z > 0f && endScreen.z > 0f;
        markerRootRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        bool isSelected = placementManager != null && placementManager.SelectedOpening == ownerOpening;
        float thickness = isSelected ? SelectedMarkerThickness : MarkerThickness;

        RectTransform canvasRect = targetCanvas.transform as RectTransform;
        if (canvasRect == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, startScreen, uiCamera, out Vector2 startPoint) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, endScreen, uiCamera, out Vector2 endPoint))
        {
            markerRootRect.gameObject.SetActive(false);
            return;
        }

        Vector2 delta = endPoint - startPoint;
        float width = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        markerRootRect.anchoredPosition = (startPoint + endPoint) * 0.5f;
        markerRootRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        markerRootRect.sizeDelta = new Vector2(width + thickness, thickness + MarkerHitPadding);
        markerRootRect.localScale = Vector3.one;
        markerRootRect.SetAsLastSibling();
        markerContentRect.localRotation = Quaternion.identity;
        markerContentRect.localScale = Vector3.one;

        if (markerGraphic == null)
        {
            return;
        }

        TopPlanSegmentBatchGraphic.SegmentData segment = new TopPlanSegmentBatchGraphic.SegmentData
        {
            start = new Vector2(-(width * 0.5f), 0f),
            end = new Vector2(width * 0.5f, 0f),
            thickness = thickness,
            color = isSelected ? SelectedMarkerColor : GetMarkerColor(),
            dashed = false,
            dashLength = 0f,
            gapLength = 0f,
            wall = null,
        };

        markerGraphic.SetSegments(new[] { segment });
    }

    private Color GetMarkerColor()
    {
        return ownerOpening != null && ownerOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? DoorMarkerColor
            : WindowMarkerColor;
    }
}
