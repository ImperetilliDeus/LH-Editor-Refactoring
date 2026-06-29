using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ExitButtonController : MonoBehaviour
{
    public static Action QuitApplication = DefaultQuitApplication;

    private Button targetButton;

    private void Awake()
    {
        targetButton = GetComponent<Button>();
        targetButton.onClick.AddListener(RequestQuit);
    }

    private void OnDestroy()
    {
        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(RequestQuit);
        }
    }

    public static void DefaultQuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void RequestQuit()
    {
        Action quitApplication = QuitApplication ?? DefaultQuitApplication;
        quitApplication.Invoke();
    }
}
