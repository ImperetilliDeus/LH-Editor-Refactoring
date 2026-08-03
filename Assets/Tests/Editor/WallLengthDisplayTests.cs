using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WallLengthDisplayTests
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
    public void SetWallLength_ShowsLabelsOnlyInWallCreationMode()
    {
        Type modeManagerType = GetAssemblyType("ModeManager");
        Type editorModeType = GetAssemblyType("EditorMode");
        Type wallLengthDisplayType = GetAssemblyType("WallLengthDisplay");

        Component modeManager = AddComponent(CreateGameObject("ModeManager"), modeManagerType);
        Component display = AddComponent(CreateGameObject("WallLengthDisplay"), wallLengthDisplayType);
        GameObject wallObject = CreateGameObject("Wall");

        wallLengthDisplayType.GetMethod("SetWallLength")?.Invoke(
            display,
            new object[]
            {
                wallObject.transform,
                2f,
                2.4f,
                false,
            });

        GameObject lengthLabel = GameObject.Find($"LengthLabel_{wallObject.transform.GetInstanceID()}");
        Assert.That(lengthLabel, Is.Not.Null);
        Canvas labelCanvas = lengthLabel.GetComponentInParent<Canvas>();
        if (labelCanvas != null)
        {
            createdObjects.Add(labelCanvas.gameObject);
        }

        Assert.That(lengthLabel.activeSelf, Is.True);

        modeManagerType.GetMethod("SetMode")?.Invoke(
            modeManager,
            new object[] { Enum.Parse(editorModeType, "DetailEdit") });

        Assert.That(lengthLabel.activeSelf, Is.False);

        modeManagerType.GetMethod("SetMode")?.Invoke(
            modeManager,
            new object[] { Enum.Parse(editorModeType, "Default") });

        Assert.That(lengthLabel.activeSelf, Is.True);
    }

    private Component AddComponent(GameObject gameObject, Type componentType)
    {
        Component component = gameObject.AddComponent(componentType);
        createdObjects.Add(component);
        return component;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        throw new InvalidOperationException($"Could not find type '{typeName}'.");
    }
}
