#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class BO001BoilerPrefabGenerator
{
    private const string Code = "BO001";
    private const string PrefabPath = "Assets/Prefabs/Furniture/Models/Prefabs/Furnish/BO001.prefab";
    private const string MeshAssetPath = "Assets/Prefabs/Furniture/Models/Prefabs/Furnish/BO001_Meshes.asset";
    private const string CatalogPath = "Assets/Prefabs/Furniture/FurnitureCatalog.asset";
    private const string ThumbnailPath = "Assets/Prefabs/Furniture/FurnitureCatalog_Thumbnails/BO001.png";
    private const string MaterialFolder = "Assets/Prefabs/Furniture/Models/Materials/Furnish/BO001";

    private static readonly Vector3 AuthoredSize = new Vector3(7f, 13f, 3f);

    [MenuItem("LH/Generate/BO001 Boiler Prefab")]
    public static void Generate()
    {
        EnsureDirectory(Path.GetDirectoryName(PrefabPath));
        EnsureDirectory(Path.GetDirectoryName(MeshAssetPath));
        EnsureDirectory(Path.GetDirectoryName(ThumbnailPath));
        EnsureDirectory(MaterialFolder);

        AssetDatabase.DeleteAsset(MeshAssetPath);

        Material bodyMaterial = EnsureMaterial("BO001_WarmWhite_Body", new Color(0.94f, 0.92f, 0.86f, 1f), 0f, 0.5f);
        Material panelMaterial = EnsureMaterial("BO001_SatinWhite_Panel", new Color(0.98f, 0.97f, 0.92f, 1f), 0f, 0.55f);
        Material shadowMaterial = EnsureMaterial("BO001_RecessShadow", new Color(0.43f, 0.42f, 0.38f, 1f), 0f, 0.25f);
        Material metalMaterial = EnsureMaterial("BO001_BrushedMetal_Pipe", new Color(0.68f, 0.66f, 0.60f, 1f), 0.5f, 0.42f);
        Material labelDarkMaterial = EnsureMaterial("BO001_Label_Dark", new Color(0.06f, 0.06f, 0.055f, 1f), 0f, 0.2f);
        Material labelYellowMaterial = EnsureMaterial("BO001_Label_Yellow", new Color(1f, 0.78f, 0.16f, 1f), 0f, 0.35f);
        Material labelRedMaterial = EnsureMaterial("BO001_Label_Red", new Color(0.78f, 0.08f, 0.06f, 1f), 0f, 0.32f);

        GameObject root = new GameObject(Code);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;

        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, AuthoredSize.y * 0.5f, 0f);
        collider.size = AuthoredSize;

        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(root.transform, false);

        List<Mesh> meshes = new List<Mesh>();
        AddBoiler(visuals.transform, bodyMaterial, panelMaterial, shadowMaterial, metalMaterial, labelDarkMaterial, labelYellowMaterial, labelRedMaterial, meshes);

        SaveMeshes(meshes);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Texture2D thumbnail = CreateThumbnail();
        UpsertCatalogItem(thumbnail);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Generated {Code} boiler prefab: {PrefabPath}");
    }

    private static void AddBoiler(
        Transform root,
        Material bodyMaterial,
        Material panelMaterial,
        Material shadowMaterial,
        Material metalMaterial,
        Material labelDarkMaterial,
        Material labelYellowMaterial,
        Material labelRedMaterial,
        List<Mesh> meshes)
    {
        float bodyWidth = 5.35f;
        float bodyHeight = 7.35f;
        float bodyDepth = 2.15f;
        float bodyBottom = 2.55f;
        float faceZ = -bodyDepth * 0.5f - 0.045f;

        AddCube(root, "Stretch_MainBoiler_Case", new Vector3(bodyWidth, bodyHeight, bodyDepth), new Vector3(0f, bodyBottom + bodyHeight * 0.5f, 0f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_MainBoiler_FrontPanel", new Vector3(bodyWidth - 0.28f, bodyHeight - 0.35f, 0.09f), new Vector3(0f, bodyBottom + bodyHeight * 0.5f, faceZ), panelMaterial, meshes, true);
        AddCube(root, "Fixed_MainBoiler_BottomShadowGap", new Vector3(bodyWidth - 0.35f, 0.11f, 0.08f), new Vector3(0f, bodyBottom + 0.28f, faceZ - 0.06f), shadowMaterial, meshes, false);
        AddCube(root, "Fixed_MainBoiler_TopVentSlot", new Vector3(bodyWidth - 0.7f, 0.08f, 0.08f), new Vector3(0f, bodyBottom + bodyHeight - 0.32f, faceZ - 0.06f), shadowMaterial, meshes, false);

        AddCube(root, "Fixed_BrandMark", new Vector3(1.05f, 0.13f, 0.055f), new Vector3(-0.22f, bodyBottom + 4.8f, faceZ - 0.08f), shadowMaterial, meshes, false);
        AddCube(root, "Fixed_Label_InspectionBlack", new Vector3(1.3f, 0.68f, 0.055f), new Vector3(-1.22f, bodyBottom + 2.7f, faceZ - 0.08f), labelDarkMaterial, meshes, false);
        AddCube(root, "Fixed_Label_InspectionRedStripe", new Vector3(1.3f, 0.18f, 0.058f), new Vector3(-1.22f, bodyBottom + 2.38f, faceZ - 0.09f), labelRedMaterial, meshes, false);
        AddCube(root, "Fixed_Label_WarningYellow", new Vector3(1.45f, 1.02f, 0.055f), new Vector3(1.0f, bodyBottom + 2.56f, faceZ - 0.08f), labelYellowMaterial, meshes, false);
        AddCube(root, "Fixed_EnergyBadge", new Vector3(0.68f, 0.42f, 0.055f), new Vector3(0.9f, bodyBottom + 1.12f, faceZ - 0.08f), labelYellowMaterial, meshes, false);
        AddCube(root, "Fixed_StatusMark", new Vector3(0.22f, 0.22f, 0.055f), new Vector3(0f, bodyBottom + 1.08f, faceZ - 0.08f), labelDarkMaterial, meshes, false);

        float serviceWidth = 4.8f;
        float serviceHeight = 2.25f;
        AddCube(root, "Fixed_LowerServiceBox", new Vector3(serviceWidth, serviceHeight, 2.05f), new Vector3(0f, serviceHeight * 0.5f, 0.06f), bodyMaterial, meshes, true);
        AddCube(root, "Fixed_LowerServiceDoor", new Vector3(serviceWidth - 0.42f, serviceHeight - 0.32f, 0.08f), new Vector3(0f, serviceHeight * 0.5f, faceZ + 0.1f), panelMaterial, meshes, true);
        AddCube(root, "Fixed_LowerServiceHandle", new Vector3(0.16f, 0.16f, 0.08f), new Vector3(-1.9f, 0.62f, faceZ + 0.02f), shadowMaterial, meshes, false);

        float pipeTopY = bodyBottom + bodyHeight + 0.15f;
        AddCylinder(root, "Fixed_ExhaustPipe_LeftCorrugated", 0.33f, 4.9f, new Vector3(-1.05f, pipeTopY + 1.4f, -0.15f), Quaternion.Euler(0f, 0f, -18f), metalMaterial, meshes, true, 16);
        AddCylinder(root, "Fixed_ExhaustPipe_RightCorrugated", 0.28f, 4.55f, new Vector3(1.03f, pipeTopY + 1.34f, -0.15f), Quaternion.Euler(0f, 0f, -12f), metalMaterial, meshes, true, 16);
        AddCylinder(root, "Fixed_ExhaustPipe_TopHorizontal", 0.35f, 3.8f, new Vector3(2.35f, pipeTopY + 3.0f, -0.2f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, meshes, true, 16);
        AddCylinder(root, "Fixed_ExhaustPipe_WallSocket", 0.5f, 1.05f, new Vector3(4.45f, pipeTopY + 3.0f, -0.2f), Quaternion.Euler(0f, 0f, 90f), metalMaterial, meshes, true, 20);
        AddCylinder(root, "Fixed_CombustionPipe_Center", 0.2f, 1.55f, new Vector3(0f, pipeTopY + 0.7f, -0.05f), Quaternion.identity, metalMaterial, meshes, true, 14);
        AddCylinder(root, "Fixed_GasPipe_BottomLeft", 0.12f, 2.2f, new Vector3(-2.2f, 1.85f, -0.15f), Quaternion.Euler(0f, 0f, -24f), metalMaterial, meshes, true, 12);
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

    private static GameObject AddCylinder(
        Transform parent,
        string name,
        float radius,
        float height,
        Vector3 localPosition,
        Quaternion localRotation,
        Material material,
        List<Mesh> meshes,
        bool castShadows,
        int segments)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.localPosition = localPosition;
        gameObject.transform.localRotation = localRotation;
        gameObject.transform.localScale = Vector3.one;

        Mesh mesh = CreateCylinderMesh(name, radius, height, Mathf.Max(8, segments));
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

    private static Mesh CreateCylinderMesh(string name, float radius, float height, int segments)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        float halfHeight = height * 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices.Add(new Vector3(x, -halfHeight, z));
            vertices.Add(new Vector3(x, halfHeight, z));
        }

        int bottomCenter = vertices.Count;
        vertices.Add(new Vector3(0f, -halfHeight, 0f));
        int topCenter = vertices.Count;
        vertices.Add(new Vector3(0f, halfHeight, 0f));

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int bottom = i * 2;
            int top = bottom + 1;
            int nextBottom = next * 2;
            int nextTop = nextBottom + 1;

            triangles.Add(bottom);
            triangles.Add(top);
            triangles.Add(nextTop);
            triangles.Add(bottom);
            triangles.Add(nextTop);
            triangles.Add(nextBottom);

            triangles.Add(bottomCenter);
            triangles.Add(nextBottom);
            triangles.Add(bottom);

            triangles.Add(topCenter);
            triangles.Add(top);
            triangles.Add(nextTop);
        }

        Mesh mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
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

    private static Texture2D CreateThumbnail()
    {
        Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, false) { name = Code };
        Color background = new Color(0.70f, 0.69f, 0.64f, 1f);
        Color body = new Color(0.98f, 0.97f, 0.92f, 1f);
        Color shadow = new Color(0.28f, 0.27f, 0.24f, 1f);
        Color metal = new Color(0.68f, 0.66f, 0.60f, 1f);
        Color yellow = new Color(1f, 0.78f, 0.16f, 1f);
        Color red = new Color(0.78f, 0.08f, 0.06f, 1f);

        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                texture.SetPixel(x, y, background);
            }
        }

        FillRect(texture, 38, 36, 52, 64, body);
        FillRect(texture, 41, 39, 46, 58, new Color(0.94f, 0.93f, 0.88f, 1f));
        FillRect(texture, 38, 100, 52, 19, body);
        FillRect(texture, 44, 57, 15, 8, shadow);
        FillRect(texture, 44, 51, 15, 4, red);
        FillRect(texture, 65, 53, 16, 15, yellow);
        FillRect(texture, 59, 77, 10, 5, yellow);
        FillRect(texture, 57, 82, 4, 4, shadow);
        DrawLine(texture, 54, 36, 47, 17, metal, 5);
        DrawLine(texture, 68, 36, 65, 16, metal, 4);
        DrawLine(texture, 65, 16, 105, 16, metal, 5);
        FillRect(texture, 103, 12, 14, 8, metal);
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
        itemProperty.FindPropertyRelative("displayName").stringValue = "BO001";
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

    private static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color color, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            FillRect(texture, x0 - thickness / 2, y0 - thickness / 2, thickness, thickness, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = err * 2;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
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
