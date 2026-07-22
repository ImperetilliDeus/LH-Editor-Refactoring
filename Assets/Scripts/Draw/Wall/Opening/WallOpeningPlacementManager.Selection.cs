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
            ClearOpeningSelection();
            Destroy(openingToDelete.gameObject);
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = CaptureLayoutSnapshot(container);
        bool hasRemainingOpenings = HasOtherOpenings(container, openingToDelete);
        ClearOpeningSelection();
        DetachAndDestroyOpening(openingToDelete);
        GameObject restoredWall = hasRemainingOpenings ? null : RestoreContainerIfEmpty(container);
        UndoRedoManager.OpeningLayoutSnapshot afterSnapshot;

        if (restoredWall != null)
        {
            if (wallSelectionManager != null)
            {
                wallSelectionManager.SetSelectedWallPreservingOpeningSelection(restoredWall);
            }
        }
        else
        {
            RebuildContainer(container, false);
            RefreshSelectedWallForContainer(container, container.WallLength * 0.5f);
        }

        afterSnapshot = CapturePostDeleteLayoutSnapshot(container, restoredWall);
        RecordOpeningDeleteUndo(beforeSnapshot, afterSnapshot);
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
        Destroy(opening.gameObject);
    }

    private UndoRedoManager.OpeningLayoutSnapshot CapturePostDeleteLayoutSnapshot(
        WallOpeningContainer container,
        GameObject restoredWall)
    {
        if (restoredWall != null)
        {
            return CaptureLayoutSnapshot(restoredWall.GetComponent<Wall>());
        }

        return CaptureLayoutSnapshot(container);
    }

    private void RecordOpeningDeleteUndo(
        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot,
        UndoRedoManager.OpeningLayoutSnapshot afterSnapshot)
    {
        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, afterSnapshot);
        }
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
}
