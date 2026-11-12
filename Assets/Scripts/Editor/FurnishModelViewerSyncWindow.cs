using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class FurnishModelViewerSyncWindow : EditorWindow
{
    private const string DefaultViewerProjectPath = @"E:\Unity\LHM_260212";
    private const string EditorPrefabRoot = "Assets/Prefabs/Furniture/Models/Prefabs";
    private const string EditorMaterialRoot = "Assets/Prefabs/Furniture/Models/Materials";
    private const string EditorTextureRoot = "Assets/Prefabs/Furniture/Models/Textures";
    private const string EditorShaderRoot = "Assets/Shaders";
    private const string ViewerPrefabRoot = "Assets/Test/Models/Prefabs";
    private const string ViewerMaterialRoot = "Assets/Test/Models/Materials";
    private const string ViewerTextureRoot = "Assets/Test/Models/Textures";
    private const string ViewerShaderRoot = "Assets/Test/Models/Shaders";
    private const string ViewerFurnishPresetPath = "Assets/Test/Models/Prefabs/Furnish.asset";
    private const string ViewerPrefabPresetScriptGuid = "94c2e4df819ef8841bd2fdf1544f4f68";

    [SerializeField] private string viewerProjectPath = DefaultViewerProjectPath;
    [SerializeField] private FurnitureCatalog catalog;
    [SerializeField] private string furnishCode = "SC001";
    [SerializeField] private bool syncAllFurnishModels;

    [MenuItem("LH/Furnish Models/Sync Furnish Models To Viewer")]
    public static void Open()
    {
        GetWindow<FurnishModelViewerSyncWindow>("Furnish Model Sync");
    }

    [MenuItem("LH/Furnish Models/Sync SC001 To Viewer")]
    public static void SyncSC001ToDefaultViewer()
    {
        FurnishModelViewerSyncWindow window = CreateInstance<FurnishModelViewerSyncWindow>();
        window.viewerProjectPath = DefaultViewerProjectPath;
        window.catalog = LoadDefaultCatalog();
        window.furnishCode = "SC001";
        window.syncAllFurnishModels = false;
        window.Sync();
        DestroyImmediate(window);
    }

    [MenuItem("LH/Furnish Models/Sync BO001 To Viewer")]
    public static void SyncBO001ToDefaultViewer()
    {
        FurnishModelViewerSyncWindow window = CreateInstance<FurnishModelViewerSyncWindow>();
        window.viewerProjectPath = DefaultViewerProjectPath;
        window.catalog = LoadDefaultCatalog();
        window.furnishCode = "BO001";
        window.syncAllFurnishModels = false;
        window.Sync();
        DestroyImmediate(window);
    }

    [MenuItem("LH/Furnish Models/Sync All Furnish Models To Viewer")]
    public static void SyncAllToDefaultViewer()
    {
        FurnishModelViewerSyncWindow window = CreateInstance<FurnishModelViewerSyncWindow>();
        window.viewerProjectPath = DefaultViewerProjectPath;
        window.catalog = LoadDefaultCatalog();
        window.syncAllFurnishModels = true;
        window.Sync();
        DestroyImmediate(window);
    }

    private void OnEnable()
    {
        if (catalog == null)
        {
            catalog = LoadDefaultCatalog();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sync Furnish Models To LH Viewer", EditorStyles.boldLabel);
        viewerProjectPath = EditorGUILayout.TextField("Viewer Project Path", viewerProjectPath);
        catalog = (FurnitureCatalog)EditorGUILayout.ObjectField("Furniture Catalog", catalog, typeof(FurnitureCatalog), false);
        syncAllFurnishModels = EditorGUILayout.Toggle("Sync All Furnish Models", syncAllFurnishModels);

        using (new EditorGUI.DisabledScope(syncAllFurnishModels))
        {
            furnishCode = EditorGUILayout.TextField("Furnish Code", furnishCode);
        }

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(viewerProjectPath) || catalog == null))
        {
            if (GUILayout.Button(syncAllFurnishModels ? "Sync All Furnish Models" : "Sync Selected Furnish Code"))
            {
                Sync();
            }
        }
    }

    private void Sync()
    {
        string viewerAssetsPath = Path.Combine(viewerProjectPath, "Assets");
        if (!Directory.Exists(viewerAssetsPath))
        {
            EditorUtility.DisplayDialog("Sync failed", $"Viewer Assets folder not found:\n{viewerAssetsPath}", "OK");
            return;
        }

        if (catalog == null || catalog.Items == null)
        {
            EditorUtility.DisplayDialog("Sync failed", "FurnitureCatalog is not assigned.", "OK");
            return;
        }

        try
        {
            int count = 0;
            IReadOnlyList<FurnitureCatalogItem> items = catalog.Items;
            for (int i = 0; i < items.Count; i++)
            {
                FurnitureCatalogItem item = items[i];
                if (!ShouldSync(item))
                {
                    continue;
                }

                SyncItem(item);
                count++;
            }

            if (count == 0)
            {
                EditorUtility.DisplayDialog("Sync complete", "No matching furnish model was found.", "OK");
                return;
            }

            Debug.Log($"Synced {count} furnish model(s) to viewer: {viewerProjectPath}");
            EditorUtility.DisplayDialog("Sync complete", $"Synced {count} furnish model(s) to LH Viewer.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Sync failed", ex.Message, "OK");
        }
    }

    private bool ShouldSync(FurnitureCatalogItem item)
    {
        if (item == null || item.prefab == null)
        {
            return false;
        }

        if (syncAllFurnishModels)
        {
            return true;
        }

        string resolvedCode = ResolveSyncCode(item);
        return string.Equals(resolvedCode, furnishCode, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(item.code, furnishCode, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(item.exportCode, furnishCode, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(item.nativeCode, furnishCode, StringComparison.OrdinalIgnoreCase);
    }

    private void SyncItem(FurnitureCatalogItem item)
    {
        string code = ResolveSyncCode(item);
        string prefabPath = AssetDatabase.GetAssetPath(item.prefab);
        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            throw new InvalidOperationException($"Prefab path not found for {code}.");
        }

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item.prefab, out string prefabGuid, out long localId))
        {
            throw new InvalidOperationException($"Prefab GUID not found for {code}: {prefabPath}");
        }

        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        for (int i = 0; i < dependencies.Length; i++)
        {
            CopyFurnishDependency(dependencies[i]);
        }

        string copiedPrefabPath = ResolveViewerAssetPath(prefabPath);
        long viewerLocalId = ResolveCopiedPrefabRootLocalId(copiedPrefabPath, localId);
        UpdateViewerFurnishPreset(code, prefabGuid, viewerLocalId);
        WriteManifest(item, code, prefabGuid, viewerLocalId, copiedPrefabPath);
    }

    private void CopyFurnishDependency(string dependencyAssetPath)
    {
        if (string.IsNullOrWhiteSpace(dependencyAssetPath) ||
            dependencyAssetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string targetAssetPath = ResolveViewerAssetPath(dependencyAssetPath);
        if (string.IsNullOrWhiteSpace(targetAssetPath))
        {
            return;
        }

        CopyAssetAndMeta(dependencyAssetPath, targetAssetPath);
    }

    private string ResolveViewerAssetPath(string sourceAssetPath)
    {
        string sourceGuid = ReadMetaGuid(ToAbsoluteEditorPath(sourceAssetPath) + ".meta");
        string existingViewerPath = FindViewerAssetPathByGuid(sourceGuid);
        if (!string.IsNullOrWhiteSpace(existingViewerPath))
        {
            return existingViewerPath;
        }

        if (sourceAssetPath.StartsWith(EditorPrefabRoot, StringComparison.OrdinalIgnoreCase))
        {
            return ViewerPrefabRoot + sourceAssetPath.Substring(EditorPrefabRoot.Length);
        }

        if (sourceAssetPath.StartsWith(EditorMaterialRoot, StringComparison.OrdinalIgnoreCase))
        {
            return ViewerMaterialRoot + sourceAssetPath.Substring(EditorMaterialRoot.Length);
        }

        if (sourceAssetPath.StartsWith(EditorTextureRoot, StringComparison.OrdinalIgnoreCase))
        {
            return ViewerTextureRoot + sourceAssetPath.Substring(EditorTextureRoot.Length);
        }

        if (sourceAssetPath.StartsWith(EditorShaderRoot, StringComparison.OrdinalIgnoreCase))
        {
            return ViewerShaderRoot + sourceAssetPath.Substring(EditorShaderRoot.Length);
        }

        return string.Empty;
    }

    private void CopyAssetAndMeta(string sourceAssetPath, string targetAssetPath)
    {
        string sourcePath = ToAbsoluteEditorPath(sourceAssetPath);
        string targetPath = ToAbsoluteViewerPath(targetAssetPath);
        if (!File.Exists(sourcePath))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
        File.Copy(sourcePath, targetPath, true);

        string sourceMetaPath = sourcePath + ".meta";
        if (File.Exists(sourceMetaPath))
        {
            File.Copy(sourceMetaPath, targetPath + ".meta", true);
        }
    }

    private long ResolveCopiedPrefabRootLocalId(string viewerAssetPath, long fallbackLocalId)
    {
        string absolutePath = ToAbsoluteViewerPath(viewerAssetPath);
        if (!File.Exists(absolutePath) ||
            !absolutePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            return fallbackLocalId;
        }

        string text = File.ReadAllText(absolutePath);
        Match rootMatch = Regex.Match(
            text,
            @"(?m)^--- !u!1 &(?<fileId>-?\d+)(?: stripped)?\r?\nGameObject:");
        if (rootMatch.Success &&
            long.TryParse(rootMatch.Groups["fileId"].Value, out long copiedLocalId))
        {
            return copiedLocalId;
        }

        return fallbackLocalId;
    }

    private void UpdateViewerFurnishPreset(string code, string prefabGuid, long localId)
    {
        string absolutePresetPath = ToAbsoluteViewerPath(ViewerFurnishPresetPath);
        List<PresetEntry> entries = ReadPresetEntries(absolutePresetPath);
        bool replaced = false;
        for (int i = 0; i < entries.Count; i++)
        {
            if (!string.Equals(entries[i].Name, code, StringComparison.Ordinal))
            {
                continue;
            }

            entries[i] = new PresetEntry(code, prefabGuid, localId);
            replaced = true;
            break;
        }

        if (!replaced)
        {
            entries.Add(new PresetEntry(code, prefabGuid, localId));
        }

        WritePreset(absolutePresetPath, "Furnish", entries);
    }

    private static List<PresetEntry> ReadPresetEntries(string presetPath)
    {
        List<PresetEntry> entries = new List<PresetEntry>();
        if (!File.Exists(presetPath))
        {
            return entries;
        }

        string text = File.ReadAllText(presetPath);
        MatchCollection matches = Regex.Matches(
            text,
            @"-\s+prefabName:\s*(?<name>[^\r\n]+)\s+prefab:\s*\{fileID:\s*(?<fileID>-?\d+),\s*guid:\s*(?<guid>[0-9a-fA-F]{32}),\s*type:\s*3\}",
            RegexOptions.Multiline);
        foreach (Match match in matches)
        {
            string name = match.Groups["name"].Value.Trim().Trim('"');
            string guid = match.Groups["guid"].Value.Trim();
            long fileId = long.TryParse(match.Groups["fileID"].Value, out long parsed) ? parsed : 0L;
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(guid))
            {
                entries.Add(new PresetEntry(name, guid, fileId));
            }
        }

        return entries;
    }

    private static void WritePreset(string presetPath, string presetName, List<PresetEntry> entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(presetPath));
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("%YAML 1.1");
        builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
        builder.AppendLine("--- !u!114 &11400000");
        builder.AppendLine("MonoBehaviour:");
        builder.AppendLine("  m_ObjectHideFlags: 0");
        builder.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
        builder.AppendLine("  m_PrefabInstance: {fileID: 0}");
        builder.AppendLine("  m_PrefabAsset: {fileID: 0}");
        builder.AppendLine("  m_GameObject: {fileID: 0}");
        builder.AppendLine("  m_Enabled: 1");
        builder.AppendLine("  m_EditorHideFlags: 0");
        builder.AppendLine($"  m_Script: {{fileID: 11500000, guid: {ViewerPrefabPresetScriptGuid}, type: 3}}");
        builder.AppendLine($"  m_Name: {presetName}");
        builder.AppendLine("  m_EditorClassIdentifier: ");
        builder.AppendLine("  prefabs:");
        for (int i = 0; i < entries.Count; i++)
        {
            builder.AppendLine($"  - prefabName: {entries[i].Name}");
            builder.AppendLine($"    prefab: {{fileID: {entries[i].LocalFileId}, guid: {entries[i].Guid}, type: 3}}");
        }

        File.WriteAllText(presetPath, builder.ToString(), Encoding.UTF8);
    }

    private void WriteManifest(FurnitureCatalogItem item, string code, string prefabGuid, long localId, string viewerPrefabPath)
    {
        string manifestPath = ToAbsoluteViewerPath($"Assets/Test/Models/Prefabs/FurnishModelManifests/{code}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
        string json = JsonUtility.ToJson(new FurnishModelManifest
        {
            code = code,
            catalogCode = item.code,
            exportCode = item.exportCode,
            nativeCode = item.nativeCode,
            displayName = item.displayName,
            prefabGuid = prefabGuid,
            prefabLocalFileId = localId.ToString(),
            viewerPrefabPath = viewerPrefabPath,
            placementOffset = item.placementOffset,
            defaultEulerAngles = item.defaultEulerAngles,
            boundsSize = item.boundsSize,
            scaleMultiplier = item.ResolvedScaleMultiplier,
        }, true);
        File.WriteAllText(manifestPath, json, Encoding.UTF8);
    }

    private string FindViewerAssetPathByGuid(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return string.Empty;
        }

        string viewerAssetsPath = Path.Combine(viewerProjectPath, "Assets");
        if (!Directory.Exists(viewerAssetsPath))
        {
            return string.Empty;
        }

        string[] metaFiles = Directory.GetFiles(viewerAssetsPath, "*.meta", SearchOption.AllDirectories);
        for (int i = 0; i < metaFiles.Length; i++)
        {
            string candidateGuid = ReadMetaGuid(metaFiles[i]);
            if (!string.Equals(candidateGuid, guid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string assetPath = metaFiles[i].Substring(0, metaFiles[i].Length - ".meta".Length);
            string relative = assetPath.Substring(viewerProjectPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        return string.Empty;
    }

    private static string ResolveSyncCode(FurnitureCatalogItem item)
    {
        if (item == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(item.exportCode))
        {
            return item.exportCode;
        }

        return item.code ?? string.Empty;
    }

    private static string ReadMetaGuid(string metaPath)
    {
        if (!File.Exists(metaPath))
        {
            return string.Empty;
        }

        string[] lines = File.ReadAllLines(metaPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("guid:", StringComparison.Ordinal))
            {
                return line.Substring("guid:".Length).Trim();
            }
        }

        return string.Empty;
    }

    private static string ToAbsoluteEditorPath(string assetPath)
    {
        return Path.GetFullPath(assetPath);
    }

    private string ToAbsoluteViewerPath(string assetPath)
    {
        string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(viewerProjectPath, relativePath);
    }

    private static FurnitureCatalog LoadDefaultCatalog()
    {
        return AssetDatabase.LoadAssetAtPath<FurnitureCatalog>("Assets/Prefabs/Furniture/FurnitureCatalog.asset");
    }

    private readonly struct PresetEntry
    {
        public PresetEntry(string name, string guid, long localFileId)
        {
            Name = name;
            Guid = guid;
            LocalFileId = localFileId;
        }

        public string Name { get; }
        public string Guid { get; }
        public long LocalFileId { get; }
    }

    [Serializable]
    private sealed class FurnishModelManifest
    {
        public string code;
        public string catalogCode;
        public string exportCode;
        public string nativeCode;
        public string displayName;
        public string prefabGuid;
        public string prefabLocalFileId;
        public string viewerPrefabPath;
        public Vector3 placementOffset;
        public Vector3 defaultEulerAngles;
        public Vector3 boundsSize;
        public float scaleMultiplier;
    }
}
