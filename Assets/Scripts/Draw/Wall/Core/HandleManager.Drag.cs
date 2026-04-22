using System.Collections.Generic;
using UnityEngine;

public partial class HandleManager
{
    private void HandleDraggingInput(EditorPointerFrame pointerFrame)
    {
        if (!pointerFrame.IsAvailable)
        {
            return;
        }

        Vector2 mousePosition = pointerFrame.ScreenPosition;

        if (pointerFrame.LeftPressedThisFrame)
        {
            TryBeginPendingDrag(mousePosition);
        }

        if (pointerFrame.LeftReleasedThisFrame)
        {
            EndHandleDrag();
            return;
        }

        if (pendingGroup != null && draggingGroup == null && pointerFrame.LeftPressed)
        {
            float thresholdSqr = clickAllowanceSensitivityPixels * clickAllowanceSensitivityPixels;
            float movedSqr = (mousePosition - pendingStartMousePosition).sqrMagnitude;
            if (movedSqr >= thresholdSqr)
            {
                BeginHandleDragFromPending();
            }
        }

        if (draggingGroup == null)
        {
            return;
        }

        if (!pointerFrame.LeftPressed)
        {
            EndHandleDrag();
            return;
        }

        if (!TryGetMouseWorldPoint(pointerFrame.ScreenPosition, out Vector3 dragPoint))
        {
            return;
        }

        Vector3 snappedPoint = dragPoint;
        bool snapped = false;
        if (IsSplitPointGroup(draggingGroup) && TryGetSplitPointDragSegment(draggingGroup, out Vector3 segmentStart, out Vector3 segmentEnd))
        {
            snappedPoint = ConstrainPointToSegment(dragPoint, segmentStart, segmentEnd);
            SetGroupColor(draggingGroup, GetActiveColor(draggingGroup));
        }
        else
        {
            dragSnapCandidates.Clear();
            if (snapManager != null)
            {
                snapManager.CollectNearbyHandleSnapCandidates(
                    dragPoint,
                    dragSnapCandidates,
                    wallRoot,
                    null,
                    draggingGroup != null ? draggingGroup.vertexId : 0);
            }

            CollectDragWallSegmentSnapCandidates(dragPoint, dragWallSegmentSnapCandidates, draggingGroup);

            snappedPoint = snapManager != null
                ? snapManager.GetSnappedHandleDragPoint(dragPoint, dragAnchorPoint, dragSnapCandidates, mainCamera, dragWallSegmentSnapCandidates, out _, out _)
                : dragPoint;

            snapped = (new Vector2(snappedPoint.x - dragPoint.x, snappedPoint.z - dragPoint.z)).sqrMagnitude > 0.000001f;
            SetGroupColor(draggingGroup, snapped ? GetSnappedColor(draggingGroup) : GetActiveColor(draggingGroup));
        }

        ApplyVertexDrag(draggingGroup, snappedPoint);
        UpdateHandlePositions();
        handlePositionsDirty = false;
    }

    private void TryBeginPendingDrag(Vector2 mousePosition)
    {
        pendingGroup = null;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null || group.handleRect == null)
            {
                continue;
            }

