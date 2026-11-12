using System.Collections.Generic;
using LH.Schema;
using UnityEngine;

namespace LH.Export
{
    public static partial class LhSceneExportBuilder
    {
        private static List<LhRoomDto> BuildRooms(IEnumerable<Room> rooms, BuildContext context)
        {
            List<LhRoomDto> results = new List<LhRoomDto>();
            if (rooms == null)
            {
                return results;
            }

            foreach (Room room in rooms)
            {
                if (room == null)
                {
                    continue;
                }

                results.Add(BuildRoom(room, context));
            }

            return results;
        }

        private static List<LhLegacyRoomDto> BuildLegacyRooms(IEnumerable<Room> rooms, BuildContext context)
        {
            List<LhLegacyRoomDto> results = new List<LhLegacyRoomDto>();
            if (rooms == null)
            {
                return results;
            }

            foreach (Room room in rooms)
            {
                if (room == null)
                {
                    continue;
                }

                RoomData roomData = room.Data;
                Vector3 roomCenter = roomData.Geometry.Center;
                Vector3 roomPosition = roomCenter + roomData.PlacementOffset;
                LhDtoFactory.FillTransform(roomPosition, Vector3.zero, Vector3.one, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);

                results.Add(new LhLegacyRoomDto
                {
                    name = string.IsNullOrWhiteSpace(roomData.RoomName) ? room.name : roomData.RoomName,
                    code = RoomCodeResolver.Resolve(roomData),
                    position = position,
                    angle = angle,
                    scale = scale,
                    walls = BuildRoomWallReferences(room, roomData, room.WallSet, context),
                    floor = BuildSurface(room, roomData, roomPosition, LegacyFloorWorldY, ResolveFloorTextureCode(room, roomData), true, false, context),
                    ceil = BuildSurface(room, roomData, roomPosition, GetCeilingWorldY(room, roomData, context), ResolveCeilingTextureCode(room, roomData), true, true, context),
                    furnish = BuildLegacyFurniture(room, context),
                });
            }

            return results;
        }

        private static LhRoomDto BuildRoom(Room room, BuildContext context)
        {
            RoomData roomData = room.Data;
            Vector3 roomCenter = roomData.Geometry.Center;
            Vector3 roomPosition = roomCenter + roomData.PlacementOffset;
            LhDtoFactory.FillTransform(roomPosition, Vector3.zero, Vector3.one, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);

            return new LhRoomDto
            {
                id = BuildRoomId(room, roomData),
                name = string.IsNullOrWhiteSpace(roomData.RoomName) ? room.name : roomData.RoomName,
                code = RoomCodeResolver.Resolve(roomData),
                roomTypeKey = roomData.RoomTypeKey ?? string.Empty,
                nativeCode = roomData.RoomNativeCode ?? string.Empty,
                position = position,
                angle = angle,
                scale = scale,
                walls = BuildRoomWallReferences(room, roomData, room.WallSet, context),
                floor = BuildSurface(room, roomData, roomPosition, DefaultFloorWorldY, ResolveFloorTextureCode(room, roomData), false, false, context),
                ceil = BuildSurface(room, roomData, roomPosition, GetCeilingWorldY(room, roomData, context), ResolveCeilingTextureCode(room, roomData), false, false, context),
                furnish = BuildFurniture(room, context),
            };
        }

        private static List<T> BuildFurnitureList<T>(
            Room room,
            BuildContext context,
            System.Func<FurnitureInstance, Room, LhVector3Dto, LhVector3Dto, LhVector3Dto, T> factory)
        {
            var results = new List<T>();
            if (room == null ||
                context == null ||
                !context.furnitureByRoom.TryGetValue(room, out List<FurnitureInstance> furnitureInstances))
            {
                return results;
            }

            for (int i = 0; i < furnitureInstances.Count; i++)
            {
                FurnitureInstance instance = furnitureInstances[i];
                if (instance == null || !instance.IsPlaced)
                {
                    continue;
                }

                FillRelativeTransform(room.transform, instance.transform, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);
                results.Add(factory(instance, room, position, angle, scale));
            }

            return results;
        }

