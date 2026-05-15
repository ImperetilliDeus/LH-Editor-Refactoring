# Scene Hierarchy Tree View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a standalone UGUI hierarchy tree that displays Rooms with child Walls, root-level standalone Walls, and Wall click selection sync.

**Architecture:** Add a testable tree model builder under `Assets/Scripts/UI` and a small UGUI renderer component that consumes that model. The model owns Room/Wall grouping, display names, depth, and representative wall selection; the view owns `ScrollRect` content rendering, row creation, event binding, and subscriptions to `RoomManager.RoomsChanged` and `WallRegistry.RegistryChanged`.

**Tech Stack:** Unity C#, UGUI (`RectTransform`, `Button`, `Text`, `LayoutElement`), NUnit EditMode tests in `Assets/Tests/Editor`, existing `RoomManager`, `Room`, `Wall`, `WallHierarchyUtility`, and `WallSelectionManager`.

---

## File Structure

- Create `Assets/Scripts/UI/SceneHierarchyTreeModel.cs`
  - Contains `SceneHierarchyTreeRowKind`, `SceneHierarchyTreeRow`, and `SceneHierarchyTreeModel`.
  - Pure model builder with no Canvas dependencies.
  - Depends only on `Transform`, `Room`, `Wall`, and existing wall hierarchy utilities.
- Create `Assets/Scripts/UI/SceneHierarchyTreeView.cs`
  - MonoBehaviour that resolves references, subscribes to changes, calls the model builder, and renders UGUI rows.
  - Handles wall row click by calling `WallSelectionManager.SetSelectedWall`.
- Create `Assets/Tests/Editor/SceneHierarchyTreeModelTests.cs`
  - Tests standalone wall root display, room child display, assigned wall exclusion from root, and logical wall root grouping.
- Create `Assets/Tests/Editor/SceneHierarchyTreeViewTests.cs`
  - Tests fallback row rendering and wall row click selection.

---

### Task 1: Testable Scene Hierarchy Model

**Files:**
- Create: `Assets/Scripts/UI/SceneHierarchyTreeModel.cs`
- Test: `Assets/Tests/Editor/SceneHierarchyTreeModelTests.cs`

- [ ] **Step 1: Write failing model tests**

