using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[Serializable]
public class WallData
{
    [SerializeField, FormerlySerializedAs("id")] private string _id;
    [SerializeField, FormerlySerializedAs("startPoint")] private Vector3 _startPoint;
    [SerializeField, FormerlySerializedAs("endPoint")] private Vector3 _endPoint;
    [SerializeField, FormerlySerializedAs("thickness")] private float _thickness;
    [SerializeField, FormerlySerializedAs("height")] private float _height;
    [SerializeField, FormerlySerializedAs("centerY")] private float _centerY;
    [SerializeField] private string textureCode = string.Empty;

    [SerializeField] private List<WallOpeningData> openings = new List<WallOpeningData>();
    [NonSerialized] private int suppressNotifications;

    public event Action Changed;

    public string id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public Vector3 startPoint
    {
        get => _startPoint;
        set => SetField(ref _startPoint, value);
    }

    public Vector3 endPoint
    {
        get => _endPoint;
        set => SetField(ref _endPoint, value);
    }

    public float thickness
    {
        get => _thickness;
        set => SetField(ref _thickness, value);
    }

    public float height
    {
        get => _height;
        set => SetField(ref _height, value);
    }

    public float centerY
    {
        get => _centerY;
        set => SetField(ref _centerY, value);
    }

    public string TextureCode
    {
        get => textureCode;
        set => SetField(ref textureCode, value ?? string.Empty);
    }

    public List<WallOpeningData> Openings => openings;

    public WallData()
    {
        _id = Guid.NewGuid().ToString("N");
    }

    public WallData(Vector3 start, Vector3 end, float wallThickness, float wallHeight, float wallCenterY)
        : this()
    {
        SetGeometry(start, end, wallThickness, wallHeight, wallCenterY);
    }

    public float GetLength()
    {
        return Vector3.Distance(startPoint, endPoint);
    }

    public Vector3 GetDirection()
    {
        Vector3 direction = endPoint - startPoint;
        return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.zero;
    }

    public bool MeetsMinimumLength(float minimumLength)
    {
        Vector3 flatDirection = endPoint - startPoint;
        flatDirection.y = 0f;
        return flatDirection.magnitude >= minimumLength;
    }

    public WallData Clone()
    {
        return new WallData
        {
            id = id,
            startPoint = startPoint,
            endPoint = endPoint,
            thickness = thickness,
            height = height,
            centerY = centerY,
            TextureCode = TextureCode,
        };
    }

    public void CopyFrom(WallData source)
    {
        if (source == null)
        {
            return;
        }

        suppressNotifications++;
        try
        {
            id = source.id;
            startPoint = source.startPoint;
            endPoint = source.endPoint;
            thickness = source.thickness;
            height = source.height;
            centerY = source.centerY;
            TextureCode = source.TextureCode;
        }
        finally
        {
            suppressNotifications--;
        }

        NotifyChanged();
    }

    public void SetGeometry(Vector3 start, Vector3 end, float wallThickness, float wallHeight, float wallCenterY)
    {
        suppressNotifications++;
        try
        {
            startPoint = start;
            endPoint = end;
            thickness = wallThickness;
            height = wallHeight;
            centerY = wallCenterY;
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
