using System.Collections.Generic;
using UnityEngine;

public partial class WallSelectionManager
{
    private void PrepareSelectUI()
    {
        if (selectUIObject == null)
        {
            return;
        }

        selectUIObject.SetActive(false);
    }

    private void SetSelectUIVisible(bool visible)
    {
        if (selectUIObject == null)
        {
            return;
        }

        if (selectUIObject.activeSelf != visible)
        {
            selectUIObject.SetActive(visible);
        }
    }

    private void EnsureSelectionCanvas()
    {
        if (wallSelectionCanvas != null)
        {
            return;
        }

        wallSelectionCanvas = LayerUtility.FindCanvasByNameOrFirst("_Screen");
    }

    private void RefreshWallSelectionUIStates()
    {
        if (wallRoot == null)
        {
            return;
        }

        processedSelectionUIContainers.Clear();
        List<Wall> walls = GetRootWalls();
        for (int i = 0; i < walls.Count; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            WallSelectionUIProxy proxy = wall.GetComponent<WallSelectionUIProxy>();
            if (!ShouldDisplaySelectionProxy(wall))
            {
                if (proxy != null)
                {
                    proxy.DestroyUI();
                    Destroy(proxy);
                }

                continue;
            }

            if (proxy == null)
            {
                proxy = wall.gameObject.AddComponent<WallSelectionUIProxy>();
            }

            proxy.Initialize(this);

            proxy.SetSelected(IsWallOrContainerSelected(wall));
        }
    }

    private void RefreshWallSelectionUIPositions()
    {
        if (wallRoot == null)
        {
            return;
        }

        processedSelectionUIContainers.Clear();
        List<Wall> walls = GetRootWalls();
        for (int i = 0; i < walls.Count; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            WallSelectionUIProxy proxy = wall.GetComponent<WallSelectionUIProxy>();
            if (!ShouldDisplaySelectionProxy(wall))
            {
                if (proxy != null)
                {
                    proxy.DestroyUI();
                    Destroy(proxy);
                }

                continue;
            }

            if (proxy == null)
            {
                proxy = wall.gameObject.AddComponent<WallSelectionUIProxy>();
            }

            proxy.Initialize(this);

            proxy.RefreshVisual();
        }
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

        if (wall.gameObject == selectedWall || detailSelectedWalls.Contains(wall.gameObject))
        {
            return true;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        if (selectedWall != null && selectedWall.transform.IsChildOf(container.transform))
        {
            return true;
        }

        foreach (GameObject detailWall in detailSelectedWalls)
        {
            if (detailWall != null && detailWall.transform.IsChildOf(container.transform))
            {
                return true;
            }
        }

        return false;
    }
}
