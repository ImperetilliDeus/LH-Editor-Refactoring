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
            if (HandleGeometryUtility.HasPointerMovedBeyondThreshold(
                    mousePosition,
                    pendingStartMousePosition,
                    clickAllowanceSensitivityPixels))
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
            snappedPoint = HandleGeometryUtility.ConstrainPointToSegment(
                dragPoint,
                segmentStart,
                segmentEnd,
                minimumWallLength,
                dragPlaneHeight);
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

            if (VertexHandleViews.ContainsScreenPoint(group.handleRect, targetCanvas, mousePosition))
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

        dragStartOpeningLayoutSnapshots.Clear();
        CollectAffectedWallsForGroup(draggingGroup, affectedWallObjects, affectedWallComponents);
        for (int i = 0; i < affectedWallComponents.Count; i++)
        {
            Wall wall = affectedWallComponents[i];
            if (wall == null)
            {
                continue;
            }

            WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
            if (container != null && wallOpeningPlacementManager != null && !dragStartOpeningLayoutSnapshots.ContainsKey(container))
            {
                OpeningLayoutSnapshot snapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
                if (snapshot.hasContainer)
                {
                    dragStartOpeningLayoutSnapshots[container] = snapshot;
                }
            }
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

        if (!IsSplitPointGroup(draggingGroup))
        {
            draggingGroup = TryMergeDraggedGroupToNearby(draggingGroup);
        }
        VertexGroup finalizedGroup = draggingGroup;
        int draggedVertexId = finalizedGroup != null ? finalizedGroup.vertexId : 0;
        FinalizeDraggedOpeningContainers(finalizedGroup, draggedVertexId);
        if (draggedVertexId > 0 && groupsByVertexId.TryGetValue(draggedVertexId, out VertexGroup rebuiltGroup))
        {
            draggingGroup = rebuiltGroup;
        }

        if (draggingGroup != null)
        {
            SetGroupColor(draggingGroup, GetBaseColor(draggingGroup));
        }

        draggingGroup = null;
        pendingGroup = null;
        dragStartOpeningLayoutSnapshots.Clear();

        RefreshAllGroupWorldPoints();
        RoomTopologyEvents.RequestRefreshAll();
        EditorVisualEvents.RequestTopViewRefresh();
        handlePositionsDirty = true;
    }

    private void FinalizeDraggedOpeningContainers(VertexGroup group, int draggedVertexId)
    {
        if (group == null || wallOpeningPlacementManager == null)
        {
            return;
        }

        affectedOpeningContainers.Clear();
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            Wall wall = group.endpoints[i]?.entry?.wallComponent;
            if (wall == null)
            {
                continue;
            }

            WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
            if (container != null)
            {
                affectedOpeningContainers.Add(container);
            }
        }

        foreach (WallOpeningContainer container in affectedOpeningContainers)
        {
            if (container == null)
            {
                continue;
            }

            Vector3 groupWorldPoint = group.worldPoint;
            float preferredDistance = Vector3.Dot(groupWorldPoint - container.WallStart, container.WallDirection);
            if (draggedVertexId == container.OuterStartVertexId)
            {
                preferredDistance = 0f;
            }
            else if (draggedVertexId == container.OuterEndVertexId)
            {
                preferredDistance = container.WallLength;
            }

            preferredDistance = Mathf.Clamp(preferredDistance, 0f, container.WallLength);

            if (TryRepartitionDraggedOpeningContainer(group, draggedVertexId, container, groupWorldPoint))
            {
                continue;
            }

            wallOpeningPlacementManager.RebuildOpeningContainer(container);
            wallOpeningPlacementManager.SelectPreferredWallForContainer(container, preferredDistance);
        }

        if (affectedOpeningContainers.Count > 0)
        {
            RefreshRegisteredWalls();
        }
    }

    private bool TryRepartitionDraggedOpeningContainer(
        VertexGroup group,
        int draggedVertexId,
        WallOpeningContainer container,
        Vector3 splitPoint)
    {
        if (group == null ||
            container == null ||
            wallOpeningPlacementManager == null ||
            !dragStartOpeningLayoutSnapshots.TryGetValue(container, out OpeningLayoutSnapshot containerStartSnapshot))
        {
            return false;
        }

        if (!containerStartSnapshot.hasContainer ||
            containerStartSnapshot.openings == null ||
            containerStartSnapshot.openings.Length == 0)
        {
            return false;
        }

        Wall adjacentWall = FindStandaloneSplitAdjacentWall(group, container);
        if (adjacentWall == null)
        {
            return false;
        }

        OpeningLayoutSnapshot adjacentSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(adjacentWall);
        if (adjacentSnapshot.hasContainer)
        {
            return false;
        }

        return wallOpeningPlacementManager.TryRepartitionContainerWithAdjacentWallAtSplit(
            container,
            containerStartSnapshot,
            adjacentWall,
            adjacentSnapshot,
            draggedVertexId,
            splitPoint);
    }

    private static Wall FindStandaloneSplitAdjacentWall(VertexGroup group, WallOpeningContainer container)
    {
        if (group == null || container == null)
        {
            return null;
        }

        for (int i = 0; i < group.endpoints.Count; i++)
        {
            Wall wall = group.endpoints[i]?.entry?.wallComponent;
            if (wall == null)
            {
                continue;
            }

            if (wall.GetComponentInParent<WallOpeningContainer>() == null)
            {
                return wall;
            }
        }

        return null;
    }

    private void ApplyVertexDrag(VertexGroup group, Vector3 newPoint)
    {
        if (group == null)
        {
            return;
        }

        newPoint.y = dragPlaneHeight;

        if (IsSplitPointGroup(group))
        {
            if (TryApplyLogicalSplitPointDrag(group, newPoint))
            {
                RefreshAllGroupWorldPoints();
                MarkHandlePositionsDirty();
            }

            return;
        }

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
            appliedSplitChain = TryApplySplitPointChainEndpointDrag(group.vertexId, affectedWallComponents, newPoint, out appliedPoint);
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

    private bool TryApplySplitPointChainEndpointDrag(int draggedVertexId, List<Wall> directlyConnectedWalls, Vector3 draggedPoint, out Vector3 appliedDraggedPoint)
    {
        appliedDraggedPoint = draggedPoint;
        if (directlyConnectedWalls == null || directlyConnectedWalls.Count == 0)
        {
            return false;
        }

        splitChainCandidates.Clear();
        for (int i = 0; i < directlyConnectedWalls.Count; i++)
        {
            if (!TryCreateLinearChainElement(directlyConnectedWalls[i], draggedVertexId, out LinearChainElement element) ||
                !ContainsVertexId(element, draggedVertexId))
            {
                continue;
            }

            int nextVertexId = GetOppositeVertexId(element, draggedVertexId);
            if (nextVertexId > 0 && IsSplitPointVertex(element, nextVertexId) && !ContainsElement(splitChainCandidates, element))
            {
                splitChainCandidates.Add(element);
            }
        }

        if (splitChainCandidates.Count == 0)
        {
            return false;
        }

        HashSet<Wall> chainWalls = new HashSet<Wall>();
        bool appliedAnyChain = false;
        for (int i = 0; i < splitChainCandidates.Count; i++)
        {
            LinearChainElement chainStartElement = splitChainCandidates[i];
            if (!TryBuildLinearSplitChainFromElement(draggedVertexId, chainStartElement, splitChainElements, splitChainVertexIds, splitChainPoints))
            {
                continue;
            }

            if (!TryApplyLinearSplitChainKeepingDraggedEndpoint(splitChainElements, splitChainVertexIds, splitChainPoints, draggedPoint))
            {
                continue;
            }

            appliedAnyChain = true;
            appliedDraggedPoint = splitChainPoints.Count > 0 ? splitChainPoints[0] : draggedPoint;
            for (int j = 0; j < splitChainElements.Count; j++)
            {
                LinearChainElement chainElement = splitChainElements[j];
                if (chainElement?.wall != null)
                {
                    chainWalls.Add(chainElement.wall);
                }
            }
        }

        if (!appliedAnyChain)
        {
            return false;
        }

        List<Wall> nonChainWalls = new List<Wall>();
        for (int i = 0; i < directlyConnectedWalls.Count; i++)
        {
            Wall wall = directlyConnectedWalls[i];
            if (wall != null && !chainWalls.Contains(wall))
            {
                nonChainWalls.Add(wall);
            }
        }

        if (nonChainWalls.Count > 0)
        {
            WallGeometryService.ApplyVertexMove(nonChainWalls, draggedVertexId, draggedPoint, dragPlaneHeight, minimumWallLength, wallLengthDisplay);
        }

        return true;
    }

    private bool TryBuildSplitChainFromWall(int startVertexId, Wall startingWall, List<Wall> orderedWalls, List<int> orderedVertexIds, List<Vector3> orderedPoints)
    {
        if (startingWall == null || orderedWalls == null || orderedVertexIds == null || orderedPoints == null)
        {
            return false;
        }

        orderedWalls.Clear();
        orderedVertexIds.Clear();
        orderedPoints.Clear();

        int currentVertexId = startVertexId;
        if (!startingWall.ContainsVertexId(currentVertexId))
        {
            return false;
        }

        int nextVertexId = startingWall.GetOppositeVertexId(currentVertexId);
        if (nextVertexId <= 0 || !startingWall.IsSplitPointVertex(nextVertexId))
        {
            return false;
        }

        Wall currentWall = startingWall;
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
            if (wall == null || wall == ignoredWall || !wall.gameObject.activeInHierarchy || !wall.ContainsVertexId(vertexId))
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
        if (group == null || !IsSplitPointGroup(group))
        {
            return false;
        }

        connectedChainElements.Clear();
        GetWallsConnectedToVertex(group.vertexId, cachedWalls, null);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (!TryCreateLinearChainElement(wall, group.vertexId, out LinearChainElement element) ||
                ContainsElement(connectedChainElements, element))
            {
                continue;
            }

            connectedChainElements.Add(element);
        }

        return TryGetLinearChainSplitPointDragSegment(
            group.vertexId,
            connectedChainElements,
            out segmentStart,
            out segmentEnd);
    }

    private bool TryGetLinearChainSplitPointDragSegment(
        int vertexId,
        List<LinearChainElement> elements,
        out Vector3 segmentStart,
        out Vector3 segmentEnd)
    {
        segmentStart = Vector3.zero;
        segmentEnd = Vector3.zero;
        if (elements == null || elements.Count == 0)
        {
            return false;
        }

        LinearChainElement primaryElement = ResolvePrimarySplitDragElement(vertexId, elements);
        if (primaryElement == null)
        {
            return false;
        }

        Vector3 axis = primaryElement.endPoint - primaryElement.startPoint;
        axis.y = 0f;
        float axisLength = axis.magnitude;
        if (axisLength <= minimumWallLength)
        {
            return false;
        }

        axis /= axisLength;
        Vector3 origin = primaryElement.startPoint;
        origin.y = dragPlaneHeight;
        float minDistance = 0f;
        float maxDistance = axisLength;

        const float collinearDotThreshold = 0.999f;
        for (int i = 0; i < elements.Count; i++)
        {
            LinearChainElement element = elements[i];
            if (element == null || !ContainsVertexId(element, vertexId))
            {
                continue;
            }

            Vector3 direction = element.endPoint - element.startPoint;
            direction.y = 0f;
            float length = direction.magnitude;
            if (length <= minimumWallLength)
            {
                continue;
            }

            direction /= length;
            if (Mathf.Abs(Vector3.Dot(direction, axis)) < collinearDotThreshold)
            {
                continue;
            }

            float startDistance = Vector3.Dot(element.startPoint - origin, axis);
            float endDistance = Vector3.Dot(element.endPoint - origin, axis);
            minDistance = Mathf.Min(minDistance, startDistance, endDistance);
            maxDistance = Mathf.Max(maxDistance, startDistance, endDistance);
        }

        if (maxDistance - minDistance <= minimumWallLength * 2f)
        {
            return false;
        }

        segmentStart = origin + axis * minDistance;
        segmentEnd = origin + axis * maxDistance;
        segmentStart.y = dragPlaneHeight;
        segmentEnd.y = dragPlaneHeight;
        return true;
    }

    private static LinearChainElement ResolvePrimarySplitDragElement(int vertexId, List<LinearChainElement> elements)
    {
        for (int i = 0; i < elements.Count; i++)
        {
            LinearChainElement element = elements[i];
            if (element != null &&
                element.IsContainer &&
                IsDraggingOuterSplitPoint(element.container, vertexId))
            {
                return element;
            }
        }

        for (int i = 0; i < elements.Count; i++)
        {
            LinearChainElement element = elements[i];
            if (element != null && ContainsVertexId(element, vertexId))
            {
                return element;
            }
        }

        return null;
    }

    private bool TryApplyLogicalSplitPointDrag(VertexGroup group, Vector3 newPoint)
    {
        if (group == null || !IsSplitPointGroup(group))
        {
            return false;
        }

        connectedChainElements.Clear();
        GetWallsConnectedToVertex(group.vertexId, cachedWalls, null);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (!TryCreateLinearChainElement(wall, group.vertexId, out LinearChainElement element) ||
                ContainsElement(connectedChainElements, element))
            {
                continue;
            }

            connectedChainElements.Add(element);
        }

        if (connectedChainElements.Count == 0)
        {
            return false;
        }

        Vector3 constrainedPoint = ConstrainSplitPointDragAgainstOpeningContainers(group.vertexId, connectedChainElements, newPoint);
        if (!CanApplySplitPointDrag(group.vertexId, connectedChainElements, constrainedPoint))
        {
            return false;
        }

        List<int> deferredContainerIndices = new List<int>();
        bool applied = false;
        for (int i = 0; i < connectedChainElements.Count; i++)
        {
            LinearChainElement element = connectedChainElements[i];
            if (element != null && element.IsContainer)
            {
                deferredContainerIndices.Add(i);
                continue;
            }

            if (element == null)
            {
                continue;
            }

            Vector3 startPoint = element.startPoint;
            Vector3 endPoint = element.endPoint;

            if (element.startVertexId == group.vertexId)
            {
                startPoint = constrainedPoint;
            }

            if (element.endVertexId == group.vertexId)
            {
                endPoint = constrainedPoint;
            }

            applied |= TryApplyLinearChainElementSpan(element, startPoint, endPoint);
        }

        for (int i = 0; i < deferredContainerIndices.Count; i++)
        {
            int elementIndex = deferredContainerIndices[i];
            LinearChainElement element = connectedChainElements[elementIndex];
            if (element == null)
            {
                continue;
            }

            Vector3 startPoint = element.startPoint;
            Vector3 endPoint = element.endPoint;

            if (element.startVertexId == group.vertexId)
            {
                startPoint = constrainedPoint;
            }

            if (element.endVertexId == group.vertexId)
            {
                endPoint = constrainedPoint;
            }

            applied |= TryApplyLinearChainElementSpan(element, startPoint, endPoint);
        }

        return applied;
    }

    private bool CanApplySplitPointDrag(
        int draggedVertexId,
        List<LinearChainElement> connectedElements,
        Vector3 desiredPoint)
    {
        if (connectedElements == null || connectedElements.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < connectedElements.Count; i++)
        {
            LinearChainElement element = connectedElements[i];
            if (element == null)
            {
                continue;
            }

            Vector3 startPoint = element.startPoint;
            Vector3 endPoint = element.endPoint;
            if (element.startVertexId == draggedVertexId)
            {
                startPoint = desiredPoint;
            }
            else if (element.endVertexId == draggedVertexId)
            {
                endPoint = desiredPoint;
            }
            else
            {
                continue;
            }

            if (element.IsContainer && element.container != null)
            {
                bool isDraggingStart = draggedVertexId == element.container.OuterStartVertexId;
                bool isDraggingEnd = draggedVertexId == element.container.OuterEndVertexId;
                if (!isDraggingStart && !isDraggingEnd)
                {
                    continue;
                }

                Vector3 oldStart = element.container.WallStart;
                Vector3 oldEnd = element.container.WallEnd;
                oldStart.y = dragPlaneHeight;
                oldEnd.y = dragPlaneHeight;
                Vector3 oldDirection = oldEnd - oldStart;
                oldDirection.y = 0f;
                float oldLength = oldDirection.magnitude;
                if (oldLength < minimumWallLength)
                {
                    return false;
                }

                oldDirection /= oldLength;

                Vector3 fixedPoint = isDraggingStart ? oldEnd : oldStart;
                Vector3 movedPoint = isDraggingStart ? startPoint : endPoint;
                Vector3 newDirection = isDraggingStart ? (fixedPoint - movedPoint) : (movedPoint - fixedPoint);
                newDirection.y = 0f;
                float newLength = newDirection.magnitude;
                float minimumContainerLength = IsDraggingOuterSplitPoint(element.container, draggedVertexId)
                    ? minimumWallLength
                    : GetMinimumContainerLengthForEndpointDrag(
                        element.container,
                        isDraggingStart,
                        oldStart,
                        oldEnd,
                        oldDirection);
                if (newLength < minimumContainerLength)
                {
                    return false;
                }

                continue;
            }

            Vector3 flatDirection = endPoint - startPoint;
            flatDirection.y = 0f;
            if (flatDirection.magnitude < minimumWallLength)
            {
                return false;
            }
        }

        return true;
    }

    private Vector3 ConstrainSplitPointDragAgainstOpeningContainers(
        int draggedVertexId,
        List<LinearChainElement> connectedElements,
        Vector3 desiredPoint)
    {
        if (wallOpeningPlacementManager == null || connectedElements == null || connectedElements.Count == 0)
        {
            return desiredPoint;
        }

        Vector3 constrainedPoint = desiredPoint;
        for (int i = 0; i < connectedElements.Count; i++)
        {
            LinearChainElement element = connectedElements[i];
            if (element == null || !element.IsContainer || element.container == null)
            {
                continue;
            }

            if (!wallOpeningPlacementManager.TryConstrainContainerOuterSplitPointDrag(
                    element.container,
                    draggedVertexId,
                    constrainedPoint,
                    out Vector3 nextPoint))
            {
                continue;
            }

            constrainedPoint = nextPoint;

            bool isDraggingStart = draggedVertexId == element.container.OuterStartVertexId;
            bool isDraggingEnd = draggedVertexId == element.container.OuterEndVertexId;
            if (!isDraggingStart && !isDraggingEnd)
            {
                continue;
            }

            Vector3 oldStart = element.container.WallStart;
            Vector3 oldEnd = element.container.WallEnd;
            oldStart.y = dragPlaneHeight;
            oldEnd.y = dragPlaneHeight;

            Vector3 oldDirection = oldEnd - oldStart;
            oldDirection.y = 0f;
            float oldLength = oldDirection.magnitude;
            if (oldLength < minimumWallLength)
            {
                continue;
            }

            oldDirection /= oldLength;

            Vector3 fixedPoint = isDraggingStart ? oldEnd : oldStart;
            Vector3 newDirection = constrainedPoint - fixedPoint;
            newDirection.y = 0f;
            float newLength = newDirection.magnitude;
            if (newLength < minimumWallLength)
            {
                continue;
            }

            float minimumContainerLength = IsDraggingOuterSplitPoint(element.container, draggedVertexId)
                ? minimumWallLength
                : GetMinimumContainerLengthForEndpointDrag(
                    element.container,
                    isDraggingStart,
                    oldStart,
                    oldEnd,
                    oldDirection);
            if (newLength < minimumContainerLength)
            {
                Vector3 movedPoint = PolylineXZ.PointAtDistanceToward(fixedPoint, constrainedPoint, minimumContainerLength);
                movedPoint.y = constrainedPoint.y;
                constrainedPoint = movedPoint;
            }
        }

        return constrainedPoint;
    }

    private static bool IsDraggingOuterSplitPoint(WallOpeningContainer container, int draggedVertexId)
    {
        return container != null &&
            ((draggedVertexId == container.OuterStartVertexId && container.OuterStartSplitPoint) ||
             (draggedVertexId == container.OuterEndVertexId && container.OuterEndSplitPoint));
    }

    private bool TryCreateLinearChainElement(Wall sourceWall, int vertexId, out LinearChainElement element)
    {
        element = null;
        if (sourceWall == null || !sourceWall.gameObject.activeInHierarchy)
        {
            return false;
        }

        WallOpeningContainer container = sourceWall.GetComponentInParent<WallOpeningContainer>();
        if (container != null)
        {
            bool usesOuterVertex = vertexId <= 0 ||
                                   container.OuterStartVertexId == vertexId ||
                                   container.OuterEndVertexId == vertexId;
            if (usesOuterVertex)
            {
                element = new LinearChainElement
                {
                    container = container,
                    startVertexId = container.OuterStartVertexId,
                    endVertexId = container.OuterEndVertexId,
                    startPoint = container.WallStart,
                    endPoint = container.WallEnd,
                };
                return vertexId <= 0 || ContainsVertexId(element, vertexId);
            }

            if (sourceWall.ContainsVertexId(vertexId))
            {
                element = new LinearChainElement
                {
                    wall = sourceWall,
                    startVertexId = sourceWall.StartVertexId,
                    endVertexId = sourceWall.EndVertexId,
                    startPoint = sourceWall.Data.startPoint,
                    endPoint = sourceWall.Data.endPoint,
                };
                return true;
            }

            return false;
        }

        element = new LinearChainElement
        {
            wall = sourceWall,
            startVertexId = sourceWall.StartVertexId,
            endVertexId = sourceWall.EndVertexId,
            startPoint = sourceWall.Data.startPoint,
            endPoint = sourceWall.Data.endPoint,
        };

        return vertexId <= 0 || ContainsVertexId(element, vertexId);
    }

    private void GetConnectedLinearChainElements(int vertexId, LinearChainElement ignoredElement, List<LinearChainElement> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        foreach (KeyValuePair<int, WallHandleEntry> pair in wallEntries)
        {
            Wall wall = pair.Value?.wallComponent;
            if (!TryCreateLinearChainElement(wall, vertexId, out LinearChainElement element) ||
                !ContainsVertexId(element, vertexId) ||
                IsSameElement(element, ignoredElement) ||
                ContainsElement(results, element))
            {
                continue;
            }

            results.Add(element);
        }
    }

    private bool TryBuildLinearSplitChainFromElement(
        int startVertexId,
        LinearChainElement startingElement,
        List<LinearChainElement> orderedElements,
        List<int> orderedVertexIds,
        List<Vector3> orderedPoints)
    {
        if (startingElement == null || orderedElements == null || orderedVertexIds == null || orderedPoints == null)
        {
            return false;
        }

        orderedElements.Clear();
        orderedVertexIds.Clear();
        orderedPoints.Clear();

        int currentVertexId = startVertexId;
        if (!ContainsVertexId(startingElement, currentVertexId))
        {
            return false;
        }

        int nextVertexId = GetOppositeVertexId(startingElement, currentVertexId);
        if (nextVertexId <= 0 || !IsSplitPointVertex(startingElement, nextVertexId))
        {
            return false;
        }

        LinearChainElement currentElement = startingElement;
        orderedVertexIds.Add(currentVertexId);
        orderedPoints.Add(GetPointForVertex(currentElement, currentVertexId));

        HashSet<string> visitedElements = new HashSet<string>();
        while (currentElement != null)
        {
            if (!visitedElements.Add(GetElementKey(currentElement)))
            {
                return false;
            }

            nextVertexId = GetOppositeVertexId(currentElement, currentVertexId);
            if (nextVertexId <= 0)
            {
                return false;
            }

            orderedElements.Add(currentElement);
            orderedVertexIds.Add(nextVertexId);
            orderedPoints.Add(GetPointForVertex(currentElement, nextVertexId));

            if (!IsSplitPointVertex(currentElement, nextVertexId))
            {
                return orderedElements.Count > 0;
            }

            GetConnectedLinearChainElements(nextVertexId, currentElement, connectedChainElements);
            if (connectedChainElements.Count != 1)
            {
                return false;
            }

            currentVertexId = nextVertexId;
            currentElement = connectedChainElements[0];
        }

        return false;
    }

    private bool TryApplyLinearSplitChainKeepingDraggedEndpoint(
        List<LinearChainElement> orderedElements,
        List<int> orderedVertexIds,
        List<Vector3> orderedPoints,
        Vector3 draggedPoint)
    {
        if (orderedElements == null || orderedVertexIds == null || orderedPoints == null)
        {
            return false;
        }

        if (orderedElements.Count == 0 || orderedPoints.Count != orderedElements.Count + 1)
        {
            return false;
        }

        if (!PolylineXZ.TryRescaleKeepingEnd(orderedPoints, draggedPoint, minimumWallLength, dragPlaneHeight, splitChainSegmentLengths))
        {
            return false;
        }

        List<int> deferredContainerIndices = new List<int>();

        for (int i = 0; i < orderedElements.Count; i++)
        {
            LinearChainElement element = orderedElements[i];
            if (element == null)
            {
                continue;
            }

            if (element.IsContainer)
            {
                deferredContainerIndices.Add(i);
                continue;
            }

            int firstVertexId = orderedVertexIds[i];
            int secondVertexId = orderedVertexIds[i + 1];
            Vector3 firstPoint = orderedPoints[i];
            Vector3 secondPoint = orderedPoints[i + 1];

            if (!TryResolveLinearChainElementSpan(element, firstVertexId, secondVertexId, firstPoint, secondPoint, out Vector3 startPoint, out Vector3 endPoint))
            {
                return false;
            }

            if (!TryApplyLinearChainElementSpan(element, startPoint, endPoint))
            {
                return false;
            }
        }

        for (int i = 0; i < deferredContainerIndices.Count; i++)
        {
            int elementIndex = deferredContainerIndices[i];
            LinearChainElement element = orderedElements[elementIndex];
            if (element == null)
            {
                continue;
            }

            int firstVertexId = orderedVertexIds[elementIndex];
            int secondVertexId = orderedVertexIds[elementIndex + 1];
            Vector3 firstPoint = orderedPoints[elementIndex];
            Vector3 secondPoint = orderedPoints[elementIndex + 1];

            if (!TryResolveLinearChainElementSpan(element, firstVertexId, secondVertexId, firstPoint, secondPoint, out Vector3 startPoint, out Vector3 endPoint))
            {
                return false;
            }

            if (!TryApplyLinearChainElementSpan(element, startPoint, endPoint))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveLinearChainElementSpan(
        LinearChainElement element,
        int firstVertexId,
        int secondVertexId,
        Vector3 firstPoint,
        Vector3 secondPoint,
        out Vector3 startPoint,
        out Vector3 endPoint)
    {
        bool forward = element != null && element.startVertexId == firstVertexId && element.endVertexId == secondVertexId;
        bool reverse = element != null && element.startVertexId == secondVertexId && element.endVertexId == firstVertexId;
        startPoint = forward ? firstPoint : reverse ? secondPoint : Vector3.zero;
        endPoint = forward ? secondPoint : reverse ? firstPoint : Vector3.zero;
        return forward || reverse;
    }


    private bool TryApplyLinearChainElementSpan(LinearChainElement element, Vector3 startPoint, Vector3 endPoint)
    {
        if (element == null)
        {
            return false;
        }

        startPoint.y = dragPlaneHeight;
        endPoint.y = dragPlaneHeight;

        if (element.IsContainer)
        {
            if (wallOpeningPlacementManager == null)
            {
                return false;
            }

            OpeningLayoutSnapshot snapshot = dragStartOpeningLayoutSnapshots.TryGetValue(
                element.container,
                out OpeningLayoutSnapshot dragStartSnapshot)
                ? dragStartSnapshot
                : wallOpeningPlacementManager.CaptureLayoutSnapshot(element.container);
            if (!snapshot.hasContainer)
            {
                return false;
            }

            wallOpeningPlacementManager.ApplyContainerSpanFromExternalDrag(
                element.container,
                startPoint,
                endPoint,
                snapshot,
                rebuildDuringDrag: false);
            return true;
        }

        return WallGeometryService.ApplyWallEndpoints(
            element.wall,
            startPoint,
            endPoint,
            minimumWallLength,
            wallLengthDisplay,
            false);
    }

    private static bool ContainsVertexId(LinearChainElement element, int vertexId)
    {
        return element != null && (element.startVertexId == vertexId || element.endVertexId == vertexId);
    }

    private static int GetOppositeVertexId(LinearChainElement element, int vertexId)
    {
        if (element == null)
        {
            return 0;
        }

        if (element.startVertexId == vertexId)
        {
            return element.endVertexId;
        }

        if (element.endVertexId == vertexId)
        {
            return element.startVertexId;
        }

        return 0;
    }

    private bool IsSplitPointVertex(LinearChainElement element, int vertexId)
    {
        if (element == null)
        {
            return false;
        }

        if (element.IsContainer)
        {
            return (element.startVertexId == vertexId && element.container.OuterStartSplitPoint) ||
                   (element.endVertexId == vertexId && element.container.OuterEndSplitPoint);
        }

        return element.wall != null && element.wall.IsSplitPointVertex(vertexId);
    }

    private static Vector3 GetPointForVertex(LinearChainElement element, int vertexId)
    {
        if (element == null)
        {
            return Vector3.zero;
        }

        if (element.startVertexId == vertexId)
        {
            return element.startPoint;
        }

        if (element.endVertexId == vertexId)
        {
            return element.endPoint;
        }

        return Vector3.zero;
    }

    private static string GetElementKey(LinearChainElement element)
    {
        if (element == null)
        {
            return string.Empty;
        }

        return element.IsContainer
            ? $"container:{element.container.GetInstanceID()}"
            : $"wall:{element.wall.GetInstanceID()}";
    }

    private static bool IsSameElement(LinearChainElement left, LinearChainElement right)
    {
        if (left == null || right == null)
        {
            return false;
        }

        if (left.IsContainer || right.IsContainer)
        {
            return left.container == right.container;
        }

        return left.wall == right.wall;
    }

    private static bool ContainsElement(List<LinearChainElement> elements, LinearChainElement candidate)
    {
        if (elements == null || candidate == null)
        {
            return false;
        }

        for (int i = 0; i < elements.Count; i++)
        {
            if (IsSameElement(elements[i], candidate))
            {
                return true;
            }
        }

        return false;
    }

}
