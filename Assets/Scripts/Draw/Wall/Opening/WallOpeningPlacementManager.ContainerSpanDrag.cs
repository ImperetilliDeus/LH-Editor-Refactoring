using UnityEngine;

public partial class WallOpeningPlacementManager
{
    public void ApplyContainerSpanFromExternalDrag(
        WallOpeningContainer container,
        Vector3 newStart,
        Vector3 newEnd,
        UndoRedoManager.OpeningLayoutSnapshot baselineSnapshot)
    {
        if (container == null || !baselineSnapshot.hasContainer)
        {
            return;
        }

        RestoreContainerSpanDragSnapshotSettings(container, baselineSnapshot);

        float oldLength = Vector3.Distance(baselineSnapshot.wallStart, baselineSnapshot.wallEnd);
        float newLength = Vector3.Distance(newStart, newEnd);
        if (!HasValidContainerSpanLength(oldLength) || !HasValidContainerSpanLength(newLength))
        {
            return;
        }

        bool movedStart = HasContainerSpanEndpointMoved(newStart, baselineSnapshot.wallStart);
        bool movedEnd = HasContainerSpanEndpointMoved(newEnd, baselineSnapshot.wallEnd);
        bool translated = IsContainerSpanTranslated(newStart, newEnd, baselineSnapshot, movedStart, movedEnd);

        ApplyContainerSpanEndpointConstraints(container, ref newStart, ref newEnd, movedStart, movedEnd);

        newLength = Vector3.Distance(newStart, newEnd);
        if (!HasValidContainerSpanLength(newLength))
        {
            return;
        }

        container.SetWallSpan(newStart, newEnd);
        RepositionOpeningsAfterContainerSpanChange(
            container,
            baselineSnapshot,
            oldLength,
            newLength,
            movedStart,
            movedEnd,
            translated);

        RebuildContainer(container, true);

        RefreshSelectedWallAfterContainerSpanDrag(container, newStart, newLength, baselineSnapshot);
    }

