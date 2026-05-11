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
        presentationController.RefreshWallSelectionUIStates(
            wallRoot,
            GetRootWalls(),
            ShouldDisplaySelectionProxy,
            IsWallOrContainerSelected,
            this);
    }

    private void RefreshWallSelectionUIPositions()
    {
        if (isShuttingDown)
        {
            return;
        }

        EnsureSelectionCanvas();
        presentationController.RefreshWallSelectionUIPositions(
            wallRoot,
            GetRootWalls(),
            ShouldDisplaySelectionProxy,
            this);
    }

    private bool ShouldDisplaySelectionProxy(Wall wall)
    {
        return queryService.ShouldDisplaySelectionProxy(wall);
    }

    private Wall GetRepresentativeWallForContainer(WallOpeningContainer container)
    {
        return queryService.GetRepresentativeWallForContainer(container);
    }

    private bool IsWallOrContainerSelected(Wall wall)
    {
        if (wallOpeningPlacementManager != null && wallOpeningPlacementManager.SelectedOpening != null)
        {
            return false;
        }

        return queryService.IsWallOrContainerSelected(wall, selectionState);
    }
}
