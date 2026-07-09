using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class DrawingOverlayToolbarTests
{
    private GameObject managerObject;
    private GameObject runtimeObject;
    private GameObject toolbarObject;
    private Texture2D texture;

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(managerObject);
        UnityEngine.Object.DestroyImmediate(runtimeObject);
        UnityEngine.Object.DestroyImmediate(toolbarObject);
        UnityEngine.Object.DestroyImmediate(texture);
    }

    [Test]
    public void SetOpacity_ClampsDocumentOpacityAndUpdatesRuntimeMaterial()
    {
        object manager = CreateManagerWithRuntime();
        texture = new Texture2D(4, 4);
        Invoke(manager, "BeginCalibration", texture, "plan.png", GetEnumValue("OverlaySourceType", "Image"), 0);
        object document = GetPropertyValue<object>(manager, "ActiveDocument");
        object calibration = GetFieldValue<object>(document, "calibration");
        SetFieldValue(calibration, "anchorPixelA", new Vector2(0f, 0f));
        SetFieldValue(calibration, "anchorPixelB", new Vector2(2f, 0f));
        SetFieldValue(calibration, "hasAnchorA", true);
        SetFieldValue(calibration, "hasAnchorB", true);
        SetFieldValue(calibration, "realDistanceMm", 2000f);
        Assert.That((bool)Invoke(manager, "ApplyCalibration"), Is.True);

        Invoke(manager, "SetOpacity", 1.5f);

        Assert.That(GetFieldValue<float>(calibration, "opacity"), Is.EqualTo(1f));
        Material material = runtimeObject.GetComponent<MeshRenderer>().sharedMaterial;
        Assert.That(material, Is.Not.Null);
        Assert.That(material.color.a, Is.EqualTo(1f).Within(0.001f));

        Invoke(manager, "SetOpacity", -0.5f);

        Assert.That(GetFieldValue<float>(calibration, "opacity"), Is.EqualTo(0f));
        Assert.That(material.color.a, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ToolbarController_SwitchesBetweenCollapsedAndExpandedStates()
    {
        object toolbar = CreateToolbar(out GameObject collapsedRoot, out GameObject expandedRoot, out Button expandButton, out Button collapseButton);

        Invoke(toolbar, "SetCollapsed", true);

        Assert.That(collapsedRoot.activeSelf, Is.True);
        Assert.That(expandedRoot.activeSelf, Is.False);

        expandButton.onClick.Invoke();

        Assert.That(collapsedRoot.activeSelf, Is.False);
        Assert.That(expandedRoot.activeSelf, Is.True);

        collapseButton.onClick.Invoke();

        Assert.That(collapsedRoot.activeSelf, Is.True);
        Assert.That(expandedRoot.activeSelf, Is.False);
    }

    [Test]
    public void ToolbarController_ChangingSliderUpdatesManagerOpacityAndPercentLabels()
    {
        object manager = CreateManagerWithRuntime();
        texture = new Texture2D(4, 4);
        Invoke(manager, "BeginCalibration", texture, "plan.png", GetEnumValue("OverlaySourceType", "Image"), 0);
        object toolbar = CreateToolbar(out _, out _, out _, out _, out Slider slider, out Component collapsedText, out Component expandedText);
        InvokeInitialize(toolbar, manager, null, null, slider, collapsedText, expandedText, null, null, null, null);

        slider.value = 0.35f;

        object document = GetPropertyValue<object>(manager, "ActiveDocument");
        object calibration = GetFieldValue<object>(document, "calibration");
        Assert.That(GetFieldValue<float>(calibration, "opacity"), Is.EqualTo(0.35f).Within(0.001f));
        Assert.That(GetPropertyValue<string>(collapsedText, "text"), Is.EqualTo("\uB3C4\uBA74 35%"));
        Assert.That(GetPropertyValue<string>(expandedText, "text"), Is.EqualTo("35%"));
    }

    [Test]
    public void ToolbarController_HidesWhenManagerHasNoActiveOverlay()
    {
        object manager = CreateManagerWithRuntime();
        object toolbar = CreateToolbar(out _, out _, out _, out _, out Slider slider, out Component collapsedText, out Component expandedText);

        InvokeInitialize(toolbar, manager, null, null, slider, collapsedText, expandedText, null, null, null, null);

        Assert.That(toolbarObject.activeSelf, Is.False);
    }

    [Test]
    public void ToolbarController_HidesWhenOverlayDocumentExistsButCalibrationHasNotBeenApplied()
    {
        object manager = CreateManagerWithRuntime();
        texture = new Texture2D(4, 4);
        Invoke(manager, "BeginCalibration", texture, "plan.png", GetEnumValue("OverlaySourceType", "Image"), 0);
        object toolbar = CreateToolbar(out _, out _, out _, out _, out Slider slider, out Component collapsedText, out Component expandedText);

        InvokeInitialize(toolbar, manager, null, null, slider, collapsedText, expandedText, null, null, null, null);

        Assert.That(GetPropertyValue<object>(manager, "ActiveDocument"), Is.Not.Null);
        Assert.That(GetPropertyValue<bool>(manager, "HasAppliedOverlay"), Is.False);
        Assert.That(toolbarObject.activeSelf, Is.False);
    }

    private object CreateManagerWithRuntime()
    {
        managerObject = new GameObject("DrawingOverlayManager");
        runtimeObject = new GameObject("DrawingOverlayRoot");
        object manager = managerObject.AddComponent(GetAssemblyType("DrawingOverlayManager"));
        Component runtime = runtimeObject.AddComponent(GetAssemblyType("DrawingOverlayRuntime"));
        SetPrivateFieldValue(manager, "activeRuntime", runtime);
        return manager;
    }

    private object CreateToolbar(
        out GameObject collapsedRoot,
        out GameObject expandedRoot,
        out Button expandButton,
        out Button collapseButton)
    {
        return CreateToolbar(out collapsedRoot, out expandedRoot, out expandButton, out collapseButton, out _, out _, out _);
    }

    private object CreateToolbar(
        out GameObject collapsedRoot,
        out GameObject expandedRoot,
        out Button expandButton,
        out Button collapseButton,
        out Slider opacitySlider,
        out Component collapsedText,
        out Component expandedText)
    {
        toolbarObject = new GameObject("Toolbar", typeof(RectTransform));
        collapsedRoot = new GameObject("Collapsed", typeof(RectTransform));
        expandedRoot = new GameObject("Expanded", typeof(RectTransform));
        collapsedRoot.transform.SetParent(toolbarObject.transform, false);
        expandedRoot.transform.SetParent(toolbarObject.transform, false);

        GameObject expandButtonObject = new GameObject("Expand", typeof(RectTransform));
        GameObject collapseButtonObject = new GameObject("Collapse", typeof(RectTransform));
        expandButtonObject.transform.SetParent(collapsedRoot.transform, false);
        collapseButtonObject.transform.SetParent(expandedRoot.transform, false);
        expandButton = expandButtonObject.AddComponent<Button>();
        collapseButton = collapseButtonObject.AddComponent<Button>();

        GameObject sliderObject = new GameObject("Slider", typeof(RectTransform));
        sliderObject.transform.SetParent(expandedRoot.transform, false);
        opacitySlider = sliderObject.AddComponent<Slider>();
        opacitySlider.minValue = 0f;
        opacitySlider.maxValue = 1f;

        collapsedText = CreateTmpText("CollapsedText", collapsedRoot.transform);
        expandedText = CreateTmpText("ExpandedText", expandedRoot.transform);

        object toolbar = toolbarObject.AddComponent(GetAssemblyType("DrawingOverlayToolbarController"));
        InvokeInitialize(toolbar, null, collapsedRoot, expandedRoot, opacitySlider, collapsedText, expandedText, expandButton, collapseButton, null, null);
        return toolbar;
    }

    private static Component CreateTmpText(string name, Transform parent)
    {
        Type tmpType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
        Assert.That(tmpType, Is.Not.Null);
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        return textObject.AddComponent(tmpType);
    }

    private static void InvokeInitialize(
        object toolbar,
        object manager,
        GameObject collapsedRoot,
        GameObject expandedRoot,
        Slider opacitySlider,
        Component collapsedText,
        Component expandedText,
        Button expandButton,
        Button collapseButton,
        Button visibilityButton,
        Button lockButton)
    {
        Invoke(
            toolbar,
            "Initialize",
            manager,
            collapsedRoot,
            expandedRoot,
            opacitySlider,
            collapsedText,
            expandedText,
            expandButton,
            collapseButton,
            visibilityButton,
            lockButton);
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }

    private static object GetEnumValue(string typeName, string valueName)
    {
        return Enum.Parse(GetAssemblyType(typeName), valueName);
    }

    private static object Invoke(object target, string methodName, params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(method, Is.Not.Null, $"Missing method {methodName}.");
        return method.Invoke(target, parameters);
    }

    private static T GetFieldValue<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static void SetFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static void SetPrivateFieldValue(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
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
