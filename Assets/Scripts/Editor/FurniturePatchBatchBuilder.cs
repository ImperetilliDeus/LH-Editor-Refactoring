#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class FurniturePatchBatchBuilder
{
    private const string PatchInputArgument = "--lh-patchInput";
    private const string ImportRootArgument = "--lh-importRoot";
    private const string CatalogAssetArgument = "--lh-catalogAsset";

    public static void RunFromCommandLine()
    {
        try
        {
            BatchBuildOptions options = ParseCommandLineArguments(Environment.GetCommandLineArgs());
            BuildCatalog(options);
            Debug.Log($"Furniture patch batch build completed: {options.catalogAssetPath}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Furniture patch batch build failed: {exception}");
            EditorApplication.Exit(1);
        }
    }

    private static void BuildCatalog(BatchBuildOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.patchInputDirectory) || !Directory.Exists(options.patchInputDirectory))
        {
            throw new DirectoryNotFoundException($"Patch input directory not found: {options.patchInputDirectory}");
        }

        if (string.IsNullOrWhiteSpace(options.importRootAssetPath) || !options.importRootAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid import root asset path: {options.importRootAssetPath}");
        }

        if (string.IsNullOrWhiteSpace(options.catalogAssetPath) || !options.catalogAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Invalid catalog asset path: {options.catalogAssetPath}");
        }

        string catalogJsonPath = Path.Combine(options.patchInputDirectory, "patch-catalog.json");
        if (!File.Exists(catalogJsonPath))
        {
            throw new FileNotFoundException("patch-catalog.json was not found.", catalogJsonPath);
        }

        string json = File.ReadAllText(catalogJsonPath);
        FurniturePatchCatalogFile catalogFile = JsonUtility.FromJson<FurniturePatchCatalogFile>(json);
        if (catalogFile == null || catalogFile.items == null)
        {
            throw new InvalidOperationException("patch-catalog.json could not be parsed.");
        }

        EnsureAssetFolderExists(options.importRootAssetPath);
        string prefabAssetFolder = $"{options.importRootAssetPath}/Prefabs";
        string thumbnailAssetFolder = $"{options.importRootAssetPath}/Thumbnails";
        EnsureAssetFolderExists(prefabAssetFolder);
        EnsureAssetFolderExists(thumbnailAssetFolder);

        FurnitureCatalog catalog = LoadOrCreateCatalog(options.catalogAssetPath);
        SerializedObject serializedCatalog = new SerializedObject(catalog);
        SerializedProperty itemsProperty = serializedCatalog.FindProperty("items");
        itemsProperty.arraySize = catalogFile.items.Length;

        for (int i = 0; i < catalogFile.items.Length; i++)
        {
            FurniturePatchCatalogItemFile sourceItem = catalogFile.items[i];
            SerializedProperty itemProperty = itemsProperty.GetArrayElementAtIndex(i);

            string prefabAssetPath = ImportAssetIntoProject(options.patchInputDirectory, sourceItem.prefabFile, prefabAssetFolder);
            string thumbnailAssetPath = string.IsNullOrWhiteSpace(sourceItem.thumbnailFile)
                ? string.Empty
                : ImportAssetIntoProject(options.patchInputDirectory, sourceItem.thumbnailFile, thumbnailAssetFolder);

            itemProperty.FindPropertyRelative("code").stringValue = sourceItem.code ?? string.Empty;
            itemProperty.FindPropertyRelative("exportCode").stringValue = string.IsNullOrWhiteSpace(sourceItem.exportCode)
                ? sourceItem.code ?? string.Empty
                : sourceItem.exportCode;
            itemProperty.FindPropertyRelative("nativeCode").stringValue = sourceItem.nativeCode ?? string.Empty;
            itemProperty.FindPropertyRelative("displayName").stringValue = sourceItem.displayName ?? string.Empty;
            itemProperty.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            itemProperty.FindPropertyRelative("thumbnail").objectReferenceValue = string.IsNullOrWhiteSpace(thumbnailAssetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Texture2D>(thumbnailAssetPath);
            itemProperty.FindPropertyRelative("placementOffset").vector3Value = ToVector3(sourceItem.placementOffset);
            itemProperty.FindPropertyRelative("defaultEulerAngles").vector3Value = ToVector3(sourceItem.defaultEulerAngles);
            itemProperty.FindPropertyRelative("boundsSize").vector3Value = ToVector3(sourceItem.boundsSize, Vector3.one);

            SerializedProperty defectsProperty = itemProperty.FindPropertyRelative("defects");
            FurniturePatchDefectFile[] defects = sourceItem.defects ?? Array.Empty<FurniturePatchDefectFile>();
            defectsProperty.arraySize = defects.Length;
            for (int defectIndex = 0; defectIndex < defects.Length; defectIndex++)
            {
                SerializedProperty defectProperty = defectsProperty.GetArrayElementAtIndex(defectIndex);
                defectProperty.FindPropertyRelative("mntnCd").stringValue = defects[defectIndex].mntnCd ?? string.Empty;
                defectProperty.FindPropertyRelative("locCd").stringValue = defects[defectIndex].locCd ?? string.Empty;
                defectProperty.FindPropertyRelative("mtrlCd").stringValue = defects[defectIndex].mtrlCd ?? string.Empty;
            }
        }

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string ImportAssetIntoProject(string patchInputDirectory, string relativeSourcePath, string targetAssetFolder)
    {
        string normalizedRelativePath = (relativeSourcePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
        string sourcePath = Path.GetFullPath(Path.Combine(patchInputDirectory, normalizedRelativePath));
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Patch asset not found: {relativeSourcePath}", sourcePath);
        }

        string targetFileName = Path.GetFileName(sourcePath);
        string destinationAssetPath = $"{targetAssetFolder}/{targetFileName}".Replace('\\', '/');
        string destinationAbsolutePath = Path.GetFullPath(destinationAssetPath);

        string directory = Path.GetDirectoryName(destinationAbsolutePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourcePath, destinationAbsolutePath, true);
        AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);
        return destinationAssetPath;
    }

    private static FurnitureCatalog LoadOrCreateCatalog(string catalogAssetPath)
    {
        EnsureAssetFolderExists(Path.GetDirectoryName(catalogAssetPath)?.Replace('\\', '/') ?? "Assets");
        FurnitureCatalog existing = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(catalogAssetPath);
        if (existing != null)
        {
            return existing;
        }

        FurnitureCatalog catalog = ScriptableObject.CreateInstance<FurnitureCatalog>();
        AssetDatabase.CreateAsset(catalog, catalogAssetPath);
        AssetDatabase.SaveAssets();
        return catalog;
    }

    private static void EnsureAssetFolderExists(string assetFolderPath)
    {
        if (string.IsNullOrWhiteSpace(assetFolderPath))
        {
            return;
        }

        string normalized = assetFolderPath.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static Vector3 ToVector3(FurniturePatchVector3File value)
    {
        if (value == null)
        {
            return Vector3.zero;
        }

        return new Vector3(value.x, value.y, value.z);
    }

    private static Vector3 ToVector3(FurniturePatchVector3File value, Vector3 fallback)
    {
        if (value == null)
        {
            return fallback;
        }

        return new Vector3(value.x, value.y, value.z);
    }

    private static BatchBuildOptions ParseCommandLineArguments(IReadOnlyList<string> args)
    {
        return new BatchBuildOptions
        {
            patchInputDirectory = GetRequiredArgument(args, PatchInputArgument),
            importRootAssetPath = GetRequiredArgument(args, ImportRootArgument).Replace('\\', '/'),
            catalogAssetPath = GetRequiredArgument(args, CatalogAssetArgument).Replace('\\', '/')
        };
    }

    private static string GetRequiredArgument(IReadOnlyList<string> args, string argumentName)
    {
        for (int i = 0; i < args.Count - 1; i++)
        {
            if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        throw new InvalidOperationException($"Missing command line argument: {argumentName}");
    }

    [Serializable]
    private sealed class FurniturePatchCatalogFile
    {
        public int manifestVersion;
        public string catalogVersion;
        public string createdAt;
        public string builtAt;
        public string author;
        public string manifestFile;
        public FurniturePatchCatalogItemFile[] items;
    }

    [Serializable]
    private sealed class FurniturePatchCatalogItemFile
    {
        public string code;
        public string displayName;
        public string exportCode;
        public string nativeCode;
        public string prefabFile;
        public string thumbnailFile;
        public FurniturePatchVector3File placementOffset;
        public FurniturePatchVector3File defaultEulerAngles;
        public FurniturePatchVector3File boundsSize;
        public FurniturePatchDefectFile[] defects;
    }

    [Serializable]
    private sealed class FurniturePatchDefectFile
    {
        public string mntnCd;
        public string locCd;
        public string mtrlCd;
    }

    [Serializable]
    private sealed class FurniturePatchVector3File
    {
        public float x;
        public float y;
        public float z;
    }

    private sealed class BatchBuildOptions
    {
        public string patchInputDirectory;
        public string importRootAssetPath;
        public string catalogAssetPath;
    }
}
#endif
