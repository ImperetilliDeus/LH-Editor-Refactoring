using System.Collections.Generic;
using UnityEngine;

public static partial class RoomGraphUtility
{
    private const int MaxCycleSearchDepth = 1024;
    private const int MaxRecordedCycleCount = 4096;
    private const float MinFaceAreaEpsilon = 0.0001f;

    public struct BoundaryCycleResult
    {
        public List<Vector3> vertices;
        public HashSet<Wall> walls;
        public float area;
        public Vector3 centroid;
    }

    public struct BoundaryFaceResult
    {
        public List<Vector3> vertices;
        public HashSet<Wall> walls;
        public HashSet<VirtualBoundary> virtualBoundaries;
        public float area;
        public Vector3 centroid;
    }

    private struct DirectedBoundaryEdge
    {
        public int startVertexId;
        public int endVertexId;
        public int boundaryEdgeIndex;
        public int oppositeDirectedEdgeIndex;
        public float angle;
    }

    public static List<Vector3> BuildLargestCycleVerticesFromBoundaryGraph(HashSet<Wall> wallSet, IEnumerable<VirtualBoundary> virtualBoundaries)
    {
        List<BoundaryCycleResult> cycles = BuildBoundaryCycles(wallSet, virtualBoundaries);
        if (cycles == null || cycles.Count == 0)
        {
            return null;
        }

        List<Vector3> bestVertices = null;
        float bestArea = 0f;
        for (int i = 0; i < cycles.Count; i++)
        {
            BoundaryCycleResult cycle = cycles[i];
            if (cycle.vertices == null || cycle.vertices.Count < 3)
            {
                continue;
            }

            float area = Mathf.Abs(cycle.area);
            if (area <= bestArea)
            {
                continue;
            }

            bestArea = area;
            bestVertices = cycle.vertices;
        }

        return bestVertices != null ? new List<Vector3>(bestVertices) : null;
    }

    public static bool TryFindContainingCycle(
        HashSet<Wall> wallSet,
        Vector3 point,
        out BoundaryCycleResult result)
    {
        return TryFindContainingCycle(wallSet, null, point, out result);
    }

    public static bool TryFindContainingCycle(
        HashSet<Wall> wallSet,
        IEnumerable<VirtualBoundary> virtualBoundaries,
        Vector3 point,
        out BoundaryCycleResult result)
    {
        result = default;
        List<BoundaryCycleResult> cycles = BuildBoundaryCycles(wallSet, virtualBoundaries);
        if (cycles == null || cycles.Count == 0)
        {
            return false;
        }
        float bestArea = float.MaxValue;

        for (int i = 0; i < cycles.Count; i++)
        {
            BoundaryCycleResult cycle = cycles[i];
            if (cycle.vertices == null || cycle.vertices.Count < 3 || !IsPointInsidePolygonXZ(point, cycle.vertices))
            {
                continue;
            }

            float area = Mathf.Abs(cycle.area);
            if (area >= bestArea)
            {
                continue;
            }

            bestArea = area;
            result = cycle;
        }

        return result.vertices != null && result.vertices.Count >= 3;
    }

    public static List<BoundaryCycleResult> BuildBoundaryCycles(HashSet<Wall> wallSet)
    {
        return BuildBoundaryCycles(wallSet, null);
    }

