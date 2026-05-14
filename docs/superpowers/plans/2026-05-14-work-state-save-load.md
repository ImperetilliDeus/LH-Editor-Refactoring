# Work State Save/Load Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a dedicated JSON work-state save/load feature that captures walls, door/window openings, rooms, and placed furniture, then restores the file by replacing the current editable work state.

**Architecture:** Add focused persistence types under `Assets/Scripts/ProjectPersistence`. The builder converts current runtime objects into versioned DTOs, the loader validates and applies DTOs back through existing wall, opening, room, and furniture creation paths, and a controller exposes save/load methods plus file dialogs.

**Tech Stack:** Unity C#, `UnityEngine.JsonUtility`, NUnit EditMode tests in `Assets/Tests/Editor`, existing `WallObjectFactory`, `WallOpeningPlacementManager.ApplyLayoutSnapshot`, `RoomManager`, and `FurnitureCatalog`.

---

### Task 1: Versioned Work-State Schema And Validation

**Files:**
- Create: `Assets/Scripts/ProjectPersistence/LhWorkStateSchema.cs`
- Test: `Assets/Tests/Editor/LhWorkStateSchemaTests.cs`

- [ ] **Step 1: Write the failing schema tests**

Create `Assets/Tests/Editor/LhWorkStateSchemaTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class LhWorkStateSchemaTests
{
    [Test]
    public void CreateEmpty_ReturnsCurrentVersionAndEmptyCollections()
    {
        LhWorkStateDto state = LhWorkStateDto.CreateEmpty();

        Assert.That(state.version, Is.EqualTo(LhWorkStateDto.CurrentVersion));
        Assert.That(state.walls, Is.Not.Null.And.Empty);
        Assert.That(state.rooms, Is.Not.Null.And.Empty);
        Assert.That(state.furniture, Is.Not.Null.And.Empty);
    }

    [Test]
    public void IsSupportedVersion_ReturnsFalse_ForUnsupportedVersion()
    {
        Assert.That(LhWorkStateDto.IsSupportedVersion(0), Is.False);
        Assert.That(LhWorkStateDto.IsSupportedVersion(LhWorkStateDto.CurrentVersion + 1), Is.False);
    }

    [Test]
    public void VectorDto_RoundTripsUnityVector3()
    {
        Vector3 value = new Vector3(1.25f, 2.5f, -3.75f);

        LhWorkVector3Dto dto = LhWorkVector3Dto.FromVector3(value);

        Assert.That(dto.ToVector3(), Is.EqualTo(value));
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\work-state-schema-tests.xml' -quit
```

Expected: FAIL because `LhWorkStateDto` and `LhWorkVector3Dto` do not exist.

- [ ] **Step 3: Add the schema DTOs**

Create `Assets/Scripts/ProjectPersistence/LhWorkStateSchema.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LhWorkStateDto
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<LhWorkWallDto> walls = new List<LhWorkWallDto>();
    public List<LhWorkRoomDto> rooms = new List<LhWorkRoomDto>();
    public List<LhWorkFurnitureDto> furniture = new List<LhWorkFurnitureDto>();

    public static LhWorkStateDto CreateEmpty()
    {
        return new LhWorkStateDto
        {
            version = CurrentVersion,
            walls = new List<LhWorkWallDto>(),
            rooms = new List<LhWorkRoomDto>(),
            furniture = new List<LhWorkFurnitureDto>(),
        };
    }

    public static bool IsSupportedVersion(int value)
    {
        return value == CurrentVersion;
    }
}

[Serializable]
public class LhWorkWallDto
{
    public string id = string.Empty;
    public string name = string.Empty;
    public LhWorkVector3Dto start;
    public LhWorkVector3Dto end;
    public float thickness;
    public float height;
    public float centerY;
    public int startVertexId;
    public int endVertexId;
    public bool suppressStartHandle;
    public bool suppressEndHandle;
    public bool startSplitPoint;
    public bool endSplitPoint;
    public List<LhWorkOpeningDto> openings = new List<LhWorkOpeningDto>();
}

[Serializable]
public class LhWorkOpeningDto
{
    public string type = string.Empty;
    public string doorTypeKey = string.Empty;
    public string windowTypeKey = string.Empty;
    public bool doorOpensRight;
    public bool doorVerticalFlip;
    public float centerDistance;
    public float width;
    public float height;
    public float depth;
    public float bottomY;
}

[Serializable]
public class LhWorkRoomDto
{
    public string name = string.Empty;
    public string roomTypeKey = string.Empty;
    public string roomCode = string.Empty;
    public string roomNativeCode = string.Empty;
    public string floorTextureCode = string.Empty;
    public string ceilingTextureCode = string.Empty;
    public bool isManualRoom;
    public LhWorkVector3Dto placementOffset;
    public List<LhWorkVector3Dto> boundaryVertices = new List<LhWorkVector3Dto>();
    public List<string> wallIds = new List<string>();
    public bool manualWallSelectionEnabled;
    public List<string> manualWallIds = new List<string>();
}

[Serializable]
public class LhWorkFurnitureDto
{
    public string catalogCode = string.Empty;
    public string exportCode = string.Empty;
    public string nativeCode = string.Empty;
    public string name = string.Empty;
    public LhWorkVector3Dto position;
    public LhWorkVector3Dto eulerAngles;
    public LhWorkVector3Dto localScale;
    public bool isPlaced;
    public string roomName = string.Empty;
}

[Serializable]
public struct LhWorkVector3Dto
{
    public float x;
    public float y;
    public float z;

    public static LhWorkVector3Dto FromVector3(Vector3 value)
    {
        return new LhWorkVector3Dto { x = value.x, y = value.y, z = value.z };
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
```

