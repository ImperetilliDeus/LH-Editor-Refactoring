using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RoomManagerTests
{
    private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            UnityEngine.Object target = createdObjects[i];
            if (target != null)
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void FindRoomByWallSet_ReturnsRoom_ForEquivalentWallSetsWithDifferentInsertionOrder()
    {
        Type roomManagerType = GetAssemblyType("RoomManager");
        Type wallType = GetAssemblyType("Wall");
        Type roomType = GetAssemblyType("Room");

        Component manager = CreateComponent("RoomManager", roomManagerType);
        Component wallA = CreateWall("WallA", wallType);
        Component wallB = CreateWall("WallB", wallType);

        object storedWallSet = CreateWallSet(wallType, wallA, wallB);
        Component room = CreateRoom("Room", roomType, storedWallSet);
        AddRoomToManager(manager, room);
        InvokePrivate(manager, "RebuildRoomLookup");

        object lookupWallSet = CreateWallSet(wallType, wallB, wallA);
        object resolved = roomManagerType.GetMethod("FindRoomByWallSet")?.Invoke(manager, new[] { lookupWallSet });

        Assert.That(resolved, Is.SameAs(room));
    }

    private Component CreateComponent(string name, Type componentType)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent(componentType);
    }

    private Component CreateWall(string name, Type wallType)
    {
        Component wall = CreateComponent(name, wallType);
        Type wallDataType = GetAssemblyType("WallData");
        object wallData = Activator.CreateInstance(
            wallDataType,
            Vector3.zero,
            Vector3.right,
            0.1f,
            3f,
            0f);
        wallType.GetMethod("Initialize")?.Invoke(wall, new[] { wallData });
        return wall;
    }

    private Component CreateRoom(string name, Type roomType, object wallSet)
    {
        Component room = CreateComponent(name, roomType);
        List<Vector3> polygon =
            new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
            };

        Type polygonUtilityType = GetAssemblyType("PolygonUtility");
        object geometry = polygonUtilityType.GetMethod("CalculateGeometry", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new object[] { polygon });
        roomType.GetMethod("Initialize", new[] { wallSet.GetType(), geometry?.GetType(), typeof(IReadOnlyList<Vector3>), typeof(bool) })
            ?.Invoke(room, new[] { wallSet, geometry, polygon, false });
        return room;
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

    private static void AddRoomToManager(Component manager, Component room)
    {
        FieldInfo field = manager.GetType().GetField("allRooms", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        IList list = field.GetValue(manager) as IList;
        Assert.That(list, Is.Not.Null);
        list.Add(room);
    }

    private static void InvokePrivate(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, null);
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }
}
