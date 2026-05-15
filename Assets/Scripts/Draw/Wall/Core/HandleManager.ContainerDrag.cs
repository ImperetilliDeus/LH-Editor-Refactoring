using UnityEngine;

public partial class HandleManager
{
    private bool TryApplyOpeningContainerEndpointDrag(VertexGroup group, Vector3 newPoint)
    {
        if (group == null || group.endpoints.Count == 0)
        {
            return false;
        }

        WallOpeningContainer container = null;
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            Wall sourceWall = group.endpoints[i]?.entry?.wallComponent;
            if (sourceWall == null)
            {
                continue;
            }

            WallOpeningContainer candidateContainer = sourceWall.GetComponentInParent<WallOpeningContainer>();
            if (candidateContainer == null)
            {
                continue;
            }

            bool isOuterVertex = group.vertexId > 0 &&
                (group.vertexId == candidateContainer.OuterStartVertexId || group.vertexId == candidateContainer.OuterEndVertexId);
            if (!isOuterVertex)
            {
                continue;
            }

            container = candidateContainer;
            break;
        }

        if (container == null)
        {
            return false;
        }

        bool isDraggingStart = group.vertexId > 0 && group.vertexId == container.OuterStartVertexId;
        bool isDraggingEnd = group.vertexId > 0 && group.vertexId == container.OuterEndVertexId;
        if (!isDraggingStart && !isDraggingEnd)
        {
            return false;
        }

        UndoRedoManager.OpeningLayoutSnapshot containerSnapshot =
            wallOpeningPlacementManager != null
                ? wallOpeningPlacementManager.CaptureLayoutSnapshot(container)
                : default;
        if (wallOpeningPlacementManager == null || !containerSnapshot.hasContainer)
        {
            return false;
        }

        if (TryApplyContainerOuterLinearChainDrag(container, group.vertexId, newPoint, out _))
        {
            if (draggingGroup == group &&
                group.vertexId > 0 &&
                groupsByVertexId.TryGetValue(group.vertexId, out VertexGroup rebuiltDraggingGroup))
            {
                draggingGroup = rebuiltDraggingGroup;
            }

            return true;
        }

        if (!TryGetContainerOuterWalls(container, out Wall startWall, out Wall endWall))
        {
            return false;
        }

        Vector3 oldStart = startWall.Data.startPoint;
        Vector3 oldEnd = endWall.Data.endPoint;
        oldStart.y = dragPlaneHeight;
        oldEnd.y = dragPlaneHeight;

        Vector3 fixedPoint = isDraggingStart ? oldEnd : oldStart;
        Vector3 movedPoint = newPoint;
        Vector3 oldDirection = oldEnd - oldStart;
        oldDirection.y = 0f;
        float oldLength = oldDirection.magnitude;
        if (oldLength < minimumWallLength)
        {
            return false;
        }

        oldDirection /= oldLength;

        Vector3 newDirection = isDraggingStart ? (fixedPoint - movedPoint) : (movedPoint - fixedPoint);
        newDirection.y = 0f;
        float newLength = newDirection.magnitude;
        if (newLength < minimumWallLength)
        {
            return false;
        }

        newDirection /= newLength;

        float minimumContainerLength = GetMinimumContainerLengthForEndpointDrag(
            container,
            isDraggingStart,
            oldStart,
            oldEnd,
            oldDirection);
        if (newLength < minimumContainerLength)
        {
            newLength = minimumContainerLength;
            movedPoint = isDraggingStart
                ? fixedPoint - newDirection * newLength
                : fixedPoint + newDirection * newLength;
        }

        Vector3 newStart = isDraggingStart ? movedPoint : fixedPoint;
        Vector3 newEnd = isDraggingStart ? fixedPoint : movedPoint;
        wallOpeningPlacementManager.ApplyContainerSpanFromExternalDrag(container, newStart, newEnd, containerSnapshot);
        if (draggingGroup == group &&
            group.vertexId > 0 &&
            groupsByVertexId.TryGetValue(group.vertexId, out VertexGroup rebuiltGroup))
        {
            draggingGroup = rebuiltGroup;
            group = rebuiltGroup;
        }

        Vector3 appliedOuterPoint = isDraggingStart ? newStart : newEnd;

        affectedOpeningContainers.Clear();
        affectedWallComponents.Clear();
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            Wall wall = endpointRef?.entry?.wallComponent;
            if (wall == null)
            {
                continue;
            }

            WallOpeningContainer connectedContainer = wall.GetComponentInParent<WallOpeningContainer>();
            if (connectedContainer == container)
            {
                continue;
            }

            if (connectedContainer != null)
            {
                affectedOpeningContainers.Add(connectedContainer);
                continue;
            }