- [ ] **Step 4: Run the schema tests and verify they pass**

Run the same Unity command from Step 2.

Expected: PASS for `LhWorkStateSchemaTests`.

- [ ] **Step 5: Commit Task 1**

```powershell
git add -- 'Assets/Scripts/ProjectPersistence/LhWorkStateSchema.cs' 'Assets/Tests/Editor/LhWorkStateSchemaTests.cs'
git commit -m "Add work state schema"
```

### Task 2: Work-State Builder

**Files:**
- Create: `Assets/Scripts/ProjectPersistence/LhWorkStateBuilder.cs`
- Test: `Assets/Tests/Editor/LhWorkStateBuilderTests.cs`

- [ ] **Step 1: Write the failing builder tests**

Create `Assets/Tests/Editor/LhWorkStateBuilderTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

public class LhWorkStateBuilderTests
{
    private GameObject wallRoot;
    private GameObject furnitureRoot;

    [SetUp]
    public void SetUp()
    {
        wallRoot = new GameObject("Walls");
        furnitureRoot = new GameObject("FurnitureRoot");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(wallRoot);
        Object.DestroyImmediate(furnitureRoot);
    }

    [Test]
    public void Build_CapturesStandaloneWallGeometryAndFlags()
    {
        GameObject wallObject = WallObjectFactory.CreateWallObject("Wall_A", wallRoot.transform, null, default);
        WallObjectFactory.ConfigureWall(
            wallObject,
            new WallData(new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), 0.2f, 3f, 1.5f),
            10,
            11,
            true,
            false,
            false,
            true,
            0.01f,
            null,
            false);

        LhWorkStateDto state = LhWorkStateBuilder.Build(wallRoot.transform, null, furnitureRoot.transform);

        Assert.That(state.walls, Has.Count.EqualTo(1));
        LhWorkWallDto wall = state.walls[0];
        Assert.That(wall.name, Is.EqualTo("Wall_A"));
        Assert.That(wall.start.ToVector3(), Is.EqualTo(new Vector3(0f, 0f, 0f)));
        Assert.That(wall.end.ToVector3(), Is.EqualTo(new Vector3(2f, 0f, 0f)));
        Assert.That(wall.thickness, Is.EqualTo(0.2f));
        Assert.That(wall.height, Is.EqualTo(3f));
        Assert.That(wall.centerY, Is.EqualTo(1.5f));
        Assert.That(wall.startVertexId, Is.EqualTo(10));
        Assert.That(wall.endVertexId, Is.EqualTo(11));
        Assert.That(wall.suppressStartHandle, Is.True);
        Assert.That(wall.endSplitPoint, Is.True);
    }
}
```

- [ ] **Step 2: Run the builder test and verify it fails**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\work-state-builder-tests.xml' -quit
```

Expected: FAIL because `LhWorkStateBuilder` does not exist.

- [ ] **Step 3: Implement the builder for walls, rooms, and furniture**

Create `Assets/Scripts/ProjectPersistence/LhWorkStateBuilder.cs` with these public methods and helper shape:

```csharp
using System.Collections.Generic;
using UnityEngine;

public static class LhWorkStateBuilder
{
    private static readonly List<Wall> CachedWalls = new List<Wall>();

    public static LhWorkStateDto Build(Transform wallRoot, RoomManager roomManager, Transform furnitureRoot)
    {
        LhWorkStateDto state = LhWorkStateDto.CreateEmpty();
        BuildWalls(wallRoot, state.walls);
        BuildRooms(roomManager, state.rooms);
        BuildFurniture(furnitureRoot, state.furniture);
        return state;
    }

