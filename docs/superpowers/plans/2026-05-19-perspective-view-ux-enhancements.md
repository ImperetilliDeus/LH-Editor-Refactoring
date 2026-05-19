# Perspective View UX Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the existing Top / 3D Perspective toggle with clearer toolbar state, automatic 3D camera framing, selection-aware focus, and read-only 3D selection highlight.

**Architecture:** Keep `EditorViewModeManager` as the owner of view state. Add small companion components for toolbar presentation, camera framing, selection focus, and transient highlight so the existing edit mode, wall data, room data, save/load schema, and export pipeline remain unchanged.

**Tech Stack:** Unity 6000.0.66f2, C#, Unity UI `Button`/`Image`, optional TMP status text, existing `WallSelectionManager`, `RoomAuthoringPanelManager`, `Room`, `Wall`, and NUnit EditMode tests.

---

## File Structure

- Create `Assets/Scripts/Camera/EditorViewModeToolbarPresenter.cs`
  - Updates active/inactive button visuals and optional status label.
- Create `Assets/Scripts/Camera/PerspectiveCameraFramingController.cs`
  - Computes bounds and positions the Perspective camera.
  - Frames whole scene or current selection.
- Create `Assets/Scripts/Camera/PerspectiveSelectionHighlightController.cs`
  - Creates and clears transient read-only 3D highlights.
- Modify `Assets/Scripts/Camera/EditorViewModeManager.cs`
  - Expose a non-mutating `IsPerspectiveViewActive` helper if needed.
  - Keep existing button binding behavior intact.
- Modify `Assets/Scripts/Draw/Wall/Core/WallSelectionManager.cs`
  - Only if needed to expose selected wall collections already available through `GetSelectedWalls`.
- Modify `Assets/Tests/Editor/EditorViewModeManagerTests.cs`
  - Add focused tests for presenter, framing, focus, and highlight behavior using the existing reflection style.
- Modify `docs/operations.md`
  - Add wiring notes for presenter, framing controller, focus behavior, and highlight controller.

---

### Task 1: Toolbar Visual Presenter

**Files:**
- Create: `Assets/Scripts/Camera/EditorViewModeToolbarPresenter.cs`
- Test: `Assets/Tests/Editor/EditorViewModeManagerTests.cs`

- [x] **Step 1: Add failing presenter tests**

Append these tests to `EditorViewModeManagerTests.cs`, using reflection helpers like the existing tests:

```csharp
[Test]
public void ToolbarPresenter_UpdatesButtonColorsWhenViewChanges()
{
    Component manager = CreateManager(out _, out _, out _, out _, out Button topButton, out Button perspectiveButton);
    Image topImage = topButton.gameObject.AddComponent<Image>();
    Image perspectiveImage = perspectiveButton.gameObject.AddComponent<Image>();
    GameObject presenterObject = new GameObject("ToolbarPresenter");
    Component presenter = presenterObject.AddComponent(GetAssemblyType("EditorViewModeToolbarPresenter"));
    SetPresenterReferences(
        presenter,
        manager,
        topButton,
        perspectiveButton,
        topImage,
        perspectiveImage,
        new Color(0.1f, 0.7f, 0.4f, 1f),
        new Color(0.2f, 0.2f, 0.2f, 1f));

    InvokePublic(presenter, "Refresh");

    Assert.That(topImage.color, Is.EqualTo(new Color(0.1f, 0.7f, 0.4f, 1f)));
    Assert.That(perspectiveImage.color, Is.EqualTo(new Color(0.2f, 0.2f, 0.2f, 1f)));

    InvokePublic(manager, "SetPerspectiveView");
    InvokePublic(presenter, "Refresh");

    Assert.That(topImage.color, Is.EqualTo(new Color(0.2f, 0.2f, 0.2f, 1f)));
    Assert.That(perspectiveImage.color, Is.EqualTo(new Color(0.1f, 0.7f, 0.4f, 1f)));

    DestroyObject(presenterObject);
}
```

Add helper:

```csharp
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
```

