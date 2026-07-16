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

public sealed class LhWorkStateLoadServices
{
    public HandleManager HandleManager { get; }
    public WallSelectionManager WallSelectionManager { get; }
    public WallLengthDisplay WallLengthDisplay { get; }
    public WallOpeningPlacementManager WallOpeningPlacementManager { get; }
    public FurniturePlacementManager FurniturePlacementManager { get; }
    public DrawManager DrawManager { get; }
    public DrawingOverlayManager DrawingOverlayManager { get; }

    public LhWorkStateLoadServices(
        HandleManager handleManager,
        WallLengthDisplay wallLengthDisplay,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        FurniturePlacementManager furniturePlacementManager,
        DrawManager drawManager = null)
        : this(
            handleManager,
            null,
            wallLengthDisplay,
            wallOpeningPlacementManager,
            furniturePlacementManager,
            drawManager,
            null)
    {
    }

    public LhWorkStateLoadServices(
        HandleManager handleManager,
        WallSelectionManager wallSelectionManager,
        WallLengthDisplay wallLengthDisplay,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        FurniturePlacementManager furniturePlacementManager,
        DrawManager drawManager,
        DrawingOverlayManager drawingOverlayManager)
    {
        HandleManager = handleManager;
        WallSelectionManager = wallSelectionManager;
        WallLengthDisplay = wallLengthDisplay;
        WallOpeningPlacementManager = wallOpeningPlacementManager;
        FurniturePlacementManager = furniturePlacementManager;
        DrawManager = drawManager;
        DrawingOverlayManager = drawingOverlayManager;
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
        return Load(state, wallRoot, roomManager, furnitureRoot, furnitureCatalog, null);
    }

