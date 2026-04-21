using UnityEngine;

public readonly struct EditorInputFrame
{
    public static readonly EditorInputFrame Unavailable = new EditorInputFrame(EditorMode.Default);

    public EditorInputFrame(
        EditorMode mode,
        bool isPointerAvailable,
        Vector2 pointerScreenPosition,
        bool pointerOverUI,
        bool leftPressedThisFrame,
        bool leftReleasedThisFrame,
        bool leftPressed,
        bool middlePressedThisFrame,
        bool middleReleasedThisFrame,
        bool middlePressed,
        bool rightPressedThisFrame,
        bool rightPressed,
        Vector2 pointerDelta,
        float scrollDeltaY,
        bool deletePressedThisFrame,
        bool escapePressedThisFrame,
        bool rotateNegativePressedThisFrame,
        bool rotateNegativePressed,
        bool rotatePositivePressedThisFrame,
        bool rotatePositivePressed)
    {
        Mode = mode;
        IsPointerAvailable = isPointerAvailable;
        PointerScreenPosition = pointerScreenPosition;
        PointerOverUI = pointerOverUI;
        LeftPressedThisFrame = leftPressedThisFrame;
        LeftReleasedThisFrame = leftReleasedThisFrame;
        LeftPressed = leftPressed;
        MiddlePressedThisFrame = middlePressedThisFrame;
        MiddleReleasedThisFrame = middleReleasedThisFrame;
        MiddlePressed = middlePressed;
        RightPressedThisFrame = rightPressedThisFrame;
        RightPressed = rightPressed;
        PointerDelta = pointerDelta;
        ScrollDeltaY = scrollDeltaY;
        DeletePressedThisFrame = deletePressedThisFrame;
        EscapePressedThisFrame = escapePressedThisFrame;
        RotateNegativePressedThisFrame = rotateNegativePressedThisFrame;
        RotateNegativePressed = rotateNegativePressed;
        RotatePositivePressedThisFrame = rotatePositivePressedThisFrame;
        RotatePositivePressed = rotatePositivePressed;
    }

    private EditorInputFrame(EditorMode mode)
    {
        Mode = mode;
        IsPointerAvailable = false;
        PointerScreenPosition = Vector2.zero;
        PointerOverUI = false;
        LeftPressedThisFrame = false;
        LeftReleasedThisFrame = false;
        LeftPressed = false;
        MiddlePressedThisFrame = false;
        MiddleReleasedThisFrame = false;
        MiddlePressed = false;
        RightPressedThisFrame = false;
        RightPressed = false;
        PointerDelta = Vector2.zero;
        ScrollDeltaY = 0f;
        DeletePressedThisFrame = false;
        EscapePressedThisFrame = false;
        RotateNegativePressedThisFrame = false;
        RotateNegativePressed = false;
        RotatePositivePressedThisFrame = false;
        RotatePositivePressed = false;
    }

    public EditorMode Mode { get; }
    public bool IsPointerAvailable { get; }
    public Vector2 PointerScreenPosition { get; }
    public bool PointerOverUI { get; }
    public bool LeftPressedThisFrame { get; }
    public bool LeftReleasedThisFrame { get; }
    public bool LeftPressed { get; }
    public bool MiddlePressedThisFrame { get; }
    public bool MiddleReleasedThisFrame { get; }
    public bool MiddlePressed { get; }
    public bool RightPressedThisFrame { get; }
    public bool RightPressed { get; }
    public Vector2 PointerDelta { get; }
    public float ScrollDeltaY { get; }
    public bool DeletePressedThisFrame { get; }
    public bool EscapePressedThisFrame { get; }
    public bool RotateNegativePressedThisFrame { get; }
    public bool RotateNegativePressed { get; }
    public bool RotatePositivePressedThisFrame { get; }
    public bool RotatePositivePressed { get; }
}
