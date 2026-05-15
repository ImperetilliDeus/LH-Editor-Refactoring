using System.Collections.Generic;
using UnityEngine;

public static partial class RoomGraphUtility
{
    private const float BoundarySplitEpsilon = 0.0001f;
    private const float BoundaryVertexSnapThreshold = 0.12f;
    private const float PointKeyPrecisionScale = 1000f;

    private struct BoundaryEdge
    {
        public Vector3 start;
        public Vector3 end;
        public int startVertexId;
        public int endVertexId;
        public Wall wall;
        public VirtualBoundary virtualBoundary;
    }

    private struct RawBoundarySegment
    {
        public Vector3 start;
        public Vector3 end;
        public int startVertexId;
        public int endVertexId;
        public Wall wall;
        public VirtualBoundary virtualBoundary;
    }

    public static bool TryBuildOrderedVertices(HashSet<Wall> wallSet, float endpointMatchThreshold, out List<Vector3> vertices)
    {
        return TryBuildOrderedVertices(wallSet, endpointMatchThreshold, null, out vertices);
    }

    public static bool TryBuildOrderedVertices(
        HashSet<Wall> wallSet,
        float endpointMatchThreshold,
        IEnumerable<VirtualBoundary> virtualBoundaries,
        out List<Vector3> vertices)
    {
        List<BoundaryEdge> boundaryEdges = BuildBoundaryEdges(wallSet, virtualBoundaries);
        vertices = BuildVerticesFromBoundaryVertexIds(boundaryEdges);
        if (vertices != null && vertices.Count >= 3)
        {
            return true;
        }

        vertices = BuildVerticesByBoundaryEndpointDistance(boundaryEdges, endpointMatchThreshold);
        return vertices != null && vertices.Count >= 3;
    }

    public static Dictionary<int, List<Wall>> BuildVertexAdjacency(IEnumerable<Wall> walls)
    {
        Dictionary<int, List<Wall>> adjacency = new Dictionary<int, List<Wall>>();
        if (walls == null)
        {
            return adjacency;
        }

        foreach (Wall wall in walls)
        {
            if (wall == null || !wall.gameObject.activeInHierarchy || !wall.HasValidVertexIds)
            {
                continue;
            }

            AddWallToAdjacency(adjacency, wall.StartVertexId, wall);
            AddWallToAdjacency(adjacency, wall.EndVertexId, wall);
        }

        return adjacency;
    }

    public static List<Vector3> BuildVerticesFromVertexIds(HashSet<Wall> wallSet)
    {
        if (wallSet == null || wallSet.Count < 3)
        {
            return null;
        }

        Dictionary<int, List<Wall>> adjacency = BuildVertexAdjacency(wallSet);
        Dictionary<int, Vector3> pointByVertexId = new Dictionary<int, Vector3>();

        foreach (Wall wall in wallSet)
        {
            if (wall == null || !wall.HasValidVertexIds)
            {
                return null;
            }

            if (!pointByVertexId.ContainsKey(wall.StartVertexId))
            {
                pointByVertexId[wall.StartVertexId] = wall.Data.startPoint;
            }

            if (!pointByVertexId.ContainsKey(wall.EndVertexId))
            {
                pointByVertexId[wall.EndVertexId] = wall.Data.endPoint;
            }
        }

        foreach (KeyValuePair<int, List<Wall>> pair in adjacency)
        {
            if (pair.Value.Count != 2)
            {
                return null;
            }
        }

        Wall firstWall = null;
        foreach (Wall wall in wallSet)
        {
            if (wall != null)
            {
                firstWall = wall;
                break;
            }
        }

        if (firstWall == null)
        {
            return null;
        }

        List<Vector3> orderedVertices = new List<Vector3>();
        HashSet<Wall> visitedWalls = new HashSet<Wall>();
        int startVertexId = firstWall.StartVertexId;
        int currentVertexId = startVertexId;
        Wall currentWall = firstWall;

        while (currentWall != null && visitedWalls.Count < wallSet.Count)
        {
            visitedWalls.Add(currentWall);
            orderedVertices.Add(pointByVertexId[currentVertexId]);

            int nextVertexId = currentWall.GetOppositeVertexId(currentVertexId);
            if (nextVertexId <= 0)
            {
                return null;
            }

            Wall nextWall = null;
            List<Wall> connectedWalls = adjacency[nextVertexId];
            for (int i = 0; i < connectedWalls.Count; i++)
            {
                Wall candidate = connectedWalls[i];
                if (!visitedWalls.Contains(candidate))
                {
                    nextWall = candidate;
                    break;
                }
            }

            currentVertexId = nextVertexId;
            currentWall = nextWall;
        }

        if (visitedWalls.Count != wallSet.Count || currentVertexId != startVertexId)
        {
            return null;
        }

        return orderedVertices;
    }