        private static List<LhFurnitureDto> BuildFurniture(Room room, BuildContext context)
        {
            return BuildFurnitureList(room, context, (instance, resolvedRoom, position, angle, scale) => new LhFurnitureDto
            {
                name = instance.gameObject.name,
                code = instance.ExportCode ?? string.Empty,
                nativeCode = instance.NativeCode ?? string.Empty,
                position = position,
                angle = angle,
                scale = scale,
                defects = BuildFurnitureDefects(instance, resolvedRoom),
            });
        }

        private static List<LhLegacyFurnitureDto> BuildLegacyFurniture(Room room, BuildContext context)
        {
            return BuildFurnitureList(room, context, (instance, resolvedRoom, position, angle, scale) => new LhLegacyFurnitureDto
            {
                code = instance.ExportCode ?? string.Empty,
                position = position,
                angle = angle,
                scale = scale,
                defects = BuildFurnitureDefects(instance, resolvedRoom),
            });
        }

        private static List<Room> CollectRooms(IEnumerable<Room> rooms)
        {
            List<Room> results = new List<Room>();
            if (rooms == null)
            {
                return results;
            }

            foreach (Room room in rooms)
            {
                if (room != null)
                {
                    results.Add(room);
                }
            }

            return results;
        }

        private static void PrimeFurnitureLookup(IReadOnlyList<Room> rooms, BuildContext context)
        {
            if (context == null)
            {
                return;
            }

            context.furnitureByRoom.Clear();
            if (rooms != null)
            {
                for (int i = 0; i < rooms.Count; i++)
                {
                    Room room = rooms[i];
                    if (room != null && !context.furnitureByRoom.ContainsKey(room))
                    {
                        context.furnitureByRoom.Add(room, new List<FurnitureInstance>());
                    }
                }
            }

            FurnitureInstance[] furnitureInstances = Object.FindObjectsByType<FurnitureInstance>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < furnitureInstances.Length; i++)
            {
                FurnitureInstance instance = furnitureInstances[i];
                if (instance == null || !instance.IsPlaced)
                {
                    continue;
                }

                Room resolvedRoom = FurnitureRoomResolver.Resolve(instance, rooms);
                if (resolvedRoom == null)
                {
                    continue;
                }

                if (!context.furnitureByRoom.TryGetValue(resolvedRoom, out List<FurnitureInstance> bucket))
                {
                    bucket = new List<FurnitureInstance>();
                    context.furnitureByRoom.Add(resolvedRoom, bucket);
                }

                bucket.Add(instance);
            }
        }

        private static List<int> BuildRoomWallReferences(Room room, RoomData roomData, IEnumerable<Wall> walls, BuildContext context)
        {
            List<int> results = new List<int>();
            if (roomData == null)
            {
                return results;
            }

            HashSet<int> addedIds = new HashSet<int>();
            IReadOnlyList<string> wallIds = ResolveRoomWallReferenceIds(room, roomData);
            for (int i = 0; i < wallIds.Count; i++)
            {
                string wallIdKey = wallIds[i];
                if (string.IsNullOrWhiteSpace(wallIdKey) ||
                    !context.wallIdsByDataId.TryGetValue(wallIdKey, out int wallId) ||
                    !HasExportableRoomWallReferenceLength(wallIdKey, context) ||
                    !addedIds.Add(wallId))
                {
                    continue;
                }

                results.Add(wallId);
            }

            if (results.Count > 0 || walls == null)
            {
                return results;
            }

            foreach (Wall wall in walls)
            {
                if (wall == null || !HasExportableRoomWallReferenceLength(wall.Data))
                {
                    continue;
                }

                Transform root = GetWallExportRoot(wall.transform);
                if (root == null || !context.wallIdsByRoot.TryGetValue(root, out int wallId) || !addedIds.Add(wallId))
                {
                    continue;
                }

                results.Add(wallId);
            }

            return results;
        }

        private static IReadOnlyList<string> ResolveRoomWallReferenceIds(Room room, RoomData roomData)
        {
            IReadOnlyList<string> dataWallIds = roomData != null ? roomData.EffectiveWallIds : null;
            if (dataWallIds != null && dataWallIds.Count > 0)
            {
                return dataWallIds;
            }

            IReadOnlyList<string> roomWallIds = room != null ? room.EffectiveWallIds : null;
            if (roomWallIds != null && roomWallIds.Count > 0)
            {
                return roomWallIds;
            }

            IReadOnlyList<string> fallbackWallIds = roomData != null ? roomData.WallIds : null;
            return fallbackWallIds ?? new List<string>();
        }

