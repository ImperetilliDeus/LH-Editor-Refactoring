using System.Collections.Generic;
using LH.Schema;
using UnityEngine;

namespace LH.Export
{
    public static class LhSceneExportBuilder
    {
        public enum ExportMode
        {
            Extended = 0,
            LegacyExact = 1,
        }

        private const int CurrentSchemaVersion = 1;
        private const float DefaultFloorWorldY = 0.1f;
        private const float LegacyFloorWorldY = 0.01f;

        private sealed class BuildContext
        {
            public readonly Dictionary<Transform, int> wallIdsByRoot = new Dictionary<Transform, int>();
            public readonly Dictionary<string, int> wallIdsByDataId = new Dictionary<string, int>();
            public readonly Dictionary<string, WallData> wallDataById = new Dictionary<string, WallData>();
            public readonly Dictionary<Room, List<FurnitureInstance>> furnitureByRoom = new Dictionary<Room, List<FurnitureInstance>>();
            public ExportMode exportMode = ExportMode.Extended;
            public int nextWallId = 1;
        }

        public static LhSceneDto Build(Vector3 startPoint, IEnumerable<Wall> walls, IEnumerable<Room> rooms)
        {
            List<Room> roomList = CollectRooms(rooms);
            BuildContext context = new BuildContext
            {
                exportMode = ExportMode.Extended,
            };

            PrimeFurnitureLookup(roomList, context);
            return new LhSceneDto
            {
                version = CurrentSchemaVersion,
                startPoint = LhVector3Dto.FromVector3(startPoint),
                wallData = BuildWalls(walls, context),
                roomData = BuildRooms(roomList, context),
            };
        }

        public static LhLegacySceneDto BuildLegacy(Vector3 startPoint, IEnumerable<Wall> walls, IEnumerable<Room> rooms)
        {
            List<Room> roomList = CollectRooms(rooms);
            BuildContext context = new BuildContext
            {
                exportMode = ExportMode.LegacyExact,
            };

            PrimeFurnitureLookup(roomList, context);
            return new LhLegacySceneDto
            {
                startPoint = LhVector3Dto.FromVector3(startPoint),
                wallData = BuildLegacyWalls(walls, context),
                roomData = BuildLegacyRooms(roomList, context),
            };
        }

        public static List<string> CollectLegacyWarnings(IEnumerable<Wall> walls, IEnumerable<Room> rooms)
        {
            List<string> warnings = new List<string>();
            HashSet<Transform> exportedRoots = new HashSet<Transform>();
            if (walls != null)
            {
                foreach (Wall wall in walls)
                {
                    if (wall == null)
                    {
                        continue;
                    }

                    Transform root = GetWallExportRoot(wall.transform);
                    if (root == null || !exportedRoots.Add(root))
                    {
                        continue;
                    }

                    if (!TryParseWallNameId(root, out _))
                    {
                        warnings.Add($"Legacy export warning: wall root '{root.name}' does not contain a numeric suffix, so fallback ids may differ from 55A-style data.");
                    }
                }
            }

            if (rooms != null)
            {
                foreach (Room room in rooms)
                {
                    if (room == null)
                    {
                        continue;
                    }

                    IReadOnlyList<Vector3> boundaryVertices = room.Data != null ? room.Data.BoundaryVertices : null;
                    if (boundaryVertices != null && boundaryVertices.Count >= 3 && !IsLegacyRectSurface(boundaryVertices))
                    {
                        warnings.Add($"Legacy export note: room '{room.name}' will export polygon floor/ceil meshType=1 instead of empty meshType=0.");
                    }
                }
            }

            return warnings;
        }

        private static List<LhWallDto> BuildWalls(IEnumerable<Wall> walls, BuildContext context)
        {
            List<LhWallDto> results = new List<LhWallDto>();
            if (walls == null)
            {
                return results;
            }

            HashSet<Transform> exportedRoots = new HashSet<Transform>();
            foreach (Wall wall in walls)
            {
                if (wall == null)
                {
                    continue;
                }

                Transform root = GetWallExportRoot(wall.transform);
                if (root == null || !exportedRoots.Add(root))
                {
                    continue;
                }

                int wallId = ResolveWallExportId(root, context);
                context.wallIdsByRoot[root] = wallId;
                RegisterWallDataIds(root, wallId, context);
                results.Add(BuildWall(root, wallId, context.exportMode == ExportMode.LegacyExact));
            }

            return results;
        }

        private static List<LhLegacyWallDto> BuildLegacyWalls(IEnumerable<Wall> walls, BuildContext context)
        {
            List<LhLegacyWallDto> results = new List<LhLegacyWallDto>();
            List<LhWallDto> builtWalls = BuildWalls(walls, context);
            for (int i = 0; i < builtWalls.Count; i++)
            {
                LhWallDto wall = builtWalls[i];
                results.Add(new LhLegacyWallDto
                {
                    name = wall.name,
                    id = wall.id,
                    position = wall.position,
                    angle = wall.angle,
                    scale = wall.scale,
                    segments = wall.segments,
                });
            }

            return results;
        }

