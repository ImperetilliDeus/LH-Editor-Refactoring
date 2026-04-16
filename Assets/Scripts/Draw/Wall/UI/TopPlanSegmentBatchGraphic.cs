using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class TopPlanSegmentBatchGraphic : MaskableGraphic
{
    public struct SegmentData
    {
        public Vector2 start;
        public Vector2 end;
        public float thickness;
        public Color color;
        public bool dashed;
        public float dashLength;
        public float gapLength;
    }

    private readonly List<SegmentData> segments = new List<SegmentData>();

    public void SetSegments(IReadOnlyList<SegmentData> values)
    {
        segments.Clear();
        if (values != null)
        {
            for (int i = 0; i < values.Count; i++)
            {
                segments.Add(values[i]);
            }
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        int vertexOffset = 0;

        for (int i = 0; i < segments.Count; i++)
        {
            SegmentData segment = segments[i];
            Vector2 delta = segment.end - segment.start;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                continue;
            }

            Vector2 direction = delta / length;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (Mathf.Max(1f, segment.thickness) * 0.5f);
            if (segment.dashed)
            {
                float dashLength = Mathf.Max(1f, segment.dashLength);
                float gapLength = Mathf.Max(0f, segment.gapLength);
                float step = dashLength + gapLength;
                if (step <= 0.001f)
                {
                    step = dashLength;
                }

                for (float distance = 0f; distance < length; distance += step)
                {
                    float dashEndDistance = Mathf.Min(length, distance + dashLength);
                    Vector2 dashStart = segment.start + direction * distance;
                    Vector2 dashEnd = segment.start + direction * dashEndDistance;
                    AddQuad(vh, dashStart, dashEnd, normal, segment.color, ref vertexOffset);
                }

                continue;
            }

            float capExtension = Mathf.Max(0f, segment.thickness) * 0.5f;
            Vector2 extension = direction * capExtension;
            AddQuad(vh, segment.start - extension, segment.end + extension, normal, segment.color, ref vertexOffset);
        }
    }

    private static void AddQuad(VertexHelper vh, Vector2 start, Vector2 end, Vector2 normal, Color color, ref int vertexOffset)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;

        vertex.position = start - normal;
        vh.AddVert(vertex);
        vertex.position = start + normal;
        vh.AddVert(vertex);
        vertex.position = end + normal;
        vh.AddVert(vertex);
        vertex.position = end - normal;
        vh.AddVert(vertex);

        vh.AddTriangle(vertexOffset, vertexOffset + 1, vertexOffset + 2);
        vh.AddTriangle(vertexOffset, vertexOffset + 2, vertexOffset + 3);
        vertexOffset += 4;
    }
}
