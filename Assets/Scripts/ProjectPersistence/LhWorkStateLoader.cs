using System.Collections.Generic;
using UnityEngine;

public sealed class LhWorkStateLoadResult
{
    public bool Success { get; }
    public string Message { get; }

    private LhWorkStateLoadResult(bool success, string message)
    {
        Success = success;
        Message = message ?? string.Empty;
    }

    public static LhWorkStateLoadResult Ok()
    {
        return new LhWorkStateLoadResult(true, string.Empty);
    }

    public static LhWorkStateLoadResult Fail(string message)
    {
        return new LhWorkStateLoadResult(false, message);
    }
}

public static class LhWorkStateLoader
{
    private const float MinimumWallLength = 0.01f;

    public static LhWorkStateLoadResult Load(
        LhWorkStateDto state,
        Transform wallRoot,
        RoomManager roomManager,
        Transform furnitureRoot,
        FurnitureCatalog furnitureCatalog)
    {
        LhWorkStateLoadResult validationResult = Validate(state, wallRoot, roomManager, furnitureRoot, furnitureCatalog);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        ClearChildren(wallRoot);
        if (roomManager != null)
        {
            roomManager.ClearAllRoomsForWorkStateLoad();
        }

        if (furnitureRoot != null)
        {
            ClearChildren(furnitureRoot);
        }

        if (!RestoreWalls(state.walls, wallRoot, out Dictionary<string, Wall> wallsById))
        {
            return LhWorkStateLoadResult.Fail("Failed to restore walls.");
        }

        Dictionary<string, Room> roomsByName = RestoreRooms(state.rooms, roomManager, wallsById);
        RestoreFurniture(state.furniture, furnitureRoot, furnitureCatalog, roomsByName);
        if (roomManager != null)
        {
            roomManager.RebuildRoomLookupForWorkStateLoad();
        }

        RoomTopologyEvents.RequestRefreshForWallReplacement(System.Array.Empty<Wall>(), wallsById.Values);
        return LhWorkStateLoadResult.Ok();
    }

    private static LhWorkStateLoadResult Validate(
        LhWorkStateDto state,
        Transform wallRoot,
        RoomManager roomManager,
        Transform furnitureRoot,
        FurnitureCatalog furnitureCatalog)
    {
        if (state == null)
        {
            return LhWorkStateLoadResult.Fail("Work state is missing.");
        }

        if (!LhWorkStateDto.IsSupportedVersion(state.version))
        {
            return LhWorkStateLoadResult.Fail($"Unsupported work state version: {state.version}.");
        }

        if (wallRoot == null)
        {
            return LhWorkStateLoadResult.Fail("Wall root is missing.");
        }

        LhWorkStateLoadResult wallResult = ValidateWalls(state.walls);
        if (!wallResult.Success)
        {
            return wallResult;
        }

        LhWorkStateLoadResult roomResult = ValidateRooms(state.rooms, roomManager);
        if (!roomResult.Success)
        {
            return roomResult;
        }

        LhWorkStateLoadResult furnitureResult = ValidateFurniture(state.furniture, furnitureRoot, furnitureCatalog);
        if (!furnitureResult.Success)
        {
            return furnitureResult;
        }

        return LhWorkStateLoadResult.Ok();
    }

    private static LhWorkStateLoadResult ValidateWalls(IReadOnlyList<LhWorkWallDto> walls)
    {
        if (walls == null)
        {
            return LhWorkStateLoadResult.Ok();
        }

        for (int i = 0; i < walls.Count; i++)
        {
            LhWorkWallDto wallDto = walls[i];
            if (wallDto == null)
            {
                return LhWorkStateLoadResult.Fail($"Wall #{i} is missing.");
            }

            if (!HasValidWallGeometry(wallDto))
            {
                return LhWorkStateLoadResult.Fail($"Wall #{i} has invalid geometry.");
            }
        }

        return LhWorkStateLoadResult.Ok();
    }

    private static LhWorkStateLoadResult ValidateRooms(IReadOnlyList<LhWorkRoomDto> rooms, RoomManager roomManager)
    {
        if (rooms == null || rooms.Count == 0)
        {
            return LhWorkStateLoadResult.Ok();
        }

        if (roomManager == null)
        {
            return LhWorkStateLoadResult.Fail("Room manager is required to load rooms.");
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            LhWorkRoomDto roomDto = rooms[i];
            if (roomDto == null)
            {
                return LhWorkStateLoadResult.Fail($"Room #{i} is missing.");
            }

            List<Vector3> vertices = ToVectors(roomDto.boundaryVertices);
            if (!RoomPolygonValidationUtility.IsValidPolygon(vertices))
            {
                return LhWorkStateLoadResult.Fail($"Room #{i} has invalid polygon.");
            }
        }

        return LhWorkStateLoadResult.Ok();
    }

