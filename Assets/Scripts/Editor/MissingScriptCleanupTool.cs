#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class MissingScriptCleanupTool
{
    [MenuItem("Tools/LH/Remove Missing Scripts From Selection")]
    private static void RemoveMissingScriptsFromSelection()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Remove Missing Scripts",
                "Select one or more GameObjects or prefab assets first.",
                "OK");
            return;
        }

        int cleanedObjectCount = 0;
        int removedComponentCount = 0;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject selectedObject = selectedObjects[i];
            if (selectedObject == null)
            {
                continue;
            }

            int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(selectedObject);
            if (removedCount <= 0)
            {
                continue;
            }

            cleanedObjectCount++;
            removedComponentCount += removedCount;
            EditorUtility.SetDirty(selectedObject);

            if (PrefabUtility.IsPartOfPrefabAsset(selectedObject))
            {
                AssetDatabase.SaveAssetIfDirty(selectedObject);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Remove Missing Scripts",
            $"Cleaned {cleanedObjectCount} object(s), removed {removedComponentCount} missing component(s).",
            "OK");
    }

    [MenuItem("Tools/LH/Remove Missing Scripts From Selection", true)]
    private static bool ValidateRemoveMissingScriptsFromSelection()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}
#endif
