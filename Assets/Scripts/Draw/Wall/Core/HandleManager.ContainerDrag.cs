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
        Vector3 newStart = isDraggingStart ? movedPoint : fixedPoint;
        Vector3 newEnd = isDraggingStart ? fixedPoint : movedPoint;
        container.SetWallSpan(newStart, newEnd);

        containerChildren.Clear();
        for (int i = 0; i < container.transform.childCount; i++)
        {
            Transform child = container.transform.GetChild(i);
            if (child != null)
            {
                containerChildren.Add(child);
            }
        }

        for (int i = 0; i < containerChildren.Count; i++)
        {
            Transform child = containerChildren[i];
            if (child == null)
            {
                continue;
            }

            Vector3 childCenter = child.position;
            float projectedCenter = Vector3.Dot(childCenter - oldStart, oldDirection);
            float distanceFromStart = projectedCenter;
            float distanceFromEnd = oldLength - projectedCenter;
            float newProjectedCenter = isDraggingStart
                ? newLength - distanceFromEnd
                : distanceFromStart;

            Vector3 nextCenter = newStart + newDirection * newProjectedCenter;
            nextCenter.y = childCenter.y;
            child.position = nextCenter;
            child.rotation = Quaternion.LookRotation(newDirection, Vector3.up);

            if (child.TryGetComponent(out WallOpening opening))
            {
                opening.SetCenterDistance(Vector3.Dot(nextCenter - newStart, newDirection));
            }

            if (child.TryGetComponent(out Wall wall))
            {
                Vector3 wallStartPoint = wall.Data.startPoint;
                Vector3 wallEndPoint = wall.Data.endPoint;
                Vector3 nextStart = ResolveContainerEndpoint(
                    wallStartPoint,
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
                    wallEndPoint,
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
        }

        affectedWallComponents.Clear();
        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            Wall wall = endpointRef?.entry?.wallComponent;
            if (wall == null || wall.GetComponentInParent<WallOpeningContainer>() == container)
            {
                continue;
            }

            affectedWallComponents.Add(wall);
        }

        if (affectedWallComponents.Count > 0)
        {
            WallGeometryService.ApplyVertexMove(
                affectedWallComponents,
                group.vertexId,
                newPoint,
                dragPlaneHeight,
                minimumWallLength,
                wallLengthDisplay);
        }

        return true;
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
