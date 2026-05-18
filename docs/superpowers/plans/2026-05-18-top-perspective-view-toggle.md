# Top / 3D Perspective View Toggle Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a top-toolbar view toggle that switches the current scene between 2D Top View editing and 3D Perspective inspection.

**Architecture:** Add a new view-state manager instead of extending `ModeManager`, because edit mode and view mode are separate axes. The manager toggles cameras, camera input components, top-view overlay roots, and optional toolbar buttons while leaving wall, room, opening, and furniture scene data untouched.

**Tech Stack:** Unity 6000.0.66f2, C#, `UnityEngine.UI.Button`, NUnit EditMode tests in `Assets/Tests/Editor`.

---

## File Structure

- Create `Assets/Scripts/Camera/EditorViewModeManager.cs`
  - Defines `EditorViewMode`.
  - Owns current view state.
  - Toggles Top/Perspective cameras and camera manager components.
  - Toggles Top View-only UI roots.
  - Binds optional toolbar buttons.
  - Provides test-only reference injection.
- Create `Assets/Scripts/Camera/EditorViewModeManager.cs.meta`
  - Unity meta file for the new script.
  - Use Unity-generated GUID if created by Unity, or create a normal MonoImporter meta if working outside Unity.
- Create `Assets/Tests/Editor/EditorViewModeManagerTests.cs`
  - Verifies camera, component, UI root, button, and event behavior.
- Create `Assets/Tests/Editor/EditorViewModeManagerTests.cs.meta`
  - Unity meta file for the new test script.
- Modify `docs/operations.md`
  - Add concise manual scene wiring notes for the view toggle.

---

### Task 1: View Mode Manager Core

**Files:**
- Create: `Assets/Scripts/Camera/EditorViewModeManager.cs`
- Test: `Assets/Tests/Editor/EditorViewModeManagerTests.cs`

- [ ] **Step 1: Write the failing core toggle tests**

Create `Assets/Tests/Editor/EditorViewModeManagerTests.cs`:

```csharp
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

    [TearDown]
    public void TearDown()
    {
        DestroyObject(perspectiveButtonObject);
        DestroyObject(topButtonObject);
        DestroyObject(topUiRoot);
        DestroyObject(perspectiveCameraObject);
        DestroyObject(topCameraObject);
        DestroyObject(managerObject);
    }

    [Test]
    public void SetPerspectiveView_EnablesPerspectiveCameraAndHidesTopViewRoots()
    {
        EditorViewModeManager manager = CreateManager(out Camera topCamera, out Camera perspectiveCamera, out Behaviour topManager, out Behaviour perspectiveManager, out Button topButton, out Button perspectiveButton);

        manager.SetPerspectiveView();

        Assert.That(manager.CurrentViewMode, Is.EqualTo(EditorViewMode.Perspective3D));
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
        EditorViewModeManager manager = CreateManager(out Camera topCamera, out Camera perspectiveCamera, out Behaviour topManager, out Behaviour perspectiveManager, out Button topButton, out Button perspectiveButton);

        manager.SetPerspectiveView();
        manager.SetTopView();

        Assert.That(manager.CurrentViewMode, Is.EqualTo(EditorViewMode.Top));
        Assert.That(topCamera.enabled, Is.True);
        Assert.That(perspectiveCamera.enabled, Is.False);
        Assert.That(topManager.enabled, Is.True);
        Assert.That(perspectiveManager.enabled, Is.False);
        Assert.That(topUiRoot.activeSelf, Is.True);
        Assert.That(topButton.interactable, Is.False);
        Assert.That(perspectiveButton.interactable, Is.True);
    }

    [Test]
    public void SetViewMode_DoesNotRaiseDuplicateEventForSameMode()
    {
        EditorViewModeManager manager = CreateManager(out _, out _, out _, out _, out _, out _);
        int eventCount = 0;
        manager.ViewModeChanged += _ => eventCount++;

        manager.SetViewMode(EditorViewMode.Top);
        manager.SetViewMode(EditorViewMode.Perspective3D);
        manager.SetViewMode(EditorViewMode.Perspective3D);

        Assert.That(eventCount, Is.EqualTo(1));
    }

    private EditorViewModeManager CreateManager(
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
        EditorViewModeManager manager = managerObject.AddComponent<EditorViewModeManager>();
        manager.SetReferencesForTests(
            topCamera,
            perspectiveCamera,
            topManager,
            perspectiveManager,
            new[] { topUiRoot },
            topButton,
            perspectiveButton);
        manager.SetViewMode(EditorViewMode.Top);
        return manager;
    }

    private static void DestroyObject(Object target)
    {
        if (target != null)
        {
            Object.DestroyImmediate(target);
        }
    }

    private sealed class TestViewInputComponent : MonoBehaviour
    {
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\editor-view-mode-manager-tests.xml' -quit
```

Expected: FAIL because `EditorViewModeManager` and `EditorViewMode` do not exist.

- [ ] **Step 3: Implement the minimal manager**

