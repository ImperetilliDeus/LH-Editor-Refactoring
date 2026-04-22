using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

internal sealed class WallPropertyPresentationController
{
    public void UpdateInputFieldValues(
        Action updateAnchorButtonState,
        Action<bool> updateLengthInputFieldValue,
        Action<bool> updateHeightInputFieldValue,
        Action<bool> updateThicknessInputFieldValue,
        Action updateAddOpeningsTarget,
        bool force)
    {
        updateAnchorButtonState?.Invoke();
        updateLengthInputFieldValue?.Invoke(force);
        updateHeightInputFieldValue?.Invoke(force);
        updateThicknessInputFieldValue?.Invoke(force);
        updateAddOpeningsTarget?.Invoke();
    }

    public void SetInputFieldInteractableSafely(InputField inputField, bool interactable)
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

    public void UpdateAddOpeningsTarget(GameObject addOpeningsTarget, bool shouldBeActive)
    {
        if (addOpeningsTarget == null)
        {
            return;
        }

        if (addOpeningsTarget.activeSelf != shouldBeActive)
        {
            addOpeningsTarget.SetActive(shouldBeActive);
        }
    }
}
