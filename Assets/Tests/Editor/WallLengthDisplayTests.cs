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
    public void SetWallLength_ShowsSelectedWallLabelOnlyInDetailEditMode()
    {
        Type editorModeType = GetAssemblyType("EditorMode");
        GameObject wallObject = CreateGameObject("Wall");
        CreateDisplay(out Component modeManager, out Component display, out Canvas testCanvas);

        InvokeSetWallLength(display, wallObject.transform, false);

        GameObject lengthLabel = FindIncludingInactive($"LengthLabel_{wallObject.transform.GetInstanceID()}");
        Assert.That(lengthLabel, Is.Not.Null);
        Assert.That(lengthLabel.transform.parent, Is.EqualTo(testCanvas.transform));
        Assert.That(lengthLabel.activeSelf, Is.False);

        display.GetType().GetMethod("SetSelectedWall")?.Invoke(display, new object[] { wallObject.transform });
        Assert.That(lengthLabel.activeSelf, Is.False, "Selected-wall labels are hidden in Default mode.");

        SetMode(modeManager, editorModeType, "DetailEdit");
        Assert.That(lengthLabel.activeSelf, Is.True);
        Text lengthText = lengthLabel.GetComponent<Text>();
        Assert.That(lengthText, Is.Not.Null);
        Assert.That(lengthText.raycastTarget, Is.False);

        SetMode(modeManager, editorModeType, "Default");
        Assert.That(lengthLabel.activeSelf, Is.False);

        SetMode(modeManager, editorModeType, "DetailEdit");
        display.GetType().GetMethod("SetSelectedWall")?.Invoke(display, new object[] { null });
        Assert.That(lengthLabel.activeSelf, Is.False);
    }

    [Test]
    public void SetWallLength_ShowsPreviewLabelOnlyInDefaultMode()
    {
        Type editorModeType = GetAssemblyType("EditorMode");
        GameObject previewWallObject = CreateGameObject("WallPreview");
        CreateDisplay(out Component modeManager, out Component display, out _);

        InvokeSetWallLength(display, previewWallObject.transform, true);

        GameObject lengthLabel = FindIncludingInactive($"LengthLabel_{previewWallObject.transform.GetInstanceID()}");
        Assert.That(lengthLabel, Is.Not.Null);
        Assert.That(lengthLabel.activeSelf, Is.True);

        SetMode(modeManager, editorModeType, "DetailEdit");
        Assert.That(lengthLabel.activeSelf, Is.False);

        SetMode(modeManager, editorModeType, "Default");
        Assert.That(lengthLabel.activeSelf, Is.True);
    }

    private void CreateDisplay(out Component modeManager, out Component display, out Canvas testCanvas)
    {
        Type modeManagerType = GetAssemblyType("ModeManager");
        Type wallLengthDisplayType = GetAssemblyType("WallLengthDisplay");

        modeManager = AddComponent(CreateGameObject("ModeManager"), modeManagerType);
        display = AddComponent(CreateGameObject("WallLengthDisplay"), wallLengthDisplayType);

        // Awake/OnEnable do not run in EditMode tests, so bind the references explicitly.
        GameObject canvasObject = CreateGameObject("TestWallLengthCanvas");
        testCanvas = canvasObject.AddComponent<Canvas>();
        testCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        SetPrivateField(wallLengthDisplayType, display, "targetCanvas", testCanvas);
        SetPrivateField(wallLengthDisplayType, display, "modeManager", modeManager);
        MethodInfo subscribe = wallLengthDisplayType.GetMethod("SubscribeModeManager", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(subscribe, Is.Not.Null);
        subscribe.Invoke(display, null);
    }

    private static void InvokeSetWallLength(Component display, Transform wallTransform, bool isPreview)
    {
        MethodInfo setWallLength = display.GetType().GetMethod("SetWallLength");
        Assert.That(setWallLength, Is.Not.Null);
        setWallLength.Invoke(display, new object[] { wallTransform, 2f, 2.4f, isPreview });
    }

    private static void SetMode(Component modeManager, Type editorModeType, string modeName)
    {
        modeManager.GetType().GetMethod("SetMode")?.Invoke(
            modeManager,
            new object[] { Enum.Parse(editorModeType, modeName) });
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
