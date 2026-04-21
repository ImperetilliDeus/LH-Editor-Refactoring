internal static class PointerInputFrameUtility
{
    public static bool TryBuildPointerFrame(IEditorInputProvider inputProvider, out EditorPointerFrame pointerFrame)
    {
        pointerFrame = EditorPointerFrame.Unavailable;
        if (inputProvider == null || !inputProvider.IsPointerAvailable)
        {
            return false;
        }

        if (!inputProvider.TryGetPointerScreenPosition(out UnityEngine.Vector2 pointerScreenPosition))
        {
            return false;
        }

        pointerFrame = new EditorPointerFrame(
            pointerScreenPosition,
            inputProvider.WasPointerButtonPressedThisFrame(PointerButton.Left),
            inputProvider.WasPointerButtonReleasedThisFrame(PointerButton.Left),
            inputProvider.IsPointerButtonPressed(PointerButton.Left),
            inputProvider.WasPointerButtonPressedThisFrame(PointerButton.Right));
        return true;
    }

    public static bool TryBuildWallToolInputFrame(
        IEditorInputProvider inputProvider,
        bool pointerOverUI,
        out WallToolInputFrame inputFrame)
    {
        inputFrame = WallToolInputFrame.Unavailable;
        if (!TryBuildPointerFrame(inputProvider, out EditorPointerFrame pointerFrame))
        {
            return false;
        }

        inputFrame = new WallToolInputFrame(
            pointerFrame.ScreenPosition,
            pointerFrame.LeftPressedThisFrame,
            pointerFrame.LeftReleasedThisFrame,
            pointerFrame.LeftPressed,
            pointerFrame.RightPressedThisFrame,
            pointerOverUI);
        return true;
    }
}