Create `Assets/Scripts/Camera/EditorViewModeManager.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum EditorViewMode
{
    Top = 0,
    Perspective3D = 1,
}

public sealed class EditorViewModeManager : MonoBehaviour
{
    [Header("State")]
    [SerializeField] private EditorViewMode initialViewMode = EditorViewMode.Top;

    [Header("Cameras")]
    [SerializeField] private Camera topCamera;
    [SerializeField] private Camera perspectiveCamera;

    [Header("Camera Input")]
    [SerializeField] private Behaviour topCameraManager;
    [SerializeField] private Behaviour perspectiveCameraManager;

    [Header("Top View UI")]
    [SerializeField] private GameObject[] topViewOnlyRoots = Array.Empty<GameObject>();

    [Header("Toolbar")]
    [SerializeField] private Button topButton;
    [SerializeField] private Button perspectiveButton;

    private UnityAction topButtonAction;
    private UnityAction perspectiveButtonAction;

    public EditorViewMode CurrentViewMode { get; private set; }

    public event Action<EditorViewMode> ViewModeChanged;

    private void Awake()
    {
        BindButtons();
        ApplyViewMode(initialViewMode, true);
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void SetTopView()
    {
        SetViewMode(EditorViewMode.Top);
    }

    public void SetPerspectiveView()
    {
        SetViewMode(EditorViewMode.Perspective3D);
    }

    public void SetViewMode(EditorViewMode mode)
    {
        ApplyViewMode(mode, false);
    }

    public void SetReferencesForTests(
        Camera testTopCamera,
        Camera testPerspectiveCamera,
        Behaviour testTopCameraManager,
        Behaviour testPerspectiveCameraManager,
        GameObject[] testTopViewOnlyRoots,
        Button testTopButton,
        Button testPerspectiveButton)
    {
        UnbindButtons();
        topCamera = testTopCamera;
        perspectiveCamera = testPerspectiveCamera;
        topCameraManager = testTopCameraManager;
        perspectiveCameraManager = testPerspectiveCameraManager;
        topViewOnlyRoots = testTopViewOnlyRoots ?? Array.Empty<GameObject>();
        topButton = testTopButton;
        perspectiveButton = testPerspectiveButton;
        BindButtons();
    }

    private void ApplyViewMode(EditorViewMode mode, bool force)
    {
        if (!force && CurrentViewMode == mode)
        {
            RefreshButtonState();
            return;
        }

        CurrentViewMode = mode;
        bool topActive = mode == EditorViewMode.Top;

        SetCameraEnabled(topCamera, topActive, nameof(topCamera));
        SetCameraEnabled(perspectiveCamera, !topActive, nameof(perspectiveCamera));
        SetBehaviourEnabled(topCameraManager, topActive, nameof(topCameraManager));
        SetBehaviourEnabled(perspectiveCameraManager, !topActive, nameof(perspectiveCameraManager));
        SetTopViewRootsActive(topActive);
        RefreshButtonState();

        if (!force)
        {
            ViewModeChanged?.Invoke(CurrentViewMode);
        }
    }

    private void SetTopViewRootsActive(bool active)
    {
        if (topViewOnlyRoots == null)
        {
            return;
        }

        for (int i = 0; i < topViewOnlyRoots.Length; i++)
        {
            GameObject root = topViewOnlyRoots[i];
            if (root != null)
            {
                root.SetActive(active);
            }
        }
    }

    private void RefreshButtonState()
    {
        if (topButton != null)
        {
            topButton.interactable = CurrentViewMode != EditorViewMode.Top;
        }

        if (perspectiveButton != null)
        {
            perspectiveButton.interactable = CurrentViewMode != EditorViewMode.Perspective3D;
        }
    }

    private void BindButtons()
    {
        if (topButtonAction == null)
        {
            topButtonAction = SetTopView;
        }

        if (perspectiveButtonAction == null)
        {
            perspectiveButtonAction = SetPerspectiveView;
        }

        if (topButton != null)
        {
            topButton.onClick.RemoveListener(topButtonAction);
            topButton.onClick.AddListener(topButtonAction);
        }

        if (perspectiveButton != null)
        {
            perspectiveButton.onClick.RemoveListener(perspectiveButtonAction);
            perspectiveButton.onClick.AddListener(perspectiveButtonAction);
        }
    }

    private void UnbindButtons()
    {
        if (topButton != null && topButtonAction != null)
        {
            topButton.onClick.RemoveListener(topButtonAction);
        }

        if (perspectiveButton != null && perspectiveButtonAction != null)
        {
            perspectiveButton.onClick.RemoveListener(perspectiveButtonAction);
        }
    }

    private void SetCameraEnabled(Camera target, bool enabled, string referenceName)
    {
        if (target == null)
        {
            Debug.LogWarning($"{nameof(EditorViewModeManager)} missing {referenceName}.", this);
            return;
        }

        target.enabled = enabled;
    }

    private void SetBehaviourEnabled(Behaviour target, bool enabled, string referenceName)
    {
        if (target == null)
        {
            Debug.LogWarning($"{nameof(EditorViewModeManager)} missing {referenceName}.", this);
            return;
        }

        target.enabled = enabled;
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\editor-view-mode-manager-tests.xml' -quit
```

Expected: PASS for `EditorViewModeManagerTests`.

