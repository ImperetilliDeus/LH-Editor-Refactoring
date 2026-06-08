using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class SceneHierarchyTreeModelTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void BuildRows_RendersStandaloneWallsAtRoot()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Component first = CreateWall("Wall_001", "wall-a", wallRoot);
        Component second = CreateWall("Wall_002", "wall-b", wallRoot);

        IList rows = BuildRows(wallRoot, CreateRoomList());

        Assert.That(rows, Has.Count.EqualTo(2));
        AssertRow(rows[0], "Wall", 0, "Wall_001", first);
        AssertRow(rows[1], "Wall", 0, "Wall_002", second);
    }

    [Test]
    public void BuildRows_RendersAssignedWallsUnderRoomOnly()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Component assigned = CreateWall("Wall_001", "wall-a", wallRoot);
        Component standalone = CreateWall("Wall_002", "wall-b", wallRoot);
        Component room = CreateRoom("RoomObject", "Living Room", assigned);

        IList rows = BuildRows(wallRoot, CreateRoomList(room));

        Assert.That(rows, Has.Count.EqualTo(3));
        AssertRow(rows[0], "Room", 0, "Room (Living Room)", null);
        AssertRow(rows[1], "Wall", 1, "Wall_001", assigned);
        AssertRow(rows[2], "Wall", 0, "Wall_002", standalone);
    }

    [Test]
    public void BuildRows_UsesRoomObjectName_WhenRoomNameIsEmpty()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Component assigned = CreateWall("Wall_001", "wall-a", wallRoot);
        Component room = CreateRoom("Room_A", string.Empty, assigned);

        IList rows = BuildRows(wallRoot, CreateRoomList(room));

        Assert.That(GetProperty<string>(rows[0], "DisplayName"), Is.EqualTo("Room_A"));
    }

    [Test]
    public void BuildRows_CollapsesOpeningContainerSegmentsToOneLogicalWall()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        GameObject containerObject = CreateObject("Wall_With_Opening");
        containerObject.transform.SetParent(wallRoot, false);
        containerObject.AddComponent(GetAssemblyType("WallOpeningContainer"));
        CreateWall("Segment_A", "segment-a", containerObject.transform);
        CreateWall("Segment_B", "segment-b", containerObject.transform);

        IList rows = BuildRows(wallRoot, CreateRoomList());

        Assert.That(rows, Has.Count.EqualTo(1));
        AssertRow(rows[0], "Wall", 0, "Wall_With_Opening", null);
    }

    [Test]
    public void BuildRows_SkipsWallPreview()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        CreateWall("WallPreview", "preview", wallRoot);
        Component wall = CreateWall("Wall_001", "wall-a", wallRoot);

        IList rows = BuildRows(wallRoot, CreateRoomList());

        Assert.That(rows, Has.Count.EqualTo(1));
        AssertRow(rows[0], "Wall", 0, "Wall_001", wall);
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private Component CreateWall(string name, string id, Transform parent)
    {
        Type wallType = GetAssemblyType("Wall");
        Type wallDataType = GetAssemblyType("WallData");
        GameObject wallObject = CreateObject(name);
        wallObject.transform.SetParent(parent, false);
        Component wall = wallObject.AddComponent(wallType);
        object wallData = Activator.CreateInstance(
            wallDataType,
            Vector3.zero,
            Vector3.forward,
            0.2f,
            3f,
            1.5f);
        wallType.GetMethod("Initialize")?.Invoke(wall, new[] { wallData });
        object data = GetProperty<object>(wall, "Data");
        PropertyInfo idProperty = wallDataType.GetProperty("id");
        Assert.That(idProperty, Is.Not.Null);
        idProperty.SetValue(data, id);
        return wall;
    }

    private Component CreateRoom(string objectName, string roomName, params Component[] walls)
    {
        Type roomType = GetAssemblyType("Room");
        Type wallType = GetAssemblyType("Wall");
        Type roomGeometryType = GetAssemblyType("RoomGeometry");
        GameObject roomObject = CreateObject(objectName);
        Component room = roomObject.AddComponent(roomType);
        object wallSet = CreateWallSet(wallType, walls);
        object geometry = Activator.CreateInstance(roomGeometryType);
        roomGeometryType.GetField("Center")?.SetValue(geometry, Vector3.zero);
        roomGeometryType.GetField("Area")?.SetValue(geometry, 1f);
        roomGeometryType.GetField("WallCount")?.SetValue(geometry, 4);

        List<Vector3> polygon = new List<Vector3>
        {
            Vector3.zero,
            Vector3.forward,
            Vector3.right + Vector3.forward,
            Vector3.right,
        };
        roomType.GetMethod("Initialize", new[] { wallSet.GetType(), roomGeometryType, typeof(IReadOnlyList<Vector3>), typeof(bool) })
            ?.Invoke(room, new[] { wallSet, geometry, polygon, true });
        roomType.GetMethod("SetRoomName")?.Invoke(room, new object[] { roomName });
        return room;
    }

    private static IList BuildRows(Transform wallRoot, object rooms)
    {
        Type modelType = GetAssemblyType("SceneHierarchyTreeModel");
        Type roomType = GetAssemblyType("Room");
        Type enumerableRoomType = typeof(IEnumerable<>).MakeGenericType(roomType);
        object rows = modelType.GetMethod("BuildRows", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Transform), enumerableRoomType }, null)
            ?.Invoke(null, new[] { wallRoot, rooms });
        Assert.That(rows, Is.InstanceOf<IList>());
        return (IList)rows;
    }

    private static object CreateRoomList(params Component[] rooms)
    {
        Type roomType = GetAssemblyType("Room");
        Type listType = typeof(List<>).MakeGenericType(roomType);
        IList list = (IList)Activator.CreateInstance(listType);
        for (int i = 0; i < rooms.Length; i++)
        {
            list.Add(rooms[i]);
        }

        return list;
    }

    private static object CreateWallSet(Type wallType, params Component[] walls)
    {
        Type wallSetType = typeof(HashSet<>).MakeGenericType(wallType);
        object wallSet = Activator.CreateInstance(wallSetType);
        MethodInfo addMethod = wallSetType.GetMethod("Add");
        Assert.That(addMethod, Is.Not.Null);

        for (int i = 0; i < walls.Length; i++)
        {
            addMethod.Invoke(wallSet, new object[] { walls[i] });
        }

        return wallSet;
    }

    private static void AssertRow(object row, string kind, int depth, string displayName, Component representativeWall)
    {
        Assert.That(GetProperty<object>(row, "Kind").ToString(), Is.EqualTo(kind));
        Assert.That(GetProperty<int>(row, "Depth"), Is.EqualTo(depth));
        Assert.That(GetProperty<string>(row, "DisplayName"), Is.EqualTo(displayName));
        if (representativeWall != null)
        {
            Assert.That(GetProperty<object>(row, "RepresentativeWall"), Is.EqualTo(representativeWall));
        }
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target);
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }
}
