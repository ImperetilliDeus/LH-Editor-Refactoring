using UnityEngine;
using UnityEngine.UI;

public static class SceneReferenceRegistryBootstrap
{
    private const string RegistryObjectName = "SceneReferenceRegistry";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRegistry()
    {
        SceneReferenceRegistry registry = Object.FindFirstObjectByType<SceneReferenceRegistry>(FindObjectsInactive.Include);
        if (registry == null)
        {
            registry = new GameObject(RegistryObjectName).AddComponent<SceneReferenceRegistry>();
        }

        SceneReferenceRegistryInstaller.ApplyDefaults(registry);
    }
}
