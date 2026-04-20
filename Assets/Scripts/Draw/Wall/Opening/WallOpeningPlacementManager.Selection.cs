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
        if (selectedOpening == opening)
        {
            SetOpeningDetailMenuVisible(opening != null);
            RefreshOpeningDetailInputs(true);
            MarkMarkerVisualsDirty();
            return;
        }

        selectedOpening = opening;
        SetOpeningDetailMenuVisible(opening != null);
        RefreshOpeningDetailInputs(true);
        MarkMarkerVisualsDirty();
        OpeningSelectionChanged?.Invoke(selectedOpening);
    }

    public void ClearOpeningSelection()
    {
        if (selectedOpening == null)
        {
            SetOpeningDetailMenuVisible(false);
            RefreshOpeningDetailInputs(true);
            MarkMarkerVisualsDirty();
            return;
        }

        selectedOpening = null;
        SetOpeningDetailMenuVisible(false);
        RefreshOpeningDetailInputs(true);
        MarkMarkerVisualsDirty();
        OpeningSelectionChanged?.Invoke(null);
    }

    public void DeleteSelectedOpening()
    {
        if (!CanEditOpenings() || selectedOpening == null)
        {
            return;
        }

        WallOpening openingToDelete = selectedOpening;
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
            RebuildContainer(container);
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

        selectedOpening = opening;
        isDraggingMarker = true;
        SetOpeningDetailMenuVisible(true);
        RefreshOpeningDetailInputs(true);
        openingDragStartSnapshot = CaptureLayoutSnapshot(opening.Container);
        hasOpeningDragStartSnapshot = true;
    }

    public void DragMarker(WallOpening opening, Vector2 screenPosition)
    {
        if (!CanEditOpenings() || opening == null)
        {
            return;
        }

        WallOpeningContainer container = opening.Container;
        if (container == null || mainCamera == null)
        {
            return;
        }

        Plane dragPlane = new Plane(Vector3.up, new Vector3(0f, container.WallPlaneY, 0f));
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        if (!dragPlane.Raycast(ray, out float enter))
        {
            return;
        }

        Vector3 point = ray.GetPoint(enter);
        Vector3 direction = container.WallDirection;
        float projectedDistance = Vector3.Dot(point - container.WallStart, direction);
        float clampedDistance = ClampOpeningCenterDistance(container, opening, projectedDistance);
        opening.SetCenterDistance(clampedDistance);
        RebuildContainer(container);
    }

    public void EndMarkerDrag(WallOpening opening)
    {
        if (opening == null)
        {
            isDraggingMarker = false;
            return;
        }

        isDraggingMarker = false;
        RebuildContainer(opening.Container);
        RefreshSelectedWallForContainer(opening.Container, opening.CenterDistance);

        if (hasOpeningDragStartSnapshot && undoRedoManager != null)
        {
            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = CaptureLayoutSnapshot(opening.Container);
            undoRedoManager.RecordOpeningLayoutChange(openingDragStartSnapshot, afterSnapshot);
        }

        hasOpeningDragStartSnapshot = false;
    }
}
