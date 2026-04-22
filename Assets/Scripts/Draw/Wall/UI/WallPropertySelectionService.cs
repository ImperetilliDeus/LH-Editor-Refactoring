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

    public void GetSelectedWallComponents(WallSelectionManager wallSelectionManager, List<Wall> result)
    {
        if (result == null)
        {
            return;
        }

        result.Clear();
        if (wallSelectionManager == null)
        {
            return;
        }

        wallSelectionManager.GetSelectedWalls(selectedWallObjects);
        for (int i = 0; i < selectedWallObjects.Count; i++)
        {
            GameObject wallObject = selectedWallObjects[i];
            if (wallObject == null || !wallObject.TryGetComponent(out Wall wall))
            {
                continue;
            }

            result.Add(wall);
        }
    }

    public bool IsFieldEnabledForCurrentSelection(
        WallSelectionManager wallSelectionManager,
        bool isDisabledForMultiSelection)
    {
        if (wallSelectionManager == null || !wallSelectionManager.HasMultiWallSelection)
        {
            return true;
        }

        return !isDisabledForMultiSelection;
    }

    public bool IsMultiSelectionActive(WallSelectionManager wallSelectionManager)
    {
        return wallSelectionManager != null && wallSelectionManager.HasMultiWallSelection;
    }
}
