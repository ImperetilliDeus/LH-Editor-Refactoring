using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class OverlayCalibrationPanelPrefabTests
{
    private const string PrefabPath = "Assets/Prefabs/UIPrefab/DrawingOverlayCalibrationPanel.prefab";
    private GameObject controllerObject;
    private GameObject closeButtonObject;

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(controllerObject);
        Object.DestroyImmediate(closeButtonObject);
    }

    [Test]
    public void ScaleAndRotationValueFields_UseLegacyTextComponents()
    {
        FieldInfo scaleField = GetField("scaleValueText");
        FieldInfo rotationField = GetField("rotationValueText");
        Assert.That(scaleField.FieldType, Is.EqualTo(typeof(Text)));
        Assert.That(rotationField.FieldType, Is.EqualTo(typeof(Text)));

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        Component controller = prefab.GetComponent(GetAssemblyType("OverlayCalibrationPanelController"));
        Assert.That(controller, Is.Not.Null);

        Assert.That(scaleField.GetValue(controller), Is.TypeOf<Text>());
        Assert.That(rotationField.GetValue(controller), Is.TypeOf<Text>());
    }

    [Test]
    public void CloseButtonField_ReferencesPrefabCloseButton()
    {
        FieldInfo closeButtonField = GetField("closeButton");
        Assert.That(closeButtonField.FieldType, Is.EqualTo(typeof(Button)));

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        Component controller = prefab.GetComponent(GetAssemblyType("OverlayCalibrationPanelController"));
        Assert.That(controller, Is.Not.Null);

        Button closeButton = closeButtonField.GetValue(controller) as Button;
        Assert.That(closeButton, Is.Not.Null);
        Assert.That(closeButton.name, Is.EqualTo("CloseButton"));
    }

    [Test]
    public void CloseButtonClick_HidesPanel()
    {
        controllerObject = new GameObject("Panel");
        Component controller = controllerObject.AddComponent(GetAssemblyType("OverlayCalibrationPanelController"));

        closeButtonObject = new GameObject("CloseButton");
        Button closeButton = closeButtonObject.AddComponent<Button>();

        controller.GetType().GetMethod("Initialize")?.Invoke(
            controller,
            new object[]
            {
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                closeButton,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            });

        Assert.That(controllerObject.activeSelf, Is.True);
        closeButton.onClick.Invoke();
        Assert.That(controllerObject.activeSelf, Is.False);
    }

    [Test]
    public void PreviewController_RespectsExistingInactiveGridRootAndDoesNotCreateHintText()
    {
        controllerObject = new GameObject("PreviewImage");
        RectTransform previewRect = controllerObject.AddComponent<RectTransform>();
        RawImage previewImage = controllerObject.AddComponent<RawImage>();
        AspectRatioFitter aspectRatioFitter = controllerObject.AddComponent<AspectRatioFitter>();

        GameObject overlayObject = new GameObject("PreviewOverlay");
        RectTransform overlayRect = overlayObject.AddComponent<RectTransform>();
        overlayRect.SetParent(previewRect, false);

        GameObject gridObject = new GameObject("GridRoot");
        RectTransform gridRect = gridObject.AddComponent<RectTransform>();
        gridRect.SetParent(overlayRect, false);
        gridObject.SetActive(false);

        Component previewController = controllerObject.AddComponent(GetAssemblyType("OverlayPreviewController"));

        previewController.GetType().GetMethod("Initialize")?.Invoke(
            previewController,
            new object[] { previewImage, previewRect, aspectRatioFitter, null });

        Assert.That(FindChildrenByName(previewRect, "PreviewOverlay"), Is.EqualTo(1));
        Assert.That(FindChildrenByName(previewRect, "GridRoot"), Is.EqualTo(1));
        Assert.That(gridObject.activeSelf, Is.False);
        Assert.That(FindChildrenByName(previewRect, "HintText"), Is.EqualTo(0));
    }

    private static FieldInfo GetField(string fieldName)
    {
        FieldInfo field = GetAssemblyType("OverlayCalibrationPanelController").GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return field;
    }

    private static System.Type GetAssemblyType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        Assert.Fail(typeName);
        return null;
    }

    private static int FindChildrenByName(Transform root, string name)
    {
        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
            {
                count++;
            }

            count += FindChildrenByName(child, name);
        }

        return count;
    }
}