    public static List<Vector3> BuildVerticesByEndpointDistance(HashSet<Wall> wallSet, float endpointMatchThreshold)
    {
        if (wallSet == null || wallSet.Count == 0)
        {
            return null;
        }

        float endpointThresholdSqr = endpointMatchThreshold * endpointMatchThreshold;
        List<Wall> orderedWalls = new List<Wall>();
        HashSet<Wall> visitedWalls = new HashSet<Wall>();
        Wall currentWall = null;
        foreach (Wall wall in wallSet)
        {
            if (wall != null)
            {
                currentWall = wall;
                break;
            }
        }

        if (currentWall == null)
        {
            return null;
        }

        orderedWalls.Add(currentWall);
        visitedWalls.Add(currentWall);
        Vector3 nextStartPoint = currentWall.Data.endPoint;

        while (orderedWalls.Count < wallSet.Count)
        {
            Wall nextWall = null;
            foreach (Wall wall in wallSet)
            {
                if (wall == null || visitedWalls.Contains(wall))
                {
                    continue;
                }

                if ((nextStartPoint - wall.Data.startPoint).sqrMagnitude < endpointThresholdSqr)
                {
                    nextWall = wall;
                    break;
                }
            }

            if (nextWall == null)
            {
                return null;
            }

            orderedWalls.Add(nextWall);
            visitedWalls.Add(nextWall);
            nextStartPoint = nextWall.Data.endPoint;
        }

        List<Vector3> vertices = new List<Vector3>(orderedWalls.Count);
        for (int i = 0; i < orderedWalls.Count; i++)
        {
            vertices.Add(orderedWalls[i].Data.startPoint);
        }

        return vertices;
    }

    private static List<BoundaryEdge> BuildBoundaryEdges(IEnumerable<Wall> walls, IEnumerable<VirtualBoundary> virtualBoundaries = null)
    {
        List<RawBoundarySegment> rawSegments = new List<RawBoundarySegment>();
        HashSet<WallOpeningContainer> processedContainers = new HashSet<WallOpeningContainer>();
        HashSet<Wall> processedWalls = new HashSet<Wall>();

        if (walls != null)
        {
            foreach (Wall wall in walls)
            {
                if (wall == null || !wall.gameObject.activeInHierarchy)
                {
                    continue;
                }

                WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
                if (container != null)
                {
                    if (!processedContainers.Add(container))
                    {
                        continue;
                    }

                    rawSegments.Add(new RawBoundarySegment
                    {
                        start = container.WallStart,
                        end = container.WallEnd,
                        startVertexId = container.OuterStartVertexId,
                        endVertexId = container.OuterEndVertexId,
                        wall = wall,
                    });
                    continue;
                }

                if (!processedWalls.Add(wall))
                {
                    continue;
                }

                rawSegments.Add(new RawBoundarySegment
                {
                    start = wall.Data.startPoint,
                    end = wall.Data.endPoint,
                    startVertexId = wall.StartVertexId,
                    endVertexId = wall.EndVertexId,
                    wall = wall,
                });
            }
        }

        if (virtualBoundaries != null)
        {
            foreach (VirtualBoundary virtualBoundary in virtualBoundaries)
            {
                if (virtualBoundary == null || !virtualBoundary.isActiveAndEnabled || !virtualBoundary.IncludeInRoomGraph)
                {
                    continue;
                }

                if (!virtualBoundary.TryGetResolvedEndpoints(out Vector3 startPoint, out Vector3 endPoint))
                {
                    continue;
                }

                rawSegments.Add(new RawBoundarySegment
                {
                    start = startPoint,
                    end = endPoint,
                    startVertexId = virtualBoundary.StartVertexId,
                    endVertexId = virtualBoundary.EndVertexId,
                    virtualBoundary = virtualBoundary,
                });
            }
        }

        return SplitBoundarySegments(rawSegments);
    }

