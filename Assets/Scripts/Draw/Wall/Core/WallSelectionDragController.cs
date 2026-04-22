using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionDragController
{
    public void PrepareDrag(
        WallSelectionDragState dragState,
        GameObject selectedWallObject,
        float dragPlaneHeight,
        float connectedEndpointThreshold,
        Transform wallRoot,
        List<Wall> rootWalls,
        List<Wall> cachedWalls,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        Action syncAllWallComponentEndpoints)
    {
        if (dragState == null)
        {
            return;
        }

        dragState.Reset();
        syncAllWallComponentEndpoints?.Invoke();

        if (selectedWallObject == null)
        {
            return;
        }

        Wall selectedWallComponent = selectedWallObject.GetComponent<Wall>();
        if (selectedWallComponent == null)
        {
            return;
        }

        dragState.DragSelectedStartPoint = selectedWallComponent.Data.startPoint;
        dragState.DragSelectedEndPoint = selectedWallComponent.Data.endPoint;
        dragState.DragSelectedStartPoint.y = dragPlaneHeight;
        dragState.DragSelectedEndPoint.y = dragPlaneHeight;
        dragState.DragSelectedStartVertexId = selectedWallComponent.StartVertexId;
        dragState.DragSelectedEndVertexId = selectedWallComponent.EndVertexId;

        WallOpeningContainer openingContainer = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (openingContainer != null)
        {
            PrepareOpeningContainerDrag(
                dragState,
                openingContainer,
                dragPlaneHeight,
                connectedEndpointThreshold,
                wallRoot,
                rootWalls,
                cachedWalls,
                wallOpeningPlacementManager);
            return;
        }

        if (wallRoot == null)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (!SharesDragEndpoint(dragState, wall, connectedEndpointThreshold) && wall.gameObject != selectedWallObject)
            {
                continue;
            }

            WallOpeningContainer connectedContainer = wall.GetComponentInParent<WallOpeningContainer>();
            if (connectedContainer != null)
            {
                if (!dragState.DragAffectedOpeningContainers.Contains(connectedContainer))
                {
                    dragState.DragAffectedOpeningContainers.Add(connectedContainer);
                    if (wallOpeningPlacementManager != null)
                    {
                        dragState.MoveStartConnectedOpeningSnapshots[connectedContainer] =
                            wallOpeningPlacementManager.CaptureLayoutSnapshot(connectedContainer);
                    }
                }

                continue;
            }

            dragState.DragAffectedWalls.Add(wall);
            dragState.MoveStartSnapshots[wall.gameObject] = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject);
            dragState.MoveStartEndpointSnapshots[wall.gameObject] = new WallGeometryService.WallEndpointState
            {
                start = wall.Data.startPoint,
                end = wall.Data.endPoint,
            };
        }
    }

    public void ApplyDrag(
        WallSelectionDragState dragState,
        GameObject selectedWallObject,
        Vector3 translationDelta,
        Vector3 selectedTargetPosition,
        float dragPlaneHeight,
        float connectedEndpointThreshold,
        float minimumWallLength,
        WallLengthDisplay wallLengthDisplay,
        HandleManager handleManager,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        List<Wall> cachedWalls,
        Action<Transform> syncWallComponentEndpoints,
        Action markTopViewDirty)
    {
        if (dragState == null || selectedWallObject == null)
        {
            return;
        }

        if (dragState.SelectedOpeningContainer != null)
        {
            ApplyOpeningContainerDrag(
                dragState,
                translationDelta,
                dragPlaneHeight,
                connectedEndpointThreshold,
                minimumWallLength,
                wallLengthDisplay,
                handleManager,
                cachedWalls,
                markTopViewDirty);
            return;
        }

        if (dragState.DragAffectedWalls.Count == 0)
        {
            selectedWallObject.transform.position = selectedTargetPosition;
            syncWallComponentEndpoints?.Invoke(selectedWallObject.transform);
            handleManager?.RefreshHandleVisuals();
            RoomTopologyEvents.RequestRefreshAll();
            markTopViewDirty?.Invoke();
            return;
        }

        WallGeometryService.ConnectedWallMoveContext moveContext = BuildMoveContext(
            dragState,
            translationDelta,
            dragPlaneHeight,
            connectedEndpointThreshold,
            minimumWallLength);

        WallGeometryService.ApplyConnectedWallMove(
            dragState.DragAffectedWalls,
            dragState.MoveStartEndpointSnapshots,
            moveContext,
            wallLengthDisplay);

        if (wallOpeningPlacementManager != null)
        {
            for (int i = 0; i < dragState.DragAffectedOpeningContainers.Count; i++)
            {
                WallOpeningContainer container = dragState.DragAffectedOpeningContainers[i];
                if (container == null ||
                    !dragState.MoveStartConnectedOpeningSnapshots.TryGetValue(container, out UndoRedoManager.OpeningLayoutSnapshot snapshot))
                {
                    continue;
                }

                Vector3 nextStart = WallGeometryService.ResolveDraggedEndpoint(container.OuterStartVertexId, snapshot.wallStart, moveContext);
                Vector3 nextEnd = WallGeometryService.ResolveDraggedEndpoint(container.OuterEndVertexId, snapshot.wallEnd, moveContext);
                nextStart.y = dragPlaneHeight;
                nextEnd.y = dragPlaneHeight;
                wallOpeningPlacementManager.ApplyContainerSpanFromExternalDrag(container, nextStart, nextEnd, snapshot);
            }
        }

        handleManager?.RefreshHandleVisuals();
        RoomTopologyEvents.RequestRefreshAll();
        markTopViewDirty?.Invoke();
    }

    private void PrepareOpeningContainerDrag(
        WallSelectionDragState dragState,
        WallOpeningContainer openingContainer,
        float dragPlaneHeight,
        float connectedEndpointThreshold,
        Transform wallRoot,
        List<Wall> rootWalls,
        List<Wall> cachedWalls,
        WallOpeningPlacementManager wallOpeningPlacementManager)
    {
        dragState.SelectedOpeningContainer = openingContainer;
        dragState.MoveStartContainerPosition = openingContainer.transform.position;
        dragState.MoveStartContainerWallStart = openingContainer.WallStart;
        dragState.MoveStartContainerWallEnd = openingContainer.WallEnd;
        dragState.DragSelectedStartPoint = openingContainer.WallStart;
        dragState.DragSelectedEndPoint = openingContainer.WallEnd;
        dragState.DragSelectedStartPoint.y = dragPlaneHeight;
        dragState.DragSelectedEndPoint.y = dragPlaneHeight;
        dragState.DragSelectedStartVertexId = openingContainer.OuterStartVertexId;
        dragState.DragSelectedEndVertexId = openingContainer.OuterEndVertexId;

        if (wallOpeningPlacementManager != null)
        {
            dragState.MoveStartOpeningLayoutSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(openingContainer);
            dragState.HasMoveStartOpeningLayoutSnapshot = true;
        }

        if (wallRoot != null && rootWalls != null)
        {
            for (int i = 0; i < rootWalls.Count; i++)
            {
                Wall wall = rootWalls[i];
                if (wall == null || wall.GetComponentInParent<WallOpeningContainer>() == openingContainer)
                {
                    continue;
                }

                if (!SharesDragEndpoint(dragState, wall, connectedEndpointThreshold))
                {
                    continue;
                }

                dragState.DragAffectedWalls.Add(wall);
                dragState.MoveStartSnapshots[wall.gameObject] = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject);
                dragState.MoveStartEndpointSnapshots[wall.gameObject] = new WallGeometryService.WallEndpointState
                {
                    start = wall.Data.startPoint,
                    end = wall.Data.endPoint,
                };
            }
        }

        WallHierarchyUtility.CollectWalls(openingContainer.transform, cachedWalls, true);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            dragState.MoveStartEndpointSnapshots[wall.gameObject] = new WallGeometryService.WallEndpointState
            {
                start = wall.Data.startPoint,
                end = wall.Data.endPoint,
            };
        }
    }

    private void ApplyOpeningContainerDrag(
        WallSelectionDragState dragState,
        Vector3 translationDelta,
        float dragPlaneHeight,
        float connectedEndpointThreshold,
        float minimumWallLength,
        WallLengthDisplay wallLengthDisplay,
        HandleManager handleManager,
        List<Wall> cachedWalls,
        Action markTopViewDirty)
    {
        if (dragState.SelectedOpeningContainer == null)
        {
            return;
        }

        dragState.SelectedOpeningContainer.transform.position = dragState.MoveStartContainerPosition + translationDelta;
        dragState.SelectedOpeningContainer.SetWallSpan(
            dragState.MoveStartContainerWallStart + translationDelta,
            dragState.MoveStartContainerWallEnd + translationDelta);

        WallHierarchyUtility.CollectWalls(dragState.SelectedOpeningContainer.transform, cachedWalls, true);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (!dragState.MoveStartEndpointSnapshots.TryGetValue(wall.gameObject, out WallGeometryService.WallEndpointState state))
            {
                continue;
            }

            wall.CopyDataFrom(new WallData(
                state.start + translationDelta,
                state.end + translationDelta,
                wall.Data.thickness,
                wall.Data.height,
                wall.Data.centerY));
            wall.RefreshLengthDisplay(wallLengthDisplay, false);
        }

        if (dragState.DragAffectedWalls.Count == 0)
        {
            handleManager?.RefreshHandleVisuals();
            RoomTopologyEvents.RequestRefreshAll();
            markTopViewDirty?.Invoke();
            return;
        }

        WallGeometryService.ConnectedWallMoveContext moveContext = BuildMoveContext(
            dragState,
            translationDelta,
            dragPlaneHeight,
            connectedEndpointThreshold,
            minimumWallLength);

        WallGeometryService.ApplyConnectedWallMove(
            dragState.DragAffectedWalls,
            dragState.MoveStartEndpointSnapshots,
            moveContext,
            wallLengthDisplay);
        handleManager?.RefreshHandleVisuals();
        RoomTopologyEvents.RequestRefreshAll();
        markTopViewDirty?.Invoke();
    }

    private static bool SharesDragEndpoint(WallSelectionDragState dragState, Wall wall, float connectedEndpointThreshold)
    {
        bool sharesStartVertex = dragState.DragSelectedStartVertexId > 0 &&
            (wall.StartVertexId == dragState.DragSelectedStartVertexId || wall.EndVertexId == dragState.DragSelectedStartVertexId);
        bool sharesEndVertex = dragState.DragSelectedEndVertexId > 0 &&
            (wall.StartVertexId == dragState.DragSelectedEndVertexId || wall.EndVertexId == dragState.DragSelectedEndVertexId);
        bool sharesByProximity =
            WallGeometryService.IsNearXZ(wall.Data.startPoint, dragState.DragSelectedStartPoint, connectedEndpointThreshold) ||
            WallGeometryService.IsNearXZ(wall.Data.startPoint, dragState.DragSelectedEndPoint, connectedEndpointThreshold) ||
            WallGeometryService.IsNearXZ(wall.Data.endPoint, dragState.DragSelectedStartPoint, connectedEndpointThreshold) ||
            WallGeometryService.IsNearXZ(wall.Data.endPoint, dragState.DragSelectedEndPoint, connectedEndpointThreshold);

        return sharesStartVertex || sharesEndVertex || sharesByProximity;
    }

    private static WallGeometryService.ConnectedWallMoveContext BuildMoveContext(
        WallSelectionDragState dragState,
        Vector3 translationDelta,
        float dragPlaneHeight,
        float connectedEndpointThreshold,
        float minimumWallLength)
    {
        Vector3 movedStartPoint = dragState.DragSelectedStartPoint + translationDelta;
        Vector3 movedEndPoint = dragState.DragSelectedEndPoint + translationDelta;
        movedStartPoint.y = dragPlaneHeight;
        movedEndPoint.y = dragPlaneHeight;

        return new WallGeometryService.ConnectedWallMoveContext
        {
            selectedStartPoint = dragState.DragSelectedStartPoint,
            selectedEndPoint = dragState.DragSelectedEndPoint,
            movedStartPoint = movedStartPoint,
            movedEndPoint = movedEndPoint,
            selectedStartVertexId = dragState.DragSelectedStartVertexId,
            selectedEndVertexId = dragState.DragSelectedEndVertexId,
            endpointThreshold = connectedEndpointThreshold,
            minimumWallLength = minimumWallLength,
        };
    }
}
