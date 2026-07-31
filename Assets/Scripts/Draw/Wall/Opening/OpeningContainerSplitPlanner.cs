using System.Collections.Generic;
using UnityEngine;

internal static class OpeningContainerSplitPlanner
{
    public static bool TryGetSplitDistance(
        WallOpeningContainer container,
        Wall selectedSegment,
        float minimumWallSegmentLength,
        out float splitDistance)
    {
        Vector3 segmentMidpoint = (selectedSegment.Data.startPoint + selectedSegment.Data.endPoint) * 0.5f;
        splitDistance = Vector3.Dot(segmentMidpoint - container.WallStart, container.WallDirection);
        return splitDistance > minimumWallSegmentLength &&
            splitDistance < container.WallLength - minimumWallSegmentLength;
    }

    public static bool SplitDistanceOverlapsOpeningSpan(
        IReadOnlyList<WallOpening> openings,
        float splitDistance)
    {
        const float splitEpsilon = 0.0001f;
        for (int i = 0; i < openings.Count; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            float openingStart = opening.CenterDistance - opening.Width * 0.5f;
            float openingEnd = opening.CenterDistance + opening.Width * 0.5f;
            if (splitDistance >= openingStart - splitEpsilon && splitDistance <= openingEnd + splitEpsilon)
            {
                return true;
            }
        }

        return false;
    }

    public static void PartitionOpeningsForSplit(
        IReadOnlyList<WallOpening> openings,
        float splitDistance,
        List<UndoRedoManager.OpeningStateSnapshot> leftOpenings,
        List<UndoRedoManager.OpeningStateSnapshot> rightOpenings)
    {
        for (int i = 0; i < openings.Count; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            UndoRedoManager.OpeningStateSnapshot snapshot = new UndoRedoManager.OpeningStateSnapshot
            {
                type = opening.Type,
                doorTypeKey = opening.DoorTypeKey,
                windowTypeKey = opening.WindowTypeKey,
                doorOpensRight = opening.DoorOpensRight,
                doorVerticalFlip = opening.DoorVerticalFlip,
                centerDistance = opening.CenterDistance,
                width = opening.Width,
                height = opening.Height,
                depth = opening.Depth,
                bottomY = opening.BottomY,
            };

            if (opening.CenterDistance < splitDistance)
            {
                leftOpenings.Add(snapshot);
                continue;
            }

            snapshot.centerDistance -= splitDistance;
            rightOpenings.Add(snapshot);
        }
    }
}
