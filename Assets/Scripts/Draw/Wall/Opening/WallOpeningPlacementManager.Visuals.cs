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

        bool hasModelPrefab = TryGetOpeningTypeDefinition(opening, out OpeningTypeCatalogItem definition) && definition.ModelPrefab != null;
        bool hidePlaceholderVisual = hasModelPrefab;
        OpeningPlaceholderVisualBuilder.Apply(
            opening,
            GetCubeMesh(),
            GetOpeningMaterial(opening.Type),
            hidePlaceholderVisual);
        OpeningModelPrefabApplier.Apply(opening, hasModelPrefab ? definition : null);

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
        visualResources.EnsureCachedResources();
    }

    private Mesh GetCubeMesh()
    {
        return visualResources.GetCubeMesh();
    }

    private Material GetOpeningMaterial(OpeningPlacementType type)
    {
        return visualResources.GetOpeningMaterial(type);
    }
}
