using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WindowOpeningUIController : MonoBehaviour
{
    [SerializeField] private GameObject detailMenu;
    [SerializeField] private InputField widthInputField;
    [SerializeField] private InputField heightInputField;
    [SerializeField] private InputField depthInputField;
    [SerializeField] private InputField bottomOffsetInputField;
    [SerializeField] private TMP_Dropdown typeDropdown;

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

        bool isWindowSelected = selectedOpening != null && selectedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Window;
        float width = isWindowSelected ? placementManager.UnitsToMillimeters(selectedOpening.Width) : defaultWidth;
        float height = isWindowSelected ? placementManager.UnitsToMillimeters(selectedOpening.Height) : defaultHeight;
        float depth = isWindowSelected ? placementManager.UnitsToMillimeters(selectedOpening.Depth) : defaultDepth;
        float bottomOffset = isWindowSelected
            ? placementManager.GetSelectedOpeningBottomOffsetMillimeters(defaultBottomOffset, WallOpeningPlacementManager.OpeningPlacementType.Window)
            : defaultBottomOffset;

        SetInputFieldValue(widthInputField, width, force, isWindowSelected);
        SetInputFieldValue(heightInputField, height, force, isWindowSelected);
        SetInputFieldValue(depthInputField, depth, force, isWindowSelected);
        SetInputFieldValue(bottomOffsetInputField, bottomOffset, force, isWindowSelected);

        if (typeDropdown != null)
        {
            typeDropdown.interactable = isWindowSelected;
            if (isWindowSelected)
            {
                int optionIndex = placementManager.GetWindowTypeOptionIndex(selectedOpening.WindowTypeKey);
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

    public string GetCurrentWindowTypeKey()
    {
        return GetWindowTypeKeyForOption(typeDropdown != null ? typeDropdown.value : -1);
    }

    public string GetWindowTypeKeyForOption(int optionIndex)
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

    public int FindWindowTypeOptionIndex(string windowTypeKey)
    {
        if (typeDropdown == null || typeDropdown.options == null)
        {
            return -1;
        }

        for (int i = 0; i < typeDropdown.options.Count; i++)
        {
            TMP_Dropdown.OptionData option = typeDropdown.options[i];
            string optionText = option != null ? option.text : string.Empty;
            if (string.Equals(optionText, windowTypeKey, System.StringComparison.Ordinal))
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
            LayerUtility.ApplyLayer(detailMenu, LayerUtility.WindowUILayerName, true);
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
    }

    private void HandleWidthChanged(string value)
    {
        placementManager?.ApplySelectedWindowWidthFromInput(value);
    }

    private void HandleHeightChanged(string value)
    {
        placementManager?.ApplySelectedWindowHeightFromInput(value);
    }

    private void HandleDepthChanged(string value)
    {
        placementManager?.ApplySelectedWindowDepthFromInput(value);
    }

    private void HandleBottomOffsetChanged(string value)
    {
        placementManager?.ApplySelectedWindowBottomOffsetFromInput(value);
    }

    private void HandleTypeChanged(int optionIndex)
    {
        placementManager?.ApplySelectedWindowTypeFromDropdown(optionIndex);
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
