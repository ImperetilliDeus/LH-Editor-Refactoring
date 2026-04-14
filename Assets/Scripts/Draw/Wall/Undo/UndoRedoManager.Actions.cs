using System.Collections.Generic;
using UnityEngine;

public partial class UndoRedoManager
{
    private interface IUndoableAction
    {
        void Undo(UndoRedoManager context);
        void Redo(UndoRedoManager context);
    }

    private class WallCreateAction : IUndoableAction
    {
        private GameObject wallObject;
        private readonly WallStateSnapshot snapshot;

        public WallCreateAction(GameObject createdWall)
        {
            wallObject = createdWall;
            snapshot = WallStateSnapshot.Capture(createdWall);
        }

        public void Undo(UndoRedoManager context)
        {
            if (wallObject == null)
            {
                return;
            }

            context.UnregisterWallVisuals(wallObject);
            Object.Destroy(wallObject);
            wallObject = null;
        }

        public void Redo(UndoRedoManager context)
        {
            if (wallObject != null)
            {
                return;
            }

            wallObject = context.CreateWallFromSnapshot(snapshot);
            context.RegisterWallVisuals(wallObject);
        }
    }

    private class MoveVertexGroupAction : IUndoableAction
    {
        private readonly int vertexId;
        private readonly List<WallStateChangeRecord> records;

        public MoveVertexGroupAction(int vertexId, List<WallStateChangeRecord> records)
        {
            this.vertexId = vertexId;
            this.records = records;
        }

        public void Undo(UndoRedoManager context)
        {
            context.ApplyWallStateChanges(records, false);
        }

        public void Redo(UndoRedoManager context)
        {
            context.ApplyWallStateChanges(records, true);
        }
    }

    private class MoveConnectedWallsAction : IUndoableAction
    {
        private readonly List<WallStateChangeRecord> records;

        public MoveConnectedWallsAction(List<WallStateChangeRecord> records)
        {
            this.records = records;
        }

        public void Undo(UndoRedoManager context)
        {
            context.ApplyWallStateChanges(records, false);
        }

        public void Redo(UndoRedoManager context)
        {
            context.ApplyWallStateChanges(records, true);
        }
    }

    private class RoomCreateAction : IUndoableAction
    {
        private Room room;
        private readonly List<WallReference> wallReferences;
        private readonly List<Vector3> manualVertices;

        public RoomCreateAction(Room createdRoom)
        {
            room = createdRoom;
            wallReferences = new List<WallReference>();
            manualVertices = new List<Vector3>();

            if (createdRoom != null && createdRoom.WallSet != null)
            {
                foreach (Wall wall in createdRoom.WallSet)
                {
                    if (wall != null)
                    {
                        wallReferences.Add(BuildWallReference(wall));
                    }
                }
            }

            if (createdRoom != null && createdRoom.ManualBoundaryVertices != null)
            {
                foreach (Vector3 vertex in createdRoom.ManualBoundaryVertices)
                {
                    manualVertices.Add(vertex);
                }
            }
        }

        public void Undo(UndoRedoManager context)
        {
            RoomManager manager = context.GetRoomManager();
            if (manager == null || room == null)
            {
                return;
            }

            manager.DeleteRoom(room);
            room = null;
        }

        public void Redo(UndoRedoManager context)
        {
            if (room != null)
            {
                return;
            }

            RoomManager manager = context.GetRoomManager();
            if (manager == null)
            {
                return;
            }

            HashSet<Wall> walls = new HashSet<Wall>();
            for (int i = 0; i < wallReferences.Count; i++)
            {
                Wall wall = context.FindWall(wallReferences[i]);
                if (wall != null)
                {
                    walls.Add(wall);
                }
            }

            if (manualVertices.Count >= 3)
            {
                room = manager.CreateRoomFromPolygon(new List<Vector3>(manualVertices), walls.Count > 0 ? walls : null);
                return;
            }

            if (walls.Count < 3)
            {
                return;
            }

            room = manager.CreateRoom(walls);
        }
    }

