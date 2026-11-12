using System.Collections.Generic;
using UnityEngine;

public static class LhWorkStateBuilder
{
    private const float MinimumExportedRoomWallReferenceLength = 0.05f;

    private static readonly List<Wall> CachedWalls = new List<Wall>();

    public static LhWorkStateDto Build(Transform wallRoot, RoomManager roomManager, Transform furnitureRoot)
    {
        Dictionary<string, string> normalizedWallIdsBySourceId = new Dictionary<string, string>(System.StringComparer.Ordinal);
        LhWorkStateDto state = LhWorkStateDto.CreateEmpty();
        state.walls = BuildWalls(wallRoot, normalizedWallIdsBySourceId);
        state.rooms = BuildRooms(roomManager, normalizedWallIdsBySourceId);
        state.furniture = BuildFurniture(furnitureRoot, roomManager);
        NormalizeFurnitureRoomsBySavedPolygons(state);
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
            textureCode = data != null ? data.TextureCode ?? string.Empty : string.Empty,
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
        string textureCode = ResolveContainerTextureCode(container);
        return new LhWorkWallDto
        {
            id = collapsedId ?? string.Empty,
            name = container.name ?? string.Empty,
            start = LhWorkVector3Dto.FromVector3(container.WallStart),
            end = LhWorkVector3Dto.FromVector3(container.WallEnd),
            thickness = container.WallThickness,
            height = container.WallHeight,
            centerY = container.CenterY,
            textureCode = textureCode,
            startVertexId = container.OuterStartVertexId,
            endVertexId = container.OuterEndVertexId,
            suppressStartHandle = container.SuppressOuterStartHandle,
            suppressEndHandle = container.SuppressOuterEndHandle,
            startSplitPoint = container.OuterStartSplitPoint,
            endSplitPoint = container.OuterEndSplitPoint,
            openings = BuildOpenings(container),
        };
    }

    private static string ResolveContainerTextureCode(WallOpeningContainer container)
    {
        if (container == null)
        {
            return string.Empty;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null || wall.Data == null || string.IsNullOrWhiteSpace(wall.Data.TextureCode))
            {
                continue;
            }

            return wall.Data.TextureCode;
        }

        return string.Empty;
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

    private static List<LhWorkFurnitureDto> BuildFurniture(Transform furnitureRoot, RoomManager roomManager)
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

            Room currentRoom = ResolveFurnitureRoom(instance, roomManager) ?? instance.CurrentRoom;
            if (currentRoom != null && currentRoom != instance.CurrentRoom)
            {
                instance.SetCurrentRoom(currentRoom);
            }