- [ ] **Step 5: Commit Task 1**

```powershell
git add -- 'Assets/Scripts/Camera/EditorViewModeManager.cs' 'Assets/Tests/Editor/EditorViewModeManagerTests.cs'
git commit -m "Add editor view mode manager"
```

---

### Task 2: Button Click Behavior And Reference Tolerance

**Files:**
- Modify: `Assets/Tests/Editor/EditorViewModeManagerTests.cs`
- Modify: `Assets/Scripts/Camera/EditorViewModeManager.cs`

- [ ] **Step 1: Add failing button and null-reference tests**

Append these tests inside `EditorViewModeManagerTests`:

```csharp
[Test]
public void ToolbarButtons_SwitchViewModes()
{
    EditorViewModeManager manager = CreateManager(out Camera topCamera, out Camera perspectiveCamera, out _, out _, out Button topButton, out Button perspectiveButton);

    perspectiveButton.onClick.Invoke();

    Assert.That(manager.CurrentViewMode, Is.EqualTo(EditorViewMode.Perspective3D));
    Assert.That(topCamera.enabled, Is.False);
    Assert.That(perspectiveCamera.enabled, Is.True);

    topButton.onClick.Invoke();

    Assert.That(manager.CurrentViewMode, Is.EqualTo(EditorViewMode.Top));
    Assert.That(topCamera.enabled, Is.True);
    Assert.That(perspectiveCamera.enabled, Is.False);
}

[Test]
public void SetPerspectiveView_ToleratesMissingReferences()
{
    managerObject = new GameObject("EditorViewModeManager");
    EditorViewModeManager manager = managerObject.AddComponent<EditorViewModeManager>();

    Assert.DoesNotThrow(() => manager.SetPerspectiveView());
    Assert.That(manager.CurrentViewMode, Is.EqualTo(EditorViewMode.Perspective3D));
}
```

- [ ] **Step 2: Run the tests and verify behavior**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\editor-view-mode-manager-tests.xml' -quit
```

Expected: PASS. If button tests fail because `Awake` bound before test references were injected, keep `SetReferencesForTests(...)` unbinding and rebinding exactly as defined in Task 1.

- [ ] **Step 3: Commit Task 2**

```powershell
git add -- 'Assets/Scripts/Camera/EditorViewModeManager.cs' 'Assets/Tests/Editor/EditorViewModeManagerTests.cs'
git commit -m "Test editor view toolbar switching"
```

---

### Task 3: Manual Wiring Documentation

**Files:**
- Modify: `docs/operations.md`

- [ ] **Step 1: Add operation notes**

Append this section to `docs/operations.md`:

```markdown

## Top / 3D Perspective View Toggle

`EditorViewModeManager` switches the same editable scene between Top View and 3D Perspective inspection.

Scene wiring:

- Add `EditorViewModeManager` to a scene object.
- Assign the 2D orthographic Top camera to `Top Camera`.
- Assign the Perspective camera to `Perspective Camera`.
- Assign the existing `CameraManager` to `Top Camera Manager`.
- Assign the existing `CameraManager_3D` to `Perspective Camera Manager`.
- Add Top View-only roots such as `TopPlanContent`, `_Handle`, and other edit overlay roots to `Top View Only Roots`.
- Assign toolbar buttons to `Top Button` and `Perspective Button`.

The 3D Perspective view is inspection-only. It should not duplicate or rebuild walls, rooms, openings, or furniture.
```

- [ ] **Step 2: Run the focused tests**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\editor-view-mode-manager-tests.xml' -quit
```

Expected: PASS for `EditorViewModeManagerTests`.

- [ ] **Step 3: Commit Task 3**

```powershell
git add -- 'docs/operations.md'
git commit -m "Document top perspective view toggle wiring"
```

---

### Task 4: Final Verification

**Files:**
- Inspect: all changed files

- [ ] **Step 1: Run all EditMode tests**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\editor-view-mode-all-editmode-tests.xml' -quit
```

Expected: all EditMode tests pass.

- [ ] **Step 2: Inspect the result XML**

Run:

```powershell
Select-String -Path 'Temp\editor-view-mode-all-editmode-tests.xml' -Pattern 'result="Failed"|result="Passed"'
```

Expected: the top-level test suite reports `result="Passed"`.

- [ ] **Step 3: Check git status**

Run:

```powershell
git status --short
```

Expected: only unrelated pre-existing files or ignored Visual Companion files remain outside the committed implementation.

---

## Self-Review

- Spec coverage: Task 1 implements the separate view-mode manager, camera toggling, input component toggling, top-view root visibility, active button state, and duplicate event guard. Task 2 covers actual toolbar clicks and incomplete scene wiring tolerance. Task 3 covers manual scene wiring. Task 4 covers final verification.
- Scope check: The plan does not add split-screen preview, preview-scene duplication, 3D editing, save/load schema changes, or geometry rebuild behavior.
- Type consistency: `EditorViewMode`, `EditorViewModeManager`, `SetViewMode`, `SetTopView`, `SetPerspectiveView`, `CurrentViewMode`, `ViewModeChanged`, and `SetReferencesForTests` use the same names across tests and implementation.
