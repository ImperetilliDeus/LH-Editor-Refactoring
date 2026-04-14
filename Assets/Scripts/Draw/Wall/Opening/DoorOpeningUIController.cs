using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DoorOpeningUIController : MonoBehaviour
{
    [SerializeField] private GameObject detailMenu;
    [SerializeField] private InputField widthInputField;
    [SerializeField] private InputField heightInputField;
    [SerializeField] private InputField depthInputField;
    [SerializeField] private InputField bottomOffsetInputField;
    [SerializeField] private TMP_Dropdown typeDropdown;
    [SerializeField] private Button leftSwingButton;
    [SerializeField] private Button rightSwingButton;
    [SerializeField] private Toggle verticalFlipToggle;

    private WallOpeningPlacementManager placementManager;

    public bool IsMenuVisible => detailMenu != null && detailMenu.activeSelf;

    public void Initialize(WallOpeningPlacementManager manager)
    {
        placementManager = manager;
        ApplyLayer();
    }

    private void OnEnable()
    {
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    public void SetVisible(bool visible)
    {
        ApplyLayer();
        if (detailMenu != null)
        {
            detailMenu.SetActive(visible);
        }
    }

    public void Refresh(WallOpening selectedOpening, float defaultWidth, float defaultHeight, float defaultDepth, float defaultBottomOffset, bool force)
    {
        if (placementManager == null)
        {
            SetInputFieldValue(widthInputField, defaultWidth, force, false);
            SetInputFieldValue(heightInputField, defaultHeight, force, false);
            SetInputFieldValue(depthInputField, defaultDepth, force, false);
            SetInputFieldValue(bottomOffsetInputField, defaultBottomOffset, force, false);

            if (leftSwingButton != null)
            {
                leftSwingButton.interactable = false;
            }

            if (rightSwingButton != null)
            {
                rightSwingButton.interactable = false;
            }

            if (verticalFlipToggle != null)
            {
                verticalFlipToggle.interactable = false;
                if (force || verticalFlipToggle.isOn)
                {
                    verticalFlipToggle.SetIsOnWithoutNotify(false);
                }
            }

            if (typeDropdown != null)
            {
                typeDropdown.interactable = false;
                if (typeDropdown.options != null && typeDropdown.options.Count > 0 && typeDropdown.value != 0)
                {
                    typeDropdown.SetValueWithoutNotify(0);
                }
            }

            return;
        }

        bool isDoorSelected = selectedOpening != null && selectedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door;
        float width = isDoorSelected ? placementManager.UnitsToMillimeters(selectedOpening.Width) : defaultWidth;
        float height = isDoorSelected ? placementManager.UnitsToMillimeters(selectedOpening.Height) : defaultHeight;
        float depth = isDoorSelected ? placementManager.UnitsToMillimeters(selectedOpening.Depth) : defaultDepth;
        float bottomOffset = isDoorSelected
            ? placementManager.GetSelectedOpeningBottomOffsetMillimeters(defaultBottomOffset, WallOpeningPlacementManager.OpeningPlacementType.Door)
            : defaultBottomOffset;

        SetInputFieldValue(widthInputField, width, force, isDoorSelected);
        SetInputFieldValue(heightInputField, height, force, isDoorSelected);
        SetInputFieldValue(depthInputField, depth, force, isDoorSelected);
        SetInputFieldValue(bottomOffsetInputField, bottomOffset, force, isDoorSelected);

        bool opensRight = isDoorSelected && selectedOpening.DoorOpensRight;
        bool verticalFlip = isDoorSelected && selectedOpening.DoorVerticalFlip;
        if (leftSwingButton != null)
        {
            leftSwingButton.interactable = isDoorSelected && opensRight;
        }

        if (rightSwingButton != null)
        {
            rightSwingButton.interactable = isDoorSelected && !opensRight;
        }

        if (verticalFlipToggle != null)
        {
            verticalFlipToggle.interactable = isDoorSelected;
            if (force || !verticalFlipToggle.isOn.Equals(verticalFlip))
            {
                verticalFlipToggle.SetIsOnWithoutNotify(verticalFlip);
            }
        }

        if (typeDropdown != null)
        {
            typeDropdown.interactable = isDoorSelected;
            if (isDoorSelected)
            {
                int optionIndex = placementManager.GetDoorTypeOptionIndex(selectedOpening.DoorTypeKey);
                if (optionIndex < 0)
                {
                    optionIndex = typeDropdown.options != null && typeDropdown.options.Count > 0 ? 0 : -1;
                }

                if (optionIndex >= 0 && typeDropdown.value != optionIndex)
                {
                    typeDropdown.SetValueWithoutNotify(optionIndex);
                }
            }
        }
    }

    public string GetCurrentDoorTypeKey()
    {
        return GetDoorTypeKeyForOption(typeDropdown != null ? typeDropdown.value : -1);
    }

    public string GetDoorTypeKeyForOption(int optionIndex)
    {
        if (typeDropdown == null || typeDropdown.options == null || typeDropdown.options.Count == 0)
        {
            return string.Empty;
        }

        if (optionIndex < 0 || optionIndex >= typeDropdown.options.Count)
        {
            return string.Empty;
        }

        TMP_Dropdown.OptionData option = typeDropdown.options[optionIndex];
        return option != null ? option.text ?? string.Empty : string.Empty;
    }

    public int FindDoorTypeOptionIndex(string doorTypeKey)
    {
        if (typeDropdown == null || typeDropdown.options == null)
        {
            return -1;
        }

        for (int i = 0; i < typeDropdown.options.Count; i++)
        {
            TMP_Dropdown.OptionData option = typeDropdown.options[i];
            string optionText = option != null ? option.text : string.Empty;
            if (string.Equals(optionText, doorTypeKey, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return typeDropdown.options.Count > 0 ? 0 : -1;
    }

    private void ApplyLayer()
    {
        if (detailMenu != null)
        {
            LayerUtility.ApplyLayer(detailMenu, LayerUtility.DoorUILayerName, true);
        }
    }

    private void BindEvents()
    {
        if (widthInputField != null)
        {
            widthInputField.onEndEdit.AddListener(HandleWidthChanged);
        }

        if (heightInputField != null)
        {
            heightInputField.onEndEdit.AddListener(HandleHeightChanged);
        }

        if (depthInputField != null)
        {
            depthInputField.onEndEdit.AddListener(HandleDepthChanged);
        }

        if (bottomOffsetInputField != null)
        {
            bottomOffsetInputField.onEndEdit.AddListener(HandleBottomOffsetChanged);
        }

        if (typeDropdown != null)
        {
            typeDropdown.onValueChanged.AddListener(HandleTypeChanged);
        }

        if (leftSwingButton != null)
        {
            leftSwingButton.onClick.AddListener(HandleLeftSwingClicked);
        }

        if (rightSwingButton != null)
        {
            rightSwingButton.onClick.AddListener(HandleRightSwingClicked);
        }

        if (verticalFlipToggle != null)
        {
            verticalFlipToggle.onValueChanged.AddListener(HandleVerticalFlipChanged);
        }
    }

    private void UnbindEvents()
    {
        if (widthInputField != null)
        {
            widthInputField.onEndEdit.RemoveListener(HandleWidthChanged);
        }

        if (heightInputField != null)
        {
            heightInputField.onEndEdit.RemoveListener(HandleHeightChanged);
        }

        if (depthInputField != null)
        {
            depthInputField.onEndEdit.RemoveListener(HandleDepthChanged);
        }

        if (bottomOffsetInputField != null)
        {
            bottomOffsetInputField.onEndEdit.RemoveListener(HandleBottomOffsetChanged);
        }

        if (typeDropdown != null)
        {
            typeDropdown.onValueChanged.RemoveListener(HandleTypeChanged);
        }

        if (leftSwingButton != null)
        {
            leftSwingButton.onClick.RemoveListener(HandleLeftSwingClicked);
        }

        if (rightSwingButton != null)
        {
            rightSwingButton.onClick.RemoveListener(HandleRightSwingClicked);
        }

        if (verticalFlipToggle != null)
        {
            verticalFlipToggle.onValueChanged.RemoveListener(HandleVerticalFlipChanged);
        }
    }

    private void HandleWidthChanged(string value)
    {
        placementManager?.ApplySelectedDoorWidthFromInput(value);
    }

    private void HandleHeightChanged(string value)
    {
        placementManager?.ApplySelectedDoorHeightFromInput(value);
    }

    private void HandleDepthChanged(string value)
    {
        placementManager?.ApplySelectedDoorDepthFromInput(value);
    }

    private void HandleBottomOffsetChanged(string value)
    {
        placementManager?.ApplySelectedDoorBottomOffsetFromInput(value);
    }

    private void HandleTypeChanged(int optionIndex)
    {
        placementManager?.ApplySelectedDoorTypeFromDropdown(optionIndex);
    }

    private void HandleLeftSwingClicked()
    {
        placementManager?.ApplySelectedDoorSwingDirection(false);
    }

    private void HandleRightSwingClicked()
    {
        placementManager?.ApplySelectedDoorSwingDirection(true);
    }

    private void HandleVerticalFlipChanged(bool value)
    {
        placementManager?.ApplySelectedDoorVerticalFlip(value);
    }

    private void SetInputFieldValue(InputField inputField, float value, bool force, bool interactable)
    {
        if (inputField == null)
        {
            return;
        }

        if (!force && inputField.isFocused)
        {
            return;
        }

        SetInputFieldInteractableSafely(inputField, interactable);

        string nextText = UnitDisplayUtility.FormatMillimetersWithConversions(Mathf.RoundToInt(value));
        if (inputField.text != nextText)
        {
            inputField.SetTextWithoutNotify(nextText);
        }
    }

    private void SetInputFieldInteractableSafely(InputField inputField, bool interactable)
    {
        if (inputField == null || inputField.interactable == interactable)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            GameObject currentSelected = eventSystem.currentSelectedGameObject;
            if (currentSelected == inputField.gameObject || currentSelected != null && currentSelected.transform.IsChildOf(inputField.transform))
            {
                return;
            }
        }

        inputField.interactable = interactable;
    }
}