    private static void BuildWalls(Transform wallRoot, List<LhWorkWallDto> results)
    {
        if (wallRoot == null || results == null)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, CachedWalls, true);
        HashSet<WallOpeningContainer> exportedContainers = new HashSet<WallOpeningContainer>();
        for (int i = 0; i < CachedWalls.Count; i++)
        {
            Wall wall = CachedWalls[i];
            if (wall == null || wall.name.Equals("WallPreview", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
            if (container != null)
            {
                if (exportedContainers.Add(container))
                {
                    results.Add(CreateContainerWallDto(container));
                }

                continue;
            }

            if (wall.transform.parent == wallRoot)
            {
                results.Add(CreateStandaloneWallDto(wall));
            }
        }
    }

    private static LhWorkWallDto CreateStandaloneWallDto(Wall wall)
    {
        WallData data = wall.Data;
        return new LhWorkWallDto
        {
            id = data.id ?? string.Empty,
            name = wall.name ?? string.Empty,
            start = LhWorkVector3Dto.FromVector3(data.startPoint),
            end = LhWorkVector3Dto.FromVector3(data.endPoint),
            thickness = data.thickness,
            height = data.height,
            centerY = data.centerY,
            startVertexId = wall.StartVertexId,
            endVertexId = wall.EndVertexId,
            suppressStartHandle = wall.SuppressStartHandle,
            suppressEndHandle = wall.SuppressEndHandle,
            startSplitPoint = wall.IsStartSplitPoint,
            endSplitPoint = wall.IsEndSplitPoint,
            openings = new List<LhWorkOpeningDto>(),
        };
    }

    private static LhWorkWallDto CreateContainerWallDto(WallOpeningContainer container)
    {
        LhWorkWallDto dto = new LhWorkWallDto
        {
            id = container.name,
            name = container.name,
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
            openings = new List<LhWorkOpeningDto>(),
        };

        WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            dto.openings.Add(new LhWorkOpeningDto
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

        dto.openings.Sort((left, right) => left.centerDistance.CompareTo(right.centerDistance));
        return dto;
    }

    private static void BuildRooms(RoomManager roomManager, List<LhWorkRoomDto> results)
    {
        if (roomManager == null || results == null)
        {
            return;
        }

        List<Room> rooms = roomManager.GetAllRooms();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null)
            {
                continue;
            }

            LhWorkRoomDto dto = new LhWorkRoomDto
            {
                name = room.RoomName ?? room.name,
                roomTypeKey = room.RoomTypeKey ?? string.Empty,
                roomCode = room.RoomCode ?? string.Empty,
                roomNativeCode = room.RoomNativeCode ?? string.Empty,
                floorTextureCode = room.FloorTextureCode ?? string.Empty,
                ceilingTextureCode = room.CeilingTextureCode ?? string.Empty,
                isManualRoom = room.IsManualRoom,
                placementOffset = LhWorkVector3Dto.FromVector3(room.Data.PlacementOffset),
                manualWallSelectionEnabled = room.Data.ManualWallSelectionEnabled,
                boundaryVertices = ConvertVectors(room.BoundaryVertices),
                wallIds = new List<string>(room.Data.WallIds),
                manualWallIds = new List<string>(room.Data.ManualWallIds),
            };
            results.Add(dto);
        }
    }

