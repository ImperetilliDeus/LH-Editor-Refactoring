using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIDragManager : MonoBehaviour
{
    private const string DefaultDragButtonName = "_DragButton";

    [Header("References")]
    [SerializeField] private Button dragButton;
    [SerializeField] private RectTransform draggableTarget;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform clampTargetRect;

    [Header("Behavior")]
    [SerializeField] private bool clampInsideParent = false;

    private RectTransform dragParentRect;
    private Vector2 dragOffset;
    private bool isDragging;

    private void Awake()
    {
        if (targetCanvas == null)
        {
            targetCanvas = LayerUtility.FindCanvasByNameOrFirst(LayerUtility.DefaultCanvasName);
        }

        RefreshReferences();

        if (dragButton == null)
        {
            ResolveDragButton();
        }

        EnsureForwarder();
    }

    public void HandleBeginDrag(PointerEventData eventData)
    {
        RefreshReferences();
        if (draggableTarget == null || dragParentRect == null)
        {
            isDragging = false;
            return;
        }

        if (!TryGetLocalPointerPosition(eventData, out Vector2 localPointerPosition))
        {
            isDragging = false;
            return;
        }

        dragOffset = draggableTarget.anchoredPosition - localPointerPosition;
        isDragging = true;
    }

    public void HandleDrag(PointerEventData eventData)
    {
        if (!isDragging || draggableTarget == null || dragParentRect == null)
        {
            return;
        }

        if (!TryGetLocalPointerPosition(eventData, out Vector2 localPointerPosition))
        {
            return;
        }

        Vector2 anchoredPosition = localPointerPosition + dragOffset;
        if (clampInsideParent)
        {
            anchoredPosition = ClampToParent(anchoredPosition);
        }

        draggableTarget.anchoredPosition = anchoredPosition;
    }

    public void HandleEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    private void RefreshReferences()
    {
        if (draggableTarget == null)
        {
            draggableTarget = transform as RectTransform;
        }

        if (draggableTarget != null)
        {
            dragParentRect = draggableTarget.parent as RectTransform;
        }

        if (clampTargetRect == null)
        {
            clampTargetRect = dragParentRect;
        }
    }

    private void EnsureForwarder()
    {
        if (dragButton == null)
        {
            return;
        }

        UIDragButtonForwarder forwarder = dragButton.GetComponent<UIDragButtonForwarder>();
        if (forwarder == null)
        {
            forwarder = dragButton.gameObject.AddComponent<UIDragButtonForwarder>();
        }

        forwarder.Initialize(this);
    }

    private void ResolveDragButton()
    {
        Transform resolved = null;
        if (draggableTarget != null)
        {
            resolved = LayerUtility.FindChildByName(draggableTarget, DefaultDragButtonName);
            if (resolved == null && draggableTarget.parent != null)
            {
                resolved = LayerUtility.FindChildByName(draggableTarget.parent, DefaultDragButtonName);
            }
        }

        if (resolved == null && targetCanvas != null)
        {
            resolved = LayerUtility.FindChildByName(targetCanvas.transform, DefaultDragButtonName);
        }

        if (resolved == null)
        {
            resolved = LayerUtility.FindTransformByName(DefaultDragButtonName, true);
        }

        if (resolved != null)
        {
            dragButton = resolved.GetComponent<Button>();
        }
    }

    private bool TryGetLocalPointerPosition(PointerEventData eventData, out Vector2 localPoint)
    {
        localPoint = Vector2.zero;
        if (dragParentRect == null)
        {
            return false;
        }

        Camera uiCamera = null;
        if (targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = targetCanvas.worldCamera;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragParentRect,
            eventData.position,
            uiCamera,
            out localPoint);
    }

    private Vector2 ClampToParent(Vector2 anchoredPosition)
    {
        RectTransform boundsRect = clampTargetRect != null ? clampTargetRect : dragParentRect;
        if (boundsRect == null || draggableTarget == null)
        {
            return anchoredPosition;
        }

        Rect boundsInParent = GetBoundsRectInDragParentSpace(boundsRect);
        Vector2 anchorReference = GetAnchorReferenceInDragParentSpace();
        Vector2 scaledSize = GetScaledTargetSizeInDragParentSpace();
        Vector2 pivot = draggableTarget.pivot;

        Vector2 minPivotPosition = new Vector2(
            boundsInParent.xMin + scaledSize.x * pivot.x,
            boundsInParent.yMin + scaledSize.y * pivot.y);
        Vector2 maxPivotPosition = new Vector2(
            boundsInParent.xMax - scaledSize.x * (1f - pivot.x),
            boundsInParent.yMax - scaledSize.y * (1f - pivot.y));

        Vector2 minAnchoredPosition = minPivotPosition - anchorReference;
        Vector2 maxAnchoredPosition = maxPivotPosition - anchorReference;

        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minAnchoredPosition.x, maxAnchoredPosition.x);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minAnchoredPosition.y, maxAnchoredPosition.y);
        return anchoredPosition;
    }

    private Rect GetBoundsRectInDragParentSpace(RectTransform boundsRect)
    {
        if (boundsRect == null || dragParentRect == null)
        {
            return new Rect();
        }

        if (boundsRect == dragParentRect)
        {
            return boundsRect.rect;
        }

        Vector3[] worldCorners = new Vector3[4];
        boundsRect.GetWorldCorners(worldCorners);

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 localCorner = dragParentRect.InverseTransformPoint(worldCorners[i]);
            min = Vector2.Min(min, localCorner);
            max = Vector2.Max(max, localCorner);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Vector2 GetAnchorReferenceInDragParentSpace()
    {
        if (dragParentRect == null || draggableTarget == null)
        {
            return Vector2.zero;
        }

        Rect parentRect = dragParentRect.rect;
        Vector2 anchorCenter = (draggableTarget.anchorMin + draggableTarget.anchorMax) * 0.5f;

        return new Vector2(
            Mathf.Lerp(parentRect.xMin, parentRect.xMax, anchorCenter.x),
            Mathf.Lerp(parentRect.yMin, parentRect.yMax, anchorCenter.y));
    }

    private Vector2 GetScaledTargetSizeInDragParentSpace()
    {
        if (draggableTarget == null)
        {
            return Vector2.zero;
        }

        Vector3 lossyScale = draggableTarget.lossyScale;
        Vector3 parentLossyScale = dragParentRect != null ? dragParentRect.lossyScale : Vector3.one;

        float scaleX = parentLossyScale.x == 0f ? 1f : lossyScale.x / parentLossyScale.x;
        float scaleY = parentLossyScale.y == 0f ? 1f : lossyScale.y / parentLossyScale.y;

        return new Vector2(
            draggableTarget.rect.width * scaleX,
            draggableTarget.rect.height * scaleY);
    }
}

public class UIDragButtonForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private UIDragManager manager;

    public void Initialize(UIDragManager dragManager)
    {
        manager = dragManager;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        manager?.HandleBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        manager?.HandleDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        manager?.HandleEndDrag(eventData);
    }
}
