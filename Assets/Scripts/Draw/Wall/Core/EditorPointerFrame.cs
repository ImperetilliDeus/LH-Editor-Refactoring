using UnityEngine;

internal readonly struct EditorPointerFrame
{
    public static readonly EditorPointerFrame Unavailable = new EditorPointerFrame(false);

    public EditorPointerFrame(
        Vector2 screenPosition,
        bool leftPressedThisFrame,
        bool leftReleasedThisFrame,
        bool leftPressed,
        bool middlePressedThisFrame,
        bool middleReleasedThisFrame,
        bool middlePressed,
        bool rightPressedThisFrame)
    {
        IsAvailable = true;
        ScreenPosition = screenPosition;
        LeftPressedThisFrame = leftPressedThisFrame;
        LeftReleasedThisFrame = leftReleasedThisFrame;
        LeftPressed = leftPressed;
        MiddlePressedThisFrame = middlePressedThisFrame;
        MiddleReleasedThisFrame = middleReleasedThisFrame;
        MiddlePressed = middlePressed;
        RightPressedThisFrame = rightPressedThisFrame;
    }

    private EditorPointerFrame(bool isAvailable)
    {
        IsAvailable = isAvailable;
        ScreenPosition = Vector2.zero;
        LeftPressedThisFrame = false;
        LeftReleasedThisFrame = false;
        LeftPressed = false;
        MiddlePressedThisFrame = false;
        MiddleReleasedThisFrame = false;
        MiddlePressed = false;
        RightPressedThisFrame = false;
    }

    public bool IsAvailable { get; }
    public Vector2 ScreenPosition { get; }
    public bool LeftPressedThisFrame { get; }
    public bool LeftReleasedThisFrame { get; }
    public bool LeftPressed { get; }
    public bool MiddlePressedThisFrame { get; }
    public bool MiddleReleasedThisFrame { get; }
    public bool MiddlePressed { get; }
    public bool RightPressedThisFrame { get; }
}
