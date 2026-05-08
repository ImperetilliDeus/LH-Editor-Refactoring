#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneReferenceRegistryInstallerEditor
{
    [MenuItem("LH/Setup/Install Scene Reference Registry")]
    private static void Install()
    {
        SceneReferenceRegistry registry = Object.FindFirstObjectByType<SceneReferenceRegistry>(FindObjectsInactive.Include);
        if (registry == null)
        {
            registry = new GameObject("SceneReferenceRegistry").AddComponent<SceneReferenceRegistry>();
        }

        Undo.RegisterCreatedObjectUndo(registry.gameObject, "Install Scene Reference Registry");
        SceneReferenceRegistryInstaller.ApplyDefaults(registry);
        EditorUtility.SetDirty(registry);
        EditorSceneManager.MarkSceneDirty(registry.gameObject.scene);
        Selection.activeObject = registry.gameObject;
    }
}
#endif
