using System.Collections.Generic;
using UnityEngine;

public static class VirtualBoundaryUtility
{
    private const float DirectionDotThreshold = 0.995f;
    private const float Epsilon = 0.0001f;

    public static bool TryBuildRectangleOutlineFromRect(
        Vector3 startCorner,
        Vector3 endCorner,
        float minimumBoundaryLength,
        List<(Vector3 start, Vector3 end)> segments,
        out Bounds previewBounds)
    {
        float minX = Mathf.Min(startCorner.x, endCorner.x);
        float maxX = Mathf.Max(startCorner.x, endCorner.x);
        float minZ = Mathf.Min(startCorner.z, endCorner.z);
        float maxZ = Mathf.Max(startCorner.z, endCorner.z);
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;
        float sizeX = maxX - minX;
        float sizeZ = maxZ - minZ;
        float y = startCorner.y;

        previewBounds = new Bounds(
            new Vector3(centerX, y, centerZ),
            new Vector3(Mathf.Max(0.01f, sizeX), 0.02f, Mathf.Max(0.01f, sizeZ)));

        if (segments != null)
        {
            segments.Clear();
        }

        if (sizeX < minimumBoundaryLength || sizeZ < minimumBoundaryLength)
        {
            return false;
        }

        if (segments != null)
        {
            Vector3 bottomLeft = new Vector3(minX, y, minZ);
            Vector3 bottomRight = new Vector3(maxX, y, minZ);
            Vector3 topRight = new Vector3(maxX, y, maxZ);
            Vector3 topLeft = new Vector3(minX, y, maxZ);

            segments.Add((bottomLeft, bottomRight));
            segments.Add((bottomRight, topRight));
            segments.Add((topRight, topLeft));
            segments.Add((topLeft, bottomLeft));
        }

        return true;
    }

    public static void MergeBoundaryIntoPeers(VirtualBoundary boundary, IEnumerable<VirtualBoundary> peers, float overlapOffset)
    {
        if (boundary == null || peers == null)
        {
            return;
        }

        if (!boundary.TryGetResolvedEndpoints(out Vector3 mergedStart, out Vector3 mergedEnd))
        {
            return;
        }

        bool mergedAny = true;
        while (mergedAny)
        {
            mergedAny = false;
            foreach (VirtualBoundary peer in peers)
            {
                if (peer == null || peer == boundary || !peer.isActiveAndEnabled || peer.PreviewOnly)
                {
                    continue;
                }

                if (!peer.TryGetResolvedEndpoints(out Vector3 peerStart, out Vector3 peerEnd))
                {
                    continue;
                }

                if (!TryMergeCollinearSegments(mergedStart, mergedEnd, peerStart, peerEnd, overlapOffset, out Vector3 candidateStart, out Vector3 candidateEnd))
                {
                    continue;
                }

                mergedStart = candidateStart;
                mergedEnd = candidateEnd;
                boundary.SetEndpoints(mergedStart, mergedEnd);
                Object.Destroy(peer.gameObject);
                mergedAny = true;
                break;
            }
        }
    }

    public static bool TryMergeCollinearSegments(
        Vector3 firstStart,
        Vector3 firstEnd,
        Vector3 secondStart,
        Vector3 secondEnd,
        float overlapOffset,
        out Vector3 mergedStart,
        out Vector3 mergedEnd)
    {
        mergedStart = Vector3.zero;
        mergedEnd = Vector3.zero;

        Vector3 firstDirection = firstEnd - firstStart;
        float firstLength = firstDirection.magnitude;
        if (firstLength <= Epsilon)
        {
            return false;
        }

        Vector3 secondDirection = secondEnd - secondStart;
        float secondLength = secondDirection.magnitude;
        if (secondLength <= Epsilon)
        {
            return false;
        }

        Vector3 firstDir = firstDirection / firstLength;
        Vector3 secondDir = secondDirection / secondLength;
        if (Mathf.Abs(Vector3.Dot(firstDir, secondDir)) < DirectionDotThreshold)
        {
            return false;
        }

        Vector3 normal = new Vector3(-firstDir.z, 0f, firstDir.x);
        float distanceA = Mathf.Abs(Vector3.Dot(secondStart - firstStart, normal));
        float distanceB = Mathf.Abs(Vector3.Dot(secondEnd - firstStart, normal));
        if (distanceA > overlapOffset || distanceB > overlapOffset)
        {
            return false;
        }

        float firstMin = 0f;
        float firstMax = firstLength;
        float secondProjectionA = Vector3.Dot(secondStart - firstStart, firstDir);
        float secondProjectionB = Vector3.Dot(secondEnd - firstStart, firstDir);
        float secondMin = Mathf.Min(secondProjectionA, secondProjectionB);
        float secondMax = Mathf.Max(secondProjectionA, secondProjectionB);

        if (secondMax < firstMin - overlapOffset || secondMin > firstMax + overlapOffset)
        {
            return false;
        }

        float mergedMin = Mathf.Min(firstMin, secondMin);
        float mergedMax = Mathf.Max(firstMax, secondMax);
        mergedStart = firstStart + firstDir * mergedMin;
        mergedEnd = firstStart + firstDir * mergedMax;
        return (mergedEnd - mergedStart).sqrMagnitude > Epsilon * Epsilon;
    }

    public static void CollectSnapPoints(List<Vector3> points, IEnumerable<VirtualBoundary> boundaries, VirtualBoundary ignoreBoundary = null)
    {
        if (points == null || boundaries == null)
        {
            return;
        }

        foreach (VirtualBoundary boundary in boundaries)
        {
            if (boundary == null || boundary == ignoreBoundary || !boundary.isActiveAndEnabled || boundary.PreviewOnly)
            {
                continue;
            }

            if (!boundary.TryGetResolvedEndpoints(out Vector3 startPoint, out Vector3 endPoint))
            {
                continue;
            }

            points.Add(startPoint);
            points.Add(endPoint);
        }
    }

}
