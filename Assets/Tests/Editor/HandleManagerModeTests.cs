using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HandleManagerModeTests
{
    private GameObject cameraObject;
    private GameObject canvasObject;
    private GameObject wallRootObject;
    private GameObject managerObject;
    private GameObject wallObject;

    [TearDown]
    public void TearDown()
    {
        DestroyObject(wallObject);
        DestroyObject(managerObject);
        DestroyObject(wallRootObject);
        DestroyObject(canvasObject);
        DestroyObject(cameraObject);
    }

    [Test]
    public void DetailEditMode_ShowsSplitPointHandles()
    {
        Camera camera = CreateCamera();
        Canvas canvas = CreateCanvas();
        wallRootObject = new GameObject("WallRoot");
        managerObject = new GameObject("HandleManager");
        Component handleManager = managerObject.AddComponent(GetAssemblyType("HandleManager"));
        SetPrivateField(handleManager, "mainCamera", camera);
        SetPrivateField(handleManager, "targetCanvas", canvas);
        SetPrivateField(handleManager, "wallRoot", wallRootObject.transform);

        wallObject = CreateWallObject("SplitWall", wallRootObject.transform);
        ConfigureWall(
            wallObject,
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            1,
            2,
            false,
            false,
            false,
            true);

        handleManager.GetType().GetMethod("RegisterWall", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(handleManager, new object[] { wallObject });

        object detailEditMode = System.Enum.Parse(GetAssemblyType("EditorMode"), "DetailEdit");
        InvokePrivate(handleManager, "HandleModeChanged", detailEditMode);

        GameObject normalEndpointHandle = canvas.transform.Find("Handle_Vertex_1")?.gameObject;
        GameObject splitPointHandle = canvas.transform.Find("Handle_Vertex_2")?.gameObject;

        Assert.That(normalEndpointHandle, Is.Not.Null);
        Assert.That(splitPointHandle, Is.Not.Null);
        Assert.That(normalEndpointHandle.activeSelf, Is.False);
        Assert.That(splitPointHandle.activeSelf, Is.True);
        Assert.That(((Behaviour)handleManager).enabled, Is.True);
    }

    [Test]
    public void DefaultMode_RepositionsHandlesImmediatelyAfterModeChange()
    {
        Camera camera = CreateCamera();
        Canvas canvas = CreateCanvas();
        wallRootObject = new GameObject("WallRoot");
        managerObject = new GameObject("HandleManager");
        Component handleManager = managerObject.AddComponent(GetAssemblyType("HandleManager"));
        SetPrivateField(handleManager, "mainCamera", camera);
        SetPrivateField(handleManager, "targetCanvas", canvas);
        SetPrivateField(handleManager, "wallRoot", wallRootObject.transform);

        Vector3 startPoint = new Vector3(0f, 0f, 0f);
        Vector3 endPoint = new Vector3(4f, 0f, 0f);
        wallObject = CreateWallObject("SplitWall", wallRootObject.transform);
        ConfigureWall(
            wallObject,
            startPoint,
            endPoint,
            1,
            2,
            false,
            false,
            false,
            true);

        handleManager.GetType().GetMethod("RegisterWall", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(handleManager, new object[] { wallObject });

        object detailEditMode = System.Enum.Parse(GetAssemblyType("EditorMode"), "DetailEdit");
        object defaultMode = System.Enum.Parse(GetAssemblyType("EditorMode"), "Default");
        InvokePrivate(handleManager, "HandleModeChanged", detailEditMode);
        InvokePrivate(handleManager, "HandleModeChanged", defaultMode);

        RectTransform startHandle = canvas.transform.Find("Handle_Vertex_1") as RectTransform;
        RectTransform endHandle = canvas.transform.Find("Handle_Vertex_2") as RectTransform;

        Assert.That(startHandle, Is.Not.Null);
        Assert.That(endHandle, Is.Not.Null);
        AssertScreenPosition(startHandle.position, camera.WorldToScreenPoint(startPoint));
        AssertScreenPosition(endHandle.position, camera.WorldToScreenPoint(endPoint));
    }

    [Test]
    public void DefaultMode_RepositionsHandlesInCanvasLocalPixels_WhenCanvasHasParentTransform()
    {
        Camera camera = CreateCamera();
        Canvas canvas = CreateCanvas();
        GameObject parentObject = new GameObject("CanvasParent", typeof(RectTransform));
        RectTransform parentRect = parentObject.GetComponent<RectTransform>();
        parentRect.localScale = new Vector3(1.25f, 1.25f, 1f);
        canvas.transform.SetParent(parentRect, false);

        wallRootObject = new GameObject("WallRoot");
        managerObject = new GameObject("HandleManager");
        Component handleManager = managerObject.AddComponent(GetAssemblyType("HandleManager"));
        SetPrivateField(handleManager, "mainCamera", camera);
        SetPrivateField(handleManager, "targetCanvas", canvas);
        SetPrivateField(handleManager, "wallRoot", wallRootObject.transform);

        Vector3 startPoint = new Vector3(0f, 0f, 0f);
        Vector3 endPoint = new Vector3(4f, 0f, 0f);
        wallObject = CreateWallObject("ScaledCanvasWall", wallRootObject.transform);
        ConfigureWall(
            wallObject,
            startPoint,
            endPoint,
            1,
            2,
            false,
            false,
            false,
            false);

        handleManager.GetType().GetMethod("RegisterWall", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(handleManager, new object[] { wallObject });

        RectTransform startHandle = canvas.transform.Find("Handle_Vertex_1") as RectTransform;
        RectTransform endHandle = canvas.transform.Find("Handle_Vertex_2") as RectTransform;

        Assert.That(startHandle, Is.Not.Null);
        Assert.That(endHandle, Is.Not.Null);
        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            camera.WorldToScreenPoint(startPoint),
            null,
            out Vector2 expectedStart);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            camera.WorldToScreenPoint(endPoint),
            null,
            out Vector2 expectedEnd);
        AssertScreenPosition(startHandle.anchoredPosition, expectedStart);
        AssertScreenPosition(endHandle.anchoredPosition, expectedEnd);

        DestroyObject(parentObject);
    }

    [Test]
    public void PreviewWallSelectionUI_UsesCanvasLocalWidth_WhenCanvasIsScaled()
    {
        Camera camera = CreateCamera();
        Canvas canvas = CreateCanvas();
        RectTransform canvasRect = canvas.transform as RectTransform;
        canvasRect.sizeDelta = new Vector2(1920f, 1080f);
        canvasRect.localScale = new Vector3(0.75f, 0.75f, 1f);

        wallRootObject = new GameObject("WallRoot");
        managerObject = new GameObject("WallSelectionManager");
        Component selectionManager = managerObject.AddComponent(GetAssemblyType("WallSelectionManager"));
        SetPrivateField(selectionManager, "mainCamera", camera);
        SetPrivateField(selectionManager, "wallSelectionCanvas", canvas);

        Vector3 startPoint = new Vector3(0f, 0f, 0f);
        Vector3 endPoint = new Vector3(4f, 0f, 0f);
        wallObject = CreateWallObject("WallPreview", wallRootObject.transform);
        ConfigureWall(
            wallObject,
            startPoint,
            endPoint,
            1,
            2,
            false,
            false,
            false,
            false);

        Component proxy = wallObject.AddComponent(GetAssemblyType("WallSelectionUIProxy"));
        proxy.GetType().GetMethod("Initialize", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(proxy, new object[] { selectionManager });
        proxy.GetType().GetMethod("RefreshVisual", BindingFlags.Instance | BindingFlags.Public)
            ?.Invoke(proxy, null);

        RectTransform wallUi = canvas.transform.Find("WallPreview_ui") as RectTransform;
        Assert.That(wallUi, Is.Not.Null);

        Vector3 startScreen = camera.WorldToScreenPoint(startPoint);
        Vector3 endScreen = camera.WorldToScreenPoint(endPoint);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, startScreen, null, out Vector2 startLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, endScreen, null, out Vector2 endLocal);
        float expectedWidth = Vector2.Distance(startLocal, endLocal);

        Assert.That(wallUi.sizeDelta.x, Is.EqualTo(expectedWidth).Within(0.01f));
    }

    private static GameObject CreateWallObject(string name, Transform parent)
    {
        System.Type factoryType = GetAssemblyType("WallObjectFactory");
        System.Type visualStateType = GetAssemblyType("WallVisualState");
        MethodInfo method = factoryType.GetMethod("CreateWallObject", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        return (GameObject)method.Invoke(null, new object[] { name, parent, null, System.Activator.CreateInstance(visualStateType) });
    }

    private static void ConfigureWall(
        GameObject currentWallObject,
        Vector3 start,
        Vector3 end,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint)
    {
        System.Type wallDataType = GetAssemblyType("WallData");
        object wallData = System.Activator.CreateInstance(wallDataType, start, end, 0.2f, 3f, 1.5f);
        System.Type factoryType = GetAssemblyType("WallObjectFactory");
        MethodInfo method = factoryType.GetMethod("ConfigureWall", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        method.Invoke(
            null,
            new object[]
            {
                currentWallObject,
                wallData,
                startVertexId,
                endVertexId,
                suppressStartHandle,
                suppressEndHandle,
                startSplitPoint,
                endSplitPoint,
                0.01f,
                null,
                false,
            });
    }

    private Camera CreateCamera()
    {
        cameraObject = new GameObject("Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.transform.position = new Vector3(2f, 10f, 2f);
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return camera;
    }

    private Canvas CreateCanvas()
    {
        canvasObject = new GameObject("HandleCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        return canvas;
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string name, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, arguments);
    }

    private static void DestroyObject(Object target)
    {
        if (target != null)
        {
            Object.DestroyImmediate(target);
        }
    }

    private static void AssertScreenPosition(Vector3 actual, Vector3 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
    }

    private static void AssertScreenPosition(Vector2 actual, Vector2 expected)
    {
        Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
        Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
    }

    private static System.Type GetAssemblyType(string typeName)
    {
        System.Type type = System.Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Expected Assembly-CSharp type {typeName}.");
        return type;
    }
}
