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
        if (SelectedOpening == opening)
        {
            SetOpeningDetailMenuVisible(opening != null);
            RefreshOpeningDetailInputs(true);
            MarkMarkerVisualsDirty();
            return;
        }

        selectionState.SetSelectedOpening(opening);
        SetOpeningDetailMenuVisible(opening != null);
        RefreshOpeningDetailInputs(true);
        MarkMarkerVisualsDirty();
    }

    public void ClearOpeningSelection()
    {
        if (SelectedOpening == null)
        {
            SetOpeningDetailMenuVisible(false);
            RefreshOpeningDetailInputs(true);
            MarkMarkerVisualsDirty();
            return;
        }

        selectionState.ClearSelectedOpening();
        SetOpeningDetailMenuVisible(false);
        RefreshOpeningDetailInputs(true);
        MarkMarkerVisualsDirty();
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
            ClearOpeningSelection();
            Destroy(openingToDelete.gameObject);
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = CaptureLayoutSnapshot(container);
        bool hasRemainingOpenings = HasOtherOpenings(container, openingToDelete);
        ClearOpeningSelection();
        openingToDelete.transform.SetParent(null, false);
        openingToDelete.gameObject.SetActive(false);
        Destroy(openingToDelete.gameObject);
        GameObject restoredWall = hasRemainingOpenings ? null : RestoreContainerIfEmpty(container);
        UndoRedoManager.OpeningLayoutSnapshot afterSnapshot;

        if (restoredWall != null)
        {
            afterSnapshot = CaptureLayoutSnapshot(restoredWall.GetComponent<Wall>());
            if (wallSelectionManager != null)
            {
                wallSelectionManager.SetSelectedWallPreservingOpeningSelection(restoredWall);
            }
        }
        else
        {
            RebuildContainer(container, false);
            RefreshSelectedWallForContainer(container, container.WallLength * 0.5f);
            afterSnapshot = CaptureLayoutSnapshot(container);
        }

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, afterSnapshot);
        }
    }

    public void RegisterMarkerUI(WallOpeningMarkerUI markerUI)
    {
        if (markerUI == null)
        {
            return;
        }

        markerUIs.Add(markerUI);
        MarkMarkerVisualsDirty();
    }

    public void UnregisterMarkerUI(WallOpeningMarkerUI markerUI)
    {
        if (markerUI == null)
        {
            return;
        }

        markerUIs.Remove(markerUI);
        MarkMarkerVisualsDirty();
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
            RefreshOpeningDetailInputs,
            CaptureLayoutSnapshot);
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
            RebuildContainer);
    }

    public void EndMarkerDrag(WallOpening opening)
    {
        markerDragController.EndDrag(
            opening,
            markerDragState,
            undoRedoManager,
            RebuildContainer,
            RefreshSelectedWallForContainer,
            CaptureLayoutSnapshot);
    }
}
