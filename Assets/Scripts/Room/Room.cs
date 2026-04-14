using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Room : MonoBehaviour
{
    private const float EndpointMatchThreshold = 0.2f;
    private const string FloorObjectName = "Floor";
    private const string CeilingObjectName = "Ceiling";
    private const float CeilingColliderThickness = 0.02f;

    public HashSet<Wall> WallSet { get; private set; }
    public IReadOnlyList<Vector3> ManualBoundaryVertices => manualBoundaryVertices;
    public RoomGeometry Geometry { get; private set; }
    public string RoomName => roomName;
    public string RoomTypeKey => roomTypeKey;
    public string RoomCode => roomCode;
    public string FloorTextureCode => floorTextureCode;
    public string CeilingTextureCode => ceilingTextureCode;

    [SerializeField] private string roomName = string.Empty;
    [SerializeField] private string roomTypeKey = string.Empty;
    [SerializeField] private string roomCode = string.Empty;
    [SerializeField] private string floorTextureCode = string.Empty;
    [SerializeField] private string ceilingTextureCode = string.Empty;

    private Mesh roomMesh;
    private GameObject floorObject;
    private MeshRenderer floorMeshRenderer;
    private MeshFilter floorMeshFilter;
    private GameObject ceilingObject;
    private MeshRenderer ceilingMeshRenderer;
    private MeshFilter ceilingMeshFilter;
    private BoxCollider ceilingCollider;
    private Material roomMaterial;
    private Material runtimeRoomMaterial;
    private Material runtimeCeilingMaterial;
    private Color roomColor;
    private Color selectionColor = new Color(0.28f, 0.6f, 1f, 0.42f);
    private Vector3 placementOffset;
    private bool isSelected;
    private readonly List<Vector3> manualBoundaryVertices = new List<Vector3>();

    private readonly List<Vector3> cachedVertices = new List<Vector3>();
    private readonly List<Vector3> cachedLocalVertices = new List<Vector3>();
    private readonly List<int> cachedTriangles = new List<int>();
    private readonly List<Vector3> cachedTriangulationVertices = new List<Vector3>();
    private readonly List<int> cachedPolygonIndices = new List<int>();
    private static Material defaultRoomMaterial;

    public void Initialize(HashSet<Wall> wallSet, RoomGeometry geometry)
    {
        WallSet = wallSet != null ? new HashSet<Wall>(wallSet) : new HashSet<Wall>();
        manualBoundaryVertices.Clear();
        Geometry = geometry;
    }

    public void Initialize(HashSet<Wall> wallSet, RoomGeometry geometry, List<Vector3> polygonVertices)
    {
        WallSet = wallSet != null ? new HashSet<Wall>(wallSet) : new HashSet<Wall>();
        manualBoundaryVertices.Clear();
        PolygonUtility.CopySanitizedVertices(polygonVertices, manualBoundaryVertices);

        Geometry = geometry;
    }

    public static List<Vector3> CreateSanitizedPolygonCopy(IReadOnlyList<Vector3> source)
    {
        return PolygonUtility.CreateSanitizedPolygonCopy(source);
    }

    public void SetPlacementOffset(Vector3 offset)
    {
        placementOffset = offset;
    }

    public bool SetManualBoundaryVertices(IReadOnlyList<Vector3> polygonVertices, bool clearWallSet = false)
    {
        PolygonUtility.CopySanitizedVertices(polygonVertices, manualBoundaryVertices);
        if (manualBoundaryVertices.Count < 3)
        {
            return false;
        }

        if (clearWallSet)
        {
            WallSet = new HashSet<Wall>();
        }

        Geometry = PolygonUtility.CalculateGeometry(manualBoundaryVertices);
        CreateOrUpdateVisual();
        return true;
    }

    public void SetMaterial(Material material, Color color)
    {
        roomMaterial = material;
        roomColor = color;
        CreateOrUpdateVisual();
    }

    public void SetSelectionState(bool selected, Color highlightColor)
    {
        isSelected = selected;
        selectionColor = highlightColor;
        ApplyRuntimeColors();
    }

    public void SetRoomTypeKey(string typeKey)
    {
        roomTypeKey = typeKey ?? string.Empty;
    }

    public void SetRoomName(string value)
    {
        roomName = value ?? string.Empty;
    }

    public void SetRoomCode(string value)
    {
        roomCode = value ?? string.Empty;
    }

    public void SetFloorTextureCode(string value)
    {
        floorTextureCode = value ?? string.Empty;
    }

    public void SetCeilingTextureCode(string value)
    {
        ceilingTextureCode = value ?? string.Empty;
    }

    public bool ReplaceWallReferences(ICollection<Wall> removedWalls, IEnumerable<Wall> addedWalls)
    {
        if (WallSet == null)
        {
            return false;
        }

        bool changed = false;
        if (removedWalls != null)
        {
            foreach (Wall wall in removedWalls)
            {
                if (wall != null && WallSet.Remove(wall))
                {
                    changed = true;
                }
            }
        }

        if (addedWalls != null)
        {
            foreach (Wall wall in addedWalls)
            {
                if (wall != null && WallSet.Add(wall))
                {
                    changed = true;
                }
            }
        }

        if (changed)
        {
            CreateOrUpdateVisual();
        }

        return changed;
    }

    public void RefreshVisual()
    {
        if ((WallSet == null || WallSet.Count == 0) && manualBoundaryVertices.Count == 0)
        {
            return;
        }

        CreateOrUpdateVisual();
    }

    private void CreateOrUpdateVisual()
    {
        if (!TryBuildOrderedVertices(out List<Vector3> worldVertices) || worldVertices.Count < 3)
        {
            return;
        }

        RoomGeometry geometry = PolygonUtility.CalculateGeometry(worldVertices);
        ApplyTransformFromGeometry(geometry);
        BuildLocalVertices(worldVertices, geometry.Center, cachedLocalVertices);

        if (roomMaterial == null)
        {
            roomMaterial = GetDefaultRoomMaterial();
        }

        EnsureFloorVisual();

        roomMesh = GenerateRoomMesh(cachedLocalVertices);
        floorMeshFilter.sharedMesh = roomMesh;

        if (runtimeRoomMaterial == null)
        {
            runtimeRoomMaterial = new Material(roomMaterial);
        }

        ApplyRuntimeColors();

        // Rooms that share walls can be generated with opposite winding; keep fill visible from both sides.
        if (runtimeRoomMaterial.HasProperty("_Cull"))
        {
            runtimeRoomMaterial.SetInt("_Cull", (int)CullMode.Off);
        }

        floorMeshRenderer.sharedMaterial = runtimeRoomMaterial;

        UpdateCeilingVisual();
        Geometry = geometry;
        CacheVertices(worldVertices);
    }

    private void ApplyRuntimeColors()
    {
        if (runtimeRoomMaterial != null)
        {
            runtimeRoomMaterial.color = isSelected ? selectionColor : roomColor;
        }
    }

    private bool TryBuildOrderedVertices(out List<Vector3> vertices)
    {
        if (manualBoundaryVertices.Count >= 3)
        {
            vertices = CreateSanitizedPolygonCopy(manualBoundaryVertices);
            return vertices.Count >= 3;
        }

        return RoomGraphUtility.TryBuildOrderedVertices(
            WallSet,
            EndpointMatchThreshold,
            VirtualBoundary.All,
            out vertices);
    }

    public bool TryGetOrderedVertices(List<Vector3> results)
    {
        if (results == null)
        {
            return false;
        }

        results.Clear();
        if (!TryBuildOrderedVertices(out List<Vector3> vertices) || vertices == null || vertices.Count < 3)
        {
            return false;
        }

        results.AddRange(vertices);
        return true;
    }

    public bool HasSameWallSet(HashSet<Wall> wallSet)
    {
        bool wallsMatch = WallSet == null
            ? wallSet == null || wallSet.Count == 0
            : wallSet != null && WallSet.SetEquals(wallSet);
        return wallsMatch;
    }

    private Mesh GenerateRoomMesh(List<Vector3> vertices)
    {
        if (roomMesh == null)
        {
            roomMesh = new Mesh
            {
                name = "RoomMesh",
            };
        }
        else
        {
            roomMesh.Clear();
        }

        cachedTriangulationVertices.Clear();
        cachedTriangulationVertices.AddRange(vertices);
        PolygonUtility.SanitizePolygonVertices(cachedTriangulationVertices);

        if (cachedTriangulationVertices.Count < 3)
        {
            return roomMesh;
        }

        cachedTriangles.Clear();
        if (!PolygonUtility.TryTriangulatePolygon(cachedTriangulationVertices, cachedTriangles, cachedPolygonIndices))
        {
            Debug.LogWarning("Room triangulation fell back to fan triangulation because ear clipping failed.", this);
            for (int i = 1; i < cachedTriangulationVertices.Count - 1; i++)
            {
                cachedTriangles.Add(0);
                cachedTriangles.Add(i);
                cachedTriangles.Add(i + 1);
            }
        }

        roomMesh.SetVertices(cachedTriangulationVertices);
        roomMesh.SetTriangles(cachedTriangles, 0);
        roomMesh.RecalculateNormals();
        roomMesh.RecalculateBounds();
        return roomMesh;
    }

    private void CacheVertices(List<Vector3> vertices)
    {
        cachedVertices.Clear();
        cachedVertices.AddRange(vertices);
    }

    private void ApplyTransformFromGeometry(RoomGeometry geometry)
    {
        Vector3 worldPosition = geometry.Center + placementOffset;
        if (transform.parent != null)
        {
            transform.localPosition = transform.parent.InverseTransformPoint(worldPosition);
        }
        else
        {
            transform.position = worldPosition;
        }

        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private void BuildLocalVertices(List<Vector3> worldVertices, Vector3 center, List<Vector3> results)
    {
        results.Clear();
        for (int i = 0; i < worldVertices.Count; i++)
        {
            results.Add(worldVertices[i] - center);
        }
    }

    private static Material GetDefaultRoomMaterial()
    {
        if (defaultRoomMaterial != null)
        {
            return defaultRoomMaterial;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            return null;
        }

        defaultRoomMaterial = new Material(shader);
        return defaultRoomMaterial;
    }

    private void EnsureFloorVisual()
    {
        if (floorObject == null)
        {
            Transform existing = transform.Find(FloorObjectName);
            floorObject = existing != null ? existing.gameObject : null;
        }

        if (floorObject == null)
        {
            floorObject = new GameObject(FloorObjectName);
            floorObject.transform.SetParent(transform, false);
            LayerUtility.ApplyLayer(floorObject, LayerUtility.FloorLayerName, false);
        }

        if (floorMeshFilter == null)
        {
            floorMeshFilter = floorObject.GetComponent<MeshFilter>();
            if (floorMeshFilter == null)
            {
                floorMeshFilter = floorObject.AddComponent<MeshFilter>();
            }
        }

        if (floorMeshRenderer == null)
        {
            floorMeshRenderer = floorObject.GetComponent<MeshRenderer>();
            if (floorMeshRenderer == null)
            {
                floorMeshRenderer = floorObject.AddComponent<MeshRenderer>();
            }
        }

        floorObject.transform.localPosition = Vector3.zero;
        floorObject.transform.localRotation = Quaternion.identity;
        floorObject.transform.localScale = Vector3.one;
    }

    private void UpdateCeilingVisual()
    {
        EnsureCeilingVisual();
        if (ceilingObject == null)
        {
            return;
        }

        float ceilingWorldY = GetCeilingWorldY();
        float localCeilingY = ceilingWorldY - transform.position.y;
        ceilingObject.transform.localPosition = new Vector3(0f, localCeilingY, 0f);
        ceilingObject.transform.localRotation = Quaternion.identity;
        ceilingObject.transform.localScale = Vector3.one;

        if (ceilingMeshFilter != null)
        {
            ceilingMeshFilter.sharedMesh = null;
        }

        if (runtimeCeilingMaterial == null && roomMaterial != null)
        {
            runtimeCeilingMaterial = new Material(roomMaterial);
        }

        if (runtimeCeilingMaterial != null)
        {
            runtimeCeilingMaterial.color = roomColor;
            if (runtimeCeilingMaterial.HasProperty("_Cull"))
            {
                runtimeCeilingMaterial.SetInt("_Cull", (int)CullMode.Off);
            }
        }

        if (ceilingMeshRenderer != null)
        {
            ceilingMeshRenderer.sharedMaterial = runtimeCeilingMaterial;
        }

        if (ceilingCollider != null)
        {
            Bounds bounds = CalculateLocalVertexBounds(cachedLocalVertices);
            Vector3 size = bounds.size;
            size.y = CeilingColliderThickness;
            if (size.x <= 0f)
            {
                size.x = CeilingColliderThickness;
            }

            if (size.z <= 0f)
            {
                size.z = CeilingColliderThickness;
            }

            ceilingCollider.center = new Vector3(bounds.center.x, 0f, bounds.center.z);
            ceilingCollider.size = size;
        }
    }

    private void EnsureCeilingVisual()
    {
        if (ceilingObject == null)
        {
            Transform existing = transform.Find(CeilingObjectName);
            ceilingObject = existing != null ? existing.gameObject : null;
        }

        if (ceilingObject == null)
        {
            ceilingObject = new GameObject(CeilingObjectName);
            ceilingObject.transform.SetParent(transform, false);
            LayerUtility.ApplyLayer(ceilingObject, LayerUtility.FloorLayerName, false);
        }

        if (ceilingMeshFilter == null)
        {
            ceilingMeshFilter = ceilingObject.GetComponent<MeshFilter>();
            if (ceilingMeshFilter == null)
            {
                ceilingMeshFilter = ceilingObject.AddComponent<MeshFilter>();
            }
        }

        if (ceilingMeshRenderer == null)
        {
            ceilingMeshRenderer = ceilingObject.GetComponent<MeshRenderer>();
            if (ceilingMeshRenderer == null)
            {
                ceilingMeshRenderer = ceilingObject.AddComponent<MeshRenderer>();
            }
        }

        if (ceilingCollider == null)
        {
            ceilingCollider = ceilingObject.GetComponent<BoxCollider>();
            if (ceilingCollider == null)
            {
                ceilingCollider = ceilingObject.AddComponent<BoxCollider>();
            }
        }
    }

    private float GetCeilingWorldY()
    {
        float bestY = Geometry.Center.y;
        if (WallSet == null)
        {
            return bestY;
        }

        foreach (Wall wall in WallSet)
        {
            if (wall == null)
            {
                continue;
            }

            float wallTopY = wall.transform.position.y + wall.transform.localScale.y * 0.5f;
            if (wallTopY > bestY)
            {
                bestY = wallTopY;
            }
        }

        return bestY;
    }

    private static Bounds CalculateLocalVertexBounds(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count == 0)
        {
            return new Bounds(Vector3.zero, Vector3.zero);
        }

        Bounds bounds = new Bounds(vertices[0], Vector3.zero);
        for (int i = 1; i < vertices.Count; i++)
        {
            bounds.Encapsulate(vertices[i]);
        }

        return bounds;
    }

    public void Delete()
    {
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.DeleteRoom(this);
            return;
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmos()
    {
        if (WallSet == null || WallSet.Count < 3)
        {
            return;
        }

        Gizmos.color = roomColor;
        Gizmos.DrawWireSphere(Geometry.Center, 0.2f);
    }

    private void OnDestroy()
    {
        if (roomMesh != null)
        {
            Destroy(roomMesh);
            roomMesh = null;
        }

        if (runtimeRoomMaterial != null)
        {
            Destroy(runtimeRoomMaterial);
            runtimeRoomMaterial = null;
        }

        if (runtimeCeilingMaterial != null)
        {
            Destroy(runtimeCeilingMaterial);
            runtimeCeilingMaterial = null;
        }
    }
}
