using System.Collections.Generic;
using UnityEngine;

public partial class WallSelectionManager
{
    private void PrepareSelectUI()
    {
        presentationController.PrepareSelectUI(selectUIObject);
    }

    private void SetSelectUIVisible(bool visible)
    {
        presentationController.SetSelectUIVisible(selectUIObject, visible);
    }

    private void EnsureSelectionCanvas()
    {
        if (isShuttingDown)
        {
            return;
        }

        wallSelectionCanvas = presentationController.EnsureSelectionCanvas(wallSelectionCanvas);
    }

    private void RefreshWallSelectionUIStates()
    {
        if (isShuttingDown)
        {
            return;
        }

        EnsureSelectionCanvas();
        CollectLogicalWallRoots(wallRoot, logicalWallRoots);

        presentationController.RefreshWallSelectionUIStates(
            wallRoot,
            logicalWallRoots,
            IsWallOrContainerSelected,
            IsSelected,
            this);
    }

    private void RefreshWallSelectionUIPositions()
    {
        if (isShuttingDown)
        {
            return;
        }

        EnsureSelectionCanvas();
        CollectLogicalWallRoots(wallRoot, logicalWallRoots);

        presentationController.RefreshWallSelectionUIPositions(
            wallRoot,
            logicalWallRoots,
            this);
    }

    private bool ShouldDisplaySelectionProxy(Wall wall)
    {
        return ShouldDisplaySelectionProxyInternal(wall);
    }

    private Wall GetRepresentativeWallForContainer(WallOpeningContainer container)
    {
        return GetRepresentativeWallForContainerInternal(container);
    }

    private bool IsWallOrContainerSelected(Wall wall)
    {
        if (wallOpeningPlacementManager != null && wallOpeningPlacementManager.SelectedOpening != null)
        {
            return false;
        }

        return IsWallOrContainerSelectedInternal(wall);
    }

    private void CollectLogicalWallRoots(Transform currentWallRoot, List<Transform> results)
    {
        results.Clear();
        if (currentWallRoot == null)
        {
            return;
        }

        for (int i = 0; i < currentWallRoot.childCount; i++)
        {
            Transform child = currentWallRoot.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy &&
                (child.GetComponent<WallOpeningContainer>() != null || child.GetComponent<Wall>() != null))
            {
                results.Add(child);
            }
        }
    }
}
