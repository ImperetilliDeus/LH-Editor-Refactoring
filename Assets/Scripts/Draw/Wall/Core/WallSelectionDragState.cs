using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionDragState
{
    public readonly Dictionary<GameObject, WallGeometryService.WallEndpointState> MoveStartEndpointSnapshots = new Dictionary<GameObject, WallGeometryService.WallEndpointState>();
    public readonly List<Wall> DragAffectedWalls = new List<Wall>();
    public readonly List<WallOpeningContainer> DragAffectedOpeningContainers = new List<WallOpeningContainer>();
    public readonly Dictionary<WallOpeningContainer, OpeningLayoutSnapshot> MoveStartConnectedOpeningSnapshots = new Dictionary<WallOpeningContainer, OpeningLayoutSnapshot>();

    public Vector3 DragSelectedStartPoint;
    public Vector3 DragSelectedEndPoint;
    public int DragSelectedStartVertexId;
    public int DragSelectedEndVertexId;
    public WallOpeningContainer SelectedOpeningContainer;
    public Vector3 MoveStartContainerPosition;
    public Vector3 MoveStartContainerWallStart;
    public Vector3 MoveStartContainerWallEnd;

    public void Reset()
    {
        MoveStartEndpointSnapshots.Clear();
        DragAffectedWalls.Clear();
        DragAffectedOpeningContainers.Clear();
        MoveStartConnectedOpeningSnapshots.Clear();
        DragSelectedStartVertexId = 0;
        DragSelectedEndVertexId = 0;
        SelectedOpeningContainer = null;
    }
}
