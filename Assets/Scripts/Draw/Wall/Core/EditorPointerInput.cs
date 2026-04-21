using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

internal static class EditorPointerInput
{
    public static bool IsPointerAvailable => Mouse.current != null;

    public static bool TryGetCurrentFrame(out EditorPointerFrame pointerFrame)
    {
        if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            pointerFrame = EditorPointerFrame.Unavailable;
            return false;
        }

        pointerFrame = new EditorPointerFrame(
            pointerScreenPosition,
            IsLeftPressedThisFrame,
            IsLeftReleasedThisFrame,
            IsLeftPressed,
            IsRightPressedThisFrame);
        return true;
    }

    public static bool TryGetPointerScreenPosition(out Vector2 pointerScreenPosition)
    {
        pointerScreenPosition = Vector2.zero;
        if (Mouse.current == null)
        {
            return false;
        }

        pointerScreenPosition = Mouse.current.position.ReadValue();
        return true;
    }

    public static Vector2 GetPointerScreenPositionOrDefault()
    {
        return TryGetPointerScreenPosition(out Vector2 pointerScreenPosition)
            ? pointerScreenPosition
            : Vector2.zero;
    }

    public static bool IsLeftPressedThisFrame => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public static bool IsLeftReleasedThisFrame => Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
    public static bool IsLeftPressed => Mouse.current != null && Mouse.current.leftButton.isPressed;
    public static bool IsRightPressedThisFrame => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

    public static bool TryIsPointerOverUI(EventSystem eventSystem)
    {
        if (eventSystem == null || Mouse.current == null)
        {
            return false;
        }

        return eventSystem.IsPointerOverGameObject(Mouse.current.deviceId);
    }
}
