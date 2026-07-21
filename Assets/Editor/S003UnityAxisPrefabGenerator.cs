using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class S003UnityAxisPrefabGenerator
{
    private const string PrefabPath = "Assets/Prefabs/Furniture/Models/Prefabs/Door/Sliding/S003.prefab";
    private const string MeshAssetPath = "Assets/Prefabs/Furniture/Models/Prefabs/Door/Sliding/S003_Meshes.asset";
    private const string MaterialFolder = "Assets/Prefabs/Furniture/Models/Materials";

    private static readonly Vector3 AuthoredSize = new Vector3(9f, 21f, 0.8f);

    [MenuItem("LH/Generate/S003 Unity Axis Prefab")]
    public static void Generate()
    {
        EnsureDirectory(Path.GetDirectoryName(PrefabPath));
        EnsureDirectory(Path.GetDirectoryName(MeshAssetPath));
        EnsureDirectory(MaterialFolder);

        AssetDatabase.DeleteAsset(MeshAssetPath);

        Material frameMaterial = EnsureMaterial("WhiteDoor_Frame", new Color(0.91f, 0.90f, 0.84f, 1f), false, 0f, 0.36f);
        Material glassMaterial = EnsureMaterial("WhiteDoor_FrostedGlass", new Color(0.72f, 0.86f, 0.92f, 0.36f), true, 0.02f, 0.88f);
        Material metalMaterial = EnsureMaterial("WhiteDoor_SatinMetal", new Color(0.68f, 0.69f, 0.66f, 1f), false, 0.35f, 0.42f);

        GameObject root = new GameObject("S003");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        ParametricOpeningModel parametric = root.AddComponent<ParametricOpeningModel>();
        SerializedObject serialized = new SerializedObject(parametric);
        serialized.FindProperty("authoredSize").vector3Value = AuthoredSize;
        serialized.FindProperty("preferAuthoredSizeWhenCatalogReferenceIsDefault").boolValue = true;
        serialized.FindProperty("profileKey").stringValue = ParametricOpeningModel.WhiteTwoPanelWindowProfile;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        List<Mesh> meshes = new List<Mesh>();
        AddDoor(root.transform, frameMaterial, glassMaterial, metalMaterial, meshes);

        SaveMeshes(meshes);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated S003 prefab: {PrefabPath}");
    }

    private static void AddDoor(Transform root, Material frameMaterial, Material glassMaterial, Material metalMaterial, List<Mesh> meshes)
    {
        const float frameThickness = 0.45f;
        const float frameDepth = 0.55f;
        const float panelDepth = 0.12f;
        const float railThickness = 0.18f;
        const float mullionWidth = 0.34f;

        float width = AuthoredSize.x;
        float depth = AuthoredSize.z;
        float height = AuthoredSize.y;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = width - frameThickness * 2f;
        float innerHeight = height - frameThickness * 2f;
        float panelWidth = (innerWidth - mullionWidth) * 0.5f;
        float panelOffset = mullionWidth * 0.5f + panelWidth * 0.5f;
        float faceY = -depth * 0.5f - panelDepth * 0.2f;
        float railFrontY = faceY - panelDepth * 0.8f;

        AddCube(root, "Fixed_Frame_Left", new Vector3(frameThickness, frameDepth, height), new Vector3(-halfWidth + frameThickness * 0.5f, 0f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Fixed_Frame_Right", new Vector3(frameThickness, frameDepth, height), new Vector3(halfWidth - frameThickness * 0.5f, 0f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_Frame_Top", new Vector3(width, frameDepth, frameThickness), new Vector3(0f, 0f, halfHeight - frameThickness * 0.5f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_Frame_Bottom", new Vector3(width, frameDepth, frameThickness), new Vector3(0f, 0f, -halfHeight + frameThickness * 0.5f), frameMaterial, meshes, true);

        AddCube(root, "Fixed_VerticalTrim_CenterMullion", new Vector3(mullionWidth, railThickness, innerHeight), new Vector3(0f, railFrontY, 0f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_Top_Rail", new Vector3(innerWidth, railThickness, railThickness), new Vector3(0f, railFrontY, halfHeight - frameThickness - railThickness * 0.5f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_Bottom_Rail", new Vector3(innerWidth, railThickness, railThickness), new Vector3(0f, railFrontY, -halfHeight + frameThickness + railThickness * 0.5f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_RecessLine_Horizontal_DoorPanel_01_Top", new Vector3(panelWidth, 0.045f, 0.045f), new Vector3(-panelOffset, railFrontY - 0.03f, halfHeight - frameThickness - railThickness * 0.5f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_RecessLine_Horizontal_DoorPanel_01_Bottom", new Vector3(panelWidth, 0.045f, 0.045f), new Vector3(-panelOffset, railFrontY - 0.03f, -halfHeight + frameThickness + railThickness * 0.5f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_RecessLine_Horizontal_DoorPanel_02_Top", new Vector3(panelWidth, 0.045f, 0.045f), new Vector3(panelOffset, railFrontY - 0.03f, halfHeight - frameThickness - railThickness * 0.5f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_RecessLine_Horizontal_DoorPanel_02_Bottom", new Vector3(panelWidth, 0.045f, 0.045f), new Vector3(panelOffset, railFrontY - 0.03f, -halfHeight + frameThickness + railThickness * 0.5f), frameMaterial, meshes, true);

        AddCube(root, "Stretch_DoorPanel_LeftGlass", new Vector3(panelWidth, panelDepth, innerHeight - railThickness * 2f), new Vector3(-panelOffset, faceY, 0f), glassMaterial, meshes, false);
        AddCube(root, "Stretch_DoorPanel_RightGlass", new Vector3(panelWidth, panelDepth, innerHeight - railThickness * 2f), new Vector3(panelOffset, faceY, 0f), glassMaterial, meshes, false);

        AddCube(root, "Fixed_Right_Handle_Slim", new Vector3(0.12f, 0.12f, 1.1f), new Vector3(halfWidth - frameThickness - 0.42f, railFrontY - 0.08f, -halfHeight + height * 0.48f), metalMaterial, meshes, true);
        AddCube(root, "Stretch_Threshold_FrontStrip", new Vector3(width, 0.16f, 0.12f), new Vector3(0f, depth * 0.5f + 0.08f, -halfHeight + frameThickness * 0.2f), metalMaterial, meshes, true);
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