Create `Assets/Tests/Editor/SceneHierarchyTreeModelTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SceneHierarchyTreeModelTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void BuildRows_RendersStandaloneWallsAtRoot()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Wall first = CreateWall("Wall_001", "wall-a", wallRoot);
        Wall second = CreateWall("Wall_002", "wall-b", wallRoot);

        List<SceneHierarchyTreeRow> rows = SceneHierarchyTreeModel.BuildRows(wallRoot, new List<Room>());

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0].Kind, Is.EqualTo(SceneHierarchyTreeRowKind.Wall));
        Assert.That(rows[0].Depth, Is.EqualTo(0));
        Assert.That(rows[0].DisplayName, Is.EqualTo("Wall_001"));
        Assert.That(rows[0].RepresentativeWall, Is.EqualTo(first));
        Assert.That(rows[1].Kind, Is.EqualTo(SceneHierarchyTreeRowKind.Wall));
        Assert.That(rows[1].Depth, Is.EqualTo(0));
        Assert.That(rows[1].DisplayName, Is.EqualTo("Wall_002"));
        Assert.That(rows[1].RepresentativeWall, Is.EqualTo(second));
    }

    [Test]
    public void BuildRows_RendersAssignedWallsUnderRoomOnly()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Wall assigned = CreateWall("Wall_001", "wall-a", wallRoot);
        Wall standalone = CreateWall("Wall_002", "wall-b", wallRoot);
        Room room = CreateRoom("RoomObject", "Living Room", assigned);

        List<SceneHierarchyTreeRow> rows = SceneHierarchyTreeModel.BuildRows(wallRoot, new[] { room });

        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(rows[0].Kind, Is.EqualTo(SceneHierarchyTreeRowKind.Room));
        Assert.That(rows[0].Depth, Is.EqualTo(0));
        Assert.That(rows[0].DisplayName, Is.EqualTo("Room (Living Room)"));
        Assert.That(rows[1].Kind, Is.EqualTo(SceneHierarchyTreeRowKind.Wall));
        Assert.That(rows[1].Depth, Is.EqualTo(1));
        Assert.That(rows[1].DisplayName, Is.EqualTo("Wall_001"));
        Assert.That(rows[1].RepresentativeWall, Is.EqualTo(assigned));
        Assert.That(rows[2].Kind, Is.EqualTo(SceneHierarchyTreeRowKind.Wall));
        Assert.That(rows[2].Depth, Is.EqualTo(0));
        Assert.That(rows[2].DisplayName, Is.EqualTo("Wall_002"));
        Assert.That(rows[2].RepresentativeWall, Is.EqualTo(standalone));
    }

    [Test]
    public void BuildRows_UsesRoomObjectName_WhenRoomNameIsEmpty()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Wall assigned = CreateWall("Wall_001", "wall-a", wallRoot);
        Room room = CreateRoom("Room_A", string.Empty, assigned);

        List<SceneHierarchyTreeRow> rows = SceneHierarchyTreeModel.BuildRows(wallRoot, new[] { room });

        Assert.That(rows[0].DisplayName, Is.EqualTo("Room_A"));
    }

    [Test]
    public void BuildRows_CollapsesOpeningContainerSegmentsToOneLogicalWall()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        GameObject containerObject = CreateObject("Wall_With_Opening");
        containerObject.transform.SetParent(wallRoot, false);
        containerObject.AddComponent<WallOpeningContainer>();
        CreateWall("Segment_A", "segment-a", containerObject.transform);
        CreateWall("Segment_B", "segment-b", containerObject.transform);

        List<SceneHierarchyTreeRow> rows = SceneHierarchyTreeModel.BuildRows(wallRoot, new List<Room>());

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].DisplayName, Is.EqualTo("Wall_With_Opening"));
        Assert.That(rows[0].Kind, Is.EqualTo(SceneHierarchyTreeRowKind.Wall));
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private Wall CreateWall(string name, string id, Transform parent)
    {
        GameObject wallObject = CreateObject(name);
        wallObject.transform.SetParent(parent, false);
        Wall wall = wallObject.AddComponent<Wall>();
        wall.Initialize(new WallData(Vector3.zero, Vector3.forward, 0.2f, 3f, 1.5f));
        wall.Data.id = id;
        return wall;
    }

    private Room CreateRoom(string objectName, string roomName, params Wall[] walls)
    {
        GameObject roomObject = CreateObject(objectName);
        Room room = roomObject.AddComponent<Room>();
        room.Initialize(
            new HashSet<Wall>(walls),
            new RoomGeometry { Center = Vector3.zero, Area = 1f, WallCount = 4 },
            new List<Vector3>
            {
                Vector3.zero,
                Vector3.forward,
                Vector3.right + Vector3.forward,
                Vector3.right,
            },
            true);
        room.SetRoomName(roomName);
        return room;
    }
}
```

- [ ] **Step 2: Run model tests and verify RED**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\scene-hierarchy-model-red.xml' -quit
```

Expected: FAIL because `SceneHierarchyTreeModel`, `SceneHierarchyTreeRow`, and `SceneHierarchyTreeRowKind` do not exist.

- [ ] **Step 3: Add minimal model implementation**

Create `Assets/Scripts/UI/SceneHierarchyTreeModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public enum SceneHierarchyTreeRowKind
{
    Room,
    Wall,
}

public sealed class SceneHierarchyTreeRow
{
    public SceneHierarchyTreeRowKind Kind { get; }
    public string DisplayName { get; }
    public int Depth { get; }
    public Room Room { get; }
    public Wall RepresentativeWall { get; }

    public SceneHierarchyTreeRow(SceneHierarchyTreeRowKind kind, string displayName, int depth, Room room, Wall representativeWall)
    {
        Kind = kind;
        DisplayName = displayName ?? string.Empty;
        Depth = Mathf.Max(0, depth);
        Room = room;
        RepresentativeWall = representativeWall;
    }
}

public static class SceneHierarchyTreeModel
{
    private sealed class LogicalWallItem
    {
        public Transform root;
        public readonly List<Wall> walls = new List<Wall>();
        public readonly HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        public Wall representative;
        public string displayName;
    }

    private static readonly List<Wall> CachedWalls = new List<Wall>();

