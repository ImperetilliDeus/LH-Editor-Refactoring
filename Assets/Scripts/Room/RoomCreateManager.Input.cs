using UnityEngine;

public sealed partial class RoomCreateManager
{
    private void Update()
    {
        if (mainCamera == null || inputProvider == null || !inputProvider.IsPointerAvailable || !isRoomCreateModeActive)
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
        bool hasPointerPosition = inputProvider.TryGetPointerScreenPosition(out Vector2 pointerScreenPosition);
        bool isPointerOverRoomHandle = hasPointerPosition &&
                                       roomHandleManager != null &&
                                       roomHandleManager.IsPointerOverHandle(pointerScreenPosition);

        if (inputProvider.WasPointerButtonPressedThisFrame(PointerButton.Right))
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
                inputProvider.WasPointerButtonPressedThisFrame(PointerButton.Left) &&
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

        if (inputProvider.IsPointerButtonPressed(PointerButton.Left))
        {
            UpdatePreviewWhileDragging();
        }

        if (inputProvider.WasPointerButtonReleasedThisFrame(PointerButton.Left))
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
        if (inputProvider == null || !inputProvider.IsPointerAvailable)
        {
            ClearPendingRoomSelection();
            return;
        }

        if (!inputProvider.TryGetPointerScreenPosition(out Vector2 mousePosition))
        {
            ClearPendingRoomSelection();
            return;
        }

        float thresholdSqr = clickToSelectThresholdPixels * clickToSelectThresholdPixels;
        float movedSqr = (mousePosition - pendingSelectionStartMousePosition).sqrMagnitude;

        if (inputProvider.WasPointerButtonReleasedThisFrame(PointerButton.Left))
        {
            FocusRoomForEditing(pendingSelectedRoom);
            ClearPendingRoomSelection();
            return;
        }

        if (!inputProvider.IsPointerButtonPressed(PointerButton.Left))
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