    private static void BuildFurniture(Transform furnitureRoot, List<LhWorkFurnitureDto> results)
    {
        if (furnitureRoot == null || results == null)
        {
            return;
        }

        FurnitureInstance[] instances = furnitureRoot.GetComponentsInChildren<FurnitureInstance>(true);
        for (int i = 0; i < instances.Length; i++)
        {
            FurnitureInstance instance = instances[i];
            if (instance == null || !instance.IsPlaced)
            {
                continue;
            }

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
                roomName = instance.CurrentRoom != null ? instance.CurrentRoom.name : string.Empty,
            });
        }
    }

    private static List<LhWorkVector3Dto> ConvertVectors(IReadOnlyList<Vector3> values)
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
}
```

- [ ] **Step 4: Run the builder tests and verify they pass**

Run the same Unity command from Step 2.

Expected: PASS for `LhWorkStateBuilderTests` and `LhWorkStateSchemaTests`.

- [ ] **Step 5: Commit Task 2**

```powershell
git add -- 'Assets/Scripts/ProjectPersistence/LhWorkStateBuilder.cs' 'Assets/Tests/Editor/LhWorkStateBuilderTests.cs'
git commit -m "Build work state from scene"
```

### Task 3: Loader Core And Replace Semantics

**Files:**
- Create: `Assets/Scripts/ProjectPersistence/LhWorkStateLoader.cs`
- Modify: `Assets/Scripts/Room/RoomManager.cs`
- Test: `Assets/Tests/Editor/LhWorkStateLoaderTests.cs`

- [ ] **Step 1: Write the failing loader tests**

Create `Assets/Tests/Editor/LhWorkStateLoaderTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class LhWorkStateLoaderTests
{
    private GameObject wallRoot;
    private GameObject furnitureRoot;
    private RoomManager roomManager;

    [SetUp]
    public void SetUp()
    {
        wallRoot = new GameObject("Walls");
        furnitureRoot = new GameObject("FurnitureRoot");
        roomManager = new GameObject("RoomManager").AddComponent<RoomManager>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(roomManager.gameObject);
        Object.DestroyImmediate(wallRoot);
        Object.DestroyImmediate(furnitureRoot);
    }

    [Test]
    public void Load_ReplacesExistingWallsWithSavedWalls()
    {
        GameObject existing = WallObjectFactory.CreateWallObject("Existing", wallRoot.transform, null, default);
        WallObjectFactory.ConfigureWall(existing, new WallData(Vector3.zero, Vector3.right, 0.1f, 2f, 1f), 1, 2, false, false, false, false, 0.01f, null, false);
        LhWorkStateDto state = LhWorkStateDto.CreateEmpty();
        state.walls.Add(new LhWorkWallDto
        {
            id = "saved-wall",
            name = "Saved",
            start = LhWorkVector3Dto.FromVector3(new Vector3(0f, 0f, 0f)),
            end = LhWorkVector3Dto.FromVector3(new Vector3(3f, 0f, 0f)),
            thickness = 0.2f,
            height = 3f,
            centerY = 1.5f,
            openings = new List<LhWorkOpeningDto>(),
        });

        LhWorkStateLoadResult result = LhWorkStateLoader.Load(state, wallRoot.transform, roomManager, furnitureRoot.transform, null);

        Assert.That(result.Success, Is.True);
        Wall[] restoredWalls = wallRoot.GetComponentsInChildren<Wall>(true);
        Assert.That(restoredWalls, Has.Length.EqualTo(1));
        Assert.That(restoredWalls[0].name, Is.EqualTo("Saved"));
        Assert.That(restoredWalls[0].Data.id, Is.EqualTo("saved-wall"));
        Assert.That(restoredWalls[0].Data.endPoint, Is.EqualTo(new Vector3(3f, 0f, 0f)));
    }

    [Test]
    public void Load_DoesNotClearScene_WhenVersionUnsupported()
    {
        GameObject existing = WallObjectFactory.CreateWallObject("Existing", wallRoot.transform, null, default);
        WallObjectFactory.ConfigureWall(existing, new WallData(Vector3.zero, Vector3.right, 0.1f, 2f, 1f), 1, 2, false, false, false, false, 0.01f, null, false);
        LhWorkStateDto state = LhWorkStateDto.CreateEmpty();
        state.version = LhWorkStateDto.CurrentVersion + 1;

        LhWorkStateLoadResult result = LhWorkStateLoader.Load(state, wallRoot.transform, roomManager, furnitureRoot.transform, null);

        Assert.That(result.Success, Is.False);
        Assert.That(wallRoot.GetComponentsInChildren<Wall>(true), Has.Length.EqualTo(1));
    }
}
```

- [ ] **Step 2: Run loader tests and verify they fail**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\work-state-loader-tests.xml' -quit
```

Expected: FAIL because `LhWorkStateLoader` and `LhWorkStateLoadResult` do not exist.

- [ ] **Step 3: Add `RoomManager.ClearAllRoomsForWorkStateLoad`**

Modify `Assets/Scripts/Room/RoomManager.cs` by adding this public method near `DeleteRoom`:

```csharp
public void ClearAllRoomsForWorkStateLoad()
{
    for (int i = allRooms.Count - 1; i >= 0; i--)
    {
        Room room = allRooms[i];
        if (room != null)
        {
            Destroy(room.gameObject);
        }
    }

    allRooms.Clear();
    roomsByWalls.Clear();
    MarkGraphDirty();
    RoomsChanged?.Invoke();
}
```

- [ ] **Step 4: Implement loader core**

