using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
    public void SetWallLength_ShowsOnlySelectedWallLabelInDefaultMode()
    {
        Type modeManagerType = GetAssemblyType("ModeManager");
        Type editorModeType = GetAssemblyType("EditorMode");
        Type wallLengthDisplayType = GetAssemblyType("WallLengthDisplay");

        Component modeManager = AddComponent(CreateGameObject("ModeManager"), modeManagerType);
        Component display = AddComponent(CreateGameObject("WallLengthDisplay"), wallLengthDisplayType);
        GameObject wallObject = CreateGameObject("Wall");

        // Awake/OnEnable do not run in EditMode tests, so bind the references explicitly.
        GameObject canvasObject = CreateGameObject("TestWallLengthCanvas");
        Canvas testCanvas = canvasObject.AddComponent<Canvas>();
        testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        SetPrivateField(wallLengthDisplayType, display, "targetCanvas", testCanvas);
        SetPrivateField(wallLengthDisplayType, display, "modeManager", modeManager);
        MethodInfo subscribe = wallLengthDisplayType.GetMethod("SubscribeModeManager", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(subscribe, Is.Not.Null);
        subscribe.Invoke(display, null);

        wallLengthDisplayType.GetMethod("SetWallLength")?.Invoke(
            display,
            new object[]
            {
                wallObject.transform,
                2f,
                2.4f,
                false,
            });

        GameObject lengthLabel = FindIncludingInactive($"LengthLabel_{wallObject.transform.GetInstanceID()}");
        Assert.That(lengthLabel, Is.Not.Null);
        Assert.That(lengthLabel.transform.parent, Is.EqualTo(testCanvas.transform));

        Assert.That(lengthLabel.activeSelf, Is.False);

        wallLengthDisplayType.GetMethod("SetSelectedWall")?.Invoke(
            display,
            new object[] { wallObject.transform });

        Assert.That(lengthLabel.activeSelf, Is.True);
        Text lengthText = lengthLabel.GetComponent<Text>();
        Assert.That(lengthText, Is.Not.Null);
        Assert.That(lengthText.raycastTarget, Is.False);

        modeManagerType.GetMethod("SetMode")?.Invoke(
            modeManager,
            new object[] { Enum.Parse(editorModeType, "DetailEdit") });

        Assert.That(lengthLabel.activeSelf, Is.False);

        modeManagerType.GetMethod("SetMode")?.Invoke(
            modeManager,
            new object[] { Enum.Parse(editorModeType, "Default") });

        Assert.That(lengthLabel.activeSelf, Is.True);

        wallLengthDisplayType.GetMethod("SetSelectedWall")?.Invoke(
            display,
            new object[] { null });

        Assert.That(lengthLabel.activeSelf, Is.False);
    }

    private Component AddComponent(GameObject gameObject, Type componentType)
    {
        Component component = gameObject.AddComponent(componentType);
        createdObjects.Add(component);
        return component;
    }

    private static GameObject FindIncludingInactive(string name)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == name)
            {
                return transforms[i].gameObject;
            }
        }

        return null;
    }

    private static void SetPrivateField(Type type, object target, string fieldName, object value)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
        field.SetValue(target, value);
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
