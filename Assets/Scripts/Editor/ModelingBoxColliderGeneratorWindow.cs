#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class ModelingBoxColliderGeneratorWindow : EditorWindow
{
    private string prefabFolder = "Assets/Prefabs/Furniture/Models/Prefabs";
    private bool includeSubfolders = true;
    private ModelingBoxColliderGenerator.Options options = ModelingBoxColliderGenerator.Options.Default;
    private Vector2 scrollPosition;
    private string lastSummary = string.Empty;

    [MenuItem("LH/Modeling/Box Collider Generator")]
    public static void Open()
    {
        ModelingBoxColliderGeneratorWindow window = GetWindow<ModelingBoxColliderGeneratorWindow>("Modeling Colliders");
        window.minSize = new Vector2(440f, 300f);
        window.Show();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Generate Modeling Box Colliders", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Adds BoxCollider components to generated modeling parts. The default filter targets Fixed_/Stretch_ parts from the parametric modeling rules.",
            MessageType.Info);

        DrawSelectionSection();
        EditorGUILayout.Space(8f);
        DrawOptionsSection();
        EditorGUILayout.Space(12f);
        DrawActionSection();
        EditorGUILayout.Space(12f);
        DrawSummarySection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawSelectionSection()
    {
        EditorGUILayout.LabelField("1. Target", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            prefabFolder = EditorGUILayout.TextField("Prefab Folder", prefabFolder);
            if (GUILayout.Button("Select", GUILayout.Width(80f)))
            {
                SelectPrefabFolder();
            }
        }

        includeSubfolders = EditorGUILayout.ToggleLeft("Include subfolders", includeSubfolders);
    }

    private void DrawOptionsSection()
    {
        EditorGUILayout.LabelField("2. Options", EditorStyles.boldLabel);
        options.parametricPartsOnly = EditorGUILayout.ToggleLeft("Only Fixed_/Stretch_ modeling parts", options.parametricPartsOnly);
        options.skipGlassParts = EditorGUILayout.ToggleLeft("Skip glass parts", options.skipGlassParts);
        options.skipInactiveObjects = EditorGUILayout.ToggleLeft("Skip inactive objects", options.skipInactiveObjects);
        options.overwriteExisting = EditorGUILayout.ToggleLeft("Overwrite existing BoxCollider bounds", options.overwriteExisting);
        options.isTrigger = EditorGUILayout.ToggleLeft("Create as trigger colliders", options.isTrigger);
    }

    private void DrawActionSection()
    {
        EditorGUILayout.LabelField("3. Generate", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!CanProcessFolder()))
        {
            if (GUILayout.Button("Generate For Prefab Folder", GUILayout.Height(32f)))
            {
                GenerateForPrefabFolder();
            }
        }

        using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
        {
            if (GUILayout.Button("Generate For Selected Objects Or Prefab Assets", GUILayout.Height(28f)))
            {
                GenerateForSelectedObjectsOrPrefabAssets();
            }
        }

        if (!CanProcessFolder())
        {
            EditorGUILayout.HelpBox("Select a valid prefab folder inside Assets.", MessageType.Warning);
        }
    }

    private void DrawSummarySection()
    {
        if (string.IsNullOrWhiteSpace(lastSummary))
        {
            return;
        }

        EditorGUILayout.LabelField("4. Last Result", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(lastSummary, MessageType.Info);
    }

    private void SelectPrefabFolder()
    {
        string selectedPath = EditorUtility.OpenFolderPanel("Select Prefab Folder", Application.dataPath, string.Empty);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        string assetPath = AbsoluteToAssetPath(selectedPath);
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            EditorUtility.DisplayDialog(
                "Invalid Folder",
                "Only folders inside this project's Assets directory can be selected.",
                "OK");
            return;
        }

        prefabFolder = assetPath;
        Repaint();
    }

    private bool CanProcessFolder()
    {
        return !string.IsNullOrWhiteSpace(prefabFolder) && AssetDatabase.IsValidFolder(prefabFolder);
    }

    private void GenerateForPrefabFolder()
    {
        if (!CanProcessFolder())
        {
            return;
        }

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabFolder });
        ModelingBoxColliderGenerator.Result total = ModelingBoxColliderGenerator.Result.Empty;
        List<string> changedPaths = new List<string>();

        try
        {
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!includeSubfolders && !IsDirectChildAsset(assetPath, prefabFolder))
                {
                    continue;
                }

                EditorUtility.DisplayProgressBar(
                    "Generating Box Colliders",
                    assetPath,
                    prefabGuids.Length > 0 ? (float)i / prefabGuids.Length : 1f);

                ModelingBoxColliderGenerator.Result result = ModelingBoxColliderGenerator.GenerateForPrefabAsset(assetPath, options);
                total.Add(result);
                if (result.changed)
                {
                    changedPaths.Add(assetPath);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        lastSummary = BuildSummary(changedPaths.Count, total);
        Debug.Log(lastSummary);
        EditorUtility.DisplayDialog("Box Collider Generation Complete", lastSummary, "OK");
    }

    private void GenerateForSelectedObjectsOrPrefabAssets()
    {
        ModelingBoxColliderGenerator.Result total = ModelingBoxColliderGenerator.Result.Empty;
        GameObject[] selectedObjects = Selection.gameObjects;
        int prefabAssetCount = 0;
        int sceneObjectCount = 0;
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Generate Modeling Box Colliders");
        int undoGroup = Undo.GetCurrentGroup();

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject selectedObject = selectedObjects[i];
            if (selectedObject == null)
            {
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (!string.IsNullOrWhiteSpace(assetPath) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == selectedObject)
            {
                total.Add(ModelingBoxColliderGenerator.GenerateForPrefabAsset(assetPath, options));
                prefabAssetCount++;
                continue;
            }

            total.Add(ModelingBoxColliderGenerator.Generate(selectedObject, options, true));
            sceneObjectCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        lastSummary =
            BuildSummary(selectedObjects.Length, total) +
            $"\nPrefab asset target(s): {prefabAssetCount}" +
            $"\nScene object target(s): {sceneObjectCount}";
        Debug.Log(lastSummary);
        Repaint();
    }

    private static string BuildSummary(int targetCount, ModelingBoxColliderGenerator.Result result)
    {
        return
            $"Processed {targetCount} target(s).\n" +
            $"Added {result.added} BoxCollider(s).\n" +
            $"Updated {result.updated} BoxCollider(s).\n" +
            $"Skipped {result.skipped} object(s).";
    }

    private static bool IsDirectChildAsset(string assetPath, string assetFolder)
    {
        string normalizedAssetPath = assetPath.Replace('\\', '/');
        string normalizedFolder = assetFolder.Replace('\\', '/').TrimEnd('/');
        string assetDirectory = System.IO.Path.GetDirectoryName(normalizedAssetPath)?.Replace('\\', '/') ?? string.Empty;
        return string.Equals(assetDirectory, normalizedFolder, System.StringComparison.Ordinal);
    }

    private static string AbsoluteToAssetPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return string.Empty;
        }

        string normalizedAbsolute = absolutePath.Replace('\\', '/');
        string normalizedAssetsRoot = Application.dataPath.Replace('\\', '/');
        if (!normalizedAbsolute.StartsWith(normalizedAssetsRoot, System.StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return "Assets" + normalizedAbsolute.Substring(normalizedAssetsRoot.Length);
    }
}
#endif