            if (!affectedWallComponents.Contains(wall))
            {
                affectedWallComponents.Add(wall);
            }
        }

        foreach (WallOpeningContainer affectedContainer in affectedOpeningContainers)
        {
            if (affectedContainer == null || wallOpeningPlacementManager == null)
            {
                continue;
            }

            UndoRedoManager.OpeningLayoutSnapshot snapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(affectedContainer);
            if (!snapshot.hasContainer)
            {
                continue;
            }

            Vector3 nextStart = snapshot.wallStart;
            Vector3 nextEnd = snapshot.wallEnd;
            if (group.vertexId == affectedContainer.OuterStartVertexId)
            {
                nextStart = appliedOuterPoint;
            }

            if (group.vertexId == affectedContainer.OuterEndVertexId)
            {
                nextEnd = appliedOuterPoint;
            }

            nextStart.y = dragPlaneHeight;
            nextEnd.y = dragPlaneHeight;
            wallOpeningPlacementManager.ApplyContainerSpanFromExternalDrag(affectedContainer, nextStart, nextEnd, snapshot);
        }

        if (affectedWallComponents.Count > 0 &&
            !TryApplySplitPointChainEndpointDrag(group.vertexId, affectedWallComponents, appliedOuterPoint, out _))
        {
            WallGeometryService.ApplyVertexMove(
                affectedWallComponents,
                group.vertexId,
                appliedOuterPoint,
                dragPlaneHeight,
                minimumWallLength,
                wallLengthDisplay);
        }

        return true;
    }

    private bool TryApplyContainerOuterLinearChainDrag(
        WallOpeningContainer container,
        int draggedVertexId,
        Vector3 draggedPoint,
        out Vector3 appliedOuterPoint)
    {
        appliedOuterPoint = draggedPoint;
        if (container == null || draggedVertexId <= 0)
        {
            return false;
        }

        int oppositeVertexId;
        if (draggedVertexId == container.OuterStartVertexId)
        {
            oppositeVertexId = container.OuterEndVertexId;
            if (!container.OuterEndSplitPoint)
            {
                return false;
            }
        }
        else if (draggedVertexId == container.OuterEndVertexId)
        {
            oppositeVertexId = container.OuterStartVertexId;
            if (!container.OuterStartSplitPoint)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        LinearChainElement containerElement = new LinearChainElement
        {
            container = container,
            startVertexId = container.OuterStartVertexId,
            endVertexId = container.OuterEndVertexId,
            startPoint = container.WallStart,
            endPoint = container.WallEnd,
        };

        if (!TryBuildLinearSplitChainFromElement(draggedVertexId, containerElement, splitChainElements, splitChainVertexIds, splitChainPoints))
        {
            return false;
        }

        if (!TryApplyLinearSplitChainKeepingDraggedEndpoint(splitChainElements, splitChainVertexIds, splitChainPoints, draggedPoint))
        {
            return false;
        }

        appliedOuterPoint = splitChainPoints.Count > 0 ? splitChainPoints[0] : draggedPoint;
        return true;
    }

    private float GetMinimumContainerLengthForEndpointDrag(
        WallOpeningContainer container,
        bool isDraggingStart,
        Vector3 oldStart,
        Vector3 oldEnd,
        Vector3 oldDirection)
    {
        if (container == null)
        {
            return minimumWallLength;
        }

        float requiredLength = minimumWallLength;
        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            requiredLength = Mathf.Max(
                requiredLength,
                GetMinimumLengthForContainerPoint(
                    wall.Data.startPoint,
                    wall.StartVertexId,
                    container,
                    isDraggingStart,
                    oldStart,
                    oldEnd,
                    oldDirection));
            requiredLength = Mathf.Max(
                requiredLength,
                GetMinimumLengthForContainerPoint(
                    wall.Data.endPoint,
                    wall.EndVertexId,
                    container,
                    isDraggingStart,
                    oldStart,
                    oldEnd,
                    oldDirection));
        }

        return requiredLength;
    }

    private float GetMinimumLengthForContainerPoint(
        Vector3 point,
        int vertexId,
        WallOpeningContainer container,
        bool isDraggingStart,
        Vector3 oldStart,
        Vector3 oldEnd,
        Vector3 oldDirection)
    {
        point.y = dragPlaneHeight;

        if (container == null)
        {
            return minimumWallLength;
        }

        if (isDraggingStart)
        {
            if (vertexId == container.OuterStartVertexId)
            {
                return minimumWallLength;
            }

            float distanceFromEnd = Vector3.Dot(oldEnd - point, oldDirection);
            return distanceFromEnd + minimumWallLength;
        }

        if (vertexId == container.OuterEndVertexId)
        {
            return minimumWallLength;
        }

        float distanceFromStart = Vector3.Dot(point - oldStart, oldDirection);
        return distanceFromStart + minimumWallLength;
    }

    private bool TryGetContainerOuterWalls(WallOpeningContainer container, out Wall startWall, out Wall endWall)
    {
        startWall = null;
        endWall = null;
        if (container == null)
        {
            return false;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            if (startWall == null && wall.StartVertexId == container.OuterStartVertexId)
            {
                startWall = wall;
            }

            if (endWall == null && wall.EndVertexId == container.OuterEndVertexId)
            {
                endWall = wall;
            }
        }

        return startWall != null && endWall != null;
    }

    private Transform GetContainerSegmentParent(WallOpeningContainer container)
    {
        if (container == null)
        {
            return null;
        }

        Transform segmentParent = container.transform.Find("Segments");
        return segmentParent != null ? segmentParent : container.transform;
    }

    private bool TryGetContainerChildReferenceCenter(Transform child, out Vector3 center)
    {
        center = Vector3.zero;
        if (child == null)
        {
            return false;
        }

        Wall[] walls = child.GetComponentsInChildren<Wall>(true);
        Wall bestWall = null;
        float bestLengthSqr = float.MinValue;
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            float lengthSqr = (wall.Data.endPoint - wall.Data.startPoint).sqrMagnitude;
            if (lengthSqr <= bestLengthSqr)
            {
                continue;
            }

            bestLengthSqr = lengthSqr;
            bestWall = wall;
        }

        if (bestWall != null)
        {
            center = (bestWall.Data.startPoint + bestWall.Data.endPoint) * 0.5f;
            return true;
        }

        WallOpening[] openings = child.GetComponentsInChildren<WallOpening>(true);
        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            center = opening.transform.position;
            return true;
        }

        return false;
    }

    private void ApplyContainerChildTransform(
        Transform child,
        WallOpeningContainer container,
        bool isDraggingStart,
        Vector3 oldStart,
        Vector3 oldEnd,
        Vector3 newStart,
        Vector3 newEnd,
        Vector3 oldDirection,
        Vector3 newDirection,
        Vector3 nextCenter)
    {
        if (child == null)
        {
            return;
        }

        Wall[] walls = child.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            Vector3 nextStart = ResolveContainerEndpoint(
                wall.Data.startPoint,
                wall.StartVertexId,
                isDraggingStart,
                container,
                oldStart,
                oldEnd,
                newStart,
                newEnd,
                oldDirection,
                newDirection);
            Vector3 nextEnd = ResolveContainerEndpoint(
                wall.Data.endPoint,
                wall.EndVertexId,
                isDraggingStart,
                container,
                oldStart,
                oldEnd,
                newStart,
                newEnd,
                oldDirection,
                newDirection);
            wall.TryApplyCurrentProfileAndRefresh(nextStart, nextEnd, minimumWallLength, wallLengthDisplay, false);
        }

        WallOpening[] openings = child.GetComponentsInChildren<WallOpening>(true);
        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            Vector3 openingCenter = nextCenter;
            openingCenter.y = opening.transform.position.y;
            opening.transform.position = openingCenter;
            opening.transform.rotation = Quaternion.LookRotation(newDirection, Vector3.up);
            opening.SetCenterDistance(Vector3.Dot(openingCenter - newStart, newDirection));
        }

        MeshRenderer[] renderers = child.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Transform rendererTransform = renderer.transform;
            if (rendererTransform.GetComponent<Wall>() != null)
            {
                continue;
            }

            if (rendererTransform.GetComponentInParent<WallOpening>() != null)
            {
                continue;
            }

            Vector3 rendererCenter = nextCenter;
            rendererCenter.y = rendererTransform.position.y;
            rendererTransform.position = rendererCenter;
            rendererTransform.rotation = Quaternion.LookRotation(newDirection, Vector3.up);
        }
    }

    private Vector3 ResolveContainerEndpoint(
        Vector3 oldPoint,
        int endpointVertexId,
        bool isDraggingStart,
        WallOpeningContainer container,
        Vector3 oldStart,
        Vector3 oldEnd,
        Vector3 newStart,
        Vector3 newEnd,
        Vector3 oldDirection,
        Vector3 newDirection)
    {
        oldPoint.y = dragPlaneHeight;

        if (container != null)
        {
            if (isDraggingStart && endpointVertexId == container.OuterStartVertexId)
            {
                return newStart;
            }

            if (!isDraggingStart && endpointVertexId == container.OuterEndVertexId)
            {
                return newEnd;
            }
        }

        if (isDraggingStart)
        {
            float distanceFromEnd = Vector3.Dot(oldEnd - oldPoint, oldDirection);
            Vector3 resolved = newEnd - newDirection * distanceFromEnd;
            resolved.y = dragPlaneHeight;
            return resolved;
        }

        float distanceFromStart = Vector3.Dot(oldPoint - oldStart, oldDirection);
        Vector3 next = newStart + newDirection * distanceFromStart;
        next.y = dragPlaneHeight;
        return next;
    }
}