Create `Assets/Scripts/ProjectPersistence/LhWorkStateLoader.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class LhWorkStateLoadResult
{
    public bool Success { get; private set; }
    public string Message { get; private set; }

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
        if (state == null)
        {
            return LhWorkStateLoadResult.Fail("Work state is null.");
        }

        if (!LhWorkStateDto.IsSupportedVersion(state.version))
        {
            return LhWorkStateLoadResult.Fail($"Unsupported work state version: {state.version}");
        }

        if (wallRoot == null)
        {
            return LhWorkStateLoadResult.Fail("Wall root is missing.");
        }

        ClearChildren(wallRoot);
        roomManager?.ClearAllRoomsForWorkStateLoad();
        if (furnitureRoot != null)
        {
            ClearChildren(furnitureRoot);
        }

        Dictionary<string, Wall> wallsById = new Dictionary<string, Wall>();
        RestoreWalls(state.walls, wallRoot, wallsById);
        RestoreRooms(state.rooms, roomManager, wallsById);
        RestoreFurniture(state.furniture, furnitureRoot, furnitureCatalog);
        RoomTopologyEvents.RequestRefreshAll();
        return LhWorkStateLoadResult.Ok();
    }

    private static void RestoreWalls(List<LhWorkWallDto> walls, Transform wallRoot, Dictionary<string, Wall> wallsById)
    {
        if (walls == null)
        {
            return;
        }

        for (int i = 0; i < walls.Count; i++)
        {
            LhWorkWallDto dto = walls[i];
            if (dto == null)
            {
                continue;
            }

            Wall restored = RestoreStandaloneWall(dto, wallRoot);
            if (restored != null && !string.IsNullOrWhiteSpace(dto.id))
            {
                wallsById[dto.id] = restored;
            }
        }
    }

    private static Wall RestoreStandaloneWall(LhWorkWallDto dto, Transform wallRoot)
    {
        Vector3 start = dto.start.ToVector3();
        Vector3 end = dto.end.ToVector3();
        if ((end - start).sqrMagnitude < MinimumWallLength * MinimumWallLength)
        {
            Debug.LogWarning($"Skipped invalid wall '{dto.name}'.");
            return null;
        }

        GameObject wallObject = WallObjectFactory.CreateWallObject(string.IsNullOrWhiteSpace(dto.name) ? "Wall" : dto.name, wallRoot, null, default);
        WallData data = new WallData(start, end, dto.thickness, dto.height, dto.centerY);
        data.id = string.IsNullOrWhiteSpace(dto.id) ? data.id : dto.id;
        if (!WallObjectFactory.ConfigureWall(
                wallObject,
                data,
                dto.startVertexId,
                dto.endVertexId,
                dto.suppressStartHandle,
                dto.suppressEndHandle,
                dto.startSplitPoint,
                dto.endSplitPoint,
                MinimumWallLength,
                null,
                false))
        {
            Object.DestroyImmediate(wallObject);
            return null;
        }

        return wallObject.GetComponent<Wall>();
    }

    private static void RestoreRooms(List<LhWorkRoomDto> rooms, RoomManager roomManager, Dictionary<string, Wall> wallsById)
    {
        if (rooms == null || roomManager == null)
        {
            return;
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            LhWorkRoomDto dto = rooms[i];
            List<Vector3> vertices = ConvertVectors(dto.boundaryVertices);
            Room room = roomManager.CreateRoomFromPolygon(vertices);
            if (room == null)
            {
                continue;
            }

            room.SetPlacementOffset(dto.placementOffset.ToVector3());
            roomManager.UpdateRoomMetadata(room, dto.name, dto.roomTypeKey, dto.roomCode, dto.roomNativeCode, dto.floorTextureCode, dto.ceilingTextureCode);
            if (dto.manualWallSelectionEnabled)
            {
                roomManager.UpdateRoomWallSelection(room, dto.manualWallIds, true);
            }
        }
    }

    private static void RestoreFurniture(List<LhWorkFurnitureDto> furniture, Transform furnitureRoot, FurnitureCatalog furnitureCatalog)
    {
        if (furniture == null || furnitureRoot == null || furnitureCatalog == null)
        {
            return;
        }

        for (int i = 0; i < furniture.Count; i++)
        {
            LhWorkFurnitureDto dto = furniture[i];
            FurnitureCatalogItem item = FindFurnitureItem(furnitureCatalog, dto.catalogCode);
            if (item == null || item.prefab == null)
            {
                Debug.LogWarning($"Skipped missing furniture catalog item '{dto.catalogCode}'.");
                continue;
            }

            GameObject instanceObject = Object.Instantiate(item.prefab, furnitureRoot);
            instanceObject.name = string.IsNullOrWhiteSpace(dto.name) ? item.prefab.name : dto.name;
            instanceObject.transform.SetPositionAndRotation(dto.position.ToVector3(), Quaternion.Euler(dto.eulerAngles.ToVector3()));
            instanceObject.transform.localScale = dto.localScale.ToVector3();
            FurnitureInstance instance = instanceObject.GetComponent<FurnitureInstance>() ?? instanceObject.AddComponent<FurnitureInstance>();
            instance.Initialize(item);
            instance.SetPlaced(dto.isPlaced);
            instance.ApplyLayerRecursively();
        }
    }

    private static FurnitureCatalogItem FindFurnitureItem(FurnitureCatalog catalog, string catalogCode)
    {
        if (catalog == null || string.IsNullOrWhiteSpace(catalogCode))
        {
            return null;
        }

        IReadOnlyList<FurnitureCatalogItem> items = catalog.Items;
        for (int i = 0; i < items.Count; i++)
        {
            FurnitureCatalogItem item = items[i];
            if (item != null && item.code == catalogCode)
            {
                return item;
            }
        }

        return null;
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
    }

    private static List<Vector3> ConvertVectors(List<LhWorkVector3Dto> values)
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
}
```

