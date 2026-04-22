internal static class PointerInputFrameUtility
{
    public static EditorPointerFrame BuildPointerFrame(EditorInputFrame inputFrame)
    {
        return inputFrame.IsPointerAvailable
            ? new EditorPointerFrame(
                inputFrame.PointerScreenPosition,
                inputFrame.LeftPressedThisFrame,
                inputFrame.LeftReleasedThisFrame,
                inputFrame.LeftPressed,
                inputFrame.RightPressedThisFrame)
            : EditorPointerFrame.Unavailable;
    }

}
