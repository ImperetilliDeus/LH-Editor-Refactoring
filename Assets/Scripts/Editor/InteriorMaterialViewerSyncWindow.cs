using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class InteriorMaterialViewerSyncWindow : EditorWindow
{
    private const string EditorMaterialRoot = "Assets/Prefabs/Furniture/Models/Materials";
    private const string EditorTextureRoot = "Assets/Prefabs/Furniture/Models/Textures";
    private const string ViewerMaterialRoot = "Assets/Test/Models/Materials";
    private const string ViewerTextureRoot = "Assets/Test/Models/Textures";
    private const string MaterialPresetScriptGuid = "43407095addb97f479adcc6ecdde42fc";
    private const string ViewerStandardShaderReference = "m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}";

    private static readonly InteriorMaterialCategory[] Categories =
    {
        InteriorMaterialCategory.Floor,
        InteriorMaterialCategory.Wall,
        InteriorMaterialCategory.Ceiling,
    };

    [SerializeField] private string viewerProjectPath = @"E:\Unity\LHM_260212";
    [SerializeField] private bool syncTextures = true;
    [SerializeField] private bool syncMaterials = true;
    [SerializeField] private bool rebuildMaterialPresets = true;

    [MenuItem("LH/Materials/Sync Interior Materials To Viewer")]
    public static void Open()
    {
        GetWindow<InteriorMaterialViewerSyncWindow>("Material Viewer Sync");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Sync Interior Materials To LH Viewer", EditorStyles.boldLabel);
        viewerProjectPath = EditorGUILayout.TextField("Viewer Project Path", viewerProjectPath);
        syncTextures = EditorGUILayout.Toggle("Sync Textures", syncTextures);
        syncMaterials = EditorGUILayout.Toggle("Sync Materials", syncMaterials);
        rebuildMaterialPresets = EditorGUILayout.Toggle("Rebuild MaterialPresets", rebuildMaterialPresets);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(viewerProjectPath)))
        {
            if (GUILayout.Button("Sync Now"))
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

        try
        {
            for (int i = 0; i < Categories.Length; i++)
            {
                SyncCategory(Categories[i], viewerProjectPath);
            }

            Debug.Log($"Interior materials synced to viewer: {viewerProjectPath}");
            EditorUtility.DisplayDialog("Sync complete", "Interior materials synced to LH Viewer.", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Sync failed", ex.Message, "OK");
        }
    }

    private void SyncCategory(InteriorMaterialCategory category, string viewerRoot)
    {
        string editorMaterialFolder = GetEditorMaterialFolder(category);
        string viewerMaterialFolder = ToAbsoluteViewerPath(viewerRoot, $"{ViewerMaterialRoot}/{GetCategoryFolderName(category)}");
        string editorTextureFolder = GetEditorTextureFolder(category);
        string viewerTextureFolder = ToAbsoluteViewerPath(viewerRoot, $"{ViewerTextureRoot}/{GetCategoryFolderName(category)}");

        if (syncTextures)
        {
            CopyAssetFolder(editorTextureFolder, viewerTextureFolder);
        }

        if (syncMaterials)
        {
            CopyAssetFolder(editorMaterialFolder, viewerMaterialFolder);
            ConvertMaterialsToViewerShader(viewerMaterialFolder);
        }

        if (rebuildMaterialPresets)
        {
            string presetPath = ToAbsoluteViewerPath(viewerRoot, $"{ViewerMaterialRoot}/{GetCategoryFolderName(category)}.asset");
            RebuildMaterialPreset(presetPath, GetPresetName(category), viewerMaterialFolder);
        }
    }

    private static void CopyAssetFolder(string sourceAssetFolder, string targetFolder)
    {
        string sourceFolder = Path.GetFullPath(sourceAssetFolder);
        if (!Directory.Exists(sourceFolder))
        {
            return;
        }

        Directory.CreateDirectory(targetFolder);
        string[] files = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string sourceFile = files[i];
            if (sourceFile.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            CopyAssetAndMeta(sourceFolder, sourceFile, targetFolder);
        }
    }

    private static void CopyAssetAndMeta(string sourceRoot, string sourceFile, string targetRoot)
    {
        string relativePath = sourceFile.Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string targetFile = Path.Combine(targetRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
        File.Copy(sourceFile, targetFile, true);

        string sourceMeta = sourceFile + ".meta";
        if (File.Exists(sourceMeta))
        {
            File.Copy(sourceMeta, targetFile + ".meta", true);
        }
    }

    private static void ConvertMaterialsToViewerShader(string materialFolder)
    {
        if (!Directory.Exists(materialFolder))
        {
            return;
        }

        string[] materialFiles = Directory.GetFiles(materialFolder, "*.mat", SearchOption.TopDirectoryOnly);
        for (int i = 0; i < materialFiles.Length; i++)
        {
            string materialFile = materialFiles[i];
            string text = File.ReadAllText(materialFile);
            string converted = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"m_Shader: \{fileID: 4800000, guid: [0-9a-fA-F]{32}, type: 3\}",
                ViewerStandardShaderReference);
            if (!string.Equals(text, converted, StringComparison.Ordinal))
            {
                File.WriteAllText(materialFile, converted, Encoding.UTF8);
            }
        }
    }

    private static void RebuildMaterialPreset(string presetPath, string presetName, string materialFolder)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(presetPath));

        List<(string code, string guid)> entries = new List<(string code, string guid)>();
        if (Directory.Exists(materialFolder))
        {
            string[] materialFiles = Directory.GetFiles(materialFolder, "*.mat", SearchOption.TopDirectoryOnly);
            Array.Sort(materialFiles, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < materialFiles.Length; i++)
            {
                string materialFile = materialFiles[i];
                string guid = ReadMetaGuid(materialFile + ".meta");
                if (string.IsNullOrWhiteSpace(guid))
                {
                    continue;
                }

                entries.Add((Path.GetFileNameWithoutExtension(materialFile), guid));
            }
        }

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
        builder.AppendLine($"  m_Script: {{fileID: 11500000, guid: {MaterialPresetScriptGuid}, type: 3}}");
        builder.AppendLine($"  m_Name: {presetName}");
        builder.AppendLine("  m_EditorClassIdentifier: ");
        builder.AppendLine("  materials:");
        for (int i = 0; i < entries.Count; i++)
        {
            builder.AppendLine($"  - materialName: {entries[i].code}");
            builder.AppendLine($"    material: {{fileID: 2100000, guid: {entries[i].guid}, type: 2}}");
        }

        File.WriteAllText(presetPath, builder.ToString(), Encoding.UTF8);
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

    private static string ToAbsoluteViewerPath(string viewerRoot, string assetPath)
    {
        string relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(viewerRoot, relativePath);
    }

    private static string GetEditorMaterialFolder(InteriorMaterialCategory category)
    {
        return $"{EditorMaterialRoot}/{GetCategoryFolderName(category)}";
    }

    private static string GetEditorTextureFolder(InteriorMaterialCategory category)
    {
        return $"{EditorTextureRoot}/{GetCategoryFolderName(category)}";
    }

    private static string GetCategoryFolderName(InteriorMaterialCategory category)
    {
        return category == InteriorMaterialCategory.Ceiling ? "Ceil" : category.ToString();
    }

    private static string GetPresetName(InteriorMaterialCategory category)
    {
        return GetCategoryFolderName(category);
    }
}
