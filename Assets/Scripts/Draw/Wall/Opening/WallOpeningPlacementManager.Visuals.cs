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
        Transform parent = OpeningVisualTransformBuilder.Apply(container, opening, segmentRoot);
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
