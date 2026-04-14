using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Room : MonoBehaviour
{
    private const float EndpointMatchThreshold = 0.2f;
    private const string FloorObjectName = "Floor";
    private const string CeilingObjectName = "Ceiling";
    private const float CeilingColliderThickness = 0.02f;
    private const float PolygonEpsilon = 0.0001f;
    private const float PolygonEpsilonSqr = 0.00000001f;

    public HashSet<Wall> WallSet { get; private set; }
    public IReadOnlyList<Vector3> ManualBoundaryVertices => manualBoundaryVertices;
    public RoomGeometry Geometry { get; private set; }
    public string RoomTypeKey => roomTypeKey;
    public string RoomCode => roomCode;
    public string FloorTextureCode => floorTextureCode;
    public string CeilingTextureCode => ceilingTextureCode;

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
        CopySanitizedVertices(polygonVertices, manualBoundaryVertices);

        Geometry = geometry;
    }

    public static List<Vector3> CreateSanitizedPolygonCopy(IReadOnlyList<Vector3> source)
    {
        List<Vector3> results = new List<Vector3>();
        CopySanitizedVertices(source, results);
        return results;
    }

    public void SetPlacementOffset(Vector3 offset)
    {
        placementOffset = offset;
    }

    public bool SetManualBoundaryVertices(IReadOnlyList<Vector3> polygonVertices, bool clearWallSet = false)
    {
        CopySanitizedVertices(polygonVertices, manualBoundaryVertices);
        if (manualBoundaryVertices.Count < 3)
        {
            return false;
        }

        if (clearWallSet)
        {
            WallSet = new HashSet<Wall>();
        }

        Geometry = CalculateGeometry(manualBoundaryVertices);
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

        RoomGeometry geometry = CalculateGeometry(worldVertices);
        ApplyTransformFromGeometry(geometry);
        BuildLocalVertices(worldVertices, geometry.Center, cachedLocalVertices);

        if (roomMaterial == null)
        {
            roomMaterial = GetDefaultRoomMaterial();
        }

        EnsureFloorVisual();

        if (roomMesh != null)
        {
            Destroy(roomMesh);
        }

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
            FindObjectsByType<VirtualBoundary>(FindObjectsInactive.Include, FindObjectsSortMode.None),
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
        cachedTriangulationVertices.Clear();
        cachedTriangulationVertices.AddRange(vertices);
        SanitizePolygonVertices(cachedTriangulationVertices);

        if (cachedTriangulationVertices.Count < 3)
        {
            return new Mesh();
        }

        cachedTriangles.Clear();
        if (!TryTriangulatePolygon(cachedTriangulationVertices, cachedTriangles))
        {
            Debug.LogWarning("Room triangulation fell back to fan triangulation because ear clipping failed.", this);
            for (int i = 1; i < cachedTriangulationVertices.Count - 1; i++)
            {
                cachedTriangles.Add(0);
                cachedTriangles.Add(i);
                cachedTriangles.Add(i + 1);
            }
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(cachedTriangulationVertices);
        mesh.SetTriangles(cachedTriangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void CopySanitizedVertices(IReadOnlyList<Vector3> source, List<Vector3> destination)
    {
        destination.Clear();
        if (source == null)
        {
            return;
        }

        for (int i = 0; i < source.Count; i++)
        {
            destination.Add(source[i]);
        }

        SanitizePolygonVertices(destination);
    }

    private static void SanitizePolygonVertices(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return;
        }

        RemoveSequentialDuplicateVertices(vertices);
        RemoveCollinearVertices(vertices);
        RemoveSequentialDuplicateVertices(vertices);
        EnsureCounterClockwiseWinding(vertices);
    }

    private static void EnsureCounterClockwiseWinding(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return;
        }

        float signedArea2 = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 a = vertices[i];
            Vector3 b = vertices[(i + 1) % vertices.Count];
            signedArea2 += (a.x * b.z) - (b.x * a.z);
        }

        if (signedArea2 < 0f)
        {
            vertices.Reverse();
        }
    }

    private static void RemoveSequentialDuplicateVertices(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 2)
        {
            return;
        }

        bool removedAny = true;
        while (removedAny && vertices.Count >= 2)
        {
            removedAny = false;
            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                Vector3 current = vertices[i];
                Vector3 next = vertices[(i + 1) % vertices.Count];
                if ((current - next).sqrMagnitude > PolygonEpsilonSqr)
                {
                    continue;
                }

                vertices.RemoveAt(i);
                removedAny = true;
            }
        }
    }

    private static void RemoveCollinearVertices(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return;
        }

        bool removedAny = true;
        while (removedAny && vertices.Count >= 3)
        {
            removedAny = false;
            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                Vector3 previous = vertices[(i - 1 + vertices.Count) % vertices.Count];
                Vector3 current = vertices[i];
                Vector3 next = vertices[(i + 1) % vertices.Count];

                Vector2 a = new Vector2(current.x - previous.x, current.z - previous.z);
                Vector2 b = new Vector2(next.x - current.x, next.z - current.z);
                if (a.sqrMagnitude <= PolygonEpsilonSqr || b.sqrMagnitude <= PolygonEpsilonSqr)
                {
                    vertices.RemoveAt(i);
                    removedAny = true;
                    continue;
                }

                float cross = a.x * b.y - a.y * b.x;
                if (Mathf.Abs(cross) > PolygonEpsilon)
                {
                    continue;
                }

                vertices.RemoveAt(i);
                removedAny = true;
            }
        }
    }

    private bool TryTriangulatePolygon(List<Vector3> vertices, List<int> triangles)
    {
        if (vertices == null || triangles == null)
        {
            return false;
        }

        SanitizePolygonVertices(vertices);
        if (vertices.Count < 3)
        {
            return false;
        }

        triangles.Clear();
        cachedPolygonIndices.Clear();
        for (int i = 0; i < vertices.Count; i++)
        {
            cachedPolygonIndices.Add(i);
        }

        int safetyLimit = vertices.Count * vertices.Count;
        while (cachedPolygonIndices.Count > 3 && safetyLimit-- > 0)
        {
            bool clippedEar = false;
            for (int i = 0; i < cachedPolygonIndices.Count; i++)
            {
                int previousIndex = cachedPolygonIndices[(i - 1 + cachedPolygonIndices.Count) % cachedPolygonIndices.Count];
                int currentIndex = cachedPolygonIndices[i];
                int nextIndex = cachedPolygonIndices[(i + 1) % cachedPolygonIndices.Count];

                Vector3 previous = vertices[previousIndex];
                Vector3 current = vertices[currentIndex];
                Vector3 next = vertices[nextIndex];

                if (!IsConvexCorner(previous, current, next))
                {
                    continue;
                }

                if (IsDiagonalIntersectingPolygon(vertices, cachedPolygonIndices, previousIndex, nextIndex))
                {
                    continue;
                }

                if (ContainsAnyVertexInTriangle(vertices, cachedPolygonIndices, previousIndex, currentIndex, nextIndex))
                {
                    continue;
                }

                triangles.Add(previousIndex);
                triangles.Add(currentIndex);
                triangles.Add(nextIndex);
                cachedPolygonIndices.RemoveAt(i);
                clippedEar = true;
                break;
            }

            if (!clippedEar)
            {
                triangles.Clear();
                return false;
            }
        }

        if (cachedPolygonIndices.Count != 3)
        {
            triangles.Clear();
            return false;
        }

        triangles.Add(cachedPolygonIndices[0]);
        triangles.Add(cachedPolygonIndices[1]);
        triangles.Add(cachedPolygonIndices[2]);
        return true;
    }

    private static bool IsConvexCorner(Vector3 previous, Vector3 current, Vector3 next)
    {
        Vector2 a = new Vector2(current.x - previous.x, current.z - previous.z);
        Vector2 b = new Vector2(next.x - current.x, next.z - current.z);
        float cross = a.x * b.y - a.y * b.x;
        return cross > PolygonEpsilon;
    }

    private static bool IsDiagonalIntersectingPolygon(List<Vector3> vertices, List<int> polygonIndices, int aIndex, int bIndex)
    {
        Vector3 diagonalStart = vertices[aIndex];
        Vector3 diagonalEnd = vertices[bIndex];

        for (int i = 0; i < polygonIndices.Count; i++)
        {
            int edgeStartIndex = polygonIndices[i];
            int edgeEndIndex = polygonIndices[(i + 1) % polygonIndices.Count];
            if (edgeStartIndex == aIndex || edgeStartIndex == bIndex || edgeEndIndex == aIndex || edgeEndIndex == bIndex)
            {
                continue;
            }

            if (SegmentsIntersectXZ(diagonalStart, diagonalEnd, vertices[edgeStartIndex], vertices[edgeEndIndex]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAnyVertexInTriangle(List<Vector3> vertices, List<int> polygonIndices, int aIndex, int bIndex, int cIndex)
    {
        for (int i = 0; i < polygonIndices.Count; i++)
        {
            int pointIndex = polygonIndices[i];
            if (pointIndex == aIndex || pointIndex == bIndex || pointIndex == cIndex)
            {
                continue;
            }

            if (IsPointStrictlyInsideTriangleXZ(vertices[pointIndex], vertices[aIndex], vertices[bIndex], vertices[cIndex]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointStrictlyInsideTriangleXZ(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        float area = CrossXZ(a, b, c);
        float area1 = CrossXZ(point, a, b);
        float area2 = CrossXZ(point, b, c);
        float area3 = CrossXZ(point, c, a);

        if (Mathf.Abs(area) <= PolygonEpsilon)
        {
            return false;
        }

        if (Mathf.Abs(area1) <= PolygonEpsilon || Mathf.Abs(area2) <= PolygonEpsilon || Mathf.Abs(area3) <= PolygonEpsilon)
        {
            return false;
        }

        bool hasNegative = area1 < -PolygonEpsilon || area2 < -PolygonEpsilon || area3 < -PolygonEpsilon;
        bool hasPositive = area1 > PolygonEpsilon || area2 > PolygonEpsilon || area3 > PolygonEpsilon;
        return !(hasNegative && hasPositive);
    }

    private static float CrossXZ(Vector3 origin, Vector3 left, Vector3 right)
    {
        Vector2 a = new Vector2(left.x - origin.x, left.z - origin.z);
        Vector2 b = new Vector2(right.x - origin.x, right.z - origin.z);
        return a.x * b.y - a.y * b.x;
    }

    private static bool SegmentsIntersectXZ(Vector3 aStart, Vector3 aEnd, Vector3 bStart, Vector3 bEnd)
    {
        float o1 = CrossXZ(aStart, aEnd, bStart);
        float o2 = CrossXZ(aStart, aEnd, bEnd);
        float o3 = CrossXZ(bStart, bEnd, aStart);
        float o4 = CrossXZ(bStart, bEnd, aEnd);

        if (o1 * o2 < -PolygonEpsilon && o3 * o4 < -PolygonEpsilon)
        {
            return true;
        }

        return Mathf.Abs(o1) <= PolygonEpsilon && IsPointOnSegmentXZ(aStart, bStart, aEnd) ||
               Mathf.Abs(o2) <= PolygonEpsilon && IsPointOnSegmentXZ(aStart, bEnd, aEnd) ||
               Mathf.Abs(o3) <= PolygonEpsilon && IsPointOnSegmentXZ(bStart, aStart, bEnd) ||
               Mathf.Abs(o4) <= PolygonEpsilon && IsPointOnSegmentXZ(bStart, aEnd, bEnd);
    }

    private static bool IsPointOnSegmentXZ(Vector3 segmentStart, Vector3 point, Vector3 segmentEnd)
    {
        return point.x >= Mathf.Min(segmentStart.x, segmentEnd.x) - PolygonEpsilon &&
               point.x <= Mathf.Max(segmentStart.x, segmentEnd.x) + PolygonEpsilon &&
               point.z >= Mathf.Min(segmentStart.z, segmentEnd.z) - PolygonEpsilon &&
               point.z <= Mathf.Max(segmentStart.z, segmentEnd.z) + PolygonEpsilon;
    }

    private RoomGeometry CalculateGeometry(List<Vector3> vertices)
    {
        float areaSum = 0f;
        float centroidX = 0f;
        float centroidZ = 0f;

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 p1 = vertices[i];
            Vector3 p2 = vertices[(i + 1) % vertices.Count];

            float cross = p1.x * p2.z - p2.x * p1.z;
            areaSum += cross;
            centroidX += (p1.x + p2.x) * cross;
            centroidZ += (p1.z + p2.z) * cross;
        }

        float signedArea = areaSum * 0.5f;
        float area = Mathf.Abs(signedArea);

        Vector3 center;
        if (Mathf.Abs(areaSum) > 0.000001f)
        {
            float inv = 1f / (3f * areaSum);
            center = new Vector3(centroidX * inv, vertices[0].y, centroidZ * inv);
        }
        else
        {
            Vector3 avg = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                avg += vertices[i];
            }

            center = avg / vertices.Count;
        }

        return new RoomGeometry
        {
            Center = center,
            Area = area,
            WallCount = vertices.Count,
        };
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
