using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WallHierarchyUtilityTests
{
    private readonly List<GameObject> objectsToDestroy = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objectsToDestroy.Count - 1; i >= 0; i--)
        {
            if (objectsToDestroy[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(objectsToDestroy[i]);
            }
        }

        objectsToDestroy.Clear();
    }

    [Test]
    public void CollectWalls_ExcludesPreviewWallByDefault()
    {
        Transform wallRoot = CreateRoot();
        CreateWall("Wall_000", wallRoot);
        CreateWall("WallPreview", wallRoot);

        IList walls = CreateWallList();
        InvokeCollectWalls(wallRoot, walls, true, false);

        Assert.That(walls, Has.Count.EqualTo(1));
        Assert.That(((Component)walls[0]).name, Is.EqualTo("Wall_000"));
    }

    [Test]
    public void CollectWalls_IncludesPreviewWallWhenRequested()
    {
        Transform wallRoot = CreateRoot();
        CreateWall("Wall_000", wallRoot);
        CreateWall("WallPreview", wallRoot);

        IList walls = CreateWallList();
        InvokeCollectWalls(wallRoot, walls, true, true);

        Assert.That(walls, Has.Count.EqualTo(2));
        Assert.That(ContainsWallNamed(walls, "WallPreview"), Is.True);
    }

    private Transform CreateRoot()
    {
        GameObject root = new GameObject("WallRoot");
        objectsToDestroy.Add(root);
        return root.transform;
    }

    private void CreateWall(string name, Transform parent)
    {
        GameObject wallObject = new GameObject(name);
        wallObject.AddComponent(GetAssemblyType("Wall"));
        wallObject.transform.SetParent(parent, false);
        objectsToDestroy.Add(wallObject);
    }

    private static IList CreateWallList()
    {
        Type listType = typeof(List<>).MakeGenericType(GetAssemblyType("Wall"));
        return (IList)Activator.CreateInstance(listType);
    }

    private static void InvokeCollectWalls(Transform root, IList walls, bool includeInactive, bool includePreview)
    {
        Type wallType = GetAssemblyType("Wall");
        Type listType = typeof(List<>).MakeGenericType(wallType);
        MethodInfo method = GetAssemblyType("WallHierarchyUtility").GetMethod(
            "CollectWalls",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(Transform), listType, typeof(bool), typeof(bool) },
            null);

        Assert.That(method, Is.Not.Null);
        method.Invoke(null, new object[] { root, walls, includeInactive, includePreview });
    }

    private static bool ContainsWallNamed(IList walls, string name)
    {
        for (int i = 0; i < walls.Count; i++)
        {
            if (walls[i] is Component component && component.name == name)
            {
                return true;
            }
        }

        return false;
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Expected Assembly-CSharp type {typeName}.");
        return type;
    }
}
