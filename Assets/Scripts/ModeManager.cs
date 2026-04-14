using System;
using UnityEngine;
using UnityEngine.UI;

public enum EditorMode
{
    Default = 0,
    RoomCreate = 1,
    DetailEdit = 3,
    DoorInsert = 6,
    WindowInsert = 7,
}

public class ModeManager : MonoBehaviour
{
    public static ModeManager Instance { get; private set; }

    [Header("UI Buttons")]
    [SerializeField] private Button defaultModeButton;
    [SerializeField] private Button roomCreateModeButton;
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
        if (rawMode == 2 || rawMode == 4 || rawMode == 5 || rawMode == 8)
        {
            return EditorMode.RoomCreate;
        }

        return Enum.IsDefined(typeof(EditorMode), mode)
            ? mode
            : EditorMode.Default;
    }
}