- [ ] **Step 5: Run loader tests and verify they pass**

Run the same Unity command from Step 2.

Expected: PASS for schema, builder, and loader tests.

- [ ] **Step 6: Commit Task 3**

```powershell
git add -- 'Assets/Scripts/ProjectPersistence/LhWorkStateLoader.cs' 'Assets/Scripts/Room/RoomManager.cs' 'Assets/Tests/Editor/LhWorkStateLoaderTests.cs'
git commit -m "Load work state by replacing scene"
```

### Task 4: Opening Container Restore

**Files:**
- Modify: `Assets/Scripts/ProjectPersistence/LhWorkStateLoader.cs`
- Test: `Assets/Tests/Editor/LhWorkStateOpeningRestoreTests.cs`

- [ ] **Step 1: Write failing opening restore test**

Create `Assets/Tests/Editor/LhWorkStateOpeningRestoreTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class LhWorkStateOpeningRestoreTests
{
    private GameObject wallRoot;

    [SetUp]
    public void SetUp()
    {
        wallRoot = new GameObject("Walls");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(wallRoot);
    }

    [Test]
    public void Load_RestoresDoorOpeningOnWall()
    {
        LhWorkStateDto state = LhWorkStateDto.CreateEmpty();
        state.walls.Add(new LhWorkWallDto
        {
            id = "wall-with-door",
            name = "WallWithDoor",
            start = LhWorkVector3Dto.FromVector3(Vector3.zero),
            end = LhWorkVector3Dto.FromVector3(new Vector3(5f, 0f, 0f)),
            thickness = 0.2f,
            height = 3f,
            centerY = 1.5f,
            openings = new List<LhWorkOpeningDto>
            {
                new LhWorkOpeningDto
                {
                    type = WallOpeningPlacementManager.OpeningPlacementType.Door.ToString(),
                    doorTypeKey = "Pass",
                    centerDistance = 2.5f,
                    width = 0.9f,
                    height = 2.1f,
                    depth = 0.1f,
                    bottomY = 0f,
                }
            },
        });

        LhWorkStateLoadResult result = LhWorkStateLoader.Load(state, wallRoot.transform, null, null, null);

        Assert.That(result.Success, Is.True);
        WallOpeningContainer container = wallRoot.GetComponentInChildren<WallOpeningContainer>(true);
        Assert.That(container, Is.Not.Null);
        WallOpening opening = wallRoot.GetComponentInChildren<WallOpening>(true);
        Assert.That(opening, Is.Not.Null);
        Assert.That(opening.Type, Is.EqualTo(WallOpeningPlacementManager.OpeningPlacementType.Door));
        Assert.That(opening.DoorTypeKey, Is.EqualTo("Pass"));
        Assert.That(opening.CenterDistance, Is.EqualTo(2.5f));
    }
}
```

- [ ] **Step 2: Run opening restore test and verify it fails**

Run the Unity EditMode test command.

Expected: FAIL because Task 3 restores all walls as standalone walls.

- [ ] **Step 3: Implement container restore path**

Modify `RestoreWalls` in `LhWorkStateLoader.cs` so walls with `openings.Count > 0` call a new `RestoreContainerWall` method. Add:

