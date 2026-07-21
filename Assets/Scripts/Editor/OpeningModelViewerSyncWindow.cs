using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class OpeningModelViewerSyncWindow : EditorWindow
{
    private const string DefaultViewerProjectPath = @"E:\Unity\LHM_260212";
    private const string CatalogResourcePath = "OpeningTypeCatalog";
    private const string EditorPrefabRoot = "Assets/Prefabs/Furniture/Models/Prefabs";
    private const string EditorMaterialRoot = "Assets/Prefabs/Furniture/Models/Materials";
    private const string EditorTextureRoot = "Assets/Prefabs/Furniture/Models/Textures";
    private const string EditorShaderRoot = "Assets/Shaders";
    private const string ViewerPrefabRoot = "Assets/Test/Models/Prefabs";
    private const string ViewerMaterialRoot = "Assets/Test/Models/Materials";
    private const string ViewerTextureRoot = "Assets/Test/Models/Textures";
    private const string ViewerShaderRoot = "Assets/Test/Models/Shaders";
    private const string ViewerDoorPresetPath = "Assets/Test/Models/Prefabs/Door.asset";
    private const string ViewerWindowPresetPath = "Assets/Test/Models/Prefabs/Window.asset";
    private const string ViewerParametricScriptPath = "Assets/Scripts/Room/Opening/ParametricOpeningModel.cs";
    private const string EditorParametricScriptPath = "Assets/Scripts/Draw/Wall/Opening/ParametricOpeningModel.cs";
    private const string EditorParametricScriptGuid = "91004903b3cdea44c8ac403bf2e7b5f1";
    private const string ViewerParametricScriptGuid = "a1f49ab6c19f4c93a75dbd1cc80bd402";
    private const string ViewerPrefabPresetScriptGuid = "94c2e4df819ef8841bd2fdf1544f4f68";

    [SerializeField] private string viewerProjectPath = DefaultViewerProjectPath;
    [SerializeField] private string typeKey = "B001";
    [SerializeField] private bool syncRuntimeScript = true;
    [SerializeField] private bool syncAllParametricModels;

    [MenuItem("LH/Opening Models/Sync Opening Models To Viewer")]
    public static void Open()
    {
        GetWindow<OpeningModelViewerSyncWindow>("Opening Model Sync");
    }

    [MenuItem("LH/Opening Models/Sync All Parametric Models To Viewer")]
    public static void SyncAllParametricModelsToDefaultViewer()
    {
        OpeningModelViewerSyncWindow window = CreateInstance<OpeningModelViewerSyncWindow>();
        window.viewerProjectPath = DefaultViewerProjectPath;
        window.syncRuntimeScript = true;
        window.syncAllParametricModels = true;
        window.Sync();
        DestroyImmediate(window);
    }

    [MenuItem("LH/Opening Models/Sync B001 To Viewer")]
    public static void SyncB001ToDefaultViewer()
    {
        OpeningModelViewerSyncWindow window = CreateInstance<OpeningModelViewerSyncWindow>();
        window.viewerProjectPath = DefaultViewerProjectPath;
        window.syncRuntimeScript = true;
        window.syncAllParametricModels = false;
        window.typeKey = "B001";
        window.Sync();
        DestroyImmediate(window);
    }

    [MenuItem("LH/Opening Models/Sync E003 To Viewer")]
    public static void SyncE003ToDefaultViewer()
    {
        OpeningModelViewerSyncWindow window = CreateInstance<OpeningModelViewerSyncWindow>();
        window.viewerProjectPath = DefaultViewerProjectPath;
        window.syncRuntimeScript = true;
        window.syncAllParametricModels = false;
        window.typeKey = "E003";
        window.Sync();
        DestroyImmediate(window);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sync Opening Models To LH Viewer", EditorStyles.boldLabel);
        viewerProjectPath = EditorGUILayout.TextField("Viewer Project Path", viewerProjectPath);
        syncRuntimeScript = EditorGUILayout.Toggle("Sync Runtime Script", syncRuntimeScript);
        syncAllParametricModels = EditorGUILayout.Toggle("Sync All Parametric Models", syncAllParametricModels);

        using (new EditorGUI.DisabledScope(syncAllParametricModels))
        {
            typeKey = EditorGUILayout.TextField("Type Key", typeKey);
        }

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(viewerProjectPath)))
        {
            if (GUILayout.Button(syncAllParametricModels ? "Sync All Parametric Models" : "Sync Selected Type Key"))
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

        OpeningTypeCatalog catalog = Resources.Load<OpeningTypeCatalog>(CatalogResourcePath);
        if (catalog == null || catalog.Items == null)
        {
            EditorUtility.DisplayDialog("Sync failed", $"Catalog not found in Resources: {CatalogResourcePath}", "OK");
            return;
        }

        try
        {
            if (syncRuntimeScript)
            {
                CopyRuntimeScript();
            }

            int count = 0;
            IReadOnlyList<OpeningTypeCatalogItem> items = catalog.Items;
            for (int i = 0; i < items.Count; i++)
            {
                OpeningTypeCatalogItem item = items[i];
                if (!ShouldSync(item))
                {
                    continue;
                }

                SyncItem(item);
                count++;
            }

            if (count == 0)
            {
                EditorUtility.DisplayDialog("Sync complete", "No matching opening model was found.", "OK");
                return;
            }

            Debug.Log($"Synced {count} opening model(s) to viewer: {viewerProjectPath}");
            EditorUtility.DisplayDialog("Sync complete", $"Synced {count} opening model(s) to LH Viewer.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Sync failed", ex.Message, "OK");
        }
    }

    private bool ShouldSync(OpeningTypeCatalogItem item)
    {
        if (item == null || item.ModelPrefab == null)
        {
            return false;
        }

        if (syncAllParametricModels)
        {
            return item.UseParametricModel;
        }

        return string.Equals(item.TypeKey, typeKey, StringComparison.OrdinalIgnoreCase);
    }

    private void SyncItem(OpeningTypeCatalogItem item)
    {
        string prefabPath = AssetDatabase.GetAssetPath(item.ModelPrefab);
        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            throw new InvalidOperationException($"Prefab path not found for {item.TypeKey}.");
        }

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(item.ModelPrefab, out string prefabGuid, out long localId))
        {
            throw new InvalidOperationException($"Prefab GUID not found for {item.TypeKey}: {prefabPath}");
        }

        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, true);
        for (int i = 0; i < dependencies.Length; i++)
        {
            CopyOpeningDependency(dependencies[i]);
        }

        string copiedPrefabPath = ResolveViewerAssetPath(prefabPath);
        PatchCopiedPrefabScriptGuid(copiedPrefabPath);
        long viewerLocalId = ResolveCopiedPrefabRootLocalId(copiedPrefabPath, localId);
        UpdateViewerPrefabPreset(item, prefabGuid, viewerLocalId);
        WriteManifest(item, prefabGuid, viewerLocalId, copiedPrefabPath);
    }

    private void CopyRuntimeScript()
    {
        string sourceDirectory = Path.GetDirectoryName(ToAbsoluteEditorPath(EditorParametricScriptPath));
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            return;
        }

        string viewerDirectory = Path.GetDirectoryName(ViewerParametricScriptPath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(viewerDirectory))
        {
            return;
        }

        string[] sourceScripts = Directory.GetFiles(sourceDirectory, "ParametricOpeningModel*.cs");
        for (int i = 0; i < sourceScripts.Length; i++)
        {
            string fileName = Path.GetFileName(sourceScripts[i]);
            bool isMainScript = string.Equals(fileName, Path.GetFileName(EditorParametricScriptPath), StringComparison.OrdinalIgnoreCase);
            string targetAssetPath = isMainScript
                ? ViewerParametricScriptPath
                : $"{viewerDirectory}/{fileName}";
            CopyRuntimeScriptFile(sourceScripts[i], targetAssetPath, copyMeta: !isMainScript);
        }
    }

    private void CopyRuntimeScriptFile(string sourcePath, string targetAssetPath, bool copyMeta)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        string targetPath = ToAbsoluteViewerPath(targetAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
        File.Copy(sourcePath, targetPath, true);

        if (!copyMeta)
        {
            // Keep the viewer main script .meta GUID stable because existing viewer prefabs reference it.
            return;
        }

        string sourceMetaPath = sourcePath + ".meta";
        if (File.Exists(sourceMetaPath))
        {
            File.Copy(sourceMetaPath, targetPath + ".meta", true);
        }
    }

    private void CopyOpeningDependency(string dependencyAssetPath)
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

    private void PatchCopiedPrefabScriptGuid(string viewerAssetPath)
    {
        string absolutePath = ToAbsoluteViewerPath(viewerAssetPath);
        if (!File.Exists(absolutePath) ||
            !absolutePath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string text = File.ReadAllText(absolutePath);
        string patched = text.Replace(EditorParametricScriptGuid, ViewerParametricScriptGuid);
        if (!string.Equals(text, patched, StringComparison.Ordinal))
        {
            File.WriteAllText(absolutePath, patched, Encoding.UTF8);
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

    private void UpdateViewerPrefabPreset(OpeningTypeCatalogItem item, string prefabGuid, long localId)
    {
        string presetAssetPath = item.OpeningType == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? ViewerDoorPresetPath
            : ViewerWindowPresetPath;
        string presetName = item.OpeningType == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? "Door"
            : "Window";
        string absolutePresetPath = ToAbsoluteViewerPath(presetAssetPath);

        List<PresetEntry> entries = ReadPresetEntries(absolutePresetPath);
        bool replaced = false;
        for (int i = 0; i < entries.Count; i++)
        {
            if (!string.Equals(entries[i].Name, item.TypeKey, StringComparison.Ordinal))
            {
                continue;
            }

            entries[i] = new PresetEntry(item.TypeKey, prefabGuid, localId);
            replaced = true;
            break;
        }

        if (!replaced)
        {
            entries.Add(new PresetEntry(item.TypeKey, prefabGuid, localId));
        }

        WritePreset(absolutePresetPath, presetName, entries);
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

    private void WriteManifest(OpeningTypeCatalogItem item, string prefabGuid, long localId, string viewerPrefabPath)
    {
        string manifestPath = ToAbsoluteViewerPath($"Assets/Test/Models/Prefabs/OpeningModelManifests/{item.TypeKey}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
        string json = JsonUtility.ToJson(new OpeningModelManifest
        {
            typeKey = item.TypeKey,
            openingType = item.OpeningType.ToString(),
            displayName = item.DisplayName,
            prefabGuid = prefabGuid,
            prefabLocalFileId = localId.ToString(),
            viewerPrefabPath = viewerPrefabPath,
            parametricProfileKey = item.ParametricProfileKey,
            referenceSize = item.ReferenceSize,
            authoredSize = item.AuthoredSize,
            minSize = item.MinSize,
            maxSize = item.MaxSize,
            modelLayoutKind = item.ModelLayoutKind.ToString(),
            collisionPolicy = item.CollisionPolicy.ToString(),
            selectable = item.Selectable,
            usesBlenderLocalAxes = item.UsesBlenderLocalAxes,
            fitWidth = item.FitWidth,
            fitHeight = item.FitHeight,
            fitDepth = item.FitDepth,
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
    private sealed class OpeningModelManifest
    {
        public string typeKey;
        public string openingType;
        public string displayName;
        public string prefabGuid;
        public string prefabLocalFileId;
        public string viewerPrefabPath;
        public string parametricProfileKey;
        public Vector3 referenceSize;
        public Vector3 authoredSize;
        public Vector3 minSize;
        public Vector3 maxSize;
        public string modelLayoutKind;
        public string collisionPolicy;
        public bool selectable;
        public bool usesBlenderLocalAxes;
        public bool fitWidth;
        public bool fitHeight;
        public bool fitDepth;
    }
}
