using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class BlurredPopupBackgroundTests
{
    private GameObject root;

    [TearDown]
    public void TearDown()
    {
        if (root != null)
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ConfigureForTests_AssignsRenderTextureToRawImageAndCaptureCamera()
    {
        root = new GameObject("BlurredBackground");
        root.AddComponent<RectTransform>();
        RawImage rawImage = root.AddComponent<RawImage>();
        Component background = root.AddComponent(GetAssemblyType("BlurredPopupBackground"));
        Camera sourceCamera = new GameObject("SourceCamera").AddComponent<Camera>();
        Camera captureCamera = new GameObject("CaptureCamera").AddComponent<Camera>();
        sourceCamera.transform.SetParent(root.transform);
        captureCamera.transform.SetParent(root.transform);

        InvokeConfigureForTests(background, sourceCamera, captureCamera, rawImage, 320, 180);

        Assert.That(rawImage.texture, Is.TypeOf<RenderTexture>());
        Assert.That(captureCamera.targetTexture, Is.EqualTo(rawImage.texture));
        Assert.That(rawImage.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(rawImage.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(rawImage.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
        Assert.That(rawImage.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
        Assert.That(captureCamera.enabled, Is.False);
    }

    [Test]
    public void ConfigureForTests_CanUseGeneratedRawImageUnderExistingImageBackground()
    {
        root = new GameObject("BlurredBackground");
        root.AddComponent<RectTransform>();
        Image backgroundImage = root.AddComponent<Image>();
        backgroundImage.color = new Color32(22, 22, 28, 179);
        Component background = root.AddComponent(GetAssemblyType("BlurredPopupBackground"));
        Camera sourceCamera = new GameObject("SourceCamera").AddComponent<Camera>();
        Camera captureCamera = new GameObject("CaptureCamera").AddComponent<Camera>();
        sourceCamera.transform.SetParent(root.transform);
        captureCamera.transform.SetParent(root.transform);

        InvokeConfigureForTests(background, sourceCamera, captureCamera, null, 320, 180);

        RawImage generatedImage = root.GetComponentInChildren<RawImage>();
        Assert.That(generatedImage, Is.Not.Null);
        Assert.That(generatedImage.transform.parent, Is.EqualTo(root.transform));
        Assert.That(generatedImage.texture, Is.TypeOf<RenderTexture>());
        Assert.That(generatedImage.raycastTarget, Is.False);
        Assert.That(generatedImage.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(generatedImage.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(generatedImage.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
        Assert.That(generatedImage.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));

        Transform overlayTransform = root.transform.Find("ColorOverlay");
        Assert.That(overlayTransform, Is.Not.Null);
        Image overlayImage = overlayTransform.GetComponent<Image>();
        Assert.That(overlayImage, Is.Not.Null);
        Assert.That(overlayImage.enabled, Is.False);
        Assert.That(overlayImage.color, Is.EqualTo(backgroundImage.color));
        Assert.That(overlayImage.raycastTarget, Is.False);
        Assert.That(overlayImage.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(overlayImage.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(overlayImage.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
        Assert.That(overlayImage.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
        Assert.That(generatedImage.transform.GetSiblingIndex(), Is.LessThan(overlayImage.transform.GetSiblingIndex()));
    }

    [Test]
    public void CaptureNow_CopiesSourceCameraTransformBeforeRendering()
    {
        root = new GameObject("BlurredBackground");
        RawImage rawImage = root.AddComponent<RawImage>();
        Component background = root.AddComponent(GetAssemblyType("BlurredPopupBackground"));
        Camera sourceCamera = new GameObject("SourceCamera").AddComponent<Camera>();
        Camera captureCamera = new GameObject("CaptureCamera").AddComponent<Camera>();
        sourceCamera.transform.SetParent(root.transform);
        captureCamera.transform.SetParent(root.transform);
        sourceCamera.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(10f, 20f, 30f));
        sourceCamera.fieldOfView = 47f;
        sourceCamera.orthographic = true;
        sourceCamera.orthographicSize = 8f;

        InvokeConfigureForTests(background, sourceCamera, captureCamera, rawImage, 128, 72);
        background.GetType().GetMethod("CaptureNow")?.Invoke(background, null);

        Assert.That(captureCamera.targetTexture, Is.EqualTo(rawImage.texture));
        Assert.That(captureCamera.transform.position, Is.EqualTo(sourceCamera.transform.position));
        Assert.That(captureCamera.transform.rotation.eulerAngles.x, Is.EqualTo(sourceCamera.transform.rotation.eulerAngles.x).Within(0.001f));
        Assert.That(captureCamera.transform.rotation.eulerAngles.y, Is.EqualTo(sourceCamera.transform.rotation.eulerAngles.y).Within(0.001f));
        Assert.That(captureCamera.transform.rotation.eulerAngles.z, Is.EqualTo(sourceCamera.transform.rotation.eulerAngles.z).Within(0.001f));
        Assert.That(captureCamera.fieldOfView, Is.EqualTo(sourceCamera.fieldOfView));
        Assert.That(captureCamera.orthographic, Is.True);
        Assert.That(captureCamera.orthographicSize, Is.EqualTo(sourceCamera.orthographicSize));
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType(typeName);
        if (type != null)
        {
            return type;
        }

        Assembly assembly = Assembly.Load("Assembly-CSharp");
        return assembly.GetType(typeName, true);
    }

    private static void InvokeConfigureForTests(Component background, Camera sourceCamera, Camera captureCamera, RawImage rawImage, int width, int height)
    {
        background.GetType().GetMethod("ConfigureForTests")?.Invoke(background, new object[]
        {
            sourceCamera,
            captureCamera,
            rawImage,
            width,
            height
        });
    }
}
