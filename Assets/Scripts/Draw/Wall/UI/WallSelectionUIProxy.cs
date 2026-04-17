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
        EnsureReferences();
        EnsureUI();
        if (rootImage == null || selectionManager == null)
        {
            return;
        }

        rootImage.color = selected
            ? selectionManager.WallUISelectedColor
            : selectionManager.WallUINormalColor;
    }

    public void RefreshVisual()
    {
        EnsureReferences();
        EnsureUI();
        if (ownerWall == null || selectionManager == null || rootRect == null || rootImage == null || targetCanvas == null)
        {
            return;
        }

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
        rootRect.sizeDelta = new Vector2(width, selectionManager.WallUIThicknessPixels);
        rootRect.localScale = Vector3.one;
        rootImage.raycastTarget = selectionManager.IsWallUIInteractionEnabled;
    }

    public void DestroyUI()
    {
        if (rootRect != null)
        {
            Destroy(rootRect.gameObject);
            rootRect = null;
            rootImage = null;
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

        GameObject rootObject = new GameObject($"WallUI_{GetInstanceID()}", typeof(RectTransform), typeof(Image));
        rootObject.transform.SetParent(targetCanvas.transform, false);
        LayerUtility.ApplyLayer(rootObject, LayerUtility.WallUILayerName, false);

        rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);

        rootImage = rootObject.GetComponent<Image>();
        rootImage.color = selectionManager != null ? selectionManager.WallUINormalColor : new Color(1f, 1f, 1f, 0.04f);
        rootImage.raycastTarget = true;

        WallSelectionUIInput input = rootObject.AddComponent<WallSelectionUIInput>();
        input.Initialize(this);
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
