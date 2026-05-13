using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    private sealed class WallSetComparer : IEqualityComparer<HashSet<Wall>>
    {
        public bool Equals(HashSet<Wall> left, HashSet<Wall> right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            return left.SetEquals(right);
        }

        public int GetHashCode(HashSet<Wall> wallSet)
        {
            if (wallSet == null)
            {
                return 0;
            }

            int xorHash = 0;
            int sumHash = 0;
            int count = 0;
            foreach (Wall wall in wallSet)
            {
                int wallHash = wall != null ? wall.GetHashCode() : 0;
                xorHash ^= wallHash;
                sumHash += wallHash;
                count++;
            }

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + count;
                hash = hash * 31 + xorHash;
                hash = hash * 31 + sumHash;
                return hash;
            }
        }
    }

    public static RoomManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform wallRoot;

    [Header("Room")]
    [SerializeField] private Transform roomRoot;
    [SerializeField] private Material roomMaterial;
    [SerializeField] private string floorMaterialFolderPath = "Assets/Materials";
    [SerializeField] private string ceilingMaterialFolderPath = "Assets/Materials";
    [SerializeField] private string defaultFloorTextureCode = string.Empty;
    [SerializeField] private string defaultCeilingTextureCode = string.Empty;
    [SerializeField, HideInInspector] private List<Material> floorMaterials = new List<Material>();
    [SerializeField, HideInInspector] private List<Material> ceilingMaterials = new List<Material>();
    [SerializeField] private Color roomColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
    [SerializeField] private float wallConnectionThreshold = 0.1f;
    [SerializeField] private Vector3 roomSpawnLocalOffset = new Vector3(0f, 0.01f, 0f);
    [SerializeField] private bool allowAutomaticRoomGeneration = false;

    private readonly Dictionary<HashSet<Wall>, Room> roomsByWalls = new Dictionary<HashSet<Wall>, Room>(new WallSetComparer());
    private readonly List<Room> allRooms = new List<Room>();
    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<Room> cachedManualRooms = new List<Room>();
    private readonly List<Room> cachedAutomaticRooms = new List<Room>();
    private readonly List<Room> cachedAvailableRooms = new List<Room>();
    private readonly List<Room> nextRooms = new List<Room>();
    private Material fallbackRoomMaterial;
    private RoomPlanarGraph cachedGraph;
    private bool isGraphDirty = true;

    public string FloorMaterialFolderPath => floorMaterialFolderPath;
    public string CeilingMaterialFolderPath => ceilingMaterialFolderPath;
    public string DefaultFloorTextureCode => defaultFloorTextureCode ?? string.Empty;
    public string DefaultCeilingTextureCode => defaultCeilingTextureCode ?? string.Empty;

    public event System.Action RoomsChanged;

    public IReadOnlyList<Material> GetFloorMaterials()
    {
        return floorMaterials;
    }

    public IReadOnlyList<Material> GetCeilingMaterials()
    {
        return ceilingMaterials;
    }

    public Material ResolveFloorMaterial(string materialCode)
    {
        return ResolveMaterial(floorMaterials, materialCode);
    }

    public Material ResolveCeilingMaterial(string materialCode)
    {
        return ResolveMaterial(ceilingMaterials, materialCode);
    }

    public string GetEffectiveFloorTextureCode(Room room)
    {
        return GetEffectiveTextureCode(room != null ? room.FloorTextureCode : null, defaultFloorTextureCode);
    }

    public string GetEffectiveCeilingTextureCode(Room room)
    {
        return GetEffectiveTextureCode(room != null ? room.CeilingTextureCode : null, defaultCeilingTextureCode);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureWallRoot();
        ValidateConfiguration();
    }

    private void OnEnable()
    {
        RoomTopologyEvents.RefreshAllRequested += HandleRefreshAllRequested;
        RoomTopologyEvents.RefreshForWallReplacementRequested += HandleRefreshForWallReplacementRequested;
        WallRegistry.RegistryChanged += MarkGraphDirty;
        isGraphDirty = true;
    }

    private void OnDisable()
    {
        RoomTopologyEvents.RefreshAllRequested -= HandleRefreshAllRequested;
        RoomTopologyEvents.RefreshForWallReplacementRequested -= HandleRefreshForWallReplacementRequested;
        WallRegistry.RegistryChanged -= MarkGraphDirty;
    }

    private void Start()
    {
        EnsureRoomRoot();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (fallbackRoomMaterial != null)
        {
            Destroy(fallbackRoomMaterial);
        }
    }

    private static Material ResolveMaterial(IReadOnlyList<Material> materials, string materialCode)
    {
        if (materials == null || string.IsNullOrWhiteSpace(materialCode))
        {
            return null;
        }

        for (int i = 0; i < materials.Count; i++)
        {
            Material material = materials[i];
            if (material != null && string.Equals(material.name, materialCode, System.StringComparison.Ordinal))
            {
                return material;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RefreshMaterialCache();
    }

    [ContextMenu("Refresh Material Cache")]
    private void RefreshMaterialCache()
    {
        RefreshMaterialCacheForFolder(floorMaterialFolderPath, floorMaterials);
        RefreshMaterialCacheForFolder(ceilingMaterialFolderPath, ceilingMaterials);
    }

    private static void RefreshMaterialCacheForFolder(string folderPath, List<Material> target)
    {
        if (target == null)
        {
            return;
        }

        target.Clear();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        string normalizedFolderPath = folderPath.Replace('\\', '/').Trim();
        if (!normalizedFolderPath.StartsWith("Assets", System.StringComparison.Ordinal))
        {
            Debug.LogWarning($"Material folder path must start with 'Assets': {normalizedFolderPath}");
            return;
        }

        string[] materialGuids = UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { normalizedFolderPath });
        List<Material> loadedMaterials = new List<Material>(materialGuids.Length);
        for (int i = 0; i < materialGuids.Length; i++)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(materialGuids[i]);
            Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
            {
                loadedMaterials.Add(material);
            }
        }

        loadedMaterials.Sort((left, right) => string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty));
        target.AddRange(loadedMaterials);
    }
