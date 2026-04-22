using System;
using UnityEngine;

internal sealed class WallSelectionInputController
{
    public void HandleDefaultModeInput(
        EditorPointerFrame pointerFrame,
        GameObject selectedWall,
        bool isWallCreationMode,
        bool isHandleDragging,
        bool pendingWallDrag,
        bool isDraggingWall,
        float dragStartThresholdPixels,
        Vector2 pendingStartMousePosition,
        Vector3 dragStartPoint,
        Vector3 selectedWallStartPosition,
        bool snapDraggedWallToGrid,
        Func<Vector2, (bool success, Vector3 point)> tryGetMouseWorldPoint,
        Func<Vector3, Vector3> snapDraggedWallPosition,
        Func<Vector3, Vector3> clampDraggedWallPosition,
        Action finalizeMoveIfNeeded,
        Action clearSingleSelection,
        Action resetDragState,
        Action<bool> setSelectUIVisible,
        Action captureMoveStartState,
        Action prepareConnectedWallDrag,
        Action<Vector3, Vector3> applyConnectedWallDrag,
        Action beginDraggingWall)
    {
        if (!pointerFrame.IsAvailable)
        {
            return;
        }

        if (selectedWall != null && pointerFrame.RightPressedThisFrame && !isWallCreationMode)
        {
            finalizeMoveIfNeeded?.Invoke();
            clearSingleSelection?.Invoke();
            resetDragState?.Invoke();
            return;
        }

        if (selectedWall == null)
        {
            clearSingleSelection?.Invoke();
            resetDragState?.Invoke();
            return;
        }

        setSelectUIVisible?.Invoke(false);

        if (pendingWallDrag && !pointerFrame.LeftPressed)
        {
            finalizeMoveIfNeeded?.Invoke();
            resetDragState?.Invoke();
            return;
        }

        if (isWallCreationMode || isHandleDragging)
        {
            finalizeMoveIfNeeded?.Invoke();
            resetDragState?.Invoke();
            return;
        }

        if (pendingWallDrag && !isDraggingWall)
        {
            Vector2 currentMouse = pointerFrame.ScreenPosition;
            float movedSqr = (currentMouse - pendingStartMousePosition).sqrMagnitude;
            float thresholdSqr = dragStartThresholdPixels * dragStartThresholdPixels;
            if (movedSqr >= thresholdSqr)
            {
                captureMoveStartState?.Invoke();
                prepareConnectedWallDrag?.Invoke();
                beginDraggingWall?.Invoke();
                isDraggingWall = true;
            }
        }

        if (!isDraggingWall)
        {
            return;
        }

        if (tryGetMouseWorldPoint == null)
        {
            return;
        }

        (bool success, Vector3 currentPoint) = tryGetMouseWorldPoint(pointerFrame.ScreenPosition);
        if (!success)
        {
            return;
        }

        Vector3 delta = currentPoint - dragStartPoint;
        Vector3 targetPosition = selectedWallStartPosition + new Vector3(delta.x, 0f, delta.z);

        if (snapDraggedWallToGrid && snapDraggedWallPosition != null)
        {
            targetPosition = snapDraggedWallPosition(targetPosition);
        }

        if (clampDraggedWallPosition != null)
        {
            targetPosition = clampDraggedWallPosition(targetPosition);
        }

        Vector3 targetWallPosition = new Vector3(targetPosition.x, selectedWallStartPosition.y, targetPosition.z);
        Vector3 translationDelta = targetWallPosition - selectedWallStartPosition;
        translationDelta.y = 0f;

        applyConnectedWallDrag?.Invoke(translationDelta, targetWallPosition);
    }

    public bool TryConsumeIdleLeftPress(
        EditorPointerFrame pointerFrame,
        bool isDefaultMode,
        bool isPointerOverUI,
        bool isWallCreationMode,
        bool isHandleDragging,
        bool isPointerOverHandle,
        Func<Vector2, (bool success, GameObject wall)> tryGetWallFromMouseRay,
        Func<Vector2, (bool success, Vector3 point)> tryGetMouseWorldPoint,
        Action<GameObject> selectWall,
        Action resetDragState,
        Action<bool> setSelectUIVisible,
        Action<Vector2, Vector3> beginPendingWallDrag)
    {
        if (!pointerFrame.IsAvailable || !isDefaultMode || isPointerOverUI || isWallCreationMode || isHandleDragging || isPointerOverHandle)
        {
            return false;
        }

        if (tryGetWallFromMouseRay == null)
        {
            return false;
        }

        (bool hitSuccess, GameObject hitWall) = tryGetWallFromMouseRay(pointerFrame.ScreenPosition);
        if (!hitSuccess)
        {
            return false;
        }

        selectWall?.Invoke(hitWall);
        resetDragState?.Invoke();
        setSelectUIVisible?.Invoke(false);

        if (tryGetMouseWorldPoint != null)
        {
            (bool pointSuccess, Vector3 worldPoint) = tryGetMouseWorldPoint(pointerFrame.ScreenPosition);
            if (pointSuccess)
            {
                beginPendingWallDrag?.Invoke(pointerFrame.ScreenPosition, worldPoint);
            }
        }

        return true;
    }