        private static LhWallDto BuildWall(Transform root, int wallId, bool legacyExact)
        {
            List<LhWallSegmentDto> segments = new List<LhWallSegmentDto>();
            WallOpeningContainer container = root.GetComponent<WallOpeningContainer>();

            if (container != null)
            {
                Wall[] childWalls = root.GetComponentsInChildren<Wall>(true);
                List<Wall> orderedSegments = new List<Wall>();
                for (int i = 0; i < childWalls.Length; i++)
                {
                    Wall segmentWall = childWalls[i];
                    if (segmentWall == null || segmentWall.transform.parent != root)
                    {
                        continue;
                    }

                    orderedSegments.Add(segmentWall);
                }

                orderedSegments.Sort((left, right) =>
                {
                    float leftDistance = GetSegmentSortDistance(container, left);
                    float rightDistance = GetSegmentSortDistance(container, right);
                    return leftDistance.CompareTo(rightDistance);
                });

                for (int i = 0; i < orderedSegments.Count; i++)
                {
                    segments.Add(BuildSegmentForContainer(root, orderedSegments[i], container, legacyExact));
                }
            }
            else if (root.TryGetComponent(out Wall wall))
            {
                segments.Add(BuildStandaloneSegment(wall));
            }

            LhDtoFactory.FillTransform(root, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);
            return new LhWallDto
            {
                name = root.name,
                id = wallId,
                position = position,
                angle = angle,
                scale = scale,
                segments = segments,
            };
        }

        private static LhWallSegmentDto BuildStandaloneSegment(Wall wall)
        {
            LhDtoFactory.FillTransform(Vector3.zero, Vector3.zero, Vector3.one, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);
            return new LhWallSegmentDto
            {
                position = position,
                angle = angle,
                scale = scale,
                hasInterior = false,
                door = null,
                window = null,
            };
        }

        private static LhWallSegmentDto BuildSegmentForContainer(Transform root, Wall segmentWall, WallOpeningContainer container, bool legacyExact)
        {
            WallOpening attachedOpening = FindOpeningForSegment(segmentWall, container);
            bool hasInterior = attachedOpening != null;
            LhDtoFactory.FillTransform(segmentWall.transform, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale, true);

            return new LhWallSegmentDto
            {
                position = position,
                angle = angle,
                scale = scale,
                hasInterior = hasInterior,
                door = attachedOpening != null && attachedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door
                    ? BuildDoor(attachedOpening, root, legacyExact)
                    : null,
                window = attachedOpening != null && attachedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Window
                    ? BuildWindow(attachedOpening, root, legacyExact)
                    : null,
            };
        }

        private static LhDoorDto BuildDoor(WallOpening opening, Transform root, bool legacyExact)
        {
            if (legacyExact)
            {
                LhDtoFactory.FillTransform(Vector3.zero, Vector3.zero, Vector3.one, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);
                return new LhDoorDto
                {
                    isExist = true,
                    code = string.IsNullOrWhiteSpace(opening.DoorTypeKey) ? "Pass" : opening.DoorTypeKey,
                    position = position,
                    angle = angle,
                    scale = scale,
                };
            }

            FillRelativeTransform(root, opening.transform, out LhVector3Dto relativePosition, out LhVector3Dto relativeAngle, out LhVector3Dto relativeScale);
            return new LhDoorDto
            {
                isExist = true,
                code = string.IsNullOrWhiteSpace(opening.DoorTypeKey) ? "Door" : opening.DoorTypeKey,
                position = relativePosition,
                angle = relativeAngle,
                scale = relativeScale,
            };
        }

