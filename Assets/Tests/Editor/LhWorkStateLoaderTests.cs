using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LhWorkStateLoaderTests
{
    private GameObject wallRoot;
    private GameObject furnitureRoot;
    private GameObject roomManagerObject;
    private GameObject handleManagerObject;
    private GameObject handleCanvasObject;
    private GameObject furniturePrefab;
    private UnityEngine.Object furnitureCatalog;
    private GameObject sceneHierarchyObject;
    private GameObject sceneHierarchyContentObject;

    [SetUp]
    public void SetUp()
    {
        wallRoot = new GameObject("Walls");
        furnitureRoot = new GameObject("FurnitureRoot");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(wallRoot);
        UnityEngine.Object.DestroyImmediate(furnitureRoot);
        UnityEngine.Object.DestroyImmediate(roomManagerObject);
        UnityEngine.Object.DestroyImmediate(handleManagerObject);
        UnityEngine.Object.DestroyImmediate(handleCanvasObject);
        UnityEngine.Object.DestroyImmediate(furniturePrefab);
        UnityEngine.Object.DestroyImmediate(furnitureCatalog);
        UnityEngine.Object.DestroyImmediate(sceneHierarchyObject);
        UnityEngine.Object.DestroyImmediate(sceneHierarchyContentObject);
    }

    [Test]
    public void Load_ReplacesExistingWallsWithSavedWalls()
    {
        GameObject existingWall = CreateWallObject("ExistingWall", wallRoot.transform);
        ConfigureWall(
            existingWall,
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            0.1f,
            2f,
            1f,
            1,
            2,
            false,
            false,
            false,
            false);
        object state = CreateState();
        AddWall(
            state,
            "saved-wall-id",
            "SavedWall",
            new Vector3(2f, 0f, 0f),
            new Vector3(5f, 0f, 0f),
            0.25f,
            3.2f,
            1.6f,
            10,
            11,
            true,
            false,
            false,
            true);

        object result = Load(state, wallRoot.transform, null, null, null);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.True);
        Array walls = wallRoot.GetComponentsInChildren(GetAssemblyType("Wall"), true);
        Assert.That(walls, Has.Length.EqualTo(1));
        Component wall = (Component)walls.GetValue(0);
        Assert.That(wall.name, Is.EqualTo("SavedWall"));
        object data = wall.GetType().GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)?.GetValue(wall);
        Assert.That(data, Is.Not.Null);
        Assert.That(GetPropertyValue<string>(data, "id"), Is.EqualTo("saved-wall-id"));
        Assert.That(GetPropertyValue<Vector3>(data, "startPoint"), Is.EqualTo(new Vector3(2f, 0f, 0f)));
        Assert.That(GetPropertyValue<Vector3>(data, "endPoint"), Is.EqualTo(new Vector3(5f, 0f, 0f)));
        Assert.That(GetPropertyValue<float>(data, "thickness"), Is.EqualTo(0.25f));
        Assert.That(GetPropertyValue<float>(data, "height"), Is.EqualTo(3.2f));
        Assert.That(GetPropertyValue<float>(data, "centerY"), Is.EqualTo(1.6f));
        Assert.That(GetPropertyValue<int>(wall, "StartVertexId"), Is.EqualTo(10));
        Assert.That(GetPropertyValue<int>(wall, "EndVertexId"), Is.EqualTo(11));
        Assert.That(GetPropertyValue<bool>(wall, "SuppressStartHandle"), Is.True);
        Assert.That(GetPropertyValue<bool>(wall, "SuppressEndHandle"), Is.False);
        Assert.That(GetPropertyValue<bool>(wall, "IsStartSplitPoint"), Is.False);
        Assert.That(GetPropertyValue<bool>(wall, "IsEndSplitPoint"), Is.True);
    }

    [Test]
    public void Load_RefreshesInactiveSceneHierarchyTreeViewAfterRestore()
    {
        object state = CreateState();
        AddWall(
            state,
            "saved-wall-id",
            "SavedWall",
            new Vector3(2f, 0f, 0f),
            new Vector3(5f, 0f, 0f),
            0.25f,
            3.2f,
            1.6f,
            10,
            11,
            false,
            false,
            false,
            false);
        sceneHierarchyContentObject = new GameObject("HierarchyContent", typeof(RectTransform));
        sceneHierarchyObject = new GameObject("SceneHierarchyTreeView");
        sceneHierarchyObject.SetActive(false);
        Component treeView = sceneHierarchyObject.AddComponent(GetAssemblyType("SceneHierarchyTreeView"));
        InvokeSetHierarchyReferencesForTests(
            treeView,
            wallRoot.transform,
            CreateRoomList(),
            sceneHierarchyContentObject.GetComponent<RectTransform>(),
            null);

        object result = Load(state, wallRoot.transform, null, null, null);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.True);
        Assert.That(sceneHierarchyContentObject.transform.childCount, Is.EqualTo(1));
        Assert.That(sceneHierarchyContentObject.transform.GetChild(0).name, Is.EqualTo("Wall_SavedWall"));
    }

    [Test]
    public void Load_DoesNotClearCurrentWalls_WhenVersionUnsupported()
    {
        GameObject existingWall = CreateWallObject("ExistingWall", wallRoot.transform);
        ConfigureWall(
            existingWall,
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            0.1f,
            2f,
            1f,
            1,
            2,
            false,
            false,
            false,
            false);
        object state = CreateState();
        SetFieldValue(state, "version", 0);
        AddWall(
            state,
            "saved-wall-id",
            "SavedWall",
            new Vector3(2f, 0f, 0f),
            new Vector3(5f, 0f, 0f),
            0.25f,
            3.2f,
            1.6f,
            10,
            11,
            false,
            false,
            false,
            false);

        object result = Load(state, wallRoot.transform, null, null, null);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.False);
        Array walls = wallRoot.GetComponentsInChildren(GetAssemblyType("Wall"), true);
        Assert.That(walls, Has.Length.EqualTo(1));
        Assert.That(((Component)walls.GetValue(0)).name, Is.EqualTo("ExistingWall"));
    }

    [Test]
    public void Load_DoesNotClearCurrentWalls_WhenSavedWallGeometryInvalid()
    {
        CreateExistingWall();
        object state = CreateState();
        AddWall(
            state,
            "invalid-wall-id",
            "InvalidWall",
            new Vector3(2f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
            0.25f,
            3.2f,
            1.6f,
            10,
            11,
            false,
            false,
            false,
            false);

        object result = Load(state, wallRoot.transform, null, null, null);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.False);
        AssertExistingWallStillPresent();
    }

    [Test]
    public void Load_DoesNotClearCurrentWalls_WhenFurnitureDependencyMissing()
    {
        CreateExistingWall();
        object state = CreateState();
        AddWall(
            state,
            "saved-wall-id",
            "SavedWall",
            new Vector3(2f, 0f, 0f),
            new Vector3(5f, 0f, 0f),
            0.25f,
            3.2f,
            1.6f,
            10,
            11,
            false,
            false,
            false,
            false);
        AddFurniture(state, "chair-a", "Chair A", "Living");

        object result = Load(state, wallRoot.transform, null, furnitureRoot.transform, null);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.False);
        AssertExistingWallStillPresent();
    }

    [Test]
    public void Load_DoesNotClearCurrentWalls_WhenFurniturePrefabMissing()
    {
        CreateExistingWall();
        object state = CreateState();
        AddWall(
            state,
            "saved-wall-id",
            "SavedWall",
            new Vector3(2f, 0f, 0f),
            new Vector3(5f, 0f, 0f),
            0.25f,
            3.2f,
            1.6f,
            10,
            11,
            false,
            false,
            false,
            false);
        AddFurniture(state, "chair-a", "Chair A", "Living");
        object catalog = CreateFurnitureCatalog("chair-a", null);

        object result = Load(state, wallRoot.transform, null, furnitureRoot.transform, catalog);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.False);
        AssertExistingWallStillPresent();
    }

    [Test]
    public void Load_RestoresFurnitureUsingExportCode_WhenCatalogCodeEmpty()
    {
        object state = CreateState();
        AddFurniture(state, string.Empty, "export-chair", string.Empty, "Export Chair", string.Empty);
        furniturePrefab = new GameObject("ChairPrefab");
        object catalog = CreateFurnitureCatalog(string.Empty, "export-chair", string.Empty, furniturePrefab);

        object result = Load(state, wallRoot.transform, null, furnitureRoot.transform, catalog);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.True);
        Assert.That(furnitureRoot.transform.childCount, Is.EqualTo(1));
        Transform restored = furnitureRoot.transform.GetChild(0);
        Assert.That(restored.name, Is.EqualTo("Export Chair"));
        Assert.That(restored.GetComponent(GetAssemblyType("FurnitureInstance")), Is.Not.Null);
    }

    [Test]
    public void Load_DoesNotClearCurrentWalls_WhenFurnitureHasNoResolvableIdentifier()
    {
        CreateExistingWall();
        object state = CreateState();
        AddFurniture(state, string.Empty, string.Empty, string.Empty, "Nameless Chair", string.Empty);
        furniturePrefab = new GameObject("ChairPrefab");
        object catalog = CreateFurnitureCatalog("chair-a", "export-chair", "native-chair", furniturePrefab);

        object result = Load(state, wallRoot.transform, null, furnitureRoot.transform, catalog);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.False);
        AssertExistingWallStillPresent();
    }

    [Test]
    public void Load_RestoresRoomMetadataAndManualFlag()
    {
        object state = CreateState();
        AddWall(
            state,
            "wall-a",
            "WallA",
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            0.2f,
            3f,
            1.5f,
            1,
            2,
            false,
            false,
            false,
            false);
        AddRoom(
            state,
            "Living",
            "living-type",
            "RM-01",
            "NATIVE-01",
            "floor-a",
            "ceiling-a",
            false,
            new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(4f, 0f, 0f),
                new Vector3(4f, 0f, 3f),
                new Vector3(0f, 0f, 3f),
            },
            new[] { "wall-a" },
            true,
            new[] { "wall-a" });
        object roomManager = CreateRoomManager();

        object result = Load(state, wallRoot.transform, roomManager, null, null);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.True);
        IList rooms = (IList)roomManager.GetType()
            .GetMethod("GetAllRooms", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
            ?.Invoke(roomManager, null);
        Assert.That(rooms, Has.Count.EqualTo(1));
        object room = rooms[0];
        Assert.That(GetPropertyValue<string>(room, "RoomName"), Is.EqualTo("Living"));
        Assert.That(GetPropertyValue<string>(room, "RoomTypeKey"), Is.EqualTo("living-type"));
        Assert.That(GetPropertyValue<string>(room, "RoomCode"), Is.EqualTo("RM-01"));
        Assert.That(GetPropertyValue<string>(room, "RoomNativeCode"), Is.EqualTo("NATIVE-01"));
        Assert.That(GetPropertyValue<string>(room, "FloorTextureCode"), Is.EqualTo("floor-a"));
        Assert.That(GetPropertyValue<string>(room, "CeilingTextureCode"), Is.EqualTo("ceiling-a"));
        Assert.That(GetPropertyValue<bool>(room, "IsManualRoom"), Is.False);
        Assert.That(GetPropertyValue<bool>(room, "ManualWallSelectionEnabled"), Is.True);
    }

    [Test]
    public void Load_AssignsSingleHandleToSharedVertexCoordinates()
    {
        object state = CreateState();
        AddWall(
            state,
            "wall-a",
            "WallA",
            new Vector3(0f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
            0.2f,
            3f,
            1.5f,
            10,
            0,
            false,
            false,
            false,
            false);
        AddWall(
            state,
            "wall-b",
            "WallB",
            new Vector3(2f, 0f, 0f),
            new Vector3(2f, 0f, 2f),
            0.2f,
            3f,
            1.5f,
            12,
            13,
            false,
            false,
            false,
            false);
        object services = CreateLoadServices(CreateHandleManager());

        object result = Load(state, wallRoot.transform, null, null, null, services);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.True);
        Assert.That(CountHandleRects(), Is.EqualTo(3));
    }

    [Test]
    public void RebuildRegisteredWallsFromHierarchy_ReplacesExistingHandleRects()
    {
        GameObject wall = CreateWallObject("Wall", wallRoot.transform);
        ConfigureWall(
            wall,
            new Vector3(0f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
            0.2f,
            3f,
            1.5f,
            10,
            11,
            false,
            false,
            false,
            false);
        object handleManager = CreateHandleManager();
        Type handleManagerType = GetAssemblyType("HandleManager");

        handleManagerType.GetMethod("RegisterWall", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(handleManager, new object[] { wall });
        Assert.That(CountHandleRects(), Is.EqualTo(2));

        handleManagerType.GetMethod("RebuildRegisteredWallsFromHierarchy", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(handleManager, null);

        Assert.That(CountHandleRects(), Is.EqualTo(2));
    }

    private static object Load(object state, Transform wallRoot, object roomManager, Transform furnitureRoot, object furnitureCatalog)
    {
        Type loaderType = GetAssemblyType("LhWorkStateLoader");
        MethodInfo method = loaderType.GetMethod(
            "Load",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[]
            {
                GetAssemblyType("LhWorkStateDto"),
                typeof(Transform),
                GetAssemblyType("RoomManager"),
                typeof(Transform),
                GetAssemblyType("FurnitureCatalog"),
            },
            null);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new[] { state, wallRoot, roomManager, furnitureRoot, furnitureCatalog });
    }

    private static object Load(object state, Transform wallRoot, object roomManager, Transform furnitureRoot, object furnitureCatalog, object services)
    {
        Type loaderType = GetAssemblyType("LhWorkStateLoader");
        MethodInfo method = loaderType.GetMethod(
            "Load",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[]
            {
                GetAssemblyType("LhWorkStateDto"),
                typeof(Transform),
                GetAssemblyType("RoomManager"),
                typeof(Transform),
                GetAssemblyType("FurnitureCatalog"),
                GetAssemblyType("LhWorkStateLoadServices"),
            },
            null);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new[] { state, wallRoot, roomManager, furnitureRoot, furnitureCatalog, services });
    }

    private static void InvokeSetHierarchyReferencesForTests(
        Component treeView,
        Transform testWallRoot,
        object rooms,
        RectTransform contentRoot,
        Component selectionManager)
    {
        Type roomType = GetAssemblyType("Room");
        Type selectionManagerType = GetAssemblyType("WallSelectionManager");
        Type enumerableRoomType = typeof(System.Collections.Generic.IEnumerable<>).MakeGenericType(roomType);
        MethodInfo method = treeView.GetType().GetMethod(
            "SetReferencesForTests",
            new[] { typeof(Transform), enumerableRoomType, typeof(RectTransform), selectionManagerType });
        Assert.That(method, Is.Not.Null);
        method.Invoke(treeView, new object[] { testWallRoot, rooms, contentRoot, selectionManager });
    }

    private static object CreateRoomList()
    {
        Type roomType = GetAssemblyType("Room");
        Type listType = typeof(System.Collections.Generic.List<>).MakeGenericType(roomType);
        return Activator.CreateInstance(listType);
    }

    private static object CreateState()
    {
        Type stateType = GetAssemblyType("LhWorkStateDto");
        return stateType.GetMethod("CreateEmpty", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, null);
    }

    private static void AddWall(
        object state,
        string id,
        string name,
        Vector3 start,
        Vector3 end,
        float thickness,
        float height,
        float centerY,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint)
    {
        Type wallDtoType = GetAssemblyType("LhWorkWallDto");
        object wall = Activator.CreateInstance(wallDtoType);
        SetFieldValue(wall, "id", id);
        SetFieldValue(wall, "name", name);
        SetFieldValue(wall, "start", ToVectorDto(start));
        SetFieldValue(wall, "end", ToVectorDto(end));
        SetFieldValue(wall, "thickness", thickness);
        SetFieldValue(wall, "height", height);
        SetFieldValue(wall, "centerY", centerY);
        SetFieldValue(wall, "startVertexId", startVertexId);
        SetFieldValue(wall, "endVertexId", endVertexId);
        SetFieldValue(wall, "suppressStartHandle", suppressStartHandle);
        SetFieldValue(wall, "suppressEndHandle", suppressEndHandle);
        SetFieldValue(wall, "startSplitPoint", startSplitPoint);
        SetFieldValue(wall, "endSplitPoint", endSplitPoint);
        GetFieldValue<IList>(state, "walls").Add(wall);
    }

    private static void AddRoom(
        object state,
        string roomName,
        string roomTypeKey,
        string roomCode,
        string roomNativeCode,
        string floorTextureCode,
        string ceilingTextureCode,
        bool isManualRoom,
        Vector3[] boundaryVertices,
        string[] wallIds,
        bool manualWallSelectionEnabled,
        string[] manualWallIds)
    {
        object room = Activator.CreateInstance(GetAssemblyType("LhWorkRoomDto"));
        SetFieldValue(room, "name", roomName);
        SetFieldValue(room, "roomTypeKey", roomTypeKey);
        SetFieldValue(room, "roomCode", roomCode);
        SetFieldValue(room, "roomNativeCode", roomNativeCode);
        SetFieldValue(room, "floorTextureCode", floorTextureCode);
        SetFieldValue(room, "ceilingTextureCode", ceilingTextureCode);
        SetFieldValue(room, "isManualRoom", isManualRoom);
        SetFieldValue(room, "placementOffset", ToVectorDto(new Vector3(0.5f, 0f, 0.25f)));
        IList boundary = GetFieldValue<IList>(room, "boundaryVertices");
        for (int i = 0; i < boundaryVertices.Length; i++)
        {
            boundary.Add(ToVectorDto(boundaryVertices[i]));
        }

        IList roomWallIds = GetFieldValue<IList>(room, "wallIds");
        for (int i = 0; i < wallIds.Length; i++)
        {
            roomWallIds.Add(wallIds[i]);
        }

        SetFieldValue(room, "manualWallSelectionEnabled", manualWallSelectionEnabled);
        IList roomManualWallIds = GetFieldValue<IList>(room, "manualWallIds");
        for (int i = 0; i < manualWallIds.Length; i++)
        {
            roomManualWallIds.Add(manualWallIds[i]);
        }

        GetFieldValue<IList>(state, "rooms").Add(room);
    }

    private static void AddFurniture(object state, string catalogCode, string name, string roomName)
    {
        AddFurniture(state, catalogCode, string.Empty, string.Empty, name, roomName);
    }

    private static void AddFurniture(
        object state,
        string catalogCode,
        string exportCode,
        string nativeCode,
        string name,
        string roomName)
    {
        object furniture = Activator.CreateInstance(GetAssemblyType("LhWorkFurnitureDto"));
        SetFieldValue(furniture, "catalogCode", catalogCode);
        SetFieldValue(furniture, "exportCode", exportCode);
        SetFieldValue(furniture, "nativeCode", nativeCode);
        SetFieldValue(furniture, "name", name);
        SetFieldValue(furniture, "position", ToVectorDto(Vector3.zero));
        SetFieldValue(furniture, "eulerAngles", ToVectorDto(Vector3.zero));
        SetFieldValue(furniture, "localScale", ToVectorDto(Vector3.one));
        SetFieldValue(furniture, "isPlaced", true);
        SetFieldValue(furniture, "roomName", roomName);
        GetFieldValue<IList>(state, "furniture").Add(furniture);
    }

    private static object ToVectorDto(Vector3 value)
    {
        return GetAssemblyType("LhWorkVector3Dto")
            .GetMethod("FromVector3", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new object[] { value });
    }

    private GameObject CreateExistingWall()
    {
        GameObject existingWall = CreateWallObject("ExistingWall", wallRoot.transform);
        ConfigureWall(
            existingWall,
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            0.1f,
            2f,
            1f,
            1,
            2,
            false,
            false,
            false,
            false);
        return existingWall;
    }

    private void AssertExistingWallStillPresent()
    {
        Array walls = wallRoot.GetComponentsInChildren(GetAssemblyType("Wall"), true);
        Assert.That(walls, Has.Length.EqualTo(1));
        Assert.That(((Component)walls.GetValue(0)).name, Is.EqualTo("ExistingWall"));
    }

    private object CreateRoomManager()
    {
        Type roomManagerType = GetAssemblyType("RoomManager");
        roomManagerObject = new GameObject("RoomManager");
        return roomManagerObject.AddComponent(roomManagerType);
    }

    private object CreateHandleManager()
    {
        Type handleManagerType = GetAssemblyType("HandleManager");
        handleManagerObject = new GameObject("HandleManager");
        Component handleManager = handleManagerObject.AddComponent(handleManagerType);

        handleCanvasObject = new GameObject("_Handle");
        Canvas canvas = handleCanvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        SetPrivateFieldValue(handleManager, "wallRoot", wallRoot.transform);
        SetPrivateFieldValue(handleManager, "targetCanvas", canvas);
        return handleManager;
    }

    private static object CreateLoadServices(object handleManager)
    {
        Type servicesType = GetAssemblyType("LhWorkStateLoadServices");
        ConstructorInfo constructor = servicesType.GetConstructor(new[]
        {
            GetAssemblyType("HandleManager"),
            GetAssemblyType("WallLengthDisplay"),
            GetAssemblyType("WallOpeningPlacementManager"),
            GetAssemblyType("FurniturePlacementManager"),
            GetAssemblyType("DrawManager"),
        });
        Assert.That(constructor, Is.Not.Null);
        return constructor.Invoke(new[] { handleManager, null, null, null, null });
    }

    private int CountHandleRects()
    {
        int count = 0;
        Transform canvasTransform = handleCanvasObject.transform;
        for (int i = 0; i < canvasTransform.childCount; i++)
        {
            Transform child = canvasTransform.GetChild(i);
            if (child != null && child.name.StartsWith("Handle_Vertex_", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    private object CreateFurnitureCatalog(string code, GameObject prefab)
    {
        return CreateFurnitureCatalog(code, string.Empty, string.Empty, prefab);
    }

    private object CreateFurnitureCatalog(string code, string exportCode, string nativeCode, GameObject prefab)
    {
        Type catalogType = GetAssemblyType("FurnitureCatalog");
        Type itemType = GetAssemblyType("FurnitureCatalogItem");
        object catalog = ScriptableObject.CreateInstance(catalogType);
        furnitureCatalog = (UnityEngine.Object)catalog;
        object item = Activator.CreateInstance(itemType);
        SetFieldValue(item, "code", code);
        SetFieldValue(item, "exportCode", exportCode);
        SetFieldValue(item, "nativeCode", nativeCode);
        SetFieldValue(item, "prefab", prefab);
        GetFieldValue<IList>(catalog, "items").Add(item);
        return catalog;
    }

    private static GameObject CreateWallObject(string name, Transform parent)
    {
        Type factoryType = GetAssemblyType("WallObjectFactory");
        Type visualStateType = GetAssemblyType("WallVisualState");
        MethodInfo method = factoryType.GetMethod("CreateWallObject", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return (GameObject)method.Invoke(null, new[] { name, parent, null, Activator.CreateInstance(visualStateType) });
    }

    private static void ConfigureWall(
        GameObject wallObject,
        Vector3 start,
        Vector3 end,
        float thickness,
        float height,
        float centerY,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint)
    {
        Type wallDataType = GetAssemblyType("WallData");
        object wallData = Activator.CreateInstance(wallDataType, start, end, thickness, height, centerY);
        Type factoryType = GetAssemblyType("WallObjectFactory");
        MethodInfo method = factoryType.GetMethod("ConfigureWall", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        method.Invoke(
            null,
            new[]
            {
                wallObject,
                wallData,
                startVertexId,
                endVertexId,
                suppressStartHandle,
                suppressEndHandle,
                startSplitPoint,
                endSplitPoint,
                0.01f,
                null,
                false,
            });
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void SetPrivateFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target);
    }
}
