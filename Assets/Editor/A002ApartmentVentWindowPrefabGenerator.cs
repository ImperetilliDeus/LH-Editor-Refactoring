using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class A002ApartmentVentWindowPrefabGenerator
{
    private const string PrefabPath = "Assets/Prefabs/Furniture/Models/Prefabs/Window/Air/A002.prefab";
    private const string MeshAssetPath = "Assets/Prefabs/Furniture/Models/Prefabs/Window/Air/A002_Meshes.asset";
    private const string MaterialFolder = "Assets/Prefabs/Furniture/Models/Materials/Window/Air";
    private const string CatalogPath = "Assets/Resources/OpeningTypeCatalog.asset";
    private const string Code = "A002";

    private static readonly Vector3 AuthoredSize = new Vector3(3f, 3f, 0.35f);

    [MenuItem("LH/Generate/A002 Apartment Vent Window Prefab")]
    public static void Generate()
    {
        EnsureDirectory(Path.GetDirectoryName(PrefabPath));
        EnsureDirectory(Path.GetDirectoryName(MeshAssetPath));
        EnsureDirectory(MaterialFolder);

        AssetDatabase.DeleteAsset(MeshAssetPath);

        Material frameMaterial = EnsureMaterial("A002_WhitePowderCoatedFrame", new Color(0.88f, 0.88f, 0.84f, 1f), false, 0f, 0.42f);
        Material bladeMaterial = EnsureMaterial("A002_SatinAluminumLouvers", new Color(0.78f, 0.78f, 0.72f, 1f), false, 0.35f, 0.48f);
        Material meshMaterial = EnsureMaterial("A002_DarkInsectMesh", new Color(0.05f, 0.055f, 0.06f, 0.52f), true, 0f, 0.2f);
        Material handleMaterial = EnsureMaterial("A002_WhitePlasticHandle", new Color(0.9f, 0.88f, 0.8f, 1f), false, 0f, 0.35f);

        GameObject root = new GameObject(Code);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        ParametricOpeningModel parametric = root.AddComponent<ParametricOpeningModel>();
        SerializedObject serialized = new SerializedObject(parametric);
        serialized.FindProperty("authoredSize").vector3Value = AuthoredSize;
        serialized.FindProperty("preferAuthoredSizeWhenCatalogReferenceIsDefault").boolValue = true;
        serialized.FindProperty("profileKey").stringValue = ParametricOpeningModel.ApartmentVentWindowProfile;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        List<Mesh> meshes = new List<Mesh>();
        AddWindow(root.transform, frameMaterial, bladeMaterial, meshMaterial, handleMaterial, meshes);

        SaveMeshes(meshes);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        UpdateOpeningCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {Code} apartment vent window prefab: {PrefabPath}");
    }

    private static void AddWindow(
        Transform root,
        Material frameMaterial,
        Material bladeMaterial,
        Material meshMaterial,
        Material handleMaterial,
        List<Mesh> meshes)
    {
        const float frame = 0.24f;
        const float rearFrame = 0.10f;
        const float frameDepth = 0.34f;
        const float bladeHeight = 0.18f;
        const float bladeDepth = 0.26f;
        const float meshDepth = 0.035f;
        const float handleWidth = 0.08f;

        float width = AuthoredSize.x;
        float height = AuthoredSize.y;
        float depth = AuthoredSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = width - frame * 2f;
        float innerHeight = height - frame * 2f;
        float frontZ = -depth * 0.5f;
        float bladeWidth = innerWidth - 0.12f;
        float firstBladeY = -0.95f;
        float bladeGap = 0.38f;

        AddCube(root, "Fixed_Frame_Left", new Vector3(frame, height, frameDepth), new Vector3(-halfWidth + frame * 0.5f, 0f, 0f), Quaternion.identity, frameMaterial, meshes, true);
        AddCube(root, "Fixed_Frame_Right", new Vector3(frame, height, frameDepth), new Vector3(halfWidth - frame * 0.5f, 0f, 0f), Quaternion.identity, frameMaterial, meshes, true);
        AddCube(root, "Stretch_Frame_Top", new Vector3(width, frame, frameDepth), new Vector3(0f, halfHeight - frame * 0.5f, 0f), Quaternion.identity, frameMaterial, meshes, true);
        AddCube(root, "Stretch_Frame_Bottom", new Vector3(width, frame, frameDepth), new Vector3(0f, -halfHeight + frame * 0.5f, 0f), Quaternion.identity, frameMaterial, meshes, true);

        AddCube(root, "Stretch_InnerLip_Top", new Vector3(innerWidth, rearFrame, rearFrame), new Vector3(0f, halfHeight - frame - rearFrame * 0.5f, frontZ - 0.02f), Quaternion.identity, frameMaterial, meshes, true);
        AddCube(root, "Stretch_InnerLip_Bottom", new Vector3(innerWidth, rearFrame, rearFrame), new Vector3(0f, -halfHeight + frame + rearFrame * 0.5f, frontZ - 0.02f), Quaternion.identity, frameMaterial, meshes, true);
        AddCube(root, "Fixed_InnerLip_Left", new Vector3(rearFrame, innerHeight, rearFrame), new Vector3(-halfWidth + frame + rearFrame * 0.5f, 0f, frontZ - 0.02f), Quaternion.identity, frameMaterial, meshes, true);
        AddCube(root, "Fixed_InnerLip_Right", new Vector3(rearFrame, innerHeight, rearFrame), new Vector3(halfWidth - frame - rearFrame * 0.5f, 0f, frontZ - 0.02f), Quaternion.identity, frameMaterial, meshes, true);
        AddCube(root, "Stretch_Back_Mesh", new Vector3(innerWidth, innerHeight, meshDepth), new Vector3(0f, 0f, depth * 0.5f - meshDepth * 0.5f), Quaternion.identity, meshMaterial, meshes, false);

        for (int i = 0; i < 8; i++)
        {
            AddCube(
                root,
                $"Stretch_LouverBlade_{i + 1:00}",
                new Vector3(bladeWidth, bladeHeight, bladeDepth),
                new Vector3(0f, firstBladeY + bladeGap * i, frontZ + bladeDepth * 0.22f),
                Quaternion.Euler(-18f, 0f, 0f),
                bladeMaterial,
                meshes,
                true);
        }

        AddCube(root, "Fixed_RightSide_Handle", new Vector3(handleWidth, height * 0.64f, 0.12f), new Vector3(halfWidth + 0.05f, 0f, 0f), Quaternion.identity, handleMaterial, meshes, true);
        AddCube(root, "Fixed_Handle_Knob", new Vector3(0.12f, 0.12f, 0.12f), new Vector3(halfWidth + 0.12f, 0f, frontZ - 0.02f), Quaternion.identity, handleMaterial, meshes, true);
    }

    private static GameObject AddCube(Transform parent, string name, Vector3 size, Vector3 localPosition, Quaternion localRotation, Material material, List<Mesh> meshes, bool castShadows)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = localRotation;
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
        item.FindPropertyRelative("displayName").stringValue = "아파트 환기창";
        item.FindPropertyRelative("modelPrefab").objectReferenceValue = prefab;
        item.FindPropertyRelative("modelLocalPosition").vector3Value = Vector3.zero;
        item.FindPropertyRelative("modelLocalEulerAngles").vector3Value = Vector3.zero;
        item.FindPropertyRelative("modelScaleMultiplier").vector3Value = Vector3.one;
        item.FindPropertyRelative("referenceSize").vector3Value = new Vector3(3f, 3f, 0.35f);
        item.FindPropertyRelative("fitDepth").boolValue = false;
        item.FindPropertyRelative("fitHeight").boolValue = false;
        item.FindPropertyRelative("fitWidth").boolValue = false;
        item.FindPropertyRelative("useParametricModel").boolValue = true;
        item.FindPropertyRelative("parametricProfileKey").stringValue = ParametricOpeningModel.ApartmentVentWindowProfile;
        item.FindPropertyRelative("authoredSize").vector3Value = AuthoredSize;
        item.FindPropertyRelative("usesBlenderLocalAxes").boolValue = false;
        item.FindPropertyRelative("minSize").vector3Value = new Vector3(2.5f, 2.5f, 0.25f);
        item.FindPropertyRelative("maxSize").vector3Value = new Vector3(12f, 12f, 0.6f);
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
        if (string.IsNullOrEmpty(path) || Directory.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(path);
    }
}
