using System.Collections.Generic;
using UnityEngine;

public static class LhWorkStateBuilder
{
    private static readonly List<Wall> CachedWalls = new List<Wall>();

    public static LhWorkStateDto Build(Transform wallRoot, RoomManager roomManager, Transform furnitureRoot)
    {
        Dictionary<string, string> normalizedWallIdsBySourceId = new Dictionary<string, string>(System.StringComparer.Ordinal);
        LhWorkStateDto state = LhWorkStateDto.CreateEmpty();
        state.walls = BuildWalls(wallRoot, normalizedWallIdsBySourceId);
        state.rooms = BuildRooms(roomManager, normalizedWallIdsBySourceId);
        state.furniture = BuildFurniture(furnitureRoot);
        return state;
    }

    private static List<LhWorkWallDto> BuildWalls(Transform wallRoot, Dictionary<string, string> normalizedWallIdsBySourceId)
    {
        List<LhWorkWallDto> results = new List<LhWorkWallDto>();
        HashSet<Transform> exportedRoots = new HashSet<Transform>();

        WallHierarchyUtility.CollectWalls(wallRoot, CachedWalls, true);
        for (int i = 0; i < CachedWalls.Count; i++)
        {
            Wall wall = CachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
            Transform root = container != null ? container.transform : wall.transform;
            if (root == null || !exportedRoots.Add(root) || IsPreviewWall(root))
            {
                continue;
            }

            if (container != null)
            {
                string collapsedId = GetCollapsedContainerId(container);
                RegisterContainerWallIds(container, collapsedId, normalizedWallIdsBySourceId);
                results.Add(BuildContainerWall(container, collapsedId));
            }
            else
            {
                RegisterWallId(wall, GetWallId(wall), normalizedWallIdsBySourceId);
                results.Add(BuildStandaloneWall(wall));
            }
        }

        return results;
    }

    private static LhWorkWallDto BuildStandaloneWall(Wall wall)
    {
        WallData data = wall.Data;
        return new LhWorkWallDto
        {
            id = data != null ? data.id ?? string.Empty : string.Empty,
            name = wall.name ?? string.Empty,
            start = LhWorkVector3Dto.FromVector3(data != null ? data.startPoint : Vector3.zero),
            end = LhWorkVector3Dto.FromVector3(data != null ? data.endPoint : Vector3.zero),
            thickness = data != null ? data.thickness : 0f,
            height = data != null ? data.height : 0f,
            centerY = data != null ? data.centerY : 0f,
            startVertexId = wall.StartVertexId,
            endVertexId = wall.EndVertexId,
            suppressStartHandle = wall.SuppressStartHandle,
            suppressEndHandle = wall.SuppressEndHandle,
            startSplitPoint = wall.IsStartSplitPoint,
            endSplitPoint = wall.IsEndSplitPoint,
            openings = new List<LhWorkOpeningDto>(),
        };
    }

    private static LhWorkWallDto BuildContainerWall(WallOpeningContainer container, string collapsedId)
    {
        return new LhWorkWallDto
        {
            id = collapsedId ?? string.Empty,
            name = container.name ?? string.Empty,
            start = LhWorkVector3Dto.FromVector3(container.WallStart),
            end = LhWorkVector3Dto.FromVector3(container.WallEnd),
            thickness = container.WallThickness,
            height = container.WallHeight,
            centerY = container.CenterY,
            startVertexId = container.OuterStartVertexId,
            endVertexId = container.OuterEndVertexId,
            suppressStartHandle = container.SuppressOuterStartHandle,
            suppressEndHandle = container.SuppressOuterEndHandle,
            startSplitPoint = container.OuterStartSplitPoint,
            endSplitPoint = container.OuterEndSplitPoint,
            openings = BuildOpenings(container),
        };
    }

    private static List<LhWorkOpeningDto> BuildOpenings(WallOpeningContainer container)
    {
        List<LhWorkOpeningDto> results = new List<LhWorkOpeningDto>();
        if (container == null)
        {
            return results;
        }

        WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            results.Add(new LhWorkOpeningDto
            {
                type = opening.Type.ToString(),
                doorTypeKey = opening.DoorTypeKey ?? string.Empty,
                windowTypeKey = opening.WindowTypeKey ?? string.Empty,
                prefabKey = ResolveOpeningPrefabKey(opening),
                doorOpensRight = opening.DoorOpensRight,
                doorVerticalFlip = opening.DoorVerticalFlip,
                centerDistance = opening.CenterDistance,
                width = opening.Width,
                height = opening.Height,
                depth = opening.Depth,
                bottomY = opening.BottomY,
            });
        }