    public static List<BoundaryCycleResult> BuildBoundaryCycles(HashSet<Wall> wallSet, IEnumerable<VirtualBoundary> virtualBoundaries)
    {
        List<BoundaryCycleResult> results = new List<BoundaryCycleResult>();
        List<BoundaryEdge> edges = BuildBoundaryEdges(wallSet, virtualBoundaries);
        if (edges == null || edges.Count < 3)
        {
            return results;
        }

        Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        Dictionary<int, Vector3> pointByVertexId = new Dictionary<int, Vector3>();
        for (int i = 0; i < edges.Count; i++)
        {
            BoundaryEdge edge = edges[i];
            if (edge.startVertexId <= 0 || edge.endVertexId <= 0)
            {
                return results;
            }

            AddEdgeToAdjacency(adjacency, edge.startVertexId, i);
            AddEdgeToAdjacency(adjacency, edge.endVertexId, i);
            pointByVertexId[edge.startVertexId] = edge.start;
            pointByVertexId[edge.endVertexId] = edge.end;
        }

        List<BoundaryCycleCandidate> cycles = new List<BoundaryCycleCandidate>();
        HashSet<string> cycleKeys = new HashSet<string>();
        List<int> vertexIds = new List<int>(adjacency.Keys);
        vertexIds.Sort();

        for (int i = 0; i < vertexIds.Count; i++)
        {
            int startVertexId = vertexIds[i];
            List<int> pathVertices = new List<int> { startVertexId };
            HashSet<int> pathVertexSet = new HashSet<int> { startVertexId };
            HashSet<int> usedEdgeIndices = new HashSet<int>();
            List<int> pathEdgeIndices = new List<int>();
            bool truncatedByLimit = false;

            FindBoundaryCyclesDepthFirst(
                startVertexId,
                startVertexId,
                adjacency,
                edges,
                usedEdgeIndices,
                pathVertices,
                pathVertexSet,
                pathEdgeIndices,
                cycleKeys,
                cycles,
                0,
                ref truncatedByLimit);
        }

        for (int i = 0; i < cycles.Count; i++)
        {
            BoundaryCycleCandidate cycle = cycles[i];
            List<Vector3> vertices = new List<Vector3>(cycle.vertexIds.Count);
            bool valid = true;
            for (int j = 0; j < cycle.vertexIds.Count; j++)
            {
                if (!pointByVertexId.TryGetValue(cycle.vertexIds[j], out Vector3 vertex))
                {
                    valid = false;
                    break;
                }

                vertices.Add(vertex);
            }

            if (!valid || vertices.Count < 3)
            {
                continue;
            }

            HashSet<Wall> cycleWalls = new HashSet<Wall>();
            for (int j = 0; j < cycle.edgeIndices.Count; j++)
            {
                BoundaryEdge edge = edges[cycle.edgeIndices[j]];
                if (edge.wall != null)
                {
                    cycleWalls.Add(edge.wall);
                }
            }

            float area = CalculateSignedAreaXZ(vertices);
            results.Add(new BoundaryCycleResult
            {
                vertices = vertices,
                walls = cycleWalls,
                area = area,
                centroid = CalculatePolygonCentroidXZ(vertices),
            });
        }

        return results;
    }

    public static List<BoundaryFaceResult> BuildBoundaryFaces(HashSet<Wall> wallSet, IEnumerable<VirtualBoundary> virtualBoundaries)
    {
        List<BoundaryEdge> edges = BuildBoundaryEdges(wallSet, virtualBoundaries);
        return BuildBoundaryFacesFromEdges(edges);
    }

    public static List<BoundaryFaceResult> BuildBoundaryFaces(List<Vector3> outerPolygon, IEnumerable<VirtualBoundary> virtualBoundaries)
    {
        List<BoundaryEdge> edges = BuildBoundaryEdges(outerPolygon, virtualBoundaries);
        return BuildBoundaryFacesFromEdges(edges);
    }

