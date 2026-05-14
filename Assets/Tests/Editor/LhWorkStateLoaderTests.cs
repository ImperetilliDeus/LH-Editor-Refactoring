using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class LhWorkStateLoaderTests
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
        UnityEngine.Object.DestroyImmediate(wallRoot);
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

    private static object Load(object state, Transform wallRoot, object roomManager, Transform furnitureRoot, object furnitureCatalog)
    {
        Type loaderType = GetAssemblyType("LhWorkStateLoader");
        MethodInfo method = loaderType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new[] { state, wallRoot, roomManager, furnitureRoot, furnitureCatalog });
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

    private static object ToVectorDto(Vector3 value)
    {
        return GetAssemblyType("LhWorkVector3Dto")
            .GetMethod("FromVector3", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new object[] { value });
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
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
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
