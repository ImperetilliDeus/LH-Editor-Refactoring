using System;
using System.Collections;
using System.Collections.Generic;
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
}
