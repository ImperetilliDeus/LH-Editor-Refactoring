using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public partial class WallPropertyInputManager
{
    private void UpdateInputFieldValues(bool force = false)
    {
        UpdateAnchorButtonState();
        UpdateLengthInputFieldValue(force);
        UpdateHeightInputFieldValue(force);
        UpdateThicknessInputFieldValue(force);
        UpdateAddOpeningsTarget();
    }

    private void UpdateLengthInputFieldValue(bool force = false)
    {
        if (wallLengthInputField == null)
        {
            return;
        }

        GameObject selectedWall = GetSelectedWall();
        bool interactable = selectedWall != null && IsFieldEnabledForCurrentSelection(MultiSelectionField.Length);
        SetInputFieldInteractableSafely(wallLengthInputField, interactable);

        if (!force && wallLengthInputField.isFocused)
        {
            return;
        }

        string nextText = string.Empty;
        if (IsMultiSelectionActive())
        {
            nextText = TryGetCommonMultiSelectionValueText(MultiSelectionField.Length);
        }
        else if (selectedWall != null && selectedWall.TryGetComponent(out Wall selectedWallComponent))
        {
            float lengthMillimeters = GetDisplayedLengthUnits(selectedWallComponent) * 100f;
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
        bool interactable = selectedWall != null && IsFieldEnabledForCurrentSelection(MultiSelectionField.Height);
        SetInputFieldInteractableSafely(wallHeightInputField, interactable);

        if (!force && wallHeightInputField.isFocused)
        {
            return;
        }

        string nextText = string.Empty;
        if (IsMultiSelectionActive())
        {
            nextText = TryGetCommonMultiSelectionValueText(MultiSelectionField.Height);
        }
        else if (selectedWall != null)
        {
            float heightMillimeters = GetDisplayedHeightUnits(selectedWall) * 100f;
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
        bool interactable = selectedWall != null && IsFieldEnabledForCurrentSelection(MultiSelectionField.Thickness);
        SetInputFieldInteractableSafely(wallThicknessInputField, interactable);

        if (!force && wallThicknessInputField.isFocused)
        {
            return;
        }

        string nextText = string.Empty;
        if (IsMultiSelectionActive())
        {
            nextText = TryGetCommonMultiSelectionValueText(MultiSelectionField.Thickness);
        }
        else if (selectedWall != null)
        {
            float thicknessMillimeters = GetDisplayedThicknessUnits(selectedWall) * 100f;
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

    private void SetInputFieldInteractableSafely(InputField inputField, bool interactable)
    {
        if (inputField == null || inputField.interactable == interactable)
        {
            return;
        }

        GameObject selectedObject = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selectedObject == inputField.gameObject)
        {
            return;
        }

        inputField.interactable = interactable;
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

    private void UpdateAnchorButtonState()
    {
        bool lengthFieldEnabled = IsFieldEnabledForCurrentSelection(MultiSelectionField.Length) && GetSelectedWall() != null;
        if (leftLengthAnchorButton != null)
        {
            leftLengthAnchorButton.interactable = lengthFieldEnabled && lengthAnchorMode != LengthAnchorMode.LeftFixed;
        }

        if (rightLengthAnchorButton != null)
        {
            rightLengthAnchorButton.interactable = lengthFieldEnabled && lengthAnchorMode != LengthAnchorMode.RightFixed;
        }
    }

    private string TryGetCommonMultiSelectionValueText(MultiSelectionField field)
    {
        GetSelectedWallComponents(selectedWallComponents);
        if (selectedWallComponents.Count == 0)
        {
            return string.Empty;
        }

        float? commonValueMillimeters = null;
        for (int i = 0; i < selectedWallComponents.Count; i++)
        {
            Wall wall = selectedWallComponents[i];
            if (wall == null)
            {
                continue;
            }

            float nextValueMillimeters;
            switch (field)
            {
                case MultiSelectionField.Length:
                    nextValueMillimeters = GetDisplayedLengthUnits(wall) * 100f;
                    break;
                case MultiSelectionField.Height:
                    nextValueMillimeters = GetDisplayedHeightUnits(wall.gameObject) * 100f;
                    break;
                case MultiSelectionField.Thickness:
                    nextValueMillimeters = GetDisplayedThicknessUnits(wall.gameObject) * 100f;
                    break;
                default:
                    return string.Empty;
            }

            if (!commonValueMillimeters.HasValue)
            {
                commonValueMillimeters = nextValueMillimeters;
                continue;
            }

            if (Mathf.Abs(commonValueMillimeters.Value - nextValueMillimeters) > 0.0001f)
            {
                return string.Empty;
            }
        }

        return commonValueMillimeters.HasValue
            ? UnitDisplayUtility.FormatMillimetersWithConversions(commonValueMillimeters.Value)
            : string.Empty;
    }

    private void UpdateAddOpeningsTarget()
    {
        if (addOpeningsTarget == null)
        {
            return;
        }

        bool shouldBeActive = GetSelectedWall() != null && IsFieldEnabledForCurrentSelection(MultiSelectionField.AddOpenings);
        if (addOpeningsTarget.activeSelf != shouldBeActive)
        {
            addOpeningsTarget.SetActive(shouldBeActive);
        }
    }
}
