using System.Collections.Generic;
using UnityEngine;

public sealed class PerspectiveSelectionHighlightController : MonoBehaviour
{
    private const string HighlightObjectName = "PerspectiveSelectionHighlight";
    private const string RoomTintObjectName = "PerspectiveSelectionRoomTint";
    private const string HighlightRootName = "PerspectiveSelectionHighlights";
    private const float BoundsEpsilon = 0.0001f;
    private const float MinimumHighlightSize = 0.01f;
    private const float MinimumHighlightAlpha = 0.9f;
    private const float MinimumRoomTintAlpha = 0.01f;
    private const float MaximumRoomTintAlpha = 0.2f;
    private const float MinimumLineWidth = 0.1f;

    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private Color highlightColor = new Color(0.1f, 0.85f, 1f, 1f);
    [SerializeField] private Color roomTintColor = new Color(0.1f, 0.85f, 1f, 0.12f);
    [SerializeField] private float boundsPadding = 0.08f;
    [SerializeField] private float lineWidth = 0.12f;
    [SerializeField] private float roomOutlineYOffset = 0.05f;
    [SerializeField] private float roomTintYOffset = 0.02f;

    private readonly List<GameObject> selectedWalls = new List<GameObject>();
    private readonly List<GameObject> highlightObjects = new List<GameObject>();
    private readonly List<Vector3> selectedRoomVertices = new List<Vector3>();
    private readonly List<Vector3> roomTintVertices = new List<Vector3>();
    private readonly List<int> roomTintTriangles = new List<int>();
    private readonly List<int> roomTintPolygonIndices = new List<int>();
    private Material runtimeHighlightMaterial;
    private Material runtimeHighlightSourceMaterial;
    private Material runtimeRoomTintMaterial;
    private Material runtimeRoomTintSourceMaterial;
    private Transform highlightRoot;
    private bool eventsBound;

    private void Awake()
    {
        NormalizeVisualSettings();
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindEvents();
        RefreshHighlight();
    }

    private void OnDisable()
    {
        UnbindEvents();
        ClearHighlight();
    }

    private void OnDestroy()
    {
        UnbindEvents();
        ClearHighlight();

        if (runtimeHighlightMaterial != null)
        {
            DestroyUnityObject(runtimeHighlightMaterial);
            runtimeHighlightMaterial = null;
            runtimeHighlightSourceMaterial = null;
        }

        if (runtimeRoomTintMaterial != null)
        {
            DestroyUnityObject(runtimeRoomTintMaterial);
            runtimeRoomTintMaterial = null;
            runtimeRoomTintSourceMaterial = null;
        }
    }

    private void OnValidate()
    {
        NormalizeVisualSettings();
    }

    public void RefreshHighlight()
    {
        ClearHighlight();

        if (!isActiveAndEnabled ||
            viewModeManager == null ||
            viewModeManager.CurrentViewMode != EditorViewMode.Perspective3D)
        {
            return;
        }

        Room selectedRoom = roomAuthoringPanelManager != null ? roomAuthoringPanelManager.SelectedRoom : null;
        if (selectedRoom != null)
        {
            ShowHighlightForTarget(selectedRoom.gameObject);
            return;
        }

        if (wallSelectionManager == null)
        {
            return;
        }

        GameObject primaryWall = wallSelectionManager.SelectedWall;
        ShowHighlightForTarget(primaryWall);

        selectedWalls.Clear();
        wallSelectionManager.GetSelectedWalls(selectedWalls);
        for (int i = 0; i < selectedWalls.Count; i++)
        {
            GameObject selectedWall = selectedWalls[i];
            if (selectedWall != null && selectedWall != primaryWall)
            {
                ShowHighlightForTarget(selectedWall);
            }
        }

        selectedWalls.Clear();
    }