            results.Add(new LhWorkFurnitureDto
            {
                catalogCode = instance.CatalogCode ?? string.Empty,
                exportCode = instance.ExportCode ?? string.Empty,
                nativeCode = instance.NativeCode ?? string.Empty,
                name = instance.name ?? string.Empty,
                furnishLocationCode = instance.FurnishLocationCode ?? string.Empty,
                position = LhWorkVector3Dto.FromVector3(instance.transform.position),
                eulerAngles = LhWorkVector3Dto.FromVector3(instance.transform.eulerAngles),
                localScale = LhWorkVector3Dto.FromVector3(instance.transform.localScale),
                isPlaced = instance.IsPlaced,
                roomName = currentRoom != null
                    ? (!string.IsNullOrEmpty(currentRoom.RoomName) ? currentRoom.RoomName : currentRoom.name ?? string.Empty)
                    : string.Empty,
                defects = BuildFurnitureDefects(instance, currentRoom),
            });
        }

        return results;
    }

    private static List<LhWorkFurnitureDefectDto> BuildFurnitureDefects(FurnitureInstance instance, Room room)
    {
        List<LhWorkFurnitureDefectDto> results = new List<LhWorkFurnitureDefectDto>();
        IReadOnlyList<FurnitureDefectCatalogEntry> defects = instance != null ? instance.ExportDefects : null;
        if (defects == null)
        {
            return results;
        }

        for (int i = 0; i < defects.Count; i++)
        {
            FurnitureDefectCatalogEntry defect = defects[i];
            if (defect == null)
            {
                continue;
            }

            results.Add(new LhWorkFurnitureDefectDto
            {
                mntnCd = !string.IsNullOrWhiteSpace(ResolveRoomCode(room)) ? ResolveRoomCode(room) : defect.mntnCd ?? string.Empty,
                locCd = defect.locCd ?? string.Empty,
                mtrlCd = defect.mtrlCd ?? string.Empty,
            });
        }

        return results;
    }

    private static Room ResolveFurnitureRoom(FurnitureInstance instance, RoomManager roomManager)
    {
        if (instance == null)
        {
            return null;
        }

        List<Room> rooms = CollectRoomCandidates(roomManager);
        if (rooms.Count == 0)
        {
            return null;
        }

        Bounds bounds = instance.CalculateWorldBounds();
        Room bestRoom = null;
        float bestArea = float.PositiveInfinity;
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null || !IsFurnitureInsideRoom(instance, room, bounds))
            {
                continue;
            }

            float area = Mathf.Abs(CalculatePolygonAreaXZ(room.BoundaryVertices));
            if (area < bestArea)
            {
                bestArea = area;
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    private static List<Room> CollectRoomCandidates(RoomManager roomManager)
    {
        List<Room> results = new List<Room>();
        if (roomManager != null)
        {
            List<Room> managerRooms = roomManager.GetAllRooms();
            if (managerRooms != null)
            {
                for (int i = 0; i < managerRooms.Count; i++)
                {
                    AddUniqueRoom(results, managerRooms[i]);
                }
            }
        }

        Room[] sceneRooms = Object.FindObjectsByType<Room>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sceneRooms.Length; i++)
        {
            AddUniqueRoom(results, sceneRooms[i]);
        }

        return results;
    }

    private static void AddUniqueRoom(List<Room> rooms, Room room)
    {
        if (rooms == null || room == null || rooms.Contains(room))
        {
            return;
        }

        rooms.Add(room);
    }

    private static bool IsFurnitureInsideRoom(FurnitureInstance instance, Room room, Bounds bounds)
    {
        if (instance == null || room == null)
        {
            return false;
        }

        List<Vector3> vertices = new List<Vector3>();
        if (!room.TryGetOrderedVertices(vertices))
        {
            return false;
        }

        return IsBoundsFootprintInsidePolygonXZ(bounds, vertices) ||
            IsPointInsidePolygonXZ(instance.transform.position, vertices);
    }

    private static bool IsPointInsidePolygonXZ(Vector3 point, List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return false;
        }

        bool inside = false;
        float x = point.x;
        float z = point.z;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            Vector3 pi = vertices[i];
            Vector3 pj = vertices[j];
            float deltaZ = pj.z - pi.z;
            bool intersects = Mathf.Abs(deltaZ) > 0.000001f &&
                              ((pi.z > z) != (pj.z > z)) &&
                              (x < (pj.x - pi.x) * (z - pi.z) / deltaZ + pi.x);
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

    private static float CalculatePolygonAreaXZ(IReadOnlyList<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            area += vertices[j].x * vertices[i].z - vertices[i].x * vertices[j].z;
        }

        return area * 0.5f;
    }

    private static string ResolveRoomCode(Room room)
    {
        if (room == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(room.RoomCode))
        {
            return room.RoomCode;
        }

        return RoomTypeCatalog.TryGetCode(room.RoomTypeKey, out int code) ? code.ToString() : string.Empty;
    }

    private static void NormalizeFurnitureRoomsBySavedPolygons(LhWorkStateDto state)
    {
        if (state == null || state.rooms == null || state.furniture == null)
        {
            return;
        }

        int roomNameFixes = 0;
        int defectCodeFixes = 0;
        int unresolvedFurniture = 0;
        for (int i = 0; i < state.furniture.Count; i++)
        {
            LhWorkFurnitureDto furniture = state.furniture[i];
            if (furniture == null || !furniture.isPlaced)
            {
                continue;
            }

            LhWorkRoomDto room = ResolveFurnitureRoomDto(furniture.position.ToVector3(), state.rooms);
            if (room == null)
            {
                unresolvedFurniture++;
                continue;
            }

            string roomName = room.name ?? string.Empty;
            if (!string.Equals(furniture.roomName ?? string.Empty, roomName, System.StringComparison.Ordinal))
            {
                roomNameFixes++;
                furniture.roomName = roomName;
            }

            string roomCode = ResolveRoomCode(room);
            if (string.IsNullOrWhiteSpace(roomCode) || furniture.defects == null)
            {
                continue;
            }

            for (int defectIndex = 0; defectIndex < furniture.defects.Count; defectIndex++)
            {
                LhWorkFurnitureDefectDto defect = furniture.defects[defectIndex];
                if (defect != null)
                {
                    if (!string.Equals(defect.mntnCd ?? string.Empty, roomCode, System.StringComparison.Ordinal))
                    {
                        defectCodeFixes++;
                        defect.mntnCd = roomCode;
                    }
                }
            }
        }

        Debug.Log(
            $"[LhWorkStateBuilder] furniture room normalization complete: " +
            $"furniture={state.furniture.Count}, roomNameFixes={roomNameFixes}, " +
            $"defectCodeFixes={defectCodeFixes}, unresolvedFurniture={unresolvedFurniture}");
    }

    private static LhWorkRoomDto ResolveFurnitureRoomDto(Vector3 position, IReadOnlyList<LhWorkRoomDto> rooms)
    {
        LhWorkRoomDto bestRoom = null;
        float bestArea = float.PositiveInfinity;
        for (int i = 0; i < rooms.Count; i++)
        {
            LhWorkRoomDto room = rooms[i];
            if (room == null || room.boundaryVertices == null || room.boundaryVertices.Count < 3 ||
                !IsPointInsidePolygonXZ(position, room.boundaryVertices))
            {
                continue;
            }

            float area = Mathf.Abs(CalculatePolygonAreaXZ(room.boundaryVertices));
            if (area < bestArea)
            {
                bestArea = area;
                bestRoom = room;
            }
        }

        return bestRoom;
    }

    private static bool IsPointInsidePolygonXZ(Vector3 point, IReadOnlyList<LhWorkVector3Dto> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return false;
        }

        bool inside = false;
        float x = point.x;
        float z = point.z;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            Vector3 pi = vertices[i].ToVector3();
            Vector3 pj = vertices[j].ToVector3();
            float deltaZ = pj.z - pi.z;
            bool intersects = Mathf.Abs(deltaZ) > 0.000001f &&
                              ((pi.z > z) != (pj.z > z)) &&
                              (x < (pj.x - pi.x) * (z - pi.z) / deltaZ + pi.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float CalculatePolygonAreaXZ(IReadOnlyList<LhWorkVector3Dto> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            Vector3 current = vertices[i].ToVector3();
            Vector3 previous = vertices[j].ToVector3();
            area += previous.x * current.z - current.x * previous.z;
        }

        return area * 0.5f;
    }

    private static string ResolveRoomCode(LhWorkRoomDto room)
    {
        if (room == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(room.roomCode))
        {
            return room.roomCode;
        }

        return RoomTypeCatalog.TryGetCode(room.roomTypeKey, out int code) ? code.ToString() : string.Empty;
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

        if (!string.IsNullOrWhiteSpace(container.PersistentWallId) && HasExportableRoomWallReferenceLength(container))
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
        if (wall == null || normalizedWallIdsBySourceId == null || !HasExportableRoomWallReferenceLength(wall))
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

    private static bool HasExportableRoomWallReferenceLength(Wall wall)
    {
        if (wall == null || wall.Data == null)
        {
            return false;
        }

        Vector3 delta = wall.Data.endPoint - wall.Data.startPoint;
        delta.y = 0f;
        return delta.magnitude >= MinimumExportedRoomWallReferenceLength;
    }

    private static bool HasExportableRoomWallReferenceLength(WallOpeningContainer container)
    {
        if (container == null)
        {
            return false;
        }

        Vector3 delta = container.WallEnd - container.WallStart;
        delta.y = 0f;
        return delta.magnitude >= MinimumExportedRoomWallReferenceLength;
    }

    private static bool IsPreviewWall(Transform root)
    {
        return root != null &&
               string.Equals(root.name, "WallPreview", System.StringComparison.OrdinalIgnoreCase);
    }
}
