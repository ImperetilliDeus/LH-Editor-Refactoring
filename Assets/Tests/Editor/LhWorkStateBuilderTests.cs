using System;
using System.Collections;
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
        Assert.That(GetFieldValue<bool>(wall, "endSplitPoint"), Is.True);
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