    private static List<BoundaryFaceResult> BuildBoundaryFacesFromEdges(List<BoundaryEdge> edges)
    {
        List<BoundaryFaceResult> results = new List<BoundaryFaceResult>();
        if (edges == null || edges.Count < 3)
        {
            return results;
        }

        Dictionary<int, Vector3> pointByVertexId = new Dictionary<int, Vector3>();
        Dictionary<int, List<int>> outgoingDirectedEdgesByVertex = new Dictionary<int, List<int>>();
        List<DirectedBoundaryEdge> directedEdges = new List<DirectedBoundaryEdge>(edges.Count * 2);

        for (int i = 0; i < edges.Count; i++)
        {
            BoundaryEdge edge = edges[i];
            if (edge.startVertexId <= 0 || edge.endVertexId <= 0)
            {
                return results;
            }

            pointByVertexId[edge.startVertexId] = edge.start;
            pointByVertexId[edge.endVertexId] = edge.end;

            int forwardIndex = directedEdges.Count;
            int reverseIndex = directedEdges.Count + 1;

            DirectedBoundaryEdge forward = new DirectedBoundaryEdge
            {
                startVertexId = edge.startVertexId,
                endVertexId = edge.endVertexId,
                boundaryEdgeIndex = i,
                oppositeDirectedEdgeIndex = reverseIndex,
                angle = Mathf.Atan2(edge.end.z - edge.start.z, edge.end.x - edge.start.x),
            };

            DirectedBoundaryEdge reverse = new DirectedBoundaryEdge
            {
                startVertexId = edge.endVertexId,
                endVertexId = edge.startVertexId,
                boundaryEdgeIndex = i,
                oppositeDirectedEdgeIndex = forwardIndex,
                angle = Mathf.Atan2(edge.start.z - edge.end.z, edge.start.x - edge.end.x),
            };

            directedEdges.Add(forward);
            directedEdges.Add(reverse);
            AddDirectedEdgeToVertexMap(outgoingDirectedEdgesByVertex, forward.startVertexId, forwardIndex);
            AddDirectedEdgeToVertexMap(outgoingDirectedEdgesByVertex, reverse.startVertexId, reverseIndex);
        }

        foreach (KeyValuePair<int, List<int>> pair in outgoingDirectedEdgesByVertex)
        {
            pair.Value.Sort((left, right) => directedEdges[left].angle.CompareTo(directedEdges[right].angle));
        }

        bool[] consumedDirectedEdges = new bool[directedEdges.Count];
        HashSet<string> faceKeys = new HashSet<string>();

        for (int i = 0; i < directedEdges.Count; i++)
        {
            if (consumedDirectedEdges[i])
            {
                continue;
            }

            if (!TryTraceFace(
                    i,
                    directedEdges,
                    edges,
                    outgoingDirectedEdgesByVertex,
                    pointByVertexId,
                    out BoundaryFaceResult face,
                    out List<int> traversedDirectedEdges))
            {
                continue;
            }

            for (int j = 0; j < traversedDirectedEdges.Count; j++)
            {
                consumedDirectedEdges[traversedDirectedEdges[j]] = true;
            }

            if (face.vertices == null || face.vertices.Count < 3 || Mathf.Abs(face.area) <= MinFaceAreaEpsilon)
            {
                continue;
            }

            string key = BuildPolygonKey(face.vertices);
            if (!faceKeys.Add(key))
            {
                continue;
            }

            results.Add(face);
        }

        results.Sort((left, right) => Mathf.Abs(left.area).CompareTo(Mathf.Abs(right.area)));
        return results;
    }

    private static void AddWallToAdjacency(Dictionary<int, List<Wall>> adjacency, int vertexId, Wall wall)
    {
        if (!adjacency.TryGetValue(vertexId, out List<Wall> list))
        {
            list = new List<Wall>();
            adjacency[vertexId] = list;
        }

        if (!list.Contains(wall))
        {
            list.Add(wall);
        }
    }

    private static void AddDirectedEdgeToVertexMap(Dictionary<int, List<int>> outgoingDirectedEdgesByVertex, int vertexId, int directedEdgeIndex)
    {
        if (!outgoingDirectedEdgesByVertex.TryGetValue(vertexId, out List<int> outgoing))
        {
            outgoing = new List<int>();
            outgoingDirectedEdgesByVertex[vertexId] = outgoing;
        }

        outgoing.Add(directedEdgeIndex);
    }

