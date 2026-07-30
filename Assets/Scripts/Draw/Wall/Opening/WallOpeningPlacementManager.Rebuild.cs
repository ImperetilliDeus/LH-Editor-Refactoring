using UnityEngine;

public partial class WallOpeningPlacementManager
{
    private void RebuildContainer(WallOpeningContainer container, bool isDragging = false)
    {
        if (isDragging)
        {
            handleManager?.BeginTransientDragRebuild();
        }

        try
        {
            if (!isDragging)
            {
                ClearDeactivatedWallsHolder();
            }

            RebuildContainerLayout(container, isDragging);
        }
        finally
        {
            if (isDragging)
            {
                handleManager?.EndTransientDragRebuild();
            }
        }
    }

    private void RebuildContainerLayout(WallOpeningContainer container, bool isDragging)
    {
        layoutRebuildController.RebuildContainer(
            container: container,
            pendingRoomRefreshRemovedWalls: pendingRoomRefreshRemovedWalls,
            cachedWalls: cachedWalls,
            cachedOpenings: cachedOpenings,
            collectOpenings: CollectOpenings,
            clearGeneratedContainerVisuals: ClearGeneratedContainerVisuals,
            getSegmentsRoot: GetOrCreateSegmentsRoot,
            createWallSegment: CreateWallSegment,
            updateOpeningVisual: UpdateOpeningVisual,
            collectWalls: (targetTransform, result, includeInactive) => WallHierarchyUtility.CollectWalls(targetTransform, result, includeInactive),
            requestRefreshForWallReplacement: RoomTopologyEvents.RequestRefreshForWallReplacement,
            markMarkerVisualsDirty: MarkMarkerVisualsDirty,
            isDragging: isDragging);
    }

    private void ClearDeactivatedWallsHolder()
    {
        if (deactivatedWallsHolder == null)
        {
            return;
        }

        for (int i = deactivatedWallsHolder.childCount - 1; i >= 0; i--)
        {
            Transform child = deactivatedWallsHolder.GetChild(i);
            if (child == null)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }
}
