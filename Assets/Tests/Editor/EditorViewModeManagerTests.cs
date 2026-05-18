using System;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class EditorViewModeManagerTests
{
    private GameObject managerObject;
    private GameObject topCameraObject;
    private GameObject perspectiveCameraObject;
    private GameObject topUiRoot;
    private GameObject topButtonObject;
    private GameObject perspectiveButtonObject;
    private int eventCount;

    [TearDown]
    public void TearDown()
    {
        DestroyObject(perspectiveButtonObject);
        DestroyObject(topButtonObject);
        DestroyObject(topUiRoot);
        DestroyObject(perspectiveCameraObject);
        DestroyObject(topCameraObject);
        DestroyObject(managerObject);
        eventCount = 0;
    }

    [Test]
    public void SetPerspectiveView_EnablesPerspectiveCameraAndHidesTopViewRoots()
    {
        Component manager = CreateManager(
            out Camera topCamera,
            out Camera perspectiveCamera,
            out Behaviour topManager,
            out Behaviour perspectiveManager,
            out Button topButton,
            out Button perspectiveButton);

        InvokePublic(manager, "SetPerspectiveView");

        Assert.That(GetCurrentViewModeName(manager), Is.EqualTo("Perspective3D"));
        Assert.That(topCamera.enabled, Is.False);
        Assert.That(perspectiveCamera.enabled, Is.True);
        Assert.That(topManager.enabled, Is.False);
        Assert.That(perspectiveManager.enabled, Is.True);
        Assert.That(topUiRoot.activeSelf, Is.False);
        Assert.That(topButton.interactable, Is.True);
        Assert.That(perspectiveButton.interactable, Is.False);
    }

    [Test]
    public void SetTopView_RestoresTopCameraAndTopViewRoots()
    {
        Component manager = CreateManager(
            out Camera topCamera,
            out Camera perspectiveCamera,
            out Behaviour topManager,
            out Behaviour perspectiveManager,
            out Button topButton,
            out Button perspectiveButton);

        InvokePublic(manager, "SetPerspectiveView");
        InvokePublic(manager, "SetTopView");

        Assert.That(GetCurrentViewModeName(manager), Is.EqualTo("Top"));
        Assert.That(topCamera.enabled, Is.True);
        Assert.That(perspectiveCamera.enabled, Is.False);
        Assert.That(topManager.enabled, Is.True);
        Assert.That(perspectiveManager.enabled, Is.False);
        Assert.That(topUiRoot.activeSelf, Is.True);
        Assert.That(topButton.interactable, Is.False);
        Assert.That(perspectiveButton.interactable, Is.True);
    }

    [Test]
    public void SetViewMode_DoesNotRaiseDuplicateEventForSameMode()
    {
        Component manager = CreateManager(out _, out _, out _, out _, out _, out _);
        SubscribeToViewModeChanged(manager);

        InvokeSetViewMode(manager, "Top");
        InvokeSetViewMode(manager, "Perspective3D");
        InvokeSetViewMode(manager, "Perspective3D");

        Assert.That(eventCount, Is.EqualTo(1));
    }

    [Test]
    public void SetReferencesForTests_RebindsButtonsWithoutDuplicateListeners()
    {
        Component manager = CreateManager(
            out Camera topCamera,
            out Camera perspectiveCamera,
            out Behaviour topManager,
            out Behaviour perspectiveManager,
            out Button topButton,
            out Button perspectiveButton);
        SubscribeToViewModeChanged(manager);

        InvokeSetReferencesForTests(
            manager,
            topCamera,
            perspectiveCamera,
            topManager,
            perspectiveManager,
            new[] { topUiRoot, null },
            topButton,
            perspectiveButton);

        perspectiveButton.onClick.Invoke();
        perspectiveButton.onClick.Invoke();
        topButton.onClick.Invoke();

        Assert.That(eventCount, Is.EqualTo(2));
        Assert.That(GetCurrentViewModeName(manager), Is.EqualTo("Top"));
        Assert.That(topButton.interactable, Is.False);
        Assert.That(perspectiveButton.interactable, Is.True);
    }

    [Test]
    public void ToolbarButtons_SwitchViewModes()
    {
        Component manager = CreateManager(
            out Camera topCamera,
            out Camera perspectiveCamera,
            out _,
            out _,
            out Button topButton,
            out Button perspectiveButton);

        perspectiveButton.onClick.Invoke();

        Assert.That(GetCurrentViewModeName(manager), Is.EqualTo("Perspective3D"));
        Assert.That(topCamera.enabled, Is.False);
        Assert.That(perspectiveCamera.enabled, Is.True);

        topButton.onClick.Invoke();

        Assert.That(GetCurrentViewModeName(manager), Is.EqualTo("Top"));
        Assert.That(topCamera.enabled, Is.True);
        Assert.That(perspectiveCamera.enabled, Is.False);
    }

    [Test]
    public void SetPerspectiveView_ToleratesMissingReferences()
    {
        managerObject = new GameObject("EditorViewModeManager");
        Component manager = managerObject.AddComponent(GetAssemblyType("EditorViewModeManager"));

        Assert.DoesNotThrow(() => InvokePublic(manager, "SetPerspectiveView"));
        Assert.That(GetCurrentViewModeName(manager), Is.EqualTo("Perspective3D"));
    }

    private Component CreateManager(
        out Camera topCamera,
        out Camera perspectiveCamera,
        out Behaviour topManager,
        out Behaviour perspectiveManager,
        out Button topButton,
        out Button perspectiveButton)
    {
        topCameraObject = new GameObject("TopCamera");
        topCamera = topCameraObject.AddComponent<Camera>();
        topManager = topCameraObject.AddComponent<TestViewInputComponent>();

        perspectiveCameraObject = new GameObject("PerspectiveCamera");
        perspectiveCamera = perspectiveCameraObject.AddComponent<Camera>();
        perspectiveManager = perspectiveCameraObject.AddComponent<TestViewInputComponent>();

        topUiRoot = new GameObject("TopPlanContent");

        topButtonObject = new GameObject("TopButton");
        topButton = topButtonObject.AddComponent<Button>();
        perspectiveButtonObject = new GameObject("PerspectiveButton");
        perspectiveButton = perspectiveButtonObject.AddComponent<Button>();

        managerObject = new GameObject("EditorViewModeManager");
        managerObject.SetActive(false);
        Component manager = managerObject.AddComponent(GetAssemblyType("EditorViewModeManager"));
        InvokeSetReferencesForTests(
            manager,
            topCamera,
            perspectiveCamera,
            topManager,
            perspectiveManager,
            new[] { topUiRoot },
            topButton,
            perspectiveButton);
        InvokeSetViewMode(manager, "Top");
        return manager;
    }

    private void SubscribeToViewModeChanged(Component manager)
    {
        EventInfo eventInfo = manager.GetType().GetEvent("ViewModeChanged", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(eventInfo, Is.Not.Null);

        Type eventType = eventInfo.EventHandlerType;
        Type parameterType = eventType.GetGenericArguments()[0];
        ParameterExpression parameter = Expression.Parameter(parameterType, "mode");
        MethodInfo incrementMethod = GetType().GetMethod(nameof(IncrementEventCount), BindingFlags.Instance | BindingFlags.NonPublic);
        Delegate handler = Expression.Lambda(eventType, Expression.Call(Expression.Constant(this), incrementMethod), parameter).Compile();
        eventInfo.AddEventHandler(manager, handler);
    }

    private void IncrementEventCount()
    {
        eventCount++;
    }

    private static void InvokeSetViewMode(Component manager, string modeName)
    {
        MethodInfo method = manager.GetType().GetMethod("SetViewMode", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null);
        object mode = Enum.Parse(GetAssemblyType("EditorViewMode"), modeName);
        method.Invoke(manager, new[] { mode });
    }

    private static void InvokeSetReferencesForTests(
        Component manager,
        Camera topCamera,
        Camera perspectiveCamera,
        Behaviour topManager,
        Behaviour perspectiveManager,
        GameObject[] topViewOnlyRoots,
        Button topButton,
        Button perspectiveButton)
    {
        MethodInfo method = manager.GetType().GetMethod("SetReferencesForTests", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null);
        method.Invoke(
            manager,
            new object[]
            {
                topCamera,
                perspectiveCamera,
                topManager,
                perspectiveManager,
                topViewOnlyRoots,
                topButton,
                perspectiveButton,
            });
    }

    private static void InvokePublic(Component manager, string methodName)
    {
        MethodInfo method = manager.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null);
        method.Invoke(manager, Array.Empty<object>());
    }

    private static string GetCurrentViewModeName(Component manager)
    {
        PropertyInfo property = manager.GetType().GetProperty("CurrentViewMode", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null);
        return property.GetValue(manager)?.ToString();
    }

    private static void DestroyObject(UnityEngine.Object target)
    {
        if (target != null)
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Expected Assembly-CSharp type {typeName}.");
        return type;
    }

    private sealed class TestViewInputComponent : MonoBehaviour
    {
    }
}
