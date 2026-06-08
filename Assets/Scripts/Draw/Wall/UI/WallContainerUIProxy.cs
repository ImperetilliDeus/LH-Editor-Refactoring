using UnityEngine;
using UnityEngine.EventSystems;

public class WallContainerUIProxy : MonoBehaviour, IPointerClickHandler
{
    private WallSelectionManager selectionManager;
    private WallOpeningContainer container;
    private TopPlanSegmentBatchGraphic lineGraphic;

    public WallOpeningContainer Container => container;

    public void Initialize(WallSelectionManager manager, WallOpeningContainer wallContainer)
    {
        selectionManager = manager;
        container = wallContainer;

        if (lineGraphic == null)
        {
            GameObject lineObject = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(TopPlanSegmentBatchGraphic));
            lineObject.transform.SetParent(transform, false);
            lineGraphic = lineObject.GetComponent<TopPlanSegmentBatchGraphic>();
            lineGraphic.raycastTarget = true;
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (lineGraphic == null || selectionManager == null)
        {
            return;
        }

        Color color = isSelected ? selectionManager.WallUISelectedColor : selectionManager.WallUINormalColor;
        if (lineGraphic.color != color)
        {
            lineGraphic.color = color;
            lineGraphic.SetAllDirty();
        }
    }

    public void RefreshVisual()
    {
        if (selectionManager == null || container == null || lineGraphic == null)
        {
            gameObject.SetActive(false);
            return;
        }

        Camera worldCamera = selectionManager.SelectionCamera;
        Canvas canvas = selectionManager.WallSelectionCanvas;
        if (worldCamera == null || canvas == null)
        {
            gameObject.SetActive(false);
            return;
        }

        Vector3 startWorld = container.WallStart;
        Vector3 endWorld = container.WallEnd;

        Vector3 startScreen = worldCamera.WorldToScreenPoint(startWorld);
        Vector3 endScreen = worldCamera.WorldToScreenPoint(endWorld);

        bool visible = startScreen.z > 0 && endScreen.z > 0;
        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        RectTransform rectTransform = transform as RectTransform;
        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera;

        Vector2 startLocal, endLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, startScreen, uiCamera, out startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, endScreen, uiCamera, out endLocal);

        Vector2 midpoint = (startLocal + endLocal) * 0.5f;
        rectTransform.anchoredPosition = midpoint;

        Vector2 delta = endLocal - startLocal;
        float width = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        rectTransform.sizeDelta = new Vector2(width, selectionManager.WallUIThicknessPixels);
        rectTransform.localRotation = Quaternion.Euler(0, 0, angle);

        lineGraphic.rectTransform.sizeDelta = rectTransform.sizeDelta;
        lineGraphic.SetAllDirty();
        WallSelectionCanvasOrderingUtility.PlaceBelowSelectableControls(rectTransform, canvas.transform);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (selectionManager != null &&
            selectionManager.IsWallUIInteractionEnabled &&
            !selectionManager.IsPointerBlockedByNonWallUI(gameObject))
        {
            selectionManager.HandleWallUIClick(container.gameObject);
        }
    }

    public void DestroyUI()
    {
        if (this != null && gameObject != null)
        {
            Destroy(gameObject);
        }
    }
}
