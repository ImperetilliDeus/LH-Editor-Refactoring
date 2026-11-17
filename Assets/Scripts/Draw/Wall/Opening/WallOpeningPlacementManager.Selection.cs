using System.Collections.Generic;
using UnityEngine;

public partial class WallOpeningPlacementManager
{
    public void CreateDoorOnSelectedWall()
    {
        CreateOpeningOnSelectedWall(OpeningPlacementType.Door);
    }

    public void CreateWindowOnSelectedWall()
    {
        CreateOpeningOnSelectedWall(OpeningPlacementType.Window);
    }

    public void SelectOpening(WallOpening opening)
    {
        if (!CanEditOpenings())
        {
            ClearOpeningSelection();
            return;
        }

        if (SelectedOpening == opening)
        {
            RefreshOpeningSelectionPresentation(opening != null);
            return;
        }

        selectionState.SetSelectedOpening(opening);
        RefreshOpeningSelectionPresentation(opening != null);
    }

    public void ClearOpeningSelection()
    {
        if (SelectedOpening == null)
        {
            RefreshOpeningSelectionPresentation(false);
            return;
        }

        selectionState.ClearSelectedOpening();
        RefreshOpeningSelectionPresentation(false);
    }

    public void DeleteSelectedOpening()
    {
        if (!CanEditOpenings() || SelectedOpening == null)
        {
            return;
        }

        WallOpening openingToDelete = SelectedOpening;
        WallOpeningContainer container = openingToDelete.Container;
        if (container == null)
        {
            DeleteOpeningWithoutContainer(openingToDelete);
            return;
        }

        bool hasRemainingOpenings = HasOtherOpenings(container, openingToDelete);
        ClearOpeningSelection();
        DetachAndDestroyOpening(openingToDelete);
        GameObject restoredWall = hasRemainingOpenings ? null : RestoreContainerIfEmpty(container);
        CompletePostDeleteLayout(container, restoredWall);
    }

    private void DeleteOpeningWithoutContainer(WallOpening opening)
    {
        ClearOpeningSelection();
        if (opening != null)
        {
            DestroyOwnedObject(opening.gameObject);
        }
    }

    private void CompletePostDeleteLayout(
        WallOpeningContainer container,
        GameObject restoredWall)
    {
        if (restoredWall != null)
        {
            SelectRestoredWallAfterOpeningDelete(restoredWall);
        }
        else
        {
            RebuildContainerAfterOpeningDelete(container);
        }
    }

    public void RegisterMarkerUI(WallOpeningMarkerUI markerUI)
    {
        SetMarkerUIRegistered(markerUI, true);
    }

    public void UnregisterMarkerUI(WallOpeningMarkerUI markerUI)
    {
        SetMarkerUIRegistered(markerUI, false);
    }

    public void BeginMarkerDrag(WallOpening opening)
    {
        if (!CanEditOpenings() || opening == null)
        {
            return;
        }

        selectionState.SetSelectedOpening(opening);
        markerDragController.BeginDrag(
            opening,
            markerDragState,
            SetOpeningDetailMenuVisible,
            RefreshOpeningDetailInputs);
    }

    public void DragMarker(WallOpening opening, Vector2 screenPosition)
    {
        if (!CanEditOpenings() || opening == null)
        {
            return;
        }

        markerDragController.Drag(
            opening,
            screenPosition,
            mainCamera,
            (container, targetOpening, projectedDistance) => ClampOpeningCenterDistance(container, targetOpening, projectedDistance),
            UpdateOpeningDragPreview);
    }

    public void EndMarkerDrag(WallOpening opening)
    {
        markerDragController.EndDrag(
            opening,
            markerDragState,
            RebuildContainer,
            RefreshSelectedWallForContainer);
    }

    private void RefreshOpeningSelectionPresentation(bool showDetailMenu)
    {
        SetOpeningDetailMenuVisible(showDetailMenu);
        RefreshOpeningDetailInputs(true);
        MarkMarkerVisualsDirty();
    }

    private void DetachAndDestroyOpening(WallOpening opening)
    {
        if (opening == null)
        {
            return;
        }

        opening.transform.SetParent(null, false);
        opening.gameObject.SetActive(false);
        DestroyOwnedObject(opening.gameObject);
    }

    private void SetMarkerUIRegistered(WallOpeningMarkerUI markerUI, bool registered)
    {
        if (markerUI == null)
        {
            return;
        }

        if (registered)
        {
            markerUIs.Add(markerUI);
        }
        else
        {
            markerUIs.Remove(markerUI);
        }

        MarkMarkerVisualsDirty();
    }

    private void RebuildContainerAfterOpeningDelete(WallOpeningContainer container)
    {
        RebuildContainer(container, false);
        RefreshSelectedWallForContainer(container, container.WallLength * 0.5f);
    }

    private void SelectRestoredWallAfterOpeningDelete(GameObject restoredWall)
    {
        if (wallSelectionManager != null)
        {
            wallSelectionManager.SetSelectedWallPreservingOpeningSelection(restoredWall);
        }
    }

    private void RefreshSelectedWallForContainer(WallOpeningContainer container, float preferredDistance)
    {
        if (container == null || wallSelectionManager == null)
        {
            return;
        }

        GameObject preferredWall = FindClosestSegmentToDistance(container, preferredDistance);
        if (preferredWall != null)
        {
            wallSelectionManager.SetSelectedWallPreservingOpeningSelection(preferredWall);
        }
    }

    private GameObject FindClosestSegmentToDistance(WallOpeningContainer container, float preferredDistance)
    {
        if (container == null)
        {
            return null;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        GameObject bestWall = null;
        float bestDelta = float.MaxValue;

        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (!CanUseSegmentForSelectionRefresh(wall))
            {
                continue;
            }

            float delta = CalculateSelectionSegmentDistanceDelta(
                wall,
                container.WallStart,
                container.WallDirection,
                preferredDistance);
            if (delta >= bestDelta)
            {
                continue;
            }

            bestDelta = delta;
            bestWall = wall.gameObject;
        }

        return bestWall;
    }

    private static float CalculateSelectionSegmentDistanceDelta(
        Wall wall,
        Vector3 wallStart,
        Vector3 wallDirection,
        float preferredDistance)
    {
        Vector3 midpoint = (wall.Data.startPoint + wall.Data.endPoint) * 0.5f;
        float distanceAlongWall = Vector3.Dot(midpoint - wallStart, wallDirection);
        return Mathf.Abs(distanceAlongWall - preferredDistance);
    }

    private static bool CanUseSegmentForSelectionRefresh(Wall wall)
    {
        return wall != null && !WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall);
    }
}
