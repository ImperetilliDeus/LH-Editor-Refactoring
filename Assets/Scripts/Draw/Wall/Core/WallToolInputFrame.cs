using UnityEngine;

internal readonly struct WallToolInputFrame
{
    public static readonly WallToolInputFrame Unavailable = new WallToolInputFrame(false);

    public WallToolInputFrame(
        Vector2 pointerScreenPosition,
        bool leftPressedThisFrame,
        bool leftReleasedThisFrame,
        bool leftPressed,
        bool rightPressedThisFrame,
        bool deletePressedThisFrame,
        bool pointerOverUI)
    {
        IsAvailable = true;
        PointerScreenPosition = pointerScreenPosition;
        LeftPressedThisFrame = leftPressedThisFrame;
        LeftReleasedThisFrame = leftReleasedThisFrame;
        LeftPressed = leftPressed;
        RightPressedThisFrame = rightPressedThisFrame;
        DeletePressedThisFrame = deletePressedThisFrame;
        PointerOverUI = pointerOverUI;
    }

    private WallToolInputFrame(bool isAvailable)
    {
        IsAvailable = isAvailable;
        PointerScreenPosition = Vector2.zero;
        LeftPressedThisFrame = false;
        LeftReleasedThisFrame = false;
        LeftPressed = false;
        RightPressedThisFrame = false;
        DeletePressedThisFrame = false;
        PointerOverUI = false;
    }

    public bool IsAvailable { get; }
    public Vector2 PointerScreenPosition { get; }
    public bool LeftPressedThisFrame { get; }
    public bool LeftReleasedThisFrame { get; }
    public bool LeftPressed { get; }
    public bool RightPressedThisFrame { get; }
    public bool DeletePressedThisFrame { get; }
    public bool PointerOverUI { get; }
}
