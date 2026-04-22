using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionDragState
{
    public readonly Dictionary<GameObject, UndoRedoManager.WallStateSnapshot> MoveStartSnapshots = new Dictionary<GameObject, UndoRedoManager.WallStateSnapshot>();
    public readonly Dictionary<GameObject, WallGeometryService.WallEndpointState> MoveStartEndpointSnapshots = new Dictionary<GameObject, WallGeometryService.WallEndpointState>();
    public readonly List<Wall> DragAffectedWalls = new List<Wall>();
    public readonly List<WallOpeningContainer> DragAffectedOpeningContainers = new List<WallOpeningContainer>();
    public readonly Dictionary<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot> MoveStartConnectedOpeningSnapshots = new Dictionary<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot>();

    public Vector3 DragSelectedStartPoint;
    public Vector3 DragSelectedEndPoint;
    public int DragSelectedStartVertexId;
    public int DragSelectedEndVertexId;
    public WallOpeningContainer SelectedOpeningContainer;
    public Vector3 MoveStartContainerPosition;
    public Vector3 MoveStartContainerWallStart;
    public Vector3 MoveStartContainerWallEnd;
    public UndoRedoManager.OpeningLayoutSnapshot MoveStartOpeningLayoutSnapshot;
    public bool HasMoveStartOpeningLayoutSnapshot;

    public void Reset()
    {
        MoveStartSnapshots.Clear();
        MoveStartEndpointSnapshots.Clear();
        DragAffectedWalls.Clear();
        DragAffectedOpeningContainers.Clear();
        MoveStartConnectedOpeningSnapshots.Clear();
        DragSelectedStartVertexId = 0;
        DragSelectedEndVertexId = 0;
        SelectedOpeningContainer = null;
        HasMoveStartOpeningLayoutSnapshot = false;
    }
}
