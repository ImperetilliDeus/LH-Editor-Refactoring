using System.Collections.Generic;
using UnityEngine;

public static class WallGeometryService
{
    private static readonly List<Wall> cachedWalls = new List<Wall>();

    public struct WallEndpointState
    {
        public Vector3 start;
        public Vector3 end;
    }

    public struct ConnectedWallMoveContext
    {
        public Vector3 selectedStartPoint;
        public Vector3 selectedEndPoint;
        public Vector3 movedStartPoint;
        public Vector3 movedEndPoint;
        public int selectedStartVertexId;
        public int selectedEndVertexId;
        public float endpointThreshold;
        public float minimumWallLength;
    }

    public static bool ApplyWallEndpoints(Wall wall, Vector3 startPoint, Vector3 endPoint, float minimumWallLength, WallLengthDisplay wallLengthDisplay, bool isPreview)
    {
        return wall != null && wall.TryApplyCurrentProfileAndRefresh(startPoint, endPoint, minimumWallLength, wallLengthDisplay, isPreview);
    }

    public static void SyncWallFromTransform(Transform wallTransform, float planeY)
    {
        if (wallTransform == null)
        {
            return;
        }

        Wall wall = wallTransform.GetComponent<Wall>();
        if (wall == null)
        {
            return;
        }

        wall.SyncEndpointsFromTransform(planeY);
    }

    public static void SyncWallsFromTransform(Transform wallRoot, float planeY)
    {
        if (wallRoot == null)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
            {
                continue;
            }

            SyncWallFromTransform(wall.transform, planeY);
        }
    }

    public static void ApplyVertexMove(IEnumerable<Wall> walls, int vertexId, Vector3 newPoint, float planeY, float minimumWallLength, WallLengthDisplay wallLengthDisplay)
    {
        if (walls == null)
        {
            return;
        }

        newPoint.y = planeY;
        foreach (Wall wall in walls)
        {
            if (wall == null)
            {
                continue;
            }

            Vector3 startPoint = wall.StartPoint;
            Vector3 endPoint = wall.EndPoint;

            if (wall.StartVertexId == vertexId)
            {
                startPoint = newPoint;
            }

            if (wall.EndVertexId == vertexId)
            {
                endPoint = newPoint;
            }

            ApplyWallEndpoints(wall, startPoint, endPoint, minimumWallLength, wallLengthDisplay, false);
        }
    }

    public static void ApplyConnectedWallMove(
        IEnumerable<Wall> walls,
        IReadOnlyDictionary<GameObject, WallEndpointState> endpointStates,
        ConnectedWallMoveContext moveContext,
        WallLengthDisplay wallLengthDisplay)
    {
        if (walls == null)
        {
            return;
        }

        foreach (Wall wall in walls)
        {
            if (wall == null)
            {
                continue;
            }

            WallEndpointState endpointState;
            if (endpointStates == null || !endpointStates.TryGetValue(wall.gameObject, out endpointState))
            {
                endpointState = new WallEndpointState
                {
                    start = wall.StartPoint,
                    end = wall.EndPoint,
                };
            }

            Vector3 newStart = ResolveDraggedEndpoint(wall.StartVertexId, endpointState.start, moveContext);
            Vector3 newEnd = ResolveDraggedEndpoint(wall.EndVertexId, endpointState.end, moveContext);
            ApplyWallEndpoints(wall, newStart, newEnd, moveContext.minimumWallLength, wallLengthDisplay, false);
        }
    }

    public static Vector3 ResolveDraggedEndpoint(int endpointVertexId, Vector3 endpointSource, ConnectedWallMoveContext moveContext)
    {
        if (moveContext.selectedStartVertexId > 0 && endpointVertexId == moveContext.selectedStartVertexId)
        {
            return moveContext.movedStartPoint;
        }

        if (moveContext.selectedEndVertexId > 0 && endpointVertexId == moveContext.selectedEndVertexId)
        {
            return moveContext.movedEndPoint;
        }

        if (IsNearXZ(endpointSource, moveContext.selectedStartPoint, moveContext.endpointThreshold))
        {
            return moveContext.movedStartPoint;
        }

        if (IsNearXZ(endpointSource, moveContext.selectedEndPoint, moveContext.endpointThreshold))
        {
            return moveContext.movedEndPoint;
        }

        return endpointSource;
    }

    public static bool IsNearXZ(Vector3 a, Vector3 b, float threshold)
    {
        float thresholdSqr = threshold * threshold;
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz <= thresholdSqr;
    }
}
