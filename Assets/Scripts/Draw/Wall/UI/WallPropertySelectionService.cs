using System.Collections.Generic;
using UnityEngine;

internal sealed class WallPropertySelectionService
{
    private readonly List<GameObject> selectedWallObjects = new List<GameObject>();

    public GameObject GetSelectedWall(WallSelectionManager wallSelectionManager)
    {
        if (wallSelectionManager == null)
        {
            return null;
        }

        if (wallSelectionManager.SelectedWall != null)
        {
            return wallSelectionManager.SelectedWall;
        }

        wallSelectionManager.GetSelectedWalls(selectedWallObjects);
        for (int i = 0; i < selectedWallObjects.Count; i++)
        {
            if (selectedWallObjects[i] != null)
            {
                return selectedWallObjects[i];
            }
        }

        return null;
    }
}
