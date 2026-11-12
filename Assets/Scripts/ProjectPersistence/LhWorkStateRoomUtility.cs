using System.Collections.Generic;
using UnityEngine;

// Room-polygon helpers shared by the work-state builder and loader, operating on saved DTOs.
internal static class LhWorkStateRoomUtility
{
    public static void NormalizeFurnitureRoomsBySavedPolygons(LhWorkStateDto state, string logTag)
    {
        if (state == null || state.rooms == null || state.furniture == null)
        {
            return;
        }

        int roomNameFixes = 0;
        int defectCodeFixes = 0;
        int unresolvedFurniture = 0;
        for (int i = 0; i < state.furniture.Count; i++)
        {
            LhWorkFurnitureDto furniture = state.furniture[i];
            if (furniture == null || !furniture.isPlaced)
            {
                continue;
            }

            LhWorkRoomDto room = ResolveFurnitureRoomDto(furniture.position.ToVector3(), furniture.roomName, state.rooms);
            if (room == null)
            {
                unresolvedFurniture++;
                continue;
            }

            string roomName = room.name ?? string.Empty;
            if (!string.Equals(furniture.roomName ?? string.Empty, roomName, System.StringComparison.Ordinal))
            {
                roomNameFixes++;
                furniture.roomName = roomName;
            }

            string roomCode = ResolveRoomCode(room);
            if (string.IsNullOrWhiteSpace(roomCode) || furniture.defects == null)
            {
                continue;
            }

            for (int defectIndex = 0; defectIndex < furniture.defects.Count; defectIndex++)
            {
                LhWorkFurnitureDefectDto defect = furniture.defects[defectIndex];
                if (defect != null)
                {
                    if (!string.Equals(defect.mntnCd ?? string.Empty, roomCode, System.StringComparison.Ordinal))
                    {
                        defectCodeFixes++;
                        defect.mntnCd = roomCode;
                    }
                }
            }
        }

        Debug.Log(
            $"[{logTag}] furniture room normalization complete: " +
            $"furniture={state.furniture.Count}, roomNameFixes={roomNameFixes}, " +
            $"defectCodeFixes={defectCodeFixes}, unresolvedFurniture={unresolvedFurniture}");
    }

    // Same rule as FurnitureRoomResolver, applied to saved data: the saved room wins while it still contains
    // the furniture, otherwise the first containing room. Saved furniture has no bounds, so only the position is tested.
    public static LhWorkRoomDto ResolveFurnitureRoomDto(Vector3 position, string currentRoomName, IReadOnlyList<LhWorkRoomDto> rooms)
    {
        if (!string.IsNullOrEmpty(currentRoomName))
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                LhWorkRoomDto room = rooms[i];
                if (room != null &&
                    string.Equals(room.name ?? string.Empty, currentRoomName, System.StringComparison.Ordinal) &&
                    IsPointInsidePolygonXZ(position, room.boundaryVertices))
                {
                    return room;
                }
            }
        }

        for (int i = 0; i < rooms.Count; i++)
        {
            LhWorkRoomDto room = rooms[i];
            if (room != null && IsPointInsidePolygonXZ(position, room.boundaryVertices))
            {
                return room;
            }
        }

        return null;
    }

    public static string ResolveRoomCode(LhWorkRoomDto room)
    {
        return room != null ? RoomCodeResolver.Resolve(room.roomCode, room.roomTypeKey) : string.Empty;
    }

    private static bool IsPointInsidePolygonXZ(Vector3 point, IReadOnlyList<LhWorkVector3Dto> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return false;
        }

        bool inside = false;
        float x = point.x;
        float z = point.z;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            Vector3 pi = vertices[i].ToVector3();
            Vector3 pj = vertices[j].ToVector3();
            float deltaZ = pj.z - pi.z;
            bool intersects = Mathf.Abs(deltaZ) > GeometryTolerance.PolygonEdgeDeltaEpsilon &&
                              ((pi.z > z) != (pj.z > z)) &&
                              (x < (pj.x - pi.x) * (z - pi.z) / deltaZ + pi.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
