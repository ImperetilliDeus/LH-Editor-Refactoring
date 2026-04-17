using System.Collections.Generic;
using UnityEngine;

public sealed class RoomPlanarGraph
{
    public sealed class Node
    {
        public int VertexId { get; }
        public Vector3 Position { get; set; }
        public List<HalfEdge> OutgoingEdges { get; } = new List<HalfEdge>();

        public Node(int vertexId, Vector3 position)
        {
            VertexId = vertexId;
            Position = position;
        }
    }

    public sealed class HalfEdge
    {
        public int Id { get; }
        public Node Origin { get; }
        public HalfEdge Twin { get; set; }
        public HalfEdge Next { get; set; }
        public Face Face { get; set; }
        public Wall SourceWall { get; }
        public VirtualBoundary SourceVirtualBoundary { get; }
        public float AngleRadians { get; }

        public Node Destination => Twin != null ? Twin.Origin : null;

        public HalfEdge(
            int id,
            Node origin,
            Wall sourceWall,
            VirtualBoundary sourceVirtualBoundary,
            float angleRadians)
        {
            Id = id;
            Origin = origin;
            SourceWall = sourceWall;
            SourceVirtualBoundary = sourceVirtualBoundary;
            AngleRadians = angleRadians;
        }
    }

    public sealed class Face
    {
        public int Id { get; }
        public List<HalfEdge> Boundary { get; } = new List<HalfEdge>();
        public List<Vector3> Vertices { get; } = new List<Vector3>();
        public HashSet<Wall> Walls { get; } = new HashSet<Wall>();
        public HashSet<VirtualBoundary> VirtualBoundaries { get; } = new HashSet<VirtualBoundary>();
        public float SignedArea { get; set; }
        public Vector3 Centroid { get; set; }

        public Face(int id)
        {
            Id = id;
        }
    }

    private readonly Dictionary<int, Node> nodesByVertexId = new Dictionary<int, Node>();
    private readonly List<Node> nodes = new List<Node>();
    private readonly List<HalfEdge> halfEdges = new List<HalfEdge>();
    private readonly List<Face> faces = new List<Face>();

    public IReadOnlyList<Node> Nodes => nodes;
    public IReadOnlyList<HalfEdge> HalfEdges => halfEdges;
    public IReadOnlyList<Face> Faces => faces;

    public Node GetOrCreateNode(int vertexId, Vector3 position)
    {
        if (nodesByVertexId.TryGetValue(vertexId, out Node existing))
        {
            existing.Position = position;
            return existing;
        }

        Node created = new Node(vertexId, position);
        nodesByVertexId[vertexId] = created;
        nodes.Add(created);
        return created;
    }

    public HalfEdge CreateHalfEdge(
        Node origin,
        Wall sourceWall,
        VirtualBoundary sourceVirtualBoundary,
        float angleRadians)
    {
        HalfEdge created = new HalfEdge(halfEdges.Count, origin, sourceWall, sourceVirtualBoundary, angleRadians);
        halfEdges.Add(created);
        origin?.OutgoingEdges.Add(created);
        return created;
    }

    public Face CreateFace()
    {
        Face created = new Face(faces.Count);
        faces.Add(created);
        return created;
    }
}
