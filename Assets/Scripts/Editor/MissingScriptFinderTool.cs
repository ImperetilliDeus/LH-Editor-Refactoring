#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MissingScriptFinderTool
{
    [MenuItem("Tools/LH/Find Missing Scripts In Open Scenes")]
    private static void FindMissingScriptsInOpenScenes()
    {
        List<string> results = new List<string>();

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                CollectSceneMissingScripts(roots[i], scene.path, roots[i].name, results);
            }
        }

        ReportResults("Open Scenes", results);
    }

    [MenuItem("Tools/LH/Find Missing Scripts In Selection")]
    private static void FindMissingScriptsInSelection()
    {
        List<string> results = new List<string>();
        GameObject[] selectedObjects = Selection.gameObjects;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject selectedObject = selectedObjects[i];
            if (selectedObject == null)
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            string rootLabel = string.IsNullOrWhiteSpace(assetPath) ? selectedObject.name : assetPath;
            CollectSceneMissingScripts(selectedObject, assetPath, rootLabel, results);
        }

        ReportResults("Selection", results);
    }

    [MenuItem("Tools/LH/Find Missing Scripts In Selection", true)]
    private static bool ValidateFindMissingScriptsInSelection()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    private static void CollectSceneMissingScripts(GameObject target, string contextPath, string hierarchyPath, List<string> results)
    {
        if (target == null)
        {
            return;
        }

        int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
        if (missingCount > 0)
        {
            results.Add($"{contextPath} :: {hierarchyPath} ({missingCount} missing)");
        }

        Transform root = target.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            CollectSceneMissingScripts(
                child.gameObject,
                contextPath,
                $"{hierarchyPath}/{child.name}",
                results);
        }
    }

    private static void ReportResults(string scope, List<string> results)
    {
        if (results.Count == 0)
        {
            Debug.Log($"[MissingScriptFinder] No missing scripts found in {scope}.");
            EditorUtility.DisplayDialog("Find Missing Scripts", $"No missing scripts found in {scope}.", "OK");
            return;
        }

        string message =
            $"Found {results.Count} object(s) with missing scripts in {scope}.\n\n" +
            string.Join("\n", results);

        Debug.LogWarning($"[MissingScriptFinder]\n{message}");
        EditorUtility.DisplayDialog(
            "Find Missing Scripts",
            $"Found {results.Count} object(s) with missing scripts in {scope}. Check the Console for details.",
            "OK");
    }
}
#endif
