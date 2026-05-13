using System.Collections.Generic;
using UnityEngine;

internal sealed class WallOpeningLayoutRebuildController
{
    private const float EndpointMatchThresholdSqr = 0.0001f;

    public void RebuildContainer(
        WallOpeningContainer container,
        List<Wall> pendingRoomRefreshRemovedWalls,
        List<Wall> cachedWalls,
        List<WallOpening> cachedOpenings,
        System.Action<WallOpeningContainer, List<WallOpening>> collectOpenings,
        System.Action<WallOpeningContainer, List<Wall>, bool> clearGeneratedContainerVisuals,
        System.Func<WallOpeningContainer, Transform> getSegmentsRoot,
        System.Func<Transform, string, Vector3, Vector3, float, float, float, int, int, bool, bool, WallVisualState, bool, bool, bool, Transform> createWallSegment,
        System.Action<WallOpeningContainer, WallOpening, int, Transform> updateOpeningVisual,
        System.Action<Transform, List<Wall>, bool> collectWalls,
        System.Action<List<Wall>, List<Wall>> requestRefreshForWallReplacement,
        System.Action markMarkerVisualsDirty,
        bool isDragging)
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
        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
            if (opening == null)
            {
                continue;
            }

            if (opening.transform.parent != container.transform)
            {
                opening.transform.SetParent(container.transform, true);
            }
        }

        clearGeneratedContainerVisuals?.Invoke(container, cachedWalls, isDragging);
        Transform segmentsRoot = getSegmentsRoot != null ? getSegmentsRoot(container) : container.transform;

        if (cachedOpenings.Count == 0)
        {
            createWallSegment?.Invoke(
                segmentsRoot,
                "Segment",
                container.WallStart,
                container.WallEnd,
                container.WallThickness,
                container.WallHeight,
                container.CenterY,
                container.OuterStartVertexId,
                container.OuterEndVertexId,
                container.SuppressOuterStartHandle,
                container.SuppressOuterEndHandle,
                container.VisualState,
                false,
                container.OuterStartSplitPoint,
                container.OuterEndSplitPoint);

            SyncContainerOuterMetadata(container, cachedWalls, collectWalls);

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
                segmentsRoot,
                "Segment",
                segmentStart,
                segmentEnd,
                container.WallThickness,
                container.WallHeight,
                container.CenterY,
                currentDistance <= 0.001f ? container.OuterStartVertexId : 0,
                0,
                currentDistance > 0.001f || container.SuppressOuterStartHandle,
                true,
                container.VisualState,
                false,
                currentDistance <= 0.001f && container.OuterStartSplitPoint,
                false);

            Transform openingSegment = createWallSegment?.Invoke(
                segmentsRoot,
                "Segment",
                startPoint + direction * openingStartDistance,
                startPoint + direction * openingEndDistance,
                container.WallThickness,
                container.WallHeight,
                container.CenterY,
                0,
                0,
                true,
                true,
                container.VisualState,
                true,
                false,
                false);

            updateOpeningVisual?.Invoke(container, opening, i, openingSegment);
            currentDistance = openingEndDistance;
        }

        Vector3 lastSegmentStart = startPoint + direction * currentDistance;
        Vector3 lastSegmentEnd = container.WallEnd;
        createWallSegment?.Invoke(
            segmentsRoot,
            "Segment",
            lastSegmentStart,
            lastSegmentEnd,
            container.WallThickness,
            container.WallHeight,
            container.CenterY,
            0,
            container.OuterEndVertexId,
            true,
            container.SuppressOuterEndHandle,
            container.VisualState,
            false,
            false,
            container.OuterEndSplitPoint);

        SyncContainerOuterMetadata(container, cachedWalls, collectWalls);

        if (removedWalls.Count > 0)
        {
            collectWalls?.Invoke(container.transform, cachedWalls, true);
            requestRefreshForWallReplacement?.Invoke(removedWalls, cachedWalls);
        }

        markMarkerVisualsDirty?.Invoke();
    }

    private static void SyncContainerOuterMetadata(
        WallOpeningContainer container,
        List<Wall> cachedWalls,
        System.Action<Transform, List<Wall>, bool> collectWalls)
    {
        if (container == null || cachedWalls == null)
        {
            return;
        }

        collectWalls?.Invoke(container.transform, cachedWalls, true);

        Wall startWall = null;
        Wall endWall = null;
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (startWall == null &&
                ArePointsNearXZ(wall.Data.startPoint, container.WallStart))
            {
                startWall = wall;
            }

            if (endWall == null &&
                ArePointsNearXZ(wall.Data.endPoint, container.WallEnd))
            {
                endWall = wall;
            }

            if (startWall != null && endWall != null)
            {
                break;
            }
        }

        int startVertexId = startWall != null ? startWall.StartVertexId : container.OuterStartVertexId;
        int endVertexId = endWall != null ? endWall.EndVertexId : container.OuterEndVertexId;
        bool startSplitPoint = startWall != null ? startWall.IsStartSplitPoint : container.OuterStartSplitPoint;
        bool endSplitPoint = endWall != null ? endWall.IsEndSplitPoint : container.OuterEndSplitPoint;

        container.SetOuterVertexIds(startVertexId, endVertexId);
        container.SetOuterSplitPointFlags(startSplitPoint, endSplitPoint);
    }

    private static bool ArePointsNearXZ(Vector3 left, Vector3 right)
    {
        float dx = left.x - right.x;
        float dz = left.z - right.z;
        return dx * dx + dz * dz <= EndpointMatchThresholdSqr;
    }
}
