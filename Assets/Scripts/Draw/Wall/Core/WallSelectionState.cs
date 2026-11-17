using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionState
{
    private WallOpeningContainer selectedContainer;
    private GameObject selectedWall;
    private GameObject lastNotifiedSelectedWall;

    public GameObject SelectedWall
    {
        get => selectedWall;
        set => selectedWall = value;
    }

    public WallOpeningContainer SelectedContainer => selectedContainer;

    public bool HasSelection => selectedWall != null || selectedContainer != null;

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
        else if (selectedContainer != null)
        {
            result.Add(selectedContainer.gameObject);
        }
    }

    public void ClearPrimarySelection()
    {
        selectedWall = null;
    }

    public void ClearAll()
    {
        selectedWall = null;
        selectedContainer = null;
    }

    public bool IsSelected(GameObject wallObject)
    {
        return wallObject != null && wallObject == selectedWall;
    }

    public bool IsContainerSelected(WallOpeningContainer container)
    {
        return container != null && container == selectedContainer;
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
        selectedContainer = container;
    }
}
