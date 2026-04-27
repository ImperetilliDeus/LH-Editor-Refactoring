using System.Collections.Generic;
using LH.Schema;
using UnityEngine;

namespace LH.Export
{
    public static class LhSceneExportBuilder
    {
        private const int CurrentSchemaVersion = 1;
        private const float FloorWorldY = 0.1f;

        private sealed class BuildContext
        {
            public readonly Dictionary<Transform, int> wallIdsByRoot = new Dictionary<Transform, int>();
            public readonly Dictionary<string, int> wallIdsByDataId = new Dictionary<string, int>();
            public readonly Dictionary<string, WallData> wallDataById = new Dictionary<string, WallData>();
            public int nextWallId = 1;
        }

        public static LhSceneDto Build(Vector3 startPoint, IEnumerable<Wall> walls, IEnumerable<Room> rooms)
        {
            BuildContext context = new BuildContext();
            List<LhWallDto> wallData = BuildWalls(walls, context);
            List<LhRoomDto> roomData = BuildRooms(rooms, context);

            return new LhSceneDto
            {
                version = CurrentSchemaVersion,
                startPoint = LhVector3Dto.FromVector3(startPoint),
                wallData = wallData,
                roomData = roomData,
            };
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

                int wallId = context.nextWallId++;
                context.wallIdsByRoot[root] = wallId;
                RegisterWallDataIds(root, wallId, context);
                results.Add(BuildWall(root, wallId));
            }

            return results;
        }

        private static LhWallDto BuildWall(Transform root, int wallId)
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
                    segments.Add(BuildSegmentForContainer(root, orderedSegments[i], container));
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

        private static LhWallSegmentDto BuildSegmentForContainer(Transform root, Wall segmentWall, WallOpeningContainer container)
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
                    ? BuildDoor(attachedOpening, root)
                    : null,
                window = attachedOpening != null && attachedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Window
                    ? BuildWindow(attachedOpening, root)
                    : null,
            };
        }

        private static LhDoorDto BuildDoor(WallOpening opening, Transform root)
        {
            FillRelativeTransform(root, opening.transform, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);

            return new LhDoorDto
            {
                isExist = true,
                code = string.IsNullOrWhiteSpace(opening.DoorTypeKey) ? "Door" : opening.DoorTypeKey,
                position = position,
                angle = angle,
                scale = scale,
            };
        }

        private static LhWindowDto BuildWindow(WallOpening opening, Transform root)
        {
            FillRelativeTransform(root, opening.transform, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);

            return new LhWindowDto
            {
                isExist = true,
                code = string.IsNullOrWhiteSpace(opening.WindowTypeKey) ? "Window" : opening.WindowTypeKey,
                position = position,
                angle = angle,
                scale = scale,
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
                code = roomData.RoomCode ?? string.Empty,
                roomTypeKey = roomData.RoomTypeKey ?? string.Empty,
                nativeCode = roomData.RoomNativeCode ?? string.Empty,
                position = position,
                angle = angle,
                scale = scale,
                walls = BuildRoomWallReferences(room, roomData, room.WallSet, context),
                floor = BuildSurface(roomData, roomPosition, FloorWorldY, roomData.FloorTextureCode),
                ceil = BuildSurface(
                    roomData,
                    roomPosition,
                    GetCeilingWorldY(roomData, context),
                    roomData.CeilingTextureCode),
                furnish = BuildFurniture(room),
            };
        }

        private static List<LhFurnitureDto> BuildFurniture(Room room)
        {
            List<LhFurnitureDto> results = new List<LhFurnitureDto>();
            if (room == null)
            {
                return results;
            }

            FurnitureInstance[] furnitureInstances = Object.FindObjectsByType<FurnitureInstance>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            for (int i = 0; i < furnitureInstances.Length; i++)
            {
                FurnitureInstance instance = furnitureInstances[i];
                if (instance == null || !instance.IsPlaced)
                {
                    continue;
                }

                if (instance.CurrentRoom != room)
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

        private static string BuildRoomId(Room room, RoomData roomData)
        {
            if (!string.IsNullOrWhiteSpace(roomData != null ? roomData.RoomCode : null))
            {
                return $"room_{roomData.RoomCode}";
            }

            if (room != null && !string.IsNullOrWhiteSpace(room.name))
            {
                return $"room_{SanitizeIdToken(room.name)}";
            }

            return "room";
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

        private static LhSurfaceDto BuildSurface(RoomData roomData, Vector3 roomPosition, float worldY, string explicitTextureCode)
        {
            IReadOnlyList<Vector3> boundaryVertices = roomData != null ? roomData.BoundaryVertices : null;
            string textureCode = explicitTextureCode ?? string.Empty;
            Vector3 localPosition = new Vector3(0f, worldY - roomPosition.y, 0f);
            Vector3 surfaceScale = CalculateSurfaceScale(boundaryVertices);
            LhDtoFactory.FillTransform(localPosition, Vector3.zero, surfaceScale, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);

            return new LhSurfaceDto
            {
                position = position,
                angle = angle,
                scale = scale,
                meshType = boundaryVertices != null && boundaryVertices.Count >= 3 ? 1 : 0,
                mesh = LhDtoFactory.CreateMeshFromPolygon(boundaryVertices, roomData != null ? roomData.Geometry.Center : Vector3.zero),
                texture = textureCode,
            };
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

    }
}