    private static bool TryTraceFace(
        int startDirectedEdgeIndex,
        List<DirectedBoundaryEdge> directedEdges,
        List<BoundaryEdge> boundaryEdges,
        Dictionary<int, List<int>> outgoingDirectedEdgesByVertex,
        Dictionary<int, Vector3> pointByVertexId,
        out BoundaryFaceResult face,
        out List<int> traversedDirectedEdges)
    {
        face = default;
        traversedDirectedEdges = new List<int>();
        HashSet<int> seenDirectedEdges = new HashSet<int>();
        List<int> faceVertexIds = new List<int>();

        int currentDirectedEdgeIndex = startDirectedEdgeIndex;
        int safetyLimit = directedEdges.Count + 1;

        while (safetyLimit-- > 0)
        {
            if (!seenDirectedEdges.Add(currentDirectedEdgeIndex))
            {
                return false;
            }

            DirectedBoundaryEdge currentDirectedEdge = directedEdges[currentDirectedEdgeIndex];
            faceVertexIds.Add(currentDirectedEdge.startVertexId);
            traversedDirectedEdges.Add(currentDirectedEdgeIndex);

            if (!outgoingDirectedEdgesByVertex.TryGetValue(currentDirectedEdge.endVertexId, out List<int> outgoingDirectedEdges) ||
                outgoingDirectedEdges.Count == 0)
            {
                return false;
            }

            int oppositeIndex = currentDirectedEdge.oppositeDirectedEdgeIndex;
            int oppositePosition = outgoingDirectedEdges.IndexOf(oppositeIndex);
            if (oppositePosition < 0)
            {
                return false;
            }

            int nextPosition = oppositePosition - 1;
            if (nextPosition < 0)
            {
                nextPosition = outgoingDirectedEdges.Count - 1;
            }

            currentDirectedEdgeIndex = outgoingDirectedEdges[nextPosition];
            if (currentDirectedEdgeIndex == startDirectedEdgeIndex)
            {
                break;
            }
        }

        if (traversedDirectedEdges.Count < 3 || safetyLimit <= 0)
        {
            return false;
        }

        List<Vector3> vertices = new List<Vector3>(faceVertexIds.Count);
        for (int i = 0; i < faceVertexIds.Count; i++)
        {
            if (!pointByVertexId.TryGetValue(faceVertexIds[i], out Vector3 vertex))
            {
                return false;
            }

            vertices.Add(vertex);
        }

        RemoveSequentialDuplicateVertices(vertices);
        RemoveCollinearVertices(vertices);
        if (vertices.Count < 3)
        {
            return false;
        }

        float area = CalculateSignedAreaXZ(vertices);
        if (area < 0f)
        {
            vertices.Reverse();
            area = -area;
        }

        HashSet<Wall> faceWalls = new HashSet<Wall>();
        HashSet<VirtualBoundary> faceVirtualBoundaries = new HashSet<VirtualBoundary>();

        for (int i = 0; i < traversedDirectedEdges.Count; i++)
        {
            BoundaryEdge boundaryEdge = boundaryEdges[directedEdges[traversedDirectedEdges[i]].boundaryEdgeIndex];
            if (boundaryEdge.wall != null)
            {
                faceWalls.Add(boundaryEdge.wall);
            }

            if (boundaryEdge.virtualBoundary != null)
            {
                faceVirtualBoundaries.Add(boundaryEdge.virtualBoundary);
            }
        }

        face = new BoundaryFaceResult
        {
            vertices = vertices,
            walls = faceWalls,
            virtualBoundaries = faceVirtualBoundaries,
            area = area,
            centroid = CalculatePolygonCentroidXZ(vertices),
        };
        return true;
    }

    private struct BoundaryCycleCandidate
    {
        public List<int> vertexIds;
        public List<int> edgeIndices;
    }

