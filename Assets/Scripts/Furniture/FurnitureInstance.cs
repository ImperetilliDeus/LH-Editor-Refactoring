using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class FurnitureInstance : MonoBehaviour
{
    [SerializeField] private string catalogCode = string.Empty;
    [SerializeField] private bool isPlaced;
    [SerializeField] private Room currentRoom;
    [SerializeField] private Vector3 boundsSize = Vector3.one;
    [SerializeField] private Vector3 placementOffset = Vector3.zero;
    [SerializeField] private Vector3 defaultEulerAngles = Vector3.zero;

    private readonly List<Renderer> cachedRenderers = new List<Renderer>();

    public string CatalogCode => catalogCode;
    public bool IsPlaced => isPlaced;
    public Room CurrentRoom => currentRoom;
    public Vector3 BoundsSize => boundsSize;
    public Vector3 PlacementOffset => placementOffset;
    public Vector3 DefaultEulerAngles => defaultEulerAngles;

    public void Initialize(FurnitureCatalogItem item)
    {
        if (item == null)
        {
            return;
        }

        catalogCode = item.code ?? string.Empty;
        boundsSize = item.boundsSize;
        placementOffset = item.placementOffset;
        defaultEulerAngles = item.defaultEulerAngles;
    }

    public void SetPlaced(bool value)
    {
        isPlaced = value;
    }

    public void SetCurrentRoom(Room room)
    {
        currentRoom = room;
    }

    public Bounds CalculateWorldBounds()
    {
        CacheRenderers();
        bool hasRendererBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        for (int i = 0; i < cachedRenderers.Count; i++)
        {
            Renderer currentRenderer = cachedRenderers[i];
            if (currentRenderer == null || !currentRenderer.enabled)
            {
                continue;
            }

            if (!hasRendererBounds)
            {
                bounds = currentRenderer.bounds;
                hasRendererBounds = true;
            }
            else
            {
                bounds.Encapsulate(currentRenderer.bounds);
            }
        }

        if (hasRendererBounds)
        {
            boundsSize = bounds.size;
            return bounds;
        }

        Vector3 scaledSize = Vector3.Scale(boundsSize, transform.lossyScale);
        if (scaledSize.sqrMagnitude <= 0.000001f)
        {
            scaledSize = Vector3.one;
        }

        return new Bounds(transform.position, scaledSize);
    }

    public void ApplyLayerRecursively()
    {
        LayerUtility.ApplyLayer(gameObject, LayerUtility.FurnishLayerName, true);
    }

    private void CacheRenderers()
    {
        cachedRenderers.Clear();
        cachedRenderers.AddRange(GetComponentsInChildren<Renderer>(true));
    }
}