    public static LhWorkStateLoadResult Load(
        LhWorkStateDto state,
        Transform wallRoot,
        RoomManager roomManager,
        Transform furnitureRoot,
        FurnitureCatalog furnitureCatalog,
        LhWorkStateLoadServices services)
    {
        LhWorkStateLoadResult validationResult = Validate(state, wallRoot, roomManager, furnitureRoot, furnitureCatalog);
        if (!validationResult.Success)
        {
            return validationResult;
        }

        services?.DrawingOverlayManager?.ClearOverlay();
        services?.WallSelectionManager?.ClearSelectionForSceneReset();
        services?.WallLengthDisplay?.ClearAllLabels();
        ClearChildren(wallRoot);
        if (roomManager != null)
        {
            roomManager.ClearAllRoomsForWorkStateLoad();
        }

        if (furnitureRoot != null)
        {
            ClearChildren(furnitureRoot);
        }

        NormalizeWallVertexIds(state.walls);
        if (!RestoreWalls(state.walls, wallRoot, services, out Dictionary<string, Wall> wallsById))
        {
            return LhWorkStateLoadResult.Fail("Failed to restore walls.");
        }

        WallNamingUtility.NormalizeWallNames(wallRoot);
        Dictionary<string, Room> roomsByName = RestoreRooms(state.rooms, roomManager, wallsById);
        RestoreFurniture(state.furniture, furnitureRoot, furnitureCatalog, roomsByName);
        if (roomManager != null)
        {
            roomManager.RebuildRoomLookupForWorkStateLoad();
        }

        RefreshRestoredEditorState(wallRoot, services);
        services?.DrawManager?.SyncWallSequenceForWorkStateLoad();
        RoomTopologyEvents.RequestRefreshAll();
        SceneHierarchyTreeView.RefreshAllInstances();
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

        LhWorkStateLoadResult roomResult = ValidateRooms(state.rooms, roomManager, state.walls);
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

    private static LhWorkStateLoadResult ValidateRooms(
        IReadOnlyList<LhWorkRoomDto> rooms,
        RoomManager roomManager,
        IReadOnlyList<LhWorkWallDto> walls)
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
        LhWorkStateLoadServices services,
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
            Wall wall = HasOpenings(wallDto)
                ? RestoreContainerWall(wallDto, wallRoot, services)
                : RestoreStandaloneWall(wallDto, wallRoot, services?.WallLengthDisplay, ResolveDefaultWallVisualState(services));
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

    private static Wall RestoreContainerWall(LhWorkWallDto wallDto, Transform wallRoot, LhWorkStateLoadServices services)
    {
        string wallName = string.IsNullOrWhiteSpace(wallDto.name) ? "Wall" : wallDto.name;
        GameObject containerObject = new GameObject(wallName);
        containerObject.transform.SetParent(wallRoot, false);
        LayerUtility.ApplyLayer(containerObject, LayerUtility.WallLayerName, false);

        WallOpeningContainer container = containerObject.AddComponent<WallOpeningContainer>();
        container.Initialize(
            wallDto.start.ToVector3(),
            wallDto.end.ToVector3(),
            wallDto.thickness,
            wallDto.height,
            wallDto.centerY,
            ResolveDefaultWallVisualState(services),
            wallDto.startVertexId,
            wallDto.endVertexId,
            wallDto.suppressStartHandle,
            wallDto.suppressEndHandle,
            wallDto.startSplitPoint,
            wallDto.endSplitPoint);
        container.SetPersistentWallId(wallDto.id);

        for (int i = 0; i < wallDto.openings.Count; i++)
        {
            RestoreOpening(container, wallDto.openings[i], services?.WallOpeningPlacementManager);
        }

        LhWorkWallDto baseWallDto = new LhWorkWallDto
        {
            id = wallDto.id,
            name = wallName + "_Base",
            start = wallDto.start,
            end = wallDto.end,
            thickness = wallDto.thickness,
            height = wallDto.height,
            centerY = wallDto.centerY,
            textureCode = wallDto.textureCode ?? string.Empty,
            startVertexId = wallDto.startVertexId,
            endVertexId = wallDto.endVertexId,
            suppressStartHandle = wallDto.suppressStartHandle,
            suppressEndHandle = wallDto.suppressEndHandle,
            startSplitPoint = wallDto.startSplitPoint,
            endSplitPoint = wallDto.endSplitPoint,
            openings = new List<LhWorkOpeningDto>(),
        };

        Wall wall = RestoreStandaloneWall(
            baseWallDto,
            container.transform,
            services?.WallLengthDisplay,
            ResolveDefaultWallVisualState(services));
        if (services?.WallOpeningPlacementManager != null)
        {
            services.WallOpeningPlacementManager.RebuildOpeningContainer(container);
            services.WallOpeningPlacementManager.MarkMarkerVisualsDirty();
            wall = ResolveRepresentativeWall(container, wallDto.id);
        }

        return wall;
    }

    private static Wall RestoreStandaloneWall(
        LhWorkWallDto wallDto,
        Transform wallRoot,
        WallLengthDisplay wallLengthDisplay,
        WallVisualState visualState)
    {
        string wallName = string.IsNullOrWhiteSpace(wallDto.name) ? "Wall" : wallDto.name;
        GameObject wallObject = WallObjectFactory.CreateWallObject(wallName, wallRoot, null, visualState);
        WallData wallData = new WallData(
            wallDto.start.ToVector3(),
            wallDto.end.ToVector3(),
            wallDto.thickness,
            wallDto.height,
            wallDto.centerY)
        {
            id = wallDto.id ?? string.Empty,
            TextureCode = wallDto.textureCode ?? string.Empty,
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
            wallLengthDisplay,
            false);

        if (!configured)
        {
            DestroyObject(wallObject);
            return null;
        }

        return wallObject.GetComponent<Wall>();
    }

    private static WallVisualState ResolveDefaultWallVisualState(LhWorkStateLoadServices services)
    {
        DrawManager drawManager = services?.DrawManager;
        return new WallVisualState
        {
            wallMaterial = drawManager != null ? drawManager.WallMaterial : null,
            topMaterial = drawManager != null ? drawManager.WallTopMaterial : null,
        };
    }

    private static void RestoreOpening(
        WallOpeningContainer container,
        LhWorkOpeningDto openingDto,
        WallOpeningPlacementManager wallOpeningPlacementManager)
    {
        if (container == null || openingDto == null)
        {
            return;
        }

        WallOpeningPlacementManager.OpeningPlacementType openingType = ResolveOpeningType(openingDto.type);
        GameObject openingObject = new GameObject(openingType == WallOpeningPlacementManager.OpeningPlacementType.Door ? "Door" : "Window");
        openingObject.transform.SetParent(container.transform, false);
        LayerUtility.ApplyLayer(
            openingObject,
            openingType == WallOpeningPlacementManager.OpeningPlacementType.Door
                ? LayerUtility.DoorLayerName
                : LayerUtility.WindowLayerName,
            false);

        WallOpening opening = openingObject.AddComponent<WallOpening>();
        string doorTypeKey = ResolveOpeningTypeKey(openingType, openingDto, true);
        string windowTypeKey = ResolveOpeningTypeKey(openingType, openingDto, false);
        opening.Initialize(
            wallOpeningPlacementManager,
            container,
            openingType,
            doorTypeKey,
            windowTypeKey,
            openingDto.doorOpensRight,
            openingDto.doorVerticalFlip,
            openingDto.centerDistance,
            openingDto.width,
            openingDto.height,
            openingDto.depth,
            openingDto.bottomY);
    }

    private static string ResolveOpeningTypeKey(
        WallOpeningPlacementManager.OpeningPlacementType openingType,
        LhWorkOpeningDto openingDto,
        bool doorKey)
    {
        if (openingDto == null)
        {
            return string.Empty;
        }

        if (doorKey && openingType == WallOpeningPlacementManager.OpeningPlacementType.Door)
        {
            return !string.IsNullOrWhiteSpace(openingDto.prefabKey)
                ? openingDto.prefabKey
                : openingDto.doorTypeKey ?? string.Empty;
        }

        if (!doorKey && openingType == WallOpeningPlacementManager.OpeningPlacementType.Window)
        {
            return !string.IsNullOrWhiteSpace(openingDto.prefabKey)
                ? openingDto.prefabKey
                : openingDto.windowTypeKey ?? string.Empty;
        }

        return string.Empty;
    }

    private static Wall ResolveRepresentativeWall(WallOpeningContainer container, string preferredWallId)
    {
        if (container == null)
        {
            return null;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        if (walls == null || walls.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(preferredWallId))
        {
            for (int i = 0; i < walls.Length; i++)
            {
                Wall wall = walls[i];
                if (wall != null && wall.Data != null && string.Equals(wall.Data.id, preferredWallId, System.StringComparison.Ordinal))
                {
                    return wall;
                }
            }

            if (walls[0] != null && walls[0].Data != null)
            {
                walls[0].Data.id = preferredWallId;
            }
        }

        return walls[0];
    }

    private static WallOpeningPlacementManager.OpeningPlacementType ResolveOpeningType(string value)
    {
        return string.Equals(value, WallOpeningPlacementManager.OpeningPlacementType.Window.ToString(), System.StringComparison.Ordinal)
            ? WallOpeningPlacementManager.OpeningPlacementType.Window
            : WallOpeningPlacementManager.OpeningPlacementType.Door;
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
            roomManager.UpdateRoomWallSelection(
                room,
                FilterResolvableWallIds(roomDto.manualWallIds, wallsById),
                roomDto.manualWallSelectionEnabled);
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

    private static List<string> FilterResolvableWallIds(IReadOnlyList<string> wallIds, Dictionary<string, Wall> wallsById)
    {
        List<string> results = new List<string>();
        if (wallIds == null || wallsById == null)
        {
            return results;
        }

        HashSet<string> seenIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = 0; i < wallIds.Count; i++)
        {
            string wallId = wallIds[i];
            if (!string.IsNullOrWhiteSpace(wallId) &&
                wallsById.ContainsKey(wallId) &&
                seenIds.Add(wallId))
            {
                results.Add(wallId);
            }
        }

        return results;
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

    private static bool HasOpenings(LhWorkWallDto wallDto)
    {
        return wallDto != null && wallDto.openings != null && wallDto.openings.Count > 0;
    }

    private static void NormalizeWallVertexIds(IReadOnlyList<LhWorkWallDto> walls)
    {
        if (walls == null || walls.Count == 0)
        {
            return;
        }

        Dictionary<VertexCoordinateKey, int> canonicalIdsByCoordinate = new Dictionary<VertexCoordinateKey, int>();
        for (int i = 0; i < walls.Count; i++)
        {
            LhWorkWallDto wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            RegisterCanonicalVertexId(wall.start.ToVector3(), wall.startVertexId, canonicalIdsByCoordinate);
            RegisterCanonicalVertexId(wall.end.ToVector3(), wall.endVertexId, canonicalIdsByCoordinate);
        }

        for (int i = 0; i < walls.Count; i++)
        {
            LhWorkWallDto wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            wall.startVertexId = ResolveCanonicalVertexId(wall.start.ToVector3(), wall.startVertexId, canonicalIdsByCoordinate);
            wall.endVertexId = ResolveCanonicalVertexId(wall.end.ToVector3(), wall.endVertexId, canonicalIdsByCoordinate);
        }
    }

    private static void RegisterCanonicalVertexId(
        Vector3 point,
        int vertexId,
        Dictionary<VertexCoordinateKey, int> canonicalIdsByCoordinate)
    {
        if (canonicalIdsByCoordinate == null || vertexId <= 0)
        {
            return;
        }

        VertexCoordinateKey key = VertexCoordinateKey.From(point);
        if (!canonicalIdsByCoordinate.ContainsKey(key))
        {
            canonicalIdsByCoordinate[key] = vertexId;
        }
    }

    private static int ResolveCanonicalVertexId(
        Vector3 point,
        int vertexId,
        Dictionary<VertexCoordinateKey, int> canonicalIdsByCoordinate)
    {
        if (canonicalIdsByCoordinate == null)
        {
            return vertexId;
        }

        return canonicalIdsByCoordinate.TryGetValue(VertexCoordinateKey.From(point), out int canonicalId)
            ? canonicalId
            : vertexId;
    }

    private readonly struct VertexCoordinateKey
    {
        private const float Precision = 10000f;

        private readonly int x;
        private readonly int y;
        private readonly int z;

        private VertexCoordinateKey(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static VertexCoordinateKey From(Vector3 point)
        {
            return new VertexCoordinateKey(
                Mathf.RoundToInt(point.x * Precision),
                Mathf.RoundToInt(point.y * Precision),
                Mathf.RoundToInt(point.z * Precision));
        }
    }

    private static void RefreshRestoredEditorState(Transform wallRoot, LhWorkStateLoadServices services)
    {
        if (services == null)
        {
            return;
        }

        if (services.WallLengthDisplay != null && wallRoot != null)
        {
            List<Wall> walls = new List<Wall>();
            WallHierarchyUtility.CollectWalls(wallRoot, walls, true);
            for (int i = 0; i < walls.Count; i++)
            {
                Wall wall = walls[i];
                if (wall != null && !WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
                {
                    wall.RefreshLengthDisplay(services.WallLengthDisplay, false);
                }
            }
        }

        services.HandleManager?.RebuildRegisteredWallsFromHierarchy();
        services.WallOpeningPlacementManager?.RefreshRestoredOpeningVisuals();
        services.FurniturePlacementManager?.RefreshRestoredFurniture();
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