    private void RepositionOpeningsAfterContainerSpanChange(
        WallOpeningContainer container,
        UndoRedoManager.OpeningLayoutSnapshot baselineSnapshot,
        float oldLength,
        float newLength,
        bool movedStart,
        bool movedEnd,
        bool translated)
    {
        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));

        int openingCount = Mathf.Min(
            baselineSnapshot.openings != null ? baselineSnapshot.openings.Length : 0,
            cachedOpenings.Count);
        for (int i = 0; i < openingCount; i++)
        {
            WallOpening opening = cachedOpenings[i];
            UndoRedoManager.OpeningStateSnapshot openingSnapshot = baselineSnapshot.openings[i];
            float nextCenterDistance = ResolveOpeningCenterDistanceAfterSpanChange(
                openingSnapshot,
                oldLength,
                newLength,
                movedStart,
                movedEnd,
                translated);

            opening.SetCenterDistance(ClampOpeningCenterDistance(container, opening, nextCenterDistance, opening.Width));
        }
    }

    private static void RestoreContainerSpanDragSnapshotSettings(
        WallOpeningContainer container,
        UndoRedoManager.OpeningLayoutSnapshot baselineSnapshot)
    {
        container.SetOuterSplitPointFlags(baselineSnapshot.outerStartSplitPoint, baselineSnapshot.outerEndSplitPoint);
        container.SetHandleSuppression(baselineSnapshot.suppressOuterStartHandle, baselineSnapshot.suppressOuterEndHandle);
    }

    private static float ResolveOpeningCenterDistanceAfterSpanChange(
        UndoRedoManager.OpeningStateSnapshot openingSnapshot,
        float oldLength,
        float newLength,
        bool movedStart,
        bool movedEnd,
        bool translated)
    {
        if (translated || (!movedStart && !movedEnd))
        {
            return openingSnapshot.centerDistance;
        }

        if (movedStart && !movedEnd)
        {
            float distanceFromEnd = oldLength - openingSnapshot.centerDistance;
            return newLength - distanceFromEnd;
        }

        if (!movedStart && movedEnd)
        {
            return openingSnapshot.centerDistance;
        }

        return oldLength > 0.001f ? (openingSnapshot.centerDistance / oldLength) * newLength : newLength * 0.5f;
    }

    private static bool HasContainerSpanEndpointMoved(Vector3 currentPoint, Vector3 baselinePoint)
    {
        return (currentPoint - baselinePoint).sqrMagnitude > 0.000001f;
    }

    private static bool HasValidContainerSpanLength(float spanLength)
    {
        return spanLength > MinimumWallSegmentLength;
    }

    private void ApplyContainerSpanEndpointConstraints(
        WallOpeningContainer container,
        ref Vector3 newStart,
        ref Vector3 newEnd,
        bool movedStart,
        bool movedEnd)
    {
        if (TryConstrainContainerStartEndpointDrag(container, newStart, movedStart, movedEnd, out Vector3 constrainedStart))
        {
            newStart = constrainedStart;
        }

        if (TryConstrainContainerEndEndpointDrag(container, newEnd, movedStart, movedEnd, out Vector3 constrainedEnd))
        {
            newEnd = constrainedEnd;
        }
    }

    private void RefreshSelectedWallAfterContainerSpanDrag(
        WallOpeningContainer container,
        Vector3 newStart,
        float newLength,
        UndoRedoManager.OpeningLayoutSnapshot baselineSnapshot)
    {
        if (wallSelectionManager == null || wallSelectionManager.SelectedWall != null)
        {
            return;
        }

        // After clearing selection during drag, re-select the appropriate segment.
        bool startDragged = (newStart - baselineSnapshot.wallStart).sqrMagnitude > 0.001f;
        float preferredDistance = startDragged ? 0f : newLength;
        RefreshSelectedWallForContainer(container, preferredDistance);
    }

    private static bool IsContainerSpanTranslated(
        Vector3 newStart,
        Vector3 newEnd,
        UndoRedoManager.OpeningLayoutSnapshot baselineSnapshot,
        bool movedStart,
        bool movedEnd)
    {
        Vector3 startDelta = newStart - baselineSnapshot.wallStart;
        Vector3 endDelta = newEnd - baselineSnapshot.wallEnd;
        return movedStart && movedEnd && (startDelta - endDelta).sqrMagnitude <= 0.000001f;
    }

    private bool TryConstrainContainerStartEndpointDrag(
        WallOpeningContainer container,
        Vector3 newStart,
        bool movedStart,
        bool movedEnd,
        out Vector3 constrainedStart)
    {
        constrainedStart = newStart;
        return movedStart && !movedEnd &&
            TryConstrainContainerOuterSplitPointDrag(container, container.OuterStartVertexId, newStart, out constrainedStart);
    }

    private bool TryConstrainContainerEndEndpointDrag(
        WallOpeningContainer container,
        Vector3 newEnd,
        bool movedStart,
        bool movedEnd,
        out Vector3 constrainedEnd)
    {
        constrainedEnd = newEnd;
        return !movedStart && movedEnd &&
            TryConstrainContainerOuterSplitPointDrag(container, container.OuterEndVertexId, newEnd, out constrainedEnd);
    }

    internal bool TryConstrainContainerOuterSplitPointDrag(
        WallOpeningContainer container,
        int draggedVertexId,
        Vector3 desiredPoint,
        out Vector3 constrainedPoint)
    {
        constrainedPoint = desiredPoint;
        if (container == null)
        {
            return false;
        }

        if (!TryResolveOuterSplitDragTarget(container, draggedVertexId, out bool draggingStart, out bool draggingEnd))
        {
            return false;
        }

        CollectOpenings(container, cachedOpenings);
        if (cachedOpenings.Count == 0)
        {
            return true;
        }

        float effectiveSideWall = MinimumWallSegmentLength;
        float minimumRequiredLength = MinimumWallSegmentLength;

        if (!TryResolveOuterSplitMinimumLengthPoint(
            desiredPoint,
            container.WallStart,
            container.WallEnd,
            draggingStart,
            minimumRequiredLength,
            out constrainedPoint))
        {
            constrainedPoint = desiredPoint;
        }

        constrainedPoint = ApplyOuterSplitDistanceConstraints(
            container,
            constrainedPoint,
            desiredPoint.y,
            cachedOpenings,
            effectiveSideWall);
        return true;
    }

    private static Vector3 ApplyOuterSplitDistanceConstraints(
        WallOpeningContainer container,
        Vector3 constrainedPoint,
        float desiredY,
        System.Collections.Generic.IReadOnlyList<WallOpening> openings,
        float clearance)
    {
        Vector3 wallDirection = container.WallDirection;
        float desiredDistance = Vector3.Dot(constrainedPoint - container.WallStart, wallDirection);
        desiredDistance = MoveOuterSplitDistanceOutsideOpeningSpan(
            desiredDistance,
            openings,
            clearance);

        Vector3 result = container.WallStart + wallDirection * desiredDistance;
        result.y = desiredY;
        return result;
    }

    private static bool TryResolveOuterSplitDragTarget(
        WallOpeningContainer container,
        int draggedVertexId,
        out bool draggingStart,
        out bool draggingEnd)
    {
        draggingStart = draggedVertexId == container.OuterStartVertexId;
        draggingEnd = draggedVertexId == container.OuterEndVertexId;
        return draggingStart || draggingEnd;
    }

    private static bool TryResolveOuterSplitMinimumLengthPoint(
        Vector3 desiredPoint,
        Vector3 wallStart,
        Vector3 wallEnd,
        bool draggingStart,
        float minimumRequiredLength,
        out Vector3 constrainedPoint)
    {
        constrainedPoint = desiredPoint;

        Vector3 anchorPoint = draggingStart ? wallEnd : wallStart;
        Vector3 desiredOffset = desiredPoint - anchorPoint;
        desiredOffset.y = 0f;
        float desiredLength = desiredOffset.magnitude;
        if (desiredLength >= minimumRequiredLength - 0.0001f)
        {
            return false;
        }

        Vector3 fallbackDirection = draggingStart
            ? wallStart - wallEnd
            : wallEnd - wallStart;
        fallbackDirection.y = 0f;

        Vector3 direction = desiredLength > 0.0001f
            ? desiredOffset / desiredLength
            : (fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector3.right);

        constrainedPoint = anchorPoint + direction * minimumRequiredLength;
        constrainedPoint.y = desiredPoint.y;
        return true;
    }

    private static float MoveOuterSplitDistanceOutsideOpeningSpan(
        float desiredDistance,
        System.Collections.Generic.IReadOnlyList<WallOpening> openings,
        float clearance)
    {
        if (openings == null)
        {
            return desiredDistance;
        }

        for (int i = 0; i < openings.Count; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            float openingStart = opening.CenterDistance - opening.Width * 0.5f - clearance;
            float openingEnd = opening.CenterDistance + opening.Width * 0.5f + clearance;
            if (desiredDistance < openingStart || desiredDistance > openingEnd)
            {
                continue;
            }

            return desiredDistance < opening.CenterDistance ? openingStart : openingEnd;
        }

        return desiredDistance;
    }

}
