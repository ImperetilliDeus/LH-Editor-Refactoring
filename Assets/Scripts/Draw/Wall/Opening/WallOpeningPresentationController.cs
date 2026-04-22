internal sealed class WallOpeningPresentationController
{
    public void RefreshOpeningDetailInputs(
        DoorOpeningUIController doorUIController,
        WindowOpeningUIController windowUIController,
        WallOpening selectedOpening,
        float defaultDoorWidthMillimeters,
        float defaultDoorHeightMillimeters,
        float defaultDoorDepthMillimeters,
        float defaultDoorBottomOffsetMillimeters,
        float defaultWindowWidthMillimeters,
        float defaultWindowHeightMillimeters,
        float defaultWindowDepthMillimeters,
        float defaultWindowBottomOffsetMillimeters,
        bool force)
    {
        doorUIController?.Refresh(
            selectedOpening,
            defaultDoorWidthMillimeters,
            defaultDoorHeightMillimeters,
            defaultDoorDepthMillimeters,
            defaultDoorBottomOffsetMillimeters,
            force);
        windowUIController?.Refresh(
            selectedOpening,
            defaultWindowWidthMillimeters,
            defaultWindowHeightMillimeters,
            defaultWindowDepthMillimeters,
            defaultWindowBottomOffsetMillimeters,
            force);
    }

    public void SetOpeningDetailMenuVisible(
        DoorOpeningUIController doorUIController,
        WindowOpeningUIController windowUIController,
        WallOpening selectedOpening,
        WallOpeningPlacementManager.OpeningPlacementType doorType,
        bool visible)
    {
        if (selectedOpening == null)
        {
            doorUIController?.SetVisible(false);
            windowUIController?.SetVisible(false);
            return;
        }

        bool showDoorMenu = visible && selectedOpening.Type == doorType;
        bool showWindowMenu = visible && selectedOpening.Type != doorType;
        doorUIController?.SetVisible(showDoorMenu);
        windowUIController?.SetVisible(showWindowMenu);
    }

    public bool IsCurrentDetailMenuActive(
        DoorOpeningUIController doorUIController,
        WindowOpeningUIController windowUIController,
        WallOpening selectedOpening,
        WallOpeningPlacementManager.OpeningPlacementType doorType)
    {
        if (selectedOpening == null)
        {
            return false;
        }

        if (selectedOpening.Type == doorType)
        {
            return doorUIController == null || doorUIController.IsMenuVisible;
        }

        return windowUIController == null || windowUIController.IsMenuVisible;
    }
}
