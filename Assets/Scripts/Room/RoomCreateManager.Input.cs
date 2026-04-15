using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class RoomCreateManager
{
    private void Update()
    {
        if (mainCamera == null || Mouse.current == null || !isRoomCreateModeActive)
        {
            return;
        }

        if (wallHandleManager != null && wallHandleManager.IsDraggingHandle)
        {
            return;
        }

        if (roomHandleManager != null && roomHandleManager.IsDraggingHandle)
        {
            return;
        }

        bool isPointerOverUI = IsPointerOverUI();
        bool isPointerOverRoomHandle = roomHandleManager != null && roomHandleManager.IsPointerOverHandle(Mouse.current.position.ReadValue());

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CancelCurrentInteraction();
            ClearSelectedRoom();
            return;
        }

        if (isDraggingSelectedRoom)
        {
            UpdateSelectedRoomDrag();
            return;
        }

        if (!isDraggingRectangle)
        {
            if (pendingRoomSelection)
            {
                HandlePendingRoomSelection();
                return;
            }

            if (!isPointerOverUI &&
                !isPointerOverRoomHandle &&
                Mouse.current.leftButton.wasPressedThisFrame &&
                TryGetMouseWorldPoint(out Vector3 startPoint))
            {
                pendingSelectedRoom = PickRoomAtWorldPoint(startPoint);
                if (pendingSelectedRoom != null)
                {
                    pendingRoomSelection = true;
                    pendingSelectionStartPoint = startPoint;
                    pendingSelectionStartMousePosition = Mouse.current.position.ReadValue();
                    return;
                }

                BeginRectangleDrag(startPoint);
            }

            return;
        }

        if (Mouse.current.leftButton.isPressed)
        {
            UpdatePreviewWhileDragging();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            CommitDraggedRoom();
        }
    }

    private void UpdatePreviewWhileDragging()
    {
        if (!TryGetMouseWorldPoint(out Vector3 currentPoint))
        {
            HidePreviewObjects();
            return;
        }

        UpdatePreviewFromRectangle(dragStartPoint, currentPoint);
    }

    private void HandlePendingRoomSelection()
    {
        if (Mouse.current == null)
        {
            ClearPendingRoomSelection();
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        float thresholdSqr = clickToSelectThresholdPixels * clickToSelectThresholdPixels;
        float movedSqr = (mousePosition - pendingSelectionStartMousePosition).sqrMagnitude;

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            FocusRoomForEditing(pendingSelectedRoom);
            ClearPendingRoomSelection();
            return;
        }

        if (!Mouse.current.leftButton.isPressed)
        {
            ClearPendingRoomSelection();
            return;
        }

        if (movedSqr < thresholdSqr)
        {
            return;
        }

        Room room = pendingSelectedRoom;
        Vector3 startPoint = pendingSelectionStartPoint;
        ClearPendingRoomSelection();
        if (room != null)
        {
            BeginSelectedRoomDrag(room, startPoint);
            return;
        }

        BeginRectangleDrag(startPoint);
    }
}
