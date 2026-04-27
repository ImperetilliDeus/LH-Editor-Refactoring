using System.Collections.Generic;
using UnityEngine;

public partial class UndoRedoManager
{
    private class RoomCreateAction : IEditorCommand
    {
        private Room room;
        private readonly List<WallReference> wallReferences;
        private readonly List<Vector3> manualVertices;
        private readonly string roomName;
        private readonly string roomTypeKey;

        public RoomCreateAction(Room createdRoom)
        {
            room = createdRoom;
            wallReferences = new List<WallReference>();
            manualVertices = new List<Vector3>();
            roomName = createdRoom != null ? createdRoom.RoomName : string.Empty;
            roomTypeKey = createdRoom != null ? createdRoom.RoomTypeKey : string.Empty;

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

        public void Execute(UndoRedoManager context)
        {
            Redo(context);
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
                ApplyRoomMetadata(room);
                return;
            }

            if (walls.Count < 3)
            {
                return;
            }

            room = manager.CreateRoom(walls);
            ApplyRoomMetadata(room);
        }

        private void ApplyRoomMetadata(Room targetRoom)
        {
            if (targetRoom == null)
            {
                return;
            }

            targetRoom.SetRoomName(roomName);
            targetRoom.SetRoomTypeKey(roomTypeKey);
        }
    }

    private class RoomPolygonChangeAction : IEditorCommand
    {
        private readonly RoomPolygonSnapshot before;
        private readonly RoomPolygonSnapshot after;

        public RoomPolygonChangeAction(RoomPolygonSnapshot before, RoomPolygonSnapshot after)
        {
            this.before = before;
            this.after = after;
        }

        public void Execute(UndoRedoManager context)
        {
            Redo(context);
        }

        public void Undo(UndoRedoManager context)
        {
            context.ApplyRoomPolygonSnapshot(before);
        }

        public void Redo(UndoRedoManager context)
        {
            context.ApplyRoomPolygonSnapshot(after);
        }
    }

    private class RoomReplaceAction : IEditorCommand
    {
        private struct RoomSnapshot
        {
            public List<WallReference> wallReferences;
            public List<Vector3> manualVertices;
            public string roomName;
            public string roomTypeKey;
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

        public void Execute(UndoRedoManager context)
        {
            Redo(context);
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
                    roomName = room.RoomName,
                    roomTypeKey = room.RoomTypeKey,
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
                    room.SetRoomName(snapshot.roomName);
                    room.SetRoomTypeKey(snapshot.roomTypeKey);
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

    private class DeleteLayoutsAction : IEditorCommand
    {
        private struct RoomSnapshot
        {
            public List<WallReference> wallReferences;
            public List<Vector3> manualVertices;
            public string roomName;
            public string roomTypeKey;
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
                    roomName = room.RoomName,
                    roomTypeKey = room.RoomTypeKey,
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

        public void Execute(UndoRedoManager context)
        {
            Redo(context);
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
                    Room restoredRoom = manager.CreateRoomFromPolygon(new List<Vector3>(snapshot.manualVertices), walls.Count > 0 ? walls : null);
                    if (restoredRoom != null)
                    {
                        restoredRoom.SetRoomName(snapshot.roomName);
                        restoredRoom.SetRoomTypeKey(snapshot.roomTypeKey);
                    }
                    continue;
                }

                if (walls.Count < 3 || manager.FindRoomByWallSet(walls) != null)
                {
                    continue;
                }

                Room restoredRoomFromWalls = manager.CreateRoom(walls);
                if (restoredRoomFromWalls != null)
                {
                    restoredRoomFromWalls.SetRoomName(snapshot.roomName);
                    restoredRoomFromWalls.SetRoomTypeKey(snapshot.roomTypeKey);
                }
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
            startPoint = wall.Data.startPoint,
            endPoint = wall.Data.endPoint,
            startVertexId = wall.StartVertexId,
            endVertexId = wall.EndVertexId,
        };
    }
}