    private static List<BoundaryEdge> BuildBoundaryEdges(List<Vector3> outerPolygon, IEnumerable<VirtualBoundary> virtualBoundaries)
    {
        List<RawBoundarySegment> rawSegments = new List<RawBoundarySegment>();
        if (outerPolygon != null && outerPolygon.Count >= 3)
        {
            for (int i = 0; i < outerPolygon.Count; i++)
            {
                Vector3 start = outerPolygon[i];
                Vector3 end = outerPolygon[(i + 1) % outerPolygon.Count];
                if ((end - start).sqrMagnitude <= BoundarySplitEpsilon * BoundarySplitEpsilon)
                {
                    continue;
                }

                rawSegments.Add(new RawBoundarySegment
                {
                    start = start,
                    end = end,
                    startVertexId = i + 1,
                    endVertexId = ((i + 1) % outerPolygon.Count) + 1,
                });
            }
        }

        if (virtualBoundaries != null)
        {
            foreach (VirtualBoundary virtualBoundary in virtualBoundaries)
            {
                if (virtualBoundary == null || !virtualBoundary.isActiveAndEnabled || !virtualBoundary.IncludeInRoomGraph)
                {
                    continue;
                }

                if (!virtualBoundary.TryGetResolvedEndpoints(out Vector3 startPoint, out Vector3 endPoint))
                {
                    continue;
                }

                rawSegments.Add(new RawBoundarySegment
                {
                    start = startPoint,
                    end = endPoint,
                    startVertexId = virtualBoundary.StartVertexId,
                    endVertexId = virtualBoundary.EndVertexId,
                    virtualBoundary = virtualBoundary,
                });
            }
        }

        return SplitBoundarySegments(rawSegments);
    }