```csharp
private static Wall RestoreContainerWall(LhWorkWallDto dto, Transform wallRoot)
{
    GameObject containerObject = new GameObject(string.IsNullOrWhiteSpace(dto.name) ? "Wall" : dto.name);
    containerObject.transform.SetParent(wallRoot, false);
    LayerUtility.ApplyLayer(containerObject, LayerUtility.WallLayerName, false);

    WallOpeningContainer container = containerObject.AddComponent<WallOpeningContainer>();
    container.Initialize(
        dto.start.ToVector3(),
        dto.end.ToVector3(),
        dto.thickness,
        dto.height,
        dto.centerY,
        default,
        dto.startVertexId,
        dto.endVertexId,
        dto.suppressStartHandle,
        dto.suppressEndHandle,
        dto.startSplitPoint,
        dto.endSplitPoint);

    for (int i = 0; i < dto.openings.Count; i++)
    {
        LhWorkOpeningDto openingDto = dto.openings[i];
        GameObject openingObject = new GameObject(openingDto.type == "Door" ? "Door" : "Window");
        openingObject.transform.SetParent(container.transform, false);
        LayerUtility.ApplyLayer(
            openingObject,
            openingDto.type == "Door" ? LayerUtility.DoorLayerName : LayerUtility.WindowLayerName,
            false);
        WallOpening opening = openingObject.AddComponent<WallOpening>();
        WallOpeningPlacementManager.OpeningPlacementType type = openingDto.type == "Window"
            ? WallOpeningPlacementManager.OpeningPlacementType.Window
            : WallOpeningPlacementManager.OpeningPlacementType.Door;
        opening.Initialize(
            null,
            container,
            type,
            openingDto.doorTypeKey,
            openingDto.windowTypeKey,
            openingDto.doorOpensRight,
            openingDto.doorVerticalFlip,
            openingDto.centerDistance,
            openingDto.width,
            openingDto.height,
            openingDto.depth,
            openingDto.bottomY);
    }

    return RestoreStandaloneWall(new LhWorkWallDto
    {
        id = dto.id,
        name = dto.name + "_Base",
        start = dto.start,
        end = dto.end,
        thickness = dto.thickness,
        height = dto.height,
        centerY = dto.centerY,
        startVertexId = dto.startVertexId,
        endVertexId = dto.endVertexId,
        suppressStartHandle = dto.suppressStartHandle,
        suppressEndHandle = dto.suppressEndHandle,
        startSplitPoint = dto.startSplitPoint,
        endSplitPoint = dto.endSplitPoint,
        openings = new List<LhWorkOpeningDto>(),
    }, container.transform);
}
```

Then change `RestoreWalls` to use:

```csharp
Wall restored = dto.openings != null && dto.openings.Count > 0
    ? RestoreContainerWall(dto, wallRoot)
    : RestoreStandaloneWall(dto, wallRoot);
```

- [ ] **Step 4: Run tests and verify they pass**

Run all EditMode work-state tests.

Expected: PASS.

- [ ] **Step 5: Commit Task 4**

```powershell
git add -- 'Assets/Scripts/ProjectPersistence/LhWorkStateLoader.cs' 'Assets/Tests/Editor/LhWorkStateOpeningRestoreTests.cs'
git commit -m "Restore wall openings from work state"
```

### Task 5: Save/Load Controller And File IO

**Files:**
- Create: `Assets/Scripts/ProjectPersistence/LhWorkStatePersistenceController.cs`
- Test: `Assets/Tests/Editor/LhWorkStatePersistenceControllerTests.cs`

- [ ] **Step 1: Write failing file IO tests**

Create `Assets/Tests/Editor/LhWorkStatePersistenceControllerTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class LhWorkStatePersistenceControllerTests
{
    [Test]
    public void SaveToPath_WritesVersionedJsonFile()
    {
        GameObject root = new GameObject("Walls");
        string path = Path.Combine(Application.temporaryCachePath, "lh-work-state-test.json");
        try
        {
            LhWorkStatePersistenceController controller = new GameObject("Controller").AddComponent<LhWorkStatePersistenceController>();
            controller.SetReferencesForTests(root.transform, null, null, null);

            controller.SaveToPath(path);

            string json = File.ReadAllText(path);
            Assert.That(json, Does.Contain("\"version\""));
            Assert.That(JsonUtility.FromJson<LhWorkStateDto>(json).version, Is.EqualTo(LhWorkStateDto.CurrentVersion));
            Object.DestroyImmediate(controller.gameObject);
        }
        finally
        {
            Object.DestroyImmediate(root);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
```

- [ ] **Step 2: Run controller test and verify it fails**

Run the Unity EditMode test command.

Expected: FAIL because `LhWorkStatePersistenceController` does not exist.

- [ ] **Step 3: Implement controller**

Create `Assets/Scripts/ProjectPersistence/LhWorkStatePersistenceController.cs`:

