using System;
using System.Collections.Generic;
using UnityEngine.Serialization;
using UnityEngine;

[Serializable]
public class WallData
{
    [SerializeField, FormerlySerializedAs("id")] private string idValue;
    [SerializeField, FormerlySerializedAs("startPoint")] private Vector3 startPointValue;
    [SerializeField, FormerlySerializedAs("endPoint")] private Vector3 endPointValue;
    [SerializeField, FormerlySerializedAs("thickness")] private float thicknessValue;
    [SerializeField, FormerlySerializedAs("height")] private float heightValue;
    [SerializeField, FormerlySerializedAs("centerY")] private float centerYValue;

    [NonSerialized] private int suppressNotifications;

    public event Action Changed;

    public string id
    {
        get => idValue;
        set => SetField(ref idValue, value);
    }

    public Vector3 startPoint
    {
        get => startPointValue;
        set => SetField(ref startPointValue, value);
    }

    public Vector3 endPoint
    {
        get => endPointValue;
        set => SetField(ref endPointValue, value);
    }

    public float thickness
    {
        get => thicknessValue;
        set => SetField(ref thicknessValue, value);
    }

    public float height
    {
        get => heightValue;
        set => SetField(ref heightValue, value);
    }

    public float centerY
    {
        get => centerYValue;
        set => SetField(ref centerYValue, value);
    }

    public WallData()
    {
        idValue = Guid.NewGuid().ToString("N");
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
