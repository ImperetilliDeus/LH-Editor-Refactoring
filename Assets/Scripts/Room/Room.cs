using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    private static readonly IReadOnlyList<Vector3> EmptyVertices = new List<Vector3>();

    public HashSet<Wall> WallSet { get; private set; }
    public RoomData Data => EnsureData();
    public IReadOnlyList<Vector3> BoundaryVertices => Data.BoundaryVertices;
    public IReadOnlyList<Vector3> ManualBoundaryVertices => Data.IsManualRoom ? Data.BoundaryVertices : EmptyVertices;
    public RoomGeometry Geometry => Data.Geometry;
    public Vector3 Centroid => Data.Geometry.Center;
    public bool IsManualRoom => Data.IsManualRoom;
    public bool ManualWallSelectionEnabled => Data.ManualWallSelectionEnabled;
    public string RoomName => Data.RoomName;
    public string RoomTypeKey => Data.RoomTypeKey;
    public string RoomCode => Data.RoomCode;
    public string RoomNativeCode => Data.RoomNativeCode;
    public string FloorTextureCode => Data.FloorTextureCode;
    public string CeilingTextureCode => Data.CeilingTextureCode;
    public IReadOnlyList<string> AutomaticWallIds => Data.WallIds;
    public IReadOnlyList<string> ManualWallIds => Data.ManualWallIds;
    public IReadOnlyList<string> EffectiveWallIds => Data.EffectiveWallIds;

    [SerializeField] private RoomData data = new RoomData();

    public void Initialize(HashSet<Wall> wallSet, RoomGeometry geometry)
    {
        Initialize(wallSet, geometry, new List<Vector3>(), false);
    }

    public void Initialize(HashSet<Wall> wallSet, RoomGeometry geometry, List<Vector3> polygonVertices)
    {
        Initialize(wallSet, geometry, polygonVertices, true);
    }

    public void Initialize(HashSet<Wall> wallSet, RoomGeometry geometry, IReadOnlyList<Vector3> polygonVertices, bool isManualRoom)
    {
        WallSet = wallSet != null ? new HashSet<Wall>(wallSet) : new HashSet<Wall>();
        EnsureData().ApplyLayout(
            polygonVertices,
            geometry,
            EnumerateWallIds(WallSet),
            isManualRoom);
    }

    public static List<Vector3> CreateSanitizedPolygonCopy(IReadOnlyList<Vector3> source)
    {
        return PolygonUtility.CreateSanitizedPolygonCopy(source);
    }

    public void SetPlacementOffset(Vector3 offset)
    {
        EnsureData().PlacementOffset = offset;
    }

    public bool SetManualBoundaryVertices(IReadOnlyList<Vector3> polygonVertices, bool clearWallSet = false)
    {
        List<Vector3> sanitizedVertices = PolygonUtility.CreateSanitizedPolygonCopy(polygonVertices);
        if (sanitizedVertices.Count < 3)
        {
            return false;
        }

        if (clearWallSet)
        {
            WallSet = new HashSet<Wall>();
        }

        EnsureData().ApplyLayout(
            sanitizedVertices,
            PolygonUtility.CalculateGeometry(sanitizedVertices),
            EnumerateWallIds(WallSet),
            true);
        return true;
    }

    public void UpdateGeometry(IReadOnlyList<Vector3> polygonVertices, IEnumerable<Wall> walls, IEnumerable<VirtualBoundary> virtualBoundaries = null)
    {
        List<Vector3> sanitizedVertices = PolygonUtility.CreateSanitizedPolygonCopy(polygonVertices);
        WallSet = walls != null ? new HashSet<Wall>(walls) : new HashSet<Wall>();
        EnsureData().ApplyLayout(
            sanitizedVertices,
            PolygonUtility.CalculateGeometry(sanitizedVertices),
            EnumerateWallIds(WallSet),
            false);
    }

    public void SetMaterial(Material material, Color color)
    {
        GetVisualizer(true).SetMaterial(material, color);
    }

    public void SetSelectionState(bool selected, Color highlightColor)
    {
        GetVisualizer(true).SetSelectionState(selected, highlightColor);
    }

    public void SetRoomTypeKey(string typeKey)
    {
        EnsureData().RoomTypeKey = typeKey;
    }

    public void SetRoomName(string value)
    {
        EnsureData().RoomName = value;
    }

    public void SetRoomCode(string value)
    {
        EnsureData().RoomCode = value;
    }

    public void SetRoomNativeCode(string value)
    {
        EnsureData().RoomNativeCode = value;
    }

    public void SetFloorTextureCode(string value)
    {
        EnsureData().FloorTextureCode = value;
    }

    public void SetCeilingTextureCode(string value)
    {
        EnsureData().CeilingTextureCode = value;
    }

    public void SetManualWallIds(IEnumerable<string> ids)
    {
        EnsureData().SetManualWallIds(ids);
    }

    public void ClearManualWallSelection()
    {
        EnsureData().ClearManualWallSelection();
    }

    public bool ReplaceWallReferences(ICollection<Wall> removedWalls, IEnumerable<Wall> addedWalls)
    {
        if (WallSet == null)
        {
            return false;
        }

        RoomData roomData = EnsureData();
        List<Wall> removedRoomWalls = new List<Wall>();
        List<string> removedWallIds = new List<string>();
        bool updateWallSet = false;
        bool changed = false;
        if (removedWalls != null)
        {
            foreach (Wall wall in removedWalls)
            {
                if (wall == null)
                {
                    continue;
                }

                bool removedFromWallSet = WallSet.Remove(wall);
                bool trackedByManualSelection = ContainsAnyWallId(roomData.EffectiveWallIds, wall);
                if (!removedFromWallSet && !trackedByManualSelection)
                {
                    continue;
                }

                removedRoomWalls.Add(wall);
                AddWallId(wall, removedWallIds);
                updateWallSet |= removedFromWallSet;
                changed = true;
            }
        }

        if (removedRoomWalls.Count == 0)
        {
            return changed;
        }

        List<string> addedWallIds = new List<string>();
        if (addedWalls != null)
        {
            foreach (Wall wall in addedWalls)
            {
                if (wall == null || !MatchesAnyReplacedWall(wall, removedRoomWalls))
                {
                    continue;
                }

                AddWallId(wall, addedWallIds);
                if (updateWallSet && WallSet.Add(wall))
                {
                    changed = true;
                }
            }
        }

        if (changed)
        {
            if (updateWallSet)
            {
                roomData.SetWallIds(EnumerateWallIds(WallSet));
            }

            roomData.ReplaceManualWallIds(removedWallIds, addedWallIds);
        }

        return changed;
    }
    public void RefreshVisual()
    {
        GetVisualizer(true).RefreshVisual();
    }

    public bool TryGetOrderedVertices(List<Vector3> results)
    {
        if (results == null)
        {
            return false;
        }

        results.Clear();
        IReadOnlyList<Vector3> vertices = Data.BoundaryVertices;
        if (vertices == null || vertices.Count < 3)
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

        Gizmos.DrawWireSphere(Data.Geometry.Center, 0.2f);
    }

    private RoomData EnsureData()
    {
        if (data == null)
        {
            data = new RoomData();
        }

        return data;
    }

    private RoomVisualizer GetVisualizer(bool createIfMissing)
    {
        RoomVisualizer visualizer = GetComponent<RoomVisualizer>();
        if (visualizer == null && createIfMissing)
        {
            visualizer = gameObject.AddComponent<RoomVisualizer>();
        }

        return visualizer;
    }

    private static void AddWallId(Wall wall, List<string> results)
    {
        if (wall == null || wall.Data == null || string.IsNullOrWhiteSpace(wall.Data.id) || results == null)
        {
            return;
        }

        if (!results.Contains(wall.Data.id))
        {
            results.Add(wall.Data.id);
        }
    }

    private static bool ContainsAnyWallId(IReadOnlyList<string> ids, Wall wall)
    {
        if (ids == null || wall == null || ids.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (wall.Data != null && string.Equals(id, wall.Data.id, System.StringComparison.Ordinal))
            {
                return true;
            }

            WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
            if (container != null && string.Equals(id, container.PersistentWallId, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAnyReplacedWall(Wall candidate, List<Wall> removedWalls)
    {
        if (candidate == null || removedWalls == null || removedWalls.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < removedWalls.Count; i++)
        {
            if (SegmentsOverlapXZ(candidate, removedWalls[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentsOverlapXZ(Wall candidate, Wall removed)
    {
        if (candidate == null || removed == null || candidate.Data == null || removed.Data == null)
        {
            return false;
        }

        Vector3 removedStart = removed.Data.startPoint;
        Vector3 removedEnd = removed.Data.endPoint;
        Vector3 candidateStart = candidate.Data.startPoint;
        Vector3 candidateEnd = candidate.Data.endPoint;
        Vector3 removedDirection = removedEnd - removedStart;
        removedDirection.y = 0f;
        float removedLength = removedDirection.magnitude;
        if (removedLength <= 0.0001f)
        {
            return false;
        }

        removedDirection /= removedLength;
        if (DistanceToSegmentLineXZ(candidateStart, removedStart, removedDirection) > 0.05f ||
            DistanceToSegmentLineXZ(candidateEnd, removedStart, removedDirection) > 0.05f)
        {
            return false;
        }

        float candidateStartDistance = Vector3.Dot(candidateStart - removedStart, removedDirection);
        float candidateEndDistance = Vector3.Dot(candidateEnd - removedStart, removedDirection);
        float min = Mathf.Min(candidateStartDistance, candidateEndDistance);
        float max = Mathf.Max(candidateStartDistance, candidateEndDistance);
        return max > -0.01f && min < removedLength + 0.01f;
    }

    private static float DistanceToSegmentLineXZ(Vector3 point, Vector3 lineStart, Vector3 lineDirection)
    {
        Vector3 offset = point - lineStart;
        offset.y = 0f;
        Vector3 projected = lineDirection * Vector3.Dot(offset, lineDirection);
        return (offset - projected).magnitude;
    }

    private static IEnumerable<string> EnumerateWallIds(IEnumerable<Wall> walls)
    {
        if (walls == null)
        {
            yield break;
        }

        foreach (Wall wall in walls)
        {
            if (wall == null || wall.Data == null || string.IsNullOrWhiteSpace(wall.Data.id))
            {
                continue;
            }

            yield return wall.Data.id;
        }
    }
}