    private static void FindBoundaryCyclesDepthFirst(
        int startVertexId,
        int currentVertexId,
        Dictionary<int, List<int>> adjacency,
        List<BoundaryEdge> edges,
        HashSet<int> usedEdgeIndices,
        List<int> pathVertices,
        HashSet<int> pathVertexSet,
        List<int> pathEdgeIndices,
        HashSet<string> cycleKeys,
        List<BoundaryCycleCandidate> cycles,
        int depth,
        ref bool truncatedByLimit)
    {
        if (depth >= MaxCycleSearchDepth || cycles.Count >= MaxRecordedCycleCount)
        {
            truncatedByLimit = true;
            return;
        }

        if (!adjacency.TryGetValue(currentVertexId, out List<int> connectedEdgeIndices))
        {
            return;
        }

        for (int i = 0; i < connectedEdgeIndices.Count; i++)
        {
            int edgeIndex = connectedEdgeIndices[i];
            if (!usedEdgeIndices.Add(edgeIndex))
            {
                continue;
            }

            BoundaryEdge edge = edges[edgeIndex];
            int nextVertexId = edge.startVertexId == currentVertexId ? edge.endVertexId : edge.startVertexId;
            if (nextVertexId <= 0)
            {
                usedEdgeIndices.Remove(edgeIndex);
                continue;
            }

            pathEdgeIndices.Add(edgeIndex);

            if (nextVertexId == startVertexId)
            {
                if (pathVertices.Count >= 3)
                {
                    List<int> cycleVertexIds = new List<int>(pathVertices);
                    string key = BuildCycleKey(cycleVertexIds);
                    if (cycleKeys.Add(key))
                    {
                        cycles.Add(new BoundaryCycleCandidate
                        {
                            vertexIds = cycleVertexIds,
                            edgeIndices = new List<int>(pathEdgeIndices),
                        });
                    }
                }

                pathEdgeIndices.RemoveAt(pathEdgeIndices.Count - 1);
                usedEdgeIndices.Remove(edgeIndex);
                continue;
            }

            if (!pathVertexSet.Add(nextVertexId))
            {
                pathEdgeIndices.RemoveAt(pathEdgeIndices.Count - 1);
                usedEdgeIndices.Remove(edgeIndex);
                continue;
            }

            pathVertices.Add(nextVertexId);
            FindBoundaryCyclesDepthFirst(
                startVertexId,
                nextVertexId,
                adjacency,
                edges,
                usedEdgeIndices,
                pathVertices,
                pathVertexSet,
                pathEdgeIndices,
                cycleKeys,
                cycles,
                depth + 1,
                ref truncatedByLimit);

            pathVertices.RemoveAt(pathVertices.Count - 1);
            pathVertexSet.Remove(nextVertexId);
            pathEdgeIndices.RemoveAt(pathEdgeIndices.Count - 1);
            usedEdgeIndices.Remove(edgeIndex);
        }
    }

    private static string BuildCycleKey(List<int> cycle)
    {
        if (cycle == null || cycle.Count == 0)
        {
            return string.Empty;
        }

        List<int> forward = BuildCanonicalRotation(cycle);
        List<int> reversed = new List<int>(cycle);
        reversed.Reverse();
        reversed = BuildCanonicalRotation(reversed);

        return CompareLexicographically(forward, reversed) <= 0
            ? string.Join("-", forward)
            : string.Join("-", reversed);
    }

    private static List<int> BuildCanonicalRotation(List<int> cycle)
    {
        int bestIndex = 0;
        for (int i = 1; i < cycle.Count; i++)
        {
            if (cycle[i] < cycle[bestIndex])
            {
                bestIndex = i;
            }
        }

        List<int> rotated = new List<int>(cycle.Count);
        for (int i = 0; i < cycle.Count; i++)
        {
            rotated.Add(cycle[(bestIndex + i) % cycle.Count]);
        }

        return rotated;
    }

    private static int CompareLexicographically(List<int> left, List<int> right)
    {
        int count = Mathf.Min(left.Count, right.Count);
        for (int i = 0; i < count; i++)
        {
            if (left[i] == right[i])
            {
                continue;
            }

            return left[i].CompareTo(right[i]);
        }

        return left.Count.CompareTo(right.Count);
    }

