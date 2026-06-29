using NUnit.Framework;
using System;
using System.Reflection;
using UnityEngine;

public class EditorScreenCoordinateUtilityTests
{
    [Test]
    public void NormalizePointerScreenPosition_ScalesDisplayPixelsToUnityScreenPixels()
    {
        Vector2 normalized = InvokeVector2(
            "NormalizePointerScreenPosition",
            new Vector2(2560f, 1440f),
            new Vector2(1920f, 1080f),
            new Vector2(2560f, 1440f));

        Assert.That(normalized.x, Is.EqualTo(1920f).Within(0.001f));
        Assert.That(normalized.y, Is.EqualTo(1080f).Within(0.001f));
    }

    [Test]
    public void NormalizePointerScreenPosition_KeepsWindowRelativeCoordinatesWithinUnityScreen()
    {
        Vector2 normalized = InvokeVector2(
            "NormalizePointerScreenPosition",
            new Vector2(960f, 540f),
            new Vector2(1920f, 1080f),
            new Vector2(2560f, 1440f));

        Assert.That(normalized.x, Is.EqualTo(960f).Within(0.001f));
        Assert.That(normalized.y, Is.EqualTo(540f).Within(0.001f));
    }

    [Test]
    public void ToCameraScreenPoint_ScalesUnityScreenPointToCameraTargetPixels()
    {
        Vector2 cameraPoint = InvokeVector2(
            "ToCameraScreenPoint",
            new Vector2(1920f, 1080f),
            new Vector2(2560f, 1440f),
            new Vector2(1920f, 1080f),
            true);

        Assert.That(cameraPoint.x, Is.EqualTo(1440f).Within(0.001f));
        Assert.That(cameraPoint.y, Is.EqualTo(810f).Within(0.001f));
    }

    [Test]
    public void ToUnityScreenPoint_ScalesCameraTargetPixelsToUnityScreenPoint()
    {
        Vector2 screenPoint = InvokeVector2(
            "ToUnityScreenPoint",
            new Vector2(1440f, 810f),
            new Vector2(2560f, 1440f),
            new Vector2(1920f, 1080f),
            true);

        Assert.That(screenPoint.x, Is.EqualTo(1920f).Within(0.001f));
        Assert.That(screenPoint.y, Is.EqualTo(1080f).Within(0.001f));
    }

    [Test]
    public void BuildViewportSignature_TracksScreenAndCameraPixelSizes()
    {
        Vector4 signature = InvokeVector4(
            "BuildViewportSignature",
            new Vector2(1110f, 570f),
            new Vector2(1920f, 1080f));

        Assert.That(signature.x, Is.EqualTo(1110f).Within(0.001f));
        Assert.That(signature.y, Is.EqualTo(570f).Within(0.001f));
        Assert.That(signature.z, Is.EqualTo(1920f).Within(0.001f));
        Assert.That(signature.w, Is.EqualTo(1080f).Within(0.001f));
    }

    [Test]
    public void ViewportSignatureChanged_ReturnsTrue_WhenScreenSizeChanges()
    {
        bool changed = InvokeBool(
            "ViewportSignatureChanged",
            new Vector4(1920f, 1080f, 1920f, 1080f),
            new Vector4(1110f, 570f, 1920f, 1080f));

        Assert.That(changed, Is.True);
    }

    [Test]
    public void ViewportSignatureChanged_ReturnsTrue_WhenCameraPixelSizeChanges()
    {
        bool changed = InvokeBool(
            "ViewportSignatureChanged",
            new Vector4(1110f, 570f, 1110f, 570f),
            new Vector4(1110f, 570f, 1920f, 1080f));

        Assert.That(changed, Is.True);
    }

    [Test]
    public void ScreenPointToAnchoredPosition_UsesCanvasLocalCoordinates()
    {
        Canvas canvas = null;
        try
        {
            GameObject canvasObject = new GameObject("TestCanvas", typeof(RectTransform), typeof(Canvas));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);

            Vector2 screenPoint = new Vector2(1440f, 810f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 expected);

            Vector2 anchoredPosition = InvokeVector2(
                "ScreenPointToAnchoredPosition",
                canvasRect,
                canvas,
                screenPoint,
                null);

            Assert.That(anchoredPosition.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(anchoredPosition.y, Is.EqualTo(expected.y).Within(0.001f));
        }
        finally
        {
            if (canvas != null)
            {
                UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            }
        }
    }

    private static Vector2 InvokeVector2(string methodName, params object[] arguments)
    {
        Type utilityType = Type.GetType("EditorScreenCoordinateUtility, Assembly-CSharp");
        Assert.That(utilityType, Is.Not.Null);

        MethodInfo method = null;
        MethodInfo[] methods = utilityType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name == methodName && methods[i].GetParameters().Length == arguments.Length)
            {
                method = methods[i];
                break;
            }
        }

        Assert.That(method, Is.Not.Null);
        return (Vector2)method.Invoke(null, arguments);
    }

    private static Vector4 InvokeVector4(string methodName, params object[] arguments)
    {
        Type utilityType = Type.GetType("EditorScreenCoordinateUtility, Assembly-CSharp");
        Assert.That(utilityType, Is.Not.Null);

        MethodInfo method = FindMethod(utilityType, methodName, arguments.Length);
        Assert.That(method, Is.Not.Null);
        return (Vector4)method.Invoke(null, arguments);
    }

    private static bool InvokeBool(string methodName, params object[] arguments)
    {
        Type utilityType = Type.GetType("EditorScreenCoordinateUtility, Assembly-CSharp");
        Assert.That(utilityType, Is.Not.Null);

        MethodInfo method = FindMethod(utilityType, methodName, arguments.Length);
        Assert.That(method, Is.Not.Null);
        return (bool)method.Invoke(null, arguments);
    }

    private static MethodInfo FindMethod(Type utilityType, string methodName, int argumentCount)
    {
        MethodInfo[] methods = utilityType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            if (methods[i].Name == methodName && methods[i].GetParameters().Length == argumentCount)
            {
                return methods[i];
            }
        }

        return null;
    }
}
