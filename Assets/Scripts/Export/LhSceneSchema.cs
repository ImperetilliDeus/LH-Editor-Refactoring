using System;
using System.Collections.Generic;
using UnityEngine;

namespace LH.Schema
{
    [Serializable]
    public struct LhSceneDto
    {
        public int version;
        public LhVector3Dto startPoint;
        public List<LhWallDto> wallData;
        public List<LhRoomDto> roomData;
    }

    [Serializable]
    public struct LhWallDto
    {
        public string name;
        public int id;
        public LhTransformDto transform;
        public List<LhWallSegmentDto> segments;
    }

    [Serializable]
    public struct LhWallSegmentDto
    {
        public LhTransformDto transform;
        public bool hasInterior;
        public LhDoorDto door;
        public LhWindowDto window;
    }

    [Serializable]
    public struct LhRoomDto
    {
        public string name;
        public string code;
        public LhTransformDto transform;
        public List<int> walls;
        public LhSurfaceDto floor;
        public LhSurfaceDto ceil;
        public List<LhFurnitureDto> furnish;
    }

    [Serializable]
    public struct LhSurfaceDto
    {
        public LhTransformDto transform;
        public int meshType;
        public LhMeshDto mesh;
        public string texture;
    }

    [Serializable]
    public class LhDoorDto
    {
        public bool isExist;
        public string code;
        public LhTransformDto transform;
    }

    [Serializable]
    public class LhWindowDto
    {
        public bool isExist;
        public string code;
        public LhTransformDto transform;
    }

    [Serializable]
    public struct LhFurnitureDto
    {
        public string name;
        public string code;
        public LhTransformDto transform;
    }

    [Serializable]
    public struct LhTransformDto
    {
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
    }

    [Serializable]
    public struct LhMeshDto
    {
        public List<LhVector3Dto> vertices;
        public List<int> triangles;
        public List<LhVector3Dto> normals;
        public List<LhVector2Dto> uvs;
    }

    [Serializable]
    public struct LhVector3Dto
    {
        public float x;
        public float y;
        public float z;

        public static LhVector3Dto FromVector3(Vector3 value)
        {
            return new LhVector3Dto
            {
                x = value.x,
                y = value.y,
                z = value.z,
            };
        }
    }

    [Serializable]
    public struct LhVector2Dto
    {
        public float x;
        public float y;

        public static LhVector2Dto FromVector2(Vector2 value)
        {
            return new LhVector2Dto
            {
                x = value.x,
                y = value.y,
            };
        }
    }

    public static class LhDtoFactory
    {
        public static LhTransformDto CreateTransform(Transform transform, bool localSpace = false)
        {
            if (transform == null)
            {
                return default;
            }

            Vector3 position = localSpace ? transform.localPosition : transform.position;
            Vector3 eulerAngles = localSpace ? transform.localEulerAngles : transform.eulerAngles;
            Vector3 scale = localSpace ? transform.localScale : transform.lossyScale;

            return new LhTransformDto
            {
                position = LhVector3Dto.FromVector3(position),
                angle = LhVector3Dto.FromVector3(eulerAngles),
                scale = LhVector3Dto.FromVector3(scale),
            };
        }

        public static LhMeshDto CreateMesh(Mesh mesh)
        {
            LhMeshDto dto = new LhMeshDto
            {
                vertices = new List<LhVector3Dto>(),
                triangles = new List<int>(),
                normals = new List<LhVector3Dto>(),
                uvs = new List<LhVector2Dto>(),
            };

            if (mesh == null)
            {
                return dto;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                dto.vertices.Add(LhVector3Dto.FromVector3(vertices[i]));
            }

            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i++)
            {
                dto.triangles.Add(triangles[i]);
            }

            Vector3[] normals = mesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                dto.normals.Add(LhVector3Dto.FromVector3(normals[i]));
            }

            Vector2[] uvs = mesh.uv;
            for (int i = 0; i < uvs.Length; i++)
            {
                dto.uvs.Add(LhVector2Dto.FromVector2(uvs[i]));
            }

            return dto;
        }

        public static LhMeshDto CreateMeshFromPolygon(IReadOnlyList<Vector3> worldVertices, Vector3 center)
        {
            LhMeshDto dto = new LhMeshDto
            {
                vertices = new List<LhVector3Dto>(),
                triangles = new List<int>(),
                normals = new List<LhVector3Dto>(),
                uvs = new List<LhVector2Dto>(),
            };

            if (worldVertices == null || worldVertices.Count < 3)
            {
                return dto;
            }

            List<Vector3> localVertices = new List<Vector3>(worldVertices.Count);
            for (int i = 0; i < worldVertices.Count; i++)
            {
                Vector3 localVertex = worldVertices[i] - center;
                localVertices.Add(localVertex);
                dto.vertices.Add(LhVector3Dto.FromVector3(localVertex));
                dto.normals.Add(LhVector3Dto.FromVector3(Vector3.up));
                dto.uvs.Add(LhVector2Dto.FromVector2(new Vector2(localVertex.x, localVertex.z)));
            }

            List<int> triangles = new List<int>();
            List<int> polygonIndices = new List<int>();
            if (!PolygonUtility.TryTriangulatePolygon(localVertices, triangles, polygonIndices))
            {
                for (int i = 1; i < localVertices.Count - 1; i++)
                {
                    triangles.Add(0);
                    triangles.Add(i);
                    triangles.Add(i + 1);
                }
            }

            dto.triangles.AddRange(triangles);
            return dto;
        }
    }
}