    private static string BuildPolygonKey(List<Vector3> polygon)
    {
        if (polygon == null || polygon.Count == 0)
        {
            return string.Empty;
        }

        List<string> forward = BuildCanonicalPolygonRotation(polygon);
        List<Vector3> reversedPolygon = new List<Vector3>(polygon);
        reversedPolygon.Reverse();
        List<string> reversed = BuildCanonicalPolygonRotation(reversedPolygon);

        return CompareLexicographically(forward, reversed) <= 0
            ? string.Join("|", forward)
            : string.Join("|", reversed);
    }

    private static List<string> BuildCanonicalPolygonRotation(List<Vector3> polygon)
    {
        List<string> keys = new List<string>(polygon.Count);
        for (int i = 0; i < polygon.Count; i++)
        {
            keys.Add(BuildPointKey(polygon[i]));
        }

        int bestIndex = 0;
        for (int i = 1; i < keys.Count; i++)
        {
            if (string.CompareOrdinal(keys[i], keys[bestIndex]) < 0)
            {
                bestIndex = i;
            }
        }

        List<string> rotated = new List<string>(keys.Count);
        for (int i = 0; i < keys.Count; i++)
        {
            rotated.Add(keys[(bestIndex + i) % keys.Count]);
        }

        return rotated;
    }

    private static int CompareLexicographically(List<string> left, List<string> right)
    {
        int count = Mathf.Min(left.Count, right.Count);
        for (int i = 0; i < count; i++)
        {
            int compare = string.CompareOrdinal(left[i], right[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return left.Count.CompareTo(right.Count);
    }

    private static void RemoveSequentialDuplicateVertices(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 2)
        {
            return;
        }

        for (int i = vertices.Count - 1; i >= 0; i--)
        {
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];
            if ((current - next).sqrMagnitude > BoundarySplitEpsilon * BoundarySplitEpsilon)
            {
                continue;
            }

            vertices.RemoveAt(i);
        }
    }

    private static void RemoveCollinearVertices(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return;
        }

        bool removedAny = true;
        while (removedAny && vertices.Count >= 3)
        {
            removedAny = false;
            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                Vector3 previous = vertices[(i - 1 + vertices.Count) % vertices.Count];
                Vector3 current = vertices[i];
                Vector3 next = vertices[(i + 1) % vertices.Count];

                Vector2 a = new Vector2(current.x - previous.x, current.z - previous.z);
                Vector2 b = new Vector2(next.x - current.x, next.z - current.z);
                float cross = a.x * b.y - a.y * b.x;
                if (Mathf.Abs(cross) > BoundarySplitEpsilon)
                {
                    continue;
                }

                vertices.RemoveAt(i);
                removedAny = true;
            }
        }
    }

    private static float CalculateSignedAreaXZ(List<Vector3> vertices)
    {
        float area = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];
            area += current.x * next.z - next.x * current.z;
        }

        return area * 0.5f;
    }

    private static bool IsPointInsidePolygonXZ(Vector3 point, List<Vector3> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector3 pi = polygon[i];
            Vector3 pj = polygon[j];
            bool intersects = ((pi.z > point.z) != (pj.z > point.z)) &&
                              (point.x < (pj.x - pi.x) * (point.z - pi.z) / ((pj.z - pi.z) + 0.000001f) + pi.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static Vector3 CalculatePolygonCentroidXZ(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count == 0)
        {
            return Vector3.zero;
        }

        float signedAreaTwice = 0f;
        float centroidX = 0f;
        float centroidZ = 0f;

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 current = vertices[i];
            Vector3 next = vertices[(i + 1) % vertices.Count];
            float cross = current.x * next.z - next.x * current.z;
            signedAreaTwice += cross;
            centroidX += (current.x + next.x) * cross;
            centroidZ += (current.z + next.z) * cross;
        }

        if (Mathf.Abs(signedAreaTwice) <= 0.000001f)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                sum += vertices[i];
            }

            return sum / vertices.Count;
        }

        float factor = 1f / (3f * signedAreaTwice);
        return new Vector3(centroidX * factor, vertices[0].y, centroidZ * factor);
    }
}
