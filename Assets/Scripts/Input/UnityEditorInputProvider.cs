using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public sealed class UnityEditorInputProvider : IEditorInputProvider
{
    public bool IsPointerAvailable => Mouse.current != null;

    public bool TryGetPointerScreenPosition(out Vector2 pointerScreenPosition)
    {
        pointerScreenPosition = Vector2.zero;
        if (Mouse.current == null)
        {
            return false;
        }

        pointerScreenPosition = Mouse.current.position.ReadValue();
        return true;
    }

    public bool TryGetPointerDelta(out Vector2 pointerDelta)
    {
        pointerDelta = Vector2.zero;
        if (Mouse.current == null)
        {
            return false;
        }

        pointerDelta = Mouse.current.delta.ReadValue();
        return true;
    }

    public float GetScrollDeltaY()
    {
        return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
    }

    public bool IsPointerOverUI(EventSystem eventSystem, List<RaycastResult> raycastResults = null)
    {
        if (eventSystem == null)
        {
            return false;
        }

        if (Mouse.current != null && eventSystem.IsPointerOverGameObject(Mouse.current.deviceId))
        {
            return true;
        }

        if (raycastResults == null || !TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            position = pointerScreenPosition,
        };

        raycastResults.Clear();
        eventSystem.RaycastAll(eventData, raycastResults);
        return raycastResults.Count > 0;
    }

    public bool WasPointerButtonPressedThisFrame(PointerButton button)
    {
        ButtonControl control = GetPointerButton(button);
        return control != null && control.wasPressedThisFrame;
    }

    public bool WasPointerButtonReleasedThisFrame(PointerButton button)
    {
        ButtonControl control = GetPointerButton(button);
        return control != null && control.wasReleasedThisFrame;
    }

    public bool IsPointerButtonPressed(PointerButton button)
    {
        ButtonControl control = GetPointerButton(button);
        return control != null && control.isPressed;
    }

    public bool WasKeyPressedThisFrame(Key key)
    {
        KeyControl control = GetKeyControl(key);
        return control != null && control.wasPressedThisFrame;
    }

    public bool IsKeyPressed(Key key)
    {
        KeyControl control = GetKeyControl(key);
        return control != null && control.isPressed;
    }

    private static ButtonControl GetPointerButton(PointerButton button)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
        {
            return null;
        }

        switch (button)
        {
            case PointerButton.Left:
                return mouse.leftButton;
            case PointerButton.Right:
                return mouse.rightButton;
            case PointerButton.Middle:
                return mouse.middleButton;
            default:
                return null;
        }
    }

    private static KeyControl GetKeyControl(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null ? keyboard[key] : null;
    }
}
