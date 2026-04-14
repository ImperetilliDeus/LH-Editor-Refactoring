using UnityEngine;

public partial class WallOpeningPlacementManager
{
    private bool CanEditOpenings()
    {
        return modeManager != null && modeManager.IsMode(EditorMode.DetailEdit);
    }

    private Wall GetSelectedWallComponent()
    {
        if (wallSelectionManager == null || wallSelectionManager.SelectedWall == null)
        {
            return null;
        }

        return wallSelectionManager.SelectedWall.GetComponent<Wall>();
    }

    public void ApplySelectedDoorWidthFromInput(string inputText)
    {
        ApplySelectedOpeningDimensionFromInput(
            inputText,
            OpeningPlacementType.Door,
            value => defaultDoorWidthMillimeters = value,
            (opening, container, valueInUnits) =>
            {
                float clampedWidth = ClampOpeningWidth(container, opening, valueInUnits);
                if (clampedWidth <= MinimumWallSegmentLength)
                {
                    return false;
                }

                opening.SetWidth(clampedWidth);
                return true;
            });
    }

    public void ApplySelectedDoorHeightFromInput(string inputText)
    {
        ApplySelectedOpeningDimensionFromInput(
            inputText,
            OpeningPlacementType.Door,
            value => defaultDoorHeightMillimeters = value,
            (opening, container, valueInUnits) =>
            {
                float clampedHeight = ClampOpeningHeight(container, opening, valueInUnits, opening.BottomY);
                if (clampedHeight <= MinimumWallSegmentLength)
                {
                    return false;
                }

                opening.SetHeight(clampedHeight);
                return true;
            });
    }

    public void ApplySelectedWindowWidthFromInput(string inputText)
    {
        ApplySelectedOpeningDimensionFromInput(
            inputText,
            OpeningPlacementType.Window,
            value => defaultWindowWidthMillimeters = value,
            (opening, container, valueInUnits) =>
            {
                float clampedWidth = ClampOpeningWidth(container, opening, valueInUnits);
                if (clampedWidth <= MinimumWallSegmentLength)
                {
                    return false;
                }

                opening.SetWidth(clampedWidth);
                return true;
            });
    }

    public void ApplySelectedWindowHeightFromInput(string inputText)
    {
        ApplySelectedOpeningDimensionFromInput(
            inputText,
            OpeningPlacementType.Window,
            value => defaultWindowHeightMillimeters = value,
            (opening, container, valueInUnits) =>
            {
                float clampedHeight = ClampOpeningHeight(container, opening, valueInUnits, opening.BottomY);
                if (clampedHeight <= MinimumWallSegmentLength)
                {
                    return false;
                }

                opening.SetHeight(clampedHeight);
                return true;
            });
    }

    public void ApplySelectedDoorDepthFromInput(string inputText)
    {
        ApplySelectedOpeningDepthFromInput(inputText, OpeningPlacementType.Door, value => defaultDoorDepthMillimeters = value);
    }

    public void ApplySelectedWindowDepthFromInput(string inputText)
    {
        ApplySelectedOpeningDepthFromInput(inputText, OpeningPlacementType.Window, value => defaultWindowDepthMillimeters = value);
    }

    private void ApplySelectedOpeningDepthFromInput(
        string inputText,
        OpeningPlacementType requiredType,
        System.Action<float> updateDefaultValue)
    {
        if (!TryParsePositiveMillimeters(inputText, out float millimeters))
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        if (selectedOpening == null || selectedOpening.Container == null || selectedOpening.Type != requiredType)
        {
            updateDefaultValue?.Invoke(millimeters);
            RefreshOpeningDetailInputs(true);
            return;
        }

        float clampedMillimeters = Mathf.Min(millimeters, UnitsToMillimeters(selectedOpening.Container.WallThickness));
        updateDefaultValue?.Invoke(clampedMillimeters);
        selectedOpening.SetDepth(MillimetersToUnits(clampedMillimeters));
        RebuildSelectedOpeningWithUndo();
    }

    public void ApplySelectedDoorBottomOffsetFromInput(string inputText)
    {
        ApplySelectedOpeningBottomOffsetFromInput(inputText, OpeningPlacementType.Door);
    }

    public void ApplySelectedWindowBottomOffsetFromInput(string inputText)
    {
        ApplySelectedOpeningBottomOffsetFromInput(inputText, OpeningPlacementType.Window);
    }

    public void ApplySelectedDoorTypeFromDropdown(int optionIndex)
    {
        if (selectedOpening == null || selectedOpening.Type != OpeningPlacementType.Door)
        {
            return;
        }

        string nextTypeKey = GetDoorTypeKeyForOption(optionIndex);
        selectedOpening.SetDoorTypeKey(nextTypeKey);
        RebuildSelectedOpeningWithUndo();
    }

