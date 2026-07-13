using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LhSceneExportBuilderTests
{
    private GameObject openingObject;

    [TearDown]
    public void TearDown()
    {
        if (openingObject != null)
        {
            UnityEngine.Object.DestroyImmediate(openingObject);
        }
    }

    [Test]
    public void LegacyWindowExport_MapsKoreanWindowNameToMobileViewerPresetCode()
    {
        Component opening = CreateWindowOpening("\uCC3D\uBB38");

        object window = InvokeBuildWindow(opening, true);

        Assert.That(GetFieldValue<string>(window, "code"), Is.EqualTo("W001"));
    }

    [Test]
    public void BuildLegacy_UsesManualWallSelectionWhenCalculatingCeilingHeight()
    {
        var createdObjects = new List<GameObject>();
        try
        {
            Type wallType = GetAssemblyType("Wall");
            Array walls = Array.CreateInstance(wallType, 4);
            walls.SetValue(CreateWall(createdObjects, "wall01", "manual-a", new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f)), 0);
            walls.SetValue(CreateWall(createdObjects, "wall02", "manual-b", new Vector3(1f, 0f, -1f), new Vector3(1f, 0f, 1f)), 1);
            walls.SetValue(CreateWall(createdObjects, "wall03", "manual-c", new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f)), 2);
            walls.SetValue(CreateWall(createdObjects, "wall04", "manual-d", new Vector3(-1f, 0f, 1f), new Vector3(-1f, 0f, -1f)), 3);

            var vertices = new List<Vector3>
            {
                new Vector3(-1f, 0f, -1f),
                new Vector3(1f, 0f, -1f),
                new Vector3(1f, 0f, 1f),
                new Vector3(-1f, 0f, 1f),
            };
            GameObject roomObject = new GameObject("ManualRoom");
            createdObjects.Add(roomObject);
            Type roomType = GetAssemblyType("Room");
            Component room = roomObject.AddComponent(roomType);
            object wallSet = CreateWallSet(wallType, walls);
            object geometry = GetAssemblyType("PolygonUtility")
                .GetMethod("CalculateGeometry", BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[] { vertices });
            Assert.That(geometry, Is.Not.Null);
            roomType.GetMethod(
                    "Initialize",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { wallSet.GetType(), geometry.GetType(), typeof(IReadOnlyList<Vector3>), typeof(bool) },
                    null)
                ?.Invoke(room, new[] { wallSet, geometry, vertices, true });
            roomType.GetMethod("SetPlacementOffset", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(room, new object[] { new Vector3(0f, 0.01f, 0f) });
            object roomData = roomType.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)?.GetValue(room);
            roomData.GetType().GetMethod("SetWallIds", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(roomData, new object[] { new[] { "stale-a", "stale-b", "stale-c", "stale-d" } });
            roomType.GetMethod("SetManualWallIds", BindingFlags.Public | BindingFlags.Instance)
                ?.Invoke(room, new object[] { new[] { "manual-a", "manual-b", "manual-c", "manual-d" } });

            Array rooms = Array.CreateInstance(roomType, 1);
            rooms.SetValue(room, 0);
            object scene = InvokeBuildLegacy(Vector3.zero, walls, rooms);

            Assert.That(GetFirstRoomCeilingPositionY(scene), Is.EqualTo(22f).Within(0.0001f));
        }
        finally
        {
            for (int i = 0; i < createdObjects.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }
    }

    [Test]
    public void BuildLegacy_MatchesMobileViewerOldContractSnapshot()
    {
        var createdObjects = new List<GameObject>();
        try
        {
            Type wallType = GetAssemblyType("Wall");
            Array walls = CreateContractWalls(createdObjects, wallType);
            Component room = CreateContractRoom(createdObjects, wallType, walls);
            CreateContractFurniture(createdObjects, room);

            Type roomType = GetAssemblyType("Room");
            Array rooms = Array.CreateInstance(roomType, 1);
            rooms.SetValue(room, 0);
            object scene = InvokeBuildLegacy(new Vector3(0.5f, 0f, -0.5f), walls, rooms);
            LegacyContractSnapshot snapshot = LegacyContractSnapshot.FromScene(scene);
            string actualJson = JsonUtility.ToJson(snapshot, true).Replace("\r\n", "\n");
            string expectedPath = Path.Combine(Application.dataPath, "Tests/Editor/Fixtures/legacy-mobile-viewer-contract.snapshot.json");
            string expectedJson = File.ReadAllText(expectedPath).Replace("\r\n", "\n");

            Assert.That(actualJson.TrimEnd(), Is.EqualTo(expectedJson.TrimEnd()));
        }
        finally
        {
            DestroyObjects(createdObjects);
        }
    }

    [Test]
    public void ValidateLegacy_ReportsMissingRoomCode()
    {
        object scene = CreateLegacySceneDto();
        object room = CreateLegacyRoomDto("Room Without Code", string.Empty);
        AddToList(GetFieldValue<IList>(scene, "roomData"), room);

        object result = InvokeValidateLegacy(scene);

        Assert.That(GetPropertyValue<bool>(result, "IsValid"), Is.False);
        Assert.That(GetPropertyValue<IList>(result, "Errors"), Does.Contain("roomData[0] 'Room Without Code' is missing code."));
    }

    [Test]
    public void ValidateLegacy_ReportsMissingFurnitureDefectTupleFields()
    {
        object scene = CreateLegacySceneDto();
        object room = CreateLegacyRoomDto("Living", "900");
        object furniture = CreateLegacyFurnitureDto("SOFA");
        object defect = CreateFurnitureDefectDto("900", string.Empty, "080");
        AddToList(GetFieldValue<IList>(furniture, "defects"), defect);
        AddToList(GetFieldValue<IList>(room, "furnish"), furniture);
        AddToList(GetFieldValue<IList>(scene, "roomData"), room);

        object result = InvokeValidateLegacy(scene);

        Assert.That(GetPropertyValue<bool>(result, "IsValid"), Is.False);
        Assert.That(GetPropertyValue<IList>(result, "Errors"), Does.Contain("roomData[0].furnish[0].defects[0] is missing locCd."));
    }

    private Component CreateWindowOpening(string windowTypeKey)
    {
        openingObject = new GameObject("WindowOpening");
        Type openingType = GetAssemblyType("WallOpening");
        Type placementManagerType = GetAssemblyType("WallOpeningPlacementManager");
        Type enumType = placementManagerType.GetNestedType("OpeningPlacementType", BindingFlags.Public);
        Component opening = openingObject.AddComponent(openingType);
        object windowType = Enum.Parse(enumType, "Window");

        openingType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance)?.Invoke(
            opening,
            new[]
            {
                null,
                null,
                windowType,
                string.Empty,
                windowTypeKey,
                false,
                false,
                0f,
                1f,
                1f,
                1f,
                0f,
            });
        return opening;
    }

    private static Component CreateWall(List<GameObject> createdObjects, string name, string id, Vector3 start, Vector3 end)
    {
        Type wallType = GetAssemblyType("Wall");
        Type wallDataType = GetAssemblyType("WallData");
        GameObject wallObject = new GameObject(name);
        createdObjects.Add(wallObject);
        Component wall = wallObject.AddComponent(wallType);
        object wallData = Activator.CreateInstance(wallDataType, start, end, 1.5f, 22f, 11.01f);
        wallDataType.GetProperty("id", BindingFlags.Public | BindingFlags.Instance)?.SetValue(wallData, id);
        wallType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance)?.Invoke(wall, new[] { wallData });
        return wall;
    }

    private static object CreateWallSet(Type wallType, Array walls)
    {
        Type wallSetType = typeof(HashSet<>).MakeGenericType(wallType);
        object wallSet = Activator.CreateInstance(wallSetType);
        MethodInfo addMethod = wallSetType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < walls.Length; i++)
        {
            addMethod.Invoke(wallSet, new[] { walls.GetValue(i) });
        }

        return wallSet;
    }

    private static Array CreateContractWalls(List<GameObject> createdObjects, Type wallType)
    {
        Array walls = Array.CreateInstance(wallType, 4);
        walls.SetValue(CreateWall(createdObjects, "wall01", "contract-a", new Vector3(-1f, 0f, -1f), new Vector3(1f, 0f, -1f)), 0);
        walls.SetValue(CreateWall(createdObjects, "wall02", "contract-b", new Vector3(1f, 0f, -1f), new Vector3(1f, 0f, 1f)), 1);
        walls.SetValue(CreateWall(createdObjects, "wall03", "contract-c", new Vector3(1f, 0f, 1f), new Vector3(-1f, 0f, 1f)), 2);
        walls.SetValue(CreateWall(createdObjects, "wall04", "contract-d", new Vector3(-1f, 0f, 1f), new Vector3(-1f, 0f, -1f)), 3);
        return walls;
    }

    private static Component CreateContractRoom(List<GameObject> createdObjects, Type wallType, Array walls)
    {
        var vertices = new List<Vector3>
        {
            new Vector3(-1f, 0f, -1f),
            new Vector3(1f, 0f, -1f),
            new Vector3(1f, 0f, 1f),
            new Vector3(-1f, 0f, 1f),
        };

        GameObject roomObject = new GameObject("LivingRoom");
        createdObjects.Add(roomObject);
        Type roomType = GetAssemblyType("Room");
        Component room = roomObject.AddComponent(roomType);
        object wallSet = CreateWallSet(wallType, walls);
        object geometry = GetAssemblyType("PolygonUtility")
            .GetMethod("CalculateGeometry", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new object[] { vertices });
        roomType.GetMethod(
                "Initialize",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { wallSet.GetType(), geometry.GetType(), typeof(IReadOnlyList<Vector3>), typeof(bool) },
                null)
            ?.Invoke(room, new[] { wallSet, geometry, vertices, true });
        roomType.GetMethod("SetRoomName", BindingFlags.Public | BindingFlags.Instance)?.Invoke(room, new object[] { "Living" });
        roomType.GetMethod("SetRoomCode", BindingFlags.Public | BindingFlags.Instance)?.Invoke(room, new object[] { "900" });
        roomType.GetMethod("SetFloorTextureCode", BindingFlags.Public | BindingFlags.Instance)?.Invoke(room, new object[] { "F001" });
        roomType.GetMethod("SetCeilingTextureCode", BindingFlags.Public | BindingFlags.Instance)?.Invoke(room, new object[] { "C001" });
        return room;
    }

    private static Component CreateContractFurniture(List<GameObject> createdObjects, Component room)
    {
        GameObject furnitureObject = new GameObject("Sofa");
        createdObjects.Add(furnitureObject);
        furnitureObject.transform.position = Vector3.zero;
        Type instanceType = GetAssemblyType("FurnitureInstance");
        Type itemType = GetAssemblyType("FurnitureCatalogItem");
        Type defectType = GetAssemblyType("FurnitureDefectCatalogEntry");
        Component instance = furnitureObject.AddComponent(instanceType);
        object item = Activator.CreateInstance(itemType);
        SetFieldValue(item, "code", "SOFA_INTERNAL");
        SetFieldValue(item, "exportCode", "SOFA_EXPORT");
        SetFieldValue(item, "nativeCode", "SOFA_NATIVE");
        SetFieldValue(item, "boundsSize", Vector3.one * 0.25f);
        IList defects = GetFieldValue<IList>(item, "defects");
        object defect = Activator.CreateInstance(defectType);
        SetFieldValue(defect, "mntnCd", "900");
        SetFieldValue(defect, "locCd", "2");
        SetFieldValue(defect, "mtrlCd", "080");
        AddToList(defects, defect);
        instanceType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance)?.Invoke(instance, new[] { item });
        instanceType.GetMethod("SetCurrentRoom", BindingFlags.Public | BindingFlags.Instance)?.Invoke(instance, new object[] { room });
        instanceType.GetMethod("SetPlaced", BindingFlags.Public | BindingFlags.Instance)?.Invoke(instance, new object[] { true });
        return instance;
    }

    private static object CreateLegacySceneDto()
    {
        Type sceneType = GetAssemblyType("LH.Schema.LhLegacySceneDto");
        Type wallType = GetAssemblyType("LH.Schema.LhLegacyWallDto");
        Type roomType = GetAssemblyType("LH.Schema.LhLegacyRoomDto");
        object scene = Activator.CreateInstance(sceneType);
        SetFieldValue(scene, "startPoint", CreateVector3Dto(Vector3.zero));
        SetFieldValue(scene, "wallData", CreateList(wallType));
        SetFieldValue(scene, "roomData", CreateList(roomType));
        return scene;
    }

    private static object CreateLegacyRoomDto(string name, string code)
    {
        Type roomType = GetAssemblyType("LH.Schema.LhLegacyRoomDto");
        object room = Activator.CreateInstance(roomType);
        SetFieldValue(room, "name", name);
        SetFieldValue(room, "code", code);
        SetFieldValue(room, "position", CreateVector3Dto(Vector3.zero));
        SetFieldValue(room, "angle", CreateVector3Dto(Vector3.zero));
        SetFieldValue(room, "scale", CreateVector3Dto(Vector3.one));
        IList walls = CreateList(typeof(int));
        AddToList(walls, 1);
        SetFieldValue(room, "walls", walls);
        SetFieldValue(room, "floor", CreateValidSurface());
        SetFieldValue(room, "ceil", CreateValidSurface());
        SetFieldValue(room, "furnish", CreateList(GetAssemblyType("LH.Schema.LhLegacyFurnitureDto")));
        return room;
    }

    private static object CreateLegacyFurnitureDto(string code)
    {
        Type furnitureType = GetAssemblyType("LH.Schema.LhLegacyFurnitureDto");
        object furniture = Activator.CreateInstance(furnitureType);
        SetFieldValue(furniture, "code", code);
        SetFieldValue(furniture, "position", CreateVector3Dto(Vector3.zero));
        SetFieldValue(furniture, "angle", CreateVector3Dto(Vector3.zero));
        SetFieldValue(furniture, "scale", CreateVector3Dto(Vector3.one));
        SetFieldValue(furniture, "defects", CreateList(GetAssemblyType("LH.Schema.LhFurnitureDefectDto")));
        return furniture;
    }

    private static object CreateFurnitureDefectDto(string mntnCd, string locCd, string mtrlCd)
    {
        Type defectType = GetAssemblyType("LH.Schema.LhFurnitureDefectDto");
        object defect = Activator.CreateInstance(defectType);
        SetFieldValue(defect, "mntnCd", mntnCd);
        SetFieldValue(defect, "locCd", locCd);
        SetFieldValue(defect, "mtrlCd", mtrlCd);
        return defect;
    }

    private static object CreateValidSurface()
    {
        Type surfaceType = GetAssemblyType("LH.Schema.LhSurfaceDto");
        object surface = Activator.CreateInstance(surfaceType);
        SetFieldValue(surface, "position", CreateVector3Dto(Vector3.zero));
        SetFieldValue(surface, "angle", CreateVector3Dto(Vector3.zero));
        SetFieldValue(surface, "scale", CreateVector3Dto(Vector3.one));
        SetFieldValue(surface, "meshType", 0);
        SetFieldValue(surface, "mesh", CreateEmptyMeshDto());
        SetFieldValue(surface, "texture", string.Empty);
        return surface;
    }

    private static object CreateEmptyMeshDto()
    {
        Type meshType = GetAssemblyType("LH.Schema.LhMeshDto");
        object mesh = Activator.CreateInstance(meshType);
        SetFieldValue(mesh, "vertices", CreateList(GetAssemblyType("LH.Schema.LhVector3Dto")));
        SetFieldValue(mesh, "triangles", CreateList(typeof(int)));
        SetFieldValue(mesh, "normals", CreateList(GetAssemblyType("LH.Schema.LhVector3Dto")));
        SetFieldValue(mesh, "uvs", CreateList(GetAssemblyType("LH.Schema.LhVector2Dto")));
        return mesh;
    }

    private static object CreateVector3Dto(Vector3 value)
    {
        Type vectorType = GetAssemblyType("LH.Schema.LhVector3Dto");
        object vector = Activator.CreateInstance(vectorType);
        SetFieldValue(vector, "x", value.x);
        SetFieldValue(vector, "y", value.y);
        SetFieldValue(vector, "z", value.z);
        return vector;
    }

    private static IList CreateList(Type itemType)
    {
        return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType));
    }

    private static void AddToList(IList list, object value)
    {
        Assert.That(list, Is.Not.Null);
        list.Add(value);
    }

    private static object InvokeValidateLegacy(object scene)
    {
        Type validatorType = GetAssemblyType("LH.Export.LhSceneExportValidator");
        MethodInfo method = validatorType.GetMethod("ValidateLegacy", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new[] { scene });
    }

    private static void DestroyObjects(List<GameObject> createdObjects)
    {
        for (int i = 0; i < createdObjects.Count; i++)
        {
            UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }
    }

    private static object InvokeBuildWindow(Component opening, bool legacyExact)
    {
        Type builderType = GetAssemblyType("LH.Export.LhSceneExportBuilder");
        MethodInfo method = builderType.GetMethod(
            "BuildWindow",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(
            null,
            new object[] { opening, Vector3.zero, Quaternion.identity, Vector3.one, legacyExact });
    }

    private static object InvokeBuildLegacy(Vector3 startPoint, Array walls, Array rooms)
    {
        Type builderType = GetAssemblyType("LH.Export.LhSceneExportBuilder");
        MethodInfo method = builderType.GetMethod("BuildLegacy", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new object[] { startPoint, walls, rooms });
    }

    private static float GetFirstRoomCeilingPositionY(object scene)
    {
        IList roomData = GetFieldValue<IList>(scene, "roomData");
        object room = roomData[0];
        object ceil = GetFieldValue<object>(room, "ceil");
        object position = GetFieldValue<object>(ceil, "position");
        return Convert.ToSingle(GetFieldValue<object>(position, "y"));
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    [Serializable]
    private class LegacyContractSnapshot
    {
        public bool hasVersionField;
        public int wallCount;
        public int roomCount;
        public string firstRoomCode;
        public int firstRoomWallCount;
        public bool firstRoomHasFloor;
        public bool firstRoomHasCeil;
        public int firstRoomFurnishCount;
        public string firstFurnitureCode;
        public int firstFurnitureDefectCount;
        public string firstFurnitureFirstDefectMntnCd;
        public string firstFurnitureFirstDefectLocCd;
        public string firstFurnitureFirstDefectMtrlCd;

        public static LegacyContractSnapshot FromScene(object scene)
        {
            IList wallData = GetFieldValue<IList>(scene, "wallData");
            IList roomData = GetFieldValue<IList>(scene, "roomData");
            object firstRoom = roomData[0];
            IList roomWalls = GetFieldValue<IList>(firstRoom, "walls");
            IList furnish = GetFieldValue<IList>(firstRoom, "furnish");
            object firstFurniture = furnish[0];
            IList defects = GetFieldValue<IList>(firstFurniture, "defects");
            object firstDefect = defects[0];

            return new LegacyContractSnapshot
            {
                hasVersionField = scene.GetType().GetField("version", BindingFlags.Public | BindingFlags.Instance) != null,
                wallCount = wallData.Count,
                roomCount = roomData.Count,
                firstRoomCode = GetFieldValue<string>(firstRoom, "code"),
                firstRoomWallCount = roomWalls.Count,
                firstRoomHasFloor = firstRoom.GetType().GetField("floor", BindingFlags.Public | BindingFlags.Instance) != null,
                firstRoomHasCeil = firstRoom.GetType().GetField("ceil", BindingFlags.Public | BindingFlags.Instance) != null,
                firstRoomFurnishCount = furnish.Count,
                firstFurnitureCode = GetFieldValue<string>(firstFurniture, "code"),
                firstFurnitureDefectCount = defects.Count,
                firstFurnitureFirstDefectMntnCd = GetFieldValue<string>(firstDefect, "mntnCd"),
                firstFurnitureFirstDefectLocCd = GetFieldValue<string>(firstDefect, "locCd"),
                firstFurnitureFirstDefectMtrlCd = GetFieldValue<string>(firstDefect, "mtrlCd"),
            };
        }
    }
}