    private static LhWorkStateLoadResult ValidateFurniture(
        IReadOnlyList<LhWorkFurnitureDto> furniture,
        Transform furnitureRoot,
        FurnitureCatalog furnitureCatalog)
    {
        if (furniture == null || furniture.Count == 0)
        {
            return LhWorkStateLoadResult.Ok();
        }

        if (furnitureRoot == null)
        {
            return LhWorkStateLoadResult.Fail("Furniture root is required to load furniture.");
        }

        if (furnitureCatalog == null)
        {
            return LhWorkStateLoadResult.Fail("Furniture catalog is required to load furniture.");
        }

        for (int i = 0; i < furniture.Count; i++)
        {
            LhWorkFurnitureDto furnitureDto = furniture[i];
            if (furnitureDto == null)
            {
                return LhWorkStateLoadResult.Fail($"Furniture #{i} is missing.");
            }

            FurnitureCatalogItem item = ResolveFurnitureItem(furnitureCatalog, furnitureDto);
            if (item == null || item.prefab == null)
            {
                return LhWorkStateLoadResult.Fail($"Furniture #{i} cannot resolve a prefab.");
            }
        }

        return LhWorkStateLoadResult.Ok();
    }

    private static bool RestoreWalls(
        IReadOnlyList<LhWorkWallDto> walls,
        Transform wallRoot,
        out Dictionary<string, Wall> wallsById)
    {
        wallsById = new Dictionary<string, Wall>(System.StringComparer.Ordinal);
        if (walls == null)
        {
            return true;
        }

        for (int i = 0; i < walls.Count; i++)
        {
            LhWorkWallDto wallDto = walls[i];
            Wall wall = RestoreStandaloneWall(wallDto, wallRoot);
            if (wall == null)
            {
                return false;
            }

            if (wall != null && wall.Data != null && !string.IsNullOrWhiteSpace(wall.Data.id))
            {
                wallsById[wall.Data.id] = wall;
            }
        }

        return true;
    }

    private static Wall RestoreStandaloneWall(LhWorkWallDto wallDto, Transform wallRoot)
    {
        string wallName = string.IsNullOrWhiteSpace(wallDto.name) ? "Wall" : wallDto.name;
        GameObject wallObject = WallObjectFactory.CreateWallObject(wallName, wallRoot, null, new WallVisualState());
        WallData wallData = new WallData(
            wallDto.start.ToVector3(),
            wallDto.end.ToVector3(),
            wallDto.thickness,
            wallDto.height,
            wallDto.centerY)
        {
            id = wallDto.id ?? string.Empty,
        };

        bool configured = WallObjectFactory.ConfigureWall(
            wallObject,
            wallData,
            wallDto.startVertexId,
            wallDto.endVertexId,
            wallDto.suppressStartHandle,
            wallDto.suppressEndHandle,
            wallDto.startSplitPoint,
            wallDto.endSplitPoint,
            MinimumWallLength,
            null,
            false);

        if (!configured)
        {
            DestroyObject(wallObject);
            return null;
        }

        return wallObject.GetComponent<Wall>();
    }

    private static Dictionary<string, Room> RestoreRooms(
        IReadOnlyList<LhWorkRoomDto> rooms,
        RoomManager roomManager,
        Dictionary<string, Wall> wallsById)
    {
        Dictionary<string, Room> roomsByName = new Dictionary<string, Room>(System.StringComparer.Ordinal);
        if (rooms == null || roomManager == null)
        {
            return roomsByName;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            LhWorkRoomDto roomDto = rooms[i];
            List<Vector3> vertices = ToVectors(roomDto.boundaryVertices);
            Room room = roomManager.CreateRoomForWorkStateLoad(vertices, ResolveWallSet(roomDto.wallIds, wallsById), roomDto.isManualRoom);
            if (room == null)
            {
                continue;
            }

            room.SetPlacementOffset(roomDto.placementOffset.ToVector3());
            roomManager.UpdateRoomMetadata(
                room,
                roomDto.name,
                roomDto.roomTypeKey,
                roomDto.roomCode,
                roomDto.roomNativeCode,
                roomDto.floorTextureCode,
                roomDto.ceilingTextureCode);
            roomManager.UpdateRoomWallSelection(room, roomDto.manualWallIds, roomDto.manualWallSelectionEnabled);
            AddRoomAlias(roomsByName, roomDto.name, room);
            AddRoomAlias(roomsByName, room.RoomName, room);
            AddRoomAlias(roomsByName, room.name, room);
        }

        return roomsByName;
    }

