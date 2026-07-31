using UnityEngine;

internal static class HandleGeometryUtility
{
    public static bool HasValidWallHandleGeometry(Wall wall, float minimumWallLength)
    {
        if (wall == null || wall.Data == null)
        {
            return false;
        }

        Vector3 startPoint = wall.Data.startPoint;
        Vector3 endPoint = wall.Data.endPoint;
        if (!IsValidWorldPoint(startPoint) || !IsValidWorldPoint(endPoint))
        {
            return false;
        }

        Vector3 delta = endPoint - startPoint;
        delta.y = 0f;
        return delta.sqrMagnitude >= minimumWallLength * minimumWallLength;
    }

    public static bool IsValidWorldPoint(Vector3 point)
    {
        return IsFinite(point.x) && IsFinite(point.y) && IsFinite(point.z);
    }

    public static float DistanceSqrXZ(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    public static bool HasPointerMovedBeyondThreshold(
        Vector2 currentPosition,
        Vector2 startPosition,
        float thresholdPixels)
    {
        float thresholdSqr = thresholdPixels * thresholdPixels;
        float movedSqr = (currentPosition - startPosition).sqrMagnitude;
        return movedSqr >= thresholdSqr;
    }

    public static Vector3 ConstrainPointToSegment(
        Vector3 point,
        Vector3 segmentStart,
        Vector3 segmentEnd,
        float minimumDistanceFromEndpoint,
        float planeHeight)
    {
        Vector3 direction = segmentEnd - segmentStart;
        direction.y = 0f;
        float length = direction.magnitude;
        if (length <= 0.0001f)
        {
            point = segmentStart;
            point.y = planeHeight;
            return point;
        }

        direction /= length;
        float minDistance = Mathf.Min(minimumDistanceFromEndpoint, length * 0.5f);
        float maxDistance = Mathf.Max(minDistance, length - minimumDistanceFromEndpoint);
        float projectedDistance = Vector3.Dot(point - segmentStart, direction);
        float clampedDistance = Mathf.Clamp(projectedDistance, minDistance, maxDistance);
        Vector3 constrainedPoint = segmentStart + direction * clampedDistance;
        constrainedPoint.y = planeHeight;
        return constrainedPoint;
    }

    public static bool TryProjectPointerToPlane(
        Camera camera,
        Vector2 pointerScreenPosition,
        Plane plane,
        float planeHeight,
        out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        Ray ray = EditorScreenCoordinateUtility.ScreenPointToRay(camera, pointerScreenPosition);
        if (!plane.Raycast(ray, out float enter))
        {
            return false;
        }

        worldPoint = ray.GetPoint(enter);
        worldPoint.y = planeHeight;
        return true;
    }

    public static float GetMinimumLengthForContainerPoint(
        Vector3 point,
        int vertexId,
        bool isDraggingStart,
        int outerStartVertexId,
        int outerEndVertexId,
        Vector3 oldStart,
        Vector3 oldEnd,
        Vector3 oldDirection,
        float minimumWallLength,
        float planeHeight)
    {
        point.y = planeHeight;

        if (isDraggingStart)
        {
            if (vertexId == outerStartVertexId)
            {
                return minimumWallLength;
            }

            float distanceFromEnd = Vector3.Dot(oldEnd - point, oldDirection);
            return distanceFromEnd + minimumWallLength;
        }

        if (vertexId == outerEndVertexId)
        {
            return minimumWallLength;
        }

        float distanceFromStart = Vector3.Dot(point - oldStart, oldDirection);
        return distanceFromStart + minimumWallLength;
    }

    public static Vector3 ResolveContainerEndpoint(
        Vector3 oldPoint,
        int endpointVertexId,
        bool isDraggingStart,
        int outerStartVertexId,
        int outerEndVertexId,
        Vector3 oldStart,
        Vector3 oldEnd,
        Vector3 newStart,
        Vector3 newEnd,
        Vector3 oldDirection,
        Vector3 newDirection,
        float planeHeight)
    {
        oldPoint.y = planeHeight;

        if (isDraggingStart && endpointVertexId == outerStartVertexId)
        {
            return newStart;
        }

        if (!isDraggingStart && endpointVertexId == outerEndVertexId)
        {
            return newEnd;
        }

        if (isDraggingStart)
        {
            float distanceFromEnd = Vector3.Dot(oldEnd - oldPoint, oldDirection);
            Vector3 resolved = newEnd - newDirection * distanceFromEnd;
            resolved.y = planeHeight;
            return resolved;
        }

        float distanceFromStart = Vector3.Dot(oldPoint - oldStart, oldDirection);
        Vector3 next = newStart + newDirection * distanceFromStart;
        next.y = planeHeight;
        return next;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
