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

            int hash = 17;
            foreach (Wall wall in wallSet)
            {
                hash = hash * 31 + (wall != null ? wall.GetHashCode() : 0);
            }

            return hash;
        }
    }

    public static RoomManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform wallRoot;

    [Header("Room")]
    [SerializeField] private Transform roomRoot;
    [SerializeField] private Material roomMaterial;
    [SerializeField] private Color roomColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
    [SerializeField] private float wallConnectionThreshold = 0.1f;
    [SerializeField] private Vector3 roomSpawnLocalOffset = new Vector3(0f, 0.01f, 0f);

    private readonly Dictionary<HashSet<Wall>, Room> roomsByWalls = new Dictionary<HashSet<Wall>, Room>(new WallSetComparer());
    private readonly List<Room> allRooms = new List<Room>();
    private Material fallbackRoomMaterial;

    public event System.Action RoomsChanged;

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

        GameObject roomObject = new GameObject($"Room_{allRooms.Count}");
        roomObject.transform.SetParent(roomRoot, false);
        LayerUtility.ApplyLayer(roomObject, LayerUtility.FloorLayerName, false);

        Room room = roomObject.AddComponent<Room>();
        room.SetPlacementOffset(roomSpawnLocalOffset);
        room.Initialize(wallSet, PolygonUtility.CalculateGeometry(vertices));
        room.SetMaterial(roomMaterial ?? GetFallbackRoomMaterial(), roomColor);

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

        GameObject roomObject = new GameObject($"Room_{allRooms.Count}");
        roomObject.transform.SetParent(roomRoot, false);
        LayerUtility.ApplyLayer(roomObject, LayerUtility.FloorLayerName, false);

        Room room = roomObject.AddComponent<Room>();
        room.SetPlacementOffset(roomSpawnLocalOffset);
        room.Initialize(wallSet ?? new HashSet<Wall>(), PolygonUtility.CalculateGeometry(sanitizedPolygonVertices), sanitizedPolygonVertices);
        room.SetMaterial(roomMaterial ?? GetFallbackRoomMaterial(), roomColor);

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

    public bool UpdateRoomMetadata(Room room, string roomName, string roomTypeKey)
    {
        if (room == null)
        {
            return false;
        }

        bool changed = false;
        string normalizedName = roomName ?? string.Empty;
        string normalizedTypeKey = roomTypeKey ?? string.Empty;

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

        if (changed)
        {
            RoomsChanged?.Invoke();
        }

        return changed;
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

    public void RefreshAllRooms()
    {
        bool refreshedAnyRoom = false;
        for (int i = 0; i < allRooms.Count; i++)
        {
            Room room = allRooms[i];
            if (room == null)
            {
                continue;
            }

            room.RefreshVisual();
            refreshedAnyRoom = true;
        }

        if (refreshedAnyRoom)
        {
            RoomsChanged?.Invoke();
        }
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

        Transform wallRootTransform = LayerUtility.FindTransformByName("Walls", true);
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

    private void ValidateConfiguration()
    {
        Debug.Assert(wallRoot != null, $"{nameof(RoomManager)} requires {nameof(wallRoot)} or a scene Walls root.", this);
    }
}
