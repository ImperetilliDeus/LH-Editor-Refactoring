using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WallSelectionUIProxy : MonoBehaviour
{
    private sealed class WallSelectionUIInput : MonoBehaviour, IPointerClickHandler
    {
        private WallSelectionUIProxy owner;

        public void Initialize(WallSelectionUIProxy proxy)
        {
            owner = proxy;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            owner?.HandlePointerClick();
        }
    }

    [SerializeField] private Wall ownerWall;
    [SerializeField] private WallSelectionManager selectionManager;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Camera worldCamera;

    private RectTransform rootRect;
    private Image rootImage;
    private RectTransform startCapRect;
    private RectTransform endCapRect;
    private Image startCapImage;
    private Image endCapImage;
    private bool isSelected;
    private bool hasTemporaryStyleOverride;
    private Color temporaryFillColor;
    private Color temporaryOutlineColor;
    private Vector2 temporaryOutlineDistance;
    private float temporaryThicknessMultiplier = 1f;
    private bool temporaryShowEndCaps;

    private void Awake()
    {
        ownerWall = GetComponent<Wall>();
        EnsureReferences();
        EnsureUI();
        RefreshVisual();
    }

    private void OnEnable()
    {
        RefreshVisual();
    }

    private void OnDestroy()
    {
        if (rootRect != null)
        {
            Destroy(rootRect.gameObject);
        }
    }

    public void Initialize(WallSelectionManager manager)
    {
        selectionManager = manager;
        if (selectionManager != null)
        {
            targetCanvas = selectionManager.WallSelectionCanvas;
            worldCamera = selectionManager.SelectionCamera;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        EnsureReferences();
        EnsureUI();
        ApplyInteractionState();
        ApplyCurrentStyle();
    }

    public void SetTemporaryVisualOverride(Color fillColor, Color outlineColor, Vector2 outlineDistance, float thicknessMultiplier, bool showEndCaps)
    {
        hasTemporaryStyleOverride = true;
        temporaryFillColor = fillColor;
        temporaryOutlineColor = outlineColor;
        temporaryOutlineDistance = outlineDistance;
        temporaryThicknessMultiplier = Mathf.Max(1f, thicknessMultiplier);
        temporaryShowEndCaps = showEndCaps;
        EnsureReferences();
        EnsureUI();
        ApplyCurrentStyle();
    }

    public void ClearTemporaryVisualOverride()
    {
        if (!hasTemporaryStyleOverride)
        {
            return;
        }

        hasTemporaryStyleOverride = false;
        EnsureReferences();
        EnsureUI();
        ApplyCurrentStyle();
    }

    public void RefreshVisual()
    {
        EnsureReferences();
        EnsureUI();
        if (ownerWall == null || selectionManager == null || rootRect == null || targetCanvas == null)
        {
            return;
        }

        rootRect.name = GetWallUIObjectName();

        Camera sourceCamera = worldCamera != null ? worldCamera : Camera.main;
        if (sourceCamera == null)
        {
            return;
        }

        GetVisualEndpoints(out Vector3 visualStart, out Vector3 visualEnd);
        Vector3 startScreen = EditorScreenCoordinateUtility.ToUnityScreenPoint(
            sourceCamera,
            sourceCamera.WorldToScreenPoint(visualStart));
        Vector3 endScreen = EditorScreenCoordinateUtility.ToUnityScreenPoint(
            sourceCamera,
            sourceCamera.WorldToScreenPoint(visualEnd));
        bool visible = ownerWall.gameObject.activeInHierarchy && startScreen.z > 0f && endScreen.z > 0f;
        rootRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas.worldCamera != null ? targetCanvas.worldCamera : sourceCamera;
        RectTransform canvasRect = targetCanvas.transform as RectTransform;
        Vector2 startPoint = startScreen;
        Vector2 endPoint = endScreen;

        if (canvasRect != null)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, startScreen, uiCamera, out startPoint) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, endScreen, uiCamera, out endPoint))
            {
                rootRect.gameObject.SetActive(false);
                return;
            }

            rootRect.anchoredPosition = (startPoint + endPoint) * 0.5f;
        }
        else
        {
            rootRect.position = (startPoint + endPoint) * 0.5f;
        }

        Vector2 delta = endPoint - startPoint;
        float width = Mathf.Max(1f, delta.magnitude);
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        rootRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        float thicknessMultiplier = hasTemporaryStyleOverride ? temporaryThicknessMultiplier : 1f;
        float thickness = Mathf.Max(selectionManager.WallUIThicknessPixels, selectionManager.WallUIThicknessPixels * thicknessMultiplier);
        rootRect.sizeDelta = new Vector2(width, thickness);
        rootRect.localScale = Vector3.one;
        WallSelectionCanvasOrderingUtility.PlaceBelowSelectableControls(rootRect, targetCanvas.transform);
        if (rootImage != null)
        {
            ApplyInteractionState();
        }

        UpdateEndCaps(width, thickness);
        ApplyCurrentStyle();
    }

    public void DestroyUI()
    {
        if (rootRect != null)
        {
            rootRect.gameObject.SetActive(false);
            Destroy(rootRect.gameObject);
            rootRect = null;
            rootImage = null;
            startCapRect = null;
            endCapRect = null;
            startCapImage = null;
            endCapImage = null;
        }
    }

    private void HandlePointerClick()
    {
        if (selectionManager == null || ownerWall == null)
        {
            return;
        }

        if (selectionManager.IsPointerBlockedByNonWallUI(rootRect != null ? rootRect.gameObject : null))
        {
            return;
        }

        selectionManager.HandleWallUIClick(ownerWall.gameObject);
    }

    private void EnsureReferences()
    {
        if (ownerWall == null)
        {
            ownerWall = GetComponent<Wall>();
        }

        if (selectionManager != null)
        {
            if (targetCanvas == null)
            {
                targetCanvas = selectionManager.WallSelectionCanvas;
            }

            if (worldCamera == null)
            {
                worldCamera = selectionManager.SelectionCamera;
            }
        }
    }

    private void EnsureUI()
    {
        if (targetCanvas == null || rootRect != null)
        {
            return;
        }

        bool isPreviewWall = WallHierarchyUtility.IsPreviewWall(ownerWall);
        GameObject rootObject = isPreviewWall
            ? new GameObject(GetWallUIObjectName(), typeof(RectTransform))
            : new GameObject(GetWallUIObjectName(), typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(targetCanvas.transform, false);
        LayerUtility.ApplyLayer(rootObject, LayerUtility.WallUILayerName, false);

        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        if (!isPreviewWall)
        {
            rootImage = rootObject.GetComponent<Image>();
            rootImage.color = Color.clear;
            rootImage.raycastTarget = true;
        }

        startCapRect = CreateEndCap("StartCap");
        endCapRect = CreateEndCap("EndCap");
        startCapImage = startCapRect != null ? startCapRect.GetComponent<Image>() : null;
        endCapImage = endCapRect != null ? endCapRect.GetComponent<Image>() : null;

        WallSelectionUIInput input = rootObject.AddComponent<WallSelectionUIInput>();
        input.Initialize(this);
    }

    private void ApplyInteractionState()
    {
        if (rootImage == null)
        {
            return;
        }

        rootImage.raycastTarget = selectionManager != null && selectionManager.IsWallUIInteractionEnabled;
    }

    private void ApplyCurrentStyle()
    {
        if (WallHierarchyUtility.IsPreviewWall(ownerWall))
        {
            Color previewFillColor = new Color(0.2f, 0.8f, 1f, 0.28f);
            Color previewOutlineColor = new Color(0.2f, 0.9f, 1f, 0.95f);
            if (rootImage != null)
            {
                rootImage.color = previewFillColor;
            }

            ApplyEndCapStyle(previewOutlineColor, true);
            return;
        }

        if (rootImage == null)
        {
            return;
        }

        if (hasTemporaryStyleOverride)
        {
            rootImage.color = Color.clear;
            ApplyEndCapStyle(Color.clear, false);
            return;
        }

        if (selectionManager == null)
        {
            return;
        }

        rootImage.color = Color.clear;
        ApplyEndCapStyle(Color.clear, false);
    }

    private RectTransform CreateEndCap(string objectName)
    {
        if (rootRect == null)
        {
            return null;
        }

        GameObject capObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        RectTransform capRect = capObject.GetComponent<RectTransform>();
        capRect.SetParent(rootRect, false);
        capRect.anchorMin = new Vector2(0f, 0.5f);
        capRect.anchorMax = new Vector2(0f, 0.5f);
        capRect.pivot = new Vector2(0.5f, 0.5f);
        capRect.anchoredPosition = Vector2.zero;
        capRect.sizeDelta = new Vector2(10f, 10f);
        capObject.GetComponent<Image>().raycastTarget = false;
        return capRect;
    }

    private void UpdateEndCaps(float width, float thickness)
    {
        if (startCapRect == null || endCapRect == null)
        {
            return;
        }

        float capSize = GetEndCapSize(thickness);
        startCapRect.anchorMin = new Vector2(0f, 0.5f);
        startCapRect.anchorMax = new Vector2(0f, 0.5f);
        endCapRect.anchorMin = new Vector2(1f, 0.5f);
        endCapRect.anchorMax = new Vector2(1f, 0.5f);
        startCapRect.anchoredPosition = Vector2.zero;
        endCapRect.anchoredPosition = Vector2.zero;
        startCapRect.sizeDelta = new Vector2(capSize, capSize);
        endCapRect.sizeDelta = new Vector2(capSize, capSize);
    }

    private float GetEndCapSize(float thickness)
    {
        if (WallHierarchyUtility.IsPreviewWall(ownerWall) && selectionManager != null)
        {
            float baseSize = Mathf.Max(
                selectionManager.PreviewWallUIEndCapMinSize,
                thickness + selectionManager.PreviewWallUIEndCapPadding);
            return baseSize * selectionManager.PreviewWallUIEndCapSizeMultiplier;
        }

        return Mathf.Max(8f, thickness + 4f);
    }

    private void ApplyEndCapStyle(Color color, bool visible)
    {
        if (startCapImage != null)
        {
            startCapImage.color = color;
            startCapImage.enabled = visible;
        }

        if (endCapImage != null)
        {
            endCapImage.color = color;
            endCapImage.enabled = visible;
        }
    }

    private string GetWallUIObjectName()
    {
        string wallName = ownerWall != null && !string.IsNullOrWhiteSpace(ownerWall.name)
            ? ownerWall.name
            : "wall";
        return $"{wallName}_ui";
    }

    private void GetVisualEndpoints(out Vector3 start, out Vector3 end)
    {
        WallOpeningContainer container = ownerWall != null ? ownerWall.GetComponentInParent<WallOpeningContainer>() : null;
        if (container != null)
        {
            start = container.WallStart;
            end = container.WallEnd;
            return;
        }

        start = ownerWall != null ? ownerWall.Data.startPoint : Vector3.zero;
        end = ownerWall != null ? ownerWall.Data.endPoint : Vector3.zero;
    }
}
