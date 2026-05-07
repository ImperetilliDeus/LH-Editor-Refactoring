using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RoomVisualizer : MonoBehaviour
{
    private const string FloorObjectName = "Floor";
    private const string CeilingObjectName = "Ceiling";
    private const float CeilingColliderThickness = 0.02f;
    private const float FloorWorldY = 0.1f;

    private Mesh roomMesh;
    private GameObject floorObject;
    private MeshRenderer floorMeshRenderer;
    private MeshFilter floorMeshFilter;
    private GameObject ceilingObject;
    private MeshRenderer ceilingMeshRenderer;
    private MeshFilter ceilingMeshFilter;
    private BoxCollider ceilingCollider;
    private Material roomMaterial;
    private Material runtimeFloorMaterial;
    private Material runtimeCeilingMaterial;
    private Material floorSourceMaterial;
    private Material ceilingSourceMaterial;
    private Color roomColor;
    private Color selectionColor = new Color(0.28f, 0.6f, 1f, 0.42f);
    private bool isSelected;
    private Room observedRoom;
    private RoomData observedData;

    private readonly List<Vector3> cachedLocalVertices = new List<Vector3>();
    private readonly List<int> cachedTriangles = new List<int>();
    private readonly List<Vector3> cachedTriangulationVertices = new List<Vector3>();
    private readonly List<int> cachedPolygonIndices = new List<int>();
    private static Material defaultRoomMaterial;

    private void OnEnable()
    {
        AttachToRoom();
        RefreshVisual();
    }

    private void OnDisable()
    {
        DetachFromData();
    }

    private void OnDestroy()
    {
        if (roomMesh != null)
        {
            Destroy(roomMesh);
            roomMesh = null;
        }

        if (runtimeFloorMaterial != null)
        {
            Destroy(runtimeFloorMaterial);
            runtimeFloorMaterial = null;
        }

        if (runtimeCeilingMaterial != null)
        {
            Destroy(runtimeCeilingMaterial);
            runtimeCeilingMaterial = null;
        }
    }

    public void SetMaterial(Material material, Color color)
    {
        roomMaterial = material;
        roomColor = color;
        RefreshVisual();
    }

    public void SetSelectionState(bool selected, Color highlightColor)
    {
        isSelected = selected;
        selectionColor = highlightColor;
        ApplyRuntimeColors();
    }

    public void RefreshVisual()
    {
        AttachToRoom();
        if (observedData == null || observedData.BoundaryVertices.Count < 3)
        {
            return;
        }

        List<Vector3> sanitizedWorldVertices = PolygonUtility.CreateSanitizedPolygonCopy(observedData.BoundaryVertices);
        if (sanitizedWorldVertices.Count < 3)
        {
            return;
        }

        RoomGeometry geometry = observedData.Geometry;
        if (geometry.WallCount == 0)
        {
            geometry = PolygonUtility.CalculateGeometry(sanitizedWorldVertices);
        }

        ApplyTransformFromGeometry(geometry, observedData.PlacementOffset);
        BuildLocalVertices(sanitizedWorldVertices, geometry.Center, cachedLocalVertices);

        if (roomMaterial == null)
        {
            roomMaterial = GetDefaultRoomMaterial();
        }

        EnsureFloorVisual();

        roomMesh = GenerateRoomMesh(cachedLocalVertices);
        floorMeshFilter.sharedMesh = roomMesh;

        Material resolvedFloorSourceMaterial = ResolveFloorSourceMaterial();
        EnsureRuntimeMaterial(ref runtimeFloorMaterial, ref floorSourceMaterial, resolvedFloorSourceMaterial);
        ApplyRuntimeMaterialAppearance(runtimeFloorMaterial, floorSourceMaterial, resolvedFloorSourceMaterial == roomMaterial);
        floorMeshRenderer.sharedMaterial = runtimeFloorMaterial;
        UpdateCeilingVisual();
    }

    private void AttachToRoom()
    {
        Room room = GetComponent<Room>();
        if (observedRoom == room && observedData == room?.Data)
        {
            return;
        }

        DetachFromData();
        observedRoom = room;
        observedData = room != null ? room.Data : null;
        if (observedData != null)
        {
            observedData.Changed += HandleRoomDataChanged;
        }
    }

    private void DetachFromData()
    {
        if (observedData != null)
        {
            observedData.Changed -= HandleRoomDataChanged;
        }

        observedData = null;
        observedRoom = null;
    }

    private void HandleRoomDataChanged()
    {
        RefreshVisual();
    }

    private void ApplyRuntimeColors()
    {
        ApplyRuntimeMaterialAppearance(runtimeFloorMaterial, floorSourceMaterial, floorSourceMaterial == roomMaterial);
        ApplyRuntimeMaterialAppearance(runtimeCeilingMaterial, ceilingSourceMaterial, ceilingSourceMaterial == roomMaterial);
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

        EnsureTrianglesFaceUp(cachedTriangulationVertices, cachedTriangles);

        roomMesh.SetVertices(cachedTriangulationVertices);
        roomMesh.SetTriangles(cachedTriangles, 0);
        roomMesh.RecalculateNormals();
        roomMesh.RecalculateBounds();
        return roomMesh;
    }

    private void ApplyTransformFromGeometry(RoomGeometry geometry, Vector3 placementOffset)
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

        floorObject.transform.localPosition = new Vector3(0f, FloorWorldY - transform.position.y, 0f);
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

        Material resolvedCeilingSourceMaterial = ResolveCeilingSourceMaterial();
        EnsureRuntimeMaterial(ref runtimeCeilingMaterial, ref ceilingSourceMaterial, resolvedCeilingSourceMaterial);
        ApplyRuntimeMaterialAppearance(runtimeCeilingMaterial, ceilingSourceMaterial, resolvedCeilingSourceMaterial == roomMaterial);

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
        }

        LayerUtility.ApplyLayer(ceilingObject, LayerUtility.CeilLayerName, false);

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
        float bestY = observedData != null ? observedData.Geometry.Center.y : transform.position.y;
        if (observedRoom == null || observedRoom.WallSet == null)
        {
            return bestY;
        }

        foreach (Wall wall in observedRoom.WallSet)
        {
            if (wall == null)
            {
                continue;
            }

            float wallTopY = wall.Data.centerY + wall.Data.height * 0.5f;
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

    private Material ResolveFloorSourceMaterial()
    {
        if (roomMaterial == null)
        {
            roomMaterial = GetDefaultRoomMaterial();
        }

        Material explicitFloorMaterial = RoomManager.Instance != null && observedData != null
            ? RoomManager.Instance.ResolveFloorMaterial(observedData.FloorTextureCode)
            : null;

        return explicitFloorMaterial != null ? explicitFloorMaterial : roomMaterial;
    }

    private Material ResolveCeilingSourceMaterial()
    {
        if (roomMaterial == null)
        {
            roomMaterial = GetDefaultRoomMaterial();
        }

        Material explicitCeilingMaterial = RoomManager.Instance != null && observedData != null
            ? RoomManager.Instance.ResolveCeilingMaterial(observedData.CeilingTextureCode)
            : null;

        return explicitCeilingMaterial != null ? explicitCeilingMaterial : roomMaterial;
    }

    private void EnsureRuntimeMaterial(ref Material runtimeMaterial, ref Material cachedSourceMaterial, Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            return;
        }

        if (runtimeMaterial != null && cachedSourceMaterial == sourceMaterial)
        {
            return;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }

        runtimeMaterial = new Material(sourceMaterial);
        cachedSourceMaterial = sourceMaterial;
    }

    private void ApplyRuntimeMaterialAppearance(Material runtimeMaterial, Material sourceMaterial, bool useRoomTint)
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (useRoomTint)
        {
            if (!runtimeMaterial.HasProperty("_Color"))
            {
                return;
            }

            runtimeMaterial.color = isSelected ? selectionColor : roomColor;
            return;
        }
    }
}
