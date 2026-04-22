using System.Collections.Generic;
using UnityEngine;

internal sealed class WallOpeningLayoutRebuildController
{
    public void RebuildContainer(
        WallOpeningContainer container,
        List<Wall> pendingRoomRefreshRemovedWalls,
        List<Wall> cachedWalls,
        List<WallOpening> cachedOpenings,
        System.Action<WallOpeningContainer, List<WallOpening>> collectOpenings,
        System.Action<WallOpeningContainer, List<Wall>> clearGeneratedContainerVisuals,
        System.Action<Transform, string, Vector3, Vector3, float, float, float, int, int, bool, bool, WallVisualState> createWallSegment,
        System.Action<WallOpeningContainer, WallOpening, int> updateOpeningVisual,
        System.Action<Transform, List<Wall>, bool> collectWalls,
        System.Action<List<Wall>, List<Wall>> requestRefreshForWallReplacement,
        System.Action markMarkerVisualsDirty)
    {
        if (container == null)
        {
            return;
        }

        List<Wall> removedWalls = new List<Wall>();
        if (pendingRoomRefreshRemovedWalls != null && pendingRoomRefreshRemovedWalls.Count > 0)
        {
            removedWalls.AddRange(pendingRoomRefreshRemovedWalls);
            pendingRoomRefreshRemovedWalls.Clear();
        }

        collectWalls?.Invoke(container.transform, cachedWalls, true);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall != null)
            {
                removedWalls.Add(wall);
            }
        }

        collectOpenings?.Invoke(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));

        clearGeneratedContainerVisuals?.Invoke(container, cachedWalls);

        if (cachedOpenings.Count == 0)
        {
            createWallSegment?.Invoke(
                container.transform,
                $"{container.name}_Segment_Full",
                container.WallStart,
                container.WallEnd,
                container.WallThickness,
                container.WallHeight,
                container.CenterY,
                container.OuterStartVertexId,
                container.OuterEndVertexId,
                container.SuppressOuterStartHandle,
                container.SuppressOuterEndHandle,
                container.VisualState);

            if (removedWalls.Count > 0)
            {
                collectWalls?.Invoke(container.transform, cachedWalls, true);
                requestRefreshForWallReplacement?.Invoke(removedWalls, cachedWalls);
            }

            markMarkerVisualsDirty?.Invoke();
            return;
        }

        Vector3 startPoint = container.WallStart;
        Vector3 direction = container.WallDirection;
        float currentDistance = 0f;

        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
            if (opening == null)
            {
                continue;
            }

            float halfWidth = opening.Width * 0.5f;
            float openingStartDistance = opening.CenterDistance - halfWidth;
            float openingEndDistance = opening.CenterDistance + halfWidth;

            Vector3 segmentStart = startPoint + direction * currentDistance;
            Vector3 segmentEnd = startPoint + direction * openingStartDistance;
            createWallSegment?.Invoke(
                container.transform,
                $"{container.name}_Segment_{i * 2}",
                segmentStart,
                segmentEnd,
                container.WallThickness,
                container.WallHeight,
                container.CenterY,
                currentDistance <= 0.001f ? container.OuterStartVertexId : 0,
                0,
                currentDistance > 0.001f || container.SuppressOuterStartHandle,
                true,
                container.VisualState);

            updateOpeningVisual?.Invoke(container, opening, i);
            currentDistance = openingEndDistance;
        }

        Vector3 lastSegmentStart = startPoint + direction * currentDistance;
        Vector3 lastSegmentEnd = container.WallEnd;
        createWallSegment?.Invoke(
            container.transform,
            $"{container.name}_Segment_End",
            lastSegmentStart,
            lastSegmentEnd,
            container.WallThickness,
            container.WallHeight,
            container.CenterY,
            0,
            container.OuterEndVertexId,
            true,
            container.SuppressOuterEndHandle,
            container.VisualState);

        if (removedWalls.Count > 0)
        {
            collectWalls?.Invoke(container.transform, cachedWalls, true);
            requestRefreshForWallReplacement?.Invoke(removedWalls, cachedWalls);
        }

        markMarkerVisualsDirty?.Invoke();
    }
}
