using UnityEngine;
using UnityEngine.InputSystem;

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
        bool polygonModifierPressed = IsPolygonCreationModifierPressed();
        bool isPointerOverRoomHandle = hasPointerPosition &&
                                       roomHandleManager != null &&
                                       roomHandleManager.IsPointerOverHandle(pointerScreenPosition);

        if (inputFrame.RightPressedThisFrame || inputFrame.EscapePressedThisFrame)
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

        if (IsPolygonDrawMode())
        {
            HandlePolygonDrawInput(inputFrame, isPointerOverUI, isPointerOverRoomHandle, hasPointerPosition, pointerScreenPosition, polygonModifierPressed);
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

                if (!hasPointerPosition || !IsRoomCreateDoubleClick(pointerScreenPosition))
                {
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

    private void HandlePolygonDrawInput(
        EditorInputFrame inputFrame,
        bool isPointerOverUI,
        bool isPointerOverRoomHandle,
        bool hasPointerPosition,
        Vector2 pointerScreenPosition,
        bool polygonModifierPressed)
    {
        if (isDrawingPolygon)
        {
            UpdatePolygonPreviewWhileDrawing();

            if ((Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) ||
                (Keyboard.current != null && Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                TryCompletePolygonDraw();
                return;
            }

            if (isPointerOverUI || isPointerOverRoomHandle || !inputFrame.LeftPressedThisFrame)
            {
                return;
            }

            if (IsPointerNearPolygonStart(pointerScreenPosition))
            {
                TryCompletePolygonDraw();
                return;
            }

            Vector3 snapAnchor = polygonDraftVertices.Count > 0
                ? polygonDraftVertices[polygonDraftVertices.Count - 1]
                : Vector3.zero;
            if (TryGetMouseWorldPoint(out Vector3 nextPoint, polygonDraftVertices.Count > 0 ? (Vector3?)snapAnchor : null))
            {
                AppendPolygonVertex(nextPoint);
            }

            return;
        }

        if (pendingRoomSelection)
        {
            HandlePendingRoomSelection(inputFrame);
            return;
        }

        if (isPointerOverUI || isPointerOverRoomHandle || !inputFrame.LeftPressedThisFrame)
        {
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 startPoint))
        {
            return;
        }

        if (!polygonModifierPressed)
        {
            pendingSelectedRoom = PickRoomAtWorldPoint(startPoint);
            if (pendingSelectedRoom != null)
            {
                pendingRoomSelection = true;
                pendingSelectionStartPoint = startPoint;
                pendingSelectionStartMousePosition = hasPointerPosition ? pointerScreenPosition : Vector2.zero;
                return;
            }
        }

        if (!hasPointerPosition || !IsRoomCreateDoubleClick(pointerScreenPosition))
        {
            return;
        }

        BeginPolygonDraw(startPoint);
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

    private void UpdatePolygonPreviewWhileDrawing()
    {
        if (polygonDraftVertices.Count == 0)
        {
            HidePreviewObjects();
            return;
        }

        Vector3 snapAnchor = polygonDraftVertices[polygonDraftVertices.Count - 1];
        if (!TryGetMouseWorldPoint(out Vector3 currentPoint, snapAnchor))
        {
            hasPolygonHoverPoint = false;
            UpdatePreviewFromPolygonDraft();
            return;
        }

        UpdatePolygonHoverPoint(currentPoint);
        UpdatePreviewFromPolygonDraft();
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

        if (IsPolygonDrawMode())
        {
            BeginPolygonDraw(startPoint);
            return;
        }

        BeginRectangleDrag(startPoint);
    }

    private bool IsRoomCreateDoubleClick(Vector2 pointerScreenPosition)
    {
        float currentTime = Time.unscaledTime;
        float threshold = Mathf.Max(0.05f, doubleClickThreshold);
        float distanceThreshold = Mathf.Max(0f, clickToSelectThresholdPixels);

        bool isDoubleClick = lastLeftClickTime >= 0f &&
                             currentTime - lastLeftClickTime <= threshold &&
                             (pointerScreenPosition - lastLeftClickPosition).sqrMagnitude <=
                             distanceThreshold * distanceThreshold;

        lastLeftClickTime = currentTime;
        lastLeftClickPosition = pointerScreenPosition;

        return isDoubleClick;
    }
}
