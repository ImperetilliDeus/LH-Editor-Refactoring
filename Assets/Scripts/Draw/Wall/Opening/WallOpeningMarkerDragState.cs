internal sealed class WallOpeningMarkerDragState
{
    public UndoRedoManager.OpeningLayoutSnapshot DragStartSnapshot;

    public bool HasDragStartSnapshot { get; set; }

    public bool IsDraggingMarker { get; set; }

    public void ClearSnapshot()
    {
        HasDragStartSnapshot = false;
    }
}