    public static List<SceneHierarchyTreeRow> BuildRows(Transform wallRoot, IEnumerable<Room> rooms)
    {
        List<SceneHierarchyTreeRow> rows = new List<SceneHierarchyTreeRow>();
        Dictionary<string, LogicalWallItem> itemsById = new Dictionary<string, LogicalWallItem>(StringComparer.Ordinal);
        List<LogicalWallItem> wallItems = BuildWallItems(wallRoot, itemsById);
        HashSet<LogicalWallItem> assignedItems = new HashSet<LogicalWallItem>();
        List<Room> sortedRooms = CreateSortedRooms(rooms);

        for (int i = 0; i < sortedRooms.Count; i++)
        {
            Room room = sortedRooms[i];
            if (room == null)
            {
                continue;
            }

            rows.Add(new SceneHierarchyTreeRow(SceneHierarchyTreeRowKind.Room, GetRoomDisplayName(room, i), 0, room, null));
            IReadOnlyList<string> wallIds = room.EffectiveWallIds;
            if (wallIds == null)
            {
                continue;
            }

            for (int j = 0; j < wallIds.Count; j++)
            {
                string wallId = wallIds[j];
                if (string.IsNullOrWhiteSpace(wallId) || !itemsById.TryGetValue(wallId, out LogicalWallItem item))
                {
                    continue;
                }

                assignedItems.Add(item);
                rows.Add(new SceneHierarchyTreeRow(SceneHierarchyTreeRowKind.Wall, item.displayName, 1, room, item.representative));
            }
        }

        wallItems.Sort((left, right) => string.CompareOrdinal(left.displayName, right.displayName));
        for (int i = 0; i < wallItems.Count; i++)
        {
            LogicalWallItem item = wallItems[i];
            if (item == null || assignedItems.Contains(item))
            {
                continue;
            }

            rows.Add(new SceneHierarchyTreeRow(SceneHierarchyTreeRowKind.Wall, item.displayName, 0, null, item.representative));
        }

        return rows;
    }

    private static List<LogicalWallItem> BuildWallItems(Transform wallRoot, Dictionary<string, LogicalWallItem> itemsById)
    {
        List<LogicalWallItem> wallItems = new List<LogicalWallItem>();
        if (wallRoot == null)
        {
            return wallItems;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, CachedWalls, true);
        Dictionary<Transform, LogicalWallItem> itemsByRoot = new Dictionary<Transform, LogicalWallItem>();
        for (int i = 0; i < CachedWalls.Count; i++)
        {
            Wall wall = CachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            Transform logicalRoot = GetLogicalWallRoot(wall.transform);
            if (logicalRoot == null)
            {
                continue;
            }

            if (!itemsByRoot.TryGetValue(logicalRoot, out LogicalWallItem item))
            {
                item = new LogicalWallItem { root = logicalRoot };
                itemsByRoot.Add(logicalRoot, item);
                wallItems.Add(item);
            }

            item.walls.Add(wall);
            string id = wall.Data != null ? wall.Data.id : null;
            if (!string.IsNullOrWhiteSpace(id))
            {
                item.ids.Add(id);
            }
        }

        for (int i = 0; i < wallItems.Count; i++)
        {
            LogicalWallItem item = wallItems[i];
            item.representative = ChooseRepresentativeWall(item.walls);
            item.displayName = GetWallDisplayName(item, i);
            foreach (string id in item.ids)
            {
                if (!itemsById.ContainsKey(id))
                {
                    itemsById.Add(id, item);
                }
            }
        }

        return wallItems;
    }

    private static Wall ChooseRepresentativeWall(List<Wall> walls)
    {
        Wall fallback = null;
        for (int i = 0; i < walls.Count; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            if (fallback == null)
            {
                fallback = wall;
            }

            if (wall.gameObject.activeInHierarchy && !WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                return wall;
            }
        }

        return fallback;
    }

    private static Transform GetLogicalWallRoot(Transform wallTransform)
    {
        if (wallTransform == null)
        {
            return null;
        }

        WallOpeningContainer container = wallTransform.GetComponentInParent<WallOpeningContainer>();
        return container != null ? container.transform : wallTransform;
    }

    private static List<Room> CreateSortedRooms(IEnumerable<Room> rooms)
    {
        List<Room> results = new List<Room>();
        if (rooms != null)
        {
            foreach (Room room in rooms)
            {
                if (room != null)
                {
                    results.Add(room);
                }
            }
        }

        results.Sort((left, right) => string.CompareOrdinal(GetRoomDisplayName(left, 0), GetRoomDisplayName(right, 0)));
        return results;
    }

