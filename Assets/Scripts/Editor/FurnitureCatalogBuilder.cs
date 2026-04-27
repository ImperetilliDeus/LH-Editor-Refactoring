#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class FurnitureCatalogBuilder : EditorWindow
{
    private const string DefaultAssetFolder = "Assets";
    private const string DefaultCatalogFolder = "Assets/Resources";
    private const string DefaultCatalogName = "FurnitureCatalog";
    private const string ThumbnailFolderSuffix = "_Thumbnails";

    private string furnitureAssetFolder = DefaultAssetFolder;
    private FurnitureCatalog outputCatalog;
    private Vector2 scrollPosition;
    private bool skipPrefabsWithMissingScripts = true;
    private bool autoCleanMissingScripts;
    private bool includeSubfolders = true;
    private string lastBuildSummary = string.Empty;
    private bool isBuilding;
    private string[] pendingPrefabGuids;
    private readonly List<FurnitureCatalogItem> pendingItems = new List<FurnitureCatalogItem>();
    private readonly List<string> pendingSkippedPaths = new List<string>();
    private int pendingCleanedPrefabCount;
    private int currentPrefabIndex;
    private GameObject currentPreviewPrefab;
    private string currentPreviewAssetPath = string.Empty;
    private double currentPreviewStartTime;
    private const double PreviewWaitTimeoutSeconds = 1.5d;

    [MenuItem("Tools/LH/Furniture Catalog Builder")]
    private static void OpenWindow()
    {
        FurnitureCatalogBuilder window = GetWindow<FurnitureCatalogBuilder>("Furniture Catalog");
        window.minSize = new Vector2(440f, 320f);
        window.Show();
    }

    private void OnEnable()
    {
        if (string.IsNullOrWhiteSpace(furnitureAssetFolder))
        {
            furnitureAssetFolder = DefaultAssetFolder;
        }

        EditorApplication.update -= ProcessBuildStep;
    }

    private void OnDisable()
    {
        StopBuildProcess();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Furniture Catalog Builder", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scan a prefab folder and create or rebuild a FurnitureCatalog asset.",
            MessageType.Info);

        DrawFolderSection();
        EditorGUILayout.Space(8f);
        DrawCatalogSection();
        EditorGUILayout.Space(12f);
        DrawOptionsSection();
        EditorGUILayout.Space(12f);
        DrawBuildSection();
        EditorGUILayout.Space(12f);
        DrawSummarySection();

        EditorGUILayout.EndScrollView();
    }

    private void DrawFolderSection()
    {
        EditorGUILayout.LabelField("1. Prefab Folder", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        furnitureAssetFolder = EditorGUILayout.TextField("Asset Folder", furnitureAssetFolder);
        if (GUILayout.Button("Select", GUILayout.Width(80f)))
        {
            SelectAssetFolder();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCatalogSection()
    {
        EditorGUILayout.LabelField("2. Output Catalog", EditorStyles.boldLabel);
        outputCatalog = (FurnitureCatalog)EditorGUILayout.ObjectField(
            "Catalog Asset",
            outputCatalog,
            typeof(FurnitureCatalog),
            false);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Create New Catalog", GUILayout.Width(160f)))
        {
            CreateNewCatalogAsset();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawOptionsSection()
    {
        EditorGUILayout.LabelField("3. Options", EditorStyles.boldLabel);
        skipPrefabsWithMissingScripts = EditorGUILayout.ToggleLeft(
            "Skip prefabs that still contain missing scripts",
            skipPrefabsWithMissingScripts);
        autoCleanMissingScripts = EditorGUILayout.ToggleLeft(
            "Auto-remove missing scripts from prefab assets before adding them",
            autoCleanMissingScripts);
        includeSubfolders = EditorGUILayout.ToggleLeft(
            "Scan subfolders recursively",
            includeSubfolders);

        EditorGUILayout.HelpBox(
            "Recommended: keep skipping enabled. If imported assets contain broken legacy components, they will be left out of the catalog instead of failing at placement time.",
            MessageType.None);
    }

    private void DrawBuildSection()
    {
        EditorGUILayout.LabelField("4. Build", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!CanBuild()))
        {
            if (GUILayout.Button("Rebuild Catalog", GUILayout.Height(34f)))
            {
                StartRebuildCatalog();
            }
        }

        if (isBuilding)
        {
            EditorGUILayout.HelpBox(
                $"Building catalog... {currentPrefabIndex}/{pendingPrefabGuids?.Length ?? 0}",
                MessageType.Info);
        }

        if (!CanBuild())
        {
            EditorGUILayout.HelpBox(
                "Select both a prefab folder and a catalog asset first.",
                MessageType.Warning);
        }
    }

    private void DrawSummarySection()
    {
        if (string.IsNullOrWhiteSpace(lastBuildSummary))
        {
            return;
        }

        EditorGUILayout.LabelField("5. Last Build", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(lastBuildSummary, MessageType.Info);
    }

    private void SelectAssetFolder()
    {
        string selectedPath = EditorUtility.OpenFolderPanel("Select Furniture Prefab Folder", Application.dataPath, string.Empty);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        string assetRelativePath = AbsoluteToAssetPath(selectedPath);
        if (string.IsNullOrWhiteSpace(assetRelativePath))
        {
            EditorUtility.DisplayDialog(
                "Invalid Folder",
                "Only folders inside the project's Assets directory can be selected.",
                "OK");
            return;
        }

        furnitureAssetFolder = assetRelativePath;
        Repaint();
    }

    private void CreateNewCatalogAsset()
    {
        string initialPath = DefaultCatalogFolder;
        if (!AssetDatabase.IsValidFolder(initialPath))
        {
            initialPath = "Assets";
        }

        string targetPath = EditorUtility.SaveFilePanelInProject(
            "Create Furniture Catalog",
            DefaultCatalogName,
            "asset",
            "Choose where to save the FurnitureCatalog asset.",
            initialPath);

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        FurnitureCatalog catalog = CreateInstance<FurnitureCatalog>();
        AssetDatabase.CreateAsset(catalog, targetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        outputCatalog = catalog;
        EditorGUIUtility.PingObject(outputCatalog);
    }

    private bool CanBuild()
    {
        return !string.IsNullOrWhiteSpace(furnitureAssetFolder) &&
               AssetDatabase.IsValidFolder(furnitureAssetFolder) &&
               outputCatalog != null;
    }

    private void StartRebuildCatalog()
    {
        if (!CanBuild())
        {
            return;
        }

        StopBuildProcess();
        pendingPrefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { furnitureAssetFolder });
        pendingItems.Clear();
        pendingSkippedPaths.Clear();
        pendingCleanedPrefabCount = 0;
        EnsureThumbnailFolderExists();
        currentPrefabIndex = 0;
        currentPreviewPrefab = null;
        currentPreviewAssetPath = string.Empty;
        currentPreviewStartTime = 0d;
        isBuilding = true;

        EditorApplication.update += ProcessBuildStep;
        Repaint();
    }

    private void ProcessBuildStep()
    {
        if (!isBuilding || pendingPrefabGuids == null)
        {
            StopBuildProcess();
            return;
        }

        if (currentPrefabIndex >= pendingPrefabGuids.Length)
        {
            FinalizeCatalogBuild();
            return;
        }

        string assetPath = AssetDatabase.GUIDToAssetPath(pendingPrefabGuids[currentPrefabIndex]);
        if (!includeSubfolders && !IsDirectChildAsset(assetPath, furnitureAssetFolder))
        {
            currentPrefabIndex++;
            return;
        }

        if (currentPreviewPrefab == null || currentPreviewAssetPath != assetPath)
        {
            PrefabInspectionResult inspectionResult = InspectPrefab(assetPath, autoCleanMissingScripts);
            if (inspectionResult.cleaned)
            {
                pendingCleanedPrefabCount++;
            }

            if (inspectionResult.missingScriptCount > 0 && skipPrefabsWithMissingScripts)
            {
                pendingSkippedPaths.Add($"{assetPath} ({inspectionResult.missingScriptCount} missing)");
                currentPrefabIndex++;
                return;
            }

            currentPreviewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            currentPreviewAssetPath = assetPath;
            currentPreviewStartTime = EditorApplication.timeSinceStartup;

            if (currentPreviewPrefab == null)
            {
                currentPrefabIndex++;
                currentPreviewAssetPath = string.Empty;
                return;
            }

            AssetPreview.GetAssetPreview(currentPreviewPrefab);
            Repaint();
            return;
        }

        Texture2D preview = AssetPreview.GetAssetPreview(currentPreviewPrefab);
        bool timedOut = EditorApplication.timeSinceStartup - currentPreviewStartTime >= PreviewWaitTimeoutSeconds;
        bool stillLoading = AssetPreview.IsLoadingAssetPreview(currentPreviewPrefab.GetInstanceID());

        if (preview == null && !timedOut && stillLoading)
        {
            Repaint();
            return;
        }

        if (preview == null)
        {
            preview = AssetPreview.GetMiniThumbnail(currentPreviewPrefab);
        }

        Texture2D thumbnailAsset = SaveThumbnailAsset(currentPreviewAssetPath, preview);

        pendingItems.Add(new FurnitureCatalogItem
        {
            code = Path.GetFileNameWithoutExtension(currentPreviewAssetPath),
            exportCode = Path.GetFileNameWithoutExtension(currentPreviewAssetPath),
            nativeCode = string.Empty,
            displayName = currentPreviewPrefab.name,
            prefab = currentPreviewPrefab,
            thumbnail = thumbnailAsset,
            placementOffset = Vector3.zero,
            defaultEulerAngles = Vector3.zero,
            boundsSize = EstimateBounds(currentPreviewPrefab),
            defects = new List<FurnitureDefectCatalogEntry>(),
        });

        currentPreviewPrefab = null;
        currentPreviewAssetPath = string.Empty;
        currentPreviewStartTime = 0d;
        currentPrefabIndex++;
        Repaint();
    }

    private void FinalizeCatalogBuild()
    {
        SerializedObject serializedCatalog = new SerializedObject(outputCatalog);
        SerializedProperty itemsProperty = serializedCatalog.FindProperty("items");
        itemsProperty.arraySize = pendingItems.Count;

        for (int i = 0; i < pendingItems.Count; i++)
        {
            SerializedProperty itemProperty = itemsProperty.GetArrayElementAtIndex(i);
            itemProperty.FindPropertyRelative("code").stringValue = pendingItems[i].code;
            itemProperty.FindPropertyRelative("exportCode").stringValue = pendingItems[i].exportCode;
            itemProperty.FindPropertyRelative("nativeCode").stringValue = pendingItems[i].nativeCode;
            itemProperty.FindPropertyRelative("displayName").stringValue = pendingItems[i].displayName;
            itemProperty.FindPropertyRelative("prefab").objectReferenceValue = pendingItems[i].prefab;
            itemProperty.FindPropertyRelative("thumbnail").objectReferenceValue = pendingItems[i].thumbnail;
            itemProperty.FindPropertyRelative("placementOffset").vector3Value = pendingItems[i].placementOffset;
            itemProperty.FindPropertyRelative("defaultEulerAngles").vector3Value = pendingItems[i].defaultEulerAngles;
            itemProperty.FindPropertyRelative("boundsSize").vector3Value = pendingItems[i].boundsSize;
            itemProperty.FindPropertyRelative("defects").arraySize = 0;
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(outputCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        lastBuildSummary = BuildSummary(pendingItems.Count, pendingSkippedPaths, pendingCleanedPrefabCount);
        Debug.Log(lastBuildSummary);
        EditorGUIUtility.PingObject(outputCatalog);
        StopBuildProcess();

        EditorUtility.DisplayDialog(
            "Furniture Catalog Rebuilt",
            lastBuildSummary,
            "OK");
    }

    private void StopBuildProcess()
    {
        EditorApplication.update -= ProcessBuildStep;
        isBuilding = false;
        currentPreviewPrefab = null;
        currentPreviewAssetPath = string.Empty;
        currentPreviewStartTime = 0d;
    }

    private static bool IsDirectChildAsset(string assetPath, string assetFolder)
    {
        string normalizedAssetPath = assetPath.Replace('\\', '/');
        string normalizedFolder = assetFolder.Replace('\\', '/').TrimEnd('/');
        string assetDirectory = Path.GetDirectoryName(normalizedAssetPath)?.Replace('\\', '/') ?? string.Empty;
        return string.Equals(assetDirectory, normalizedFolder);
    }

    private static PrefabInspectionResult InspectPrefab(string assetPath, bool cleanMissingScripts)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
        try
        {
            int missingScriptCount = CountMissingScriptsRecursively(prefabRoot);
            bool cleaned = false;

            if (missingScriptCount > 0 && cleanMissingScripts)
            {
                cleaned = RemoveMissingScriptsRecursively(prefabRoot) > 0;
                if (cleaned)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                    missingScriptCount = CountMissingScriptsRecursively(prefabRoot);
                }
            }

            return new PrefabInspectionResult(missingScriptCount, cleaned);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static int CountMissingScriptsRecursively(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            count += CountMissingScriptsRecursively(rootTransform.GetChild(i).gameObject);
        }

        return count;
    }

    private static int RemoveMissingScriptsRecursively(GameObject root)
    {
        if (root == null)
        {
            return 0;
        }

        int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            removed += RemoveMissingScriptsRecursively(rootTransform.GetChild(i).gameObject);
        }

        return removed;
    }

    private static string BuildSummary(int addedCount, List<string> skippedPaths, int cleanedPrefabCount)
    {
        if (skippedPaths.Count == 0)
        {
            return cleanedPrefabCount > 0
                ? $"Added {addedCount} prefab(s). Cleaned missing scripts from {cleanedPrefabCount} prefab asset(s)."
                : $"Added {addedCount} prefab(s).";
        }

        string skippedList = string.Join("\n", skippedPaths);
        return
            $"Added {addedCount} prefab(s).\n" +
            $"Skipped {skippedPaths.Count} prefab(s) with missing scripts.\n" +
            (cleanedPrefabCount > 0 ? $"Cleaned {cleanedPrefabCount} prefab asset(s) before adding.\n" : string.Empty) +
            skippedList;
    }

    private static string AbsoluteToAssetPath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return string.Empty;
        }

        string normalizedAbsolute = absolutePath.Replace('\\', '/');
        string normalizedAssetsRoot = Application.dataPath.Replace('\\', '/');
        if (!normalizedAbsolute.StartsWith(normalizedAssetsRoot))
        {
            return string.Empty;
        }

        return "Assets" + normalizedAbsolute.Substring(normalizedAssetsRoot.Length);
    }

    private static Vector3 EstimateBounds(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return Vector3.one;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.size;
    }

    private void EnsureThumbnailFolderExists()
    {
        string folderPath = GetThumbnailFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string[] parts = folderPath.Split('/');
        string currentPath = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }

    private string GetThumbnailFolderPath()
    {
        if (outputCatalog == null)
        {
            return string.Empty;
        }

        string catalogPath = AssetDatabase.GetAssetPath(outputCatalog);
        if (string.IsNullOrWhiteSpace(catalogPath))
        {
            return string.Empty;
        }

        string catalogDirectory = Path.GetDirectoryName(catalogPath)?.Replace('\\', '/') ?? "Assets";
        string catalogName = Path.GetFileNameWithoutExtension(catalogPath);
        return $"{catalogDirectory}/{catalogName}{ThumbnailFolderSuffix}";
    }

    private Texture2D SaveThumbnailAsset(string prefabAssetPath, Texture2D previewTexture)
    {
        if (previewTexture == null)
        {
            return null;
        }

        string folderPath = GetThumbnailFolderPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return previewTexture;
        }

        string thumbnailName = $"{Path.GetFileNameWithoutExtension(prefabAssetPath)}.png";
        string assetPath = $"{folderPath}/{thumbnailName}";
        Texture2D readableTexture = CreateReadableCopy(previewTexture);
        if (readableTexture == null)
        {
            return previewTexture;
        }

        byte[] pngBytes = readableTexture.EncodeToPNG();
        Object.DestroyImmediate(readableTexture);
        if (pngBytes == null || pngBytes.Length == 0)
        {
            return previewTexture;
        }

        string absolutePath = Path.GetFullPath(assetPath);
        File.WriteAllBytes(absolutePath, pngBytes);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static Texture2D CreateReadableCopy(Texture source)
    {
        if (source == null)
        {
            return null;
        }

        int width = Mathf.Max(1, source.width);
        int height = Mathf.Max(1, source.height);
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;

        try
        {
            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;
            Texture2D copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            copy.Apply();
            return copy;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    private readonly struct PrefabInspectionResult
    {
        public PrefabInspectionResult(int missingScriptCount, bool cleaned)
        {
            this.missingScriptCount = missingScriptCount;
            this.cleaned = cleaned;
        }

        public int missingScriptCount { get; }
        public bool cleaned { get; }
    }
}
#endif
