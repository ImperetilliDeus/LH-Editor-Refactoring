using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class EditorInputManager : MonoBehaviour
{
    private static EditorInputManager instance;

    private readonly Dictionary<EditorMode, IEditorModeInputHandler> modeHandlers = new Dictionary<EditorMode, IEditorModeInputHandler>();
    private readonly HashSet<IEditorModeInputHandler> globalHandlers = new HashSet<IEditorModeInputHandler>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private readonly EditorInputCommandFactory commandFactory = new EditorInputCommandFactory();

    private IEditorInputProvider inputProvider;
    private ModeManager modeManager;

    public static EditorInputManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject managerObject = new GameObject(nameof(EditorInputManager));
            instance = managerObject.AddComponent<EditorInputManager>();
            return instance;
        }
    }

    public static bool HasInstance => instance != null;

    public IEditorInputProvider InputProvider => inputProvider;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        inputProvider = new UnityEditorInputProvider();
        DontDestroyOnLoad(gameObject);
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        if (modeManager == null || inputProvider == null)
        {
            return;
        }

        EditorInputFrame inputFrame = BuildInputFrame(modeManager.CurrentMode);
        foreach (IEditorModeInputHandler globalHandler in globalHandlers)
        {
            globalHandler?.HandleEditorInput(inputFrame);
        }

        IEditorInputCommand command = commandFactory.CreateCommand(modeManager.CurrentMode, inputFrame, modeHandlers);
        command?.Execute();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public void RegisterHandler(EditorMode mode, IEditorModeInputHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        modeHandlers[mode] = handler;
    }

    public void UnregisterHandler(EditorMode mode, IEditorModeInputHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        if (modeHandlers.TryGetValue(mode, out IEditorModeInputHandler registeredHandler) && ReferenceEquals(registeredHandler, handler))
        {
            modeHandlers.Remove(mode);
        }
    }

    public void RegisterGlobalHandler(IEditorModeInputHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        globalHandlers.Add(handler);
    }

    public void UnregisterGlobalHandler(IEditorModeInputHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        globalHandlers.Remove(handler);
    }

    private EditorInputFrame BuildInputFrame(EditorMode mode)
    {
        Vector2 pointerScreenPosition = Vector2.zero;
        Vector2 pointerDelta = Vector2.zero;
        bool hasPointer = inputProvider.IsPointerAvailable &&
                          inputProvider.TryGetPointerScreenPosition(out pointerScreenPosition);
        if (hasPointer)
        {
            inputProvider.TryGetPointerDelta(out pointerDelta);
        }

        bool pointerOverUI = hasPointer && inputProvider.IsPointerOverUI(EventSystem.current, uiRaycastResults);

        return new EditorInputFrame(
            mode,
            hasPointer,
            hasPointer ? pointerScreenPosition : Vector2.zero,
            pointerOverUI,
            inputProvider.WasPointerButtonPressedThisFrame(PointerButton.Left),
            inputProvider.WasPointerButtonReleasedThisFrame(PointerButton.Left),
            inputProvider.IsPointerButtonPressed(PointerButton.Left),
            inputProvider.WasPointerButtonPressedThisFrame(PointerButton.Middle),
            inputProvider.WasPointerButtonReleasedThisFrame(PointerButton.Middle),
            inputProvider.IsPointerButtonPressed(PointerButton.Middle),
            inputProvider.WasPointerButtonPressedThisFrame(PointerButton.Right),
            inputProvider.IsPointerButtonPressed(PointerButton.Right),
            pointerDelta,
            inputProvider.GetScrollDeltaY(),
            inputProvider.WasKeyPressedThisFrame(Key.Delete) || inputProvider.WasKeyPressedThisFrame(Key.Backspace),
            inputProvider.WasKeyPressedThisFrame(Key.Escape),
            inputProvider.WasKeyPressedThisFrame(Key.Q),
            inputProvider.IsKeyPressed(Key.Q),
            inputProvider.WasKeyPressedThisFrame(Key.E),
            inputProvider.IsKeyPressed(Key.E));
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            modeManager = FindFirstObjectByType<ModeManager>();
        }
    }
}
