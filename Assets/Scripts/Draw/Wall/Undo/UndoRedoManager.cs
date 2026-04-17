using System.Collections.Generic;
using UnityEngine;

public partial class UndoRedoManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform wallRoot;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;

    [Header("History")]
    [SerializeField] private int maxUndoHistory = 50;
    [SerializeField] private int maxRedoHistory = 50;

    private readonly Stack<IUndoableAction> undoStack = new Stack<IUndoableAction>();
    private readonly Stack<IUndoableAction> redoStack = new Stack<IUndoableAction>();
    private Mesh cachedCubeMesh;

    private const float PositionEpsilonSqr = 0.000001f;
    private const float ScaleEpsilonSqr = 0.000001f;
    private const float RotationEpsilonDot = 0.999999f;

    private void OnValidate()
    {
        maxUndoHistory = Mathf.Max(1, maxUndoHistory);
        maxRedoHistory = Mathf.Max(1, maxRedoHistory);
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureWallRoot(false);
        EnsureCachedResources();
    }

    public void RecordWallCreated(GameObject wallObject)
    {
        if (wallObject == null)
        {
            return;
        }

        PushAction(new WallCreateAction(wallObject));
    }

    public void RecordMoveVertexGroup(int vertexId, List<WallStateChangeRecord> records)
    {
        if (records == null || records.Count == 0)
        {
            return;
        }

        List<WallStateChangeRecord> meaningful = FilterMeaningfulStateChanges(records);
        if (meaningful.Count == 0)
        {
            return;
        }

        PushAction(new MoveVertexGroupAction(vertexId, meaningful));
    }

    public void RecordMoveConnectedWalls(List<WallStateChangeRecord> records)
    {
        if (records == null || records.Count == 0)
        {
            return;
        }

        List<WallStateChangeRecord> meaningful = FilterMeaningfulStateChanges(records);
        if (meaningful.Count == 0)
        {
            return;
        }

        PushAction(new MoveConnectedWallsAction(meaningful));
    }

    public void RecordWallTransformChange(
        GameObject wallObject,
        Vector3 beforePosition,
        Quaternion beforeRotation,
        Vector3 beforeScale,
        Vector3 afterPosition,
        Quaternion afterRotation,
        Vector3 afterScale)
    {
        if (wallObject == null)
        {
            return;
        }

        RecordMoveConnectedWalls(new List<WallStateChangeRecord>
        {
            new WallStateChangeRecord
            {
                before = WallStateSnapshot.Capture(wallObject, beforePosition, beforeRotation, beforeScale),
                after = WallStateSnapshot.Capture(wallObject, afterPosition, afterRotation, afterScale),
            }
        });
    }

    public void RecordWallTransformChanges(List<WallTransformRecord> records)
    {
        if (records == null || records.Count == 0)
        {
            return;
        }

        List<WallStateChangeRecord> converted = new List<WallStateChangeRecord>(records.Count);
        for (int i = 0; i < records.Count; i++)
        {
            WallTransformRecord record = records[i];
            if (record.wallObject == null)
            {
                continue;
            }

            converted.Add(new WallStateChangeRecord
            {
                before = WallStateSnapshot.Capture(record.wallObject, record.beforePosition, record.beforeRotation, record.beforeScale),
                after = WallStateSnapshot.Capture(record.wallObject, record.afterPosition, record.afterRotation, record.afterScale),
            });
        }

        RecordMoveConnectedWalls(converted);
    }

    public void RecordRoomCreated(Room room)
    {
        if (room == null)
        {
            return;
        }

        PushAction(new RoomCreateAction(room));
    }

    public void RecordWallSplit(GameObject originalWall, GameObject firstSplitWall, GameObject secondSplitWall)
    {
        if (originalWall == null || firstSplitWall == null || secondSplitWall == null)
        {
            return;
        }

        PushAction(new WallSplitAction(originalWall, firstSplitWall, secondSplitWall));
    }

    public void RecordRoomsReplaced(List<Room> deletedRooms, List<Room> createdRooms)
    {
        bool hasDeleted = deletedRooms != null && deletedRooms.Count > 0;
        bool hasCreated = createdRooms != null && createdRooms.Count > 0;
        if (!hasDeleted && !hasCreated)
        {
            return;
        }

        PushAction(new RoomReplaceAction(deletedRooms, createdRooms));
    }

    public void RecordOpeningLayoutChange(OpeningLayoutSnapshot before, OpeningLayoutSnapshot after)
    {
        if (!OpeningLayoutSnapshot.HasMeaningfulDelta(before, after))
        {
            return;
        }

        PushAction(new OpeningLayoutChangeAction(before, after));
    }

    public void RecordDeletedLayouts(List<OpeningLayoutSnapshot> layouts, List<Room> affectedRooms)
    {
        if ((layouts == null || layouts.Count == 0) && (affectedRooms == null || affectedRooms.Count == 0))
        {
            return;
        }

        PushAction(new DeleteLayoutsAction(layouts, affectedRooms));
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        IUndoableAction action = undoStack.Pop();
        action.Undo(this);
        redoStack.Push(action);
        TrimStackToNewest(redoStack, maxRedoHistory);
        RefreshPostUndoRedoVisuals();
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        IUndoableAction action = redoStack.Pop();
        action.Redo(this);
        undoStack.Push(action);
        TrimStackToNewest(undoStack, maxUndoHistory);
        RefreshPostUndoRedoVisuals();
    }

    // Inspector button용 별칭 메서드
    public void UndoFromUI()
    {
        Undo();
    }

    // Inspector button용 별칭 메서드
    public void RedoFromUI()
    {
        Redo();
    }

    private void PushAction(IUndoableAction action)
    {
        undoStack.Push(action);
        TrimStackToNewest(undoStack, maxUndoHistory);
        redoStack.Clear();
    }

    private void TrimStackToNewest(Stack<IUndoableAction> stack, int maxCount)
    {
        if (stack == null || stack.Count <= maxCount)
        {
            return;
        }

        IUndoableAction[] current = stack.ToArray();
        stack.Clear();

        int keepCount = Mathf.Min(maxCount, current.Length);
        for (int i = keepCount - 1; i >= 0; i--)
        {
            stack.Push(current[i]);
        }
    }

    private List<WallStateChangeRecord> FilterMeaningfulStateChanges(List<WallStateChangeRecord> records)
    {
        List<WallStateChangeRecord> meaningful = new List<WallStateChangeRecord>(records.Count);
        for (int i = 0; i < records.Count; i++)
        {
            WallStateChangeRecord record = records[i];
            if (record.after.wallObject == null && record.before.wallObject == null)
            {
                continue;
            }

            if (!WallStateSnapshot.HasMeaningfulDelta(record.before, record.after))
            {
                continue;
            }

            meaningful.Add(record);
        }

        return meaningful;
    }

    private GameObject CreateWallFromSnapshot(WallStateSnapshot snapshot)
    {
        EnsureWallRoot(true);

        EnsureCachedResources();
        GameObject wallObject = new GameObject(snapshot.name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        wallObject.transform.SetParent(wallRoot, true);
        wallObject.transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
        wallObject.transform.localScale = snapshot.scale;

        MeshFilter filter = wallObject.GetComponent<MeshFilter>();
        if (filter != null)
        {
            filter.sharedMesh = cachedCubeMesh;
        }

        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null && snapshot.sharedMaterial != null)
        {
            renderer.sharedMaterial = snapshot.sharedMaterial;
        }

        Wall wall = wallObject.AddComponent<Wall>();
        wall.Initialize(snapshot.wallData != null ? snapshot.wallData.Clone() : new WallData());
        wall.SetVertexIds(snapshot.startVertexId, snapshot.endVertexId);
        wall.SetHandleSuppressed(snapshot.suppressStartHandle, snapshot.suppressEndHandle);
        wall.SetSplitPointFlags(snapshot.startSplitPoint, snapshot.endSplitPoint);
        wall.SetTopMaterial(snapshot.topMaterial);
        wall.SetTopFaceOffset(0.01f);
        wall.UpdateView(0.01f);

        return wallObject;
    }

    private void EnsureCachedResources()
    {
        if (cachedCubeMesh != null)
        {
            return;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter filter = cube.GetComponent<MeshFilter>();
        if (filter != null)
        {
            cachedCubeMesh = filter.sharedMesh;
        }

        Destroy(cube);
    }

    private void ApplyWallStateChanges(List<WallStateChangeRecord> records, bool useAfterState)
    {
        if (records == null)
        {
            return;
        }

        for (int i = 0; i < records.Count; i++)
        {
            WallStateSnapshot snapshot = useAfterState ? records[i].after : records[i].before;
            ApplyWallState(snapshot);
        }

        if (GetHandleManager() != null)
        {
            handleManager.RefreshRegisteredWalls();
        }
    }

    private void ApplyWallState(WallStateSnapshot snapshot)
    {
        GameObject wallObject = snapshot.wallObject;
        if (wallObject == null)
        {
            return;
        }

        wallObject.transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);
        wallObject.transform.localScale = snapshot.scale;

        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = snapshot.sharedMaterial;
        }

        Wall wallComponent = wallObject.GetComponent<Wall>();
        if (wallComponent == null)
        {
            wallComponent = wallObject.AddComponent<Wall>();
        }

        wallComponent.Initialize(snapshot.wallData != null ? snapshot.wallData.Clone() : new WallData());
        wallComponent.SetVertexIds(snapshot.startVertexId, snapshot.endVertexId);
        wallComponent.SetHandleSuppressed(snapshot.suppressStartHandle, snapshot.suppressEndHandle);
        wallComponent.SetSplitPointFlags(snapshot.startSplitPoint, snapshot.endSplitPoint);
        wallComponent.SetTopMaterial(snapshot.topMaterial);
        wallComponent.SetTopFaceOffset(0.01f);
        wallObject.name = snapshot.name;
        wallComponent.UpdateView(0.01f);
        wallComponent.RefreshLengthDisplay(wallLengthDisplay, false);
    }

    private void RegisterWallVisuals(GameObject wallObject)
    {
        if (wallObject == null)
        {
            return;
        }

        if (GetHandleManager() != null)
        {
            handleManager.RegisterWall(wallObject);
        }

        Wall wallComponent = wallObject.GetComponent<Wall>();
        if (wallComponent != null)
        {
            wallComponent.RefreshLengthDisplay(GetWallLengthDisplay(), false);
        }
    }

    private void UnregisterWallVisuals(GameObject wallObject)
    {
        if (wallObject == null)
        {
            return;
        }

        if (GetHandleManager() != null)
        {
            handleManager.UnregisterWall(wallObject);
        }

        Wall wallComponent = wallObject.GetComponent<Wall>();
        if (wallComponent != null)
        {
            wallComponent.ClearLengthDisplay(GetWallLengthDisplay());
        }
    }

    private RoomManager GetRoomManager()
    {
        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }

        return roomManager;
    }

    private WallOpeningPlacementManager GetWallOpeningPlacementManager()
    {
        if (wallOpeningPlacementManager == null)
        {
            wallOpeningPlacementManager = FindFirstObjectByType<WallOpeningPlacementManager>();
        }

        return wallOpeningPlacementManager;
    }

    private void ApplyOpeningLayoutSnapshot(OpeningLayoutSnapshot target, OpeningLayoutSnapshot current)
    {
        WallOpeningPlacementManager manager = GetWallOpeningPlacementManager();
        if (manager == null)
        {
            return;
        }

        manager.ApplyLayoutSnapshot(target, current);

        if (GetHandleManager() != null)
        {
            handleManager.RefreshRegisteredWalls();
        }
    }

    private Wall FindWall(WallReference wallReference)
    {
        EnsureWallRoot(false);
        if (wallRoot == null)
        {
            return null;
        }

        Wall fallbackMatch = null;
        Wall[] walls = wallRoot.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (wall.StartVertexId == wallReference.startVertexId &&
                wall.EndVertexId == wallReference.endVertexId)
            {
                return wall;
            }

            if (wall.name == wallReference.name &&
                ArePointsClose(wall.Data.startPoint, wallReference.startPoint) &&
                ArePointsClose(wall.Data.endPoint, wallReference.endPoint))
            {
                fallbackMatch = wall;
            }
        }

        return fallbackMatch;
    }

    private bool ArePointsClose(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.0001f;
    }

    private void ResolveReferences()
    {
        if (handleManager == null)
        {
            handleManager = FindFirstObjectByType<HandleManager>();
        }

        if (wallLengthDisplay == null)
        {
            wallLengthDisplay = FindFirstObjectByType<WallLengthDisplay>();
        }

        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }

        if (wallOpeningPlacementManager == null)
        {
            wallOpeningPlacementManager = FindFirstObjectByType<WallOpeningPlacementManager>();
        }
    }

    private HandleManager GetHandleManager()
    {
        if (handleManager == null)
        {
            handleManager = FindFirstObjectByType<HandleManager>();
        }

        return handleManager;
    }

    private WallLengthDisplay GetWallLengthDisplay()
    {
        if (wallLengthDisplay == null)
        {
            wallLengthDisplay = FindFirstObjectByType<WallLengthDisplay>();
        }

        return wallLengthDisplay;
    }

    private void EnsureWallRoot(bool createIfMissing)
    {
        if (wallRoot != null)
        {
            return;
        }

        Transform wallRootTransform = LayerUtility.FindTransformByName("Walls", true);
        if (wallRootTransform == null && createIfMissing)
        {
            wallRootTransform = new GameObject("Walls").transform;
        }

        wallRoot = wallRootTransform;
    }

    private void RefreshPostUndoRedoVisuals()
    {
        if (GetHandleManager() != null)
        {
            handleManager.RefreshRegisteredWalls();
        }

        RoomTopologyEvents.RequestRefreshAll();

        WallOpeningPlacementManager openingManager = GetWallOpeningPlacementManager();
        if (openingManager != null)
        {
            openingManager.MarkMarkerVisualsDirty();
        }

        TopViewRenderManager[] topViewManagers = FindObjectsByType<TopViewRenderManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < topViewManagers.Length; i++)
        {
            if (topViewManagers[i] != null)
            {
                topViewManagers[i].MarkDirty();
            }
        }
    }
}
