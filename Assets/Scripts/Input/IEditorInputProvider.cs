using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public interface IEditorInputProvider
{
    bool IsPointerAvailable { get; }
    bool TryGetPointerScreenPosition(out Vector2 pointerScreenPosition);
    bool IsPointerOverUI(EventSystem eventSystem, List<RaycastResult> raycastResults = null);
    bool WasPointerButtonPressedThisFrame(PointerButton button);
    bool WasPointerButtonReleasedThisFrame(PointerButton button);
    bool IsPointerButtonPressed(PointerButton button);
    bool WasKeyPressedThisFrame(Key key);
    bool IsKeyPressed(Key key);
}