            if (ContainsScreenPoint(group.handleRect, mousePosition))
            {
                pendingGroup = group;
                pendingStartMousePosition = mousePosition;
                return;
            }
        }
    }

    private void BeginHandleDragFromPending()
    {
        if (pendingGroup == null)
        {
            return;
        }

        ClearPreviewSnappedHandle();

        draggingGroup = pendingGroup;
        pendingGroup = null;
        dragAnchorPoint = draggingGroup.worldPoint;

        dragStartStates.Clear();
        CollectAffectedWallsForGroup(draggingGroup, affectedWallObjects, affectedWallComponents);
        for (int i = 0; i < affectedWallComponents.Count; i++)
        {
            Wall wall = affectedWallComponents[i];
            if (wall == null || dragStartStates.ContainsKey(wall.gameObject))
            {
                continue;
            }

            dragStartStates[wall.gameObject] = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject);
        }

        SetGroupColor(draggingGroup, GetActiveColor(draggingGroup));
    }

    private void EndHandleDrag()
    {
        if (draggingGroup == null)
        {
            pendingGroup = null;
            return;
        }

        draggingGroup = TryMergeDraggedGroupToNearby(draggingGroup);

        int draggedVertexId = draggingGroup.vertexId;
        BuildVertexDragStateChangeRecords();
        if (undoRedoManager != null && dragStateChangeRecords.Count > 0)
        {
            undoRedoManager.ExecuteCommand(
                new VertexGroupMoveCommand(dragStateChangeRecords),
                alreadyExecuted: true);
        }

        SetGroupColor(draggingGroup, GetBaseColor(draggingGroup));
        draggingGroup = null;
        pendingGroup = null;
        dragStartStates.Clear();

        RefreshAllGroupWorldPoints();
        RoomTopologyEvents.RequestRefreshAll();
        EditorVisualEvents.RequestTopViewRefresh();
        handlePositionsDirty = true;
    }

    private void BuildVertexDragStateChangeRecords()
    {
        dragStateChangeRecords.Clear();

        foreach (KeyValuePair<GameObject, UndoRedoManager.WallStateSnapshot> pair in dragStartStates)
        {
            GameObject wallObject = pair.Key;
            if (wallObject == null)
            {
                continue;
            }

            UndoRedoManager.WallStateSnapshot before = pair.Value;
            UndoRedoManager.WallStateSnapshot after = UndoRedoManager.WallStateSnapshot.Capture(wallObject);

            if (!UndoRedoManager.WallStateSnapshot.HasMeaningfulDelta(before, after))
            {
                continue;
            }

            dragStateChangeRecords.Add(new UndoRedoManager.WallStateChangeRecord
            {
                before = before,
                after = after,
            });
        }
    }

    private void ApplyVertexDrag(VertexGroup group, Vector3 newPoint)
    {
        if (group == null)
        {
            return;
        }

        newPoint.y = dragPlaneHeight;

        if (TryApplyOpeningContainerEndpointDrag(group, newPoint))
        {
            RefreshAllGroupWorldPoints();
            RoomTopologyEvents.RequestRefreshAll();
            EditorVisualEvents.RequestTopViewRefresh();
            MarkHandlePositionsDirty();
            return;
        }

        CollectAffectedWallsForGroup(group, affectedWallObjects, affectedWallComponents);

        bool appliedSplitChain = false;
        Vector3 appliedPoint = newPoint;
        if (!IsSplitPointGroup(group))
        {
            appliedSplitChain = TryApplySplitPointChainEndpointDrag(group.vertexId, newPoint, out appliedPoint);
        }

        if (!appliedSplitChain)
        {
            WallGeometryService.ApplyVertexMove(affectedWallComponents, group.vertexId, newPoint, dragPlaneHeight, minimumWallLength, wallLengthDisplay);
        }

        RefreshAllGroupWorldPoints();
        group.worldPoint = appliedSplitChain ? appliedPoint : group.worldPoint;
        RoomTopologyEvents.RequestRefreshAll();
        EditorVisualEvents.RequestTopViewRefresh();
        MarkHandlePositionsDirty();
    }

    private bool TryApplySplitPointChainEndpointDrag(int draggedVertexId, Vector3 draggedPoint, out Vector3 appliedDraggedPoint)
    {
        appliedDraggedPoint = draggedPoint;
        if (!TryBuildSplitChainFromEndpoint(draggedVertexId, splitChainWalls, splitChainVertexIds, splitChainPoints))
        {
            return false;
        }

        if (splitChainWalls.Count == 0 || splitChainPoints.Count != splitChainWalls.Count + 1)
        {
            return false;
        }

        float originalTotalLength = 0f;
        float minimumRequiredTotalLength = 0f;
        List<float> originalSegmentLengths = new List<float>(splitChainWalls.Count);
        for (int i = 0; i < splitChainWalls.Count; i++)
        {
            float segmentLength = Vector3.Distance(splitChainPoints[i], splitChainPoints[i + 1]);
            originalTotalLength += segmentLength;
            originalSegmentLengths.Add(segmentLength);
        }

        if (originalTotalLength <= 0.0001f)
        {
            return false;
        }

        for (int i = 0; i < splitChainWalls.Count; i++)
        {
            float segmentLength = originalSegmentLengths[i];
            if (segmentLength <= 0.0001f)
            {
                return false;
            }

            float segmentRatio = segmentLength / originalTotalLength;
            minimumRequiredTotalLength = Mathf.Max(minimumRequiredTotalLength, minimumWallLength / segmentRatio);
        }

        Vector3 terminalPoint = splitChainPoints[splitChainPoints.Count - 1];
        terminalPoint.y = dragPlaneHeight;
        draggedPoint.y = dragPlaneHeight;

        Vector3 draggedToTerminal = draggedPoint - terminalPoint;
        draggedToTerminal.y = 0f;
        float newTotalLength = draggedToTerminal.magnitude;
        if (newTotalLength <= 0.0001f)
        {
            Vector3 fallbackDirection = splitChainPoints[0] - terminalPoint;
            fallbackDirection.y = 0f;
            if (fallbackDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            draggedToTerminal = fallbackDirection.normalized * minimumRequiredTotalLength;
            newTotalLength = minimumRequiredTotalLength;
        }
        else if (newTotalLength < minimumRequiredTotalLength)
        {
            draggedToTerminal = draggedToTerminal.normalized * minimumRequiredTotalLength;
            newTotalLength = minimumRequiredTotalLength;
        }

        Vector3 adjustedDraggedPoint = terminalPoint + draggedToTerminal;
        appliedDraggedPoint = adjustedDraggedPoint;
        Vector3 direction = (terminalPoint - adjustedDraggedPoint).normalized;

        splitChainPoints[0] = adjustedDraggedPoint;
        float accumulatedDistance = 0f;
        for (int i = 1; i < splitChainPoints.Count - 1; i++)
        {
            float originalSegmentLength = originalSegmentLengths[i - 1];
            float segmentRatio = originalSegmentLength / originalTotalLength;
            accumulatedDistance += newTotalLength * segmentRatio;
            Vector3 point = adjustedDraggedPoint + direction * accumulatedDistance;
            point.y = dragPlaneHeight;
            splitChainPoints[i] = point;
        }

        splitChainPoints[splitChainPoints.Count - 1] = terminalPoint;

        for (int i = 0; i < splitChainWalls.Count; i++)
        {
            Wall wall = splitChainWalls[i];
            if (wall == null)
            {
                continue;
            }

            int firstVertexId = splitChainVertexIds[i];
            int secondVertexId = splitChainVertexIds[i + 1];
            Vector3 firstPoint = splitChainPoints[i];
            Vector3 secondPoint = splitChainPoints[i + 1];

            Vector3 startPoint;
            Vector3 endPoint;
            if (wall.StartVertexId == firstVertexId && wall.EndVertexId == secondVertexId)
            {
                startPoint = firstPoint;
                endPoint = secondPoint;
            }
            else if (wall.StartVertexId == secondVertexId && wall.EndVertexId == firstVertexId)
            {
                startPoint = secondPoint;
                endPoint = firstPoint;
            }
            else
            {
                startPoint = wall.StartVertexId == firstVertexId ? firstPoint : secondPoint;
                endPoint = wall.EndVertexId == secondVertexId ? secondPoint : firstPoint;
            }

            WallGeometryService.ApplyWallEndpoints(wall, startPoint, endPoint, minimumWallLength, wallLengthDisplay, false);
        }

        return true;
    }

    private bool TryBuildSplitChainFromEndpoint(int draggedVertexId, List<Wall> orderedWalls, List<int> orderedVertexIds, List<Vector3> orderedPoints)
    {
        if (orderedWalls == null || orderedVertexIds == null || orderedPoints == null)
        {
            return false;
        }

        orderedWalls.Clear();
        orderedVertexIds.Clear();
        orderedPoints.Clear();

        GetWallsConnectedToVertex(draggedVertexId, cachedWalls, null);
        if (cachedWalls.Count != 1)
        {
            return false;
        }

        Wall currentWall = cachedWalls[0];
        int currentVertexId = draggedVertexId;
        int nextVertexId = currentWall.GetOppositeVertexId(currentVertexId);
        if (nextVertexId <= 0 || !currentWall.IsSplitPointVertex(nextVertexId))
        {
            return false;
        }

        orderedVertexIds.Add(currentVertexId);
        orderedPoints.Add(GetWallPointForVertex(currentWall, currentVertexId));

        HashSet<Wall> visitedWalls = new HashSet<Wall>();
        while (currentWall != null)
        {
            if (!visitedWalls.Add(currentWall))
            {
                return false;
            }

            nextVertexId = currentWall.GetOppositeVertexId(currentVertexId);
            if (nextVertexId <= 0)
            {
                return false;
            }

            orderedWalls.Add(currentWall);
            orderedVertexIds.Add(nextVertexId);
            orderedPoints.Add(GetWallPointForVertex(currentWall, nextVertexId));

            if (!currentWall.IsSplitPointVertex(nextVertexId))
            {
                return orderedWalls.Count > 1;
            }

            GetWallsConnectedToVertex(nextVertexId, cachedWalls, currentWall);
            if (cachedWalls.Count != 1)
            {
                return false;
            }

            currentVertexId = nextVertexId;
            currentWall = cachedWalls[0];
        }

        return false;
    }

    private void GetWallsConnectedToVertex(int vertexId, List<Wall> results, Wall ignoredWall)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        foreach (KeyValuePair<int, WallHandleEntry> pair in wallEntries)
        {
            Wall wall = pair.Value?.wallComponent;
            if (wall == null || wall == ignoredWall || !wall.ContainsVertexId(vertexId))
            {
                continue;
            }

            results.Add(wall);
        }
    }

    private static Vector3 GetWallPointForVertex(Wall wall, int vertexId)
    {
        if (wall == null)
        {
            return Vector3.zero;
        }

        return wall.StartVertexId == vertexId ? wall.Data.startPoint : wall.Data.endPoint;
    }

    private bool TryGetSplitPointDragSegment(VertexGroup group, out Vector3 segmentStart, out Vector3 segmentEnd)
    {
        segmentStart = Vector3.zero;
        segmentEnd = Vector3.zero;
        if (!IsSplitPointGroup(group))
        {
            return false;
        }

        const float uniquePointThresholdSqr = 0.0001f;
        List<Vector3> uniqueOppositePoints = new List<Vector3>();
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            Wall wall = endpointRef?.entry?.wallComponent;
            if (wall == null)
            {
                continue;
            }

            Vector3 oppositePoint = endpointRef.isStart ? wall.Data.endPoint : wall.Data.startPoint;
            oppositePoint.y = dragPlaneHeight;

            bool alreadyAdded = false;
            for (int j = 0; j < uniqueOppositePoints.Count; j++)
            {
                if ((uniqueOppositePoints[j] - oppositePoint).sqrMagnitude <= uniquePointThresholdSqr)
                {
                    alreadyAdded = true;
                    break;
                }
            }

            if (!alreadyAdded)
            {
                uniqueOppositePoints.Add(oppositePoint);
            }
        }

        if (uniqueOppositePoints.Count < 2)
        {
            return false;
        }

        float maxDistanceSqr = -1f;
        for (int i = 0; i < uniqueOppositePoints.Count - 1; i++)
        {
            for (int j = i + 1; j < uniqueOppositePoints.Count; j++)
            {
                float distanceSqr = (uniqueOppositePoints[i] - uniqueOppositePoints[j]).sqrMagnitude;
                if (distanceSqr <= maxDistanceSqr)
                {
                    continue;
                }

                maxDistanceSqr = distanceSqr;
                segmentStart = uniqueOppositePoints[i];
                segmentEnd = uniqueOppositePoints[j];
            }
        }

        return maxDistanceSqr > minimumWallLength * minimumWallLength;
    }

    private Vector3 ConstrainPointToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 direction = segmentEnd - segmentStart;
        direction.y = 0f;
        float length = direction.magnitude;
        if (length <= 0.0001f)
        {
            point = segmentStart;
            point.y = dragPlaneHeight;
            return point;
        }

        direction /= length;
        float minDistance = Mathf.Min(minimumWallLength, length * 0.5f);
        float maxDistance = Mathf.Max(minDistance, length - minimumWallLength);
        float projectedDistance = Vector3.Dot(point - segmentStart, direction);
        float clampedDistance = Mathf.Clamp(projectedDistance, minDistance, maxDistance);
        Vector3 constrainedPoint = segmentStart + direction * clampedDistance;
        constrainedPoint.y = dragPlaneHeight;
        return constrainedPoint;
    }
}
