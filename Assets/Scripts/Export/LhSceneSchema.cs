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
    public struct LhLegacySceneDto
    {
        public LhVector3Dto startPoint;
        public List<LhLegacyWallDto> wallData;
        public List<LhLegacyRoomDto> roomData;
    }

    [Serializable]
    public struct LhWallDto
    {
        public string name;
        public int id;
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public string texture;
        public List<LhWallSegmentDto> segments;
    }

    [Serializable]
    public struct LhLegacyWallDto
    {
        public string name;
        public int id;
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public string texture;
        public List<LhWallSegmentDto> segments;
    }

    [Serializable]
    public struct LhWallSegmentDto
    {
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public bool hasInterior;
        public LhDoorDto door;
        public LhWindowDto window;
    }

    [Serializable]
    public struct LhRoomDto
    {
        public string id;
        public string name;
        public string code;
        public string roomTypeKey;
        public string nativeCode;
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public List<int> walls;
        public LhSurfaceDto floor;
        public LhSurfaceDto ceil;
        public List<LhFurnitureDto> furnish;
    }

    [Serializable]
    public struct LhLegacyRoomDto
    {
        public string name;
        public string code;
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public List<int> walls;
        public LhSurfaceDto floor;
        public LhSurfaceDto ceil;
        public List<LhLegacyFurnitureDto> furnish;
    }

    [Serializable]
    public struct LhSurfaceDto
    {
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public int meshType;
        public LhMeshDto mesh;
        public string texture;
    }

    [Serializable]
    public class LhDoorDto
    {
        public bool isExist;
        public string code;
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public string parametricProfileKey;
        public LhVector3Dto authoredSize;
        public float width;
        public float height;
        public float depth;
        public float bottomY;
    }

    [Serializable]
    public class LhWindowDto
    {
        public bool isExist;
        public string code;
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public string parametricProfileKey;
        public LhVector3Dto authoredSize;
        public float width;
        public float height;
        public float depth;
        public float bottomY;
    }

    [Serializable]
    public struct LhFurnitureDto
    {
        public string name;
        public string code;
        public string nativeCode;
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public List<LhFurnitureDefectDto> defects;
    }

    [Serializable]
    public struct LhLegacyFurnitureDto
    {
        public string code;
        public LhVector3Dto position;
        public LhVector3Dto angle;
        public LhVector3Dto scale;
        public List<LhFurnitureDefectDto> defects;
    }

    [Serializable]
    public struct LhFurnitureDefectDto
    {
        public string mntnCd;
        public string locCd;
        public string mtrlCd;
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
        public static void FillTransform(Transform transform, out LhVector3Dto positionDto, out LhVector3Dto angleDto, out LhVector3Dto scaleDto, bool localSpace = false)
        {
            if (transform == null)
            {
                positionDto = default;
                angleDto = default;
                scaleDto = default;
                return;
            }

            Vector3 position = localSpace ? transform.localPosition : transform.position;
            Vector3 eulerAngles = localSpace ? transform.localEulerAngles : transform.eulerAngles;
            Vector3 scale = localSpace ? transform.localScale : transform.lossyScale;

            FillTransform(position, eulerAngles, scale, out positionDto, out angleDto, out scaleDto);
        }

        public static void FillTransform(Vector3 position, Vector3 angle, Vector3 scale, out LhVector3Dto positionDto, out LhVector3Dto angleDto, out LhVector3Dto scaleDto)
        {
            positionDto = LhVector3Dto.FromVector3(position);
            angleDto = LhVector3Dto.FromVector3(angle);
            scaleDto = LhVector3Dto.FromVector3(scale);
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

        public static LhMeshDto CreateMeshFromPolygon(
            IReadOnlyList<Vector3> worldVertices,
            Vector3 center,
            Vector3 normal,
            bool normalizeUvs)
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
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            for (int i = 0; i < worldVertices.Count; i++)
            {
                Vector3 localVertex = worldVertices[i] - center;
                localVertices.Add(localVertex);
                dto.vertices.Add(LhVector3Dto.FromVector3(localVertex));
                dto.normals.Add(LhVector3Dto.FromVector3(normal));

                if (i == 0)
                {
                    min = localVertex;
                    max = localVertex;
                }
                else
                {
                    min = Vector3.Min(min, localVertex);
                    max = Vector3.Max(max, localVertex);
                }
            }

            float sizeX = Mathf.Max(max.x - min.x, 0.0001f);
            float sizeZ = Mathf.Max(max.z - min.z, 0.0001f);
            for (int i = 0; i < localVertices.Count; i++)
            {
                Vector3 localVertex = localVertices[i];
                Vector2 uv = normalizeUvs
                    ? new Vector2((localVertex.x - min.x) / sizeX, (localVertex.z - min.z) / sizeZ)
                    : new Vector2(localVertex.x, localVertex.z);
                dto.uvs.Add(LhVector2Dto.FromVector2(uv));
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

            if (ShouldFlipTriangleWinding(localVertices, triangles, normal))
            {
                for (int i = 0; i + 2 < triangles.Count; i += 3)
                {
                    int temp = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = temp;
                }
            }

            dto.triangles.AddRange(triangles);
            return dto;
        }

        public static LhMeshDto CreateMeshFromPolygon(IReadOnlyList<Vector3> worldVertices, Vector3 center)
        {
            return CreateMeshFromPolygon(worldVertices, center, Vector3.up, false);
        }

        private static bool ShouldFlipTriangleWinding(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, Vector3 expectedNormal)
        {
            if (vertices == null || triangles == null || triangles.Count < 3)
            {
                return expectedNormal.y < 0f;
            }

            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                int aIndex = triangles[i];
                int bIndex = triangles[i + 1];
                int cIndex = triangles[i + 2];
                if (aIndex < 0 || bIndex < 0 || cIndex < 0 ||
                    aIndex >= vertices.Count || bIndex >= vertices.Count || cIndex >= vertices.Count)
                {
                    continue;
                }

                Vector3 a = vertices[aIndex];
                Vector3 b = vertices[bIndex];
                Vector3 c = vertices[cIndex];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                return Vector3.Dot(cross, expectedNormal) < 0f;
            }

            return expectedNormal.y < 0f;
        }
    }
}
