public static class RoomCodeResolver
{
    // An explicit room code wins; otherwise fall back to the code registered for the room type.
    public static string Resolve(string roomCode, string roomTypeKey)
    {
        if (!string.IsNullOrWhiteSpace(roomCode))
        {
            return roomCode;
        }

        return RoomTypeCatalog.TryGetCode(roomTypeKey, out int code) ? code.ToString() : string.Empty;
    }

    public static string Resolve(Room room)
    {
        return room != null ? Resolve(room.RoomCode, room.RoomTypeKey) : string.Empty;
    }

    public static string Resolve(RoomData roomData)
    {
        return roomData != null ? Resolve(roomData.RoomCode, roomData.RoomTypeKey) : string.Empty;
    }
}
