using UnityEngine;

internal sealed class WallOpeningMarkerDragController
{
    public void BeginDrag(
        WallOpening opening,
        WallOpeningMarkerDragState dragState,
        System.Action<bool> setOpeningDetailMenuVisible,
        System.Action<bool> refreshOpeningDetailInputs,
        System.Func<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot> captureLayoutSnapshot)
    {
        if (opening == null || dragState == null || captureLayoutSnapshot == null)
        {
            return;
        }

        dragState.IsDraggingMarker = true;
        setOpeningDetailMenuVisible?.Invoke(true);
        refreshOpeningDetailInputs?.Invoke(true);
        dragState.DragStartSnapshot = captureLayoutSnapshot(opening.Container);
        dragState.HasDragStartSnapshot = true;
    }

    public void Drag(
        WallOpening opening,
        Vector2 screenPosition,
        Camera mainCamera,
        System.Func<WallOpeningContainer, WallOpening, float, float> clampOpeningCenterDistance,
        System.Action<WallOpeningContainer, bool> rebuildContainer)
    {
        if (opening == null || mainCamera == null || clampOpeningCenterDistance == null || rebuildContainer == null)
        {
            return;
        }

        WallOpeningContainer container = opening.Container;
        if (container == null)
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
        float clampedDistance = clampOpeningCenterDistance(container, opening, projectedDistance);
        opening.SetCenterDistance(clampedDistance);
        rebuildContainer(container, true);
    }

    public void EndDrag(
        WallOpening opening,
        WallOpeningMarkerDragState dragState,
        UndoRedoManager undoRedoManager,
        System.Action<WallOpeningContainer, bool> rebuildContainer,
        System.Action<WallOpeningContainer, float> refreshSelectedWallForContainer,
        System.Func<WallOpeningContainer, UndoRedoManager.OpeningLayoutSnapshot> captureLayoutSnapshot)
    {
        if (dragState == null)
        {
            return;
        }

        if (opening == null)
        {
            dragState.IsDraggingMarker = false;
            return;
        }

        dragState.IsDraggingMarker = false;
        rebuildContainer?.Invoke(opening.Container, false);
        refreshSelectedWallForContainer?.Invoke(opening.Container, opening.CenterDistance);

        if (dragState.HasDragStartSnapshot && undoRedoManager != null && captureLayoutSnapshot != null)
        {
            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = captureLayoutSnapshot(opening.Container);
            undoRedoManager.RecordOpeningLayoutChange(dragState.DragStartSnapshot, afterSnapshot);
        }

        dragState.ClearSnapshot();
    }
}
