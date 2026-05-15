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
                    code = ResolveRoomCode(roomData),
                    position = position,
                    angle = angle,
                    scale = scale,
                    walls = BuildRoomWallReferences(room, roomData, room.WallSet, context),
                    floor = BuildSurface(room, roomData, roomPosition, LegacyFloorWorldY, ResolveFloorTextureCode(room, roomData), true, false),
                    ceil = BuildSurface(room, roomData, roomPosition, GetCeilingWorldY(roomData, context), ResolveCeilingTextureCode(room, roomData), true, true),
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
                code = ResolveRoomCode(roomData),
                roomTypeKey = roomData.RoomTypeKey ?? string.Empty,
                nativeCode = roomData.RoomNativeCode ?? string.Empty,
                position = position,
                angle = angle,
                scale = scale,
                walls = BuildRoomWallReferences(room, roomData, room.WallSet, context),
                floor = BuildSurface(room, roomData, roomPosition, DefaultFloorWorldY, ResolveFloorTextureCode(room, roomData), false, false),
                ceil = BuildSurface(room, roomData, roomPosition, GetCeilingWorldY(roomData, context), ResolveCeilingTextureCode(room, roomData), false, false),
                furnish = BuildFurniture(room, context),
            };
        }

        private static List<T> BuildFurnitureList<T>(
            Room room,
            BuildContext context,
            System.Func<FurnitureInstance, LhVector3Dto, LhVector3Dto, LhVector3Dto, T> factory)
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
                results.Add(factory(instance, position, angle, scale));
            }

            return results;
        }

        private static List<LhFurnitureDto> BuildFurniture(Room room, BuildContext context)
        {
            return BuildFurnitureList(room, context, (instance, position, angle, scale) => new LhFurnitureDto
            {
                name = instance.gameObject.name,
                code = instance.ExportCode ?? string.Empty,
                nativeCode = instance.NativeCode ?? string.Empty,
                position = position,
                angle = angle,
                scale = scale,
                defects = BuildFurnitureDefects(instance),
            });
        }

        private static List<LhLegacyFurnitureDto> BuildLegacyFurniture(Room room, BuildContext context)
        {
            return BuildFurnitureList(room, context, (instance, position, angle, scale) => new LhLegacyFurnitureDto
            {
                code = instance.ExportCode ?? string.Empty,
                position = position,
                angle = angle,
                scale = scale,
                defects = BuildFurnitureDefects(instance),
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

                Room resolvedRoom = ResolveFurnitureRoom(instance, rooms);
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

        private static Room ResolveFurnitureRoom(FurnitureInstance instance, IReadOnlyList<Room> rooms)
        {
            if (instance == null || rooms == null || rooms.Count == 0)
            {
                return null;
            }

            Bounds bounds = instance.CalculateWorldBounds();
            Room currentRoom = instance.CurrentRoom;
            if (IsFurnitureInsideRoom(currentRoom, bounds, instance.transform.position))
            {
                return currentRoom;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (IsFurnitureInsideRoom(room, bounds, instance.transform.position))
                {
                    return room;
                }
            }

            return null;
        }

        private static bool IsFurnitureInsideRoom(Room room, Bounds bounds, Vector3 worldPosition)
        {
            if (room == null)
            {
                return false;
            }

            List<Vector3> vertices = new List<Vector3>();
            if (!room.TryGetOrderedVertices(vertices))
            {
                return false;
            }

            return IsBoundsFootprintInsidePolygonXZ(bounds, vertices) || IsPointInsidePolygonXZ(worldPosition, vertices);
        }

        private static bool IsPointInsidePolygonXZ(Vector3 point, List<Vector3> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            bool inside = false;
            float x = point.x;
            float z = point.z;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector3 pi = polygon[i];
                Vector3 pj = polygon[j];

                bool intersects = ((pi.z > z) != (pj.z > z)) &&
                                  (x < (pj.x - pi.x) * (z - pi.z) / Mathf.Max(0.000001f, pj.z - pi.z) + pi.x);
                if (intersects)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static bool IsBoundsFootprintInsidePolygonXZ(Bounds bounds, List<Vector3> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            float y = bounds.center.y;
            Vector3[] testPoints =
            {
                new Vector3(bounds.center.x, y, bounds.center.z),
                new Vector3(bounds.min.x, y, bounds.min.z),
                new Vector3(bounds.min.x, y, bounds.max.z),
                new Vector3(bounds.max.x, y, bounds.min.z),
                new Vector3(bounds.max.x, y, bounds.max.z),
            };

            for (int i = 0; i < testPoints.Length; i++)
            {
                if (!IsPointInsidePolygonXZ(testPoints[i], polygon))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<int> BuildRoomWallReferences(Room room, RoomData roomData, IEnumerable<Wall> walls, BuildContext context)
        {
            List<int> results = new List<int>();
            if (roomData == null)
            {
                return results;
            }

            HashSet<int> addedIds = new HashSet<int>();
            IReadOnlyList<string> wallIds = room != null ? room.EffectiveWallIds : roomData.WallIds;
            for (int i = 0; i < wallIds.Count; i++)
            {
                string wallIdKey = wallIds[i];
                if (string.IsNullOrWhiteSpace(wallIdKey) ||
                    !context.wallIdsByDataId.TryGetValue(wallIdKey, out int wallId) ||
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
                if (wall == null)
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

        private static List<LhFurnitureDefectDto> BuildFurnitureDefects(FurnitureInstance instance)
        {
            List<LhFurnitureDefectDto> results = new List<LhFurnitureDefectDto>();
            if (instance == null || instance.ExportDefects == null)
            {
                return results;
            }

            for (int i = 0; i < instance.ExportDefects.Count; i++)
            {
                FurnitureDefectCatalogEntry entry = instance.ExportDefects[i];
                if (entry == null)
                {
                    continue;
                }

                results.Add(new LhFurnitureDefectDto
                {
                    mntnCd = entry.mntnCd ?? string.Empty,
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
            bool flipNormals)
        {
            IReadOnlyList<Vector3> boundaryVertices = ResolveSurfaceBoundaryVertices(room, roomData);
            string textureCode = explicitTextureCode ?? string.Empty;
            Vector3 localPosition = new Vector3(0f, worldY - roomPosition.y, 0f);
            bool useCustomMesh = boundaryVertices != null && boundaryVertices.Count >= 3;
            Vector3 surfaceScale = useCustomMesh ? Vector3.one : CalculateSurfaceScale(boundaryVertices);
            LhDtoFactory.FillTransform(localPosition, Vector3.zero, surfaceScale, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);

            int meshType = boundaryVertices != null && boundaryVertices.Count >= 3 ? 1 : 0;
            LhMeshDto mesh = meshType == 0
                ? CreateEmptyMeshDto()
                : LhDtoFactory.CreateMeshFromPolygon(
                    boundaryVertices,
                    roomPosition,
                    flipNormals ? Vector3.down : Vector3.up,
                    legacyExact);

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

        private static string ResolveRoomCode(RoomData roomData)
        {
            if (roomData == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(roomData.RoomCode))
            {
                return roomData.RoomCode;
            }

            return RoomTypeCatalog.TryGetCode(roomData.RoomTypeKey, out int resolvedCode)
                ? resolvedCode.ToString()
                : string.Empty;
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
            string resolvedRoomCode = ResolveRoomCode(roomData);
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
