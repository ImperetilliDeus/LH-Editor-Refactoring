using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WallOpeningMarkerInput : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private WallOpening ownerOpening;
    private WallOpeningPlacementManager placementManager;

    public void Initialize(WallOpening opening, WallOpeningPlacementManager manager)
    {
        ownerOpening = opening;
        placementManager = manager;

        Image image = GetComponent<Image>();
        if (image == null)
        {
            image = gameObject.AddComponent<Image>();
        }

        image.color = Color.clear;
        image.raycastTarget = true;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (placementManager != null)
        {
            placementManager.SelectOpening(ownerOpening);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (placementManager != null)
        {
            placementManager.BeginMarkerDrag(ownerOpening);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (placementManager != null)
        {
            placementManager.DragMarker(ownerOpening, eventData.position);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (placementManager != null)
        {
            placementManager.EndMarkerDrag(ownerOpening);
        }
    }
}
