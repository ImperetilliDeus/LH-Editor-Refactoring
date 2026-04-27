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
        if (wall == null)
        {
            return false;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return true;
        }

        return GetRepresentativeWallForContainer(container) == wall;
    }

    private Wall GetRepresentativeWallForContainer(WallOpeningContainer container)
    {
        if (container == null)
        {
            return null;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        Wall representative = null;
        float bestLengthSqr = float.MinValue;

        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            float lengthSqr = (wall.Data.endPoint - wall.Data.startPoint).sqrMagnitude;
            if (lengthSqr <= bestLengthSqr)
            {
                continue;
            }

            bestLengthSqr = lengthSqr;
            representative = wall;
        }

        return representative;
    }

    private bool IsWallOrContainerSelected(Wall wall)
    {
        if (wall == null)
        {
            return false;
        }

        if (selectionState.IsSelected(wall.gameObject))
        {
            return true;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        if (selectionState.SelectedWall != null && selectionState.SelectedWall.transform.IsChildOf(container.transform))
        {
            return true;
        }

        foreach (GameObject detailWall in selectionState.DetailSelectedWalls)
        {
            if (detailWall != null && detailWall.transform.IsChildOf(container.transform))
            {
                return true;
            }
        }

        return false;
    }
}
