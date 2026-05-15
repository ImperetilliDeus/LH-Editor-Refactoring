using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RoomGraphUtilityTests
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
    public void BuildPlanarGraph_ReusesOuterPolygonVertex_WhenVirtualBoundaryStartsOnExistingCorner()
    {
        Type boundaryType = GetAssemblyType("VirtualBoundary");
        Component boundary = CreateComponent("VirtualBoundary", boundaryType);
        boundaryType.GetMethod("SetEndpoints")?.Invoke(
            boundary,
            new object[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(5f, 0f, 10f),
            });

        object graph = BuildPlanarGraph(
            new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f),
                new Vector3(0f, 0f, 10f),
            },
            boundaryType,
            boundary);

        Assert.That(GetReadOnlyListCount(graph, "Faces"), Is.EqualTo(2));
    }

    private object BuildPlanarGraph(List<Vector3> outerPolygon, Type boundaryType, Component boundary)
    {
        Type utilityType = GetAssemblyType("RoomGraphUtility");
        Array boundaries = Array.CreateInstance(boundaryType, 1);
        boundaries.SetValue(boundary, 0);

        MethodInfo method = utilityType.GetMethod(
            "BuildPlanarGraph",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(List<Vector3>), typeof(IEnumerable<>).MakeGenericType(boundaryType) },
            null);
        Assert.That(method, Is.Not.Null);

        return method.Invoke(null, new object[] { outerPolygon, boundaries });
    }

    private Component CreateComponent(string name, Type componentType)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject.AddComponent(componentType);
    }

    private static int GetReadOnlyListCount(object target, string propertyName)
    {
        object list = target.GetType().GetProperty(propertyName)?.GetValue(target);
        Assert.That(list, Is.Not.Null);
        return ((ICollection)list).Count;
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }
}
