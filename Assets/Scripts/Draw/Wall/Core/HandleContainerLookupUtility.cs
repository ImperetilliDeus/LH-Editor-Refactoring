using UnityEngine;

internal static class HandleContainerLookupUtility
{
    public static bool TryGetOuterWalls(WallOpeningContainer container, out Wall startWall, out Wall endWall)
    {
        startWall = null;
        endWall = null;
        if (container == null)
        {
            return false;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            if (startWall == null && wall.StartVertexId == container.OuterStartVertexId)
            {
                startWall = wall;
            }

            if (endWall == null && wall.EndVertexId == container.OuterEndVertexId)
            {
                endWall = wall;
            }
        }

        return startWall != null && endWall != null;
    }
}
