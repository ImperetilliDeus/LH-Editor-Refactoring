using System.Collections.Generic;
using UnityEngine;

public static class PolylineXZ
{
    // Segment lengths are 3D distances. minimumRequiredTotalLength is the shortest total length that keeps every
    // segment at or above minimumSegmentLength when the polyline is rescaled proportionally.
    public static bool TryGetSegmentLengths(
        IReadOnlyList<Vector3> points,
        float minimumSegmentLength,
        List<float> segmentLengths,
        out float totalLength,
        out float minimumRequiredTotalLength)
    {
        totalLength = 0f;
        minimumRequiredTotalLength = 0f;
        segmentLengths?.Clear();
        if (points == null || segmentLengths == null || points.Count < 2)
        {
            return false;
        }

        for (int i = 0; i < points.Count - 1; i++)
        {
            float segmentLength = Vector3.Distance(points[i], points[i + 1]);
            if (segmentLength <= GeometryTolerance.PolygonVertexEpsilon)
            {
                return false;
            }

            totalLength += segmentLength;
            segmentLengths.Add(segmentLength);
        }

        if (totalLength <= GeometryTolerance.PolygonVertexEpsilon)
        {
            return false;
        }

        for (int i = 0; i < segmentLengths.Count; i++)
        {
            float segmentRatio = segmentLengths[i] / totalLength;
            minimumRequiredTotalLength = Mathf.Max(minimumRequiredTotalLength, minimumSegmentLength / segmentRatio);
        }

        return true;
    }

    // Moves the first point to newStart and lays the inner points on the straight line to the fixed last point,
    // keeping their original length ratios. Fails without touching points when any segment would end up shorter
    // than minimumSegmentLength. All points are placed at planeY.
    public static bool TryRescaleKeepingEnd(
        List<Vector3> points,
        Vector3 newStart,
        float minimumSegmentLength,
        float planeY,
        List<float> segmentLengthsBuffer)
    {
        if (!TryGetSegmentLengths(points, minimumSegmentLength, segmentLengthsBuffer, out float originalTotalLength, out float minimumRequiredTotalLength))
        {
            return false;
        }

        Vector3 endPoint = points[points.Count - 1];
        endPoint.y = planeY;
        newStart.y = planeY;

        Vector3 startToEnd = newStart - endPoint;
        startToEnd.y = 0f;
        float newTotalLength = startToEnd.magnitude;
        if (newTotalLength <= minimumSegmentLength || newTotalLength < minimumRequiredTotalLength)
        {
            return false;
        }

        Vector3 direction = (endPoint - newStart).normalized;
        points[0] = newStart;
        float accumulatedDistance = 0f;
        for (int i = 1; i < points.Count - 1; i++)
        {
            float segmentRatio = segmentLengthsBuffer[i - 1] / originalTotalLength;
            accumulatedDistance += newTotalLength * segmentRatio;
            Vector3 point = newStart + direction * accumulatedDistance;
            point.y = planeY;
            points[i] = point;
        }

        points[points.Count - 1] = endPoint;
        return true;
    }

    // Point at the given XZ distance from origin in the direction of toward. Keeps origin.y.
    // Callers must ensure toward is not at origin in XZ.
    public static Vector3 PointAtDistanceToward(Vector3 origin, Vector3 toward, float distance)
    {
        Vector3 direction = toward - origin;
        direction.y = 0f;
        direction /= direction.magnitude;
        return origin + direction * distance;
    }
}
