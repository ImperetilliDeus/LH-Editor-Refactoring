using System;
using UnityEngine;

[Serializable]
public class WallData
{
    public string id;
    public Vector3 startPoint;
    public Vector3 endPoint;
    public float thickness;
    public float height;
    public float centerY;

    public WallData()
    {
        id = Guid.NewGuid().ToString("N");
    }

    public WallData(Vector3 start, Vector3 end, float wallThickness, float wallHeight, float wallCenterY)
        : this()
    {
        startPoint = start;
        endPoint = end;
        thickness = wallThickness;
        height = wallHeight;
        centerY = wallCenterY;
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

        id = source.id;
        startPoint = source.startPoint;
        endPoint = source.endPoint;
        thickness = source.thickness;
        height = source.height;
        centerY = source.centerY;
    }
}
