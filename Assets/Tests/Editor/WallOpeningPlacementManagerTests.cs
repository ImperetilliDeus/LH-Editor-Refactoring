using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
    public void OpeningContainerSplitPlanner_UsesClickedWorldPointForSplitDistance()
    {
        Type containerType = GetAssemblyType("WallOpeningContainer");
        Type wallVisualStateType = GetAssemblyType("WallVisualState");
        Type plannerType = GetAssemblyType("OpeningContainerSplitPlanner");

        Component container = AddComponent(CreateGameObject("OpeningContainer"), containerType);
        object visualState = Activator.CreateInstance(wallVisualStateType);
        containerType.GetMethod("Initialize")?.Invoke(
            container,
            new object[]
            {
                new Vector3(2f, 0f, 2f),
                new Vector3(8f, 0f, 2f),
                0.1f,
                3f,
                1.5f,
                visualState,
                11,
                12,
                false,
                false,
                false,
                false,
            });

        object[] arguments =
        {
            container,
            new Vector3(5.25f, 0f, 4f),
            0.01f,
            null,
        };

        MethodInfo method = plannerType.GetMethod(
            "TryGetSplitDistance",
            new[] { containerType, typeof(Vector3), typeof(float), typeof(float).MakeByRefType() });
        Assert.That(method, Is.Not.Null);

        object invoked = method.Invoke(null, arguments);
        Assert.That(invoked, Is.EqualTo(true));
        Assert.That((float)arguments[3], Is.EqualTo(3.25f).Within(0.0001f));
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
        Assert.That(constrainedPoint.x, Is.LessThan(2f));
        Assert.That(constrainedPoint.x, Is.EqualTo(2f - 0.01f).Within(0.0001f));

        arguments[2] = new Vector3(4.5f, 0f, 0f);
        invoked = method.Invoke(manager, arguments);
        Assert.That(invoked, Is.EqualTo(true));

        constrainedPoint = (Vector3)arguments[3];
        Assert.That(constrainedPoint.x, Is.EqualTo(4.5f).Within(0.0001f));
    }

    [Test]
    public void RepartitionContainerWithAdjacentWallAtSplit_MovesCrossedOpeningToAdjacentLayout()
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

        object visualState = Activator.CreateInstance(wallVisualStateType);
        Component container = AddComponent(CreateChildGameObject(wallRoot.transform, "OpeningContainer"), containerType);
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
                1f,
                2f,
                0.1f,
                0f,
            });

        object startSnapshot = managerType.GetMethod("CaptureLayoutSnapshot", new[] { containerType })
            ?.Invoke(manager, new object[] { container });

        containerType.GetMethod("SetWallSpan")?.Invoke(
            container,
            new object[] { new Vector3(5f, 0f, 0f), new Vector3(10f, 0f, 0f) });
        openingType.GetMethod("SetCenterDistance")?.Invoke(opening, new object[] { 0.5f });

        Component adjacentWall = AddComponent(CreateChildGameObject(wallRoot.transform, "AdjacentWall"), wallType);
        object wallData = Activator.CreateInstance(
            wallDataType,
            new Vector3(-2f, 0f, 0f),
            new Vector3(5f, 0f, 0f),
            0.1f,
            3f,
            1.5f);
        wallType.GetMethod("Initialize")?.Invoke(adjacentWall, new[] { wallData });
        wallType.GetMethod("SetVertexIds")?.Invoke(adjacentWall, new object[] { 21, 11 });
        wallType.GetMethod("SetSplitPointFlags")?.Invoke(adjacentWall, new object[] { false, true });

        object adjacentSnapshot = managerType.GetMethod("CaptureLayoutSnapshot", new[] { wallType })
            ?.Invoke(manager, new object[] { adjacentWall });

        LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
        LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
        object didRepartition = managerType.GetMethod("TryRepartitionContainerWithAdjacentWallAtSplit")
            ?.Invoke(
                manager,
                new[]
                {
                    container,
                    startSnapshot,
                    adjacentWall,
                    adjacentSnapshot,
                    (object)11,
                    new Vector3(5f, 0f, 0f),
                });

        Assert.That(didRepartition, Is.True);
        Component[] activeOpenings = wallRoot.GetComponentsInChildren(openingType, false);
        Assert.That(activeOpenings.Length, Is.EqualTo(1));
        Assert.That((float)openingType.GetProperty("CenterDistance")?.GetValue(activeOpenings[0]), Is.EqualTo(5f).Within(0.0001f));
        Assert.That(activeOpenings[0].GetComponentInParent(containerType).name, Does.EndWith("_A"));
    }

    [Test]
    public void ApplyContainerSpanFromExternalDrag_LightweightPreview_ReusesGeneratedSegments()
    {
        Type managerType = GetAssemblyType("WallOpeningPlacementManager");
        Type containerType = GetAssemblyType("WallOpeningContainer");
        Type openingType = GetAssemblyType("WallOpening");
        Type wallType = GetAssemblyType("Wall");
        Type wallVisualStateType = GetAssemblyType("WallVisualState");
        Type openingPlacementType = managerType.GetNestedType("OpeningPlacementType", BindingFlags.Public);

        Component manager = CreateComponent("OpeningManager", managerType);
        Component container = AddComponent(CreateGameObject("OpeningContainer"), containerType);
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

        Component opening = AddComponent(CreateChildGameObject(container.transform, "Window"), openingType);
        object windowEnumValue = Enum.Parse(openingPlacementType, "Window");
        openingType.GetMethod("Initialize")?.Invoke(
            opening,
            new object[]
            {
                manager,
                container,
                windowEnumValue,
                string.Empty,
                "S003",
                false,
                false,
                3f,
                2f,
                2f,
                0.1f,
                0f,
            });

        managerType.GetMethod("RebuildOpeningContainer")?.Invoke(manager, new object[] { container });
        Component[] beforeWalls = container.GetComponentsInChildren(wallType, true);
        Assert.That(beforeWalls, Has.Length.EqualTo(3));

        object baselineSnapshot = managerType.GetMethod("CaptureLayoutSnapshot", new[] { containerType })
            ?.Invoke(manager, new object[] { container });
        managerType.GetMethod(
                "ApplyContainerSpanFromExternalDrag",
                new[]
                {
                    containerType,
                    typeof(Vector3),
                    typeof(Vector3),
                    GetAssemblyType("UndoRedoManager+OpeningLayoutSnapshot"),
                    typeof(bool),
                })
            ?.Invoke(
                manager,
                new[]
                {
                    container,
                    new Vector3(0f, 0f, 0f),
                    new Vector3(12f, 0f, 0f),
                    baselineSnapshot,
                    false,
                });

        Component[] afterWalls = container.GetComponentsInChildren(wallType, true);
        Assert.That(afterWalls, Has.Length.EqualTo(3));
        CollectionAssert.AreEquivalent(beforeWalls, afterWalls);
        Assert.That(GetPropertyValue<Vector3>(GetPropertyValue<object>(afterWalls[2], "Data"), "endPoint").x, Is.EqualTo(12f).Within(0.0001f));
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
    public void UpdateOpeningVisual_DoesNotOverlayWindowMaterial_WhenWindowPrefabExistsOnRotatedWall()
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
                new Vector3(4f, 0f, 4f),
                0.2f,
                2.4f,
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
        GameObject windowPrefab = CreateGameObject("WindowPrefab");
        MeshRenderer prefabRenderer = windowPrefab.AddComponent<MeshRenderer>();
        windowPrefab.AddComponent<MeshFilter>();
        prefabRenderer.sharedMaterial = prefabMaterial;

        ScriptableObject catalog = ScriptableObject.CreateInstance(catalogType);
        createdObjects.Add(catalog);
        object catalogItem = Activator.CreateInstance(catalogItemType);
        object windowEnumValue = Enum.Parse(openingPlacementType, "Window");
        SetPrivateField(catalogItem, "openingType", windowEnumValue);
        SetPrivateField(catalogItem, "typeKey", "WindowA");
        SetPrivateField(catalogItem, "displayName", "Window A");
        SetPrivateField(catalogItem, "modelPrefab", windowPrefab);
        GetPrivateField<System.Collections.IList>(catalog, "items").Add(catalogItem);
        SetPrivateField(manager, "openingTypeCatalog", catalog);

        Component opening = AddComponent(CreateChildGameObject(container.transform, "Window"), openingType);
        openingType.GetMethod("Initialize")?.Invoke(
            opening,
            new object[]
            {
                manager,
                container,
                windowEnumValue,
                string.Empty,
                "WindowA",
                false,
                false,
                2f,
                1f,
                1.2f,
                0.2f,
                0.8f,
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
    public void UpdateOpeningVisual_ScalesDoorModelFromReferenceSize()
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
                0.2f,
                2.4f,
                1.5f,
                visualState,
                1,
                2,
                false,
                false,
                false,
                false,
            });

        GameObject doorPrefab = CreateGameObject("DoorPrefab");
        doorPrefab.AddComponent<MeshRenderer>();
        doorPrefab.AddComponent<MeshFilter>();

        ScriptableObject catalog = ScriptableObject.CreateInstance(catalogType);
        createdObjects.Add(catalog);
        object catalogItem = Activator.CreateInstance(catalogItemType);
        object doorEnumValue = Enum.Parse(openingPlacementType, "Door");
        SetPrivateField(catalogItem, "openingType", doorEnumValue);
        SetPrivateField(catalogItem, "typeKey", "DoorA");
        SetPrivateField(catalogItem, "displayName", "Door A");
        SetPrivateField(catalogItem, "modelPrefab", doorPrefab);
        SetPrivateField(catalogItem, "referenceSize", new Vector3(0.4f, 1.2f, 1f));
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
                2f,
                2.4f,
                0.2f,
                0f,
            });
        Transform legacyModelRoot = CreateChildGameObject(((Component)opening).transform, "ModelRoot").transform;
        CreateChildGameObject(legacyModelRoot, "ModelScaleRoot");

        InvokePrivate(manager, "UpdateOpeningVisual", container, opening, 0, null);

        Transform modelRotationRoot = ((Component)opening).transform.Find("ModelRoot/ModelRotationRoot");
        Transform modelScaleRoot = modelRotationRoot != null ? modelRotationRoot.Find("ModelScaleRoot") : null;
        Assert.That(modelRotationRoot, Is.Not.Null);
        Assert.That(modelScaleRoot, Is.Not.Null);
        Assert.That(modelScaleRoot.parent, Is.EqualTo(modelRotationRoot));
        Assert.That(((Component)opening).transform.Find("ModelRoot/ModelScaleRoot"), Is.Null);
        Assert.That(modelScaleRoot.localScale.x, Is.EqualTo(5f).Within(0.0001f));
        Assert.That(modelScaleRoot.localScale.y, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(modelScaleRoot.localScale.z, Is.EqualTo(0.2f).Within(0.0001f));
    }

    [Test]
    public void UpdateOpeningVisual_MapsModelWidthAxisAlongRotatedWall()
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
                new Vector3(4f, 0f, 4f),
                0.2f,
                2.4f,
                1.5f,
                visualState,
                1,
                2,
                false,
                false,
                false,
                false,
            });

        GameObject doorPrefab = CreateGameObject("DoorPrefab");
        Transform widthAxisProbe = CreateChildGameObject(doorPrefab.transform, "WidthAxisProbe").transform;
        widthAxisProbe.localPosition = Vector3.right;

        ScriptableObject catalog = ScriptableObject.CreateInstance(catalogType);
        createdObjects.Add(catalog);
        object catalogItem = Activator.CreateInstance(catalogItemType);
        object doorEnumValue = Enum.Parse(openingPlacementType, "Door");
        SetPrivateField(catalogItem, "openingType", doorEnumValue);
        SetPrivateField(catalogItem, "typeKey", "DoorA");
        SetPrivateField(catalogItem, "displayName", "Door A");
        SetPrivateField(catalogItem, "modelPrefab", doorPrefab);
        SetPrivateField(catalogItem, "referenceSize", new Vector3(1f, 1f, 1f));
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
                2f,
                2.4f,
                0.2f,
                0f,
            });

        InvokePrivate(manager, "UpdateOpeningVisual", container, opening, 0, null);

        Transform restoredProbe = ((Component)opening).transform.Find("ModelRoot/ModelRotationRoot/ModelScaleRoot/DoorPrefab/WidthAxisProbe");
        Assert.That(restoredProbe, Is.Not.Null);

        Vector3 widthDirection = restoredProbe.position - ((Component)opening).transform.position;
        widthDirection.y = 0f;
        widthDirection.Normalize();

        Vector3 wallDirection = ((Component)container).GetType().GetProperty("WallDirection") != null
            ? (Vector3)((Component)container).GetType().GetProperty("WallDirection").GetValue(container)
            : Vector3.zero;
        Assert.That(Vector3.Dot(widthDirection, wallDirection), Is.GreaterThan(0.99f));
    }

    [Test]
    public void UpdateOpeningVisual_ResetsImportedPivotYaw()
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
                0.2f,
                2.4f,
                1.5f,
                visualState,
                1,
                2,
                false,
                false,
                false,
                false,
            });

        GameObject doorPrefab = CreateGameObject("DoorPrefab");
        Transform pivot = CreateChildGameObject(doorPrefab.transform, "Pivot").transform;
        pivot.localRotation = Quaternion.Euler(0f, 9f, 0f);
        CreateChildGameObject(pivot, "DoorPanel");

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
                2f,
                2.4f,
                0.2f,
                0f,
            });

        InvokePrivate(manager, "UpdateOpeningVisual", container, opening, 0, null);

        Transform restoredPivot = ((Component)opening).transform.Find("ModelRoot/ModelRotationRoot/ModelScaleRoot/DoorPrefab/Pivot");
        Assert.That(restoredPivot, Is.Not.Null);
        Assert.That(NormalizeAngle(restoredPivot.localEulerAngles.y), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ParametricOpeningModel_ResizesStretchParts_WithoutScalingFixedRailingBars()
    {
        Type parametricType = GetAssemblyType("ParametricOpeningModel");
        GameObject root = CreateGameObject("BalconyPrefab");
        Component parametric = root.AddComponent(parametricType);

        Transform topRail = CreateChildGameObject(root.transform, "Stretch_Railing_Top_Rail").transform;
        topRail.localPosition = new Vector3(0f, -0.48f, 0.72f);
        topRail.localScale = Vector3.one;
        Transform verticalBar = CreateChildGameObject(root.transform, "Fixed_Railing_Vertical_Bar_01").transform;
        verticalBar.localPosition = new Vector3(0.45f, -0.72f, -0.78f);
        verticalBar.localScale = Vector3.one;

        parametricType.GetMethod("ApplyOpeningSize")?.Invoke(
            parametric,
            new object[]
            {
                new Vector3(24f, 24f, 0.5f),
                new Vector3(18f, 21f, 1.4f),
            });

        Assert.That(topRail.localScale.x, Is.EqualTo(24f / 18f).Within(0.0001f));
        Assert.That(topRail.localScale.y, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(topRail.localScale.z, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(verticalBar.localScale, Is.EqualTo(Vector3.one));
        Assert.That(verticalBar.localPosition.x, Is.EqualTo(0.45f * (24f / 18f)).Within(0.0001f));
        Assert.That(verticalBar.localPosition.y, Is.EqualTo(-0.72f).Within(0.0001f));
        Assert.That(verticalBar.localPosition.z, Is.EqualTo(-12f + (-0.78f + 10.5f)).Within(0.0001f));
    }

    [Test]
    public void ParametricOpeningModel_WhiteGlassFrameDoor_ResizesRailsFromAuthoredRailSize()
    {
        Type parametricType = GetAssemblyType("ParametricOpeningModel");
        GameObject root = CreateGameObject("WhiteGlassFrameDoorPrefab");
        Component parametric = root.AddComponent(parametricType);

        Transform topRail = CreateChildGameObject(root.transform, "Stretch_Top_Rail").transform;
        topRail.localScale = Vector3.one;
        Transform bottomRail = CreateChildGameObject(root.transform, "Stretch_Bottom_Rail").transform;
        bottomRail.localScale = Vector3.one;

        string profileKey = (string)parametricType.GetField("WhiteGlassFrameDoorProfile")?.GetValue(null);
        parametricType.GetMethod(
                "ApplyOpeningSize",
                new[] { typeof(Vector3), typeof(Vector3), typeof(bool), typeof(string) })
            ?.Invoke(
                parametric,
                new object[]
                {
                    new Vector3(12f, 1f, 8f),
                    new Vector3(10f, 1f, 6f),
                    false,
                    profileKey,
                });

        float expectedRailScaleX = (12f - 0.45f * 2f) / (10f - 0.45f * 2f);
        Assert.That(topRail.localScale.x, Is.EqualTo(expectedRailScaleX).Within(0.0001f));
        Assert.That(topRail.localScale.y, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(topRail.localScale.z, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(bottomRail.localScale.x, Is.EqualTo(expectedRailScaleX).Within(0.0001f));
        Assert.That(bottomRail.localScale.y, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(bottomRail.localScale.z, Is.EqualTo(1f).Within(0.0001f));
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

    [Test]
    public void ApplyLayoutSnapshot_DeactivatesRemovedStandaloneWallImmediately()
    {
        Type managerType = GetAssemblyType("WallOpeningPlacementManager");
        Type wallType = GetAssemblyType("Wall");
        Type wallDataType = GetAssemblyType("WallData");
        Type snapshotType = GetAssemblyType("UndoRedoManager+OpeningLayoutSnapshot");
        Type sceneHierarchyModelType = GetAssemblyType("SceneHierarchyTreeModel");

        Component manager = CreateComponent("OpeningManager", managerType);
        GameObject wallRoot = CreateGameObject("WallRoot");
        SetPrivateField(manager, "wallRoot", wallRoot.transform);

        Component wall = AddComponent(CreateChildGameObject(wallRoot.transform, "wall04"), wallType);
        object wallData = Activator.CreateInstance(
            wallDataType,
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            0.2f,
            2.4f,
            1.2f);
        wallType.GetMethod("Initialize")?.Invoke(wall, new[] { wallData });

        object currentSnapshot = managerType.GetMethod("CaptureLayoutSnapshot", new[] { wallType })
            ?.Invoke(manager, new object[] { wall });
        object emptyTargetSnapshot = Activator.CreateInstance(snapshotType);

        managerType.GetMethod("ApplyLayoutSnapshot")?.Invoke(manager, new[] { emptyTargetSnapshot, currentSnapshot });

        Assert.That(wall.gameObject.activeInHierarchy, Is.False);

        object hierarchyRows = sceneHierarchyModelType.GetMethod("BuildRows")
            ?.Invoke(null, new object[] { wallRoot.transform, null });
        Assert.That(((System.Collections.ICollection)hierarchyRows).Count, Is.EqualTo(0));
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
        MethodInfo method = ResolvePrivateMethod(target.GetType(), methodName, args);
        Assert.That(method, Is.Not.Null);
        method.Invoke(target, args);
    }

    private static MethodInfo ResolvePrivateMethod(Type targetType, string methodName, object[] args)
    {
        MethodInfo[] methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != methodName)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != args.Length)
            {
                continue;
            }

            bool matches = true;
            for (int j = 0; j < parameters.Length; j++)
            {
                if (args[j] != null && !parameters[j].ParameterType.IsInstanceOfType(args[j]))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return method;
            }
        }

        return null;
    }

    private static T GetPropertyValue<T>(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null);
        return (T)property.GetValue(target);
    }

    private static Type GetAssemblyType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Failed to resolve type '{typeName}' from Assembly-CSharp.");
        return type;
    }

    private static float NormalizeAngle(float value)
    {
        while (value > 180f)
        {
            value -= 360f;
        }

        while (value < -180f)
        {
            value += 360f;
        }

        return value;
    }
}
