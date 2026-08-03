using System.Collections.Generic;
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

    private void UpdateOpeningDragPreview(WallOpeningContainer container, WallOpening opening)
    {
        if (container == null || opening == null)
        {
            return;
        }

        OpeningVisualTransformBuilder.Apply(container, opening, null);
        MarkMarkerVisualsDirty();
    }

    private void ClearGeneratedContainerVisuals(WallOpeningContainer container, List<Wall> wallsInContainer = null, bool isDragging = false)
    {
        if (container == null)
        {
            return;
        }

        List<Wall> walls = wallsInContainer;
        if (walls == null)
        {
            WallHierarchyUtility.CollectWalls(container.transform, cachedWalls);
            walls = cachedWalls;
        }

        for (int i = 0; i < walls.Count; i++)
        {
            if (walls[i] == null)
            {
                continue;
            }

            ClearSelectedGeneratedWallDuringDrag(walls[i], isDragging);

            walls[i].ClearLengthDisplay(wallLengthDisplay);

            if (isDragging)
            {
                MoveWallToDeactivatedHolderForDrag(walls[i]);
            }
            else
            {
                DestroyGeneratedWall(walls[i]);
            }
        }

        for (int i = container.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = container.transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (ShouldPreserveContainerChildDuringVisualClear(child))
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

    private void ClearSelectedGeneratedWallDuringDrag(Wall wall, bool isDragging)
    {
        if (isDragging && wallSelectionManager != null && wallSelectionManager.SelectedWall == wall.gameObject)
        {
            wallSelectionManager.SetSelectedWallPreservingOpeningSelection(null);
        }
    }

    private static bool ShouldPreserveContainerChildDuringVisualClear(Transform child)
    {
        return child.GetComponent<WallOpening>() != null
            || child.GetComponentInChildren<WallOpening>(true) != null
            || child.name == SegmentGroupName;
    }

    private void MoveWallToDeactivatedHolderForDrag(Wall wall)
    {
        if (handleManager != null)
        {
            handleManager.UnregisterWall(wall.gameObject);
        }

        WallSelectionUIProxy selectionProxy = wall.GetComponent<WallSelectionUIProxy>();
        if (selectionProxy != null)
        {
            selectionProxy.DestroyUI();
            Destroy(selectionProxy);
        }

        wall.transform.SetParent(deactivatedWallsHolder, false);
    }

    private void DestroyGeneratedWall(Wall wall)
    {
        if (handleManager != null)
        {
            handleManager.UnregisterWall(wall.gameObject);
        }

        Destroy(wall.gameObject);
    }

    private void CreateFillerSegment(
        Transform parent,
        string fillerName,
        WallOpeningContainer container,
        WallOpening opening,
        float segmentHeight,
        float segmentBottomY)
    {
        geometryFactory.CreateFillerSegment(
            parent,
            GetCubeMesh(),
            fillerName,
            container,
            opening,
            segmentHeight,
            segmentBottomY);
    }

    private GameObject CreateCubeObject(string objectName, Transform parent, bool withCollider)
    {
        return geometryFactory.CreateCubeObject(GetCubeMesh(), objectName, parent, withCollider);
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