If `SetPrivateField` does not exist, add:

```csharp
private static void SetPrivateField(Component target, string fieldName, object value)
{
    FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
    Assert.That(field, Is.Not.Null, $"Expected field {fieldName} on {target.GetType().Name}.");
    field.SetValue(target, value);
}
```

- [x] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring\.worktrees\top-perspective-view-toggle' -runTests -testPlatform EditMode -testResults 'E:\Unity\LH Editor_Refactoring\.worktrees\top-perspective-view-toggle\Temp\perspective-ux-tests.xml' -quit
```

Expected: FAIL because `EditorViewModeToolbarPresenter` does not exist. In this environment Unity may exit `0` without producing XML; if that happens, inspect `Editor.log` for compiler errors.

- [x] **Step 3: Implement `EditorViewModeToolbarPresenter`**

Create `Assets/Scripts/Camera/EditorViewModeToolbarPresenter.cs`:

```csharp
using UnityEngine;
using UnityEngine.UI;

public sealed class EditorViewModeToolbarPresenter : MonoBehaviour
{
    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private Button topButton;
    [SerializeField] private Button perspectiveButton;
    [SerializeField] private Image topButtonBackground;
    [SerializeField] private Image perspectiveButtonBackground;
    [SerializeField] private Image topIcon;
    [SerializeField] private Image perspectiveIcon;
    [SerializeField] private Color activeColor = new Color(0.14f, 0.56f, 0.44f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.18f, 0.2f, 0.23f, 1f);
    [SerializeField] private Color activeIconColor = Color.white;
    [SerializeField] private Color inactiveIconColor = new Color(1f, 1f, 1f, 0.62f);

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
        Refresh();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    public void Refresh()
    {
        if (viewModeManager == null)
        {
            return;
        }

        bool topActive = viewModeManager.CurrentViewMode == EditorViewMode.Top;
        ApplyVisual(topButtonBackground, topActive);
        ApplyVisual(perspectiveButtonBackground, !topActive);
        ApplyIcon(topIcon, topActive);
        ApplyIcon(perspectiveIcon, !topActive);
    }

    private void ResolveReferences()
    {
        if (viewModeManager == null)
        {
            LayerUtility.ResolveObject(ref viewModeManager);
        }
    }

