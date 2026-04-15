using System;
using UnityEngine;

public enum OverlaySourceType
{
    Image = 0,
    PdfPage = 1,
}

public enum OverlayCalibrationStep
{
    Idle = 0,
    PickingAnchorA = 1,
    PickingAnchorB = 2,
    PickingRotationA = 3,
    PickingRotationB = 4,
    PickingOrigin = 5,
    ReadyToApply = 6,
}

[Serializable]
public sealed class DrawingOverlaySource
{
    public string sourcePath;
    public OverlaySourceType sourceType;
    public int pdfPageIndex;
    public int pixelWidth;
    public int pixelHeight;
    public float embeddedDpiX;
    public float embeddedDpiY;
    public int originalRotationDeg;
    public bool hasReliablePhysicalPageSize;
    public Vector2 physicalPageSizeMm;
}

[Serializable]
public sealed class DrawingOverlayCalibration
{
    public Vector2 anchorPixelA;
    public Vector2 anchorPixelB;
    public float realDistanceMm = 3000f;
    public bool hasAnchorA;
    public bool hasAnchorB;

    public bool hasRotationGuide;
    public Vector2 rotationPixelA;
    public Vector2 rotationPixelB;
    public bool hasRotationPointA;
    public bool hasRotationPointB;
    public bool rotationGuideShouldBeHorizontal = true;

    public Vector2 originPixel;
    public Vector2 originWorldXZ;
    public bool hasOriginPixel;

    public float manualRotationOffsetDeg;
    [Range(0f, 1f)] public float opacity = 0.35f;
    public bool flipX;
    public bool flipY;
}

[Serializable]
public sealed class DrawingOverlayTransform
{
    public float mmPerPixel;
    public float unitPerPixel;
    public float totalRotationDeg;
    public Vector2 worldOffsetXZ;
}

[Serializable]
public sealed class DrawingOverlayDocument
{
    public string id;
    public DrawingOverlaySource source = new DrawingOverlaySource();
    public DrawingOverlayCalibration calibration = new DrawingOverlayCalibration();
    public DrawingOverlayTransform solved = new DrawingOverlayTransform();

    public void ResetCalibration()
    {
        calibration = new DrawingOverlayCalibration();
        solved = new DrawingOverlayTransform();
    }
}

public static class DrawingOverlayUnits
{
    public const float UnitToMillimeters = 100f;

    public static float MillimetersToUnits(float millimeters)
    {
        return millimeters / UnitToMillimeters;
    }

    public static float UnitsToMillimeters(float units)
    {
        return units * UnitToMillimeters;
    }
}
