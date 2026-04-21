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

    private readonly Stack<IEditorCommand> undoStack = new Stack<IEditorCommand>();
    private readonly Stack<IEditorCommand> redoStack = new Stack<IEditorCommand>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private Mesh cachedCubeMesh;

    private const float PositionEpsilonSqr = 0.000001f;

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

    public void RecordRoomPolygonChanged(Room room, IReadOnlyList<Vector3> beforeVertices, IReadOnlyList<Vector3> afterVertices)
    {
        if (room == null)
        {
            return;
        }

        RoomPolygonSnapshot before = RoomPolygonSnapshot.Capture(room, beforeVertices);
        RoomPolygonSnapshot after = RoomPolygonSnapshot.Capture(room, afterVertices);
        if (!RoomPolygonSnapshot.HasMeaningfulDelta(before, after))
        {
            return;
        }

        PushAction(new RoomPolygonChangeAction(before, after));
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

        IEditorCommand action = undoStack.Pop();
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

        IEditorCommand action = redoStack.Pop();
        action.Redo(this);
        undoStack.Push(action);
        TrimStackToNewest(undoStack, maxUndoHistory);
        RefreshPostUndoRedoVisuals();
    }

    // Inspector button alias
    public void UndoFromUI()
    {
        Undo();
    }

    // Inspector button alias
    public void RedoFromUI()
    {
        Redo();
    }

    public void ExecuteCommand(IEditorCommand command, bool alreadyExecuted = false)
    {
        if (command == null)
        {
            return;
        }

        if (!alreadyExecuted)
        {
            command.Execute(this);
        }

        PushAction(command);
        RefreshPostUndoRedoVisuals();
    }

    private void PushAction(IEditorCommand action)
    {
        undoStack.Push(action);
        TrimStackToNewest(undoStack, maxUndoHistory);
        redoStack.Clear();
    }

    private void TrimStackToNewest(Stack<IEditorCommand> stack, int maxCount)
    {
        if (stack == null || stack.Count <= maxCount)
        {
            return;
        }

        IEditorCommand[] current = stack.ToArray();
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
        GameObject wallObject = WallObjectFactory.CreateWallObject(
            snapshot.name,
            wallRoot,
            cachedCubeMesh,
            snapshot.visualState);
        if (!WallObjectFactory.ConfigureWall(
                wallObject,
                snapshot.wallData,
                snapshot.startVertexId,
                snapshot.endVertexId,
                snapshot.suppressStartHandle,
                snapshot.suppressEndHandle,
                snapshot.startSplitPoint,
                snapshot.endSplitPoint,
                0.01f,
                wallLengthDisplay,
                false))
        {
            Destroy(wallObject);
            return null;
        }
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

    internal void ApplyWallStateChanges(List<WallStateChangeRecord> records, bool useAfterState)
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

        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null && snapshot.visualState.wallMaterial != null)
        {
            renderer.sharedMaterial = snapshot.visualState.wallMaterial;
        }

        Wall wallComponent = wallObject.GetComponent<Wall>();
        if (wallComponent == null)
        {
            wallComponent = wallObject.AddComponent<Wall>();
        }

        wallComponent.SetTopMaterial(snapshot.visualState.topMaterial);
        wallComponent.SetTopFaceOffset(snapshot.visualState.topFaceOffset);
        wallObject.name = snapshot.name;
        WallObjectFactory.ConfigureWall(
            wallObject,
            snapshot.wallData,
            snapshot.startVertexId,
            snapshot.endVertexId,
            snapshot.suppressStartHandle,
            snapshot.suppressEndHandle,
            snapshot.startSplitPoint,
            snapshot.endSplitPoint,
            0.01f,
            wallLengthDisplay,
            false);
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

    internal void ApplyOpeningLayoutSnapshot(OpeningLayoutSnapshot target, OpeningLayoutSnapshot current)
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

    internal void ApplyRoomPolygonSnapshot(RoomPolygonSnapshot snapshot)
    {
        if (snapshot.room == null || snapshot.vertices == null || snapshot.vertices.Count < 3)
        {
            return;
        }

        RoomManager manager = GetRoomManager();
        if (manager == null)
        {
            return;
        }

        manager.UpdateRoomPolygon(snapshot.room, snapshot.vertices);
    }

    private Wall FindWall(WallReference wallReference)
    {
        EnsureWallRoot(false);
        if (wallRoot == null)
        {
            return null;
        }

        Wall fallbackMatch = null;
        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls, true);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
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

        Transform wallRootTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        if (wallRootTransform == null && createIfMissing)
        {
            wallRootTransform = new GameObject(LayerUtility.DefaultWallRootName).transform;
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

        EditorVisualEvents.RequestOpeningMarkerRefresh();
        EditorVisualEvents.RequestTopViewRefresh();
    }
}