        private static LhWindowDto BuildWindow(WallOpening opening, Transform root, bool legacyExact)
        {
            if (legacyExact)
            {
                LhDtoFactory.FillTransform(Vector3.zero, Vector3.zero, Vector3.one, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);
                return new LhWindowDto
                {
                    isExist = true,
                    code = string.IsNullOrWhiteSpace(opening.WindowTypeKey) ? "Window" : opening.WindowTypeKey,
                    position = position,
                    angle = angle,
                    scale = scale,
                };
            }

            FillRelativeTransform(root, opening.transform, out LhVector3Dto relativePosition, out LhVector3Dto relativeAngle, out LhVector3Dto relativeScale);
            return new LhWindowDto
            {
                isExist = true,
                code = string.IsNullOrWhiteSpace(opening.WindowTypeKey) ? "Window" : opening.WindowTypeKey,
                position = relativePosition,
                angle = relativeAngle,
                scale = relativeScale,
            };
        }

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
                    floor = BuildSurface(roomData, roomPosition, LegacyFloorWorldY, ResolveFloorTextureCode(room, roomData), true, false),
                    ceil = BuildSurface(roomData, roomPosition, GetCeilingWorldY(roomData, context), ResolveCeilingTextureCode(room, roomData), true, true),
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
                floor = BuildSurface(roomData, roomPosition, DefaultFloorWorldY, ResolveFloorTextureCode(room, roomData), false, false),
                ceil = BuildSurface(roomData, roomPosition, GetCeilingWorldY(roomData, context), ResolveCeilingTextureCode(room, roomData), false, false),
                furnish = BuildFurniture(room, context),
            };
        }

        private static List<LhFurnitureDto> BuildFurniture(Room room, BuildContext context)
        {
            List<LhFurnitureDto> results = new List<LhFurnitureDto>();
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
                results.Add(new LhFurnitureDto
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

            return results;
        }

        private static List<LhLegacyFurnitureDto> BuildLegacyFurniture(Room room, BuildContext context)
        {
            List<LhLegacyFurnitureDto> results = new List<LhLegacyFurnitureDto>();
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
                results.Add(new LhLegacyFurnitureDto
                {
                    code = instance.ExportCode ?? string.Empty,
                    position = position,
                    angle = angle,
                    scale = scale,
                    defects = BuildFurnitureDefects(instance),
                });
            }

            return results;
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
            RoomData roomData,
            Vector3 roomPosition,
            float worldY,
            string explicitTextureCode,
            bool legacyExact,
            bool flipNormals)
        {
            IReadOnlyList<Vector3> boundaryVertices = roomData != null ? roomData.BoundaryVertices : null;
            string textureCode = explicitTextureCode ?? string.Empty;
            Vector3 localPosition = new Vector3(0f, worldY - roomPosition.y, 0f);
            Vector3 surfaceScale = CalculateSurfaceScale(boundaryVertices);
            LhDtoFactory.FillTransform(localPosition, Vector3.zero, surfaceScale, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);

            bool useLegacyRectSurface = legacyExact && IsLegacyRectSurface(boundaryVertices);
            int meshType = useLegacyRectSurface ? 0 : boundaryVertices != null && boundaryVertices.Count >= 3 ? 1 : 0;
            LhMeshDto mesh = meshType == 0
                ? CreateEmptyMeshDto()
                : LhDtoFactory.CreateMeshFromPolygon(
                    boundaryVertices,
                    roomData != null ? roomData.Geometry.Center : Vector3.zero,
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

        private static WallOpening FindOpeningForSegment(Wall segmentWall, WallOpeningContainer container)
        {
            if (segmentWall == null || container == null)
            {
                return null;
            }

            float segmentMin = GetDistanceAlongContainer(container, segmentWall.Data.startPoint);
            float segmentMax = GetDistanceAlongContainer(container, segmentWall.Data.endPoint);
            if (segmentMin > segmentMax)
            {
                float swap = segmentMin;
                segmentMin = segmentMax;
                segmentMax = swap;
            }

            WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
            for (int i = 0; i < openings.Length; i++)
            {
                WallOpening opening = openings[i];
                if (opening == null)
                {
                    continue;
                }

                float openingStart = opening.CenterDistance - opening.Width * 0.5f;
                float openingEnd = opening.CenterDistance + opening.Width * 0.5f;
                if (openingEnd + 0.0001f < segmentMin || openingStart - 0.0001f > segmentMax)
                {
                    continue;
                }

                return opening;
            }

            return null;
        }

        private static float GetDistanceAlongContainer(WallOpeningContainer container, Vector3 point)
        {
            Vector3 fromStart = point - container.WallStart;
            return Vector3.Dot(fromStart, container.WallDirection);
        }

        private static float GetSegmentSortDistance(WallOpeningContainer container, Wall segmentWall)
        {
            if (container == null || segmentWall == null || segmentWall.Data == null)
            {
                return 0f;
            }

            float startDistance = GetDistanceAlongContainer(container, segmentWall.Data.startPoint);
            float endDistance = GetDistanceAlongContainer(container, segmentWall.Data.endPoint);
            return Mathf.Min(startDistance, endDistance);
        }

        private static Vector3 CalculateSurfaceScale(IReadOnlyList<Vector3> boundaryVertices)
        {
            if (boundaryVertices == null || boundaryVertices.Count == 0)
            {
                return Vector3.one;
            }

            Vector3 min = boundaryVertices[0];
            Vector3 max = boundaryVertices[0];
            for (int i = 1; i < boundaryVertices.Count; i++)
            {
                Vector3 vertex = boundaryVertices[i];
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            Vector3 size = max - min;
            if (size.x <= 0.0001f)
            {
                size.x = 1f;
            }

            if (size.z <= 0.0001f)
            {
                size.z = 1f;
            }

            return new Vector3(size.x, 1f, size.z);
        }

        private static void FillRelativeTransform(Transform root, Transform target, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale)
        {
            if (root == null || target == null)
            {
                position = default;
                angle = default;
                scale = default;
                return;
            }

            LhDtoFactory.FillTransform(
                root.InverseTransformPoint(target.position),
                (Quaternion.Inverse(root.rotation) * target.rotation).eulerAngles,
                target.localScale,
                out position,
                out angle,
                out scale);
        }

        private static string SanitizeIdToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "item";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (char.IsLetterOrDigit(current))
                {
                    builder.Append(char.ToLowerInvariant(current));
                    continue;
                }

                if (builder.Length == 0 || builder[builder.Length - 1] == '_')
                {
                    continue;
                }

                builder.Append('_');
            }

            return builder.Length > 0 ? builder.ToString().Trim('_') : "item";
        }

        private static Transform GetWallExportRoot(Transform wallTransform)
        {
            if (wallTransform == null)
            {
                return null;
            }

            WallOpeningContainer container = wallTransform.GetComponentInParent<WallOpeningContainer>();
            return container != null ? container.transform : wallTransform;
        }

        private static int ResolveWallExportId(Transform root, BuildContext context)
        {
            if (context != null && context.exportMode == ExportMode.LegacyExact && TryParseWallNameId(root, out int parsedId))
            {
                return parsedId;
            }

            int nextWallId = context != null ? context.nextWallId : 1;
            if (context != null)
            {
                context.nextWallId++;
            }

            return nextWallId;
        }

        private static bool TryParseWallNameId(Transform root, out int wallId)
        {
            wallId = 0;
            if (root == null || string.IsNullOrWhiteSpace(root.name))
            {
                return false;
            }

            string name = root.name;
            int digitStart = -1;
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsDigit(name[i]))
                {
                    digitStart = i;
                    break;
                }
            }

            return digitStart >= 0 && int.TryParse(name.Substring(digitStart), out wallId) && wallId > 0;
        }

        private static void RegisterWallDataIds(Transform root, int wallId, BuildContext context)
        {
            if (root == null)
            {
                return;
            }

            Wall rootWall = root.GetComponent<Wall>();
            if (rootWall != null)
            {
                RegisterWallDataId(rootWall.Data, wallId, context);
                return;
            }

            Wall[] childWalls = root.GetComponentsInChildren<Wall>(true);
            for (int i = 0; i < childWalls.Length; i++)
            {
                Wall childWall = childWalls[i];
                if (childWall == null || childWall.transform.parent != root)
                {
                    continue;
                }

                RegisterWallDataId(childWall.Data, wallId, context);
            }
        }

        private static void RegisterWallDataId(WallData wallData, int wallId, BuildContext context)
        {
            if (wallData == null || string.IsNullOrWhiteSpace(wallData.id))
            {
                return;
            }

            context.wallIdsByDataId[wallData.id] = wallId;
            context.wallDataById[wallData.id] = wallData;
        }

        private static float GetCeilingWorldY(RoomData roomData, BuildContext context)
        {
            float bestY = roomData != null ? roomData.Geometry.Center.y : 0f;
            if (roomData == null)
            {
                return bestY;
            }

            IReadOnlyList<string> wallIds = roomData.WallIds;
            for (int i = 0; i < wallIds.Count; i++)
            {
                string wallId = wallIds[i];
                if (string.IsNullOrWhiteSpace(wallId) || !context.wallDataById.TryGetValue(wallId, out WallData wallData))
                {
                    continue;
                }

                float wallTopY = wallData.centerY + wallData.height * 0.5f;
                if (wallTopY > bestY)
                {
                    bestY = wallTopY;
                }
            }

            return bestY;
        }

        private static bool IsLegacyRectSurface(IReadOnlyList<Vector3> boundaryVertices)
        {
            if (boundaryVertices == null)
            {
                return false;
            }

            List<Vector3> sanitized = PolygonUtility.CreateSanitizedPolygonCopy(boundaryVertices);
            if (sanitized.Count != 4)
            {
                return false;
            }

            for (int i = 0; i < sanitized.Count; i++)
            {
                Vector3 current = sanitized[i];
                Vector3 next = sanitized[(i + 1) % sanitized.Count];
                Vector3 edge = next - current;
                edge.y = 0f;
                if (edge.sqrMagnitude <= 0.000001f)
                {
                    return false;
                }

                bool axisAlignedX = Mathf.Abs(edge.x) > 0.0001f && Mathf.Abs(edge.z) <= 0.0001f;
                bool axisAlignedZ = Mathf.Abs(edge.z) > 0.0001f && Mathf.Abs(edge.x) <= 0.0001f;
                if (!axisAlignedX && !axisAlignedZ)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