    private static List<BoundaryEdge> SplitBoundarySegments(List<RawBoundarySegment> rawSegments)
    {
        List<BoundaryEdge> results = new List<BoundaryEdge>();
        if (rawSegments == null || rawSegments.Count == 0)
        {
            return results;
        }

        List<List<SegmentSplitPoint>> splitPointsBySegment = new List<List<SegmentSplitPoint>>(rawSegments.Count);
        List<GeneratedBoundaryVertex> generatedVertices = new List<GeneratedBoundaryVertex>();
        int nextGeneratedVertexId = 1;

        for (int i = 0; i < rawSegments.Count; i++)
        {
            nextGeneratedVertexId = Mathf.Max(nextGeneratedVertexId, rawSegments[i].startVertexId + 1);
            nextGeneratedVertexId = Mathf.Max(nextGeneratedVertexId, rawSegments[i].endVertexId + 1);
            SeedExplicitBoundaryVertex(rawSegments[i].start, rawSegments[i].startVertexId, generatedVertices);
            SeedExplicitBoundaryVertex(rawSegments[i].end, rawSegments[i].endVertexId, generatedVertices);
        }

        for (int i = 0; i < rawSegments.Count; i++)
        {
            splitPointsBySegment.Add(new List<SegmentSplitPoint>());
            RawBoundarySegment segment = rawSegments[i];

            int startVertexId = segment.startVertexId > 0
                ? segment.startVertexId
                : GetOrCreateGeneratedVertexId(segment.start, generatedVertices, ref nextGeneratedVertexId);
            int endVertexId = segment.endVertexId > 0
                ? segment.endVertexId
                : GetOrCreateGeneratedVertexId(segment.end, generatedVertices, ref nextGeneratedVertexId);

            AddSplitPoint(splitPointsBySegment[i], 0f, segment.start, startVertexId);
            AddSplitPoint(splitPointsBySegment[i], 1f, segment.end, endVertexId);
        }

        for (int i = 0; i < rawSegments.Count; i++)
        {
            for (int j = i + 1; j < rawSegments.Count; j++)
            {
                if (TryGetSegmentIntersectionXZ(rawSegments[i], rawSegments[j], out float tA, out float tB, out Vector3 intersection))
                {
                    int sharedVertexId = GetOrCreateGeneratedVertexId(intersection, generatedVertices, ref nextGeneratedVertexId);
                    AddSplitPoint(splitPointsBySegment[i], tA, intersection, sharedVertexId);
                    AddSplitPoint(splitPointsBySegment[j], tB, intersection, sharedVertexId);
                    continue;
                }

                if (!TryAddCollinearOverlapSplitPoints(
                        rawSegments[i],
                        rawSegments[j],
                        splitPointsBySegment[i],
                        splitPointsBySegment[j],
                        generatedVertices,
                        ref nextGeneratedVertexId))
                {
                    continue;
                }
            }
        }

        Dictionary<string, int> resultIndexByEdgeKey = new Dictionary<string, int>();
        for (int i = 0; i < rawSegments.Count; i++)
        {
            RawBoundarySegment segment = rawSegments[i];
            List<SegmentSplitPoint> splitPoints = splitPointsBySegment[i];
            splitPoints.Sort(static (left, right) => left.t.CompareTo(right.t));

            for (int j = 0; j < splitPoints.Count - 1; j++)
            {
                SegmentSplitPoint start = splitPoints[j];
                SegmentSplitPoint end = splitPoints[j + 1];
                if ((end.point - start.point).sqrMagnitude <= BoundarySplitEpsilon * BoundarySplitEpsilon)
                {
                    continue;
                }

                BoundaryEdge edge = new BoundaryEdge
                {
                    start = start.point,
                    end = end.point,
                    startVertexId = start.vertexId,
                    endVertexId = end.vertexId,
                    wall = segment.wall,
                    virtualBoundary = segment.virtualBoundary,
                };

                string edgeKey = BuildUndirectedEdgeKey(edge.start, edge.end);
                if (resultIndexByEdgeKey.TryGetValue(edgeKey, out int existingIndex))
                {
                    BoundaryEdge existing = results[existingIndex];
                    if (existing.wall == null && edge.wall != null)
                    {
                        existing.wall = edge.wall;
                    }

                    if (existing.virtualBoundary == null && edge.virtualBoundary != null)
                    {
                        existing.virtualBoundary = edge.virtualBoundary;
                    }

                    results[existingIndex] = existing;
                    continue;
                }

                resultIndexByEdgeKey[edgeKey] = results.Count;
                results.Add(edge);
            }
        }

        return results;
    }

    private struct SegmentSplitPoint
    {
        public float t;
        public Vector3 point;
        public int vertexId;
    }

    private struct GeneratedBoundaryVertex
    {
        public Vector3 point;
        public int vertexId;
    }

    private static void AddSplitPoint(List<SegmentSplitPoint> points, float t, Vector3 point, int vertexId)
    {
        if (points == null)
        {
            return;
        }

        for (int i = 0; i < points.Count; i++)
        {
            if (Mathf.Abs(points[i].t - t) > BoundarySplitEpsilon)
            {
                continue;
            }

            if ((points[i].point - point).sqrMagnitude > BoundarySplitEpsilon * BoundarySplitEpsilon)
            {
                continue;
            }

            if (points[i].vertexId <= 0 && vertexId > 0)
            {
                SegmentSplitPoint updated = points[i];
                updated.vertexId = vertexId;
                points[i] = updated;
            }

            return;
        }

        points.Add(new SegmentSplitPoint
        {
            t = Mathf.Clamp01(t),
            point = point,
            vertexId = vertexId,
        });
    }

