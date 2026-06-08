using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SceneHierarchyTreeViewTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void RebuildNow_CreatesFallbackRowsWithIndent()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Component wall = CreateWall("Wall_001", "wall-a", wallRoot);
        Component room = CreateRoom("RoomObject", "Living Room", wall);
        RectTransform contentRoot = CreateRectObject("Content");
        Component treeView = CreateComponent("TreeView", GetAssemblyType("SceneHierarchyTreeView"));
        InvokeSetReferencesForTests(treeView, wallRoot, CreateRoomList(room), contentRoot, null);

        treeView.GetType().GetMethod("RebuildNow")?.Invoke(treeView, null);

        Assert.That(contentRoot.childCount, Is.EqualTo(2));
        Assert.That(contentRoot.GetChild(0).GetComponentInChildren<Text>().text, Is.EqualTo("Room (Living Room)"));
        Assert.That(contentRoot.GetChild(1).GetComponentInChildren<Text>().text, Is.EqualTo("Wall_001"));
        Assert.That(((RectTransform)contentRoot.GetChild(0)).anchoredPosition.x, Is.EqualTo(0f));
        Assert.That(((RectTransform)contentRoot.GetChild(1)).anchoredPosition.x, Is.EqualTo(0f));
        Assert.That(contentRoot.GetComponent<VerticalLayoutGroup>(), Is.Not.Null);
        Assert.That(((RectTransform)contentRoot.GetChild(0)).anchoredPosition.y, Is.Not.EqualTo(((RectTransform)contentRoot.GetChild(1)).anchoredPosition.y));
        Assert.That(contentRoot.GetChild(1).GetComponentInChildren<Text>().rectTransform.offsetMin.x, Is.GreaterThan(contentRoot.GetChild(0).GetComponentInChildren<Text>().rectTransform.offsetMin.x));
    }

    [Test]
    public void RebuildNow_ConfiguresScrollContentForVerticalOverflow()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        CreateWall("Wall_001", "wall-a", wallRoot);
        ScrollRect scrollRect = CreateScrollRect("HierarchyScroll");
        RectTransform contentRoot = CreateRectObject("Content");
        contentRoot.SetParent(scrollRect.transform, false);
        Component treeView = CreateComponent("TreeView", GetAssemblyType("SceneHierarchyTreeView"));
        SetField(treeView, "scrollRect", scrollRect);
        InvokeSetReferencesForTests(treeView, wallRoot, CreateRoomList(), contentRoot, null);

        treeView.GetType().GetMethod("RebuildNow")?.Invoke(treeView, null);

        Assert.That(scrollRect.content, Is.EqualTo(contentRoot));
        Assert.That(scrollRect.vertical, Is.True);
        Assert.That(scrollRect.horizontal, Is.False);
        Assert.That(scrollRect.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
        Assert.That(scrollRect.scrollSensitivity, Is.EqualTo(96f));
        Assert.That(scrollRect.inertia, Is.True);
        Assert.That(scrollRect.decelerationRate, Is.EqualTo(0.12f));
        Assert.That(scrollRect.viewport, Is.Not.Null);
        Assert.That(scrollRect.viewport, Is.Not.EqualTo(contentRoot));
        Assert.That(contentRoot.parent, Is.EqualTo(scrollRect.viewport));
        Assert.That(scrollRect.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
        ContentSizeFitter sizeFitter = contentRoot.GetComponent<ContentSizeFitter>();
        Assert.That(sizeFitter, Is.Not.Null);
        Assert.That(sizeFitter.verticalFit, Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
    }

    [Test]
    public void RebuildNow_MakesScrollHostReceivePointerWheelEvents()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        CreateWall("Wall_001", "wall-a", wallRoot);
        ScrollRect scrollRect = CreateScrollRect("HierarchyScroll");
        RectTransform contentRoot = CreateRectObject("Content");
        contentRoot.SetParent(scrollRect.transform, false);
        Component treeView = CreateComponent("TreeView", GetAssemblyType("SceneHierarchyTreeView"));
        SetField(treeView, "scrollRect", scrollRect);
        InvokeSetReferencesForTests(treeView, wallRoot, CreateRoomList(), contentRoot, null);

        treeView.GetType().GetMethod("RebuildNow")?.Invoke(treeView, null);

        Graphic hitTarget = scrollRect.viewport.GetComponent<Graphic>();
        Assert.That(hitTarget, Is.Not.Null);
        Assert.That(hitTarget.raycastTarget, Is.True);
        Assert.That(hitTarget.color.a, Is.EqualTo(0f));
        Assert.That(scrollRect.viewport.GetComponent(GetAssemblyType("SceneHierarchySmoothScrollHandler")), Is.Not.Null);
    }

    [Test]
    public void RebuildNow_BoundsBackgroundToHierarchyPanelWithoutDisablingNestedCanvas()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        CreateWall("Wall_001", "wall-a", wallRoot);
        RectTransform parentCanvasRoot = CreateRectObject("ParentCanvas");
        parentCanvasRoot.gameObject.AddComponent<Canvas>();
        RectTransform panelRoot = CreateRectObject("_Hierachy");
        panelRoot.SetParent(parentCanvasRoot, false);
        Canvas nestedCanvas = panelRoot.gameObject.AddComponent<Canvas>();
        RectTransform background = CreateRectObject("_Background");
        background.SetParent(panelRoot, false);
        RectTransform contentRoot = CreateRectObject("_Content");
        contentRoot.SetParent(panelRoot, false);
        Component treeView = CreateComponent("TreeView", GetAssemblyType("SceneHierarchyTreeView"));
        InvokeSetReferencesForTests(treeView, wallRoot, CreateRoomList(), contentRoot, null);

        treeView.GetType().GetMethod("RebuildNow")?.Invoke(treeView, null);

        Assert.That(nestedCanvas.enabled, Is.True);
        Assert.That(background.parent, Is.EqualTo(panelRoot));
        Assert.That(background.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(background.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(background.offsetMin, Is.EqualTo(Vector2.zero));
        Assert.That(background.offsetMax, Is.EqualTo(Vector2.zero));
        Assert.That(panelRoot.Find("HierarchyScroll"), Is.Not.Null);
    }

    [Test]
    public void RebuildNow_MovesMisplacedContentScrollRectToParent()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        CreateWall("Wall_001", "wall-a", wallRoot);
        RectTransform panelRoot = CreateRectObject("HierarchyPanel");
        RectTransform contentRoot = CreateRectObject("Content");
        contentRoot.SetParent(panelRoot, false);
        contentRoot.gameObject.AddComponent<ScrollRect>();
        Component treeView = CreateComponent("TreeView", GetAssemblyType("SceneHierarchyTreeView"));
        InvokeSetReferencesForTests(treeView, wallRoot, CreateRoomList(), contentRoot, null);

        treeView.GetType().GetMethod("RebuildNow")?.Invoke(treeView, null);

        RectTransform scrollRoot = panelRoot.Find("HierarchyScroll") as RectTransform;
        Assert.That(scrollRoot, Is.Not.Null);
        ScrollRect panelScrollRect = scrollRoot.GetComponent<ScrollRect>();
        Assert.That(panelScrollRect, Is.Not.Null);
        Assert.That(panelScrollRect.content, Is.EqualTo(contentRoot));
        Assert.That(panelScrollRect.viewport, Is.Not.Null);
        Assert.That(panelScrollRect.viewport.parent, Is.EqualTo(scrollRoot));
        Assert.That(contentRoot.parent, Is.EqualTo(panelScrollRect.viewport));
        Assert.That(panelScrollRect.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
        Assert.That(contentRoot.GetComponent<ScrollRect>(), Is.Null);
    }

    [Test]
    public void WallButtonClick_SelectsRepresentativeWall()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Component wall = CreateWall("Wall_001", "wall-a", wallRoot);
        RectTransform contentRoot = CreateRectObject("Content");
        Component selectionManager = CreateComponent("SelectionManager", GetAssemblyType("WallSelectionManager"));
        Component treeView = CreateComponent("TreeView", GetAssemblyType("SceneHierarchyTreeView"));
        InvokeSetReferencesForTests(treeView, wallRoot, CreateRoomList(), contentRoot, selectionManager);

        treeView.GetType().GetMethod("RebuildNow")?.Invoke(treeView, null);
        Button button = contentRoot.GetChild(0).GetComponent<Button>();
        button.onClick.Invoke();

        Assert.That(GetProperty<GameObject>(selectionManager, "SelectedWall"), Is.EqualTo(wall.gameObject));
    }

    [Test]
    public void RoomWallAuthoringPanel_DoesNotCreateToggleForWallPreview()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Component preview = CreateWall("WallPreview", "preview", wallRoot);
        preview.gameObject.SetActive(false);
        CreateWall("Wall_001", "wall-a", wallRoot);
        RectTransform container = CreateRectObject("ToggleContainer");
        Toggle template = CreateToggle("_WallToggleTemplate");
        template.transform.SetParent(container, false);
        template.gameObject.SetActive(false);
        Component controller = CreateComponent("RoomWallAuthoringPanel", GetAssemblyType("RoomWallAuthoringPanelController"));
        SetField(controller, "wallRoot", wallRoot);
        SetField(controller, "wallToggleContainer", container);
        SetField(controller, "wallToggleTemplate", template);

        controller.GetType().GetMethod("RefreshWallList", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(controller, null);

        Assert.That(container.childCount, Is.EqualTo(2));
        Assert.That(container.GetChild(1).name, Is.EqualTo("WallToggle_Wall_001"));
    }

    [Test]
    public void RoomWallAuthoringPanel_TreatsCollapsedContainerIdAsSelectingAllSegments()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        GameObject containerObject = CreateObject("Wall_With_Opening");
        containerObject.transform.SetParent(wallRoot, false);
        containerObject.AddComponent(GetAssemblyType("WallOpeningContainer"));
        Component firstSegment = CreateWall("Segment_A", "collapsed-wall-id", containerObject.transform);
        Component secondSegment = CreateWall("Segment_B", "segment-b", containerObject.transform);
        Component room = CreateRoom("LoadedRoom", "Loaded", firstSegment);
        RectTransform toggleContainer = CreateRectObject("ToggleContainer");
        Toggle template = CreateToggle("_WallToggleTemplate");
        template.transform.SetParent(toggleContainer, false);
        template.gameObject.SetActive(false);
        Component controller = CreateComponent("RoomWallAuthoringPanel", GetAssemblyType("RoomWallAuthoringPanelController"));
        SetField(controller, "wallRoot", wallRoot);
        SetField(controller, "wallToggleContainer", toggleContainer);
        SetField(controller, "wallToggleTemplate", template);

        controller.GetType().GetMethod("HandleSelectedRoomChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(controller, new object[] { room });

        Assert.That((bool)controller.GetType().GetMethod("IsWallSelectedForAuthoring")
            ?.Invoke(controller, new object[] { firstSegment }), Is.True);
        Assert.That((bool)controller.GetType().GetMethod("IsWallSelectedForAuthoring")
            ?.Invoke(controller, new object[] { secondSegment }), Is.True);
    }

    [Test]
    public void RoomWallAuthoringPanel_TreatsPersistentContainerIdAsSelectingGeneratedSegments()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        GameObject containerObject = CreateObject("Wall_With_Opening");
        containerObject.transform.SetParent(wallRoot, false);
        Component container = containerObject.AddComponent(GetAssemblyType("WallOpeningContainer"));
        container.GetType().GetMethod("SetPersistentWallId")
            ?.Invoke(container, new object[] { "collapsed-wall-id" });
        Component firstSegment = CreateWall("Segment_A", "generated-a", containerObject.transform);
        Component secondSegment = CreateWall("Segment_B", "generated-b", containerObject.transform);
        Component room = CreateRoom("LoadedRoom", "Loaded", firstSegment);
        room.GetType().GetMethod("SetManualWallIds")
            ?.Invoke(room, new object[] { new[] { "collapsed-wall-id" } });
        RectTransform toggleContainer = CreateRectObject("ToggleContainer");
        Toggle template = CreateToggle("_WallToggleTemplate");
        template.transform.SetParent(toggleContainer, false);
        template.gameObject.SetActive(false);
        Component controller = CreateComponent("RoomWallAuthoringPanel", GetAssemblyType("RoomWallAuthoringPanelController"));
        SetField(controller, "wallRoot", wallRoot);
        SetField(controller, "wallToggleContainer", toggleContainer);
        SetField(controller, "wallToggleTemplate", template);

        controller.GetType().GetMethod("HandleSelectedRoomChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(controller, new object[] { room });

        Assert.That((bool)controller.GetType().GetMethod("IsWallSelectedForAuthoring")
            ?.Invoke(controller, new object[] { firstSegment }), Is.True);
        Assert.That((bool)controller.GetType().GetMethod("IsWallSelectedForAuthoring")
            ?.Invoke(controller, new object[] { secondSegment }), Is.True);
    }

    [Test]
    public void RebuildNow_UsesConfiguredFallbackFont()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        CreateWall("Wall_001", "wall-a", wallRoot);
        RectTransform contentRoot = CreateRectObject("Content");
        Component treeView = CreateComponent("TreeView", GetAssemblyType("SceneHierarchyTreeView"));
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        SetField(treeView, "rowFont", font);
        SetField(treeView, "rowTextColor", Color.green);
        SetField(treeView, "rowFontSize", 17);
        InvokeSetReferencesForTests(treeView, wallRoot, CreateRoomList(), contentRoot, null);

        treeView.GetType().GetMethod("RebuildNow")?.Invoke(treeView, null);

        Text text = contentRoot.GetChild(0).GetComponentInChildren<Text>();
        Assert.That(text.font, Is.EqualTo(font));
        Assert.That(text.color, Is.EqualTo(Color.green));
        Assert.That(text.fontSize, Is.EqualTo(17));
    }

    [Test]
    public void RebuildNow_KeepsUnityDefaultFont_WhenFontIsNotConfigured()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        CreateWall("Wall_001", "wall-a", wallRoot);
        RectTransform contentRoot = CreateRectObject("Content");
        Component treeView = CreateComponent("TreeView", GetAssemblyType("SceneHierarchyTreeView"));
        InvokeSetReferencesForTests(treeView, wallRoot, CreateRoomList(), contentRoot, null);

        treeView.GetType().GetMethod("RebuildNow")?.Invoke(treeView, null);

        Text text = contentRoot.GetChild(0).GetComponentInChildren<Text>();
        Assert.That(text.font, Is.Not.Null);
    }

    [Test]
    public void ResizeHandleDrag_ChangesTargetWidthWithinLimits()
    {
        Type treeViewType = GetAssemblyType("SceneHierarchyTreeView");
        RectTransform target = CreateRectObject("TreePanel");
        target.sizeDelta = new Vector2(240f, 100f);
        Component treeView = target.gameObject.AddComponent(treeViewType);
        Button resizeHandle = CreateButton("ResizeHandle");
        InvokeConfigureResizeForTests(treeView, resizeHandle, target, 180f, 320f);

        Component dragHandle = resizeHandle.GetComponent(GetAssemblyType("SceneHierarchyTreeResizeHandle"));
        Assert.That(dragHandle, Is.Not.Null);

        EventSystem eventSystem = CreateObject("EventSystem").AddComponent<EventSystem>();
        PointerEventData beginEvent = new PointerEventData(eventSystem) { position = new Vector2(100f, 0f) };
        PointerEventData dragEvent = new PointerEventData(eventSystem) { position = new Vector2(300f, 0f) };
        dragHandle.GetType().GetMethod("OnBeginDrag")?.Invoke(dragHandle, new object[] { beginEvent });
        dragHandle.GetType().GetMethod("OnDrag")?.Invoke(dragHandle, new object[] { dragEvent });

        Assert.That(target.sizeDelta.x, Is.EqualTo(320f));
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private Component CreateComponent(string name, Type componentType)
    {
        GameObject gameObject = CreateObject(name);
        return gameObject.AddComponent(componentType);
    }

    private RectTransform CreateRectObject(string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        return gameObject.GetComponent<RectTransform>();
    }

    private Button CreateButton(string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Button));
        createdObjects.Add(gameObject);
        return gameObject.GetComponent<Button>();
    }

    private Toggle CreateToggle(string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        createdObjects.Add(gameObject);
        return gameObject.GetComponent<Toggle>();
    }

    private ScrollRect CreateScrollRect(string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
        createdObjects.Add(gameObject);
        return gameObject.GetComponent<ScrollRect>();
    }

    private Component CreateWall(string name, string id, Transform parent)
    {
        Type wallType = GetAssemblyType("Wall");
        Type wallDataType = GetAssemblyType("WallData");
        GameObject wallObject = CreateObject(name);
        wallObject.transform.SetParent(parent, false);
        Component wall = wallObject.AddComponent(wallType);
        object wallData = Activator.CreateInstance(
            wallDataType,
            Vector3.zero,
            Vector3.forward,
            0.2f,
            3f,
            1.5f);
        wallType.GetMethod("Initialize")?.Invoke(wall, new[] { wallData });
        object data = GetProperty<object>(wall, "Data");
        PropertyInfo idProperty = wallDataType.GetProperty("id");
        Assert.That(idProperty, Is.Not.Null);
        idProperty.SetValue(data, id);
        return wall;
    }

    private Component CreateRoom(string objectName, string roomName, params Component[] walls)
    {
        Type roomType = GetAssemblyType("Room");
        Type wallType = GetAssemblyType("Wall");
        Type roomGeometryType = GetAssemblyType("RoomGeometry");
        GameObject roomObject = CreateObject(objectName);
        Component room = roomObject.AddComponent(roomType);
        object wallSet = CreateWallSet(wallType, walls);
        object geometry = Activator.CreateInstance(roomGeometryType);
        roomGeometryType.GetField("Center")?.SetValue(geometry, Vector3.zero);
        roomGeometryType.GetField("Area")?.SetValue(geometry, 1f);
        roomGeometryType.GetField("WallCount")?.SetValue(geometry, 4);
        List<Vector3> polygon = new List<Vector3>
        {
            Vector3.zero,
            Vector3.forward,
            Vector3.right + Vector3.forward,
            Vector3.right,
        };
        roomType.GetMethod("Initialize", new[] { wallSet.GetType(), roomGeometryType, typeof(IReadOnlyList<Vector3>), typeof(bool) })
            ?.Invoke(room, new[] { wallSet, geometry, polygon, true });
        roomType.GetMethod("SetRoomName")?.Invoke(room, new object[] { roomName });
        return room;
    }

    private static void InvokeSetReferencesForTests(Component treeView, Transform wallRoot, object rooms, RectTransform contentRoot, Component selectionManager)
    {
        Type roomType = GetAssemblyType("Room");
        Type selectionManagerType = GetAssemblyType("WallSelectionManager");
        Type enumerableRoomType = typeof(IEnumerable<>).MakeGenericType(roomType);
        MethodInfo method = treeView.GetType().GetMethod(
            "SetReferencesForTests",
            new[] { typeof(Transform), enumerableRoomType, typeof(RectTransform), selectionManagerType });
        Assert.That(method, Is.Not.Null);
        method.Invoke(treeView, new object[] { wallRoot, rooms, contentRoot, selectionManager });
    }

    private static void InvokeConfigureResizeForTests(Component treeView, Button resizeHandle, RectTransform target, float minWidth, float maxWidth)
    {
        MethodInfo method = treeView.GetType().GetMethod(
            "ConfigureResizeForTests",
            new[] { typeof(Button), typeof(RectTransform), typeof(float), typeof(float) });
        Assert.That(method, Is.Not.Null);
        method.Invoke(treeView, new object[] { resizeHandle, target, minWidth, maxWidth });
    }

    private static object CreateRoomList(params Component[] rooms)
    {
        Type roomType = GetAssemblyType("Room");
        Type listType = typeof(List<>).MakeGenericType(roomType);
        IList list = (IList)Activator.CreateInstance(listType);
        for (int i = 0; i < rooms.Length; i++)
        {
            list.Add(rooms[i]);
        }

        return list;
    }

    private static object CreateWallSet(Type wallType, params Component[] walls)
    {
        Type wallSetType = typeof(HashSet<>).MakeGenericType(wallType);
        object wallSet = Activator.CreateInstance(wallSetType);
        MethodInfo addMethod = wallSetType.GetMethod("Add");
        Assert.That(addMethod, Is.Not.Null);

        for (int i = 0; i < walls.Length; i++)
        {
            addMethod.Invoke(wallSet, new object[] { walls[i] });
        }

        return wallSet;
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }
}
