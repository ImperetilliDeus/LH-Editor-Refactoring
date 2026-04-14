using System;
using UnityEngine;
using UnityEngine.UI;

public enum EditorMode
{
    Default = 0,
    RoomCreate = 1,
    FurniturePlace = 9,
    DetailEdit = 3,
    DoorInsert = 6,
    WindowInsert = 7,
}

public class ModeManager : MonoBehaviour
{
    private const string DefaultModeButtonName = "_Button_Default";
    private const string DefaultRoomCreateButtonName = "_Button_Room";
    private const string DefaultFurnitureButtonName = "_Button_EditFurnish";
    private const string DefaultDetailEditButtonName = "_Button_EditDetail";

    private const int LegacyRoomCreateModeA = 2;
    private const int LegacyRoomCreateModeB = 4;
    private const int LegacyRoomCreateModeC = 5;
    private const int LegacyRoomCreateModeD = 8;

    public static ModeManager Instance { get; private set; }

    [Header("UI Buttons")]
    [SerializeField] private Button defaultModeButton;
    [SerializeField] private Button roomCreateModeButton;
    [SerializeField] private Button furniturePlaceModeButton;
    [SerializeField] private Button detailEditModeButton;
    [SerializeField] private Button doorInsertModeButton;
    [SerializeField] private Button windowInsertModeButton;

    [Header("State")]
    [SerializeField] private EditorMode initialMode = EditorMode.Default;

    public EditorMode CurrentMode { get; private set; }

    public event Action<EditorMode> ModeChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveButtons();
        CurrentMode = NormalizeLegacyMode(initialMode);
        BindButtons();
        RefreshButtonState();
    }

    private void OnDestroy()
    {
        UnbindButtons();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public bool IsMode(EditorMode mode)
    {
        return CurrentMode == mode;
    }

    public void SetMode(EditorMode mode)
    {
        mode = NormalizeLegacyMode(mode);
        if (CurrentMode == mode)
        {
            RefreshButtonState();
            return;
        }

        CurrentMode = mode;
        RefreshButtonState();
        ModeChanged?.Invoke(CurrentMode);
    }

    public void SetDefaultMode()
    {
        SetMode(EditorMode.Default);
    }

    public void SetRoomCreateMode()
    {
        SetMode(EditorMode.RoomCreate);
    }

    public void SetDetailEditMode()
    {
        SetMode(EditorMode.DetailEdit);
    }

    public void SetFurniturePlaceMode()
    {
        SetMode(EditorMode.FurniturePlace);
    }

    public void SetDoorInsertMode()
    {
        SetMode(EditorMode.DoorInsert);
    }

    public void SetWindowInsertMode()
    {
        SetMode(EditorMode.WindowInsert);
    }

    private void BindButtons()
    {
        if (defaultModeButton != null)
        {
            defaultModeButton.onClick.AddListener(SetDefaultMode);
        }

        if (roomCreateModeButton != null)
        {
            roomCreateModeButton.onClick.AddListener(SetRoomCreateMode);
        }

        if (furniturePlaceModeButton != null)
        {
            furniturePlaceModeButton.onClick.AddListener(SetFurniturePlaceMode);
        }

        if (detailEditModeButton != null)
        {
            detailEditModeButton.onClick.AddListener(SetDetailEditMode);
        }

        if (doorInsertModeButton != null)
        {
            doorInsertModeButton.onClick.AddListener(SetDoorInsertMode);
        }

        if (windowInsertModeButton != null)
        {
            windowInsertModeButton.onClick.AddListener(SetWindowInsertMode);
        }
    }

    private void ResolveButtons()
    {
        defaultModeButton = ResolveButton(defaultModeButton, DefaultModeButtonName);
        roomCreateModeButton = ResolveButton(roomCreateModeButton, DefaultRoomCreateButtonName);
        furniturePlaceModeButton = ResolveButton(furniturePlaceModeButton, DefaultFurnitureButtonName);
        detailEditModeButton = ResolveButton(detailEditModeButton, DefaultDetailEditButtonName);
    }

    private static Button ResolveButton(Button currentButton, string objectName)
    {
        if (currentButton != null || string.IsNullOrWhiteSpace(objectName))
        {
            return currentButton;
        }

        Transform target = LayerUtility.FindTransformByName(objectName, true);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private void UnbindButtons()
    {
        if (defaultModeButton != null)
        {
            defaultModeButton.onClick.RemoveListener(SetDefaultMode);
        }

        if (roomCreateModeButton != null)
        {
            roomCreateModeButton.onClick.RemoveListener(SetRoomCreateMode);
        }

        if (furniturePlaceModeButton != null)
        {
            furniturePlaceModeButton.onClick.RemoveListener(SetFurniturePlaceMode);
        }

        if (detailEditModeButton != null)
        {
            detailEditModeButton.onClick.RemoveListener(SetDetailEditMode);
        }

        if (doorInsertModeButton != null)
        {
            doorInsertModeButton.onClick.RemoveListener(SetDoorInsertMode);
        }

        if (windowInsertModeButton != null)
        {
            windowInsertModeButton.onClick.RemoveListener(SetWindowInsertMode);
        }
    }

    private void RefreshButtonState()
    {
        if (defaultModeButton != null)
        {
            defaultModeButton.interactable = CurrentMode != EditorMode.Default;
        }

        bool isRoomCreateMode = CurrentMode == EditorMode.RoomCreate;
        if (roomCreateModeButton != null)
        {
            roomCreateModeButton.interactable = !isRoomCreateMode;
        }

        if (furniturePlaceModeButton != null)
        {
            furniturePlaceModeButton.interactable = CurrentMode != EditorMode.FurniturePlace;
        }

        if (detailEditModeButton != null)
        {
            detailEditModeButton.interactable = CurrentMode != EditorMode.DetailEdit;
        }

        if (doorInsertModeButton != null)
        {
            doorInsertModeButton.interactable = CurrentMode != EditorMode.DoorInsert;
        }

        if (windowInsertModeButton != null)
        {
            windowInsertModeButton.interactable = CurrentMode != EditorMode.WindowInsert;
        }
    }

    private static EditorMode NormalizeLegacyMode(EditorMode mode)
    {
        int rawMode = (int)mode;
        if (rawMode == LegacyRoomCreateModeA ||
            rawMode == LegacyRoomCreateModeB ||
            rawMode == LegacyRoomCreateModeC ||
            rawMode == LegacyRoomCreateModeD)
        {
            return EditorMode.RoomCreate;
        }

        return Enum.IsDefined(typeof(EditorMode), mode)
            ? mode
            : EditorMode.Default;
    }
}