    private static int GetOrCreateGeneratedVertexId(Vector3 point, List<GeneratedBoundaryVertex> generatedVertices, ref int nextGeneratedVertexId)
    {
        float thresholdSqr = BoundaryVertexSnapThreshold * BoundaryVertexSnapThreshold;
        for (int i = 0; i < generatedVertices.Count; i++)
        {
            if ((generatedVertices[i].point - point).sqrMagnitude <= thresholdSqr)
            {
                return generatedVertices[i].vertexId;
            }
        }

        int created = nextGeneratedVertexId++;
        generatedVertices.Add(new GeneratedBoundaryVertex
        {
            point = point,
            vertexId = created,
        });
        return created;
    }

    private static void SeedExplicitBoundaryVertex(
        Vector3 point,
        int vertexId,
        List<GeneratedBoundaryVertex> generatedVertices)
    {
        if (vertexId <= 0 || generatedVertices == null)
        {
            return;
        }

        float thresholdSqr = BoundaryVertexSnapThreshold * BoundaryVertexSnapThreshold;
        for (int i = 0; i < generatedVertices.Count; i++)
        {
            if ((generatedVertices[i].point - point).sqrMagnitude <= thresholdSqr)
            {
                return;
            }
        }

        generatedVertices.Add(new GeneratedBoundaryVertex
        {
            point = point,
            vertexId = vertexId,
        });
    }

    private static string BuildPointKey(Vector3 point)
    {
        int x = Mathf.RoundToInt(point.x * PointKeyPrecisionScale);
        int y = Mathf.RoundToInt(point.y * PointKeyPrecisionScale);
        int z = Mathf.RoundToInt(point.z * PointKeyPrecisionScale);
        return $"{x}:{y}:{z}";
    }

    private static string BuildUndirectedEdgeKey(Vector3 start, Vector3 end)
    {
        string startKey = BuildPointKey(start);
        string endKey = BuildPointKey(end);
        return string.CompareOrdinal(startKey, endKey) <= 0
            ? $"{startKey}|{endKey}"
            : $"{endKey}|{startKey}";
    }

    private static bool TryAddCollinearOverlapSplitPoints(
        RawBoundarySegment first,
        RawBoundarySegment second,
        List<SegmentSplitPoint> firstSplitPoints,
        List<SegmentSplitPoint> secondSplitPoints,
        List<GeneratedBoundaryVertex> generatedVertices,
        ref int nextGeneratedVertexId)
    {
        Vector2 firstStart = new Vector2(first.start.x, first.start.z);
        Vector2 firstEnd = new Vector2(first.end.x, first.end.z);
        Vector2 secondStart = new Vector2(second.start.x, second.start.z);
        Vector2 secondEnd = new Vector2(second.end.x, second.end.z);

        Vector2 firstDirection = firstEnd - firstStart;
        Vector2 secondDirection = secondEnd - secondStart;
        if (firstDirection.sqrMagnitude <= BoundarySplitEpsilon * BoundarySplitEpsilon ||
            secondDirection.sqrMagnitude <= BoundarySplitEpsilon * BoundarySplitEpsilon)
        {
            return false;
        }

        float crossDirections = Cross(firstDirection, secondDirection);
        if (Mathf.Abs(crossDirections) > BoundarySplitEpsilon)
        {
            return false;
        }

        if (Mathf.Abs(Cross(secondStart - firstStart, firstDirection)) > BoundaryVertexSnapThreshold ||
            Mathf.Abs(Cross(secondEnd - firstStart, firstDirection)) > BoundaryVertexSnapThreshold)
        {
            return false;
        }

        bool addedAny = false;
        addedAny |= TryAddPointOnSegment(first, second.start, firstSplitPoints, generatedVertices, ref nextGeneratedVertexId);
        addedAny |= TryAddPointOnSegment(first, second.end, firstSplitPoints, generatedVertices, ref nextGeneratedVertexId);
        addedAny |= TryAddPointOnSegment(second, first.start, secondSplitPoints, generatedVertices, ref nextGeneratedVertexId);
        addedAny |= TryAddPointOnSegment(second, first.end, secondSplitPoints, generatedVertices, ref nextGeneratedVertexId);
        return addedAny;
    }

