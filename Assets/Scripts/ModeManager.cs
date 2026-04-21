using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum EditorMode
{
    Default = 0,
    RoomCreate = 1,
    FurniturePlace = 9,
    DetailEdit = 3,
    DoorInsert = 6,
    WindowInsert = 7,
    DrawingOverlayCalibrate = 10,
}

public class ModeManager : MonoBehaviour
{
    private sealed class RegisteredModeButton
    {
        public Button button;
        public EditorMode mode;
        public UnityAction action;
    }

    private const int LegacyRoomCreateModeA = 2;
    private const int LegacyRoomCreateModeB = 4;
    private const int LegacyRoomCreateModeC = 5;
    private const int LegacyRoomCreateModeD = 8;

    public static ModeManager Instance { get; private set; }

    [Header("State")]
    [SerializeField] private EditorMode initialMode = EditorMode.Default;

    public EditorMode CurrentMode { get; private set; }

    public event Action<EditorMode> ModeChanged;

    private readonly List<RegisteredModeButton> registeredButtons = new List<RegisteredModeButton>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CurrentMode = NormalizeLegacyMode(initialMode);
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

    public void RegisterModeButton(Button button, EditorMode mode)
    {
        if (button == null)
        {
            return;
        }

        for (int i = 0; i < registeredButtons.Count; i++)
        {
            RegisteredModeButton existing = registeredButtons[i];
            if (existing.button != button)
            {
                continue;
            }

            if (existing.mode == mode)
            {
                RefreshButtonState();
                return;
            }

            button.onClick.RemoveListener(existing.action);
            registeredButtons.RemoveAt(i);
            break;
        }

        EditorMode targetMode = mode;
        UnityAction action = () => SetMode(targetMode);
        button.onClick.AddListener(action);
        registeredButtons.Add(new RegisteredModeButton
        {
            button = button,
            mode = mode,
            action = action,
        });

        RefreshButtonState();
    }

    public void UnregisterModeButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        for (int i = registeredButtons.Count - 1; i >= 0; i--)
        {
            RegisteredModeButton registeredButton = registeredButtons[i];
            if (registeredButton.button != button)
            {
                continue;
            }

            button.onClick.RemoveListener(registeredButton.action);
            registeredButtons.RemoveAt(i);
        }
    }

    private void UnbindButtons()
    {
        for (int i = 0; i < registeredButtons.Count; i++)
        {
            RegisteredModeButton registeredButton = registeredButtons[i];
            if (registeredButton.button == null)
            {
                continue;
            }

            registeredButton.button.onClick.RemoveListener(registeredButton.action);
        }

        registeredButtons.Clear();
    }

    private void RefreshButtonState()
    {
        for (int i = 0; i < registeredButtons.Count; i++)
        {
            RegisteredModeButton registeredButton = registeredButtons[i];
            if (registeredButton.button == null)
            {
                continue;
            }

            registeredButton.button.interactable = CurrentMode != registeredButton.mode;
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
