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
    public void SetTopView_RestoresOnlyPreviouslyActiveTopViewRoots()
    {
        Component manager = CreateManager(out _, out _, out _, out _, out _, out _);
        topUiRoot.SetActive(false);

        InvokePublic(manager, "SetPerspectiveView");
        InvokePublic(manager, "SetTopView");

        Assert.That(topUiRoot.activeSelf, Is.False);
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
    public void ToolbarPresenter_UpdatesButtonColorsWhenViewChanges()
    {
        Component manager = CreateManager(
            out _,
            out _,
            out _,
            out _,
            out Button topButton,
            out Button perspectiveButton);
        Image topImage = topButton.gameObject.AddComponent<Image>();
        Image perspectiveImage = perspectiveButton.gameObject.AddComponent<Image>();
        Color activeColor = Color.green;
        Color inactiveColor = Color.gray;
        GameObject presenterObject = new GameObject("ToolbarPresenter");
        presenterObject.SetActive(false);

        try
        {
            Component presenter = presenterObject.AddComponent(GetAssemblyType("EditorViewModeToolbarPresenter"));
            SetPresenterReferences(
                presenter,
                manager,
                topButton,
                perspectiveButton,
                topImage,
                perspectiveImage,
                activeColor,
                inactiveColor);

            presenterObject.SetActive(true);

            Assert.That(topImage.color, Is.EqualTo(activeColor));
            Assert.That(perspectiveImage.color, Is.EqualTo(inactiveColor));

            InvokePublic(manager, "SetPerspectiveView");

            Assert.That(topImage.color, Is.EqualTo(inactiveColor));
            Assert.That(perspectiveImage.color, Is.EqualTo(activeColor));

            DestroyObject(presenterObject);
            presenterObject = null;

            Assert.DoesNotThrow(() => InvokePublic(manager, "SetTopView"));
        }
        finally
        {
            DestroyObject(presenterObject);
        }
    }

    [Test]
    public void SetPerspectiveView_ToleratesMissingReferences()
    {
        managerObject = new GameObject("EditorViewModeManager");
        Component manager = managerObject.AddComponent(GetAssemblyType("EditorViewModeManager"));

        Assert.DoesNotThrow(() => InvokePublic(manager, "SetPerspectiveView"));
        Assert.That(GetCurrentViewModeName(manager), Is.EqualTo("Perspective3D"));
    }

    [Test]
    public void PerspectiveFraming_FramesProvidedBounds()
    {
        GameObject cameraObject = new GameObject("PerspectiveCamera");
        GameObject framingObject = new GameObject("PerspectiveCameraFramingController");

        try
        {
            Camera perspectiveCamera = cameraObject.AddComponent<Camera>();
            perspectiveCamera.fieldOfView = 60f;
            Component framingController = framingObject.AddComponent(GetAssemblyType("PerspectiveCameraFramingController"));

            SetPrivateField(framingController, "perspectiveCamera", perspectiveCamera);
            SetPrivateField(framingController, "defaultYaw", -35f);
            SetPrivateField(framingController, "defaultPitch", 45f);
            SetPrivateField(framingController, "distancePadding", 1.2f);

            object result = InvokePublicWithResult(
                framingController,
                "FrameBounds",
                new Bounds(Vector3.zero, new Vector3(10f, 3f, 8f)));

            Assert.That(result, Is.EqualTo(true));
            Assert.That(perspectiveCamera.transform.position, Is.Not.EqualTo(Vector3.zero));
            float centerDot = Vector3.Dot(perspectiveCamera.transform.forward, (Vector3.zero - perspectiveCamera.transform.position).normalized);
            Assert.That(centerDot, Is.GreaterThan(0.98f));
        }
        finally
        {
            DestroyObject(framingObject);
            DestroyObject(cameraObject);
        }
    }

    [Test]
    public void PerspectiveCameraManager_SyncsRotationStateFromCameraTransform()
    {
        GameObject cameraObject = new GameObject("PerspectiveCamera");
        cameraObject.SetActive(false);

        try
        {
            Camera perspectiveCamera = cameraObject.AddComponent<Camera>();
            Component cameraManager = cameraObject.AddComponent(GetAssemblyType("CameraManager_3D"));
            SetPrivateField(cameraManager, "targetCamera", perspectiveCamera);
            perspectiveCamera.transform.rotation = Quaternion.Euler(35f, 125f, 0f);

            InvokePublic(cameraManager, "SyncRotationFromCameraTransform");

            Assert.That(GetPrivateField<float>(cameraManager, "yaw"), Is.EqualTo(125f).Within(0.001f));
            Assert.That(GetPrivateField<float>(cameraManager, "pitch"), Is.EqualTo(35f).Within(0.001f));
        }
        finally
        {
            DestroyObject(cameraObject);
        }
    }

    [Test]
    public void PerspectiveFraming_SyncsSeparateCameraManagerAfterFrame()
    {
        GameObject cameraObject = new GameObject("PerspectiveCamera");
        GameObject cameraManagerObject = new GameObject("PerspectiveCameraManager");
        GameObject framingObject = new GameObject("PerspectiveCameraFramingController");
        cameraManagerObject.SetActive(false);

        try
        {
            Camera perspectiveCamera = cameraObject.AddComponent<Camera>();
            Component cameraManager = cameraManagerObject.AddComponent(GetAssemblyType("CameraManager_3D"));
            Component framingController = framingObject.AddComponent(GetAssemblyType("PerspectiveCameraFramingController"));

            SetPrivateField(cameraManager, "targetCamera", perspectiveCamera);
            SetPrivateField(framingController, "perspectiveCamera", perspectiveCamera);
            SetPrivateField(framingController, "perspectiveCameraManager", cameraManager);
            SetPrivateField(framingController, "defaultYaw", -35f);
            SetPrivateField(framingController, "defaultPitch", 45f);

            object result = InvokePublicWithResult(
                framingController,
                "FrameBounds",
                new Bounds(Vector3.zero, new Vector3(10f, 3f, 8f)));

            Assert.That(result, Is.EqualTo(true));
            Assert.That(GetPrivateField<float>(cameraManager, "yaw"), Is.EqualTo(perspectiveCamera.transform.eulerAngles.y).Within(0.001f));
            Assert.That(GetPrivateField<float>(cameraManager, "pitch"), Is.EqualTo(NormalizePitchForTest(perspectiveCamera.transform.eulerAngles.x)).Within(0.001f));
        }
        finally
        {
            DestroyObject(framingObject);
            DestroyObject(cameraManagerObject);
            DestroyObject(cameraObject);
        }
    }

    [Test]
    public void PerspectiveFraming_FitsWideBoundsForNarrowAspect()
    {
        GameObject cameraObject = new GameObject("PerspectiveCamera");
        GameObject framingObject = new GameObject("PerspectiveCameraFramingController");

        try
        {
            Camera perspectiveCamera = cameraObject.AddComponent<Camera>();
            perspectiveCamera.fieldOfView = 60f;
            perspectiveCamera.aspect = 0.5f;
            Component framingController = framingObject.AddComponent(GetAssemblyType("PerspectiveCameraFramingController"));
            Bounds wideBounds = new Bounds(Vector3.zero, new Vector3(30f, 2f, 2f));

            SetPrivateField(framingController, "perspectiveCamera", perspectiveCamera);
            SetPrivateField(framingController, "defaultYaw", 0f);
            SetPrivateField(framingController, "defaultPitch", 30f);
            SetPrivateField(framingController, "distancePadding", 1.2f);

            object result = InvokePublicWithResult(framingController, "FrameBounds", wideBounds);

            Assert.That(result, Is.EqualTo(true));
            AssertBoundsVisible(perspectiveCamera, wideBounds);
        }
        finally
        {
            DestroyObject(framingObject);
            DestroyObject(cameraObject);
        }
    }

    [Test]
    public void PerspectiveFraming_UsesExplicitSelectionBoundsBeforeSceneBounds()
    {
        GameObject cameraObject = new GameObject("PerspectiveCamera");
        GameObject framingObject = new GameObject("PerspectiveCameraFramingController");

        try
        {
            Camera perspectiveCamera = cameraObject.AddComponent<Camera>();
            perspectiveCamera.fieldOfView = 60f;
            Component framingController = framingObject.AddComponent(GetAssemblyType("PerspectiveCameraFramingController"));
            Bounds selectionBounds = new Bounds(new Vector3(20f, 0f, 0f), new Vector3(2f, 2f, 2f));
            Bounds sceneBounds = new Bounds(Vector3.zero, new Vector3(30f, 2f, 30f));

            SetPrivateField(framingController, "perspectiveCamera", perspectiveCamera);

            object result = InvokePublicWithResult(
                framingController,
                "FrameSelectionOrSceneBoundsForTests",
                selectionBounds,
                true,
                sceneBounds,
                true);

            Assert.That(result, Is.EqualTo(true));
            Vector3 targetDirection = (selectionBounds.center - perspectiveCamera.transform.position).normalized;
            float centerDot = Vector3.Dot(perspectiveCamera.transform.forward, targetDirection);
            Assert.That(centerDot, Is.GreaterThan(0.98f));
        }
        finally
        {
            DestroyObject(framingObject);
            DestroyObject(cameraObject);
        }
    }

    [Test]
    public void PerspectiveFraming_IgnoresLargeGridWhenContentExists()
    {
        GameObject framingObject = new GameObject("PerspectiveCameraFramingController");
        GameObject wallRootObject = new GameObject("Walls");
        GameObject wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject gridObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            wallObject.name = "Wall";
            wallObject.transform.SetParent(wallRootObject.transform);
            wallObject.transform.position = Vector3.zero;
            wallObject.transform.localScale = new Vector3(10f, 3f, 8f);

            gridObject.name = "Grid";
            gridObject.transform.position = Vector3.zero;
            gridObject.transform.localScale = new Vector3(5000f, 0.01f, 5000f);

            Component framingController = framingObject.AddComponent(GetAssemblyType("PerspectiveCameraFramingController"));
            SetPrivateField(framingController, "wallRoot", wallRootObject.transform);
            SetPrivateField(framingController, "gridObject", gridObject);

            object[] arguments = { null };
            object result = InvokePublicWithResult(framingController, "TryGetSceneBounds", arguments);
            Bounds sceneBounds = (Bounds)arguments[0];

            Assert.That(result, Is.EqualTo(true));
            Assert.That(sceneBounds.size.x, Is.LessThan(100f));
            Assert.That(sceneBounds.size.z, Is.LessThan(100f));
        }
        finally
        {
            DestroyObject(gridObject);
            DestroyObject(wallObject);
            DestroyObject(wallRootObject);
            DestroyObject(framingObject);
        }
    }

    [Test]
    public void PerspectiveFraming_UsesLimitedFallbackWhenOnlyGridExists()
    {
        GameObject framingObject = new GameObject("PerspectiveCameraFramingController");
        GameObject gridObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            gridObject.name = "Grid";
            gridObject.transform.localScale = new Vector3(5000f, 0.01f, 5000f);

            Component framingController = framingObject.AddComponent(GetAssemblyType("PerspectiveCameraFramingController"));
            SetPrivateField(framingController, "gridObject", gridObject);
            SetPrivateField(framingController, "emptySceneFallbackBoundsSize", new Vector3(100f, 10f, 100f));

            object[] arguments = { null };
            object result = InvokePublicWithResult(framingController, "TryGetSceneBounds", arguments);
            Bounds sceneBounds = (Bounds)arguments[0];

            Assert.That(result, Is.EqualTo(true));
            Assert.That(sceneBounds.size, Is.EqualTo(new Vector3(100f, 10f, 100f)));
        }
        finally
        {
            DestroyObject(gridObject);
            DestroyObject(framingObject);
        }
    }

    [Test]
    public void PerspectiveHighlight_CreatesAndClearsTransientHighlight()
    {
        GameObject selectedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject controllerObject = new GameObject("PerspectiveSelectionHighlightController");

        try
        {
            Component controller = controllerObject.AddComponent(GetAssemblyType("PerspectiveSelectionHighlightController"));

            bool created = (bool)InvokePublicWithResult(controller, "ShowHighlightForTarget", selectedObject);

            Assert.That(created, Is.True);
            Transform highlightRoot = controllerObject.transform.Find("PerspectiveSelectionHighlights");
            Assert.That(highlightRoot, Is.Not.Null);
            Transform highlight = highlightRoot.Find("PerspectiveSelectionHighlight");
            Assert.That(highlight, Is.Not.Null);
            LineRenderer lineRenderer = highlight.GetComponent<LineRenderer>();
            Assert.That(lineRenderer, Is.Not.Null);
            Assert.That(lineRenderer.widthMultiplier, Is.GreaterThanOrEqualTo(0.1f));
            Assert.That(lineRenderer.startColor.a, Is.GreaterThanOrEqualTo(0.9f));
            Assert.That(lineRenderer.sortingOrder, Is.GreaterThanOrEqualTo(1000));
            Assert.That(highlight.GetComponent<MeshRenderer>(), Is.Null);
            Assert.That(highlight.GetComponent<Collider>(), Is.Null);
            Assert.That(selectedObject.transform.Find("PerspectiveSelectionHighlight"), Is.Null);

            InvokePublic(controller, "ClearHighlight");

            Assert.That(controllerObject.transform.Find("PerspectiveSelectionHighlights"), Is.Null);
        }
        finally
        {
            DestroyObject(controllerObject);
            DestroyObject(selectedObject);
        }
    }

    [Test]
    public void PerspectiveHighlight_UsesWallDataOutlineForWall()
    {
        GameObject wallObject = new GameObject("Wall");
        GameObject controllerObject = new GameObject("PerspectiveSelectionHighlightController");

        try
        {
            Component wall = wallObject.AddComponent(GetAssemblyType("Wall"));
            object wallData = Activator.CreateInstance(
                GetAssemblyType("WallData"),
                new Vector3(0f, 0f, 0f),
                new Vector3(4f, 0f, 0f),
                0.4f,
                3f,
                1.5f);
            InvokePublicWithResult(wall, "Initialize", wallData);
            Component controller = controllerObject.AddComponent(GetAssemblyType("PerspectiveSelectionHighlightController"));

            bool created = (bool)InvokePublicWithResult(controller, "ShowHighlightForTarget", wallObject);

            Assert.That(created, Is.True);
            Transform highlight = controllerObject.transform.Find("PerspectiveSelectionHighlights/PerspectiveSelectionHighlight");
            Assert.That(highlight, Is.Not.Null);
            LineRenderer lineRenderer = highlight.GetComponent<LineRenderer>();
            Assert.That(lineRenderer, Is.Not.Null);
            Assert.That(lineRenderer.positionCount, Is.EqualTo(24));
        }
        finally
        {
            DestroyObject(controllerObject);
            DestroyObject(wallObject);
        }
    }

    [Test]
    public void PerspectiveHighlight_UsesPolygonOutlineForRoom()
    {
        GameObject roomObject = new GameObject("Room");
        GameObject controllerObject = new GameObject("PerspectiveSelectionHighlightController");

        try
        {
            Component room = roomObject.AddComponent(GetAssemblyType("Room"));
            bool roomInitialized = (bool)InvokePublicWithResult(
                room,
                "SetManualBoundaryVertices",
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(4f, 0f, 0f),
                    new Vector3(4f, 0f, 3f),
                    new Vector3(0f, 0f, 3f),
                },
                false);
            Assert.That(roomInitialized, Is.True);
            Component controller = controllerObject.AddComponent(GetAssemblyType("PerspectiveSelectionHighlightController"));

            bool created = (bool)InvokePublicWithResult(controller, "ShowHighlightForTarget", roomObject);

            Assert.That(created, Is.True);
            Transform highlight = controllerObject.transform.Find("PerspectiveSelectionHighlights/PerspectiveSelectionHighlight");
            Assert.That(highlight, Is.Not.Null);
            LineRenderer lineRenderer = highlight.GetComponent<LineRenderer>();
            Assert.That(lineRenderer, Is.Not.Null);
            Assert.That(lineRenderer.positionCount, Is.EqualTo(5));
        }
        finally
        {
            DestroyObject(controllerObject);
            DestroyObject(roomObject);
        }
    }

    [Test]
    public void PerspectiveHighlight_AddsWeakAreaTintForRoom()
    {
        GameObject roomObject = new GameObject("Room");
        GameObject controllerObject = new GameObject("PerspectiveSelectionHighlightController");

        try
        {
            Component room = roomObject.AddComponent(GetAssemblyType("Room"));
            bool roomInitialized = (bool)InvokePublicWithResult(
                room,
                "SetManualBoundaryVertices",
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(4f, 0f, 0f),
                    new Vector3(4f, 0f, 3f),
                    new Vector3(0f, 0f, 3f),
                },
                false);
            Assert.That(roomInitialized, Is.True);
            Component controller = controllerObject.AddComponent(GetAssemblyType("PerspectiveSelectionHighlightController"));

            bool created = (bool)InvokePublicWithResult(controller, "ShowHighlightForTarget", roomObject);

            Assert.That(created, Is.True);
            Transform tint = controllerObject.transform.Find("PerspectiveSelectionHighlights/PerspectiveSelectionRoomTint");
            Assert.That(tint, Is.Not.Null);

            MeshFilter meshFilter = tint.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = tint.GetComponent<MeshRenderer>();
            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(meshRenderer, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh.vertexCount, Is.EqualTo(4));
            Assert.That(meshFilter.sharedMesh.triangles.Length, Is.EqualTo(6));
            Assert.That(meshRenderer.sharedMaterial.color.a, Is.LessThan(0.2f));
            Assert.That(tint.GetComponent<Collider>(), Is.Null);
        }
        finally
        {
            DestroyObject(controllerObject);
            DestroyObject(roomObject);
        }
    }

    [Test]
    public void PerspectiveHighlight_DrawsRoomTintAboveEnclosingWalls()
    {
        GameObject roomObject = new GameObject("Room");
        GameObject firstWallObject = new GameObject("WallA");
        GameObject secondWallObject = new GameObject("WallB");
        GameObject controllerObject = new GameObject("PerspectiveSelectionHighlightController");

        try
        {
            Component room = roomObject.AddComponent(GetAssemblyType("Room"));
            bool roomInitialized = (bool)InvokePublicWithResult(
                room,
                "SetManualBoundaryVertices",
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(4f, 0f, 0f),
                    new Vector3(4f, 0f, 3f),
                    new Vector3(0f, 0f, 3f),
                },
                false);
            Assert.That(roomInitialized, Is.True);

            Component firstWall = CreateWall(firstWallObject, new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f), 0.4f, 3f, 1.5f);
            Component secondWall = CreateWall(secondWallObject, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 3f), 0.4f, 4f, 2f);
            SetRoomWallSet(room, firstWall, secondWall);

            Component controller = controllerObject.AddComponent(GetAssemblyType("PerspectiveSelectionHighlightController"));

            bool created = (bool)InvokePublicWithResult(controller, "ShowHighlightForTarget", roomObject);

            Assert.That(created, Is.True);
            Transform tint = controllerObject.transform.Find("PerspectiveSelectionHighlights/PerspectiveSelectionRoomTint");
            Assert.That(tint, Is.Not.Null);
            MeshFilter meshFilter = tint.GetComponent<MeshFilter>();
            Assert.That(meshFilter, Is.Not.Null);
            Assert.That(meshFilter.sharedMesh.vertices[0].y, Is.GreaterThan(4f));
        }
        finally
        {
            DestroyObject(controllerObject);
            DestroyObject(secondWallObject);
            DestroyObject(firstWallObject);
            DestroyObject(roomObject);
        }
    }

    [Test]
    public void PerspectiveHighlight_DrawsRoomOutlineAboveEnclosingWalls()
    {
        GameObject roomObject = new GameObject("Room");
        GameObject firstWallObject = new GameObject("WallA");
        GameObject secondWallObject = new GameObject("WallB");
        GameObject controllerObject = new GameObject("PerspectiveSelectionHighlightController");

        try
        {
            Component room = roomObject.AddComponent(GetAssemblyType("Room"));
            bool roomInitialized = (bool)InvokePublicWithResult(
                room,
                "SetManualBoundaryVertices",
                new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(4f, 0f, 0f),
                    new Vector3(4f, 0f, 3f),
                    new Vector3(0f, 0f, 3f),
                },
                false);
            Assert.That(roomInitialized, Is.True);

            Component firstWall = CreateWall(firstWallObject, new Vector3(0f, 0f, 0f), new Vector3(4f, 0f, 0f), 0.4f, 3f, 1.5f);
            Component secondWall = CreateWall(secondWallObject, new Vector3(4f, 0f, 0f), new Vector3(4f, 0f, 3f), 0.4f, 4f, 2f);
            SetRoomWallSet(room, firstWall, secondWall);

            Component controller = controllerObject.AddComponent(GetAssemblyType("PerspectiveSelectionHighlightController"));

            bool created = (bool)InvokePublicWithResult(controller, "ShowHighlightForTarget", roomObject);

            Assert.That(created, Is.True);
            Transform highlight = controllerObject.transform.Find("PerspectiveSelectionHighlights/PerspectiveSelectionHighlight");
            Assert.That(highlight, Is.Not.Null);
            LineRenderer lineRenderer = highlight.GetComponent<LineRenderer>();
            Assert.That(lineRenderer, Is.Not.Null);
            Assert.That(lineRenderer.GetPosition(0).y, Is.GreaterThan(4f));
        }
        finally
        {
            DestroyObject(controllerObject);
            DestroyObject(secondWallObject);
            DestroyObject(firstWallObject);
            DestroyObject(roomObject);
        }
    }

    [Test]
    public void WallSelectionManager_IgnoresPerspectiveRightClick()
    {
        GameObject viewModeObject = new GameObject("EditorViewModeManager");
        GameObject selectionObject = new GameObject("WallSelectionManager");
        GameObject wallObject = new GameObject("SelectedWall");

        try
        {
            Component viewModeManager = viewModeObject.AddComponent(GetAssemblyType("EditorViewModeManager"));
            InvokePublic(viewModeManager, "SetPerspectiveView");

            Component selectionManager = selectionObject.AddComponent(GetAssemblyType("WallSelectionManager"));
            SetPrivateField(selectionManager, "viewModeManager", viewModeManager);
            InvokePublicWithResult(selectionManager, "SetSelectedWall", wallObject);

            object inputFrame = CreateEditorInputFrame(rightPressedThisFrame: true);
            InvokePublicWithResult(selectionManager, "HandleEditorInput", inputFrame);

            Assert.That(GetPublicProperty<GameObject>(selectionManager, "SelectedWall"), Is.EqualTo(wallObject));
        }
        finally
        {
            DestroyObject(wallObject);
            DestroyObject(selectionObject);
            DestroyObject(viewModeObject);
            DestroyObject(GameObject.Find("EditorInputManager"));
        }
    }

    [Test]
    public void FurnitureMenu_IsVisibleOnlyInTopFurnitureMode()
    {
        GameObject modeObject = new GameObject("ModeManager");
        GameObject viewModeObject = new GameObject("EditorViewModeManager");
        GameObject menuRoot = new GameObject("_FurnishMenu");
        GameObject controllerObject = new GameObject("FurnitureMenuController");
        controllerObject.SetActive(false);

        try
        {
            Component modeManager = modeObject.AddComponent(GetAssemblyType("ModeManager"));
            Component viewModeManager = viewModeObject.AddComponent(GetAssemblyType("EditorViewModeManager"));
            Component controller = controllerObject.AddComponent(GetAssemblyType("FurnitureMenuController"));
            SetPrivateField(controller, "modeManager", modeManager);
            SetPrivateField(controller, "viewModeManager", viewModeManager);
            SetPrivateField(controller, "furnitureMenuRoot", menuRoot);

            controllerObject.SetActive(true);
            SetEditorMode(modeManager, "FurniturePlace");

            Assert.That(menuRoot.activeSelf, Is.True);

            InvokePublic(viewModeManager, "SetPerspectiveView");

            Assert.That(menuRoot.activeSelf, Is.False);

            InvokePublic(viewModeManager, "SetTopView");

            Assert.That(menuRoot.activeSelf, Is.True);
        }
        finally
        {
            DestroyObject(controllerObject);
            DestroyObject(menuRoot);
            DestroyObject(viewModeObject);
            DestroyObject(modeObject);
        }
    }

    [Test]
    public void FurniturePlacement_BeginPlacementReturnsToTopView()
    {
        GameObject modeObject = new GameObject("ModeManager");
        GameObject viewModeObject = new GameObject("EditorViewModeManager");
        GameObject cameraObject = new GameObject("TopCamera");
        GameObject prefabObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject placementObject = new GameObject("FurniturePlacementManager");
        placementObject.SetActive(false);

        try
        {
            Component modeManager = modeObject.AddComponent(GetAssemblyType("ModeManager"));
            Component viewModeManager = viewModeObject.AddComponent(GetAssemblyType("EditorViewModeManager"));
            InvokePublic(viewModeManager, "SetPerspectiveView");

            Camera camera = cameraObject.AddComponent<Camera>();
            Component placementManager = placementObject.AddComponent(GetAssemblyType("FurniturePlacementManager"));
            SetPrivateField(placementManager, "modeManager", modeManager);
            SetPrivateField(placementManager, "viewModeManager", viewModeManager);
            SetPrivateField(placementManager, "targetCamera", camera);
            placementObject.SetActive(true);

            object item = Activator.CreateInstance(GetAssemblyType("FurnitureCatalogItem"));
            FieldInfo prefabField = item.GetType().GetField("prefab", BindingFlags.Instance | BindingFlags.Public);
            Assert.That(prefabField, Is.Not.Null);
            prefabField.SetValue(item, prefabObject);

            InvokePublicWithResult(placementManager, "BeginPlacement", item);

            Assert.That(GetCurrentViewModeName(viewModeManager), Is.EqualTo("Top"));
            Assert.That(GetCurrentEditorModeName(modeManager), Is.EqualTo("FurniturePlace"));
        }
        finally
        {
            DestroyObject(placementObject);
            DestroyObject(prefabObject);
            DestroyObject(cameraObject);
            DestroyObject(viewModeObject);
            DestroyObject(modeObject);
            DestroyObject(GameObject.Find("EditorInputManager"));
            DestroyObject(GameObject.Find("FurnitureRoot"));
        }
    }

    [Test]
    public void FurniturePlacement_UsesPointerRoomBeforeStrictBoundsRoom()
    {
        GameObject placementObject = new GameObject("FurniturePlacementManager");
        GameObject roomObject = new GameObject("Room");

        try
        {
            Component placementManager = placementObject.AddComponent(GetAssemblyType("FurniturePlacementManager"));
            Component room = roomObject.AddComponent(GetAssemblyType("Room"));

            object result = InvokePrivateWithResult(
                placementManager,
                "ResolvePlacementRoom",
                new Bounds(new Vector3(100f, 0f, 100f), Vector3.one),
                room);

            Assert.That(result, Is.EqualTo(room));
        }
        finally
        {
            DestroyObject(roomObject);
            DestroyObject(placementObject);
            DestroyObject(GameObject.Find("EditorInputManager"));
            DestroyObject(GameObject.Find("FurnitureRoot"));
        }
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

    private static void SetEditorMode(Component manager, string modeName)
    {
        MethodInfo method = manager.GetType().GetMethod("SetMode", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null);
        object mode = Enum.Parse(GetAssemblyType("EditorMode"), modeName);
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

    private static void SetPresenterReferences(
        Component presenter,
        Component manager,
        Button topButton,
        Button perspectiveButton,
        Image topImage,
        Image perspectiveImage,
        Color activeColor,
        Color inactiveColor)
    {
        SetPrivateField(presenter, "viewModeManager", manager);
        SetPrivateField(presenter, "topButton", topButton);
        SetPrivateField(presenter, "perspectiveButton", perspectiveButton);
        SetPrivateField(presenter, "topButtonBackground", topImage);
        SetPrivateField(presenter, "perspectiveButtonBackground", perspectiveImage);
        SetPrivateField(presenter, "activeColor", activeColor);
        SetPrivateField(presenter, "inactiveColor", inactiveColor);
    }

    private static void SetPrivateField(Component target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field {fieldName} on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static T GetPublicProperty<T>(Component target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, $"Expected public property {propertyName} on {target.GetType().Name}.");
        return (T)property.GetValue(target);
    }

    private static T GetPrivateField<T>(Component target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private field {fieldName} on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static float NormalizePitchForTest(float eulerX)
    {
        return eulerX > 180f ? eulerX - 360f : eulerX;
    }

    private static void InvokePublic(Component manager, string methodName)
    {
        MethodInfo method = manager.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null);
        method.Invoke(manager, Array.Empty<object>());
    }

    private static object InvokePublicWithResult(Component target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(target, arguments);
    }

    private static object InvokePrivateWithResult(Component target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        return method.Invoke(target, arguments);
    }

    private static Component CreateWall(GameObject wallObject, Vector3 start, Vector3 end, float thickness, float height, float centerY)
    {
        Component wall = wallObject.AddComponent(GetAssemblyType("Wall"));
        object wallData = Activator.CreateInstance(
            GetAssemblyType("WallData"),
            start,
            end,
            thickness,
            height,
            centerY);
        InvokePublicWithResult(wall, "Initialize", wallData);
        return wall;
    }

    private static void SetRoomWallSet(Component room, params Component[] walls)
    {
        Type wallType = GetAssemblyType("Wall");
        Type wallSetType = typeof(System.Collections.Generic.HashSet<>).MakeGenericType(wallType);
        object wallSet = Activator.CreateInstance(wallSetType);
        MethodInfo addMethod = wallSetType.GetMethod("Add", new[] { wallType });
        Assert.That(addMethod, Is.Not.Null);

        for (int i = 0; i < walls.Length; i++)
        {
            addMethod.Invoke(wallSet, new object[] { walls[i] });
        }

        PropertyInfo property = room.GetType().GetProperty("WallSet", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null);
        property.SetValue(room, wallSet);
    }

    private static object CreateEditorInputFrame(bool rightPressedThisFrame)
    {
        object defaultMode = Enum.Parse(GetAssemblyType("EditorMode"), "Default");
        return Activator.CreateInstance(
            GetAssemblyType("EditorInputFrame"),
            defaultMode,
            true,
            Vector2.zero,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            rightPressedThisFrame,
            rightPressedThisFrame,
            Vector2.zero,
            0f,
            false,
            false,
            false,
            false,
            false,
            false);
    }

    private static void AssertBoundsVisible(Camera camera, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(corners[i]);
            Assert.That(viewportPoint.z, Is.GreaterThan(0f));
            Assert.That(viewportPoint.x, Is.InRange(-0.001f, 1.001f));
            Assert.That(viewportPoint.y, Is.InRange(-0.001f, 1.001f));
        }
    }

    private static string GetCurrentViewModeName(Component manager)
    {
        PropertyInfo property = manager.GetType().GetProperty("CurrentViewMode", BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null);
        return property.GetValue(manager)?.ToString();
    }

    private static string GetCurrentEditorModeName(Component manager)
    {
        PropertyInfo property = manager.GetType().GetProperty("CurrentMode", BindingFlags.Instance | BindingFlags.Public);
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
