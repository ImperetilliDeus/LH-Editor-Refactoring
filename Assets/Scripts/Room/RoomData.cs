using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class RoomData
{
    [SerializeField, FormerlySerializedAs("roomName")] private string roomNameValue = string.Empty;
    [SerializeField, FormerlySerializedAs("roomTypeKey")] private string roomTypeKeyValue = string.Empty;
    [SerializeField, FormerlySerializedAs("roomCode")] private string roomCodeValue = string.Empty;
    [SerializeField, FormerlySerializedAs("floorTextureCode")] private string floorTextureCodeValue = string.Empty;
    [SerializeField, FormerlySerializedAs("ceilingTextureCode")] private string ceilingTextureCodeValue = string.Empty;
    [SerializeField] private bool isManualRoomValue;
    [SerializeField] private Vector3 placementOffsetValue;
    [SerializeField] private RoomGeometry geometryValue;
    [SerializeField] private List<Vector3> boundaryVertices = new List<Vector3>();
    [SerializeField] private List<string> wallIds = new List<string>();

    [NonSerialized] private int suppressNotifications;

    public event Action Changed;

    public string RoomName
    {
        get => roomNameValue;
        set => SetField(ref roomNameValue, value ?? string.Empty);
    }

    public string RoomTypeKey
    {
        get => roomTypeKeyValue;
        set => SetField(ref roomTypeKeyValue, value ?? string.Empty);
    }

    public string RoomCode
    {
        get => roomCodeValue;
        set => SetField(ref roomCodeValue, value ?? string.Empty);
    }

    public string FloorTextureCode
    {
        get => floorTextureCodeValue;
        set => SetField(ref floorTextureCodeValue, value ?? string.Empty);
    }

    public string CeilingTextureCode
    {
        get => ceilingTextureCodeValue;
        set => SetField(ref ceilingTextureCodeValue, value ?? string.Empty);
    }

    public bool IsManualRoom
    {
        get => isManualRoomValue;
        set => SetField(ref isManualRoomValue, value);
    }

    public Vector3 PlacementOffset
    {
        get => placementOffsetValue;
        set => SetField(ref placementOffsetValue, value);
    }

    public RoomGeometry Geometry
    {
        get => geometryValue;
        set => SetField(ref geometryValue, value);
    }

    public IReadOnlyList<Vector3> BoundaryVertices => boundaryVertices;
    public IReadOnlyList<string> WallIds => wallIds;

    public void SetBoundaryVertices(IReadOnlyList<Vector3> polygonVertices)
    {
        PolygonUtility.CopySanitizedVertices(polygonVertices, boundaryVertices);
        NotifyChanged();
    }

    public void SetWallIds(IEnumerable<string> ids)
    {
        wallIds.Clear();
        if (ids != null)
        {
            foreach (string id in ids)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    wallIds.Add(id);
                }
            }
        }

        NotifyChanged();
    }

    public void ApplyLayout(
        IReadOnlyList<Vector3> polygonVertices,
        RoomGeometry geometry,
        IEnumerable<string> ids,
        bool isManualRoom)
    {
        suppressNotifications++;
        try
        {
            SetBoundaryVertices(polygonVertices);
            Geometry = geometry;
            SetWallIds(ids);
            IsManualRoom = isManualRoom;
        }
        finally
        {
            suppressNotifications--;
        }

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (suppressNotifications > 0)
        {
            return;
        }

        Changed?.Invoke();
    }

    private void SetField<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        NotifyChanged();
    }
}
