using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionState
{
    private readonly HashSet<WallOpeningContainer> detailSelectedContainers = new HashSet<WallOpeningContainer>();
    private readonly HashSet<GameObject> detailSelectedWalls = new HashSet<GameObject>();
    private GameObject selectedWall;
    private GameObject lastNotifiedSelectedWall;

    public GameObject SelectedWall
    {
        get => selectedWall;
        set => selectedWall = value;
    }

    public HashSet<WallOpeningContainer> DetailSelectedContainers => detailSelectedContainers;
    public HashSet<GameObject> DetailSelectedWalls => detailSelectedWalls;

    public int SelectedWallCount => (selectedWall != null ? 1 : 0) + detailSelectedWalls.Count + detailSelectedContainers.Count;

    public void GetSelectedWalls(List<GameObject> result)
    {
        if (result == null)
        {
            return;
        }

        result.Clear();
        if (selectedWall != null)
        {
            result.Add(selectedWall);
        }

        foreach (GameObject wall in detailSelectedWalls)
        {
            if (wall != null && wall != selectedWall)
            {
                result.Add(wall);
            }
        }

        foreach (var container in detailSelectedContainers)
        {
            if (container != null)
            {
                result.Add(container.gameObject);
            }
        }
    }

    public void ClearDetailSelection()
    {
        detailSelectedWalls.Clear();
        detailSelectedContainers.Clear();
    }

    public void ClearPrimarySelection()
    {
        selectedWall = null;
    }

    public void ClearAll()
    {
        selectedWall = null;
        detailSelectedWalls.Clear();
        detailSelectedContainers.Clear();
    }

    public bool IsSelected(GameObject wallObject)
    {
        return wallObject != null &&
               (wallObject == selectedWall || detailSelectedWalls.Contains(wallObject));
    }

    public bool IsContainerSelected(WallOpeningContainer container)
    {
        return container != null && detailSelectedContainers.Contains(container);
    }

    public bool ToggleDetailSelection(GameObject wallObject)
    {
        if (wallObject == null)
        {
            return false;
        }

        if (wallObject.TryGetComponent(out WallOpeningContainer container))
        {
            if (detailSelectedContainers.Contains(container))
            {
                detailSelectedContainers.Remove(container);
                return false;
            }

            detailSelectedContainers.Add(container);
            return true;
        }

        if (detailSelectedWalls.Contains(wallObject))
        {
            detailSelectedWalls.Remove(wallObject);
            return false;
        }

        detailSelectedWalls.Add(wallObject);
        return true;
    }

    public void ApplyMultiSelection(IEnumerable<GameObject> wallObjects, bool additive)
    {
        if (!additive)
        {
            ClearAll();
        }

        if (wallObjects == null)
        {
            return;
        }

        foreach (GameObject wallObject in wallObjects)
        {
            if (wallObject == null)
            {
                continue;
            }

            if (selectedWall == null)
            {
                selectedWall = wallObject;
            }
            else if (wallObject != selectedWall)
            {
                detailSelectedWalls.Add(wallObject);
            }
            else if (wallObject.TryGetComponent(out WallOpeningContainer container))
            {
                detailSelectedContainers.Add(container);
            }
        }
    }

    public bool TryGetSelectionChanged(out GameObject currentSelection)
    {
        currentSelection = selectedWall;
        if (lastNotifiedSelectedWall == selectedWall)
        {
            return false;
        }

        lastNotifiedSelectedWall = selectedWall;
        return true;
    }

    public void SetPrimarySelection(WallOpeningContainer container)
    {
        ClearAll();
        detailSelectedContainers.Add(container);
    }
}