```csharp
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public class LhWorkStatePersistenceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private FurnitureCatalog furnitureCatalog;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;

    [Header("Persistence")]
    [SerializeField] private string defaultFilePath = "WorkStates/lh_work_state.json";
    [SerializeField] private bool prettyPrint = true;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void SaveToConfiguredPath()
    {
        SaveToPath(ResolveDefaultPath());
    }

    public void LoadFromConfiguredPath()
    {
        LoadFromPath(ResolveDefaultPath());
    }

    public void SaveToPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Debug.LogWarning("Work-state save skipped: path is empty.", this);
            return;
        }

        ResolveReferences();
        LhWorkStateDto state = LhWorkStateBuilder.Build(wallRoot, roomManager, furnitureRoot);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, JsonUtility.ToJson(state, prettyPrint), Encoding.UTF8);
        Debug.Log($"Work state saved: {path}", this);
    }

    public LhWorkStateLoadResult LoadFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return LhWorkStateLoadResult.Fail($"Work-state file not found: {path}");
        }

        ResolveReferences();
        LhWorkStateDto state = JsonUtility.FromJson<LhWorkStateDto>(File.ReadAllText(path, Encoding.UTF8));
        LhWorkStateLoadResult result = LhWorkStateLoader.Load(state, wallRoot, roomManager, furnitureRoot, furnitureCatalog);
        if (!result.Success)
        {
            Debug.LogError(result.Message, this);
        }
        else
        {
            Debug.Log($"Work state loaded: {path}", this);
        }

        return result;
    }

    public void SetReferencesForTests(Transform testWallRoot, RoomManager testRoomManager, Transform testFurnitureRoot, FurnitureCatalog testFurnitureCatalog)
    {
        wallRoot = testWallRoot;
        roomManager = testRoomManager;
        furnitureRoot = testFurnitureRoot;
        furnitureCatalog = testFurnitureCatalog;
    }

    private void ResolveReferences()
    {
        if (wallRoot == null)
        {
            wallRoot = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        }

        if (roomManager == null)
        {
            LayerUtility.ResolveObject(ref roomManager);
        }

        if (furnitureRoot == null)
        {
            furnitureRoot = LayerUtility.FindTransformByName("FurnitureRoot", true);
        }
    }

    private void BindButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SaveToConfiguredPath);
            saveButton.onClick.AddListener(SaveToConfiguredPath);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(LoadFromConfiguredPath);
            loadButton.onClick.AddListener(LoadFromConfiguredPath);
        }
    }

    private void UnbindButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SaveToConfiguredPath);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(LoadFromConfiguredPath);
        }
    }

    private string ResolveDefaultPath()
    {
        if (Path.IsPathRooted(defaultFilePath))
        {
            return defaultFilePath;
        }

        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", defaultFilePath));
    }
}
```

- [ ] **Step 4: Run controller tests and verify they pass**

Run all work-state EditMode tests.

Expected: PASS.

- [ ] **Step 5: Commit Task 5**

```powershell
git add -- 'Assets/Scripts/ProjectPersistence/LhWorkStatePersistenceController.cs' 'Assets/Tests/Editor/LhWorkStatePersistenceControllerTests.cs'
git commit -m "Add work state file persistence"
```

### Task 6: Final Verification And Manual Wiring Notes

**Files:**
- Modify: `docs/work-state-save-load.md`

- [ ] **Step 1: Add user-facing notes**

Create `docs/work-state-save-load.md`:

```markdown
# Work State Save/Load

`LhWorkStatePersistenceController` saves the editable LH Editor work state to JSON and loads it by replacing the current editable state.

## Saved State

- Walls and wall editor flags
- Door/window opening placement values
- Rooms, room metadata, room surfaces, and wall references
- Placed furniture resolved by `FurnitureCatalog` item code

## Scene Wiring

Add `LhWorkStatePersistenceController` to a scene object and assign:

- `Wall Root`
- `Room Manager`
- `Furniture Root`
- `Furniture Catalog`
- Optional save/load buttons

The default path is `WorkStates/lh_work_state.json` under the project root.
```

- [ ] **Step 2: Run all EditMode tests**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\work-state-all-editmode-tests.xml' -quit
```

Expected: all EditMode tests pass.

- [ ] **Step 3: Inspect test result XML**

Run:

```powershell
Select-String -Path 'Temp\work-state-all-editmode-tests.xml' -Pattern 'result="Failed"|result="Passed"'
```

Expected: the top-level test-suite reports `result="Passed"`.

- [ ] **Step 4: Check git status**

Run:

```powershell
git status --short
```

Expected: only intended work-state files and documentation are modified, plus any unrelated pre-existing Room changes remain unstaged unless the user asks to include them.

- [ ] **Step 5: Commit Task 6**

```powershell
git add -- 'docs/work-state-save-load.md'
git commit -m "Document work state persistence"
```

---

## Self-Review

- Spec coverage: the plan covers dedicated JSON schema, version validation, save builder, replace-on-load loader, room/furniture restore, opening restore, controller file IO, and docs.
- Risk: Task 4 uses a minimal opening container restore and does not initially call `WallOpeningPlacementManager.RebuildOpeningContainer` because tests can run without a configured manager. If scene visuals require generated filler segments, add an optional serialized `WallOpeningPlacementManager` reference to the controller or loader and call its rebuild method after restoring containers.
- Existing worktree note: do not stage or revert the pre-existing `Assets/Scripts/Room/*` and `Assets/Tests/Editor/RoomGraphUtilityTests.cs` changes unless the user explicitly asks.