    private void BindEvents()
    {
        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
            viewModeManager.ViewModeChanged += HandleViewModeChanged;
        }
    }

    private void UnbindEvents()
    {
        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        }
    }

    private void HandleViewModeChanged(EditorViewMode mode)
    {
        Refresh();
    }

    private void ApplyVisual(Image image, bool active)
    {
        if (image != null)
        {
            image.color = active ? activeColor : inactiveColor;
        }
    }

    private void ApplyIcon(Image image, bool active)
    {
        if (image != null)
        {
            image.color = active ? activeIconColor : inactiveIconColor;
        }
    }
}
```

- [x] **Step 4: Run focused tests**

Run the Unity command from Step 2.

Expected: presenter test compiles and passes, or Unity exits `0` with compiler `ExitCode: 0` but no XML due to the known environment issue.

- [ ] **Step 5: Commit Task 1**

```powershell
git add -- 'Assets/Scripts/Camera/EditorViewModeToolbarPresenter.cs' 'Assets/Tests/Editor/EditorViewModeManagerTests.cs'
git commit -m "Add view mode toolbar presenter"
```

---

### Task 2: Whole-Scene Perspective Camera Fit

**Files:**
- Create: `Assets/Scripts/Camera/PerspectiveCameraFramingController.cs`
- Test: `Assets/Tests/Editor/EditorViewModeManagerTests.cs`

- [x] **Step 1: Add failing framing tests**

Append:

```csharp
[Test]
public void PerspectiveFraming_FramesProvidedBounds()
{
    GameObject cameraObject = new GameObject("PerspectiveCamera");
    Camera camera = cameraObject.AddComponent<Camera>();
    camera.fieldOfView = 60f;
    GameObject framingObject = new GameObject("PerspectiveCameraFramingController");
    Component framing = framingObject.AddComponent(GetAssemblyType("PerspectiveCameraFramingController"));
    SetPrivateField(framing, "perspectiveCamera", camera);
    SetPrivateField(framing, "defaultYaw", -35f);
    SetPrivateField(framing, "defaultPitch", 45f);
    SetPrivateField(framing, "distancePadding", 1.2f);

    bool framed = (bool)InvokePublicWithResult(
        framing,
        "FrameBounds",
        new Bounds(Vector3.zero, new Vector3(10f, 3f, 8f)));

    Assert.That(framed, Is.True);
    Assert.That(camera.transform.position, Is.Not.EqualTo(Vector3.zero));
    Assert.That(Vector3.Dot(camera.transform.forward, (Vector3.zero - camera.transform.position).normalized), Is.GreaterThan(0.98f));

    DestroyObject(framingObject);
    DestroyObject(cameraObject);
}
```

Add helper:

```csharp
private static object InvokePublicWithResult(Component target, string methodName, params object[] arguments)
{
    MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
    Assert.That(method, Is.Not.Null);
    return method.Invoke(target, arguments);
}
```

- [x] **Step 2: Run tests and verify failure**

Run the focused Unity command.

Expected: FAIL because `PerspectiveCameraFramingController` does not exist.

- [x] **Step 3: Implement framing controller**

Create `Assets/Scripts/Camera/PerspectiveCameraFramingController.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class PerspectiveCameraFramingController : MonoBehaviour
{
    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private Camera perspectiveCamera;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private GameObject gridObject;
    [SerializeField] private float defaultYaw = -35f;
    [SerializeField] private float defaultPitch = 45f;
    [SerializeField] private float distancePadding = 1.2f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 2500f;

    private readonly List<Room> cachedRooms = new List<Room>();
    private bool warnedNoBounds;

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    public bool FocusCurrentSelectionOrScene()
    {
        return TryGetSceneBounds(out Bounds bounds) && FrameBounds(bounds);
    }

    public bool FrameBounds(Bounds bounds)
    {
        if (perspectiveCamera == null || bounds.size == Vector3.zero)
        {
            return false;
        }

        Vector3 center = bounds.center;
        Quaternion rotation = Quaternion.Euler(defaultPitch, defaultYaw, 0f);
        float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
        float halfFov = Mathf.Max(1f, perspectiveCamera.fieldOfView) * 0.5f * Mathf.Deg2Rad;
        float distance = Mathf.Clamp(radius / Mathf.Sin(halfFov) * distancePadding, minDistance, maxDistance);
        perspectiveCamera.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);
        perspectiveCamera.transform.LookAt(center, Vector3.up);
        return true;
    }

    public bool TryGetSceneBounds(out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = default;
        EncapsulateHierarchyBounds(wallRoot, ref bounds, ref hasBounds);
        EncapsulateRooms(ref bounds, ref hasBounds);
        EncapsulateHierarchyBounds(furnitureRoot, ref bounds, ref hasBounds);
        EncapsulateObjectBounds(gridObject, ref bounds, ref hasBounds);
        return hasBounds;
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref viewModeManager);
        if (perspectiveCamera == null)
        {
            perspectiveCamera = Camera.main;
        }
        LayerUtility.ResolveTransformByName(ref wallRoot, LayerUtility.DefaultWallRootName, true);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveTransformByName(ref furnitureRoot, "FurnitureRoot", true);
        if (gridObject == null)
        {
            Transform grid = LayerUtility.FindTransformByName(LayerUtility.DefaultGridName, true);
            gridObject = grid != null ? grid.gameObject : null;
        }
    }

    private void BindEvents()
    {
        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
            viewModeManager.ViewModeChanged += HandleViewModeChanged;
        }
    }

    private void UnbindEvents()
    {
        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        }
    }

    private void HandleViewModeChanged(EditorViewMode mode)
    {
        if (mode != EditorViewMode.Perspective3D)
        {
            return;
        }

        if (!FocusCurrentSelectionOrScene() && !warnedNoBounds)
        {
            Debug.LogWarning($"{nameof(PerspectiveCameraFramingController)} could not find bounds to frame.", this);
            warnedNoBounds = true;
        }
    }

    private static void EncapsulateHierarchyBounds(Transform root, ref Bounds bounds, ref bool hasBounds)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Encapsulate(renderers[i].bounds, ref bounds, ref hasBounds);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Encapsulate(colliders[i].bounds, ref bounds, ref hasBounds);
        }
    }

    private void EncapsulateRooms(ref Bounds bounds, ref bool hasBounds)
    {
        if (roomManager == null)
        {
            return;
        }

        cachedRooms.Clear();
        cachedRooms.AddRange(roomManager.GetAllRooms());
        for (int i = 0; i < cachedRooms.Count; i++)
        {
            if (TryBuildRoomBounds(cachedRooms[i], out Bounds roomBounds))
            {
                Encapsulate(roomBounds, ref bounds, ref hasBounds);
            }
        }
    }

    private static bool TryBuildRoomBounds(Room room, out Bounds bounds)
    {
        bounds = default;
        if (room == null || room.BoundaryVertices == null || room.BoundaryVertices.Count == 0)
        {
            return false;
        }

        bounds = new Bounds(room.BoundaryVertices[0], Vector3.zero);
        for (int i = 1; i < room.BoundaryVertices.Count; i++)
        {
            bounds.Encapsulate(room.BoundaryVertices[i]);
        }
        bounds.Expand(new Vector3(0f, 3f, 0f));
        return true;
    }

    private static void EncapsulateObjectBounds(GameObject target, ref Bounds bounds, ref bool hasBounds)
    {
        if (target == null)
        {
            return;
        }

        if (target.TryGetComponent(out Renderer renderer))
        {
            Encapsulate(renderer.bounds, ref bounds, ref hasBounds);
        }
        if (target.TryGetComponent(out Collider collider))
        {
            Encapsulate(collider.bounds, ref bounds, ref hasBounds);
        }
    }

    private static void Encapsulate(Bounds next, ref Bounds bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = next;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(next);
    }
}
```

- [x] **Step 4: Run focused tests**

Run the focused Unity command and inspect XML or compiler log.

- [x] **Step 5: Commit Task 2**

```powershell
git add -- 'Assets/Scripts/Camera/PerspectiveCameraFramingController.cs' 'Assets/Tests/Editor/EditorViewModeManagerTests.cs'
git commit -m "Frame perspective camera on view entry"
```

---

### Task 3: Selection-Aware 3D Focus

**Files:**
- Modify: `Assets/Scripts/Camera/PerspectiveCameraFramingController.cs`
- Test: `Assets/Tests/Editor/EditorViewModeManagerTests.cs`

- [x] **Step 1: Add failing selection priority test**

Append:

```csharp
[Test]
public void PerspectiveFraming_UsesExplicitSelectionBoundsBeforeSceneBounds()
{
    GameObject cameraObject = new GameObject("PerspectiveCamera");
    Camera camera = cameraObject.AddComponent<Camera>();
    GameObject framingObject = new GameObject("PerspectiveCameraFramingController");
    Component framing = framingObject.AddComponent(GetAssemblyType("PerspectiveCameraFramingController"));
    SetPrivateField(framing, "perspectiveCamera", camera);
    Bounds selectionBounds = new Bounds(new Vector3(20f, 0f, 0f), new Vector3(2f, 2f, 2f));
    Bounds sceneBounds = new Bounds(Vector3.zero, new Vector3(30f, 2f, 30f));

    bool framed = (bool)InvokePublicWithResult(framing, "FrameSelectionOrSceneBoundsForTests", selectionBounds, true, sceneBounds, true);

    Assert.That(framed, Is.True);
    Assert.That(Vector3.Dot(camera.transform.forward, (selectionBounds.center - camera.transform.position).normalized), Is.GreaterThan(0.98f));

    DestroyObject(framingObject);
    DestroyObject(cameraObject);
}
```

- [x] **Step 2: Run tests and verify failure**

Expected: FAIL because `FrameSelectionOrSceneBoundsForTests` does not exist.

- [x] **Step 3: Implement selection-aware focus**

Modify `PerspectiveCameraFramingController`:

- Add serialized references:

```csharp
[SerializeField] private WallSelectionManager wallSelectionManager;
[SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
```

- Resolve them in `ResolveReferences()` with `LayerUtility.ResolveObject`.
- Change `FocusCurrentSelectionOrScene()` to:

```csharp
public bool FocusCurrentSelectionOrScene()
{
    if (TryGetSelectionBounds(out Bounds selectionBounds))
    {
        return FrameBounds(selectionBounds);
    }

    return TryGetSceneBounds(out Bounds sceneBounds) && FrameBounds(sceneBounds);
}
```

- Add:

```csharp
public bool TryGetSelectionBounds(out Bounds bounds)
{
    if (TryGetSelectedRoomBounds(out bounds))
    {
        return true;
    }

    if (TryGetSelectedWallBounds(out bounds))
    {
        return true;
    }

    return false;
}
```

- Implement room selection from `roomAuthoringPanelManager.SelectedRoom`.
- Implement wall selection from `wallSelectionManager.SelectedWall`, then `GetSelectedWalls(...)`.
- Use renderers/colliders for selected wall bounds first. If none exist, use `Wall.Data.startPoint`, `endPoint`, `height`, and `thickness`.
- Add public test seam:

```csharp
public bool FrameSelectionOrSceneBoundsForTests(Bounds selectionBounds, bool hasSelectionBounds, Bounds sceneBounds, bool hasSceneBounds)
{
    if (hasSelectionBounds)
    {
        return FrameBounds(selectionBounds);
    }

    return hasSceneBounds && FrameBounds(sceneBounds);
}
```

- [x] **Step 4: Run focused tests**

Run Unity command and inspect results/logs.

- [x] **Step 5: Commit Task 3**

```powershell
git add -- 'Assets/Scripts/Camera/PerspectiveCameraFramingController.cs' 'Assets/Tests/Editor/EditorViewModeManagerTests.cs'
git commit -m "Focus perspective camera on selection"
```

---

### Task 4: Read-Only 3D Selection Highlight

**Files:**
- Create: `Assets/Scripts/Camera/PerspectiveSelectionHighlightController.cs`
- Test: `Assets/Tests/Editor/EditorViewModeManagerTests.cs`

- [ ] **Step 1: Add failing highlight tests**

Append:

```csharp
[Test]
public void PerspectiveHighlight_CreatesAndClearsTransientHighlight()
{
    GameObject selectedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
    GameObject controllerObject = new GameObject("PerspectiveSelectionHighlightController");
    Component controller = controllerObject.AddComponent(GetAssemblyType("PerspectiveSelectionHighlightController"));

    bool created = (bool)InvokePublicWithResult(controller, "ShowHighlightForTarget", selectedObject);

    Assert.That(created, Is.True);
    Assert.That(selectedObject.transform.Find("PerspectiveSelectionHighlight"), Is.Not.Null);

    InvokePublic(controller, "ClearHighlight");

    Assert.That(selectedObject.transform.Find("PerspectiveSelectionHighlight"), Is.Null);

    DestroyObject(controllerObject);
    DestroyObject(selectedObject);
}
```

- [ ] **Step 2: Run tests and verify failure**

Expected: FAIL because `PerspectiveSelectionHighlightController` does not exist.

- [ ] **Step 3: Implement highlight controller**

Create `Assets/Scripts/Camera/PerspectiveSelectionHighlightController.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

public sealed class PerspectiveSelectionHighlightController : MonoBehaviour
{
    private const string HighlightObjectName = "PerspectiveSelectionHighlight";

    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private Color highlightColor = new Color(0.1f, 0.85f, 1f, 0.28f);
    [SerializeField] private float boundsPadding = 0.08f;

    private readonly List<GameObject> selectedWalls = new List<GameObject>();
    private readonly List<GameObject> highlightObjects = new List<GameObject>();
    private Material runtimeHighlightMaterial;

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        ClearHighlight();
        if (runtimeHighlightMaterial != null)
        {
            Destroy(runtimeHighlightMaterial);
        }
    }

    public void RefreshHighlight()
    {
        ClearHighlight();
        if (viewModeManager == null || viewModeManager.CurrentViewMode != EditorViewMode.Perspective3D)
        {
            return;
        }

        Room selectedRoom = roomAuthoringPanelManager != null ? roomAuthoringPanelManager.SelectedRoom : null;
        if (selectedRoom != null)
        {
            ShowHighlightForTarget(selectedRoom.gameObject);
            return;
        }

        if (wallSelectionManager == null)
        {
            return;
        }

        if (wallSelectionManager.SelectedWall != null)
        {
            ShowHighlightForTarget(wallSelectionManager.SelectedWall);
        }

        selectedWalls.Clear();
        wallSelectionManager.GetSelectedWalls(selectedWalls);
        for (int i = 0; i < selectedWalls.Count; i++)
        {
            if (selectedWalls[i] != wallSelectionManager.SelectedWall)
            {
                ShowHighlightForTarget(selectedWalls[i]);
            }
        }
    }

    public bool ShowHighlightForTarget(GameObject target)
    {
        if (target == null || !TryGetTargetBounds(target, out Bounds bounds))
        {
            return false;
        }

        GameObject highlight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        highlight.name = HighlightObjectName;
        highlight.transform.SetParent(target.transform, true);
        highlight.transform.position = bounds.center;
        highlight.transform.rotation = Quaternion.identity;
        highlight.transform.localScale = bounds.size + Vector3.one * boundsPadding;
        Collider collider = highlight.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = highlight.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetHighlightMaterial();
        }

        highlightObjects.Add(highlight);
        return true;
    }

    public void ClearHighlight()
    {
        for (int i = highlightObjects.Count - 1; i >= 0; i--)
        {
            if (highlightObjects[i] != null)
            {
                DestroyImmediate(highlightObjects[i]);
            }
        }

        highlightObjects.Clear();
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref viewModeManager);
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveObject(ref roomAuthoringPanelManager);
    }

    private void BindEvents()
    {
        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
            viewModeManager.ViewModeChanged += HandleViewModeChanged;
        }
        if (wallSelectionManager != null)
        {
            wallSelectionManager.SelectionChanged -= HandleWallSelectionChanged;
            wallSelectionManager.SelectionChanged += HandleWallSelectionChanged;
            wallSelectionManager.SelectionSetChanged -= HandleSelectionSetChanged;
            wallSelectionManager.SelectionSetChanged += HandleSelectionSetChanged;
        }
        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged -= HandleSelectedRoomChanged;
            roomAuthoringPanelManager.SelectedRoomChanged += HandleSelectedRoomChanged;
        }
    }

    private void UnbindEvents()
    {
        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        }
        if (wallSelectionManager != null)
        {
            wallSelectionManager.SelectionChanged -= HandleWallSelectionChanged;
            wallSelectionManager.SelectionSetChanged -= HandleSelectionSetChanged;
        }
        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged -= HandleSelectedRoomChanged;
        }
    }

    private void HandleViewModeChanged(EditorViewMode mode)
    {
        if (mode == EditorViewMode.Perspective3D)
        {
            RefreshHighlight();
        }
        else
        {
            ClearHighlight();
        }
    }

    private void HandleWallSelectionChanged(GameObject selectedWall)
    {
        RefreshHighlight();
    }

    private void HandleSelectionSetChanged()
    {
        RefreshHighlight();
    }

    private void HandleSelectedRoomChanged(Room room)
    {
        RefreshHighlight();
    }

    private Material GetHighlightMaterial()
    {
        if (highlightMaterial != null)
        {
            return highlightMaterial;
        }

        if (runtimeHighlightMaterial == null)
        {
            runtimeHighlightMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            runtimeHighlightMaterial.color = highlightColor;
        }

        return runtimeHighlightMaterial;
    }

    private static bool TryGetTargetBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name == HighlightObjectName)
            {
                continue;
            }
            Encapsulate(renderers[i].bounds, ref bounds, ref hasBounds);
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].gameObject.name == HighlightObjectName)
            {
                continue;
            }
            Encapsulate(colliders[i].bounds, ref bounds, ref hasBounds);
        }

        return hasBounds;
    }

    private static void Encapsulate(Bounds next, ref Bounds bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = next;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(next);
    }
}
```

- [ ] **Step 4: Run focused tests**

Run focused Unity command and inspect XML/logs.

- [ ] **Step 5: Commit Task 4**

```powershell
git add -- 'Assets/Scripts/Camera/PerspectiveSelectionHighlightController.cs' 'Assets/Tests/Editor/EditorViewModeManagerTests.cs'
git commit -m "Add read only perspective selection highlight"
```

---

### Task 5: Documentation And Manual QA Checklist

**Files:**
- Modify: `docs/operations.md`

- [ ] **Step 1: Update operations documentation**

Append under the existing `Top / 3D Perspective View Toggle` section:

```markdown

