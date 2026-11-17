using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class WallPropertyInputManager
{
    private void UpdateInputFieldValues(bool force = false)
    {
        presentationController.UpdateInputFieldValues(
            UpdateAnchorButtonState,
            UpdateLengthInputFieldValue,
            UpdateHeightInputFieldValue,
            UpdateThicknessInputFieldValue,
            UpdateAddOpeningsTarget,
            force);
    }

    private void UpdateLengthInputFieldValue(bool force = false)
    {
        if (wallLengthInputField == null)
        {
            return;
        }

        GameObject selectedWall = GetSelectedWall();
        bool interactable = selectedWall != null;
        presentationController.SetInputFieldInteractableSafely(wallLengthInputField, interactable);

        if (!force && wallLengthInputField.isFocused)
        {
            return;
        }

        string nextText = string.Empty;
        if (selectedWall != null && selectedWall.TryGetComponent(out Wall selectedWallComponent))
        {
            float lengthMillimeters = MeasurementUnits.UnitsToMillimeters(GetDisplayedLengthUnits(selectedWallComponent));
            nextText = UnitDisplayUtility.FormatMillimetersWithConversions(lengthMillimeters);
        }

        SetInputFieldText(wallLengthInputField, nextText);
    }

    private void UpdateHeightInputFieldValue(bool force = false)
    {
        if (wallHeightInputField == null)
        {
            return;
        }

        GameObject selectedWall = GetSelectedWall();
        bool interactable = selectedWall != null;
        presentationController.SetInputFieldInteractableSafely(wallHeightInputField, interactable);

        if (!force && wallHeightInputField.isFocused)
        {
            return;
        }

        string nextText = string.Empty;
        if (selectedWall != null)
        {
            float heightMillimeters = MeasurementUnits.UnitsToMillimeters(GetDisplayedHeightUnits(selectedWall));
            nextText = UnitDisplayUtility.FormatMillimetersWithConversions(heightMillimeters);
        }

        SetInputFieldText(wallHeightInputField, nextText);
    }

    private void UpdateThicknessInputFieldValue(bool force = false)
    {
        if (wallThicknessInputField == null)
        {
            return;
        }

        GameObject selectedWall = GetSelectedWall();
        bool interactable = selectedWall != null;
        presentationController.SetInputFieldInteractableSafely(wallThicknessInputField, interactable);

        if (!force && wallThicknessInputField.isFocused)
        {
            return;
        }

        string nextText = string.Empty;
        if (selectedWall != null)
        {
            float thicknessMillimeters = MeasurementUnits.UnitsToMillimeters(GetDisplayedThicknessUnits(selectedWall));
            nextText = UnitDisplayUtility.FormatMillimetersWithConversions(thicknessMillimeters);
        }

        SetInputFieldText(wallThicknessInputField, nextText);
    }

    private void SetInputFieldText(InputField inputField, string text)
    {
        if (inputField == null || inputField.text == text)
        {
            return;
        }

        suppressInputCallback = true;
        inputField.SetTextWithoutNotify(text);
        suppressInputCallback = false;
    }

    private void BindAnchorButtons()
    {
        if (leftLengthAnchorButton != null)
        {
            leftLengthAnchorButton.onClick.AddListener(SetLeftLengthAnchor);
        }

        if (rightLengthAnchorButton != null)
        {
            rightLengthAnchorButton.onClick.AddListener(SetRightLengthAnchor);
        }
    }

    private void UnbindAnchorButtons()
    {
        if (leftLengthAnchorButton != null)
        {
            leftLengthAnchorButton.onClick.RemoveListener(SetLeftLengthAnchor);
        }

        if (rightLengthAnchorButton != null)
        {
            rightLengthAnchorButton.onClick.RemoveListener(SetRightLengthAnchor);
        }
    }

    private void BindStateEvents()
    {
        if (wallSelectionManager != null)
        {
            wallSelectionManager.SelectionChanged -= HandleSelectionChanged;
            wallSelectionManager.SelectionChanged += HandleSelectionChanged;
            wallSelectionManager.SelectionSetChanged -= HandleSelectionSetChanged;
            wallSelectionManager.SelectionSetChanged += HandleSelectionSetChanged;
        }

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
            modeManager.ModeChanged += HandleModeChanged;
        }

        if (wallLengthDisplay != null)
        {
            wallLengthDisplay.LengthLabelClicked -= HandleLengthLabelClicked;
            wallLengthDisplay.LengthLabelClicked += HandleLengthLabelClicked;
        }
    }

    private void UnbindStateEvents()
    {
        if (wallSelectionManager != null)
        {
            wallSelectionManager.SelectionChanged -= HandleSelectionChanged;
            wallSelectionManager.SelectionSetChanged -= HandleSelectionSetChanged;
        }

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
        }

        if (wallLengthDisplay != null)
        {
            wallLengthDisplay.LengthLabelClicked -= HandleLengthLabelClicked;
        }
    }

    private void HandleSelectionChanged(GameObject _)
    {
        UpdateInputFieldValues(true);
    }

    private void HandleSelectionSetChanged()
    {
        UpdateInputFieldValues(true);
    }

    private void HandleModeChanged(EditorMode mode)
    {
        UpdateInputFieldValues(true);
    }

    private void HandleLengthLabelClicked(Transform wallTransform)
    {
        if (wallTransform == null || wallLengthInputField == null)
        {
            return;
        }

        GameObject wallObject = wallTransform.gameObject;
        if (wallSelectionManager != null)
        {
            wallSelectionManager.SetSelectedWall(wallObject);
        }

        UpdateInputFieldValues(true);
        if (!wallLengthInputField.interactable)
        {
            return;
        }

        wallLengthInputField.ActivateInputField();
        wallLengthInputField.Select();

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(wallLengthInputField.gameObject);
        }

        wallLengthInputField.MoveTextEnd(false);
    }

    private void UpdateAnchorButtonState()
    {
        bool lengthFieldEnabled = GetSelectedWall() != null;
        if (leftLengthAnchorButton != null)
        {
            leftLengthAnchorButton.interactable = lengthFieldEnabled && lengthAnchorMode != LengthAnchorMode.LeftFixed;
        }

        if (rightLengthAnchorButton != null)
        {
            rightLengthAnchorButton.interactable = lengthFieldEnabled && lengthAnchorMode != LengthAnchorMode.RightFixed;
        }
    }

    private void UpdateAddOpeningsTarget()
    {
        bool shouldBeActive = GetSelectedWall() != null;
        presentationController.UpdateAddOpeningsTarget(addOpeningsTarget, shouldBeActive);
    }
}