        results.Sort((left, right) => left.centerDistance.CompareTo(right.centerDistance));
        return results;
    }

    private static string ResolveOpeningPrefabKey(WallOpening opening)
    {
        if (opening == null)
        {
            return string.Empty;
        }

        return opening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? opening.DoorTypeKey ?? string.Empty
            : opening.WindowTypeKey ?? string.Empty;
    }

    private static List<LhWorkRoomDto> BuildRooms(RoomManager roomManager, Dictionary<string, string> normalizedWallIdsBySourceId)
    {
        List<LhWorkRoomDto> results = new List<LhWorkRoomDto>();
        if (roomManager == null)
        {
            return results;
        }

        List<Room> rooms = roomManager.GetAllRooms();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null)
            {
                continue;
            }

            RoomData data = room.Data;
            results.Add(new LhWorkRoomDto
            {
                name = !string.IsNullOrEmpty(room.RoomName) ? room.RoomName : room.name ?? string.Empty,
                roomTypeKey = room.RoomTypeKey ?? string.Empty,
                roomCode = room.RoomCode ?? string.Empty,
                roomNativeCode = room.RoomNativeCode ?? string.Empty,
                floorTextureCode = room.FloorTextureCode ?? string.Empty,
                ceilingTextureCode = room.CeilingTextureCode ?? string.Empty,
                isManualRoom = room.IsManualRoom,
                placementOffset = LhWorkVector3Dto.FromVector3(data != null ? data.PlacementOffset : Vector3.zero),
                boundaryVertices = ToVectorDtos(room.BoundaryVertices),
                wallIds = NormalizeWallIds(data != null ? data.WallIds : null, normalizedWallIdsBySourceId),
                manualWallSelectionEnabled = data != null && data.ManualWallSelectionEnabled,
                manualWallIds = NormalizeWallIds(data != null ? data.ManualWallIds : null, normalizedWallIdsBySourceId),
            });
        }

        return results;
    }

    private static List<LhWorkFurnitureDto> BuildFurniture(Transform furnitureRoot)
    {
        List<LhWorkFurnitureDto> results = new List<LhWorkFurnitureDto>();
        if (furnitureRoot == null)
        {
            return results;
        }

        FurnitureInstance[] instances = furnitureRoot.GetComponentsInChildren<FurnitureInstance>(true);
        for (int i = 0; i < instances.Length; i++)
        {
            FurnitureInstance instance = instances[i];
            if (instance == null || !instance.IsPlaced)
            {
                continue;
            }

            Room currentRoom = instance.CurrentRoom;
            results.Add(new LhWorkFurnitureDto
            {
                catalogCode = instance.CatalogCode ?? string.Empty,
                exportCode = instance.ExportCode ?? string.Empty,
                nativeCode = instance.NativeCode ?? string.Empty,
                name = instance.name ?? string.Empty,
                position = LhWorkVector3Dto.FromVector3(instance.transform.position),
                eulerAngles = LhWorkVector3Dto.FromVector3(instance.transform.eulerAngles),
                localScale = LhWorkVector3Dto.FromVector3(instance.transform.localScale),
                isPlaced = instance.IsPlaced,
                roomName = currentRoom != null
                    ? (!string.IsNullOrEmpty(currentRoom.RoomName) ? currentRoom.RoomName : currentRoom.name ?? string.Empty)
                    : string.Empty,
            });
        }

        return results;
    }

    private static List<LhWorkVector3Dto> ToVectorDtos(IReadOnlyList<Vector3> values)
    {
        List<LhWorkVector3Dto> results = new List<LhWorkVector3Dto>();
        if (values == null)
        {
            return results;
        }

        for (int i = 0; i < values.Count; i++)
        {
            results.Add(LhWorkVector3Dto.FromVector3(values[i]));
        }

        return results;
    }

    private static List<string> NormalizeWallIds(IReadOnlyList<string> values, Dictionary<string, string> normalizedWallIdsBySourceId)
    {
        List<string> results = new List<string>();
        if (values == null)
        {
            return results;
        }

        HashSet<string> seenIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            string sourceId = values[i] ?? string.Empty;
            if (normalizedWallIdsBySourceId == null ||
                !normalizedWallIdsBySourceId.TryGetValue(sourceId, out string normalizedId))
            {
                continue;
            }

            if (seenIds.Add(normalizedId))
            {
                results.Add(normalizedId);
            }
        }

        return results;
    }

    private static string GetCollapsedContainerId(WallOpeningContainer container)
    {
        if (container == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(container.PersistentWallId))
        {
            return container.PersistentWallId;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            string id = GetWallId(walls[i]);
            if (!string.IsNullOrEmpty(id))
            {
                return id;
            }
        }

        return container.name ?? string.Empty;
    }

    private static void RegisterContainerWallIds(
        WallOpeningContainer container,
        string collapsedId,
        Dictionary<string, string> normalizedWallIdsBySourceId)
    {
        if (container == null || normalizedWallIdsBySourceId == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(container.PersistentWallId))
        {
            normalizedWallIdsBySourceId[container.PersistentWallId] = collapsedId ?? string.Empty;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            RegisterWallId(walls[i], collapsedId, normalizedWallIdsBySourceId);
        }
    }

    private static void RegisterWallId(
        Wall wall,
        string normalizedId,
        Dictionary<string, string> normalizedWallIdsBySourceId)
    {
        if (wall == null || normalizedWallIdsBySourceId == null)
        {
            return;
        }

        string sourceId = GetWallId(wall);
        if (string.IsNullOrEmpty(sourceId))
        {
            return;
        }

        normalizedWallIdsBySourceId[sourceId] = normalizedId ?? string.Empty;
    }

    private static string GetWallId(Wall wall)
    {
        return wall != null && wall.Data != null ? wall.Data.id ?? string.Empty : string.Empty;
    }

    private static bool IsPreviewWall(Transform root)
    {
        return root != null &&
               string.Equals(root.name, "WallPreview", System.StringComparison.OrdinalIgnoreCase);
    }
}
