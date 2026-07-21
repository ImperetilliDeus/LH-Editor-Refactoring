using UnityEngine;

internal sealed class OpeningVisualResources
{
    private Mesh cachedCubeMesh;
    private Material cachedDoorMaterial;
    private Material cachedWindowMaterial;

    public void EnsureCachedResources()
    {
        if (cachedCubeMesh != null)
        {
            return;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter cubeFilter = cube.GetComponent<MeshFilter>();
        if (cubeFilter != null)
        {
            cachedCubeMesh = cubeFilter.sharedMesh;
        }

        DestroyTemporaryObject(cube);
    }

    public Mesh GetCubeMesh()
    {
        EnsureCachedResources();
        return cachedCubeMesh;
    }

    public Material GetOpeningMaterial(WallOpeningPlacementManager.OpeningPlacementType type)
    {
        Material cached = type == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? cachedDoorMaterial
            : cachedWindowMaterial;
        if (cached != null)
        {
            return cached;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.color = type == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? new Color(0.5f, 0.28f, 0.12f, 0.75f)
            : new Color(0.35f, 0.75f, 1f, 0.55f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (type == WallOpeningPlacementManager.OpeningPlacementType.Door)
        {
            cachedDoorMaterial = material;
        }
        else
        {
            cachedWindowMaterial = material;
        }

        return material;
    }

    public void DestroyCachedMaterials(System.Action<Object> destroy)
    {
        if (destroy == null)
        {
            return;
        }

        if (cachedDoorMaterial != null)
        {
            destroy(cachedDoorMaterial);
            cachedDoorMaterial = null;
        }

        if (cachedWindowMaterial != null)
        {
            destroy(cachedWindowMaterial);
            cachedWindowMaterial = null;
        }
    }

    private static void DestroyTemporaryObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}