    private static bool TryAddPointOnSegment(
        RawBoundarySegment segment,
        Vector3 point,
        List<SegmentSplitPoint> splitPoints,
        List<GeneratedBoundaryVertex> generatedVertices,
        ref int nextGeneratedVertexId)
    {
        if (!TryGetPointParameterOnSegment(segment, point, out float t))
        {
            return false;
        }

        int vertexId = GetOrCreateGeneratedVertexId(point, generatedVertices, ref nextGeneratedVertexId);
        AddSplitPoint(splitPoints, t, point, vertexId);
        return true;
    }

    private static bool TryGetPointParameterOnSegment(RawBoundarySegment segment, Vector3 point, out float t)
    {
        t = 0f;
        Vector2 start = new Vector2(segment.start.x, segment.start.z);
        Vector2 end = new Vector2(segment.end.x, segment.end.z);
        Vector2 candidate = new Vector2(point.x, point.z);
        Vector2 direction = end - start;
        float lengthSqr = direction.sqrMagnitude;
        if (lengthSqr <= BoundarySplitEpsilon * BoundarySplitEpsilon)
        {
            return false;
        }

        Vector2 toPoint = candidate - start;
        if (Mathf.Abs(Cross(toPoint, direction)) > BoundaryVertexSnapThreshold)
        {
            return false;
        }

        t = Vector2.Dot(toPoint, direction) / lengthSqr;
        if (t < -BoundarySplitEpsilon || t > 1f + BoundarySplitEpsilon)
        {
            return false;
        }

        t = Mathf.Clamp01(t);
        return true;
    }

