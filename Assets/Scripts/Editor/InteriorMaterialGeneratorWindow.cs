using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class InteriorMaterialGeneratorWindow : EditorWindow
{
    private const string MaterialRoot = "Assets/Prefabs/Furniture/Models/Materials";
    private const string TextureRoot = "Assets/Prefabs/Furniture/Models/Textures";

    private InteriorMaterialCategory category = InteriorMaterialCategory.Floor;
    private bool setAsDefault;
    private bool copyTextureIntoLibrary = true;
    private Vector2 repeatsPerMeter = new Vector2(2f, 2f);
    private Vector2 scrollPosition;
    private readonly List<Texture2D> selectedTextures = new List<Texture2D>();

    [MenuItem("LH/Materials/Interior Material Generator")]
    public static void Open()
    {
        GetWindow<InteriorMaterialGeneratorWindow>("Interior Materials");
    }

    private void OnEnable()
    {
        RefreshSelection();
    }

    private void OnSelectionChange()
    {
        RefreshSelection();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Generate Interior Materials", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        category = (InteriorMaterialCategory)EditorGUILayout.EnumPopup("Category", category);
        if (EditorGUI.EndChangeCheck())
        {
            repeatsPerMeter = GetDefaultRepeatsPerMeter(category);
        }

        repeatsPerMeter = EditorGUILayout.Vector2Field("Repeats Per Meter", repeatsPerMeter);
        repeatsPerMeter.x = Mathf.Max(0.01f, repeatsPerMeter.x);
        repeatsPerMeter.y = Mathf.Max(0.01f, repeatsPerMeter.y);
        setAsDefault = EditorGUILayout.Toggle("Use Last As Default", setAsDefault);
        copyTextureIntoLibrary = EditorGUILayout.Toggle("Copy Texture To Library", copyTextureIntoLibrary);

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Selection"))
            {
                RefreshSelection();
            }

            if (GUILayout.Button("Import Texture Files..."))
            {
                ImportExternalTextureFiles();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField($"Selected Textures ({selectedTextures.Count})", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(120f));
        for (int i = 0; i < selectedTextures.Count; i++)
        {
            EditorGUILayout.ObjectField(selectedTextures[i], typeof(Texture2D), false);
        }

        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(selectedTextures.Count == 0))
        {
            if (GUILayout.Button("Generate Materials"))
            {
                GenerateMaterialsFromSelection();
            }
        }
    }

    private void RefreshSelection()
    {
        selectedTextures.Clear();
        Object[] selection = Selection.GetFiltered(typeof(Texture2D), SelectionMode.Assets);
        for (int i = 0; i < selection.Length; i++)
        {
            if (selection[i] is Texture2D texture)
            {
                selectedTextures.Add(texture);
            }
        }
    }

    private void ImportExternalTextureFiles()
    {
        string sourcePath = EditorUtility.OpenFilePanelWithFilters(
            "Import texture",
            string.Empty,
            new[] { "Image files", "png,jpg,jpeg,tga,psd", "All files", "*" });

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        string textureFolder = GetTextureFolder(category);
        EnsureAssetFolder(textureFolder);
        string destinationPath = AssetDatabase.GenerateUniqueAssetPath($"{textureFolder}/{Path.GetFileName(sourcePath)}");
        File.Copy(sourcePath, Path.GetFullPath(destinationPath));
        AssetDatabase.ImportAsset(destinationPath);

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(destinationPath);
        if (texture != null && !selectedTextures.Contains(texture))
        {
            selectedTextures.Add(texture);
        }
    }

    private void GenerateMaterialsFromSelection()
    {
        Material lastMaterial = null;
        for (int i = 0; i < selectedTextures.Count; i++)
        {
            Texture2D texture = selectedTextures[i];
            if (texture == null)
            {
                continue;
            }

            lastMaterial = GenerateMaterial(texture);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RefreshOpenRoomManagers();

        if (setAsDefault && lastMaterial != null)
        {
            ApplyDefaultMaterial(lastMaterial);
        }
    }

    private Material GenerateMaterial(Texture2D sourceTexture)
    {
        string materialFolder = GetMaterialFolder(category);
        string textureFolder = GetTextureFolder(category);
        string uiTextureFolder = $"{textureFolder}/UI";
        EnsureAssetFolder(materialFolder);
        EnsureAssetFolder(textureFolder);
        EnsureAssetFolder(uiTextureFolder);

        Texture2D materialTexture = copyTextureIntoLibrary
            ? CopyTextureAsset(sourceTexture, textureFolder)
            : sourceTexture;

        if (materialTexture == null)
        {
            return null;
        }

        CopyTextureAsset(materialTexture, uiTextureFolder);

        string materialName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(materialTexture));
        string materialPath = $"{materialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(ResolveLitShader())
            {
                name = materialName,
            };
            AssetDatabase.CreateAsset(material, materialPath);
        }

        ConfigureMaterial(material, materialTexture, repeatsPerMeter);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D CopyTextureAsset(Texture2D sourceTexture, string destinationFolder)
    {
        string sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        EnsureAssetFolder(destinationFolder);
        string destinationPath = $"{destinationFolder}/{Path.GetFileName(sourcePath)}";
        if (!string.Equals(sourcePath.Replace('\\', '/'), destinationPath, System.StringComparison.OrdinalIgnoreCase))
        {
            destinationPath = AssetDatabase.GenerateUniqueAssetPath(destinationPath);
            if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                Debug.LogWarning($"Failed to copy texture asset: {sourcePath} -> {destinationPath}");
                return sourceTexture;
            }
        }

        AssetDatabase.ImportAsset(destinationPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(destinationPath);
    }

    private static void ConfigureMaterial(Material material, Texture texture, Vector2 tiling)
    {
        if (material == null || texture == null)
        {
            return;
        }

        material.mainTexture = texture;
        material.mainTextureScale = tiling;
        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetTextureScale("_BaseMap", tiling);
        }

        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", tiling);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }
    }

    private static Vector2 GetDefaultRepeatsPerMeter(InteriorMaterialCategory category)
    {
        switch (category)
        {
            case InteriorMaterialCategory.Floor:
                return new Vector2(2f, 2f);
            case InteriorMaterialCategory.Wall:
                return new Vector2(1f, 1f);
            case InteriorMaterialCategory.Ceiling:
                return new Vector2(1f, 1f);
            default:
                return new Vector2(1f, 1f);
        }
    }

    private static Shader ResolveLitShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }

    private static string GetMaterialFolder(InteriorMaterialCategory category)
    {
        switch (category)
        {
            case InteriorMaterialCategory.Floor:
                return $"{MaterialRoot}/Floor";
            case InteriorMaterialCategory.Wall:
                return $"{MaterialRoot}/Wall";
            case InteriorMaterialCategory.Ceiling:
                return $"{MaterialRoot}/Ceil";
            default:
                return $"{MaterialRoot}/Floor";
        }
    }

    private static string GetTextureFolder(InteriorMaterialCategory category)
    {
        switch (category)
        {
            case InteriorMaterialCategory.Floor:
                return $"{TextureRoot}/Floor";
            case InteriorMaterialCategory.Wall:
                return $"{TextureRoot}/Wall";
            case InteriorMaterialCategory.Ceiling:
                return $"{TextureRoot}/Ceil";
            default:
                return $"{TextureRoot}/Floor";
        }
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        string normalized = folderPath.Replace('\\', '/').Trim('/');
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

    private static void RefreshOpenRoomManagers()
    {
        RoomManager[] managers = Resources.FindObjectsOfTypeAll<RoomManager>();
        for (int i = 0; i < managers.Length; i++)
        {
            RoomManager manager = managers[i];
            if (manager == null || EditorUtility.IsPersistent(manager))
            {
                continue;
            }

            manager.RefreshMaterialCacheForEditor();
            EditorUtility.SetDirty(manager);
        }
    }

    private void ApplyDefaultMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        string materialCode = material.name;
        RoomManager[] managers = Resources.FindObjectsOfTypeAll<RoomManager>();
        for (int i = 0; i < managers.Length; i++)
        {
            RoomManager manager = managers[i];
            if (manager == null || EditorUtility.IsPersistent(manager))
            {
                continue;
            }

            SerializedObject serializedManager = new SerializedObject(manager);
            string propertyName = category == InteriorMaterialCategory.Floor
                ? "defaultFloorTextureCode"
                : category == InteriorMaterialCategory.Wall
                    ? "defaultWallTextureCode"
                    : "defaultCeilingTextureCode";
            SerializedProperty property = serializedManager.FindProperty(propertyName);
            if (property != null)
            {
                property.stringValue = materialCode;
                serializedManager.ApplyModifiedProperties();
            }
        }

        if (category != InteriorMaterialCategory.Wall)
        {
            return;
        }

        DrawManager[] drawManagers = Resources.FindObjectsOfTypeAll<DrawManager>();
        for (int i = 0; i < drawManagers.Length; i++)
        {
            DrawManager drawManager = drawManagers[i];
            if (drawManager == null || EditorUtility.IsPersistent(drawManager))
            {
                continue;
            }

            SerializedObject serializedDrawManager = new SerializedObject(drawManager);
            SerializedProperty wallMaterialProperty = serializedDrawManager.FindProperty("_wallMaterial");
            if (wallMaterialProperty != null)
            {
                wallMaterialProperty.objectReferenceValue = material;
                serializedDrawManager.ApplyModifiedProperties();
            }
        }
    }
}