Additional 3D inspection wiring:

- Add `EditorViewModeToolbarPresenter` and assign the same Top / 3D buttons plus optional icon/background images.
- Add `PerspectiveCameraFramingController` and assign `Editor View Mode Manager`, `Perspective Camera`, `Walls`, `Room Manager`, `FurnitureRoot`, and optional `Grid`.
- Add `PerspectiveSelectionHighlightController` and assign `Editor View Mode Manager`, `Wall Selection Manager`, `Room Authoring Panel Manager`, and optional highlight material.

Manual QA:

- Top / 3D buttons visibly show the active view.
- Entering 3D with no selection frames the whole editable scene.
- Entering 3D with a selected room frames that room.
- Entering 3D with a selected wall frames that wall.
- 3D selection highlight appears only in Perspective view.
- Returning to Top clears transient 3D highlight and restores Top View overlays.
```

- [ ] **Step 2: Commit Task 5**

```powershell
git add -- 'docs/operations.md'
git commit -m "Document perspective view inspection wiring"
```

---

### Task 6: Final Verification

**Files:**
- Inspect all changed files

- [ ] **Step 1: Run all EditMode tests**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring\.worktrees\top-perspective-view-toggle' -runTests -testPlatform EditMode -testResults 'E:\Unity\LH Editor_Refactoring\.worktrees\top-perspective-view-toggle\Temp\perspective-ux-all-editmode-tests.xml' -quit
```