    public void ApplySelectedDoorSwingDirection(bool opensRight)
    {
        if (selectedOpening == null || selectedOpening.Type != OpeningPlacementType.Door)
        {
            return;
        }

        selectedOpening.SetDoorOpensRight(opensRight);
        RebuildSelectedOpeningWithUndo();
    }

    public void ApplySelectedDoorVerticalFlip(bool verticalFlip)
    {
        if (selectedOpening == null || selectedOpening.Type != OpeningPlacementType.Door)
        {
            return;
        }

        selectedOpening.SetDoorVerticalFlip(verticalFlip);
        RebuildSelectedOpeningWithUndo();
    }

    public void ApplySelectedWindowTypeFromDropdown(int optionIndex)
    {
        if (selectedOpening == null || selectedOpening.Type != OpeningPlacementType.Window)
        {
            return;
        }

        string nextTypeKey = GetWindowTypeKeyForOption(optionIndex);
        selectedOpening.SetWindowTypeKey(nextTypeKey);
        RebuildSelectedOpeningWithUndo();
    }

    private void ApplySelectedOpeningDimensionFromInput(
        string inputText,
        OpeningPlacementType? requiredType,
        System.Action<float> updateDefaultValue,
        System.Func<WallOpening, WallOpeningContainer, float, bool> applyToOpening)
    {
        if (!TryParsePositiveMillimeters(inputText, out float millimeters))
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        updateDefaultValue?.Invoke(millimeters);

        if (selectedOpening == null || (requiredType.HasValue && selectedOpening.Type != requiredType.Value))
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        WallOpeningContainer container = selectedOpening.Container;
        if (container == null || applyToOpening == null)
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        float units = MillimetersToUnits(millimeters);
        if (!applyToOpening(selectedOpening, container, units))
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        RebuildSelectedOpeningWithUndo();
    }

    private void ApplySelectedOpeningBottomOffsetFromInput(string inputText, OpeningPlacementType requiredType)
    {
        if (!TryParseMillimeters(inputText, out float millimeters))
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        if (requiredType == OpeningPlacementType.Door)
        {
            defaultDoorBottomOffsetMillimeters = millimeters;
        }
        else
        {
            defaultWindowBottomOffsetMillimeters = millimeters;
        }

        if (selectedOpening == null || selectedOpening.Type != requiredType)
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        WallOpeningContainer container = selectedOpening.Container;
        if (container == null)
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        float bottomY = container.WallBottomY + MillimetersToUnits(millimeters);
        bottomY = ClampOpeningBottomY(container, selectedOpening, bottomY);
        selectedOpening.SetBottomY(bottomY);
        float clampedHeight = ClampOpeningHeight(container, selectedOpening, selectedOpening.Height, bottomY);
        selectedOpening.SetHeight(clampedHeight);
        RebuildSelectedOpeningWithUndo();
    }

