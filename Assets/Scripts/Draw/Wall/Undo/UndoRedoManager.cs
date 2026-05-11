using System.Collections.Generic;
using UnityEngine;

public partial class UndoRedoManager : MonoBehaviour
{
    public void RecordWallCreated(GameObject wallObject) { }
    public void RecordMoveConnectedWalls(List<WallStateChangeRecord> records) { }
    public void RecordWallTransformChange(
        GameObject wallObject,
        Vector3 beforePosition,
        Quaternion beforeRotation,
        Vector3 beforeScale,
        Vector3 afterPosition,
        Quaternion afterRotation,
        Vector3 afterScale) { }
    public void RecordWallTransformChanges(List<WallTransformRecord> records) { }
    public void RecordRoomCreated(Room room) { }
    public void RecordRoomPolygonChanged(Room room, IReadOnlyList<Vector3> beforeVertices, IReadOnlyList<Vector3> afterVertices) { }
    public void RecordWallSplit(GameObject originalWall, GameObject firstSplitWall, GameObject secondSplitWall) { }
    public void RecordRoomsReplaced(List<Room> deletedRooms, List<Room> createdRooms) { }
    public void RecordOpeningLayoutChange(OpeningLayoutSnapshot before, OpeningLayoutSnapshot after) { }
    public void RecordDeletedLayouts(List<OpeningLayoutSnapshot> layouts, List<Room> affectedRooms) { }
    public void Undo() { }
    public void Redo() { }
    public void UndoFromUI() { }
    public void RedoFromUI() { }
    public void ExecuteCommand(IEditorCommand command, bool alreadyExecuted = false) { }
}
