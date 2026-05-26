using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WallOpeningPlacementManagerTests
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
    public void SplitContainerWallSegment_DoesNotSplit_WhenSplitPointTouchesOpeningBoundary()
    {
        Type managerType = GetAssemblyType("WallOpeningPlacementManager");
        Type containerType = GetAssemblyType("WallOpeningContainer");
        Type openingType = GetAssemblyType("WallOpening");
        Type wallType = GetAssemblyType("Wall");
        Type wallDataType = GetAssemblyType("WallData");
        Type wallVisualStateType = GetAssemblyType("WallVisualState");
        Type openingPlacementType = managerType.GetNestedType("OpeningPlacementType", BindingFlags.Public);

        Component manager = CreateComponent("OpeningManager", managerType);
        GameObject wallRoot = CreateGameObject("WallRoot");
        SetPrivateField(manager, "wallRoot", wallRoot.transform);

        Component container = AddComponent(CreateChildGameObject(wallRoot.transform, "OpeningContainer"), containerType);
        object visualState = Activator.CreateInstance(wallVisualStateType);
        containerType.GetMethod("Initialize")?.Invoke(
            container,
            new[]
            {
                (object)new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                0.1f,
                3f,
                1.5f,
                visualState,
                0,
                0,
                false,
                false,
                false,
                false,
            });

        Component selectedSegment = AddComponent(CreateChildGameObject(container.transform, "Segment"), wallType);
        object wallData = Activator.CreateInstance(
            wallDataType,
            new Vector3(1f, 0f, 0f),
            new Vector3(3f, 0f, 0f),
            0.1f,
            3f,
            1.5f);
        wallType.GetMethod("Initialize")?.Invoke(selectedSegment, new[] { wallData });

        Component opening = AddComponent(CreateChildGameObject(container.transform, "Door"), openingType);
        object doorEnumValue = Enum.Parse(openingPlacementType, "Door");
        openingType.GetMethod("Initialize")?.Invoke(
            opening,
            new[]
            {
                manager,
                container,
                doorEnumValue,
                string.Empty,
                string.Empty,
                false,
                false,
                3f,
                2f,
                2f,
                0.1f,
                0f,
            });

        InvokePrivate(
            manager,
            "SplitContainerWallSegment",
            container,
            selectedSegment);

        Assert.That(wallRoot.transform.childCount, Is.EqualTo(1), "Split should be rejected when it touches an opening boundary.");
        Assert.That(wallRoot.GetComponentsInChildren(Type.GetType("WallOpeningContainer, Assembly-CSharp"), true).Length, Is.EqualTo(1));
    }

    [Test]
    public void ConstrainContainerOuterSplitPointDrag_ClampsDraggedStartBeforeOpeningBoundary()
    {
        Type managerType = GetAssemblyType("WallOpeningPlacementManager");
        Type containerType = GetAssemblyType("WallOpeningContainer");
        Type openingType = GetAssemblyType("WallOpening");
        Type wallVisualStateType = GetAssemblyType("WallVisualState");
        Type openingPlacementType = managerType.GetNestedType("OpeningPlacementType", BindingFlags.Public);

        Component manager = CreateComponent("OpeningManager", managerType);
        GameObject wallRoot = CreateGameObject("WallRoot");
        SetPrivateField(manager, "wallRoot", wallRoot.transform);

        Component container = AddComponent(CreateChildGameObject(wallRoot.transform, "OpeningContainer"), containerType);
        object visualState = Activator.CreateInstance(wallVisualStateType);
        containerType.GetMethod("Initialize")?.Invoke(
            container,
            new object[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                0.1f,
                3f,
                1.5f,
                visualState,
                11,
                12,
                false,
                false,
                true,
                false,
            });

        Component opening = AddComponent(CreateChildGameObject(container.transform, "Door"), openingType);
        object doorEnumValue = Enum.Parse(openingPlacementType, "Door");
        openingType.GetMethod("Initialize")?.Invoke(
            opening,
            new object[]
            {
                manager,
                container,
                doorEnumValue,
                string.Empty,
                string.Empty,
                false,
                false,
                3f,
                2f,
                2f,
                0.1f,
                0f,
            });

        object[] arguments =
        {
            container,
            11,
            new Vector3(2f, 0f, 0f),
            null,
        };

        MethodInfo method = managerType.GetMethod(
            "TryConstrainContainerOuterSplitPointDrag",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);

        object invoked = method.Invoke(manager, arguments);
        Assert.That(invoked, Is.EqualTo(true));

        Vector3 constrainedPoint = (Vector3)arguments[3];
        float minimumSideWallUnits = (float)managerType.GetProperty("MinimumSideWallUnits")?.GetValue(manager);
        Assert.That(constrainedPoint.x, Is.LessThan(2f));
        Assert.That(constrainedPoint.x, Is.EqualTo(2f - minimumSideWallUnits).Within(0.0001f));
    }

    [Test]
    public void UpdateOpeningVisual_DoesNotOverlayDoorMaterial_WhenDoorPrefabExists()
    {
        Type managerType = GetAssemblyType("WallOpeningPlacementManager");
        Type containerType = GetAssemblyType("WallOpeningContainer");
        Type openingType = GetAssemblyType("WallOpening");
        Type wallVisualStateType = GetAssemblyType("WallVisualState");
        Type catalogType = GetAssemblyType("OpeningTypeCatalog");
        Type catalogItemType = GetAssemblyType("OpeningTypeCatalogItem");
        Type openingPlacementType = managerType.GetNestedType("OpeningPlacementType", BindingFlags.Public);

        Component manager = CreateComponent("OpeningManager", managerType);
        Component container = AddComponent(CreateGameObject("OpeningContainer"), containerType);
        object visualState = Activator.CreateInstance(wallVisualStateType);
        containerType.GetMethod("Initialize")?.Invoke(
            container,
            new object[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(4f, 0f, 0f),
                0.1f,
                3f,
                1.5f,
                visualState,
                1,
                2,
                false,
                false,
                false,
                false,
            });

        Material prefabMaterial = new Material(Shader.Find("Standard"));
        createdObjects.Add(prefabMaterial);
        GameObject doorPrefab = CreateGameObject("DoorPrefab");
        MeshRenderer prefabRenderer = doorPrefab.AddComponent<MeshRenderer>();
        doorPrefab.AddComponent<MeshFilter>();
        prefabRenderer.sharedMaterial = prefabMaterial;

        ScriptableObject catalog = ScriptableObject.CreateInstance(catalogType);
        createdObjects.Add(catalog);
        object catalogItem = Activator.CreateInstance(catalogItemType);
        object doorEnumValue = Enum.Parse(openingPlacementType, "Door");
        SetPrivateField(catalogItem, "openingType", doorEnumValue);
        SetPrivateField(catalogItem, "typeKey", "DoorA");
        SetPrivateField(catalogItem, "displayName", "Door A");
        SetPrivateField(catalogItem, "modelPrefab", doorPrefab);
        GetPrivateField<System.Collections.IList>(catalog, "items").Add(catalogItem);
        SetPrivateField(manager, "openingTypeCatalog", catalog);

        Component opening = AddComponent(CreateChildGameObject(container.transform, "Door"), openingType);
        openingType.GetMethod("Initialize")?.Invoke(
            opening,
            new object[]
            {
                manager,
                container,
                doorEnumValue,
                "DoorA",
                string.Empty,
                false,
                false,
                2f,
                1f,
                2f,
                0.1f,
                0f,
            });

        InvokePrivate(manager, "UpdateOpeningVisual", container, opening, 0, null);

        MeshRenderer openingRenderer = ((Component)opening).GetComponent<MeshRenderer>();
        MeshRenderer restoredPrefabRenderer = null;
        MeshRenderer[] renderers = ((Component)opening).GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != openingRenderer)
            {
                restoredPrefabRenderer = renderers[i];
                break;
            }
        }

        Assert.That(openingRenderer.enabled, Is.False);
        Assert.That(restoredPrefabRenderer, Is.Not.Null);
        Assert.That(restoredPrefabRenderer.sharedMaterial, Is.SameAs(prefabMaterial));
    }

    [Test]
    public void SelectOpening_IgnoresOpening_WhenNotInDetailEditMode()
    {
        Type managerType = GetAssemblyType("WallOpeningPlacementManager");
        Type modeManagerType = GetAssemblyType("ModeManager");
        Type openingType = GetAssemblyType("WallOpening");

        Component manager = CreateComponent("OpeningManager", managerType);
        Component modeManager = CreateComponent("ModeManager", modeManagerType);
        modeManagerType.GetMethod("SetMode")?.Invoke(modeManager, new object[] { Enum.Parse(GetAssemblyType("EditorMode"), "RoomCreate") });
        SetPrivateField(manager, "modeManager", modeManager);

        Component opening = AddComponent(CreateGameObject("Door"), openingType);

        managerType.GetMethod("SelectOpening")?.Invoke(manager, new object[] { opening });

        object selectedOpening = managerType.GetProperty("SelectedOpening")?.GetValue(manager);
        Assert.That(selectedOpening, Is.Null);
    }

    private Component CreateComponent(string name, Type componentType)
    {
        return AddComponent(CreateGameObject(name), componentType);
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private GameObject CreateChildGameObject(Transform parent, string name)
    {
        GameObject gameObject = CreateGameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static Component AddComponent(GameObject gameObject, Type componentType)
    {
        return gameObject.AddComponent(componentType);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(target);
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, args);
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }
}
