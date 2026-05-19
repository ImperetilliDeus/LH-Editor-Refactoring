using System.Collections.Generic;
using UnityEngine;

public sealed class PerspectiveSelectionHighlightController : MonoBehaviour
{
    private const string HighlightObjectName = "PerspectiveSelectionHighlight";
    private const string HighlightRootName = "PerspectiveSelectionHighlights";
    private const float BoundsEpsilon = 0.0001f;
    private const float MinimumHighlightSize = 0.01f;

    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
    [SerializeField] private Material highlightMaterial;
    [SerializeField] private Color highlightColor = new Color(0.25f, 0.65f, 1f, 0.22f);
    [SerializeField] private float boundsPadding = 0.08f;

    private readonly List<GameObject> selectedWalls = new List<GameObject>();
    private readonly List<GameObject> highlightObjects = new List<GameObject>();
    private Material runtimeHighlightMaterial;
    private Transform highlightRoot;
    private bool eventsBound;

    private void Awake()
    {
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
        }
    }

    private void OnValidate()
    {
        boundsPadding = Mathf.Max(0f, boundsPadding);
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
        if (target == null || !TryGetTargetBounds(target, out Bounds bounds))
        {
            return false;
        }

        bounds.Expand(Vector3.one * boundsPadding);
        Vector3 size = bounds.size;
        size.x = Mathf.Max(size.x, MinimumHighlightSize);
        size.y = Mathf.Max(size.y, MinimumHighlightSize);
        size.z = Mathf.Max(size.z, MinimumHighlightSize);

        Transform root = EnsureHighlightRoot();
        GameObject highlightObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        highlightObject.name = HighlightObjectName;
        highlightObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        highlightObject.transform.SetParent(root, false);
        highlightObject.transform.position = bounds.center;
        highlightObject.transform.rotation = Quaternion.identity;
        highlightObject.transform.localScale = size;

        Collider collider = highlightObject.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyUnityObject(collider);
        }

        Renderer renderer = highlightObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetHighlightMaterial();
        }

        highlightObjects.Add(highlightObject);
        return true;
    }

    public void ClearHighlight()
    {
        for (int i = highlightObjects.Count - 1; i >= 0; i--)
        {
            GameObject highlightObject = highlightObjects[i];
            if (highlightObject != null)
            {
                DestroyUnityObject(highlightObject);
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

    private Material GetHighlightMaterial()
    {
        if (highlightMaterial != null)
        {
            return highlightMaterial;
        }

        if (runtimeHighlightMaterial != null)
        {
            runtimeHighlightMaterial.color = highlightColor;
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
            color = highlightColor,
        };
        ConfigureTransparentMaterial(runtimeHighlightMaterial, highlightColor);
        return runtimeHighlightMaterial;
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
}
