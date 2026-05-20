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
    private Outline rootOutline;
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
        if (ownerWall == null || selectionManager == null || rootRect == null || rootImage == null || targetCanvas == null)
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
        Vector3 startScreen = sourceCamera.WorldToScreenPoint(visualStart);
        Vector3 endScreen = sourceCamera.WorldToScreenPoint(visualEnd);
        bool visible = ownerWall.gameObject.activeInHierarchy && startScreen.z > 0f && endScreen.z > 0f;
        rootRect.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Vector2 startPoint = startScreen;
        Vector2 endPoint = endScreen;
        Vector2 midpoint = (startPoint + endPoint) * 0.5f;
        Vector2 delta = endPoint - startPoint;
        float width = Mathf.Max(1f, delta.magnitude);
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
        RectTransform canvasRect = targetCanvas.transform as RectTransform;
        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, midpoint, uiCamera, out Vector2 localPoint))
        {
            rootRect.anchoredPosition = localPoint;
        }
        else
        {
            rootRect.position = midpoint;
        }

        rootRect.localRotation = Quaternion.Euler(0f, 0f, angle);
        float thicknessMultiplier = hasTemporaryStyleOverride ? temporaryThicknessMultiplier : (isSelected ? 1.15f : 1f);
        float thickness = Mathf.Max(selectionManager.WallUIThicknessPixels, selectionManager.WallUIThicknessPixels * thicknessMultiplier);
        rootRect.sizeDelta = new Vector2(width, thickness);
        rootRect.localScale = Vector3.one;
        rootRect.SetAsLastSibling();
        rootImage.raycastTarget = selectionManager.IsWallUIInteractionEnabled;
        UpdateEndCaps(width, thickness);
        ApplyCurrentStyle();
    }

    public void DestroyUI()
    {
        if (rootRect != null)
        {
            Destroy(rootRect.gameObject);
            rootRect = null;
            rootImage = null;
            rootOutline = null;
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

        GameObject rootObject = new GameObject(GetWallUIObjectName(), typeof(RectTransform), typeof(Image), typeof(Outline));
        rootObject.transform.SetParent(targetCanvas.transform, false);
        LayerUtility.ApplyLayer(rootObject, LayerUtility.WallUILayerName, false);

        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        rootImage = rootObject.GetComponent<Image>();
        rootImage.color = selectionManager != null ? selectionManager.WallUINormalColor : new Color(1f, 1f, 1f, 0.04f);
        rootImage.raycastTarget = true;
        rootOutline = rootObject.GetComponent<Outline>();
        rootOutline.useGraphicAlpha = true;

        startCapRect = CreateEndCap("StartCap");
        endCapRect = CreateEndCap("EndCap");
        startCapImage = startCapRect != null ? startCapRect.GetComponent<Image>() : null;
        endCapImage = endCapRect != null ? endCapRect.GetComponent<Image>() : null;

        WallSelectionUIInput input = rootObject.AddComponent<WallSelectionUIInput>();
        input.Initialize(this);
    }

    private void ApplyCurrentStyle()
    {
        if (rootImage == null)
        {
            return;
        }

        if (WallHierarchyUtility.IsPreviewWall(ownerWall))
        {
            Color previewFillColor = new Color(0.2f, 0.8f, 1f, 0.28f);
            Color previewOutlineColor = new Color(0.2f, 0.9f, 1f, 0.95f);
            rootImage.color = previewFillColor;
            ApplyOutline(previewOutlineColor, new Vector2(2f, 2f));
            ApplyEndCapStyle(previewOutlineColor, true);
            return;
        }

        if (hasTemporaryStyleOverride)
        {
            rootImage.color = temporaryFillColor;
            ApplyOutline(temporaryOutlineColor, temporaryOutlineDistance);
            ApplyEndCapStyle(temporaryOutlineColor, temporaryShowEndCaps);
            return;
        }

        if (selectionManager == null)
        {
            return;
        }

        Color baseColor = isSelected
            ? selectionManager.WallUISelectedColor
            : selectionManager.WallUINormalColor;
        Color fillColor = baseColor;
        fillColor.a = isSelected ? Mathf.Min(0.08f, baseColor.a) : Mathf.Min(0.02f, baseColor.a);
        Color outlineColor = baseColor;
        outlineColor.a = isSelected ? Mathf.Max(0.95f, baseColor.a) : Mathf.Max(0.18f, baseColor.a);

        rootImage.color = fillColor;
        ApplyOutline(outlineColor, isSelected ? new Vector2(2f, 2f) : new Vector2(1f, 1f));
        ApplyEndCapStyle(outlineColor, isSelected);
    }

    private void ApplyOutline(Color outlineColor, Vector2 outlineDistance)
    {
        if (rootOutline == null)
        {
            return;
        }

        rootOutline.effectColor = outlineColor;
        rootOutline.effectDistance = outlineDistance;
        rootOutline.enabled = outlineColor.a > 0.0001f &&
                              (Mathf.Abs(outlineDistance.x) > 0.0001f || Mathf.Abs(outlineDistance.y) > 0.0001f);
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

        float capSize = Mathf.Max(8f, thickness + 4f);
        startCapRect.anchorMin = new Vector2(0f, 0.5f);
        startCapRect.anchorMax = new Vector2(0f, 0.5f);
        endCapRect.anchorMin = new Vector2(1f, 0.5f);
        endCapRect.anchorMax = new Vector2(1f, 0.5f);
        startCapRect.anchoredPosition = Vector2.zero;
        endCapRect.anchoredPosition = Vector2.zero;
        startCapRect.sizeDelta = new Vector2(capSize, capSize);
        endCapRect.sizeDelta = new Vector2(capSize, capSize);
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