Expected: all EditMode tests pass. If Unity exits `0` but produces no XML, record that limitation and inspect `Editor.log` for compiler `ExitCode: 0`.

- [ ] **Step 2: Check git status**

Run:

```powershell
git status --short
```

Expected: only pre-existing unrelated scene changes remain. Do not stage `Assets/Scenes/SampleScene.unity` unless the user explicitly asks to include scene wiring.

- [ ] **Step 3: Manual Unity QA**

In Unity Editor, verify:

- Top / 3D button active state.
- Whole-scene camera fit.
- Selected room focus.
- Selected wall focus.
- 3D highlight visibility and cleanup.
- Top View editing still works after returning from 3D.

---

## Self-Review

- Spec coverage: Tasks cover toolbar state, whole-scene fit, selection-aware focus, read-only highlight, documentation, and verification.
- Scope check: Plan does not add split-screen preview, animated transitions, 3D editing, save/load schema changes, or a new selection model.
- Known environment limitation: Unity batch runs in this worktree have exited `0` but often do not create XML test result files. Treat compiler `ExitCode: 0` as compile evidence, not as proof that tests passed.
- Existing worktree note: `Assets/Scenes/SampleScene.unity` is already modified in the worktree. This plan intentionally avoids staging it unless the user explicitly wants scene wiring committed.
