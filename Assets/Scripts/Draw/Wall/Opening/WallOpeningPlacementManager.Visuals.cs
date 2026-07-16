using UnityEngine;

public partial class WallOpeningPlacementManager
{
    private void BindVisualEvents()
    {
        EditorVisualEvents.OpeningMarkerRefreshRequested -= HandleOpeningMarkerRefreshRequested;
        EditorVisualEvents.OpeningMarkerRefreshRequested += HandleOpeningMarkerRefreshRequested;
    }

    private void UnbindVisualEvents()
    {
        EditorVisualEvents.OpeningMarkerRefreshRequested -= HandleOpeningMarkerRefreshRequested;
    }

    private void HandleOpeningMarkerRefreshRequested()
    {
        MarkMarkerVisualsDirty();
    }

    public void MarkMarkerVisualsDirty()
    {
        markerVisualsDirty = true;
    }

    public void RefreshRestoredOpeningVisuals()
    {
        MarkMarkerVisualsDirty();
        RefreshOpeningMarkerVisuals();
    }

    private void UpdateOpeningVisual(WallOpeningContainer container, WallOpening opening, int index, Transform segmentRoot)
    {
        Transform parent = segmentRoot != null ? segmentRoot : container.transform;
        if (opening.transform.parent != parent)
        {
            opening.transform.SetParent(parent, false);
        }

        Vector3 openingCenter = container.WallStart + container.WallDirection * opening.CenterDistance;
        float openingCenterY = opening.BottomY + opening.Height * 0.5f;
        if (segmentRoot != null)
        {
            Vector3 parentScale = parent.localScale;
            opening.transform.localPosition = new Vector3(
                0f,
                SafeDivide(openingCenterY - parent.position.y, parentScale.y),
                0f);
            opening.transform.localRotation = Quaternion.identity;
            opening.transform.localScale = new Vector3(
                SafeDivide(opening.Depth, parentScale.x),
                SafeDivide(opening.Height, parentScale.y),
                SafeDivide(opening.Width, parentScale.z));
        }
        else
        {
            opening.transform.position = new Vector3(
                openingCenter.x,
                openingCenterY,
                openingCenter.z);
            opening.transform.rotation = Quaternion.LookRotation(container.WallDirection, Vector3.up);
            opening.transform.localScale = new Vector3(opening.Depth, opening.Height, opening.Width);
        }

        opening.name = opening.Type == OpeningPlacementType.Door ? $"Door_{index}" : $"Window_{index}";

        MeshFilter meshFilter = opening.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = opening.gameObject.AddComponent<MeshFilter>();
        }

        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = GetCubeMesh();
        }

        BoxCollider collider = opening.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = opening.gameObject.AddComponent<BoxCollider>();
        }

        MeshRenderer renderer = opening.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            renderer = opening.gameObject.AddComponent<MeshRenderer>();
        }

        bool hasModelPrefab = TryGetOpeningTypeDefinition(opening, out OpeningTypeCatalogItem definition) && definition.ModelPrefab != null;
        bool hidePlaceholderVisual = hasModelPrefab;
        renderer.enabled = !hidePlaceholderVisual;
        renderer.sharedMaterial = hidePlaceholderVisual ? null : GetOpeningMaterial(opening.Type);
        if (hasModelPrefab)
        {
            opening.ApplyModelPrefab(
                definition.ModelPrefab,
                definition.ModelLocalPosition,
                definition.ModelLocalEulerAngles,
                definition.ModelScaleMultiplier,
                new Vector3(opening.Depth, opening.Height, opening.Width),
                new Vector3(opening.Width, opening.Height, opening.Depth),
                definition.ReferenceSize,
                definition.FitDepth,
                definition.FitHeight,
                definition.FitWidth,
                definition.UseParametricModel,
                definition.AuthoredSize,
                definition.UsesBlenderLocalAxes);
        }
        else
        {
            opening.ClearModelPrefab();
        }

        CreateFillerSegment(parent, $"{opening.name}_BottomFill", container, opening, opening.BottomY - container.WallBottomY, container.WallBottomY);
        CreateFillerSegment(
            parent,
            $"{opening.name}_TopFill",
            container,
            opening,
            container.WallTopY - (opening.BottomY + opening.Height),
            opening.BottomY + opening.Height);

        opening.EnsureMarker(previewCanvas, mainCamera);
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Abs(denominator) > 0.000001f ? numerator / denominator : 0f;
    }

    private void EnsureCachedResources()
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

    private Mesh GetCubeMesh()
    {
        EnsureCachedResources();
        return cachedCubeMesh;
    }

    private static void DestroyTemporaryObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private Material GetOpeningMaterial(OpeningPlacementType type)
    {
        Material cached = type == OpeningPlacementType.Door ? cachedDoorMaterial : cachedWindowMaterial;
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
        material.color = type == OpeningPlacementType.Door
            ? new Color(0.5f, 0.28f, 0.12f, 0.75f)
            : new Color(0.35f, 0.75f, 1f, 0.55f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (type == OpeningPlacementType.Door)
        {
            cachedDoorMaterial = material;
        }
        else
        {
            cachedWindowMaterial = material;
        }

        return material;
    }
}
