#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SC001ShoeCabinetPrefabGenerator
{
    private const string Code = "SC001";
    private const string PrefabPath = "Assets/Prefabs/Furniture/Models/Prefabs/Furnish/SC001.prefab";
    private const string MeshAssetPath = "Assets/Prefabs/Furniture/Models/Prefabs/Furnish/SC001_Meshes.asset";
    private const string CatalogPath = "Assets/Prefabs/Furniture/FurnitureCatalog.asset";
    private const string ThumbnailPath = "Assets/Prefabs/Furniture/FurnitureCatalog_Thumbnails/SC001.png";
    private const string MaterialFolder = "Assets/Prefabs/Furniture/Models/Materials/Furnish/SC001";

    private static readonly Vector3 AuthoredSize = new Vector3(10f, 21.5f, 5f);

    [MenuItem("LH/Generate/SC001 Shoe Cabinet Prefab")]
    public static void Generate()
    {
        EnsureDirectory(Path.GetDirectoryName(PrefabPath));
        EnsureDirectory(Path.GetDirectoryName(MeshAssetPath));
        EnsureDirectory(Path.GetDirectoryName(ThumbnailPath));
        EnsureDirectory(MaterialFolder);

        AssetDatabase.DeleteAsset(MeshAssetPath);

        Material bodyMaterial = EnsureMaterial("SC001_WarmWhite_Body", new Color(0.91f, 0.89f, 0.83f, 1f), 0f, 0.48f);
        Material faceMaterial = EnsureMaterial("SC001_SatinWhite_Doors", new Color(0.96f, 0.95f, 0.90f, 1f), 0f, 0.58f);
        Material shadowMaterial = EnsureMaterial("SC001_RecessShadow", new Color(0.55f, 0.53f, 0.47f, 1f), 0f, 0.34f);
        Material lightMaterial = EnsureEmissionMaterial("SC001_WarmShelfLight", new Color(1f, 0.78f, 0.42f, 1f), 1.8f);

        GameObject root = new GameObject(Code);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, AuthoredSize.y * 0.5f, 0f);
        collider.size = AuthoredSize;

        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(root.transform, false);

        List<Mesh> meshes = new List<Mesh>();
        AddCabinet(visuals.transform, bodyMaterial, faceMaterial, shadowMaterial, lightMaterial, meshes);

        SaveMeshes(meshes);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Texture2D thumbnail = CreateThumbnail();
        UpsertCatalogItem(thumbnail);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {Code} shoe cabinet prefab: {PrefabPath}");
    }

    private static void AddCabinet(
        Transform root,
        Material bodyMaterial,
        Material faceMaterial,
        Material shadowMaterial,
        Material lightMaterial,
        List<Mesh> meshes)
    {
        float width = AuthoredSize.x;
        float height = AuthoredSize.y;
        float depth = AuthoredSize.z;
        float side = 0.18f;
        float back = 0.12f;
        float topBottom = 0.2f;
        float plinth = 0.28f;
        float faceZ = -depth * 0.5f - 0.04f;
        float bodyCenterY = height * 0.5f;
        float halfWidth = width * 0.5f;

        AddCube(root, "Fixed_Carcass_LeftSide", new Vector3(side, height, depth), new Vector3(-halfWidth + side * 0.5f, bodyCenterY, 0f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_Carcass_RightSide", new Vector3(side, height, depth), new Vector3(halfWidth - side * 0.5f, bodyCenterY, 0f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_Carcass_BackPanel", new Vector3(width, height, back), new Vector3(0f, bodyCenterY, depth * 0.5f - back * 0.5f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_Carcass_Top", new Vector3(width, topBottom, depth), new Vector3(0f, height - topBottom * 0.5f, 0f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_Carcass_Bottom", new Vector3(width, topBottom, depth), new Vector3(0f, topBottom * 0.5f, 0f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_ToeKick_RecessShadow", new Vector3(width - side * 2f, plinth, 0.22f), new Vector3(0f, plinth * 0.5f, faceZ + 0.22f), shadowMaterial, meshes, false);

        float openShelfBottom = 11.3f;
        float openShelfHeight = 2.45f;
        float openShelfTop = openShelfBottom + openShelfHeight;
        float upperDoorHeight = height - topBottom - openShelfTop;
        float lowerDoorHeight = openShelfBottom - plinth - topBottom;
        float doorWidth = (width - side * 2f) * 0.5f;
        float leftDoorX = -doorWidth * 0.5f;
        float rightDoorX = doorWidth * 0.5f;
        float doorDepth = 0.11f;

        AddCube(root, "Stretch_LowerDoor_LeftFlatSlab", new Vector3(doorWidth - 0.035f, lowerDoorHeight, doorDepth), new Vector3(leftDoorX, plinth + lowerDoorHeight * 0.5f, faceZ), faceMaterial, meshes, true);
        AddCube(root, "Stretch_LowerDoor_RightFlatSlab", new Vector3(doorWidth - 0.035f, lowerDoorHeight, doorDepth), new Vector3(rightDoorX, plinth + lowerDoorHeight * 0.5f, faceZ), faceMaterial, meshes, true);
        AddCube(root, "Fixed_LowerDoor_CenterReveal", new Vector3(0.035f, lowerDoorHeight, 0.035f), new Vector3(0f, plinth + lowerDoorHeight * 0.5f, faceZ - 0.045f), shadowMaterial, meshes, false);

        AddCube(root, "Fixed_OpenShelf_BackPanel", new Vector3(width - side * 2f, openShelfHeight, 0.08f), new Vector3(0f, openShelfBottom + openShelfHeight * 0.5f, depth * 0.5f - 0.08f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_OpenShelf_BottomBoard", new Vector3(width - side * 2f, 0.16f, depth), new Vector3(0f, openShelfBottom, 0f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_OpenShelf_TopBoard", new Vector3(width - side * 2f, 0.16f, depth), new Vector3(0f, openShelfTop, 0f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_OpenShelf_WarmLinearLight", new Vector3(width - side * 2.8f, 0.07f, 0.08f), new Vector3(0f, openShelfTop - 0.18f, faceZ - 0.08f), lightMaterial, meshes, false);

        AddCube(root, "Stretch_UpperDoor_LeftFlatSlab", new Vector3(doorWidth - 0.035f, upperDoorHeight, doorDepth), new Vector3(leftDoorX, openShelfTop + upperDoorHeight * 0.5f, faceZ), faceMaterial, meshes, true);
        AddCube(root, "Stretch_UpperDoor_RightFlatSlab", new Vector3(doorWidth - 0.035f, upperDoorHeight, doorDepth), new Vector3(rightDoorX, openShelfTop + upperDoorHeight * 0.5f, faceZ), faceMaterial, meshes, true);
        AddCube(root, "Fixed_UpperDoor_CenterReveal", new Vector3(0.035f, upperDoorHeight, 0.035f), new Vector3(0f, openShelfTop + upperDoorHeight * 0.5f, faceZ - 0.045f), shadowMaterial, meshes, false);

        AddCube(root, "Fixed_LeftEdge_RevealLine", new Vector3(0.035f, height - topBottom * 2f, 0.035f), new Vector3(-halfWidth + side, height * 0.5f, faceZ - 0.045f), shadowMaterial, meshes, false);
        AddCube(root, "Fixed_RightEdge_RevealLine", new Vector3(0.035f, height - topBottom * 2f, 0.035f), new Vector3(halfWidth - side, height * 0.5f, faceZ - 0.045f), shadowMaterial, meshes, false);
        AddCube(root, "Fixed_HorizontalReveal_UpperLowerSplit", new Vector3(width - side * 2f, 0.035f, 0.035f), new Vector3(0f, openShelfBottom, faceZ - 0.045f), shadowMaterial, meshes, false);
        AddCube(root, "Fixed_HorizontalReveal_ShelfTop", new Vector3(width - side * 2f, 0.035f, 0.035f), new Vector3(0f, openShelfTop, faceZ - 0.045f), shadowMaterial, meshes, false);
    }

    private static GameObject AddCube(Transform parent, string name, Vector3 size, Vector3 localPosition, Material material, List<Mesh> meshes, bool castShadows)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;

        Mesh mesh = CreateBoxMesh(name, size);
        meshes.Add(mesh);

        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = castShadows
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = castShadows;
        return gameObject;
    }

    private static Mesh CreateBoxMesh(string name, Vector3 size)
    {
        Vector3 half = size * 0.5f;
        Vector3[] vertices =
        {
            new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z), new Vector3(half.x, half.y, -half.z), new Vector3(-half.x, half.y, -half.z),
            new Vector3(-half.x, -half.y, half.z), new Vector3(half.x, -half.y, half.z), new Vector3(half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z),
        };

        int[] triangles =
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            2, 3, 7, 2, 7, 6,
            1, 2, 6, 1, 6, 5,
            3, 0, 4, 3, 4, 7,
        };

        Mesh mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void SaveMeshes(List<Mesh> meshes)
    {
        if (meshes.Count == 0)
        {
            return;
        }

        AssetDatabase.CreateAsset(meshes[0], MeshAssetPath);
        for (int i = 1; i < meshes.Count; i++)
        {
            AssetDatabase.AddObjectToAsset(meshes[i], MeshAssetPath);
        }
    }

    private static Material EnsureMaterial(string name, Color color, float metallic, float smoothness)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        SetColor(material, color);
        SetFloat(material, "_Metallic", metallic);
        SetFloat(material, "_Smoothness", smoothness);
        SetFloat(material, "_Surface", 0f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = -1;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material EnsureEmissionMaterial(string name, Color color, float intensity)
    {
        Material material = EnsureMaterial(name, color, 0f, 0.2f);
        Color emission = color * intensity;
        material.EnableKeyword("_EMISSION");
        SetColor(material, emission);
        if (material.HasProperty("_EmissionColor"))
        {
            material.SetColor("_EmissionColor", emission);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D CreateThumbnail()
    {
        Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = Code };
        Color background = new Color(0.82f, 0.80f, 0.74f, 1f);
        Color cabinet = new Color(0.96f, 0.95f, 0.90f, 1f);
        Color line = new Color(0.55f, 0.53f, 0.47f, 1f);
        Color light = new Color(1f, 0.75f, 0.38f, 1f);

        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                texture.SetPixel(x, y, background);
            }
        }

        FillRect(texture, 34, 10, 60, 108, cabinet);
        FillRect(texture, 62, 12, 2, 106, line);
        FillRect(texture, 36, 63, 56, 15, new Color(0.88f, 0.86f, 0.80f, 1f));
        FillRect(texture, 39, 75, 50, 3, light);
        FillRect(texture, 34, 62, 60, 2, line);
        FillRect(texture, 34, 79, 60, 2, line);
        FillRect(texture, 34, 10, 2, 108, line);
        FillRect(texture, 92, 10, 2, 108, line);
        FillRect(texture, 34, 10, 60, 2, line);
        FillRect(texture, 34, 116, 60, 2, line);
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        File.WriteAllBytes(ThumbnailPath, png);
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(ThumbnailPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(ThumbnailPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(ThumbnailPath);
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int yy = y; yy < y + height; yy++)
        {
            for (int xx = x; xx < x + width; xx++)
            {
                if (xx >= 0 && xx < texture.width && yy >= 0 && yy < texture.height)
                {
                    texture.SetPixel(xx, yy, color);
                }
            }
        }
    }

    private static void UpsertCatalogItem(Texture2D thumbnail)
    {
        FurnitureCatalog catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(CatalogPath);
        if (catalog == null)
        {
            Debug.LogWarning($"Furniture catalog not found: {CatalogPath}");
            return;
        }

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        SerializedProperty itemsProperty = serializedCatalog.FindProperty("items");
        int index = FindCatalogItemIndex(itemsProperty, Code);
        if (index < 0)
        {
            index = itemsProperty.arraySize;
            itemsProperty.InsertArrayElementAtIndex(index);
        }

        SerializedProperty itemProperty = itemsProperty.GetArrayElementAtIndex(index);
        itemProperty.FindPropertyRelative("code").stringValue = Code;
        itemProperty.FindPropertyRelative("exportCode").stringValue = Code;
        itemProperty.FindPropertyRelative("nativeCode").stringValue = string.Empty;
        itemProperty.FindPropertyRelative("displayName").stringValue = "SC001";
        itemProperty.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        itemProperty.FindPropertyRelative("thumbnail").objectReferenceValue = thumbnail;
        itemProperty.FindPropertyRelative("placementOffset").vector3Value = Vector3.zero;
        itemProperty.FindPropertyRelative("defaultEulerAngles").vector3Value = Vector3.zero;
        itemProperty.FindPropertyRelative("boundsSize").vector3Value = AuthoredSize;
        itemProperty.FindPropertyRelative("scaleMultiplier").floatValue = 1f;
        itemProperty.FindPropertyRelative("defects").arraySize = 0;

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static int FindCatalogItemIndex(SerializedProperty itemsProperty, string code)
    {
        if (itemsProperty == null)
        {
            return -1;
        }

        for (int i = 0; i < itemsProperty.arraySize; i++)
        {
            SerializedProperty item = itemsProperty.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("code").stringValue == code)
            {
                return i;
            }
        }

        return -1;
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || Directory.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(path);
    }
}
#endif
