using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class WallSelectionCanvasOrderingUtilityTests
{
    private GameObject canvasObject;
    private GameObject backgroundObject;
    private GameObject proxyObject;
    private GameObject toolbarObject;

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(canvasObject);
        UnityEngine.Object.DestroyImmediate(backgroundObject);
        UnityEngine.Object.DestroyImmediate(proxyObject);
        UnityEngine.Object.DestroyImmediate(toolbarObject);
    }

    [Test]
    public void PlaceBelowSelectableControls_KeepsWallProxyBelowToolbarButton()
    {
        canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        backgroundObject = CreateChild(canvasObject.transform, "TopViewContent");
        proxyObject = CreateChild(canvasObject.transform, "WallProxy");
        toolbarObject = CreateChild(canvasObject.transform, "Toolbar");
        CreateChild(toolbarObject.transform, "LoadButton").AddComponent<Button>();

        RectTransform proxyRect = proxyObject.GetComponent<RectTransform>();

        InvokePlaceBelowSelectableControls(proxyRect, canvasObject.transform);

        Assert.That(proxyObject.transform.GetSiblingIndex(), Is.GreaterThan(backgroundObject.transform.GetSiblingIndex()));
        Assert.That(proxyObject.transform.GetSiblingIndex(), Is.LessThan(toolbarObject.transform.GetSiblingIndex()));
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    private static void InvokePlaceBelowSelectableControls(RectTransform rectTransform, Transform canvasTransform)
    {
        Type utilityType = Type.GetType("WallSelectionCanvasOrderingUtility, Assembly-CSharp");
        Assert.That(utilityType, Is.Not.Null);

        MethodInfo method = utilityType.GetMethod(
            "PlaceBelowSelectableControls",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(RectTransform), typeof(Transform) },
            null);
        Assert.That(method, Is.Not.Null);
        method.Invoke(null, new object[] { rectTransform, canvasTransform });
    }
}
