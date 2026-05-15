using System;
using System.Collections;
using System.Reflection;
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
        UnityEngine.Object.DestroyImmediate(wallRoot);
    }

    [Test]
    public void Load_RestoresDoorOpeningOnWall()
    {
        object state = CreateState();
        object wall = AddWall(
            state,
            "wall-with-door",
            "WallWithDoor",
            Vector3.zero,
            new Vector3(5f, 0f, 0f));
        AddOpening(
            wall,
            "Door",
            "Pass",
            string.Empty,
            false,
            false,
            2.5f,
            0.9f,
            2.1f,
            0.1f,
            0f);

        object result = Load(state, wallRoot.transform);

        Assert.That(GetPropertyValue<bool>(result, "Success"), Is.True);
        Component container = (Component)wallRoot.GetComponentInChildren(GetAssemblyType("WallOpeningContainer"), true);
        Assert.That(container, Is.Not.Null);
        Component opening = (Component)wallRoot.GetComponentInChildren(GetAssemblyType("WallOpening"), true);
        Assert.That(opening, Is.Not.Null);
        Assert.That(GetPropertyValue<object>(opening, "Type").ToString(), Is.EqualTo("Door"));
        Assert.That(GetPropertyValue<string>(opening, "DoorTypeKey"), Is.EqualTo("Pass"));
        Assert.That(GetPropertyValue<float>(opening, "CenterDistance"), Is.EqualTo(2.5f));
        Assert.That(GetPropertyValue<float>(opening, "Width"), Is.EqualTo(0.9f));
        Assert.That(GetPropertyValue<float>(opening, "Height"), Is.EqualTo(2.1f));
        Assert.That(GetPropertyValue<float>(opening, "Depth"), Is.EqualTo(0.1f));
        Assert.That(GetPropertyValue<float>(opening, "BottomY"), Is.EqualTo(0f));
    }

    private static object Load(object state, Transform wallRoot)
    {
        Type loaderType = GetAssemblyType("LhWorkStateLoader");
        MethodInfo method = loaderType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new object[] { state, wallRoot, null, null, null });
    }

    private static object CreateState()
    {
        Type stateType = GetAssemblyType("LhWorkStateDto");
        return stateType.GetMethod("CreateEmpty", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, null);
    }

    private static object AddWall(object state, string id, string name, Vector3 start, Vector3 end)
    {
        object wall = Activator.CreateInstance(GetAssemblyType("LhWorkWallDto"));
        SetFieldValue(wall, "id", id);
        SetFieldValue(wall, "name", name);
        SetFieldValue(wall, "start", ToVectorDto(start));
        SetFieldValue(wall, "end", ToVectorDto(end));
        SetFieldValue(wall, "thickness", 0.2f);
        SetFieldValue(wall, "height", 3f);
        SetFieldValue(wall, "centerY", 1.5f);
        GetFieldValue<IList>(state, "walls").Add(wall);
        return wall;
    }

    private static void AddOpening(
        object wall,
        string type,
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
        object opening = Activator.CreateInstance(GetAssemblyType("LhWorkOpeningDto"));
        SetFieldValue(opening, "type", type);
        SetFieldValue(opening, "doorTypeKey", doorTypeKey);
        SetFieldValue(opening, "windowTypeKey", windowTypeKey);
        SetFieldValue(opening, "doorOpensRight", doorOpensRight);
        SetFieldValue(opening, "doorVerticalFlip", doorVerticalFlip);
        SetFieldValue(opening, "centerDistance", centerDistance);
        SetFieldValue(opening, "width", width);
        SetFieldValue(opening, "height", height);
        SetFieldValue(opening, "depth", depth);
        SetFieldValue(opening, "bottomY", bottomY);
        GetFieldValue<IList>(wall, "openings").Add(opening);
    }

    private static object ToVectorDto(Vector3 value)
    {
        return GetAssemblyType("LhWorkVector3Dto")
            .GetMethod("FromVector3", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, new object[] { value });
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
