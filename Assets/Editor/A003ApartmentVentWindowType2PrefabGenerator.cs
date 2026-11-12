using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class A003ApartmentVentWindowType2PrefabGenerator
{
    private const string PrefabPath = "Assets/Prefabs/Furniture/Models/Prefabs/Window/Air/A003.prefab";
    private const string MeshAssetPath = "Assets/Prefabs/Furniture/Models/Prefabs/Window/Air/A003_Meshes.asset";
    private const string MaterialFolder = "Assets/Prefabs/Furniture/Models/Materials/Window/Air";
    private const string CatalogPath = "Assets/Resources/OpeningTypeCatalog.asset";
    private const string Code = "A003";

    private static readonly Vector3 AuthoredSize = new Vector3(8f, 8f, 0.6f);

    [MenuItem("LH/Generate/A003 Apartment Vent Window Type2 Prefab")]
    public static void Generate()
    {
        EnsureDirectory(Path.GetDirectoryName(PrefabPath));
        EnsureDirectory(Path.GetDirectoryName(MeshAssetPath));
        EnsureDirectory(MaterialFolder);

        AssetDatabase.DeleteAsset(MeshAssetPath);

        Material frameMaterial = EnsureMaterial("A003_WhiteVinylFrame", new Color(0.88f, 0.88f, 0.84f, 1f), false, 0f, 0.38f);
        Material glassMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Furniture/Models/Materials/Door/Glass/G001.mat");
        if (glassMaterial == null)
        {
            glassMaterial = EnsureMaterial("A003_ClearClosedGlass", new Color(0.70f, 0.88f, 1f, 0.30f), true, 0f, 0.82f);
        }
        Material handleMaterial = EnsureMaterial("A003_WhitePlasticHandle", new Color(0.90f, 0.88f, 0.82f, 1f), false, 0f, 0.35f);

        GameObject root = new GameObject(Code);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        ParametricOpeningModel parametric = root.AddComponent<ParametricOpeningModel>();
        SerializedObject serialized = new SerializedObject(parametric);
        serialized.FindProperty("authoredSize").vector3Value = AuthoredSize;
        serialized.FindProperty("preferAuthoredSizeWhenCatalogReferenceIsDefault").boolValue = true;
        serialized.FindProperty("profileKey").stringValue = ParametricOpeningModel.ApartmentVentWindowType2Profile;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        List<Mesh> meshes = new List<Mesh>();
        AddWindow(root.transform, frameMaterial, glassMaterial, handleMaterial, meshes);

        SaveMeshes(meshes);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        UpdateOpeningCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {Code} apartment vent window type2 prefab: {PrefabPath}");
    }

    private static void AddWindow(Transform root, Material frameMaterial, Material glassMaterial, Material handleMaterial, List<Mesh> meshes)
    {
        const float outerFrame = 0.28f;
        const float sash = 0.22f;
        const float frameDepth = 0.36f;
        const float sashDepth = 0.18f;
        const float glassDepth = 0.035f;
        const float handleWidth = 0.42f;
        const float handleHeight = 0.08f;
        const float handleDepth = 0.12f;

        float width = AuthoredSize.x;
        float height = AuthoredSize.y;
        float depth = AuthoredSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = width - outerFrame * 2f;
        float innerHeight = height - outerFrame * 2f;
        float paneWidth = innerWidth - sash * 2f;
        float paneHeight = innerHeight - sash * 2f;
        float frontZ = -depth * 0.5f;
        float glassZ = frontZ - 0.04f;
        float sashZ = frontZ - 0.08f;
        float handleY = -halfHeight + outerFrame + sash * 0.45f;
        float handleZ = frontZ - 0.18f;

        AddCube(root, "Fixed_Type2OuterFrame_Left", new Vector3(outerFrame, height, frameDepth), new Vector3(-halfWidth + outerFrame * 0.5f, 0f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Fixed_Type2OuterFrame_Right", new Vector3(outerFrame, height, frameDepth), new Vector3(halfWidth - outerFrame * 0.5f, 0f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_Type2OuterFrame_Top", new Vector3(width, outerFrame, frameDepth), new Vector3(0f, halfHeight - outerFrame * 0.5f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_Type2OuterFrame_Bottom", new Vector3(width, outerFrame, frameDepth), new Vector3(0f, -halfHeight + outerFrame * 0.5f, 0f), frameMaterial, meshes, true);

        AddCube(root, "Fixed_Type2Sash_Left", new Vector3(sash, innerHeight, sashDepth), new Vector3(-innerWidth * 0.5f + sash * 0.5f, 0f, sashZ), frameMaterial, meshes, true);
        AddCube(root, "Fixed_Type2Sash_Right", new Vector3(sash, innerHeight, sashDepth), new Vector3(innerWidth * 0.5f - sash * 0.5f, 0f, sashZ), frameMaterial, meshes, true);
        AddCube(root, "Stretch_Type2Sash_Top", new Vector3(innerWidth, sash, sashDepth), new Vector3(0f, innerHeight * 0.5f - sash * 0.5f, sashZ), frameMaterial, meshes, true);
        AddCube(root, "Stretch_Type2Sash_Bottom", new Vector3(innerWidth, sash, sashDepth), new Vector3(0f, -innerHeight * 0.5f + sash * 0.5f, sashZ), frameMaterial, meshes, true);

        AddCube(root, "Stretch_Type2_ClearGlass", new Vector3(paneWidth, paneHeight, glassDepth), new Vector3(0f, 0f, glassZ), glassMaterial, meshes, false);
        AddCube(root, "Stretch_Type2_BottomSeal", new Vector3(paneWidth, 0.05f, 0.08f), new Vector3(0f, handleY + sash * 0.55f, handleZ + 0.02f), frameMaterial, meshes, true);
        AddCube(root, "Fixed_Type2_Handle_Base", new Vector3(handleWidth, handleHeight, handleDepth), new Vector3(0f, handleY, handleZ), handleMaterial, meshes, true);
        AddCube(root, "Fixed_Type2_Handle_Lever", new Vector3(0.12f, 0.36f, 0.10f), new Vector3(0f, handleY - 0.18f, handleZ - 0.02f), handleMaterial, meshes, true);
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

    private static Material EnsureMaterial(string name, Color color, bool transparent, float metallic, float smoothness)
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

        if (transparent)
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_Blend", 0f);
            SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Opaque");
            SetFloat(material, "_Surface", 0f);
            SetFloat(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            SetFloat(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            SetFloat(material, "_ZWrite", 1f);
            material.renderQueue = -1;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void UpdateOpeningCatalog()
    {
        OpeningTypeCatalog catalog = AssetDatabase.LoadAssetAtPath<OpeningTypeCatalog>(CatalogPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (catalog == null || prefab == null)
        {
            Debug.LogWarning($"Skipped {Code} catalog update. Catalog or prefab is missing.");
            return;
        }

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        SerializedProperty items = serializedCatalog.FindProperty("items");
        SerializedProperty item = FindCatalogItem(items, Code);
        if (item == null)
        {
            int index = items.arraySize;
            items.InsertArrayElementAtIndex(index);
            item = items.GetArrayElementAtIndex(index);
        }

        item.FindPropertyRelative("openingType").enumValueIndex = (int)WallOpeningPlacementManager.OpeningPlacementType.Window;
        item.FindPropertyRelative("typeKey").stringValue = Code;
        item.FindPropertyRelative("displayName").stringValue = "아파트 환기창 타입2";
        item.FindPropertyRelative("modelPrefab").objectReferenceValue = prefab;
        item.FindPropertyRelative("modelLocalPosition").vector3Value = Vector3.zero;
        item.FindPropertyRelative("modelLocalEulerAngles").vector3Value = Vector3.zero;
        item.FindPropertyRelative("modelScaleMultiplier").vector3Value = Vector3.one;
        item.FindPropertyRelative("referenceSize").vector3Value = AuthoredSize;
        item.FindPropertyRelative("fitDepth").boolValue = false;
        item.FindPropertyRelative("fitHeight").boolValue = false;
        item.FindPropertyRelative("fitWidth").boolValue = false;
        item.FindPropertyRelative("useParametricModel").boolValue = true;
        item.FindPropertyRelative("parametricProfileKey").stringValue = ParametricOpeningModel.ApartmentVentWindowType2Profile;
        item.FindPropertyRelative("authoredSize").vector3Value = AuthoredSize;
        item.FindPropertyRelative("usesBlenderLocalAxes").boolValue = false;
        item.FindPropertyRelative("minSize").vector3Value = new Vector3(4f, 4f, 0.25f);
        item.FindPropertyRelative("maxSize").vector3Value = new Vector3(12f, 12f, 0.8f);
        item.FindPropertyRelative("modelLayoutKind").enumValueIndex = (int)OpeningModelLayoutKind.WindowLike;
        item.FindPropertyRelative("collisionPolicy").enumValueIndex = (int)OpeningCollisionPolicy.BlockCharacter;
        item.FindPropertyRelative("selectable").boolValue = true;

        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static SerializedProperty FindCatalogItem(SerializedProperty items, string typeKey)
    {
        for (int i = 0; i < items.arraySize; i++)
        {
            SerializedProperty item = items.GetArrayElementAtIndex(i);
            if (item.FindPropertyRelative("typeKey").stringValue == typeKey)
            {
                return item;
            }
        }

        return null;
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
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