    private static void RestoreFurniture(
        IReadOnlyList<LhWorkFurnitureDto> furniture,
        Transform furnitureRoot,
        FurnitureCatalog furnitureCatalog,
        Dictionary<string, Room> roomsByName)
    {
        if (furniture == null || furnitureRoot == null || furnitureCatalog == null)
        {
            return;
        }

        for (int i = 0; i < furniture.Count; i++)
        {
            LhWorkFurnitureDto furnitureDto = furniture[i];
            if (furnitureDto == null)
            {
                continue;
            }

            FurnitureCatalogItem item = ResolveFurnitureItem(furnitureCatalog, furnitureDto);
            if (item == null || item.prefab == null)
            {
                continue;
            }

            GameObject furnitureObject = Object.Instantiate(item.prefab, furnitureRoot);
            furnitureObject.name = string.IsNullOrWhiteSpace(furnitureDto.name) ? item.prefab.name : furnitureDto.name;
            furnitureObject.transform.position = furnitureDto.position.ToVector3();
            furnitureObject.transform.eulerAngles = furnitureDto.eulerAngles.ToVector3();
            furnitureObject.transform.localScale = furnitureDto.localScale.ToVector3();

            FurnitureInstance instance = furnitureObject.GetComponent<FurnitureInstance>();
            if (instance == null)
            {
                instance = furnitureObject.AddComponent<FurnitureInstance>();
            }

            instance.Initialize(item);
            instance.SetPlaced(furnitureDto.isPlaced);
            if (TryResolveRoom(roomsByName, furnitureDto.roomName, out Room room))
            {
                instance.SetCurrentRoom(room);
            }

            instance.ApplyLayerRecursively();
        }
    }

    private static FurnitureCatalogItem ResolveFurnitureItem(FurnitureCatalog catalog, LhWorkFurnitureDto furnitureDto)
    {
        if (catalog == null || furnitureDto == null)
        {
            return null;
        }

        IReadOnlyList<FurnitureCatalogItem> items = catalog.Items;
        for (int i = 0; i < items.Count; i++)
        {
            FurnitureCatalogItem item = items[i];
            if (item == null)
            {
                continue;
            }

            if (MatchesCode(item.code, furnitureDto.catalogCode) ||
                MatchesCode(item.exportCode, furnitureDto.exportCode) ||
                MatchesCode(item.nativeCode, furnitureDto.nativeCode))
            {
                return item;
            }
        }

        return null;
    }

    private static bool MatchesCode(string left, string right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, System.StringComparison.Ordinal);
    }

    private static HashSet<Wall> ResolveWallSet(IReadOnlyList<string> wallIds, Dictionary<string, Wall> wallsById)
    {
        HashSet<Wall> wallSet = new HashSet<Wall>();
        if (wallIds == null || wallsById == null)
        {
            return wallSet;
        }

        for (int i = 0; i < wallIds.Count; i++)
        {
            string wallId = wallIds[i];
            if (!string.IsNullOrWhiteSpace(wallId) && wallsById.TryGetValue(wallId, out Wall wall) && wall != null)
            {
                wallSet.Add(wall);
            }
        }

        return wallSet;
    }

    private static void AddRoomAlias(Dictionary<string, Room> roomsByName, string roomName, Room room)
    {
        if (roomsByName == null || string.IsNullOrWhiteSpace(roomName) || room == null)
        {
            return;
        }

        roomsByName[roomName] = room;
    }

    private static bool TryResolveRoom(Dictionary<string, Room> roomsByName, string roomName, out Room room)
    {
        room = null;
        return roomsByName != null &&
               !string.IsNullOrWhiteSpace(roomName) &&
               roomsByName.TryGetValue(roomName, out room);
    }

    private static bool HasValidWallGeometry(LhWorkWallDto wallDto)
    {
        if (wallDto == null)
        {
            return false;
        }

        Vector3 delta = wallDto.end.ToVector3() - wallDto.start.ToVector3();
        delta.y = 0f;
        return delta.magnitude >= MinimumWallLength;
    }

    private static List<Vector3> ToVectors(IReadOnlyList<LhWorkVector3Dto> values)
    {
        List<Vector3> results = new List<Vector3>();
        if (values == null)
        {
            return results;
        }

        for (int i = 0; i < values.Count; i++)
        {
            results.Add(values[i].ToVector3());
        }

        return results;
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child != null)
            {
                DestroyObject(child.gameObject);
            }
        }
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            if (target is GameObject gameObject)
            {
                gameObject.SetActive(false);
            }

            Object.Destroy(target);
        }
        else
        {
            Object.DestroyImmediate(target);
        }
    }
}