    private void RebuildSelectedOpeningWithUndo()
    {
        if (selectedOpening == null || selectedOpening.Container == null)
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = CaptureLayoutSnapshot(selectedOpening.Container);
        RebuildContainer(selectedOpening.Container);
        RefreshSelectedWallForContainer(selectedOpening.Container, selectedOpening.CenterDistance);
        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, CaptureLayoutSnapshot(selectedOpening.Container));
        }

        RefreshOpeningDetailInputs(true);
    }

    private void RefreshOpeningDetailInputs(bool force)
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

    private void BindButtons()
    {
        if (addDoorButton != null)
        {
            addDoorButton.onClick.AddListener(CreateDoorOnSelectedWall);
        }

        if (addWindowButton != null)
        {
            addWindowButton.onClick.AddListener(CreateWindowOnSelectedWall);
        }

        if (addSplitPointButton != null)
        {
            addSplitPointButton.onClick.AddListener(SplitSelectedWall);
        }
    }

    private void UnbindButtons()
    {
        if (addDoorButton != null)
        {
            addDoorButton.onClick.RemoveListener(CreateDoorOnSelectedWall);
        }

        if (addWindowButton != null)
        {
            addWindowButton.onClick.RemoveListener(CreateWindowOnSelectedWall);
        }

        if (addSplitPointButton != null)
        {
            addSplitPointButton.onClick.RemoveListener(SplitSelectedWall);
        }
    }

    private void SetOpeningDetailMenuVisible(bool visible)
    {
        if (selectedOpening == null)
        {
            doorUIController?.SetVisible(false);
            windowUIController?.SetVisible(false);
            return;
        }

        bool showDoorMenu = visible && selectedOpening.Type == OpeningPlacementType.Door;
        bool showWindowMenu = visible && selectedOpening.Type == OpeningPlacementType.Window;
        doorUIController?.SetVisible(showDoorMenu);
        windowUIController?.SetVisible(showWindowMenu);
    }

    private bool IsCurrentDetailMenuActive()
    {
        if (selectedOpening == null)
        {
            return false;
        }

        if (selectedOpening.Type == OpeningPlacementType.Door)
        {
            return doorUIController == null || doorUIController.IsMenuVisible;
        }

        return windowUIController == null || windowUIController.IsMenuVisible;
    }

    public int GetDoorTypeOptionIndex(string doorTypeKey)
    {
        return doorUIController != null ? doorUIController.FindDoorTypeOptionIndex(doorTypeKey) : -1;
    }

    public int GetWindowTypeOptionIndex(string windowTypeKey)
    {
        return windowUIController != null ? windowUIController.FindWindowTypeOptionIndex(windowTypeKey) : -1;
    }

    private string GetCurrentDoorTypeKey()
    {
        return doorUIController != null ? doorUIController.GetCurrentDoorTypeKey() : string.Empty;
    }

    private string GetCurrentWindowTypeKey()
    {
        return windowUIController != null ? windowUIController.GetCurrentWindowTypeKey() : string.Empty;
    }

    private string GetDoorTypeKeyForOption(int optionIndex)
    {
        return doorUIController != null ? doorUIController.GetDoorTypeKeyForOption(optionIndex) : string.Empty;
    }

    private string GetWindowTypeKeyForOption(int optionIndex)
    {
        return windowUIController != null ? windowUIController.GetWindowTypeKeyForOption(optionIndex) : string.Empty;
    }

    private void RefreshOpeningMarkerVisuals()
    {
        if (markerUIs.Count == 0)
        {
            return;
        }

        removedMarkerUIs.Clear();
        foreach (WallOpeningMarkerUI markerUI in markerUIs)
        {
            if (markerUI == null)
            {
                removedMarkerUIs.Add(markerUI);
                continue;
            }

            markerUI.RefreshVisual();
        }

        for (int i = 0; i < removedMarkerUIs.Count; i++)
        {
            markerUIs.Remove(removedMarkerUIs[i]);
        }
    }

    private void CacheCameraState()
    {
        if (mainCamera == null)
        {
            return;
        }

        Transform cameraTransform = mainCamera.transform;
        lastCameraPosition = cameraTransform.position;
        lastCameraRotation = cameraTransform.rotation;
        lastCameraOrthoSize = mainCamera.orthographicSize;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        if (previewCanvas != null)
        {
            lastCanvasScaleFactor = previewCanvas.scaleFactor;
            lastPreviewCanvasWorldCamera = previewCanvas.worldCamera;
        }
        else
        {
            lastCanvasScaleFactor = 0f;
            lastPreviewCanvasWorldCamera = null;
        }
    }

    private bool HasCameraStateChanged()
    {
        bool cameraChanged = false;
        if (mainCamera != null)
        {
            Transform cameraTransform = mainCamera.transform;
            cameraChanged = cameraTransform.position != lastCameraPosition ||
                            cameraTransform.rotation != lastCameraRotation ||
                            !Mathf.Approximately(mainCamera.orthographicSize, lastCameraOrthoSize);
        }

        bool screenChanged = Screen.width != lastScreenWidth || Screen.height != lastScreenHeight;

        float currentCanvasScale = previewCanvas != null ? previewCanvas.scaleFactor : 0f;
        Camera currentCanvasCamera = previewCanvas != null ? previewCanvas.worldCamera : null;
        bool canvasChanged = !Mathf.Approximately(currentCanvasScale, lastCanvasScaleFactor) ||
                             currentCanvasCamera != lastPreviewCanvasWorldCamera;

        return cameraChanged || screenChanged || canvasChanged;
    }

    private bool HasMarkerGeometryChanged()
    {
        return CalculateMarkerGeometryHash() != lastMarkerGeometryHash;
    }

    private int CalculateMarkerGeometryHash()
    {
        unchecked
        {
            int hash = 17;
            if (wallRoot == null)
            {
                return hash;
            }

            WallOpening[] openings = wallRoot.GetComponentsInChildren<WallOpening>(true);
            hash = hash * 31 + openings.Length;
            for (int i = 0; i < openings.Length; i++)
            {
                WallOpening opening = openings[i];
                if (opening == null || !opening.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Transform openingTransform = opening.transform;
                hash = hash * 31 + openingTransform.position.GetHashCode();
                hash = hash * 31 + openingTransform.rotation.GetHashCode();
                hash = hash * 31 + openingTransform.localScale.GetHashCode();
                hash = hash * 31 + opening.CenterDistance.GetHashCode();
                hash = hash * 31 + opening.Width.GetHashCode();
                hash = hash * 31 + opening.Depth.GetHashCode();

                WallOpeningContainer container = opening.Container;
                if (container != null)
                {
                    hash = hash * 31 + container.WallStart.GetHashCode();
                    hash = hash * 31 + container.WallEnd.GetHashCode();
                    hash = hash * 31 + container.WallThickness.GetHashCode();
                }
            }

            return hash;
        }
    }
}
