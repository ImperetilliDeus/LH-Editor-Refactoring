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
                for (int i = 0; i < childWalls.Length; i++)
                {
                    Wall segmentWall = childWalls[i];
                    if (segmentWall == null || segmentWall.transform.parent != root)
                    {
                        continue;
                    }

                    segments.Add(BuildSegmentForContainer(root, segmentWall, container));
                }
            }
            else if (root.TryGetComponent(out Wall wall))
            {
                segments.Add(BuildStandaloneSegment(wall));
            }

            return new LhWallDto
            {
                name = root.name,
                id = wallId,
                transform = LhDtoFactory.CreateTransform(root, false),
                segments = segments,
            };
        }

        private static LhWallSegmentDto BuildStandaloneSegment(Wall wall)
        {
            return new LhWallSegmentDto
            {
                transform = new LhTransformDto
                {
                    position = LhVector3Dto.FromVector3(Vector3.zero),
                    angle = LhVector3Dto.FromVector3(Vector3.zero),
                    scale = LhVector3Dto.FromVector3(Vector3.one),
                },
                hasInterior = false,
                door = null,
                window = null,
            };
        }

        private static LhWallSegmentDto BuildSegmentForContainer(Transform root, Wall segmentWall, WallOpeningContainer container)
        {
            WallOpening attachedOpening = FindOpeningForSegment(segmentWall, container);
            bool hasInterior = attachedOpening != null;

            return new LhWallSegmentDto
            {
                transform = LhDtoFactory.CreateTransform(segmentWall.transform, true),
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
            return new LhDoorDto
            {
                isExist = true,
                code = string.IsNullOrWhiteSpace(opening.DoorTypeKey) ? "Door" : opening.DoorTypeKey,
                transform = CreateRelativeTransform(root, opening.transform),
            };
        }

        private static LhWindowDto BuildWindow(WallOpening opening, Transform root)
        {
            return new LhWindowDto
            {
                isExist = true,
                code = string.IsNullOrWhiteSpace(opening.WindowTypeKey) ? "Window" : opening.WindowTypeKey,
                transform = CreateRelativeTransform(root, opening.transform),
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

            return new LhRoomDto
            {
                name = room.name,
                code = roomData.RoomCode ?? string.Empty,
                transform = CreateTransform(roomPosition, Vector3.zero, Vector3.one),
                walls = BuildRoomWallReferences(roomData, room.WallSet, context),
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

                results.Add(new LhFurnitureDto
                {
                    name = instance.gameObject.name,
                    code = instance.CatalogCode ?? string.Empty,
                    transform = CreateRelativeTransform(room.transform, instance.transform),
                });
            }

            return results;
        }

        private static List<int> BuildRoomWallReferences(RoomData roomData, IEnumerable<Wall> walls, BuildContext context)
        {
            List<int> results = new List<int>();
            if (roomData == null)
            {
                return results;
            }

            HashSet<int> addedIds = new HashSet<int>();
            IReadOnlyList<string> wallIds = roomData.WallIds;
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

        private static LhSurfaceDto BuildSurface(RoomData roomData, Vector3 roomPosition, float worldY, string explicitTextureCode)
        {
            IReadOnlyList<Vector3> boundaryVertices = roomData != null ? roomData.BoundaryVertices : null;
            RoomGeometry geometry = roomData != null ? roomData.Geometry : default;
            string textureCode = explicitTextureCode ?? string.Empty;

            return new LhSurfaceDto
            {
                transform = CreateTransform(
                    new Vector3(0f, worldY - roomPosition.y, 0f),
                    Vector3.zero,
                    Vector3.one),
                meshType = boundaryVertices != null && boundaryVertices.Count >= 3 ? 1 : 0,
                mesh = LhDtoFactory.CreateMeshFromPolygon(boundaryVertices, geometry.Center),
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

        private static LhTransformDto CreateRelativeTransform(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return default;
            }

            return new LhTransformDto
            {
                position = LhVector3Dto.FromVector3(root.InverseTransformPoint(target.position)),
                angle = LhVector3Dto.FromVector3((Quaternion.Inverse(root.rotation) * target.rotation).eulerAngles),
                scale = LhVector3Dto.FromVector3(target.localScale),
            };
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

        private static LhTransformDto CreateTransform(Vector3 position, Vector3 angle, Vector3 scale)
        {
            return new LhTransformDto
            {
                position = LhVector3Dto.FromVector3(position),
                angle = LhVector3Dto.FromVector3(angle),
                scale = LhVector3Dto.FromVector3(scale),
            };
        }
    }
}
