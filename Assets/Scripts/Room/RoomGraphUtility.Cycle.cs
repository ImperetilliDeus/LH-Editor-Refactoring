using System.Collections.Generic;
using UnityEngine;

public static partial class RoomGraphUtility
{
    private const float MinFaceAreaEpsilon = 0.0001f;

    public static RoomPlanarGraph BuildPlanarGraph(HashSet<Wall> wallSet, IEnumerable<VirtualBoundary> virtualBoundaries)
    {
        return BuildPlanarGraphFromEdges(BuildBoundaryEdges(wallSet, virtualBoundaries));
    }

    public static RoomPlanarGraph BuildPlanarGraph(List<Vector3> outerPolygon, IEnumerable<VirtualBoundary> virtualBoundaries)
    {
        return BuildPlanarGraphFromEdges(BuildBoundaryEdges(outerPolygon, virtualBoundaries));
    }

    private static RoomPlanarGraph BuildPlanarGraphFromEdges(List<BoundaryEdge> edges)
    {
        if (edges == null || edges.Count < 3)
        {
            return null;
        }

        RoomPlanarGraph graph = new RoomPlanarGraph();
        List<RoomPlanarGraph.HalfEdge> halfEdges = new List<RoomPlanarGraph.HalfEdge>(edges.Count * 2);

        for (int i = 0; i < edges.Count; i++)
        {
            BoundaryEdge edge = edges[i];
            if (edge.startVertexId <= 0 || edge.endVertexId <= 0)
            {
                return graph;
            }

            RoomPlanarGraph.Node startNode = graph.GetOrCreateNode(edge.startVertexId, edge.start);
            RoomPlanarGraph.Node endNode = graph.GetOrCreateNode(edge.endVertexId, edge.end);

            RoomPlanarGraph.HalfEdge forward = graph.CreateHalfEdge(
                startNode,
                edge.wall,
                edge.virtualBoundary,
                Mathf.Atan2(edge.end.z - edge.start.z, edge.end.x - edge.start.x));
            RoomPlanarGraph.HalfEdge reverse = graph.CreateHalfEdge(
                endNode,
                edge.wall,
                edge.virtualBoundary,
                Mathf.Atan2(edge.start.z - edge.end.z, edge.start.x - edge.end.x));

            forward.Twin = reverse;
            reverse.Twin = forward;
            halfEdges.Add(forward);
            halfEdges.Add(reverse);
        }

        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            RoomPlanarGraph.Node node = graph.Nodes[i];
            node.OutgoingEdges.Sort((left, right) => left.AngleRadians.CompareTo(right.AngleRadians));
        }

        for (int i = 0; i < halfEdges.Count; i++)
        {
            RoomPlanarGraph.HalfEdge halfEdge = halfEdges[i];
            RoomPlanarGraph.Node destination = halfEdge.Destination;
            if (destination == null || destination.OutgoingEdges.Count == 0 || halfEdge.Twin == null)
            {
                continue;
            }

            List<RoomPlanarGraph.HalfEdge> outgoing = destination.OutgoingEdges;
            int twinPosition = outgoing.IndexOf(halfEdge.Twin);
            if (twinPosition < 0)
            {
                continue;
            }

            int nextPosition = twinPosition - 1;
            if (nextPosition < 0)
            {
                nextPosition = outgoing.Count - 1;
            }

            halfEdge.Next = outgoing[nextPosition];
        }

        bool[] visited = new bool[halfEdges.Count];
        HashSet<string> faceKeys = new HashSet<string>();
        for (int i = 0; i < halfEdges.Count; i++)
        {
            RoomPlanarGraph.HalfEdge startHalfEdge = halfEdges[i];
            if (startHalfEdge == null || visited[startHalfEdge.Id] || startHalfEdge.Next == null)
            {
                continue;
            }

            if (!TryCreateFace(startHalfEdge, visited, faceKeys, graph))
            {
                continue;
            }
        }

        return graph;
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

    private static bool TryCreateFace(
        RoomPlanarGraph.HalfEdge startHalfEdge,
        bool[] visited,
        HashSet<string> faceKeys,
        RoomPlanarGraph graph)
    {
        HashSet<int> seenHalfEdges = new HashSet<int>();
        List<RoomPlanarGraph.HalfEdge> boundary = new List<RoomPlanarGraph.HalfEdge>();
        RoomPlanarGraph.HalfEdge current = startHalfEdge;
        int safetyLimit = graph.HalfEdges.Count + 1;

        while (safetyLimit-- > 0)
        {
            if (current == null || current.Next == null || !seenHalfEdges.Add(current.Id))
            {
                return false;
            }

            boundary.Add(current);
            current = current.Next;
            if (current == startHalfEdge)
            {
                break;
            }
        }

        if (boundary.Count < 3 || safetyLimit <= 0)
        {
            return false;
        }

        List<Vector3> vertices = new List<Vector3>(boundary.Count);
        for (int i = 0; i < boundary.Count; i++)
        {
            vertices.Add(boundary[i].Origin.Position);
        }

        RemoveSequentialDuplicateVertices(vertices);
        RemoveCollinearVertices(vertices);
        if (vertices.Count < 3)
        {
            return false;
        }

        string key = BuildPolygonKey(vertices);
        if (!faceKeys.Add(key))
        {
            return false;
        }

        float area = CalculateSignedAreaXZ(vertices);
        if (Mathf.Abs(area) <= MinFaceAreaEpsilon)
        {
            return false;
        }

        if (area < 0f)
        {
            vertices.Reverse();
            area = -area;
        }

        RoomPlanarGraph.Face face = graph.CreateFace();
        face.SignedArea = area;
        face.Centroid = CalculatePolygonCentroidXZ(vertices);
        face.Vertices.AddRange(vertices);

        for (int i = 0; i < boundary.Count; i++)
        {
            RoomPlanarGraph.HalfEdge halfEdge = boundary[i];
            visited[halfEdge.Id] = true;
            halfEdge.Face = face;
            face.Boundary.Add(halfEdge);

            if (halfEdge.SourceWall != null)
            {
                face.Walls.Add(halfEdge.SourceWall);
            }

            if (halfEdge.SourceVirtualBoundary != null)
            {
                face.VirtualBoundaries.Add(halfEdge.SourceVirtualBoundary);
            }
        }

        return true;
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
