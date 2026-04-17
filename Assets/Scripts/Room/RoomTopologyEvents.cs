using System;
using System.Collections.Generic;

public static class RoomTopologyEvents
{
    public static event Action RefreshAllRequested;
    public static event Action<ICollection<Wall>, IEnumerable<Wall>> RefreshForWallReplacementRequested;

    public static void RequestRefreshAll()
    {
        RefreshAllRequested?.Invoke();
    }

    public static void RequestRefreshForWallReplacement(ICollection<Wall> removedWalls, IEnumerable<Wall> addedWalls)
    {
        RefreshForWallReplacementRequested?.Invoke(removedWalls, addedWalls);
    }
}