    private class WallSplitAction : IUndoableAction
    {
        private readonly WallStateSnapshot originalSnapshot;
        private readonly WallStateSnapshot firstSplitSnapshot;
        private readonly WallStateSnapshot secondSplitSnapshot;
        private GameObject restoredOriginalWall;
        private GameObject restoredFirstSplitWall;
        private GameObject restoredSecondSplitWall;

        public WallSplitAction(GameObject originalWall, GameObject firstSplitWall, GameObject secondSplitWall)
        {
            originalSnapshot = WallStateSnapshot.Capture(originalWall);
            firstSplitSnapshot = WallStateSnapshot.Capture(firstSplitWall);
            secondSplitSnapshot = WallStateSnapshot.Capture(secondSplitWall);
            restoredFirstSplitWall = firstSplitWall;
            restoredSecondSplitWall = secondSplitWall;
        }

        public void Undo(UndoRedoManager context)
        {
            RoomManager roomManager = context.GetRoomManager();
            List<Wall> removedWalls = CollectExistingWalls(restoredFirstSplitWall, restoredSecondSplitWall);

            DeleteWall(context, ref restoredFirstSplitWall);
            DeleteWall(context, ref restoredSecondSplitWall);

            if (restoredOriginalWall == null)
            {
                restoredOriginalWall = context.CreateWallFromSnapshot(originalSnapshot);
                context.RegisterWallVisuals(restoredOriginalWall);
            }

            if (roomManager != null)
            {
                Wall restoredWallComponent = restoredOriginalWall != null ? restoredOriginalWall.GetComponent<Wall>() : null;
                roomManager.RefreshRoomsForWallReplacement(
                    removedWalls,
                    restoredWallComponent != null ? new[] { restoredWallComponent } : null);
            }
        }

        public void Redo(UndoRedoManager context)
        {
            RoomManager roomManager = context.GetRoomManager();
            List<Wall> removedWalls = CollectExistingWalls(restoredOriginalWall);

            DeleteWall(context, ref restoredOriginalWall);

            if (restoredFirstSplitWall == null)
            {
                restoredFirstSplitWall = context.CreateWallFromSnapshot(firstSplitSnapshot);
                context.RegisterWallVisuals(restoredFirstSplitWall);
            }

            if (restoredSecondSplitWall == null)
            {
                restoredSecondSplitWall = context.CreateWallFromSnapshot(secondSplitSnapshot);
                context.RegisterWallVisuals(restoredSecondSplitWall);
            }

            if (roomManager != null)
            {
                List<Wall> addedWalls = CollectExistingWalls(restoredFirstSplitWall, restoredSecondSplitWall);
                roomManager.RefreshRoomsForWallReplacement(removedWalls, addedWalls);
            }
        }

        private static List<Wall> CollectExistingWalls(params GameObject[] wallObjects)
        {
            List<Wall> results = new List<Wall>();
            if (wallObjects == null)
            {
                return results;
            }

            for (int i = 0; i < wallObjects.Length; i++)
            {
                GameObject wallObject = wallObjects[i];
                if (wallObject == null)
                {
                    continue;
                }

                Wall wall = wallObject.GetComponent<Wall>();
                if (wall != null)
                {
                    results.Add(wall);
                }
            }

            return results;
        }

        private static void DeleteWall(UndoRedoManager context, ref GameObject wallObject)
        {
            if (wallObject == null)
            {
                return;
            }

            context.UnregisterWallVisuals(wallObject);
            Object.Destroy(wallObject);
            wallObject = null;
        }
    }

    private class RoomReplaceAction : IUndoableAction
    {
        private struct RoomSnapshot
        {
            public List<WallReference> wallReferences;
            public List<Vector3> manualVertices;
        }

        private readonly List<RoomSnapshot> deletedSnapshots;
        private readonly List<RoomSnapshot> createdSnapshots;
        private List<Room> deletedRuntimeRooms;
        private List<Room> createdRuntimeRooms;