        private static bool HasExportableRoomWallReferenceLength(string wallIdKey, BuildContext context)
        {
            if (string.IsNullOrWhiteSpace(wallIdKey) ||
                context == null ||
                !context.wallDataById.TryGetValue(wallIdKey, out WallData wallData) ||
                wallData == null)
            {
                return false;
            }

            return HasExportableRoomWallReferenceLength(wallData);
        }

        private static bool HasExportableRoomWallReferenceLength(WallData wallData)
        {
            if (wallData == null)
            {
                return false;
            }

            Vector3 delta = wallData.endPoint - wallData.startPoint;
            delta.y = 0f;
            return delta.magnitude >= MinimumExportedRoomWallReferenceLength;
        }

        private static List<LhFurnitureDefectDto> BuildFurnitureDefects(FurnitureInstance instance, Room room)
        {
            List<LhFurnitureDefectDto> results = new List<LhFurnitureDefectDto>();
            if (instance == null || instance.ExportDefects == null)
            {
                return results;
            }

            string roomCode = RoomCodeResolver.Resolve(room != null ? room.Data : null);
            for (int i = 0; i < instance.ExportDefects.Count; i++)
            {
                FurnitureDefectCatalogEntry entry = instance.ExportDefects[i];
                if (entry == null)
                {
                    continue;
                }

                results.Add(new LhFurnitureDefectDto
                {
                    mntnCd = !string.IsNullOrWhiteSpace(roomCode) ? roomCode : entry.mntnCd ?? string.Empty,
                    locCd = entry.locCd ?? string.Empty,
                    mtrlCd = entry.mtrlCd ?? string.Empty,
                });
            }

            return results;
        }

        private static LhSurfaceDto BuildSurface(
            Room room,
            RoomData roomData,
            Vector3 roomPosition,
            float worldY,
            string explicitTextureCode,
            bool legacyExact,
            bool flipNormals,
            BuildContext context)
        {
            IReadOnlyList<Vector3> boundaryVertices = ResolveSurfaceBoundaryVertices(room, roomData);
            string textureCode = explicitTextureCode ?? string.Empty;
            Vector3 localPosition = new Vector3(0f, worldY - roomPosition.y, 0f);
            List<List<Vector3>> surfaceRegions = ResolveSurfaceRegions(room, boundaryVertices, roomPosition, legacyExact, context);
            bool useCustomMesh = surfaceRegions != null;
            Vector3 surfaceScale = useCustomMesh ? Vector3.one : CalculateSurfaceScale(boundaryVertices);
            LhDtoFactory.FillTransform(localPosition, Vector3.zero, surfaceScale, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);

            int meshType = useCustomMesh ? 1 : 0;
            LhMeshDto mesh = meshType == 0
                ? CreateEmptyMeshDto()
                : CreateMeshFromSurfaceRegions(surfaceRegions, roomPosition, flipNormals ? Vector3.down : Vector3.up, legacyExact);

            return new LhSurfaceDto
            {
                position = position,
                angle = angle,
                scale = scale,
                meshType = meshType,
                mesh = mesh,
                texture = textureCode,
            };
        }

        private static List<List<Vector3>> ResolveSurfaceRegions(
            Room room,
            IReadOnlyList<Vector3> boundaryVertices,
            Vector3 roomPosition,
            bool legacyExact,
            BuildContext context)
        {
            if (room != null &&
                context != null &&
                context.roomSurfaceRegionsByRoom.TryGetValue(room, out List<List<Vector3>> resolvedRegions))
            {
                return resolvedRegions;
            }

            if (boundaryVertices == null || boundaryVertices.Count < 3)
            {
                return null;
            }

            return new List<List<Vector3>>
            {
                new List<Vector3>(boundaryVertices),
            };
        }