    private static string GetRoomDisplayName(Room room, int index)
    {
        if (room == null)
        {
            return $"Room {index + 1}";
        }

        if (!string.IsNullOrWhiteSpace(room.RoomName))
        {
            return $"Room ({room.RoomName.Trim()})";
        }

        return !string.IsNullOrWhiteSpace(room.name) ? room.name : $"Room {index + 1}";
    }

    private static string GetWallDisplayName(LogicalWallItem item, int index)
    {
        if (item != null && item.root != null && !string.IsNullOrWhiteSpace(item.root.name))
        {
            return item.root.name;
        }

        if (item != null && item.representative != null && !string.IsNullOrWhiteSpace(item.representative.name))
        {
            return item.representative.name;
        }

        return $"Wall {index + 1}";
    }
}
```

- [ ] **Step 4: Run model tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\scene-hierarchy-model-green.xml' -quit
```

Expected: PASS for `SceneHierarchyTreeModelTests`.

- [ ] **Step 5: Commit model work**

Run:

```powershell
git add -- Assets/Scripts/UI/SceneHierarchyTreeModel.cs Assets/Tests/Editor/SceneHierarchyTreeModelTests.cs
git commit -m "Add scene hierarchy tree model"
```

Expected: a commit containing only the model and model tests.

---

### Task 2: UGUI Tree Rendering

**Files:**
- Create: `Assets/Scripts/UI/SceneHierarchyTreeView.cs`
- Test: `Assets/Tests/Editor/SceneHierarchyTreeViewTests.cs`

- [ ] **Step 1: Write failing rendering test**

Create `Assets/Tests/Editor/SceneHierarchyTreeViewTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void RebuildNow_CreatesFallbackRowsWithIndent()
    {
        Transform wallRoot = CreateObject("Walls").transform;
        Wall wall = CreateWall("Wall_001", "wall-a", wallRoot);
        Room room = CreateRoom("RoomObject", "Living Room", wall);
        RectTransform contentRoot = CreateRectObject("Content");
        SceneHierarchyTreeView treeView = CreateObject("TreeView").AddComponent<SceneHierarchyTreeView>();
        treeView.SetReferencesForTests(wallRoot, new[] { room }, contentRoot, null);

        treeView.RebuildNow();

        Assert.That(contentRoot.childCount, Is.EqualTo(2));
        Assert.That(contentRoot.GetChild(0).GetComponentInChildren<Text>().text, Is.EqualTo("Room (Living Room)"));
        Assert.That(contentRoot.GetChild(1).GetComponentInChildren<Text>().text, Is.EqualTo("Wall_001"));
        Assert.That(((RectTransform)contentRoot.GetChild(0)).anchoredPosition.x, Is.EqualTo(0f));
        Assert.That(((RectTransform)contentRoot.GetChild(1)).anchoredPosition.x, Is.GreaterThan(0f));
    }

    private GameObject CreateObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private RectTransform CreateRectObject(string name)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        createdObjects.Add(gameObject);
        return gameObject.GetComponent<RectTransform>();
    }

    private Wall CreateWall(string name, string id, Transform parent)
    {
        GameObject wallObject = CreateObject(name);
        wallObject.transform.SetParent(parent, false);
        Wall wall = wallObject.AddComponent<Wall>();
        wall.Initialize(new WallData(Vector3.zero, Vector3.forward, 0.2f, 3f, 1.5f));
        wall.Data.id = id;
        return wall;
    }

    private Room CreateRoom(string objectName, string roomName, params Wall[] walls)
    {
        GameObject roomObject = CreateObject(objectName);
        Room room = roomObject.AddComponent<Room>();
        room.Initialize(
            new HashSet<Wall>(walls),
            new RoomGeometry { Center = Vector3.zero, Area = 1f, WallCount = 4 },
            new List<Vector3>
            {
                Vector3.zero,
                Vector3.forward,
                Vector3.right + Vector3.forward,
                Vector3.right,
            },
            true);
        room.SetRoomName(roomName);
        return room;
    }
}
```

