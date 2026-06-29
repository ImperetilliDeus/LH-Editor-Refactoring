using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityObject = UnityEngine.Object;

public class DwgWallImportSceneApplierTests
{
    private readonly List<UnityObject> createdObjects = new List<UnityObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityObject.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void Apply_ClearsExistingManualRooms_WhenClearExistingRoomsEnabled()
    {
        GameObject wallRoot = CreateObject("Walls");
        GameObject managerObject = CreateObject("RoomManager");
        Component roomManager = managerObject.AddComponent(GetAssemblyType("RoomManager"));
        object room = roomManager.GetType().GetMethod("CreateRoomFromPolygon")?.Invoke(roomManager, new object[]
        {
            new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(2f, 0f, 2f),
                new Vector3(0f, 0f, 2f),
            },
            null,
        });
        Assert.That(room, Is.Not.Null);
        Assert.That(GetPropertyValue<bool>(room, "IsManualRoom"), Is.True);

        object context = Activator.CreateInstance(GetAssemblyType("DwgWallImportSceneApplyContext"));
        SetPropertyValue(context, "ImporterId", "test-importer");
        SetPropertyValue(context, "WallRoot", wallRoot.transform);
        SetPropertyValue(context, "RoomManager", roomManager);
        SetPropertyValue(context, "ClearExistingRooms", true);
        SetPropertyValue(context, "DestroyObject", (System.Action<UnityObject>)UnityObject.DestroyImmediate);
        object result = GetAssemblyType("DwgWallImportSceneApplier")
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new[]
            {
                CreateSegmentList(),
                context,
            });

        Assert.That(GetPropertyValue<int>(result, "RemovedRoomCount"), Is.EqualTo(1));
        IList<object> rooms = InvokeGetAllRooms(roomManager);
        Assert.That(rooms, Is.Empty);
        Assert.That((Component)room == null || ((Component)room).gameObject == null, Is.True);
    }

    [Test]
    public void Apply_ClearsExistingWallsWithoutDwgOwnership_WhenClearExistingWallsEnabled()
    {
        GameObject wallRoot = CreateObject("Walls");
        GameObject existingWall = new GameObject("ExistingWall");
        existingWall.transform.SetParent(wallRoot.transform, false);
        GameObject existingDwgWall = new GameObject("ExistingDwgWall");
        existingDwgWall.transform.SetParent(wallRoot.transform, false);
        Component ownership = existingDwgWall.AddComponent(GetAssemblyType("DwgImportedWallOwnership"));
        ownership.GetType().GetMethod("SetImporterId")?.Invoke(ownership, new object[] { "test-importer" });

        object context = Activator.CreateInstance(GetAssemblyType("DwgWallImportSceneApplyContext"));
        SetPropertyValue(context, "ImporterId", "test-importer");
        SetPropertyValue(context, "WallRoot", wallRoot.transform);
        SetPropertyValue(context, "ClearExistingWalls", true);
        SetPropertyValue(context, "DestroyObject", (System.Action<UnityObject>)UnityObject.DestroyImmediate);
        object result = GetAssemblyType("DwgWallImportSceneApplier")
            .GetMethod("Apply", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new[]
            {
                CreateSegmentList(),
                context,
            });

        Assert.That(GetPropertyValue<int>(result, "RemovedWallCount"), Is.EqualTo(2));
        Assert.That(wallRoot.transform.childCount, Is.EqualTo(0));
        Assert.That(existingWall == null, Is.True);
        Assert.That(existingDwgWall == null, Is.True);
    }

    private static object CreateSegmentList()
    {
        Type segmentType = GetAssemblyType("CadWallSegment");
        return Activator.CreateInstance(typeof(List<>).MakeGenericType(segmentType));
    }

    private static IList<object> InvokeGetAllRooms(Component roomManager)
    {
        System.Collections.IEnumerable rooms = roomManager.GetType()
            .GetMethod("GetAllRooms", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)
            ?.Invoke(roomManager, null) as System.Collections.IEnumerable;
        List<object> results = new List<object>();
        if (rooms == null)
        {
            return results;
        }

        foreach (object room in rooms)
        {
            results.Add(room);
        }

        return results;
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target);
    }

    private static void SetPropertyValue(object target, string propertyName, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        property.SetValue(target, value);
    }
}
