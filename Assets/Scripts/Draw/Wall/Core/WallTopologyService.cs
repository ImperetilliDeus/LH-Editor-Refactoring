using System.Collections.Generic;
using UnityEngine;

public static class WallTopologyService
{
    private const float ParallelThreshold = 0.0001f;

    public static void GetEndpointsForCenterPosition(Transform wallTransform, Vector3 centerPosition, float planeY, out Vector3 start, out Vector3 end)
    {
        float halfLength = wallTransform.localScale.z * 0.5f;
        Vector3 direction = wallTransform.forward;

        start = centerPosition - direction * halfLength;
        end = centerPosition + direction * halfLength;
        start.y = planeY;
        end.y = planeY;
    }

    public static bool TryGetSnapSegment(Transform wallTransform, float planeY, float minimumLength, out SnapManager.WallSnapSegment segment)
    {
        segment = default;
        GetEndpointsForCenterPosition(wallTransform, wallTransform.position, planeY, out Vector3 start, out Vector3 end);
        float dx = end.x - start.x;
        float dz = end.z - start.z;
        if (dx * dx + dz * dz < minimumLength * minimumLength)
        {
            return false;
        }

        segment = new SnapManager.WallSnapSegment
        {
            start = start,
            end = end,
        };
        return true;
    }

    public static float CalculateEndpointExtension(
        Wall wall,
        int vertexId,
        bool isStartEndpoint,
        IReadOnlyList<Wall> candidateWalls)
    {
        if (wall == null)
        {
            return 0f;
        }

        Vector2 currentOutwardDirection = GetEndpointOutwardDirection2D(wall.Data, isStartEndpoint);
        float currentThickness = wall.transform.localScale.x;
        if (vertexId <= 0 || currentOutwardDirection.sqrMagnitude <= 0.000001f)
        {
            return currentThickness * 0.5f;
        }

        float extension = currentThickness * 0.5f;
        if (candidateWalls == null)
        {
            return extension;
        }

        for (int i = 0; i < candidateWalls.Count; i++)
        {
            Wall other = candidateWalls[i];
            if (other == null || other == wall || !other.ContainsVertexId(vertexId))
            {
                continue;
            }

            extension = Mathf.Max(extension, CalculateJoinExtension(wall, other, vertexId));
        }

        return extension;
    }

    public static float CalculateJoinExtension(Wall currentWall, Wall otherWall, int sharedVertexId)
    {
        if (currentWall == null || otherWall == null)
        {
            return 0f;
        }

        float currentThickness = currentWall.transform.localScale.x;
        float otherThickness = otherWall.transform.localScale.x;

        if (!TryGetVertexWorldPoint(currentWall.Data, currentWall.StartVertexId, currentWall.EndVertexId, sharedVertexId, out Vector3 sharedPoint3))
        {
            return currentThickness * 0.5f;
        }

        if (!TryGetOutwardDirectionForVertex(currentWall.Data, currentWall.StartVertexId, currentWall.EndVertexId, sharedVertexId, out Vector2 thisOutward) ||
            !TryGetOutwardDirectionForVertex(otherWall.Data, otherWall.StartVertexId, otherWall.EndVertexId, sharedVertexId, out Vector2 otherOutward))
        {
            return otherThickness * 0.5f;
        }

        Vector2 sharedPoint = new Vector2(sharedPoint3.x, sharedPoint3.z);
        Vector2 thisNormal = new Vector2(-thisOutward.y, thisOutward.x);
        Vector2 otherNormal = new Vector2(-otherOutward.y, otherOutward.x);

        float fallbackExtension = otherThickness * 0.5f;
        float bestExtension = fallbackExtension;

        for (int currentSign = -1; currentSign <= 1; currentSign += 2)
        {
            Vector2 currentSideOrigin = sharedPoint + thisNormal * (currentSign * currentThickness * 0.5f);
            for (int otherSign = -1; otherSign <= 1; otherSign += 2)
            {
                Vector2 otherSideOrigin = sharedPoint + otherNormal * (otherSign * otherThickness * 0.5f);
                if (!TryIntersectRays(currentSideOrigin, thisOutward, otherSideOrigin, otherOutward, out float currentDistance, out float otherDistance))
                {
                    continue;
                }

                if (currentDistance < 0f || otherDistance < 0f)
                {
                    continue;
                }

                bestExtension = Mathf.Max(bestExtension, currentDistance);
            }
        }

        return bestExtension;
    }

    public static Vector2 GetEndpointOutwardDirection2D(WallData wallData, bool isStartEndpoint)
    {
        Vector3 direction = wallData != null ? wallData.GetDirection() : Vector3.zero;
        Vector2 forward = new Vector2(direction.x, direction.z);
        if (forward.sqrMagnitude <= 0.000001f)
        {
            return Vector2.zero;
        }

        return isStartEndpoint ? -forward.normalized : forward.normalized;
    }

    public static bool TryGetVertexWorldPoint(WallData wallData, int startVertexId, int endVertexId, int vertexId, out Vector3 point)
    {
        if (wallData != null && startVertexId == vertexId)
        {
            point = wallData.startPoint;
            return true;
        }

        if (wallData != null && endVertexId == vertexId)
        {
            point = wallData.endPoint;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    public static bool TryGetOutwardDirectionForVertex(WallData wallData, int startVertexId, int endVertexId, int vertexId, out Vector2 outwardDirection)
    {
        outwardDirection = Vector2.zero;
        if (wallData == null)
        {
            return false;
        }

        Vector3 direction3 = wallData.GetDirection();
        Vector2 direction = new Vector2(direction3.x, direction3.z);
        if (direction.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        direction.Normalize();
        if (startVertexId == vertexId)
        {
            outwardDirection = direction;
            return true;
        }

        if (endVertexId == vertexId)
        {
            outwardDirection = -direction;
            return true;
        }

        return false;
    }

    public static bool TryIntersectRays(
        Vector2 firstOrigin,
        Vector2 firstDirection,
        Vector2 secondOrigin,
        Vector2 secondDirection,
        out float firstDistance,
        out float secondDistance)
    {
        firstDistance = 0f;
        secondDistance = 0f;

        float cross = Cross(firstDirection, secondDirection);
        if (Mathf.Abs(cross) <= ParallelThreshold)
        {
            return false;
        }

        Vector2 delta = secondOrigin - firstOrigin;
        firstDistance = Cross(delta, secondDirection) / cross;
        secondDistance = Cross(delta, firstDirection) / cross;
        return true;
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }
}
