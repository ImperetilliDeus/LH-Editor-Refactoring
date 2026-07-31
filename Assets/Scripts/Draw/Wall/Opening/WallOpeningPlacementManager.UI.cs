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

    public float UnitsToMillimeters(float units)
    {
        return MeasurementUnits.UnitsToMillimeters(units);
    }

    public float GetSelectedOpeningBottomOffsetMillimeters(float fallbackValue, OpeningPlacementType requiredType)
    {
        if (!CanUseSelectedOpeningBottomOffset(requiredType))
        {
            return fallbackValue;
        }

        return UnitsToMillimeters(SelectedOpening.BottomY - SelectedOpening.Container.WallBottomY);
    }

    private bool CanUseSelectedOpeningBottomOffset(OpeningPlacementType requiredType)
    {
        return SelectedOpening != null
            && SelectedOpening.Type == requiredType
            && SelectedOpening.Container != null;
    }

    public void ApplySelectedDoorWidthFromInput(string inputText)
    {
        ApplySelectedOpeningWidthFromInput(inputText, OpeningPlacementType.Door, value => defaultDoorWidthMillimeters = value);
    }

    public void ApplySelectedDoorHeightFromInput(string inputText)
    {
        ApplySelectedOpeningHeightFromInput(inputText, OpeningPlacementType.Door, value => defaultDoorHeightMillimeters = value);
    }

    public void ApplySelectedWindowWidthFromInput(string inputText)
    {
        ApplySelectedOpeningWidthFromInput(inputText, OpeningPlacementType.Window, value => defaultWindowWidthMillimeters = value);
    }

    public void ApplySelectedWindowHeightFromInput(string inputText)
    {
        ApplySelectedOpeningHeightFromInput(inputText, OpeningPlacementType.Window, value => defaultWindowHeightMillimeters = value);
    }

    private void ApplySelectedOpeningWidthFromInput(
        string inputText,
        OpeningPlacementType requiredType,
        System.Action<float> updateDefaultValue)
    {
        ApplySelectedOpeningDimensionFromInput(
            inputText,
            requiredType,
            updateDefaultValue,
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

    private void ApplySelectedOpeningHeightFromInput(
        string inputText,
        OpeningPlacementType requiredType,
        System.Action<float> updateDefaultValue)
    {
        ApplySelectedOpeningDimensionFromInput(
            inputText,
            requiredType,
            updateDefaultValue,
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

        if (SelectedOpening == null || SelectedOpening.Container == null || SelectedOpening.Type != requiredType)
        {
            updateDefaultValue?.Invoke(millimeters);
            RefreshOpeningDetailInputs(true);
            return;
        }

        float depth = ResolveSelectedOpeningDepthFromInputMillimeters(SelectedOpening, millimeters);

        SelectedOpening.SetDepth(depth);
        RebuildSelectedOpeningWithUndo();
    }

    private float ResolveSelectedOpeningDepthFromInputMillimeters(WallOpening opening, float millimeters)
    {
        float depth = MillimetersToUnits(millimeters);
        depth = Mathf.Min(depth, opening.Container.WallThickness);
        if (TryGetOpeningTypeDefinition(opening, out OpeningTypeCatalogItem definition))
        {
            depth = definition.ClampDepth(depth);
            depth = Mathf.Min(depth, opening.Container.WallThickness);
        }

        return depth;
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
        ApplySelectedOpeningTypeFromDropdown(
            optionIndex,
            OpeningPlacementType.Door,
            GetDoorTypeKeyForOption,
            key => SelectedOpening.SetDoorTypeKey(key));
    }

    public void ApplySelectedDoorSwingDirection(bool opensRight)
    {
    }

    public void ApplySelectedDoorVerticalFlip(bool verticalFlip)
    {
    }

    public void ApplySelectedWindowTypeFromDropdown(int optionIndex)
    {
        ApplySelectedOpeningTypeFromDropdown(
            optionIndex,
            OpeningPlacementType.Window,
            GetWindowTypeKeyForOption,
            key => SelectedOpening.SetWindowTypeKey(key));
    }

    private void ApplySelectedOpeningTypeFromDropdown(
        int optionIndex,
        OpeningPlacementType requiredType,
        System.Func<int, string> resolveTypeKey,
        System.Action<string> applyTypeKey)
    {
        if (SelectedOpening == null || SelectedOpening.Type != requiredType)
        {
            return;
        }

        string nextTypeKey = resolveTypeKey(optionIndex);
        applyTypeKey(nextTypeKey);
        ApplySelectedOpeningTypeSizeConstraints();
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

        if (!CanApplySelectedOpeningDimension(requiredType))
        {
            updateDefaultValue?.Invoke(millimeters);
            RefreshOpeningDetailInputs(true);
            return;
        }

        WallOpeningContainer container = SelectedOpening.Container;
        if (container == null || applyToOpening == null)
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        float units = MillimetersToUnits(millimeters);
        if (!applyToOpening(SelectedOpening, container, units))
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        RebuildSelectedOpeningWithUndo();
    }

    private bool CanApplySelectedOpeningDimension(OpeningPlacementType? requiredType)
    {
        return SelectedOpening != null
            && (!requiredType.HasValue || SelectedOpening.Type == requiredType.Value);
    }

    private void ApplySelectedOpeningTypeSizeConstraints()
    {
        if (SelectedOpening == null || SelectedOpening.Container == null)
        {
            return;
        }

        ApplySelectedOpeningWidthConstraint();

        ApplySelectedOpeningHeightConstraint();

        ApplySelectedOpeningDepthConstraint();
    }

    private void ApplySelectedOpeningWidthConstraint()
    {
        float width = ClampOpeningWidth(SelectedOpening.Container, SelectedOpening, SelectedOpening.Width);
        if (width > MinimumWallSegmentLength)
        {
            SelectedOpening.SetWidth(width);
        }
    }

    private void ApplySelectedOpeningHeightConstraint()
    {
        float height = ClampOpeningHeight(SelectedOpening.Container, SelectedOpening, SelectedOpening.Height, SelectedOpening.BottomY);
        if (height > MinimumWallSegmentLength)
        {
            SelectedOpening.SetHeight(height);
        }
    }

    private void ApplySelectedOpeningDepthConstraint()
    {
        if (TryGetOpeningTypeDefinition(SelectedOpening, out OpeningTypeCatalogItem definition))
        {
            float depth = definition.ClampDepth(SelectedOpening.Depth);
            depth = Mathf.Min(depth, SelectedOpening.Container.WallThickness);
            if (depth > MinimumWallSegmentLength)
            {
                SelectedOpening.SetDepth(depth);
            }
        }
    }

    private void ApplySelectedOpeningBottomOffsetFromInput(string inputText, OpeningPlacementType requiredType)
    {
        if (!UnitDisplayUtility.TryParseMillimeters(inputText, out float millimeters))
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        if (SelectedOpening == null || SelectedOpening.Type != requiredType)
        {
            UpdateDefaultBottomOffsetMillimeters(requiredType, millimeters);
            RefreshOpeningDetailInputs(true);
            return;
        }

        WallOpeningContainer container = SelectedOpening.Container;
        if (container == null)
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        ApplySelectedOpeningBottomOffset(container, millimeters);
        RebuildSelectedOpeningWithUndo();
    }

    private void ApplySelectedOpeningBottomOffset(WallOpeningContainer container, float millimeters)
    {
        float bottomY = container.WallBottomY + MillimetersToUnits(millimeters);
        bottomY = ClampOpeningBottomY(container, SelectedOpening, bottomY);
        SelectedOpening.SetBottomY(bottomY);

        float clampedHeight = ClampOpeningHeight(container, SelectedOpening, SelectedOpening.Height, bottomY);
        SelectedOpening.SetHeight(clampedHeight);
    }

    private void UpdateDefaultBottomOffsetMillimeters(OpeningPlacementType requiredType, float millimeters)
    {
        if (requiredType == OpeningPlacementType.Door)
        {
            defaultDoorBottomOffsetMillimeters = millimeters;
            return;
        }

        defaultWindowBottomOffsetMillimeters = millimeters;
    }

    private void RebuildSelectedOpeningWithUndo()
    {
        if (SelectedOpening == null || SelectedOpening.Container == null)
        {
            RefreshOpeningDetailInputs(true);
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = CaptureLayoutSnapshot(SelectedOpening.Container);
        RebuildContainer(SelectedOpening.Container, false);
        RefreshSelectedWallForContainer(SelectedOpening.Container, SelectedOpening.CenterDistance);
        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, CaptureLayoutSnapshot(SelectedOpening.Container));
        }

        RefreshOpeningDetailInputs(true);
    }

    private void RefreshOpeningDetailInputs(bool force)
    {
        presentationController.RefreshOpeningDetailInputs(
            doorUIController,
            windowUIController,
            SelectedOpening,
            defaultDoorWidthMillimeters,
            defaultDoorHeightMillimeters,
            defaultDoorDepthMillimeters,
            defaultDoorBottomOffsetMillimeters,
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
        presentationController.SetOpeningDetailMenuVisible(
            doorUIController,
            windowUIController,
            SelectedOpening,
            OpeningPlacementType.Door,
            visible);
    }

    private bool IsCurrentDetailMenuActive()
    {
        return presentationController.IsCurrentDetailMenuActive(
            doorUIController,
            windowUIController,
            SelectedOpening,
            OpeningPlacementType.Door);
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
            RefreshMarkerVisualOrQueueRemoval(markerUI);
        }

        for (int i = 0; i < removedMarkerUIs.Count; i++)
        {
            markerUIs.Remove(removedMarkerUIs[i]);
        }
    }

    private void RefreshMarkerVisualOrQueueRemoval(WallOpeningMarkerUI markerUI)
    {
        if (markerUI == null)
        {
            removedMarkerUIs.Add(markerUI);
            return;
        }

        markerUI.RefreshVisual();
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
        bool cameraChanged = HasMainCameraStateChanged();
        bool screenChanged = HasScreenStateChanged();
        bool canvasChanged = HasPreviewCanvasStateChanged();

        return cameraChanged || screenChanged || canvasChanged;
    }

    private bool HasMainCameraStateChanged()
    {
        if (mainCamera != null)
        {
            Transform cameraTransform = mainCamera.transform;
            return cameraTransform.position != lastCameraPosition ||
                cameraTransform.rotation != lastCameraRotation ||
                !Mathf.Approximately(mainCamera.orthographicSize, lastCameraOrthoSize);
        }

        return false;
    }

    private bool HasScreenStateChanged()
    {
        return Screen.width != lastScreenWidth || Screen.height != lastScreenHeight;
    }

    private bool HasPreviewCanvasStateChanged()
    {
        float currentCanvasScale = previewCanvas != null ? previewCanvas.scaleFactor : 0f;
        Camera currentCanvasCamera = previewCanvas != null ? previewCanvas.worldCamera : null;
        return !Mathf.Approximately(currentCanvasScale, lastCanvasScaleFactor) ||
            currentCanvasCamera != lastPreviewCanvasWorldCamera;
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