    public bool ShowHighlightForTarget(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        if (target.TryGetComponent(out Wall wall) && TryCreateWallOutline(wall, out GameObject wallHighlight))
        {
            TrackHighlight(wallHighlight);
            return true;
        }

        if (target.TryGetComponent(out Room room) &&
            TryCreateRoomHighlight(room, out GameObject roomHighlight, out GameObject roomTint))
        {
            TrackHighlight(roomTint);
            TrackHighlight(roomHighlight);
            return true;
        }

        if (!TryGetTargetBounds(target, out Bounds bounds))
        {
            return false;
        }

        GameObject boundsHighlight = CreateBoundsOutline(bounds);
        TrackHighlight(boundsHighlight);
        return true;
    }

    private GameObject CreateBoundsOutline(Bounds bounds)
    {
        bounds.Expand(Vector3.one * boundsPadding);
        Vector3 size = bounds.size;
        size.x = Mathf.Max(size.x, MinimumHighlightSize);
        size.y = Mathf.Max(size.y, MinimumHighlightSize);
        size.z = Mathf.Max(size.z, MinimumHighlightSize);
        bounds.size = size;

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z),
        };

        return CreateLineHighlight(BuildBoxEdgePositions(corners), true);
    }

    private bool TryCreateWallOutline(Wall wall, out GameObject highlightObject)
    {
        highlightObject = null;
        if (wall == null || wall.Data == null)
        {
            return false;
        }

        WallData wallData = wall.Data;
        Vector3 start = wallData.startPoint;
        Vector3 end = wallData.endPoint;
        Vector3 direction = end - start;
        direction.y = 0f;
        if (direction.sqrMagnitude <= BoundsEpsilon * BoundsEpsilon)
        {
            return false;
        }

        direction.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        float halfThickness = Mathf.Max(Mathf.Abs(wallData.thickness), MinimumHighlightSize) * 0.5f + boundsPadding;
        float halfHeight = Mathf.Max(Mathf.Abs(wallData.height), MinimumHighlightSize) * 0.5f + boundsPadding;
        float centerY = wallData.centerY;
        Vector3 startCenter = new Vector3(start.x, centerY, start.z);
        Vector3 endCenter = new Vector3(end.x, centerY, end.z);

        Vector3[] corners =
        {
            startCenter - side * halfThickness + Vector3.down * halfHeight,
            startCenter + side * halfThickness + Vector3.down * halfHeight,
            endCenter + side * halfThickness + Vector3.down * halfHeight,
            endCenter - side * halfThickness + Vector3.down * halfHeight,
            startCenter - side * halfThickness + Vector3.up * halfHeight,
            startCenter + side * halfThickness + Vector3.up * halfHeight,
            endCenter + side * halfThickness + Vector3.up * halfHeight,
            endCenter - side * halfThickness + Vector3.up * halfHeight,
        };

        highlightObject = CreateLineHighlight(BuildBoxEdgePositions(corners), true);
        return true;
    }

    private bool TryCreateRoomHighlight(Room room, out GameObject highlightObject, out GameObject tintObject)
    {
        highlightObject = null;
        tintObject = null;
        if (room == null || !room.TryGetOrderedVertices(selectedRoomVertices) || selectedRoomVertices.Count < 3)
        {
            selectedRoomVertices.Clear();
            return false;
        }

        tintObject = CreateRoomTint(selectedRoomVertices, ResolveRoomTintY(room));

        float outlineY = ResolveRoomOutlineY(room);
        Vector3[] positions = new Vector3[selectedRoomVertices.Count + 1];
        for (int i = 0; i < selectedRoomVertices.Count; i++)
        {
            Vector3 vertex = selectedRoomVertices[i];
            vertex.y = outlineY;
            positions[i] = vertex;
        }

        positions[positions.Length - 1] = positions[0];
        selectedRoomVertices.Clear();
        highlightObject = CreateLineHighlight(positions, false);
        return true;
    }

    private GameObject CreateRoomTint(List<Vector3> roomVertices, float tintY)
    {
        roomTintVertices.Clear();
        for (int i = 0; i < roomVertices.Count; i++)
        {
            Vector3 vertex = roomVertices[i];
            vertex.y = tintY;
            roomTintVertices.Add(vertex);
        }

        PolygonUtility.SanitizePolygonVertices(roomTintVertices);
        if (roomTintVertices.Count < 3)
        {
            return null;
        }

        roomTintTriangles.Clear();
        if (!PolygonUtility.TryTriangulatePolygon(roomTintVertices, roomTintTriangles, roomTintPolygonIndices))
        {
            for (int i = 1; i < roomTintVertices.Count - 1; i++)
            {
                roomTintTriangles.Add(0);
                roomTintTriangles.Add(i);
                roomTintTriangles.Add(i + 1);
            }
        }

        EnsureTrianglesFaceUp(roomTintVertices, roomTintTriangles);

        Mesh tintMesh = new Mesh
        {
            name = RoomTintObjectName + "Mesh",
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
        };
        tintMesh.SetVertices(roomTintVertices);
        tintMesh.SetTriangles(roomTintTriangles, 0);
        tintMesh.RecalculateNormals();
        tintMesh.RecalculateBounds();

        GameObject tintObject = new GameObject(RoomTintObjectName);
        tintObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        tintObject.transform.SetParent(EnsureHighlightRoot(), false);

        MeshFilter meshFilter = tintObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = tintMesh;

        MeshRenderer meshRenderer = tintObject.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.allowOcclusionWhenDynamic = false;
        meshRenderer.sharedMaterial = GetRoomTintMaterial();

        return tintObject;
    }

    private float ResolveRoomOutlineY(Room room)
    {
        float outlineY = float.MinValue;
        if (room != null && room.WallSet != null)
        {
            foreach (Wall wall in room.WallSet)
            {
                if (wall == null || wall.Data == null)
                {
                    continue;
                }

                WallData wallData = wall.Data;
                float wallTopY = wallData.centerY + Mathf.Abs(wallData.height) * 0.5f;
                outlineY = Mathf.Max(outlineY, wallTopY);
            }
        }

        if (outlineY > float.MinValue)
        {
            return outlineY + roomOutlineYOffset + boundsPadding;
        }

        return selectedRoomVertices.Count > 0
            ? selectedRoomVertices[0].y + roomOutlineYOffset
            : roomOutlineYOffset;
    }

    private GameObject CreateLineHighlight(Vector3[] positions, bool edgePairs)
    {
        GameObject highlightObject = new GameObject(HighlightObjectName);
        highlightObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        highlightObject.transform.SetParent(EnsureHighlightRoot(), false);

        LineRenderer lineRenderer = highlightObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = positions.Length;
        lineRenderer.SetPositions(positions);
        lineRenderer.widthMultiplier = Mathf.Max(lineWidth, MinimumLineWidth);
        lineRenderer.numCornerVertices = edgePairs ? 0 : 2;
        lineRenderer.numCapVertices = 2;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.allowOcclusionWhenDynamic = false;
        lineRenderer.sortingOrder = 1000;
        lineRenderer.sharedMaterial = GetHighlightMaterial();
        Color visibleHighlightColor = GetVisibleHighlightColor();
        lineRenderer.startColor = visibleHighlightColor;
        lineRenderer.endColor = visibleHighlightColor;
        return highlightObject;
    }

    private void TrackHighlight(GameObject highlightObject)
    {
        if (highlightObject != null)
        {
            highlightObjects.Add(highlightObject);
        }
    }

    private static Vector3[] BuildBoxEdgePositions(Vector3[] corners)
    {
        return new[]
        {
            corners[0], corners[1],
            corners[1], corners[2],
            corners[2], corners[3],
            corners[3], corners[0],
            corners[4], corners[5],
            corners[5], corners[6],
            corners[6], corners[7],
            corners[7], corners[4],
            corners[0], corners[4],
            corners[1], corners[5],
            corners[2], corners[6],
            corners[3], corners[7],
        };
    }

    public void ClearHighlight()
    {
        for (int i = highlightObjects.Count - 1; i >= 0; i--)
        {
            GameObject highlightObject = highlightObjects[i];
            if (highlightObject != null)
            {
                DestroyHighlightObject(highlightObject);
            }
        }

        highlightObjects.Clear();

        if (highlightRoot != null && highlightRoot.childCount == 0)
        {
            DestroyUnityObject(highlightRoot.gameObject);
            highlightRoot = null;
        }
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref viewModeManager);
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveObject(ref roomAuthoringPanelManager);
    }

    private Transform EnsureHighlightRoot()
    {
        if (highlightRoot != null)
        {
            return highlightRoot;
        }

        GameObject rootObject = new GameObject(HighlightRootName);
        rootObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        rootObject.transform.SetParent(transform, false);
        highlightRoot = rootObject.transform;
        return highlightRoot;
    }

    private void BindEvents()
    {
        if (eventsBound)
        {
            return;
        }

        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged += HandleViewModeChanged;
        }

        if (wallSelectionManager != null)
        {
            wallSelectionManager.SelectionChanged += HandleWallSelectionChanged;
            wallSelectionManager.SelectionSetChanged += HandleWallSelectionSetChanged;
        }

        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged += HandleSelectedRoomChanged;
        }

        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound)
        {
            return;
        }

        if (viewModeManager != null)
        {
            viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        }

        if (wallSelectionManager != null)
        {
            wallSelectionManager.SelectionChanged -= HandleWallSelectionChanged;
            wallSelectionManager.SelectionSetChanged -= HandleWallSelectionSetChanged;
        }

        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged -= HandleSelectedRoomChanged;
        }

        eventsBound = false;
    }

    private void HandleViewModeChanged(EditorViewMode viewMode)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RefreshHighlight();
    }

    private void HandleWallSelectionChanged(GameObject selectedWall)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RefreshHighlight();
    }

    private void HandleWallSelectionSetChanged()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RefreshHighlight();
    }

    private void HandleSelectedRoomChanged(Room selectedRoom)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        RefreshHighlight();
    }

    private bool TryGetTargetBounds(GameObject target, out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null && !IsHighlightTransform(renderer.transform))
            {
                EncapsulateBounds(renderer.bounds, ref bounds, ref hasBounds);
            }
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && !IsHighlightTransform(collider.transform))
            {
                EncapsulateBounds(collider.bounds, ref bounds, ref hasBounds);
            }
        }

        return hasBounds;
    }

    private static bool IsHighlightTransform(Transform candidate)
    {
        while (candidate != null)
        {
            if (candidate.name == HighlightObjectName)
            {
                return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }

    private static bool EncapsulateBounds(Bounds candidate, ref Bounds bounds, ref bool hasBounds)
    {
        if (candidate.extents.sqrMagnitude <= BoundsEpsilon * BoundsEpsilon)
        {
            return false;
        }

        if (!hasBounds)
        {
            bounds = candidate;
            hasBounds = true;
            return true;
        }

        bounds.Encapsulate(candidate);
        return true;
    }

    private float ResolveRoomTintY(Room room)
    {
        float tintY = float.MinValue;
        if (room != null && room.WallSet != null)
        {
            foreach (Wall wall in room.WallSet)
            {
                if (wall == null || wall.Data == null)
                {
                    continue;
                }

                WallData wallData = wall.Data;
                float wallTopY = wallData.centerY + Mathf.Abs(wallData.height) * 0.5f;
                tintY = Mathf.Max(tintY, wallTopY);
            }
        }

        if (tintY > float.MinValue)
        {
            return tintY + roomTintYOffset;
        }

        return selectedRoomVertices.Count > 0
            ? selectedRoomVertices[0].y + roomTintYOffset
            : roomTintYOffset;
    }

    private Material GetHighlightMaterial()
    {
        Color visibleHighlightColor = GetVisibleHighlightColor();

        if (highlightMaterial != null)
        {
            if (runtimeHighlightMaterial == null || runtimeHighlightSourceMaterial != highlightMaterial)
            {
                if (runtimeHighlightMaterial != null)
                {
                    DestroyUnityObject(runtimeHighlightMaterial);
                }

                runtimeHighlightMaterial = new Material(highlightMaterial)
                {
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                };
                runtimeHighlightSourceMaterial = highlightMaterial;
            }

            ConfigureTransparentMaterial(runtimeHighlightMaterial, visibleHighlightColor);
            return runtimeHighlightMaterial;
        }

        if (runtimeHighlightMaterial != null)
        {
            ConfigureTransparentMaterial(runtimeHighlightMaterial, visibleHighlightColor);
            return runtimeHighlightMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeHighlightMaterial = new Material(shader)
        {
            color = visibleHighlightColor,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
        };
        runtimeHighlightSourceMaterial = null;
        ConfigureTransparentMaterial(runtimeHighlightMaterial, visibleHighlightColor);
        return runtimeHighlightMaterial;
    }

    private Material GetRoomTintMaterial()
    {
        if (highlightMaterial != null)
        {
            if (runtimeRoomTintMaterial == null || runtimeRoomTintSourceMaterial != highlightMaterial)
            {
                if (runtimeRoomTintMaterial != null)
                {
                    DestroyUnityObject(runtimeRoomTintMaterial);
                }

                runtimeRoomTintMaterial = new Material(highlightMaterial)
                {
                    hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
                };
                runtimeRoomTintSourceMaterial = highlightMaterial;
            }

            ConfigureTransparentMaterial(runtimeRoomTintMaterial, roomTintColor);
            return runtimeRoomTintMaterial;
        }

        if (runtimeRoomTintMaterial != null)
        {
            ConfigureTransparentMaterial(runtimeRoomTintMaterial, roomTintColor);
            return runtimeRoomTintMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        runtimeRoomTintMaterial = new Material(shader)
        {
            color = roomTintColor,
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild,
        };
        runtimeRoomTintSourceMaterial = null;
        ConfigureTransparentMaterial(runtimeRoomTintMaterial, roomTintColor);
        return runtimeRoomTintMaterial;
    }

    private void NormalizeVisualSettings()
    {
        boundsPadding = Mathf.Max(0f, boundsPadding);
        lineWidth = Mathf.Max(MinimumLineWidth, lineWidth);
        roomOutlineYOffset = Mathf.Max(0f, roomOutlineYOffset);
        roomTintYOffset = Mathf.Max(0f, roomTintYOffset);
        highlightColor.a = Mathf.Max(MinimumHighlightAlpha, highlightColor.a);
        roomTintColor.a = Mathf.Clamp(roomTintColor.a, MinimumRoomTintAlpha, MaximumRoomTintAlpha);
    }

    private Color GetVisibleHighlightColor()
    {
        Color visibleHighlightColor = highlightColor;
        visibleHighlightColor.a = Mathf.Max(MinimumHighlightAlpha, visibleHighlightColor.a);
        return visibleHighlightColor;
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        SetMaterialFloatIfPresent(material, "_Surface", 1f);
        SetMaterialFloatIfPresent(material, "_Blend", 0f);
        SetMaterialFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetMaterialFloatIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetMaterialFloatIfPresent(material, "_ZWrite", 0f);
        SetMaterialFloatIfPresent(material, "_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        SetMaterialFloatIfPresent(material, "_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetMaterialFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
        {
            material.SetFloat(propertyName, value);
        }
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private static void DestroyHighlightObject(GameObject highlightObject)
    {
        if (highlightObject == null)
        {
            return;
        }

        MeshFilter meshFilter = highlightObject.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            DestroyUnityObject(meshFilter.sharedMesh);
            meshFilter.sharedMesh = null;
        }

        DestroyUnityObject(highlightObject);
    }

    private static void EnsureTrianglesFaceUp(List<Vector3> vertices, List<int> triangles)
    {
        if (vertices == null || triangles == null || triangles.Count < 3)
        {
            return;
        }

        int firstTriangleIndex = triangles[0];
        int secondTriangleIndex = triangles[1];
        int thirdTriangleIndex = triangles[2];

        Vector3 a = vertices[firstTriangleIndex];
        Vector3 b = vertices[secondTriangleIndex];
        Vector3 c = vertices[thirdTriangleIndex];
        Vector3 normal = Vector3.Cross(b - a, c - a);

        if (normal.y >= 0f)
        {
            return;
        }

        for (int i = 0; i < triangles.Count; i += 3)
        {
            int temp = triangles[i + 1];
            triangles[i + 1] = triangles[i + 2];
            triangles[i + 2] = temp;
        }
    }
}