        private static LhMeshDto CreateMeshFromSurfaceRegions(
            IReadOnlyList<List<Vector3>> surfaceRegions,
            Vector3 center,
            Vector3 normal,
            bool normalizeUvs)
        {
            LhMeshDto dto = CreateEmptyMeshDto();
            if (surfaceRegions == null || surfaceRegions.Count == 0)
            {
                return dto;
            }

            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            bool hasBounds = false;
            List<List<Vector3>> localRegions = new List<List<Vector3>>(surfaceRegions.Count);
            for (int i = 0; i < surfaceRegions.Count; i++)
            {
                List<Vector3> region = surfaceRegions[i];
                if (region == null || region.Count < 3)
                {
                    continue;
                }

                List<Vector3> localRegion = new List<Vector3>(region.Count);
                for (int j = 0; j < region.Count; j++)
                {
                    Vector3 localVertex = region[j] - center;
                    localRegion.Add(localVertex);
                    if (!hasBounds)
                    {
                        min = localVertex;
                        max = localVertex;
                        hasBounds = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, localVertex);
                        max = Vector3.Max(max, localVertex);
                    }
                }

                localRegions.Add(localRegion);
            }

            if (!hasBounds)
            {
                return dto;
            }

            float sizeX = Mathf.Max(max.x - min.x, 0.0001f);
            float sizeZ = Mathf.Max(max.z - min.z, 0.0001f);
            for (int i = 0; i < localRegions.Count; i++)
            {
                List<Vector3> localRegion = localRegions[i];
                int vertexOffset = dto.vertices.Count;
                for (int j = 0; j < localRegion.Count; j++)
                {
                    Vector3 localVertex = localRegion[j];
                    dto.vertices.Add(LhVector3Dto.FromVector3(localVertex));
                    dto.normals.Add(LhVector3Dto.FromVector3(normal));
                    Vector2 uv = normalizeUvs
                        ? new Vector2((localVertex.x - min.x) / sizeX, (localVertex.z - min.z) / sizeZ)
                        : new Vector2(localVertex.x, localVertex.z);
                    dto.uvs.Add(LhVector2Dto.FromVector2(uv));
                }

                List<int> triangles = new List<int>();
                List<int> polygonIndices = new List<int>();
                if (!PolygonUtility.TryTriangulatePolygon(localRegion, triangles, polygonIndices))
                {
                    for (int j = 1; j < localRegion.Count - 1; j++)
                    {
                        triangles.Add(0);
                        triangles.Add(j);
                        triangles.Add(j + 1);
                    }
                }

                if (ShouldFlipTriangleWinding(localRegion, triangles, normal))
                {
                    for (int j = 0; j + 2 < triangles.Count; j += 3)
                    {
                        int temp = triangles[j + 1];
                        triangles[j + 1] = triangles[j + 2];
                        triangles[j + 2] = temp;
                    }
                }

                for (int j = 0; j < triangles.Count; j++)
                {
                    dto.triangles.Add(vertexOffset + triangles[j]);
                }
            }

            return dto;
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

