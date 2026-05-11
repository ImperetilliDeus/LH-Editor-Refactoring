using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DwgWallImportPopupView : MonoBehaviour
{
    [SerializeField] private Text selectedPathText;
    [SerializeField] private InputField cadScaleInputField;
    [SerializeField] private InputField layerSearchInputField;
    [SerializeField] private Transform layerToggleContainer;
    [SerializeField] private Toggle layerTogglePrefab;
    [SerializeField] private Button selectAllLayersButton;
    [SerializeField] private Button clearAllLayersButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;

    public Text SelectedPathText => selectedPathText;
    public InputField CadScaleInputField => cadScaleInputField;
    public InputField LayerSearchInputField => layerSearchInputField;
    public Transform LayerToggleContainer => layerToggleContainer;
    public Toggle LayerTogglePrefab => layerTogglePrefab;
    public Button SelectAllLayersButton => selectAllLayersButton;
    public Button ClearAllLayersButton => clearAllLayersButton;
    public Button CancelButton => cancelButton;
    public Button ConfirmButton => confirmButton;

    private void Reset()
    {
        RefreshReferences();
    }

    private void OnValidate()
    {
        RefreshReferences();
    }

    public void RefreshReferences()
    {
        selectedPathText = selectedPathText != null ? selectedPathText : FindText("PathValue");
        cadScaleInputField = cadScaleInputField != null ? cadScaleInputField : FindInputField("ScaleInput");
        layerSearchInputField = layerSearchInputField != null ? layerSearchInputField : FindInputField("LayerSearchInput");
        layerToggleContainer = layerToggleContainer != null ? layerToggleContainer : FindTransform("LayerToggleContainer");
        layerTogglePrefab = layerTogglePrefab != null ? layerTogglePrefab : FindToggle("LayerToggleTemplate");
        selectAllLayersButton = selectAllLayersButton != null ? selectAllLayersButton : FindButton("SelectAllLayersButton");
        clearAllLayersButton = clearAllLayersButton != null ? clearAllLayersButton : FindButton("ClearAllLayersButton");
        cancelButton = cancelButton != null ? cancelButton : FindButton("CancelButton");
        confirmButton = confirmButton != null ? confirmButton : FindButton("ImportButton");
    }

    private Text FindText(string childName)
    {
        Transform child = FindTransform(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private InputField FindInputField(string childName)
    {
        Transform child = FindTransform(childName);
        return child != null ? child.GetComponent<InputField>() : null;
    }

    private Toggle FindToggle(string childName)
    {
        Transform child = FindTransform(childName);
        return child != null ? child.GetComponent<Toggle>() : null;
    }

    private Button FindButton(string childName)
    {
        Transform child = FindTransform(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private Transform FindTransform(string childName)
    {
        return string.IsNullOrWhiteSpace(childName) ? null : LayerUtility.FindChildByName(transform, childName);
    }
}
