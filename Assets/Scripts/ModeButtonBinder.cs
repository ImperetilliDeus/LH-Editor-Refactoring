using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ModeButtonBinder : MonoBehaviour
{
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private EditorMode targetMode = EditorMode.Default;
    [SerializeField] private Button targetButton;

    private void Awake()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        if (modeManager == null)
        {
            LayerUtility.ResolveObject(ref modeManager);
        }

        modeManager?.RegisterModeButton(targetButton, targetMode);
    }

    private void OnDestroy()
    {
        modeManager?.UnregisterModeButton(targetButton);
    }
}
