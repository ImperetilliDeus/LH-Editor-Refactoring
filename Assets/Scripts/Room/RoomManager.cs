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
                FindObjectsByType<VirtualBoundary>(FindObjectsInactive.Include, FindObjectsSortMode.None),
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
        room.Initialize(wallSet, CalculateRoomGeometry(vertices));
        room.SetMaterial(roomMaterial ?? GetFallbackRoomMaterial(), roomColor);

        allRooms.Add(room);
        roomsByWalls[wallSet] = room;
        RoomsChanged?.Invoke();
        return room;
    }

    public Room CreateRoomFromPolygon(List<Vector3> polygonVertices, HashSet<Wall> wallSet = null)
    {
        List<Vector3> sanitizedPolygonVertices = Room.CreateSanitizedPolygonCopy(polygonVertices);
        if (sanitizedPolygonVertices.Count < 3)
        {
            Debug.LogWarning("Cannot create room: polygon requires at least three vertices");
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

            if (ArePolygonsEquivalent(existingVertices, sanitizedPolygonVertices))
            {
                return existingRoom;
            }
        }

        GameObject roomObject = new GameObject($"Room_{allRooms.Count}");
        roomObject.transform.SetParent(roomRoot, false);
        LayerUtility.ApplyLayer(roomObject, LayerUtility.FloorLayerName, false);

        Room room = roomObject.AddComponent<Room>();
        room.SetPlacementOffset(roomSpawnLocalOffset);
        room.Initialize(wallSet ?? new HashSet<Wall>(), CalculateRoomGeometry(sanitizedPolygonVertices), sanitizedPolygonVertices);
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

        if (!room.SetManualBoundaryVertices(polygonVertices, clearWallSet))
        {
            return false;
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
                FindObjectsByType<VirtualBoundary>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                out List<Vector3> vertices) || vertices.Count == 0)
        {
            return new RoomGeometry();
        }

        return CalculateRoomGeometry(vertices);
    }

    private static RoomGeometry CalculateRoomGeometry(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count == 0)
        {
            return new RoomGeometry();
        }

        float signedAreaTwice = 0f;
        float centroidX = 0f;
        float centroidZ = 0f;

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 p1 = vertices[i];
            Vector3 p2 = vertices[(i + 1) % vertices.Count];
            float cross = p1.x * p2.z - p2.x * p1.z;
            signedAreaTwice += cross;
            centroidX += (p1.x + p2.x) * cross;
            centroidZ += (p1.z + p2.z) * cross;
        }

        float area = Mathf.Abs(signedAreaTwice) * 0.5f;
        Vector3 center;
        if (Mathf.Abs(signedAreaTwice) <= 0.000001f)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                sum += vertices[i];
            }

            center = sum / vertices.Count;
        }
        else
        {
            float factor = 1f / (3f * signedAreaTwice);
            center = new Vector3(
                centroidX * factor,
                vertices[0].y,
                centroidZ * factor);
        }

        return new RoomGeometry
        {
            Center = center,
            Area = area,
            WallCount = vertices.Count,
        };
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

    private static bool ArePolygonsEquivalent(List<Vector3> left, List<Vector3> right, float epsilon = 0.01f)
    {
        if (left == null || right == null || left.Count != right.Count)
        {
            return false;
        }

        if (left.Count == 0)
        {
            return true;
        }

        float epsilonSqr = epsilon * epsilon;
        for (int offset = 0; offset < right.Count; offset++)
        {
            bool matchesForward = true;
            bool matchesReverse = true;
            for (int i = 0; i < left.Count; i++)
            {
                if ((left[i] - right[(offset + i) % right.Count]).sqrMagnitude > epsilonSqr)
                {
                    matchesForward = false;
                }

                int reverseIndex = offset - i;
                if (reverseIndex < 0)
                {
                    reverseIndex += right.Count;
                }

                if ((left[i] - right[reverseIndex]).sqrMagnitude > epsilonSqr)
                {
                    matchesReverse = false;
                }

                if (!matchesForward && !matchesReverse)
                {
                    break;
                }
            }

            if (matchesForward || matchesReverse)
            {
                return true;
            }
        }

        return false;
    }
}
