using UnityEngine;

public static class DrawingOverlayCalibrationService
{
    private const float MinimumPixelDistance = 1f;

    public static bool TrySolve(DrawingOverlayDocument document, out DrawingOverlayTransform solved)
    {
        solved = new DrawingOverlayTransform();
        if (document == null || document.source == null || document.calibration == null)
        {
            return false;
        }

        if (!document.calibration.hasAnchorA || !document.calibration.hasAnchorB)
        {
            return false;
        }

        float pixelDistance = Vector2.Distance(document.calibration.anchorPixelA, document.calibration.anchorPixelB);
        if (pixelDistance < MinimumPixelDistance || document.calibration.realDistanceMm <= 0f)
        {
            return false;
        }

        solved.mmPerPixel = document.calibration.realDistanceMm / pixelDistance;
        solved.unitPerPixel = DrawingOverlayUnits.MillimetersToUnits(solved.mmPerPixel);
        solved.totalRotationDeg = GetAutoRotationDeg(document) + document.calibration.manualRotationOffsetDeg;

        Vector2 originPixel = document.calibration.hasOriginPixel
            ? document.calibration.originPixel
            : GetImageCenterPixel(document.source);
        Vector2 originWorld = document.calibration.originWorldXZ;
        Vector2 local = PixelToLocalCentered(originPixel, document.source);
        Vector2 transformed = TransformLocalToWorldVector(local, solved.unitPerPixel, solved.totalRotationDeg, document.calibration.flipX, document.calibration.flipY);
        solved.worldOffsetXZ = originWorld - transformed;
        return true;
    }

    public static Vector2 PixelToWorldXZ(Vector2 pixel, DrawingOverlayDocument document)
    {
        if (document == null || document.source == null || document.calibration == null || document.solved == null)
        {
            return Vector2.zero;
        }

        Vector2 local = PixelToLocalCentered(pixel, document.source);
        Vector2 transformed = TransformLocalToWorldVector(
            local,
            document.solved.unitPerPixel,
            document.solved.totalRotationDeg,
            document.calibration.flipX,
            document.calibration.flipY);
        return transformed + document.solved.worldOffsetXZ;
    }

    public static Vector2 WorldXZToPixel(Vector2 worldXZ, DrawingOverlayDocument document)
    {
        if (document == null || document.source == null || document.calibration == null || document.solved == null)
        {
            return Vector2.zero;
        }

        Vector2 delta = worldXZ - document.solved.worldOffsetXZ;
        Vector2 unrotated = Rotate(delta, -document.solved.totalRotationDeg);
        if (Mathf.Abs(document.solved.unitPerPixel) > 0.000001f)
        {
            unrotated /= document.solved.unitPerPixel;
        }

        if (document.calibration.flipX)
        {
            unrotated.x = -unrotated.x;
        }

        if (document.calibration.flipY)
        {
            unrotated.y = -unrotated.y;
        }

        return new Vector2(
            unrotated.x + document.source.pixelWidth * 0.5f,
            -unrotated.y + document.source.pixelHeight * 0.5f);
    }

    public static Vector2 PixelToLocalCentered(Vector2 pixel, DrawingOverlaySource source)
    {
        return new Vector2(
            pixel.x - source.pixelWidth * 0.5f,
            -(pixel.y - source.pixelHeight * 0.5f));
    }

    public static Vector2 GetImageCenterPixel(DrawingOverlaySource source)
    {
        return new Vector2(source.pixelWidth * 0.5f, source.pixelHeight * 0.5f);
    }

    public static float GetAutoRotationDeg(DrawingOverlayDocument document)
    {
        if (document == null || document.calibration == null || !document.calibration.hasRotationGuide ||
            !document.calibration.hasRotationPointA || !document.calibration.hasRotationPointB)
        {
            return 0f;
        }

        Vector2 delta = document.calibration.rotationPixelB - document.calibration.rotationPixelA;
        if (delta.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        float guideAngleDeg = Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg;
        float targetAngleDeg = document.calibration.rotationGuideShouldBeHorizontal ? 0f : 90f;
        return Mathf.DeltaAngle(guideAngleDeg, targetAngleDeg);
    }

    private static Vector2 TransformLocalToWorldVector(
        Vector2 local,
        float unitPerPixel,
        float rotationDeg,
        bool flipX,
        bool flipY)
    {
        if (flipX)
        {
            local.x = -local.x;
        }

        if (flipY)
        {
            local.y = -local.y;
        }

        local *= unitPerPixel;
        return Rotate(local, rotationDeg);
    }

    private static Vector2 Rotate(Vector2 value, float rotationDeg)
    {
        float rad = rotationDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(
            value.x * cos - value.y * sin,
            value.x * sin + value.y * cos);
    }
}