#endif

    public Room CreateRoom(HashSet<Wall> wallSet)
    {
        if (wallSet == null || wallSet.Count == 0)
        {
            Debug.LogWarning("Cannot create room: need at least one wall");
            return null;
        }

        EnsureRoomRoot();

        if (!RoomGraphUtility.TryBuildOrderedVertices(
                wallSet,
                wallConnectionThreshold,
                VirtualBoundary.All,
                out List<Vector3> vertices) || vertices.Count < 3)
        {
            Debug.LogWarning("Cannot create room: failed to resolve a valid outer boundary from the selected walls");
            return null;
        }

        for (int i = 0; i < allRooms.Count; i++)
        {
            Room existingRoom = allRooms[i];
            if (existingRoom != null && existingRoom.HasSameWallSet(wallSet))
            {
                return existingRoom;
            }
        }

        Room room = CreateRoomObject(wallSet, vertices, false);

        allRooms.Add(room);
        roomsByWalls[wallSet] = room;
        RoomsChanged?.Invoke();
        return room;
    }

    public Room CreateRoomFromPolygon(List<Vector3> polygonVertices, HashSet<Wall> wallSet = null)
    {
        List<Vector3> sanitizedPolygonVertices = PolygonUtility.CreateSanitizedPolygonCopy(polygonVertices);
        if (!RoomPolygonValidationUtility.IsValidPolygon(sanitizedPolygonVertices))
        {
            Debug.LogWarning("Cannot create room: polygon is invalid");
            return null;
        }

        EnsureRoomRoot();

        for (int i = 0; i < allRooms.Count; i++)
        {
            Room existingRoom = allRooms[i];
            if (existingRoom == null)
            {
                continue;
            }

            List<Vector3> existingVertices = new List<Vector3>();
            if (!existingRoom.TryGetOrderedVertices(existingVertices))
            {
                continue;
            }

            if (PolygonUtility.ArePolygonsEquivalent(existingVertices, sanitizedPolygonVertices))
            {
                return existingRoom;
            }
        }

        Room room = CreateRoomObject(wallSet ?? new HashSet<Wall>(), sanitizedPolygonVertices, true);

        allRooms.Add(room);
        RoomsChanged?.Invoke();
        return room;
    }

    public bool UpdateRoomPolygon(Room room, IReadOnlyList<Vector3> polygonVertices, bool clearWallSet = false)
    {
        if (room == null)
        {
            return false;
        }

        if (!RoomPolygonValidationUtility.IsValidPolygon(polygonVertices))
        {
            return false;
        }

        if (!room.SetManualBoundaryVertices(polygonVertices, clearWallSet))
        {
            return false;
        }

        RoomsChanged?.Invoke();
        return true;
    }

    public bool UpdateRoomMetadata(
        Room room,
        string roomName,
        string roomTypeKey,
        string roomCode = null,
        string roomNativeCode = null,
        string floorTextureCode = null,
        string ceilingTextureCode = null)
    {
        if (room == null)
        {
            return false;
        }

        bool changed = false;
        string normalizedName = roomName ?? string.Empty;
        string normalizedTypeKey = roomTypeKey ?? string.Empty;
        string normalizedRoomCode = roomCode ?? room.RoomCode ?? string.Empty;
        string normalizedRoomNativeCode = roomNativeCode ?? room.RoomNativeCode ?? string.Empty;
        string normalizedFloorTextureCode = NormalizeFloorTextureCode(floorTextureCode ?? room.FloorTextureCode);
        string normalizedCeilingTextureCode = NormalizeCeilingTextureCode(ceilingTextureCode ?? room.CeilingTextureCode);

        if (!string.Equals(room.RoomName, normalizedName, System.StringComparison.Ordinal))
        {
            room.SetRoomName(normalizedName);
            changed = true;
        }

        if (!string.Equals(room.RoomTypeKey, normalizedTypeKey, System.StringComparison.Ordinal))
        {
            room.SetRoomTypeKey(normalizedTypeKey);
            changed = true;
        }

        if (!string.Equals(room.RoomCode, normalizedRoomCode, System.StringComparison.Ordinal))
        {
            room.SetRoomCode(normalizedRoomCode);
            changed = true;
        }

        if (!string.Equals(room.RoomNativeCode, normalizedRoomNativeCode, System.StringComparison.Ordinal))
        {
            room.SetRoomNativeCode(normalizedRoomNativeCode);
            changed = true;
        }

        if (!string.Equals(room.FloorTextureCode, normalizedFloorTextureCode, System.StringComparison.Ordinal))
        {
            room.SetFloorTextureCode(normalizedFloorTextureCode);
            changed = true;
        }

        if (!string.Equals(room.CeilingTextureCode, normalizedCeilingTextureCode, System.StringComparison.Ordinal))
        {
            room.SetCeilingTextureCode(normalizedCeilingTextureCode);
            changed = true;
        }

        if (changed)
        {
            RoomsChanged?.Invoke();
        }

        return changed;
    }

    public bool UpdateRoomWallSelection(Room room, IEnumerable<string> wallIds, bool enableManualSelection)
    {
        if (room == null)
        {
            return false;
        }

        if (enableManualSelection)
        {
            room.SetManualWallIds(wallIds);
        }
        else
        {
            room.ClearManualWallSelection();
        }

        RoomsChanged?.Invoke();
        return true;
    }

    public void DeleteRoom(Room room)
    {
        if (room == null)
        {
            return;
        }

        HashSet<Wall> wallSet = room.WallSet;
        allRooms.Remove(room);
        if (wallSet != null && roomsByWalls.TryGetValue(wallSet, out Room mappedRoom) && mappedRoom == room)
        {
            roomsByWalls.Remove(wallSet);
        }

        Destroy(room.gameObject);
        RoomsChanged?.Invoke();
    }

    public List<Room> GetAllRooms()
    {
        return new List<Room>(allRooms);
    }

    public void GetAllRooms(List<Room> results)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();
        results.AddRange(allRooms);
    }

    public void RefreshAllRooms()
    {
        EnsureRoomRoot();
        if (!allowAutomaticRoomGeneration)
        {
            RemoveAutomaticRooms();
            return;
        }

        RebuildGraphIfNeeded();
        if (cachedGraph == null)
        {
            return;
        }

        cachedManualRooms.Clear();
        cachedAutomaticRooms.Clear();
        for (int i = 0; i < allRooms.Count; i++)
        {
            Room room = allRooms[i];
            if (room == null)
            {
                continue;
            }

            if (room.IsManualRoom)
            {
                cachedManualRooms.Add(room);
            }
            else
            {
                cachedAutomaticRooms.Add(room);
            }
        }

        nextRooms.Clear();
        nextRooms.AddRange(cachedManualRooms);

        cachedAvailableRooms.Clear();
        cachedAvailableRooms.AddRange(cachedAutomaticRooms);

        for (int i = 0; i < cachedGraph.Faces.Count; i++)
        {
            RoomPlanarGraph.Face face = cachedGraph.Faces[i];
            if (face == null || face.Vertices.Count < 3)
            {
                continue;
            }

            Room createdRoom = CreateRoomFromFace(face);
            if (createdRoom != null)
            {
                nextRooms.Add(createdRoom);
            }
        }

        for (int i = 0; i < cachedAvailableRooms.Count; i++)
        {
            Room obsoleteRoom = cachedAvailableRooms[i];
            if (obsoleteRoom == null)
            {
                continue;
            }

            Destroy(obsoleteRoom.gameObject);
        }

        allRooms.Clear();
        allRooms.AddRange(nextRooms);
        RebuildRoomLookup();
        RoomsChanged?.Invoke();
    }

    public void RefreshRoomsForWallReplacement(ICollection<Wall> removedWalls, IEnumerable<Wall> addedWalls)
    {
        if (removedWalls == null || removedWalls.Count == 0)
        {
            return;
        }

        bool changed = false;
        for (int i = 0; i < allRooms.Count; i++)
        {
            Room room = allRooms[i];
            if (room == null)
            {
                continue;
            }

            if (room.ReplaceWallReferences(removedWalls, addedWalls))
            {
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        RebuildRoomLookup();
        RoomsChanged?.Invoke();
    }

    public Room FindRoomByWallSet(HashSet<Wall> wallSet)
    {
        roomsByWalls.TryGetValue(wallSet, out Room room);
        return room;
    }

    private void RebuildRoomLookup()
    {
        roomsByWalls.Clear();
        for (int i = 0; i < allRooms.Count; i++)
        {
            Room room = allRooms[i];
            if (room == null || room.WallSet == null)
            {
                continue;
            }

            roomsByWalls[room.WallSet] = room;
        }
    }

    private Room CreateRoomFromFace(RoomPlanarGraph.Face face)
    {
        if (face == null || face.Vertices.Count < 3)
        {
            return null;
        }

        Room room = CreateRoomObject(face.Walls != null ? new HashSet<Wall>(face.Walls) : new HashSet<Wall>(), face.Vertices, false);
        room.UpdateGeometry(face.Vertices, face.Walls, face.VirtualBoundaries);
        return room;
    }

    public void MarkGraphDirty()
    {
        isGraphDirty = true;
    }

    private void RebuildGraphIfNeeded()
    {
        if (!isGraphDirty && cachedGraph != null)
        {
            return;
        }

        WallRegistry.CollectWalls(cachedWalls, wallRoot);
        cachedGraph = RoomGraphUtility.BuildPlanarGraph(new HashSet<Wall>(cachedWalls), VirtualBoundary.All);
        isGraphDirty = false;
    }

    private Room CreateRoomObject(HashSet<Wall> wallSet, IReadOnlyList<Vector3> polygonVertices, bool keepManualVertices)
    {
        EnsureRoomRoot();

        GameObject roomObject = new GameObject($"Room_{allRooms.Count}");
        roomObject.transform.SetParent(roomRoot, false);
        LayerUtility.ApplyLayer(roomObject, LayerUtility.FloorLayerName, false);

        Room room = roomObject.AddComponent<Room>();
        room.SetPlacementOffset(roomSpawnLocalOffset);
        room.Initialize(
            wallSet ?? new HashSet<Wall>(),
            PolygonUtility.CalculateGeometry(polygonVertices),
            Room.CreateSanitizedPolygonCopy(polygonVertices),
            keepManualVertices);
        room.SetFloorTextureCode(NormalizeFloorTextureCode(room.FloorTextureCode));
        room.SetCeilingTextureCode(NormalizeCeilingTextureCode(room.CeilingTextureCode));

        room.SetMaterial(roomMaterial ?? GetFallbackRoomMaterial(), roomColor);
        return room;
    }

    private string NormalizeFloorTextureCode(string materialCode)
    {
        return GetEffectiveTextureCode(materialCode, defaultFloorTextureCode);
    }

    private string NormalizeCeilingTextureCode(string materialCode)
    {
        return GetEffectiveTextureCode(materialCode, defaultCeilingTextureCode);
    }

    private static string GetEffectiveTextureCode(string selectedCode, string defaultCode)
    {
        if (!string.IsNullOrWhiteSpace(selectedCode))
        {
            return selectedCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(defaultCode))
        {
            return defaultCode.Trim();
        }

        return string.Empty;
    }

    private RoomGeometry CalculateRoomGeometry(HashSet<Wall> wallSet)
    {
        if (!RoomGraphUtility.TryBuildOrderedVertices(
                wallSet,
                wallConnectionThreshold,
                VirtualBoundary.All,
                out List<Vector3> vertices) || vertices.Count == 0)
        {
            return new RoomGeometry();
        }

        return PolygonUtility.CalculateGeometry(vertices);
    }

    private void EnsureWallRoot()
    {
        if (wallRoot != null)
        {
            return;
        }

        Transform wallRootTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        if (wallRootTransform != null)
        {
            wallRoot = wallRootTransform;
        }
    }

    private void EnsureRoomRoot()
    {
        if (roomRoot != null)
        {
            return;
        }

        GameObject rootObject = new GameObject("Rooms");
        roomRoot = rootObject.transform;
        roomRoot.SetParent(transform, false);
    }

    private Material GetFallbackRoomMaterial()
    {
        if (fallbackRoomMaterial != null)
        {
            return fallbackRoomMaterial;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            return null;
        }

        fallbackRoomMaterial = new Material(shader);
        return fallbackRoomMaterial;
    }

    private void RemoveAutomaticRooms()
    {
        bool changed = false;
        for (int i = allRooms.Count - 1; i >= 0; i--)
        {
            Room room = allRooms[i];
            if (room == null)
            {
                allRooms.RemoveAt(i);
                changed = true;
                continue;
            }

            if (room.IsManualRoom)
            {
                continue;
            }

            Destroy(room.gameObject);
            allRooms.RemoveAt(i);
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        RebuildRoomLookup();
        RoomsChanged?.Invoke();
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(wallRoot != null, $"{nameof(RoomManager)} requires {nameof(wallRoot)} or a scene Walls root.", this);
    }

    private void HandleRefreshAllRequested()
    {
        RefreshAllRooms();
    }

    private void HandleRefreshForWallReplacementRequested(ICollection<Wall> removedWalls, IEnumerable<Wall> addedWalls)
    {
        MarkGraphDirty();
        RefreshRoomsForWallReplacement(removedWalls, addedWalls);
    }
}