    public void UpdateDetailEditMode(
        EditorPointerFrame pointerFrame,
        WallSelectionInputState inputState,
        float multiSelectDragThresholdPixels,
        Action finalizeMoveIfNeeded,
        Action resetDragState,
        Action refreshWallSelectionUIPositions,
        Action clearAllSelectionState,
        Action<EditorPointerFrame> tryBeginDetailSelection,
        Action<EditorPointerFrame> updateMultiSelectDrag,
        Action finishMultiSelectDrag)
    {
        finalizeMoveIfNeeded?.Invoke();
        resetDragState?.Invoke();
        refreshWallSelectionUIPositions?.Invoke();

        if (pointerFrame.RightPressedThisFrame)
        {
            clearAllSelectionState?.Invoke();
            return;
        }

        if (pointerFrame.LeftPressedThisFrame)
        {
            tryBeginDetailSelection?.Invoke(pointerFrame);
        }

        if (inputState.PendingMultiSelectDrag && pointerFrame.LeftPressed)
        {
            Vector2 currentMouse = pointerFrame.ScreenPosition;
            float movedSqr = (currentMouse - inputState.MultiSelectStartMousePosition).sqrMagnitude;
            if (movedSqr >= multiSelectDragThresholdPixels * multiSelectDragThresholdPixels)
            {
                inputState.PendingMultiSelectDrag = false;
                inputState.IsMultiSelecting = true;
                updateMultiSelectDrag?.Invoke(pointerFrame);
            }
        }

        if (inputState.IsMultiSelecting && pointerFrame.LeftPressed)
        {
            updateMultiSelectDrag?.Invoke(pointerFrame);
        }

        if ((inputState.PendingMultiSelectDrag || inputState.IsMultiSelecting) && pointerFrame.LeftReleasedThisFrame)
        {
            finishMultiSelectDrag?.Invoke();
        }
    }

    public void BeginDetailSelectionBox(
        WallSelectionInputState inputState,
        EditorPointerFrame pointerFrame,
        bool addToSelectionOnDragStart,
        Vector3 worldPoint,
        Action showMultiSelectBox,
        Action<Vector3, Vector3> updateMultiSelectBox)
    {
        inputState.AddToSelectionOnDragStart = addToSelectionOnDragStart;
        inputState.PendingMultiSelectDrag = true;
        inputState.IsMultiSelecting = false;
        inputState.MultiSelectStartMousePosition = pointerFrame.ScreenPosition;
        inputState.MultiSelectStartWorldPoint = worldPoint;
        showMultiSelectBox?.Invoke();
        updateMultiSelectBox?.Invoke(inputState.MultiSelectStartWorldPoint, inputState.MultiSelectStartWorldPoint);
    }

    public void UpdateMultiSelectDrag(
        WallSelectionInputState inputState,
        EditorPointerFrame pointerFrame,
        Func<Vector2, (bool success, Vector3 point)> tryGetMouseWorldPoint,
        Action<Vector3, Vector3> updateMultiSelectBox,
        Action<bool> updateWallsFromMultiSelectBox)
    {
        if (inputState == null || tryGetMouseWorldPoint == null)
        {
            return;
        }

        (bool success, Vector3 currentWorldPoint) = tryGetMouseWorldPoint(pointerFrame.ScreenPosition);
        if (!success)
        {
            return;
        }

        updateMultiSelectBox?.Invoke(inputState.MultiSelectStartWorldPoint, currentWorldPoint);
        updateWallsFromMultiSelectBox?.Invoke(inputState.AddToSelectionOnDragStart);
    }

    public void FinishMultiSelectDrag(
        WallSelectionInputState inputState,
        Action hideMultiSelectBox,
        Action updateSelectUIVisibility)
    {
        bool hadDrag = inputState != null && inputState.IsMultiSelecting;
        inputState?.ResetMultiSelect();
        hideMultiSelectBox?.Invoke();

        if (!hadDrag)
        {
            return;
        }

        updateSelectUIVisibility?.Invoke();
    }

    public void CancelMultiSelectDrag(
        WallSelectionInputState inputState,
        Action hideMultiSelectBox)
    {
        inputState?.ResetMultiSelect();
        hideMultiSelectBox?.Invoke();
    }
}
