using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class B001UnityAxisPrefabGenerator
{
    private const string PrefabPath = "Assets/Prefabs/Furniture/Models/Prefabs/Window/B001_1.prefab";
    private const string MeshAssetPath = "Assets/Prefabs/Furniture/Models/Prefabs/Window/B001_1_Meshes.asset";
    private const string MaterialFolder = "Assets/Prefabs/Furniture/Models/Materials";

    private static readonly Vector3 AuthoredSize = new Vector3(18f, 21f, 1.4f);

    [MenuItem("LH/Generate/B001 Unity Axis Prefab")]
    public static void Generate()
    {
        EnsureDirectory(Path.GetDirectoryName(PrefabPath));
        EnsureDirectory(Path.GetDirectoryName(MeshAssetPath));
        EnsureDirectory(MaterialFolder);

        AssetDatabase.DeleteAsset(MeshAssetPath);

        Material frameMaterial = EnsureMaterial("Balcony_Frame", new Color(0.78f, 0.84f, 0.89f, 1f), "LH/B001/Simple Opaque", false);
        Material glassMaterial = EnsureMaterial("Balcony_Glass", new Color(0.55f, 0.72f, 0.85f, 0.32f), "LH/B001/Simple Transparent", true);
        Material railMaterial = EnsureMaterial("Balcony_Rail", new Color(0.86f, 0.9f, 0.93f, 1f), "LH/B001/Simple Opaque", false);

        GameObject root = new GameObject("B001_1");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        ParametricOpeningModel parametric = root.AddComponent<ParametricOpeningModel>();
        SerializedObject serialized = new SerializedObject(parametric);
        serialized.FindProperty("authoredSize").vector3Value = AuthoredSize;
        serialized.FindProperty("preferAuthoredSizeWhenCatalogReferenceIsDefault").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        List<Mesh> meshes = new List<Mesh>();
        AddWindow(root.transform, frameMaterial, glassMaterial, meshes);
        AddRailing(root.transform, railMaterial, meshes);

        SaveMeshes(meshes);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated B001 prefab: {PrefabPath}");
    }

    private static void AddWindow(Transform root, Material frameMaterial, Material glassMaterial, List<Mesh> meshes)
    {
        float width = AuthoredSize.x;
        float height = AuthoredSize.y;
        float frame = 0.65f;
        float frameDepth = 0.8f;
        float glassDepth = 0.08f;
        float mullionWidth = 0.32f;
        float innerWidth = width - frame * 2f;
        float glassHeight = height - frame * 2f;
        float glassWidth = (innerWidth - mullionWidth) * 0.5f;
        float glassOffset = mullionWidth * 0.5f + glassWidth * 0.5f;

        AddCube(root, "Fixed_BalconyFrame_Left", new Vector3(frame, height, frameDepth), new Vector3(-width * 0.5f + frame * 0.5f, 0f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Fixed_BalconyFrame_Right", new Vector3(frame, height, frameDepth), new Vector3(width * 0.5f - frame * 0.5f, 0f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_BalconyFrame_Top", new Vector3(width, frame, frameDepth), new Vector3(0f, height * 0.5f - frame * 0.5f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Stretch_BalconyFrame_BottomRail", new Vector3(width, frame, frameDepth), new Vector3(0f, -height * 0.5f + frame * 0.5f, 0f), frameMaterial, meshes, true);
        AddCube(root, "Fixed_Center_Mullion", new Vector3(mullionWidth, glassHeight, frameDepth * 0.65f), new Vector3(0f, 0f, -0.03f), frameMaterial, meshes, true);

        AddCube(root, "Stretch_Glass_Left_SlidingPanel", new Vector3(glassWidth, glassHeight, glassDepth), new Vector3(-glassOffset, 0f, 0.08f), glassMaterial, meshes, false);
        AddCube(root, "Stretch_Glass_Right_SlidingPanel", new Vector3(glassWidth, glassHeight, glassDepth), new Vector3(glassOffset, 0f, 0.08f), glassMaterial, meshes, false);
    }

    private static void AddRailing(Transform root, Material railMaterial, List<Mesh> meshes)
    {
        float width = AuthoredSize.x;
        float height = AuthoredSize.y;
        float railHeight = 7.0f;
        float bottomInset = 0.55f;
        float railYCenter = -height * 0.5f + bottomInset + railHeight * 0.5f;
        float railZ = -1.05f;
        float railThickness = 0.25f;
        float postThickness = 0.28f;
        float barThickness = 0.12f;
        float barInsetX = 1.45f;
        float activeSpacing = 1.2f;
        int activeBars = 13;
        int maxBars = 32;

        AddCube(root, "Fixed_Railing_Left_Post", new Vector3(postThickness, railHeight, postThickness), new Vector3(-width * 0.5f + postThickness * 0.5f, railYCenter, railZ), railMaterial, meshes, true);
        AddCube(root, "Fixed_Railing_Right_Post", new Vector3(postThickness, railHeight, postThickness), new Vector3(width * 0.5f - postThickness * 0.5f, railYCenter, railZ), railMaterial, meshes, true);
        AddCube(root, "Stretch_Railing_Top_Rail", new Vector3(width, railThickness, railThickness), new Vector3(0f, -height * 0.5f + bottomInset + railHeight - railThickness * 0.5f, railZ), railMaterial, meshes, true);
        AddCube(root, "Stretch_Railing_Mid_Rail", new Vector3(width, railThickness * 0.82f, railThickness * 0.82f), new Vector3(0f, railYCenter, railZ), railMaterial, meshes, true);
        AddCube(root, "Stretch_Railing_Bottom_Rail", new Vector3(width, railThickness, railThickness), new Vector3(0f, -height * 0.5f + bottomInset + railThickness * 0.5f, railZ), railMaterial, meshes, true);

        float firstX = -width * 0.5f + barInsetX;
        for (int i = 0; i < maxBars; i++)
        {
            GameObject bar = AddCube(
                root,
                $"Fixed_Railing_Vertical_Bar_{i + 1:00}",
                new Vector3(barThickness, railHeight - railThickness * 2.2f, barThickness),
                new Vector3(firstX + activeSpacing * i, railYCenter, railZ),
                railMaterial,
                meshes,
                true);
            bar.SetActive(i < activeBars);
        }
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

    private static Material EnsureMaterial(string name, Color color, string shaderName, bool transparent)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            shader = Shader.Find(transparent ? "Transparent/Diffuse" : "Unlit/Color");
        }

        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        if (transparent)
        {
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0f);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            material.renderQueue = -1;
        }

        EditorUtility.SetDirty(material);
        return material;
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