- [ ] **Step 2: Run view tests and verify RED**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\scene-hierarchy-view-red.xml' -quit
```

Expected: FAIL because `SceneHierarchyTreeView` does not exist.

- [ ] **Step 3: Add minimal UGUI renderer**

Create `Assets/Scripts/UI/SceneHierarchyTreeView.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SceneHierarchyTreeView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform rowTemplate;

    [Header("Layout")]
    [SerializeField] private float rowHeight = 28f;
    [SerializeField] private float childIndent = 18f;

    private readonly List<Room> cachedRooms = new List<Room>();
    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private IEnumerable<Room> testRooms;

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
        RebuildNow();
    }

    private void OnEnable()
    {
        BindEvents();
        RebuildNow();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    public void RebuildNow()
    {
        ResolveReferences();
        ClearRows();
        if (contentRoot == null)
        {
            return;
        }

        List<SceneHierarchyTreeRow> rows = SceneHierarchyTreeModel.BuildRows(wallRoot, GetRooms());
        for (int i = 0; i < rows.Count; i++)
        {
            CreateRow(rows[i]);
        }
    }

    public void SetReferencesForTests(Transform testWallRoot, IEnumerable<Room> rooms, RectTransform testContentRoot, WallSelectionManager testSelectionManager)
    {
        wallRoot = testWallRoot;
        testRooms = rooms;
        contentRoot = testContentRoot;
        wallSelectionManager = testSelectionManager;
    }

    private IEnumerable<Room> GetRooms()
    {
        if (testRooms != null)
        {
            return testRooms;
        }

        cachedRooms.Clear();
        if (roomManager != null)
        {
            roomManager.GetAllRooms(cachedRooms);
        }

        return cachedRooms;
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveTransformByName(ref wallRoot, LayerUtility.DefaultWallRootName, true);
    }

    private void BindEvents()
    {
        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleHierarchyChanged;
            roomManager.RoomsChanged += HandleHierarchyChanged;
        }

        WallRegistry.RegistryChanged -= HandleHierarchyChanged;
        WallRegistry.RegistryChanged += HandleHierarchyChanged;
    }

    private void UnbindEvents()
    {
        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleHierarchyChanged;
        }

        WallRegistry.RegistryChanged -= HandleHierarchyChanged;
    }

    private void HandleHierarchyChanged()
    {
        RebuildNow();
    }

    private void CreateRow(SceneHierarchyTreeRow row)
    {
        RectTransform rowTransform = rowTemplate != null
            ? Instantiate(rowTemplate, contentRoot)
            : CreateFallbackRow(contentRoot);

        rowTransform.gameObject.SetActive(true);
        rowTransform.name = $"{row.Kind}_{row.DisplayName}";
        rowTransform.SetParent(contentRoot, false);
        rowTransform.localScale = Vector3.one;
        rowTransform.anchoredPosition = new Vector2(row.Depth * childIndent, rowTransform.anchoredPosition.y);

        LayoutElement layoutElement = rowTransform.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = rowTransform.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredHeight = rowHeight;

        Text label = rowTransform.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = row.DisplayName;
        }

        Button button = rowTransform.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = row.Kind == SceneHierarchyTreeRowKind.Wall && row.RepresentativeWall != null;
            button.onClick.RemoveAllListeners();
        }

        spawnedRows.Add(rowTransform.gameObject);
    }

    private RectTransform CreateFallbackRow(RectTransform parent)
    {
        GameObject rowObject = new GameObject("HierarchyRow", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.SetParent(parent, false);
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        Image image = rowObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.04f);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rowRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        Text text = textObject.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleLeft;
        text.color = Color.black;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 13;

        return rowRect;
    }

    private void ClearRows()
    {
        for (int i = spawnedRows.Count - 1; i >= 0; i--)
        {
            if (spawnedRows[i] != null)
            {
                DestroyImmediate(spawnedRows[i]);
            }
        }

        spawnedRows.Clear();
    }
}
```

- [ ] **Step 4: Run view tests and verify GREEN**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\scene-hierarchy-view-green.xml' -quit
```

Expected: PASS for `SceneHierarchyTreeViewTests`.

- [ ] **Step 5: Commit view rendering work**

Run:

```powershell
git add -- Assets/Scripts/UI/SceneHierarchyTreeView.cs Assets/Tests/Editor/SceneHierarchyTreeViewTests.cs
git commit -m "Add scene hierarchy tree view"
```

Expected: a commit containing only the view and view tests.

---

### Task 3: Wall Click Selection Sync Test

**Files:**
- Modify: `Assets/Tests/Editor/SceneHierarchyTreeViewTests.cs`
- Modify: `Assets/Scripts/UI/SceneHierarchyTreeView.cs`

