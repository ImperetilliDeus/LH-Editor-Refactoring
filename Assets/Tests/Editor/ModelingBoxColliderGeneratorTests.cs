using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ModelingBoxColliderGeneratorTests
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
    public void Generate_AddsColliderUsingMeshLocalBounds()
    {
        GameObject root = CreateGameObject("Root");
        GameObject part = CreateChild(root.transform, "Fixed_Frame_Left");
        Mesh mesh = CreateBoxMesh(new Vector3(0.25f, 2.1f, 0.12f), new Vector3(0.1f, 0.2f, 0.3f));
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>();

        Type generatorType = GetEditorAssemblyType("ModelingBoxColliderGenerator");
        object result = Generate(generatorType, root, GetDefaultOptions(generatorType));

        BoxCollider collider = part.GetComponent<BoxCollider>();
        Assert.That(GetResultField(result, "added"), Is.EqualTo(1));
        Assert.That(collider, Is.Not.Null);
        Assert.That(collider.center, Is.EqualTo(new Vector3(0.1f, 0.2f, 0.3f)));
        Assert.That(collider.size, Is.EqualTo(new Vector3(0.25f, 2.1f, 0.12f)));
    }

    [Test]
    public void Generate_DefaultOptions_SkipGlassAndNonParametricParts()
    {
        GameObject root = CreateGameObject("Root");
        GameObject glass = CreateMeshChild(root.transform, "Stretch_Glass_Left");
        GameObject decorative = CreateMeshChild(root.transform, "Decorative_Handle");
        GameObject frame = CreateMeshChild(root.transform, "Fixed_Frame_Right");

        Type generatorType = GetEditorAssemblyType("ModelingBoxColliderGenerator");
        object result = Generate(generatorType, root, GetDefaultOptions(generatorType));

        Assert.That(GetResultField(result, "added"), Is.EqualTo(1));
        Assert.That(GetResultField(result, "skipped"), Is.EqualTo(2));
        Assert.That(frame.GetComponent<BoxCollider>(), Is.Not.Null);
        Assert.That(glass.GetComponent<BoxCollider>(), Is.Null);
        Assert.That(decorative.GetComponent<BoxCollider>(), Is.Null);
    }

    [Test]
    public void Generate_DoesNotOverwriteExistingCollider_WhenDisabled()
    {
        GameObject root = CreateGameObject("Root");
        GameObject part = CreateMeshChild(root.transform, "Stretch_Railing_Top_Rail");
        BoxCollider existing = part.AddComponent<BoxCollider>();
        existing.center = Vector3.one;
        existing.size = Vector3.one * 2f;

        Type generatorType = GetEditorAssemblyType("ModelingBoxColliderGenerator");
        object options = GetDefaultOptions(generatorType);
        SetOptionsField(options, "overwriteExisting", false);

        object result = Generate(generatorType, root, options);

        Assert.That(GetResultField(result, "added"), Is.EqualTo(0));
        Assert.That(GetResultField(result, "updated"), Is.EqualTo(0));
        Assert.That(GetResultField(result, "skipped"), Is.EqualTo(1));
        Assert.That(existing.center, Is.EqualTo(Vector3.one));
        Assert.That(existing.size, Is.EqualTo(Vector3.one * 2f));
    }

    private GameObject CreateMeshChild(Transform parent, string name)
    {
        GameObject child = CreateChild(parent, name);
        child.AddComponent<MeshFilter>().sharedMesh = CreateBoxMesh(Vector3.one, Vector3.zero);
        child.AddComponent<MeshRenderer>();
        return child;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = CreateGameObject(name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private Mesh CreateBoxMesh(Vector3 size, Vector3 center)
    {
        Mesh mesh = new Mesh { name = "TestBoxMesh" };
        Vector3 half = size * 0.5f;
        Vector3 min = center - half;
        Vector3 max = center + half;
        mesh.vertices = new[]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
        };
        mesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
        };
        mesh.RecalculateBounds();
        createdObjects.Add(mesh);
        return mesh;
    }

    private static Type GetEditorAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp-Editor");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp-Editor.");
        return type;
    }

    private static object GetDefaultOptions(Type generatorType)
    {
        Type optionsType = generatorType.GetNestedType("Options", BindingFlags.Public);
        Assert.That(optionsType, Is.Not.Null);
        PropertyInfo defaultProperty = optionsType.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
        Assert.That(defaultProperty, Is.Not.Null);
        return defaultProperty.GetValue(null);
    }

    private static void SetOptionsField(object options, string fieldName, object value)
    {
        FieldInfo field = options.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(options, value);
    }

    private static object Generate(Type generatorType, GameObject root, object options)
    {
        MethodInfo method = generatorType.GetMethod(
            "Generate",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(GameObject), options.GetType(), typeof(bool) },
            null);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(null, new[] { root, options, false });
    }

    private static int GetResultField(object result, string fieldName)
    {
        FieldInfo field = result.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        return (int)field.GetValue(result);
    }
}
