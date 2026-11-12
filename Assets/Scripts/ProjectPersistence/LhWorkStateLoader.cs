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
    private const float MinimumWallLength = GeometryTolerance.MinimumWallLength;

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

        WallRegistry.BeginChangeEventBatch();
        roomManager?.BeginRoomsChangedBatch();
        LhWorkStateLoadResult loadResult = LhWorkStateLoadResult.Fail("Work-state load did not complete.");
        bool refreshSceneHierarchy = false;
        try
        {
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

            Dictionary<string, string> mergedWallIds = NormalizeOverlappingOpeningWalls(state.walls);
            NormalizeRoomWallIds(state.rooms, mergedWallIds);
            NormalizeWallVertexIds(state.walls);
            LhWorkStateRoomUtility.NormalizeFurnitureRoomsBySavedPolygons(state, "LhWorkStateLoader");
            if (!RestoreWalls(state.walls, wallRoot, services, out Dictionary<string, Wall> wallsById))
            {
                loadResult = LhWorkStateLoadResult.Fail("Failed to restore walls.");
            }
            else
            {
                WallNamingUtility.NormalizeWallNames(wallRoot);
                Dictionary<string, Room> roomsByName = RestoreRooms(state.rooms, roomManager, wallsById);
                RestoreFurniture(state.furniture, furnitureRoot, furnitureCatalog, roomsByName, roomManager);
                if (roomManager != null)
                {
                    roomManager.RebuildRoomLookupForWorkStateLoad();
                }

                RefreshRestoredEditorState(wallRoot, services);
                services?.DrawManager?.SyncWallSequenceForWorkStateLoad();
                RoomTopologyEvents.RequestRefreshAll();
                refreshSceneHierarchy = true;
                loadResult = LhWorkStateLoadResult.Ok();
            }
        }
        finally
        {
            roomManager?.EndRoomsChangedBatch();
            WallRegistry.EndChangeEventBatch();
        }

        if (refreshSceneHierarchy)
        {
            SceneHierarchyTreeView.RefreshAllInstances();
        }

        return loadResult;
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

        Wall firstVisibleWall = null;
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall != null && !WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                firstVisibleWall = wall;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(preferredWallId))
        {
            for (int i = 0; i < walls.Length; i++)
            {
                Wall wall = walls[i];
                if (wall != null &&
                    wall.Data != null &&
                    !WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall) &&
                    string.Equals(wall.Data.id, preferredWallId, System.StringComparison.Ordinal))
                {
                    return wall;
                }
            }

            Wall targetWall = firstVisibleWall != null ? firstVisibleWall : walls[0];
            if (targetWall != null && targetWall.Data != null)
            {
                targetWall.Data.id = preferredWallId;
            }
        }

        return firstVisibleWall != null ? firstVisibleWall : walls[0];
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
        Dictionary<string, Room> roomsByName,
        RoomManager roomManager)
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
            instance.SetFurnishLocationCode(furnitureDto.furnishLocationCode);
            if (furnitureDto.defects != null)
            {
                instance.SetExportDefects(ToFurnitureDefects(furnitureDto.defects));
            }
            instance.SetPlaced(furnitureDto.isPlaced);
            Room resolvedRoom = ResolveFurnitureRoom(instance, roomManager);
            if (resolvedRoom == null && TryResolveRoom(roomsByName, furnitureDto.roomName, out Room namedRoom))
            {
                resolvedRoom = namedRoom;
            }

            if (resolvedRoom != null)
            {
                instance.SetCurrentRoom(resolvedRoom);
                ApplyRoomCodeToDefects(instance, resolvedRoom);
            }

            instance.ApplyLayerRecursively();
        }
    }

    private static Room ResolveFurnitureRoom(FurnitureInstance instance, RoomManager roomManager)
    {
        if (instance == null || roomManager == null)
        {
            return null;
        }

        List<Room> rooms = roomManager.GetAllRooms();
        Vector3 position = instance.transform.position;
        Room bestRoom = null;
        float bestArea = float.PositiveInfinity;
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null || !PolygonXZ.ContainsPoint(position, room.BoundaryVertices))
            {
                continue;
            }

            float area = Mathf.Abs(PolygonXZ.SignedArea(room.BoundaryVertices));
            if (area < bestArea)
            {
                bestArea = area;
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    private static void ApplyRoomCodeToDefects(FurnitureInstance instance, Room room)
    {
        if (instance == null || room == null || instance.ExportDefects == null)
        {
            return;
        }

        string roomCode = RoomCodeResolver.Resolve(room);
        if (string.IsNullOrWhiteSpace(roomCode))
        {
            return;
        }

        List<FurnitureDefectCatalogEntry> defects = new List<FurnitureDefectCatalogEntry>();
        for (int i = 0; i < instance.ExportDefects.Count; i++)
        {
            FurnitureDefectCatalogEntry defect = instance.ExportDefects[i];
            if (defect == null)
            {
                continue;
            }

            defects.Add(new FurnitureDefectCatalogEntry
            {
                mntnCd = roomCode,
                locCd = defect.locCd ?? string.Empty,
                mtrlCd = defect.mtrlCd ?? string.Empty,
            });
        }

        instance.SetExportDefects(defects);
    }

    private static List<FurnitureDefectCatalogEntry> ToFurnitureDefects(IReadOnlyList<LhWorkFurnitureDefectDto> defects)
    {
        List<FurnitureDefectCatalogEntry> results = new List<FurnitureDefectCatalogEntry>();
        if (defects == null)
        {
            return results;
        }

        for (int i = 0; i < defects.Count; i++)
        {
            LhWorkFurnitureDefectDto defect = defects[i];
            if (defect == null)
            {
                continue;
            }

            results.Add(new FurnitureDefectCatalogEntry
            {
                mntnCd = defect.mntnCd ?? string.Empty,
                locCd = defect.locCd ?? string.Empty,
                mtrlCd = defect.mtrlCd ?? string.Empty,
            });
        }

        return results;
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

    private static Dictionary<string, string> NormalizeOverlappingOpeningWalls(List<LhWorkWallDto> walls)
    {
        Dictionary<string, string> mergedWallIds = new Dictionary<string, string>(System.StringComparer.Ordinal);
        if (walls == null || walls.Count < 2)
        {
            return mergedWallIds;
        }

        List<int> wallIndices = new List<int>(walls.Count);
        for (int i = 0; i < walls.Count; i++)
        {
            wallIndices.Add(i);
        }

        wallIndices.Sort((left, right) => GetWallLength(walls[right]).CompareTo(GetWallLength(walls[left])));

        bool[] merged = new bool[walls.Count];
        for (int i = 0; i < wallIndices.Count; i++)
        {
            int targetIndex = wallIndices[i];
            if (merged[targetIndex])
            {
                continue;
            }

            LhWorkWallDto target = walls[targetIndex];
            if (!HasOpenings(target))
            {
                continue;
            }

            for (int j = 0; j < wallIndices.Count; j++)
            {
                int sourceIndex = wallIndices[j];
                if (sourceIndex == targetIndex || merged[sourceIndex])
                {
                    continue;
                }

                LhWorkWallDto source = walls[sourceIndex];
                if (!CanMergeNestedOpeningWall(target, source))
                {
                    continue;
                }

                MergeOpeningsIntoWall(target, source);
                if (!string.IsNullOrWhiteSpace(source.id) && !string.IsNullOrWhiteSpace(target.id))
                {
                    mergedWallIds[source.id] = target.id;
                }

                merged[sourceIndex] = true;
            }
        }

        for (int i = walls.Count - 1; i >= 0; i--)
        {
            if (merged[i])
            {
                walls.RemoveAt(i);
            }
        }

        return mergedWallIds;
    }

    private static void NormalizeRoomWallIds(IReadOnlyList<LhWorkRoomDto> rooms, Dictionary<string, string> mergedWallIds)
    {
        if (rooms == null || mergedWallIds == null || mergedWallIds.Count == 0)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            LhWorkRoomDto room = rooms[i];
            if (room == null)
            {
                continue;
            }

            NormalizeWallIdList(room.wallIds, mergedWallIds);
            NormalizeWallIdList(room.manualWallIds, mergedWallIds);
        }
    }

    private static void NormalizeWallIdList(List<string> wallIds, Dictionary<string, string> mergedWallIds)
    {
        if (wallIds == null || mergedWallIds == null || mergedWallIds.Count == 0)
        {
            return;
        }

        HashSet<string> seenIds = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = wallIds.Count - 1; i >= 0; i--)
        {
            string wallId = wallIds[i] ?? string.Empty;
            if (mergedWallIds.TryGetValue(wallId, out string mergedWallId))
            {
                wallId = mergedWallId;
                wallIds[i] = mergedWallId;
            }

            if (!seenIds.Add(wallId))
            {
                wallIds.RemoveAt(i);
            }
        }
    }

    private static bool CanMergeNestedOpeningWall(LhWorkWallDto target, LhWorkWallDto source)
    {
        if (!HasOpenings(target) ||
            !HasOpenings(source) ||
            target == source ||
            !HasCompatibleWallMetadata(target, source))
        {
            return false;
        }

        Vector3 targetStart = target.start.ToVector3();
        Vector3 targetEnd = target.end.ToVector3();
        Vector3 sourceStart = source.start.ToVector3();
        Vector3 sourceEnd = source.end.ToVector3();

        Vector3 targetDirection = targetEnd - targetStart;
        targetDirection.y = 0f;
        float targetLength = targetDirection.magnitude;
        if (targetLength <= MinimumWallLength)
        {
            return false;
        }

        targetDirection /= targetLength;

        Vector3 sourceDirection = sourceEnd - sourceStart;
        sourceDirection.y = 0f;
        float sourceLength = sourceDirection.magnitude;
        if (sourceLength <= MinimumWallLength || sourceLength >= targetLength - 0.0001f)
        {
            return false;
        }

        sourceDirection /= sourceLength;
        if (Mathf.Abs(Vector3.Dot(targetDirection, sourceDirection)) < 0.9999f)
        {
            return false;
        }

        float sourceStartDistance = Vector3.Dot(sourceStart - targetStart, targetDirection);
        float sourceEndDistance = Vector3.Dot(sourceEnd - targetStart, targetDirection);
        float sourceMin = Mathf.Min(sourceStartDistance, sourceEndDistance);
        float sourceMax = Mathf.Max(sourceStartDistance, sourceEndDistance);
        if (sourceMin < -0.001f || sourceMax > targetLength + 0.001f)
        {
            return false;
        }

        return SharesEndpointXZ(targetStart, targetEnd, sourceStart, sourceEnd);
    }

    private static bool HasCompatibleWallMetadata(LhWorkWallDto left, LhWorkWallDto right)
    {
        return Mathf.Abs(left.thickness - right.thickness) <= 0.0001f &&
            Mathf.Abs(left.height - right.height) <= 0.0001f &&
            Mathf.Abs(left.centerY - right.centerY) <= 0.0001f &&
            string.Equals(left.textureCode ?? string.Empty, right.textureCode ?? string.Empty, System.StringComparison.Ordinal);
    }

    private static bool SharesEndpointXZ(Vector3 targetStart, Vector3 targetEnd, Vector3 sourceStart, Vector3 sourceEnd)
    {
        return ArePointsNearXZ(targetStart, sourceStart) ||
            ArePointsNearXZ(targetStart, sourceEnd) ||
            ArePointsNearXZ(targetEnd, sourceStart) ||
            ArePointsNearXZ(targetEnd, sourceEnd);
    }

    private static bool ArePointsNearXZ(Vector3 left, Vector3 right)
    {
        float dx = left.x - right.x;
        float dz = left.z - right.z;
        return dx * dx + dz * dz <= 0.0001f;
    }

    private static void MergeOpeningsIntoWall(LhWorkWallDto target, LhWorkWallDto source)
    {
        if (target == null || source == null || source.openings == null)
        {
            return;
        }

        if (target.openings == null)
        {
            target.openings = new List<LhWorkOpeningDto>();
        }

        Vector3 targetStart = target.start.ToVector3();
        Vector3 targetEnd = target.end.ToVector3();
        Vector3 targetDirection = targetEnd - targetStart;
        targetDirection.y = 0f;
        float targetLength = targetDirection.magnitude;
        if (targetLength <= MinimumWallLength)
        {
            return;
        }

        targetDirection /= targetLength;

        Vector3 sourceStart = source.start.ToVector3();
        Vector3 sourceEnd = source.end.ToVector3();
        Vector3 sourceDirection = sourceEnd - sourceStart;
        sourceDirection.y = 0f;
        float sourceLength = sourceDirection.magnitude;
        if (sourceLength <= MinimumWallLength)
        {
            return;
        }

        sourceDirection /= sourceLength;
        for (int i = 0; i < source.openings.Count; i++)
        {
            LhWorkOpeningDto opening = source.openings[i];
            if (opening == null)
            {
                continue;
            }

            Vector3 openingWorldPoint = sourceStart + sourceDirection * opening.centerDistance;
            float targetCenterDistance = Vector3.Dot(openingWorldPoint - targetStart, targetDirection);
            if (targetCenterDistance < -0.001f || targetCenterDistance > targetLength + 0.001f)
            {
                continue;
            }

            LhWorkOpeningDto mergedOpening = CloneOpening(opening);
            mergedOpening.centerDistance = Mathf.Clamp(targetCenterDistance, 0f, targetLength);
            if (!ContainsEquivalentOpening(target.openings, mergedOpening))
            {
                target.openings.Add(mergedOpening);
            }
        }

        target.openings.Sort((left, right) => left.centerDistance.CompareTo(right.centerDistance));
    }

    private static LhWorkOpeningDto CloneOpening(LhWorkOpeningDto source)
    {
        return new LhWorkOpeningDto
        {
            type = source.type ?? string.Empty,
            doorTypeKey = source.doorTypeKey ?? string.Empty,
            windowTypeKey = source.windowTypeKey ?? string.Empty,
            prefabKey = source.prefabKey ?? string.Empty,
            doorOpensRight = source.doorOpensRight,
            doorVerticalFlip = source.doorVerticalFlip,
            centerDistance = source.centerDistance,
            width = source.width,
            height = source.height,
            depth = source.depth,
            bottomY = source.bottomY,
        };
    }

    private static bool ContainsEquivalentOpening(IReadOnlyList<LhWorkOpeningDto> openings, LhWorkOpeningDto candidate)
    {
        if (openings == null || candidate == null)
        {
            return false;
        }

        for (int i = 0; i < openings.Count; i++)
        {
            LhWorkOpeningDto opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            if (string.Equals(opening.type ?? string.Empty, candidate.type ?? string.Empty, System.StringComparison.Ordinal) &&
                string.Equals(opening.doorTypeKey ?? string.Empty, candidate.doorTypeKey ?? string.Empty, System.StringComparison.Ordinal) &&
                string.Equals(opening.windowTypeKey ?? string.Empty, candidate.windowTypeKey ?? string.Empty, System.StringComparison.Ordinal) &&
                string.Equals(opening.prefabKey ?? string.Empty, candidate.prefabKey ?? string.Empty, System.StringComparison.Ordinal) &&
                Mathf.Abs(opening.centerDistance - candidate.centerDistance) <= 0.001f &&
                Mathf.Abs(opening.width - candidate.width) <= 0.001f)
            {
                return true;
            }
        }

        return false;
    }

    private static float GetWallLength(LhWorkWallDto wall)
    {
        if (wall == null)
        {
            return 0f;
        }

        Vector3 delta = wall.end.ToVector3() - wall.start.ToVector3();
        delta.y = 0f;
        return delta.magnitude;
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