- [ ] **Step 1: Add failing click selection test**

Append this test to `SceneHierarchyTreeViewTests`:

```csharp
[Test]
public void WallButtonClick_SelectsRepresentativeWall()
{
    Transform wallRoot = CreateObject("Walls").transform;
    Wall wall = CreateWall("Wall_001", "wall-a", wallRoot);
    RectTransform contentRoot = CreateRectObject("Content");
    WallSelectionManager selectionManager = CreateObject("SelectionManager").AddComponent<WallSelectionManager>();
    SceneHierarchyTreeView treeView = CreateObject("TreeView").AddComponent<SceneHierarchyTreeView>();
    treeView.SetReferencesForTests(wallRoot, new List<Room>(), contentRoot, selectionManager);

    treeView.RebuildNow();
    Button button = contentRoot.GetChild(0).GetComponent<Button>();
    button.onClick.Invoke();

    Assert.That(selectionManager.SelectedWall, Is.EqualTo(wall.gameObject));
}
```

- [ ] **Step 2: Run click test and verify RED**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\scene-hierarchy-click-red.xml' -quit
```

Expected: FAIL because the wall button has no `onClick` listener yet, so `selectionManager.SelectedWall` remains null.

- [ ] **Step 3: Implement or adjust click binding**

Update `SceneHierarchyTreeView.CreateRow` so the button binding uses a captured local `Wall`:

```csharp
if (button.interactable)
{
    Wall wall = row.RepresentativeWall;
    button.onClick.AddListener(() => SelectWall(wall));
}
```

Keep `SelectWall` as:

```csharp
private void SelectWall(Wall wall)
{
    if (wallSelectionManager == null || wall == null)
    {
        return;
    }

    wallSelectionManager.SetSelectedWall(wall.gameObject);
}
```

- [ ] **Step 4: Run click test and verify GREEN**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\scene-hierarchy-click-green.xml' -quit
```

Expected: PASS for `WallButtonClick_SelectsRepresentativeWall`.

- [ ] **Step 5: Commit click sync work**

Run:

```powershell
git add -- Assets/Scripts/UI/SceneHierarchyTreeView.cs Assets/Tests/Editor/SceneHierarchyTreeViewTests.cs
git commit -m "Sync scene hierarchy wall clicks"
```

Expected: a commit containing the click sync test and any required click binding change.

---

### Task 4: Verification And Scene Integration Notes

**Files:**
- Modify: `docs/superpowers/plans/2026-05-15-scene-hierarchy-tree-view.md` only if execution notes need to be checked off.
- Manual scene wiring is not committed unless the user explicitly asks to update `Assets/Scenes/SampleScene.unity`, because the scene already has unrelated local modifications.

- [ ] **Step 1: Build runtime project**

Run:

```powershell
dotnet build Assembly-CSharp.csproj
```

Expected: build succeeds with no new compile errors from `SceneHierarchyTreeModel.cs` or `SceneHierarchyTreeView.cs`.

- [ ] **Step 2: Build editor tests project**

Run:

```powershell
dotnet build LH.Editor.Tests.csproj
```

Expected: build succeeds with no new compile errors from `SceneHierarchyTreeModelTests.cs` or `SceneHierarchyTreeViewTests.cs`.

- [ ] **Step 3: Run focused Unity EditMode tests**

Run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.0.66f2\Editor\Unity.exe' -batchmode -projectPath 'E:\Unity\LH Editor_Refactoring' -runTests -testPlatform EditMode -testResults 'Temp\scene-hierarchy-focused-tests.xml' -quit
```

Expected: `SceneHierarchyTreeModelTests` and `SceneHierarchyTreeViewTests` pass. Existing unrelated tests should not regress.

- [ ] **Step 4: Check git status**

Run:

```powershell
git status --short
```

Expected: new implementation files are committed. Pre-existing unrelated local changes remain untouched. `.superpowers/` remains untracked unless the user decides to keep or ignore browser mockup artifacts.

- [ ] **Step 5: Report scene wiring instructions**

In the final implementation response, tell the user:

```text
Add SceneHierarchyTreeView to a Canvas object, assign its Content RectTransform from a ScrollRect, and optionally assign a row template. If no template is assigned, it creates basic Button/Text rows at runtime.
```

Do not modify `Assets/Scenes/SampleScene.unity` unless the user confirms scene wiring should be committed.