                Vector3 normal = Vector3.Cross(vertices[bIndex] - vertices[aIndex], vertices[cIndex] - vertices[aIndex]);
                return Vector3.Dot(normal, expectedNormal) < 0f;
            }

            return expectedNormal.y < 0f;
        }

        private static IReadOnlyList<Vector3> ResolveSurfaceBoundaryVertices(Room room, RoomData roomData)
        {
            IReadOnlyList<Vector3> boundaryVertices = roomData != null ? roomData.BoundaryVertices : null;
            if (boundaryVertices != null && boundaryVertices.Count >= 3)
            {
                return boundaryVertices;
            }

            if (room == null || room.WallSet == null || room.WallSet.Count < 3)
            {
                return boundaryVertices;
            }

            List<Vector3> reconstructed = TryBuildPolygonFromWalls(room.WallSet);
            return reconstructed != null && reconstructed.Count >= 3 ? reconstructed : boundaryVertices;
        }

        private static List<Vector3> TryBuildPolygonFromWalls(IEnumerable<Wall> walls)
        {
            if (walls == null)
            {
                return null;
            }

            Dictionary<string, Vector3> positionsByKey = new Dictionary<string, Vector3>();
            Dictionary<string, HashSet<string>> neighborsByKey = new Dictionary<string, HashSet<string>>();
            string firstKey = null;

            foreach (Wall wall in walls)
            {
                if (wall == null || wall.Data == null)
                {
                    continue;
                }

                Vector3 start = FlattenWallPoint(wall.Data.startPoint);
                Vector3 end = FlattenWallPoint(wall.Data.endPoint);
                string startKey = BuildVertexKey(start);
                string endKey = BuildVertexKey(end);
                if (startKey == endKey)
                {
                    continue;
                }

                if (firstKey == null)
                {
                    firstKey = startKey;
                }

                positionsByKey[startKey] = start;
                positionsByKey[endKey] = end;
                AddNeighbor(neighborsByKey, startKey, endKey);
                AddNeighbor(neighborsByKey, endKey, startKey);
            }

            if (firstKey == null || !neighborsByKey.TryGetValue(firstKey, out HashSet<string> firstNeighbors) || firstNeighbors.Count == 0)
            {
                return null;
            }

            List<Vector3> orderedVertices = new List<Vector3>();
            HashSet<string> visitedEdges = new HashSet<string>();
            string previousKey = null;
            string currentKey = firstKey;

            for (int i = 0; i < neighborsByKey.Count + 2; i++)
            {
                orderedVertices.Add(positionsByKey[currentKey]);

                if (!neighborsByKey.TryGetValue(currentKey, out HashSet<string> neighbors) || neighbors.Count == 0)
                {
                    return null;
                }

                string nextKey = null;
                foreach (string candidate in neighbors)
                {
                    if (candidate != previousKey)
                    {
                        nextKey = candidate;
                        break;
                    }
                }

                if (nextKey == null)
                {
                    nextKey = previousKey;
                }

                if (nextKey == null)
                {
                    return null;
                }

                string edgeKey = BuildEdgeKey(currentKey, nextKey);
                if (!visitedEdges.Add(edgeKey))
                {
                    if (nextKey == firstKey)
                    {
                        break;
                    }

                    return null;
                }

                previousKey = currentKey;
                currentKey = nextKey;
                if (currentKey == firstKey)
                {
                    break;
                }
            }

            List<Vector3> sanitized = PolygonUtility.CreateSanitizedPolygonCopy(orderedVertices);
            return sanitized.Count >= 3 ? sanitized : null;
        }

        private static Vector3 FlattenWallPoint(Vector3 point)
        {
            return new Vector3(point.x, 0f, point.z);
        }

        private static string BuildVertexKey(Vector3 point)
        {
            return $"{Mathf.RoundToInt(point.x * 1000f)}:{Mathf.RoundToInt(point.z * 1000f)}";
        }

        private static string BuildEdgeKey(string first, string second)
        {
            return string.CompareOrdinal(first, second) <= 0
                ? $"{first}|{second}"
                : $"{second}|{first}";
        }

        private static void AddNeighbor(Dictionary<string, HashSet<string>> neighborsByKey, string key, string neighbor)
        {
            if (!neighborsByKey.TryGetValue(key, out HashSet<string> neighbors))
            {
                neighbors = new HashSet<string>();
                neighborsByKey[key] = neighbors;
            }

            neighbors.Add(neighbor);
        }

        private static string ResolveFloorTextureCode(Room room, RoomData roomData)
        {
            if (RoomManager.Instance != null && room != null)
            {
                return RoomManager.Instance.GetEffectiveFloorTextureCode(room);
            }

            return roomData != null ? roomData.FloorTextureCode ?? string.Empty : string.Empty;
        }

        private static string ResolveCeilingTextureCode(Room room, RoomData roomData)
        {
            if (RoomManager.Instance != null && room != null)
            {
                return RoomManager.Instance.GetEffectiveCeilingTextureCode(room);
            }

            return roomData != null ? roomData.CeilingTextureCode ?? string.Empty : string.Empty;
        }

        private static LhMeshDto CreateEmptyMeshDto()
        {
            return new LhMeshDto
            {
                vertices = new List<LhVector3Dto>(),
                triangles = new List<int>(),
                normals = new List<LhVector3Dto>(),
                uvs = new List<LhVector2Dto>(),
            };
        }

        private static string BuildRoomId(Room room, RoomData roomData)
        {
            string resolvedRoomCode = RoomCodeResolver.Resolve(roomData);
            if (!string.IsNullOrWhiteSpace(resolvedRoomCode))
            {
                return $"room_{resolvedRoomCode}";
            }

            if (room != null && !string.IsNullOrWhiteSpace(room.name))
            {
                return $"room_{SanitizeIdToken(room.name)}";
            }

            return "room";
        }
    }
}
