using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
        UnityEngine.Object.DestroyImmediate(wallRoot);
        UnityEngine.Object.DestroyImmediate(furnitureRoot);
    }

    [Test]
    public void Build_CapturesStandaloneWallGeometryAndFlags()
    {
        GameObject wallObject = CreateWallObject("Wall_A", wallRoot.transform);
        ConfigureWall(
            wallObject,
            new Vector3(0f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
            0.2f,
            3f,
            1.5f,
            10,
            11,
            true,
            false,
            false,
            true);

        Type builderType = GetAssemblyType("LhWorkStateBuilder");
        object state = builderType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new object[] { wallRoot.transform, null, furnitureRoot.transform });

        IList walls = GetFieldValue<IList>(state, "walls");
        Assert.That(walls, Has.Count.EqualTo(1));
        object wall = walls[0];
        Assert.That(GetFieldValue<string>(wall, "name"), Is.EqualTo("Wall_A"));
        Assert.That(ToVector3(GetFieldValue<object>(wall, "start")), Is.EqualTo(new Vector3(0f, 0f, 0f)));
        Assert.That(ToVector3(GetFieldValue<object>(wall, "end")), Is.EqualTo(new Vector3(2f, 0f, 0f)));
        Assert.That(GetFieldValue<float>(wall, "thickness"), Is.EqualTo(0.2f));
        Assert.That(GetFieldValue<float>(wall, "height"), Is.EqualTo(3f));
        Assert.That(GetFieldValue<float>(wall, "centerY"), Is.EqualTo(1.5f));
        Assert.That(GetFieldValue<int>(wall, "startVertexId"), Is.EqualTo(10));
        Assert.That(GetFieldValue<int>(wall, "endVertexId"), Is.EqualTo(11));
        Assert.That(GetFieldValue<bool>(wall, "suppressStartHandle"), Is.True);
        Assert.That(GetFieldValue<bool>(wall, "suppressEndHandle"), Is.False);
        Assert.That(GetFieldValue<bool>(wall, "startSplitPoint"), Is.False);
        Assert.That(GetFieldValue<bool>(wall, "endSplitPoint"), Is.True);
    }

    [Test]
    public void Build_SkipsWallPreview()
    {
        GameObject wallObject = CreateWallObject("WallPreview", wallRoot.transform);
        ConfigureWall(
            wallObject,
            new Vector3(0f, 0f, 0f),
            new Vector3(2f, 0f, 0f),
            0.2f,
            3f,
            1.5f,
            1,
            2,
            false,
            false,
            false,
            false);

        object state = BuildState(null);

        Assert.That(GetFieldValue<IList>(state, "walls"), Is.Empty);
    }

    [Test]
    public void Build_CollapsesOpeningContainerAndCapturesOpeningData()
    {
        GameObject containerObject = CreateOpeningContainer(
            "Wall_With_Opening",
            wallRoot.transform,
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            0.25f,
            3.2f,
            1.6f,
            100,
            101);
        GameObject firstSegment = CreateWallObject("Segment_A", containerObject.transform);
        ConfigureWall(firstSegment, new Vector3(0f, 0f, 0f), new Vector3(1.5f, 0f, 0f), 0.25f, 3.2f, 1.6f, 100, 200, false, true, false, true);
        SetWallId(firstSegment, "segment-a");
        GameObject secondSegment = CreateWallObject("Segment_B", containerObject.transform);
        ConfigureWall(secondSegment, new Vector3(2.5f, 0f, 0f), new Vector3(4f, 0f, 0f), 0.25f, 3.2f, 1.6f, 201, 101, true, false, true, false);
        SetWallId(secondSegment, "segment-b");
        CreateOpening(
            containerObject.transform,
            containerObject.GetComponent(GetAssemblyType("WallOpeningContainer")),
            "Door",
            "hinged-door",
            string.Empty,
            true,
            false,
            2f,
            0.9f,
            2.1f,
            0.05f,
            0.1f);

        object state = BuildState(null);

        IList walls = GetFieldValue<IList>(state, "walls");
        Assert.That(walls, Has.Count.EqualTo(1));
        object wall = walls[0];
        Assert.That(GetFieldValue<string>(wall, "id"), Is.EqualTo("segment-a"));
        Assert.That(GetFieldValue<string>(wall, "name"), Is.EqualTo("Wall_With_Opening"));
        Assert.That(ToVector3(GetFieldValue<object>(wall, "start")), Is.EqualTo(new Vector3(0f, 0f, 0f)));
        Assert.That(ToVector3(GetFieldValue<object>(wall, "end")), Is.EqualTo(new Vector3(4f, 0f, 0f)));
        Assert.That(GetFieldValue<float>(wall, "thickness"), Is.EqualTo(0.25f));
        Assert.That(GetFieldValue<float>(wall, "height"), Is.EqualTo(3.2f));
        Assert.That(GetFieldValue<float>(wall, "centerY"), Is.EqualTo(1.6f));
        Assert.That(GetFieldValue<int>(wall, "startVertexId"), Is.EqualTo(100));
        Assert.That(GetFieldValue<int>(wall, "endVertexId"), Is.EqualTo(101));

        IList openings = GetFieldValue<IList>(wall, "openings");
        Assert.That(openings, Has.Count.EqualTo(1));
        object opening = openings[0];
        Assert.That(GetFieldValue<string>(opening, "type"), Is.EqualTo("Door"));
        Assert.That(GetFieldValue<string>(opening, "doorTypeKey"), Is.EqualTo("hinged-door"));
        Assert.That(GetFieldValue<bool>(opening, "doorOpensRight"), Is.True);
        Assert.That(GetFieldValue<float>(opening, "centerDistance"), Is.EqualTo(2f));
        Assert.That(GetFieldValue<float>(opening, "width"), Is.EqualTo(0.9f));
        Assert.That(GetFieldValue<float>(opening, "height"), Is.EqualTo(2.1f));
        Assert.That(GetFieldValue<float>(opening, "depth"), Is.EqualTo(0.05f));
        Assert.That(GetFieldValue<float>(opening, "bottomY"), Is.EqualTo(0.1f));
    }

    [Test]
    public void Build_NormalizesRoomWallIdsForCollapsedOpeningContainer()
    {
        GameObject containerObject = CreateOpeningContainer(
            "Wall_With_Opening",
            wallRoot.transform,
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            0.25f,
            3.2f,
            1.6f,
            100,
            101);
        GameObject firstSegment = CreateWallObject("Segment_A", containerObject.transform);
        ConfigureWall(firstSegment, new Vector3(0f, 0f, 0f), new Vector3(1.5f, 0f, 0f), 0.25f, 3.2f, 1.6f, 100, 200, false, false, false, false);
        SetWallId(firstSegment, "segment-a");
        GameObject secondSegment = CreateWallObject("Segment_B", containerObject.transform);
        ConfigureWall(secondSegment, new Vector3(2.5f, 0f, 0f), new Vector3(4f, 0f, 0f), 0.25f, 3.2f, 1.6f, 201, 101, false, false, false, false);
        SetWallId(secondSegment, "segment-b");
        object roomManager = CreateRoomManagerWithRoom(
            furnitureRoot.transform,
            new[] { "segment-b", "segment-a", "segment-b", "external-wall" },
            new[] { "segment-a", "segment-b", "segment-a" });

        object state = BuildState(roomManager);

        IList rooms = GetFieldValue<IList>(state, "rooms");
        Assert.That(rooms, Has.Count.EqualTo(1));
        object room = rooms[0];
        CollectionAssert.AreEqual(new[] { "segment-a" }, ToStringArray(GetFieldValue<IList>(room, "wallIds")));
        CollectionAssert.AreEqual(new[] { "segment-a" }, ToStringArray(GetFieldValue<IList>(room, "manualWallIds")));
    }

    private object BuildState(object roomManager)
    {
        Type builderType = GetAssemblyType("LhWorkStateBuilder");
        return builderType.GetMethod("Build", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new[] { wallRoot.transform, roomManager, furnitureRoot.transform });
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

    private static GameObject CreateOpeningContainer(
        string name,
        Transform parent,
        Vector3 start,
        Vector3 end,
        float thickness,
        float height,
        float centerY,
        int startVertexId,
        int endVertexId)
    {
        Type containerType = GetAssemblyType("WallOpeningContainer");
        Type visualStateType = GetAssemblyType("WallVisualState");
        GameObject containerObject = new GameObject(name);
        containerObject.transform.SetParent(parent, false);
        object container = containerObject.AddComponent(containerType);
        MethodInfo initialize = containerType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(initialize, Is.Not.Null);
        initialize.Invoke(
            container,
            new[]
            {
                start,
                end,
                thickness,
                height,
                centerY,
                Activator.CreateInstance(visualStateType),
                startVertexId,
                endVertexId,
                false,
                false,
                false,
                false,
            });
        return containerObject;
    }

    private static void CreateOpening(
        Transform parent,
        object container,
        string typeName,
        string doorTypeKey,
        string windowTypeKey,
        bool doorOpensRight,
        bool doorVerticalFlip,
        float centerDistance,
        float width,
        float height,
        float depth,
        float bottomY)
    {
        Type openingType = GetAssemblyType("WallOpening");
        Type placementManagerType = GetAssemblyType("WallOpeningPlacementManager");
        Type enumType = placementManagerType.GetNestedType("OpeningPlacementType", BindingFlags.Public);
        Assert.That(enumType, Is.Not.Null);
        GameObject openingObject = new GameObject("Opening");
        openingObject.transform.SetParent(parent, false);
        object opening = openingObject.AddComponent(openingType);
        MethodInfo initialize = openingType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(initialize, Is.Not.Null);
        initialize.Invoke(
            opening,
            new[]
            {
                null,
                container,
                Enum.Parse(enumType, typeName),
                doorTypeKey,
                windowTypeKey,
                doorOpensRight,
                doorVerticalFlip,
                centerDistance,
                width,
                height,
                depth,
                bottomY,
            });
    }

    private static void SetWallId(GameObject wallObject, string id)
    {
        object wall = wallObject.GetComponent(GetAssemblyType("Wall"));
        object data = wall.GetType().GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)?.GetValue(wall);
        Assert.That(data, Is.Not.Null);
        PropertyInfo idProperty = data.GetType().GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
        Assert.That(idProperty, Is.Not.Null);
        idProperty.SetValue(data, id);
    }

    private static object CreateRoomManagerWithRoom(Transform parent, IEnumerable<string> wallIds, IEnumerable<string> manualWallIds)
    {
        Type roomManagerType = GetAssemblyType("RoomManager");
        Type roomType = GetAssemblyType("Room");
        GameObject roomManagerObject = new GameObject("RoomManager");
        roomManagerObject.transform.SetParent(parent, false);
        object roomManager = roomManagerObject.AddComponent(roomManagerType);
        GameObject roomObject = new GameObject("Room_A");
        roomObject.transform.SetParent(parent, false);
        object room = roomObject.AddComponent(roomType);
        object data = roomType.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance)?.GetValue(room);
        Assert.That(data, Is.Not.Null);
        data.GetType().GetMethod("SetWallIds", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(data, new object[] { wallIds });
        data.GetType().GetMethod("SetManualWallIds", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(data, new object[] { manualWallIds });

        FieldInfo allRoomsField = roomManagerType.GetField("allRooms", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(allRoomsField, Is.Not.Null);
        IList allRooms = (IList)allRoomsField.GetValue(roomManager);
        allRooms.Add(room);
        return roomManager;
    }

    private static string[] ToStringArray(IList values)
    {
        List<string> results = new List<string>();
        for (int i = 0; i < values.Count; i++)
        {
            results.Add((string)values[i]);
        }

        return results.ToArray();
    }

    private static Vector3 ToVector3(object target)
    {
        return (Vector3)target.GetType()
            .GetMethod("ToVector3", BindingFlags.Public | BindingFlags.Instance)
            ?.Invoke(target, null);
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