    private static bool TryGetSegmentIntersectionXZ(
        RawBoundarySegment a,
        RawBoundarySegment b,
        out float tA,
        out float tB,
        out Vector3 intersection)
    {
        tA = 0f;
        tB = 0f;
        intersection = Vector3.zero;

        Vector2 p = new Vector2(a.start.x, a.start.z);
        Vector2 r = new Vector2(a.end.x - a.start.x, a.end.z - a.start.z);
        Vector2 q = new Vector2(b.start.x, b.start.z);
        Vector2 s = new Vector2(b.end.x - b.start.x, b.end.z - b.start.z);

        float rxs = Cross(r, s);
        Vector2 qp = q - p;

        if (Mathf.Abs(rxs) <= BoundarySplitEpsilon)
        {
            return false;
        }

        tA = Cross(qp, s) / rxs;
        tB = Cross(qp, r) / rxs;

        if (tA < -BoundarySplitEpsilon || tA > 1f + BoundarySplitEpsilon ||
            tB < -BoundarySplitEpsilon || tB > 1f + BoundarySplitEpsilon)
        {
            return false;
        }

        tA = Mathf.Clamp01(tA);
        tB = Mathf.Clamp01(tB);
        Vector2 intersection2D = p + r * tA;
        intersection = new Vector3(intersection2D.x, a.start.y, intersection2D.y);
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static List<Vector3> BuildVerticesFromBoundaryVertexIds(List<BoundaryEdge> edges)
    {
        if (edges == null || edges.Count < 3)
        {
            return null;
        }

        Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        Dictionary<int, Vector3> pointByVertexId = new Dictionary<int, Vector3>();

        for (int i = 0; i < edges.Count; i++)
        {
            BoundaryEdge edge = edges[i];
            if (edge.startVertexId <= 0 || edge.endVertexId <= 0)
            {
                return null;
            }

            AddEdgeToAdjacency(adjacency, edge.startVertexId, i);
            AddEdgeToAdjacency(adjacency, edge.endVertexId, i);
            pointByVertexId[edge.startVertexId] = edge.start;
            pointByVertexId[edge.endVertexId] = edge.end;
        }

        foreach (KeyValuePair<int, List<int>> pair in adjacency)
        {
            if (pair.Value.Count != 2)
            {
                return null;
            }
        }

        BoundaryEdge firstEdge = edges[0];
        List<Vector3> orderedVertices = new List<Vector3>();
        HashSet<int> visitedEdges = new HashSet<int>();
        int currentVertexId = firstEdge.startVertexId;
        int edgeIndex = 0;

        while (edgeIndex >= 0 && visitedEdges.Count < edges.Count)
        {
            visitedEdges.Add(edgeIndex);
            BoundaryEdge currentEdge = edges[edgeIndex];
            orderedVertices.Add(pointByVertexId[currentVertexId]);

            int nextVertexId = currentEdge.startVertexId == currentVertexId ? currentEdge.endVertexId : currentEdge.startVertexId;
            if (nextVertexId <= 0)
            {
                return null;
            }

            int nextEdgeIndex = -1;
            List<int> connectedEdges = adjacency[nextVertexId];
            for (int i = 0; i < connectedEdges.Count; i++)
            {
                int candidateIndex = connectedEdges[i];
                if (!visitedEdges.Contains(candidateIndex))
                {
                    nextEdgeIndex = candidateIndex;
                    break;
                }
            }

            currentVertexId = nextVertexId;
            edgeIndex = nextEdgeIndex;
        }

        if (visitedEdges.Count != edges.Count || currentVertexId != firstEdge.startVertexId)
        {
            return null;
        }

        return orderedVertices;
    }

    private static List<Vector3> BuildVerticesByBoundaryEndpointDistance(List<BoundaryEdge> edges, float endpointMatchThreshold)
    {
        if (edges == null || edges.Count < 3)
        {
            return null;
        }

        float endpointThresholdSqr = endpointMatchThreshold * endpointMatchThreshold;
        List<Vector3> vertices = new List<Vector3>(edges.Count);
        HashSet<int> visitedIndices = new HashSet<int>();
        BoundaryEdge currentEdge = edges[0];
        vertices.Add(currentEdge.start);
        visitedIndices.Add(0);
        Vector3 currentPoint = currentEdge.end;

        while (visitedIndices.Count < edges.Count)
        {
            int nextIndex = -1;
            bool useStartPoint = true;
            for (int i = 0; i < edges.Count; i++)
            {
                if (visitedIndices.Contains(i))
                {
                    continue;
                }

                if ((currentPoint - edges[i].start).sqrMagnitude < endpointThresholdSqr)
                {
                    nextIndex = i;
                    useStartPoint = true;
                    break;
                }

                if ((currentPoint - edges[i].end).sqrMagnitude < endpointThresholdSqr)
                {
                    nextIndex = i;
                    useStartPoint = false;
                    break;
                }
            }

            if (nextIndex < 0)
            {
                return null;
            }

            visitedIndices.Add(nextIndex);
            BoundaryEdge nextEdge = edges[nextIndex];
            if (useStartPoint)
            {
                vertices.Add(nextEdge.start);
                currentPoint = nextEdge.end;
            }
            else
            {
                vertices.Add(nextEdge.end);
                currentPoint = nextEdge.start;
            }
        }

        return vertices;
    }

    private static void AddEdgeToAdjacency(Dictionary<int, List<int>> adjacency, int vertexId, int edgeIndex)
    {
        if (!adjacency.TryGetValue(vertexId, out List<int> list))
        {
            list = new List<int>();
            adjacency[vertexId] = list;
        }

        list.Add(edgeIndex);
    }
}
