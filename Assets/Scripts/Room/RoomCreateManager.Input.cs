using UnityEngine;

public sealed partial class RoomCreateManager
{
    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        lastInputFrame = inputFrame;
        if (mainCamera == null || inputProvider == null || !inputFrame.IsPointerAvailable || !isRoomCreateModeActive)
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

        bool isPointerOverUI = inputFrame.PointerOverUI;
        bool hasPointerPosition = inputFrame.IsPointerAvailable;
        Vector2 pointerScreenPosition = inputFrame.PointerScreenPosition;
        bool isPointerOverRoomHandle = hasPointerPosition &&
                                       roomHandleManager != null &&
                                       roomHandleManager.IsPointerOverHandle(pointerScreenPosition);

        if (inputFrame.RightPressedThisFrame)
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
                HandlePendingRoomSelection(inputFrame);
                return;
            }

            if (!isPointerOverUI &&
                !isPointerOverRoomHandle &&
                inputFrame.LeftPressedThisFrame &&
                TryGetMouseWorldPoint(out Vector3 startPoint))
            {
                pendingSelectedRoom = PickRoomAtWorldPoint(startPoint);
                if (pendingSelectedRoom != null)
                {
                    pendingRoomSelection = true;
                    pendingSelectionStartPoint = startPoint;
                    pendingSelectionStartMousePosition = hasPointerPosition ? pointerScreenPosition : Vector2.zero;
                    return;
                }

                BeginRectangleDrag(startPoint);
            }

            return;
        }

        if (inputFrame.LeftPressed)
        {
            UpdatePreviewWhileDragging();
        }

        if (inputFrame.LeftReleasedThisFrame)
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

    private void HandlePendingRoomSelection(EditorInputFrame inputFrame)
    {
        if (!inputFrame.IsPointerAvailable && (inputProvider == null || !inputProvider.IsPointerAvailable))
        {
            ClearPendingRoomSelection();
            return;
        }

        if (!TryGetPointerScreenPosition(out Vector2 mousePosition))
        {
            ClearPendingRoomSelection();
            return;
        }

        float thresholdSqr = clickToSelectThresholdPixels * clickToSelectThresholdPixels;
        float movedSqr = (mousePosition - pendingSelectionStartMousePosition).sqrMagnitude;

        if (inputFrame.LeftReleasedThisFrame)
        {
            FocusRoomForEditing(pendingSelectedRoom);
            ClearPendingRoomSelection();
            return;
        }

        if (!inputFrame.LeftPressed)
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
