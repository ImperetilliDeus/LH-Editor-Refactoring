using System.Collections.Generic;
using LH.Schema;
using UnityEngine;

namespace LH.Export
{
    public static class LhSceneExportBuilder
    {
        private const int CurrentSchemaVersion = 1;

        private sealed class BuildContext
        {
            public readonly Dictionary<Transform, int> wallIdsByRoot = new Dictionary<Transform, int>();
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
            Transform floor = room.transform.Find("Floor");
            Transform ceil = room.transform.Find("Ceiling");

            return new LhRoomDto
            {
                name = room.name,
                code = room.RoomCode ?? string.Empty,
                transform = LhDtoFactory.CreateTransform(room.transform, false),
                walls = BuildRoomWallReferences(room, context),
                floor = BuildSurface(floor, room.FloorTextureCode),
                ceil = BuildSurface(ceil, room.CeilingTextureCode),
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

        private static List<int> BuildRoomWallReferences(Room room, BuildContext context)
        {
            List<int> results = new List<int>();
            if (room == null || room.WallSet == null)
            {
                return results;
            }

            HashSet<int> addedIds = new HashSet<int>();
            foreach (Wall wall in room.WallSet)
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

        private static LhSurfaceDto BuildSurface(Transform surfaceTransform, string explicitTextureCode)
        {
            MeshFilter meshFilter = surfaceTransform != null ? surfaceTransform.GetComponent<MeshFilter>() : null;
            MeshRenderer meshRenderer = surfaceTransform != null ? surfaceTransform.GetComponent<MeshRenderer>() : null;
            Mesh mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            string textureCode = string.IsNullOrWhiteSpace(explicitTextureCode)
                ? ResolveMaterialCode(meshRenderer)
                : explicitTextureCode;

            return new LhSurfaceDto
            {
                transform = LhDtoFactory.CreateTransform(surfaceTransform, true),
                meshType = mesh != null ? 1 : 0,
                mesh = LhDtoFactory.CreateMesh(mesh),
                texture = textureCode,
            };
        }

        private static string ResolveMaterialCode(Renderer renderer)
        {
            if (renderer == null || renderer.sharedMaterial == null)
            {
                return string.Empty;
            }

            return renderer.sharedMaterial.name.Replace(" (Instance)", string.Empty);
        }

        private static WallOpening FindOpeningForSegment(Wall segmentWall, WallOpeningContainer container)
        {
            if (segmentWall == null || container == null)
            {
                return null;
            }

            float segmentMin = GetDistanceAlongContainer(container, segmentWall.StartPoint);
            float segmentMax = GetDistanceAlongContainer(container, segmentWall.EndPoint);
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
    }
}