        public RoomReplaceAction(List<Room> deletedRooms, List<Room> createdRooms)
        {
            deletedSnapshots = CaptureRoomSnapshots(deletedRooms);
            createdSnapshots = CaptureRoomSnapshots(createdRooms);
            deletedRuntimeRooms = deletedRooms != null ? new List<Room>(deletedRooms) : new List<Room>();
            createdRuntimeRooms = createdRooms != null ? new List<Room>(createdRooms) : new List<Room>();
        }

        public void Undo(UndoRedoManager context)
        {
            RoomManager manager = context.GetRoomManager();
            if (manager == null)
            {
                return;
            }

            DeleteRooms(manager, createdRuntimeRooms);
            createdRuntimeRooms = new List<Room>();
            deletedRuntimeRooms = RestoreRoomSnapshots(context, deletedSnapshots);
        }

        public void Redo(UndoRedoManager context)
        {
            RoomManager manager = context.GetRoomManager();
            if (manager == null)
            {
                return;
            }

            DeleteRooms(manager, deletedRuntimeRooms);
            deletedRuntimeRooms = new List<Room>();
            createdRuntimeRooms = RestoreRoomSnapshots(context, createdSnapshots);
        }

        private static List<RoomSnapshot> CaptureRoomSnapshots(List<Room> rooms)
        {
            List<RoomSnapshot> snapshots = new List<RoomSnapshot>();
            if (rooms == null)
            {
                return snapshots;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (room == null)
                {
                    continue;
                }

                RoomSnapshot snapshot = new RoomSnapshot
                {
                    wallReferences = new List<WallReference>(),
                    manualVertices = new List<Vector3>(),
                };

                if (room.WallSet != null)
                {
                    foreach (Wall wall in room.WallSet)
                    {
                        if (wall != null)
                        {
                            snapshot.wallReferences.Add(BuildWallReference(wall));
                        }
                    }
                }

                if (room.ManualBoundaryVertices != null)
                {
                    foreach (Vector3 vertex in room.ManualBoundaryVertices)
                    {
                        snapshot.manualVertices.Add(vertex);
                    }
                }

                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        private static List<Room> RestoreRoomSnapshots(UndoRedoManager context, List<RoomSnapshot> snapshots)
        {
            List<Room> restoredRooms = new List<Room>();
            RoomManager manager = context.GetRoomManager();
            if (manager == null || snapshots == null)
            {
                return restoredRooms;
            }

            for (int i = 0; i < snapshots.Count; i++)
            {
                RoomSnapshot snapshot = snapshots[i];
                HashSet<Wall> walls = new HashSet<Wall>();
                for (int j = 0; j < snapshot.wallReferences.Count; j++)
                {
                    Wall wall = context.FindWall(snapshot.wallReferences[j]);
                    if (wall != null)
                    {
                        walls.Add(wall);
                    }
                }

                Room room = null;
                if (snapshot.manualVertices != null && snapshot.manualVertices.Count >= 3)
                {
                    room = manager.CreateRoomFromPolygon(new List<Vector3>(snapshot.manualVertices), walls.Count > 0 ? walls : null);
                }
                else if (walls.Count >= 3)
                {
                    room = manager.CreateRoom(walls);
                }

                if (room != null)
                {
                    restoredRooms.Add(room);
                }
            }

            return restoredRooms;
        }

        private static void DeleteRooms(RoomManager manager, List<Room> rooms)
        {
            if (manager == null || rooms == null)
            {
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                if (rooms[i] != null)
                {
                    manager.DeleteRoom(rooms[i]);
                }
            }
        }
    }

    private class OpeningLayoutChangeAction : IUndoableAction
    {
        private readonly OpeningLayoutSnapshot before;
        private readonly OpeningLayoutSnapshot after;

        public OpeningLayoutChangeAction(OpeningLayoutSnapshot before, OpeningLayoutSnapshot after)
        {
            this.before = before;
            this.after = after;
        }

        public void Undo(UndoRedoManager context)
        {
            context.ApplyOpeningLayoutSnapshot(before, after);
        }

        public void Redo(UndoRedoManager context)
        {
            context.ApplyOpeningLayoutSnapshot(after, before);
        }
    }

    private class DeleteLayoutsAction : IUndoableAction
    {
        private struct RoomSnapshot
        {
            public List<WallReference> wallReferences;
            public List<Vector3> manualVertices;
        }

        private readonly List<OpeningLayoutSnapshot> deletedLayouts;
        private readonly List<RoomSnapshot> deletedRooms;

        public DeleteLayoutsAction(List<OpeningLayoutSnapshot> layouts, List<Room> affectedRooms)
        {
            deletedLayouts = layouts != null ? new List<OpeningLayoutSnapshot>(layouts) : new List<OpeningLayoutSnapshot>();
            deletedRooms = new List<RoomSnapshot>();

            if (affectedRooms == null)
            {
                return;
            }

            for (int i = 0; i < affectedRooms.Count; i++)
            {
                Room room = affectedRooms[i];
                if (room == null || room.WallSet == null)
                {
                    continue;
                }

                RoomSnapshot snapshot = new RoomSnapshot
                {
                    wallReferences = new List<WallReference>(),
                    manualVertices = new List<Vector3>(),
                };

                foreach (Wall wall in room.WallSet)
                {
                    if (wall != null)
                    {
                        snapshot.wallReferences.Add(BuildWallReference(wall));
                    }
                }

                if (room.ManualBoundaryVertices != null)
                {
                    foreach (Vector3 vertex in room.ManualBoundaryVertices)
                    {
                        snapshot.manualVertices.Add(vertex);
                    }
                }

                deletedRooms.Add(snapshot);
            }
        }

        public void Undo(UndoRedoManager context)
        {
            for (int i = 0; i < deletedLayouts.Count; i++)
            {
                context.ApplyOpeningLayoutSnapshot(deletedLayouts[i], default);
            }

            RoomManager manager = context.GetRoomManager();
            if (manager == null)
            {
                return;
            }

            for (int i = 0; i < deletedRooms.Count; i++)
            {
                RoomSnapshot snapshot = deletedRooms[i];
                HashSet<Wall> walls = new HashSet<Wall>();
                for (int j = 0; j < snapshot.wallReferences.Count; j++)
                {
                    Wall wall = context.FindWall(snapshot.wallReferences[j]);
                    if (wall != null)
                    {
                        walls.Add(wall);
                    }
                }

                if (snapshot.manualVertices != null && snapshot.manualVertices.Count >= 3)
                {
                    manager.CreateRoomFromPolygon(new List<Vector3>(snapshot.manualVertices), walls.Count > 0 ? walls : null);
                    continue;
                }

                if (walls.Count < 3 || manager.FindRoomByWallSet(walls) != null)
                {
                    continue;
                }

                manager.CreateRoom(walls);
            }

            manager.RefreshAllRooms();
        }

        public void Redo(UndoRedoManager context)
        {
            RoomManager manager = context.GetRoomManager();
            if (manager != null)
            {
                for (int i = 0; i < deletedRooms.Count; i++)
                {
                    RoomSnapshot snapshot = deletedRooms[i];
                HashSet<Wall> walls = new HashSet<Wall>();
                for (int j = 0; j < snapshot.wallReferences.Count; j++)
                {
                        Wall wall = context.FindWall(snapshot.wallReferences[j]);
                        if (wall != null)
                        {
                            walls.Add(wall);
                    }
                }

                    Room room = manager.FindRoomByWallSet(walls);
                    if (room != null)
                    {
                        manager.DeleteRoom(room);
                    }
                }
            }

            for (int i = 0; i < deletedLayouts.Count; i++)
            {
                context.ApplyOpeningLayoutSnapshot(default, deletedLayouts[i]);
            }
        }
    }

    private static WallReference BuildWallReference(Wall wall)
    {
        return new WallReference
        {
            name = wall.gameObject.name,
            startPoint = wall.StartPoint,
            endPoint = wall.EndPoint,
            startVertexId = wall.StartVertexId,
            endVertexId = wall.EndVertexId,
        };
    }
}
