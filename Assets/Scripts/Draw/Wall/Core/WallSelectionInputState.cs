using UnityEngine;

internal sealed class WallSelectionInputState
{
    public bool PendingMultiSelectDrag;
    public bool IsMultiSelecting;
    public bool AddToSelectionOnDragStart;
    public Vector2 MultiSelectStartMousePosition;
    public Vector3 MultiSelectStartWorldPoint;

    public void ResetMultiSelect()
    {
        PendingMultiSelectDrag = false;
        IsMultiSelecting = false;
    }
}
