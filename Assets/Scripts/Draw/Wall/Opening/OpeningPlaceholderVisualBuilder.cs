using UnityEngine;

internal static class OpeningPlaceholderVisualBuilder
{
    public static void Apply(
        WallOpening opening,
        Mesh cubeMesh,
        Material placeholderMaterial,
        bool hidePlaceholderVisual)
    {
        if (opening == null)
        {
            return;
        }

        MeshFilter meshFilter = opening.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = opening.gameObject.AddComponent<MeshFilter>();
        }

        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = cubeMesh;
        }

        if (opening.GetComponent<BoxCollider>() == null)
        {
            opening.gameObject.AddComponent<BoxCollider>();
        }

        MeshRenderer renderer = opening.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = opening.gameObject.AddComponent<MeshRenderer>();
        }

        renderer.enabled = !hidePlaceholderVisual;
        renderer.sharedMaterial = hidePlaceholderVisual ? null : placeholderMaterial;
    }
}
