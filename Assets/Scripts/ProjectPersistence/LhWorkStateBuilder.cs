using System.Collections.Generic;
using UnityEngine;

public static class LhWorkStateBuilder
{
    private static readonly List<Wall> CachedWalls = new List<Wall>();

    public static LhWorkStateDto Build(Transform wallRoot, RoomManager roomManager, Transform furnitureRoot)
    {
        LhWorkStateDto state = LhWorkStateDto.CreateEmpty();
        state.walls = BuildWalls(wallRoot);
        state.rooms = BuildRooms(roomManager);
        state.furniture = BuildFurniture(furnitureRoot);
        return state;
    }

    private static List<LhWorkWallDto> BuildWalls(Transform wallRoot)
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

            results.Add(container != null ? BuildContainerWall(container, wall) : BuildStandaloneWall(wall));
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

    private static LhWorkWallDto BuildContainerWall(WallOpeningContainer container, Wall representativeWall)
    {
        return new LhWorkWallDto
        {
            id = representativeWall != null && representativeWall.Data != null
                ? representativeWall.Data.id ?? string.Empty
                : string.Empty,
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

    private static List<LhWorkRoomDto> BuildRooms(RoomManager roomManager)
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
                wallIds = ToStringList(data != null ? data.WallIds : null),
                manualWallSelectionEnabled = data != null && data.ManualWallSelectionEnabled,
                manualWallIds = ToStringList(data != null ? data.ManualWallIds : null),
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

    private static List<string> ToStringList(IReadOnlyList<string> values)
    {
        List<string> results = new List<string>();
        if (values == null)
        {
            return results;
        }

        for (int i = 0; i < values.Count; i++)
        {
            results.Add(values[i] ?? string.Empty);
        }

        return results;
    }

    private static bool IsPreviewWall(Transform root)
    {
        return root != null &&
               string.Equals(root.name, "WallPreview", System.StringComparison.OrdinalIgnoreCase);
    }
}
