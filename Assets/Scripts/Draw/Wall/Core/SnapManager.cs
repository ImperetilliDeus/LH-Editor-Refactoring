using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class SnapManager : MonoBehaviour
{
    public enum SnapModifierKey
    {
        None,
        Shift,
        Ctrl,
        Alt,
    }

    public struct WallSnapSegment
    {
        public Vector3 start;
        public Vector3 end;
    }

    [SerializeField] private float gridSnapSize = 1f;
    [SerializeField] private bool enableGridSnapModifier = true;
    [SerializeField] private SnapModifierKey gridSnapModifier = SnapModifierKey.Alt;
    [SerializeField] private bool enableAxisSnapModifier = true;
    [SerializeField] private SnapModifierKey axisSnapModifier = SnapModifierKey.Shift;
    [SerializeField] private bool enableHandleSnap = true;
    [SerializeField] private bool enableHandleSnapModifier = true;
    [SerializeField] private SnapModifierKey handleSnapModifier = SnapModifierKey.Ctrl;
    [SerializeField] private float handleSnapDistance = 1f;
    [SerializeField] private bool useScreenPixelHandleSnap = false;
    [SerializeField] private float handleSnapPixelDistance = 16f;
    [SerializeField] private bool enableWallSegmentSnap = true;
    [SerializeField] private float wallSegmentSnapDistance = 10f;
    [SerializeField] private bool preferWallSegmentSnap = true;
    [SerializeField] private bool enableHandleDragGridSnapModifier = true;
    [SerializeField] private SnapModifierKey handleDragGridSnapModifier = SnapModifierKey.Shift;
    [SerializeField] private float handleDragGridSnapSize = 10f;

    private void OnValidate()
    {
        gridSnapSize = Mathf.Max(0.01f, gridSnapSize);
        handleSnapDistance = Mathf.Max(0.01f, handleSnapDistance);
        handleSnapPixelDistance = Mathf.Max(1f, handleSnapPixelDistance);
        wallSegmentSnapDistance = Mathf.Max(0.01f, wallSegmentSnapDistance);
        handleDragGridSnapSize = Mathf.Max(0.01f, handleDragGridSnapSize);
    }

    public Vector3 GetSnappedPoint(Vector3 rawPoint, Vector3 anchorPoint)
    {
        bool applyGridSnap = enableGridSnapModifier && IsModifierPressed(GetEffectiveGridSnapModifier());
        return GetSnappedPoint(rawPoint, anchorPoint, applyGridSnap);
    }

    public Vector3 GetSnappedHandleDragPoint(Vector3 rawPoint)
    {
        if (!enableHandleDragGridSnapModifier || !IsModifierPressed(handleDragGridSnapModifier))
        {
            return rawPoint;
        }

        return ApplyGridSnap(rawPoint, handleDragGridSnapSize);
    }

    public Vector3 GetSnappedHandleDragPoint(
        Vector3 rawPoint,
        Vector3 anchorPoint,
        List<Vector3> handlePoints,
        Camera camera,
        List<WallSnapSegment> wallSegments,
        out bool snappedByWallSegment,
        out bool snappedByHandlePoint)
    {
        snappedByWallSegment = false;
        snappedByHandlePoint = false;

        if (enableHandleDragGridSnapModifier && IsModifierPressed(handleDragGridSnapModifier))
        {
            return ApplyGridSnap(rawPoint, handleDragGridSnapSize);
        }

        if (!ShouldApplyHandleSnap())
        {
            return GetSnappedPoint(rawPoint, anchorPoint, false);
        }

        return GetSnappedHandlePoint(rawPoint, anchorPoint, handlePoints, camera, wallSegments, out snappedByWallSegment, out snappedByHandlePoint);
    }

    public Vector3 GetSnappedWallDrawPoint(
        Vector3 rawPoint,
        Vector3 anchorPoint,
        List<Vector3> handlePoints,
        Camera camera,
        List<WallSnapSegment> wallSegments,
        out bool snappedByWallSegment,
        out bool snappedByHandlePoint)
    {
        bool forceWallDrawGridSnap = enableGridSnapModifier && IsModifierPressed(GetEffectiveGridSnapModifier());
        bool applyWallDrawGridSnap = forceWallDrawGridSnap;

        Vector3 baseSnappedPoint = GetSnappedPoint(rawPoint, anchorPoint, applyWallDrawGridSnap);
        snappedByWallSegment = false;
        snappedByHandlePoint = false;

        if (forceWallDrawGridSnap)
        {
            return baseSnappedPoint;
        }

        bool canPointSnap = enableHandleSnap && handlePoints != null && handlePoints.Count > 0;
        bool canSegmentSnap = enableWallSegmentSnap && wallSegments != null && wallSegments.Count > 0;
        if (!canPointSnap && !canSegmentSnap)
        {
            return baseSnappedPoint;
        }

        float pointMaxDistanceSqr = handleSnapDistance * handleSnapDistance;
        float segmentMaxDistanceSqr = wallSegmentSnapDistance * wallSegmentSnapDistance;

        Vector3 pointSnap = Vector3.zero;
        Vector3 segmentSnapPoint = Vector3.zero;
        bool hasPointSnap = canPointSnap && TryGetClosestPointSnap(baseSnappedPoint, handlePoints, pointMaxDistanceSqr, camera, out pointSnap);
        bool hasSegmentSnap = canSegmentSnap && TryGetClosestSegmentSnap(baseSnappedPoint, wallSegments, segmentMaxDistanceSqr, out segmentSnapPoint);

        if (!hasPointSnap && !hasSegmentSnap)
        {
            return baseSnappedPoint;
        }

        Vector3 chosenSnap = hasPointSnap ? pointSnap : segmentSnapPoint;
        bool choseSegmentSnap = !hasPointSnap && hasSegmentSnap;
        if (hasPointSnap && hasSegmentSnap)
        {
            if (preferWallSegmentSnap)
            {
                choseSegmentSnap = true;
            }
            else
            {
                float pointMetric = GetSnapDistanceMetric(baseSnappedPoint, pointSnap, camera);
                float segmentMetric = GetSnapDistanceMetric(baseSnappedPoint, segmentSnapPoint, camera);
                choseSegmentSnap = segmentMetric < pointMetric;
            }

            chosenSnap = choseSegmentSnap ? segmentSnapPoint : pointSnap;
        }

        snappedByWallSegment = choseSegmentSnap;
        snappedByHandlePoint = hasPointSnap && !choseSegmentSnap;
        return chosenSnap;
    }

    public Vector3 GetSnappedHandlePoint(Vector3 rawPoint, Vector3 anchorPoint, List<Vector3> handlePoints)
    {
        return GetSnappedHandlePoint(rawPoint, anchorPoint, handlePoints, null, null, out _, out _);
    }

    public Vector3 GetSnappedHandlePoint(Vector3 rawPoint, Vector3 anchorPoint, List<Vector3> handlePoints, Camera camera)
    {
        return GetSnappedHandlePoint(rawPoint, anchorPoint, handlePoints, camera, null, out _, out _);
    }

    public Vector3 GetSnappedHandlePoint(Vector3 rawPoint, Vector3 anchorPoint, List<Vector3> handlePoints, Camera camera, List<WallSnapSegment> wallSegments)
    {
        return GetSnappedHandlePoint(rawPoint, anchorPoint, handlePoints, camera, wallSegments, out _, out _);
    }

    public Vector3 GetSnappedHandlePoint(
        Vector3 rawPoint,
        Vector3 anchorPoint,
        List<Vector3> handlePoints,
        Camera camera,
        List<WallSnapSegment> wallSegments,
        out bool snappedByWallSegment)
    {
        return GetSnappedHandlePoint(rawPoint, anchorPoint, handlePoints, camera, wallSegments, out snappedByWallSegment, out _);
    }

    public Vector3 GetSnappedHandlePoint(
        Vector3 rawPoint,
        Vector3 anchorPoint,
        List<Vector3> handlePoints,
        Camera camera,
        List<WallSnapSegment> wallSegments,
        out bool snappedByWallSegment,
        out bool snappedByHandlePoint)
    {
        Vector3 baseSnappedPoint = GetSnappedPoint(rawPoint, anchorPoint);
        snappedByWallSegment = false;
        snappedByHandlePoint = false;

        if (!ShouldApplyHandleSnap())
        {
            return baseSnappedPoint;
        }

        bool canPointSnap = enableHandleSnap && handlePoints != null && handlePoints.Count > 0;
        bool canSegmentSnap = enableWallSegmentSnap && wallSegments != null && wallSegments.Count > 0;
        if (!canPointSnap && !canSegmentSnap)
        {
            return baseSnappedPoint;
        }

        float pointMaxDistanceSqr = handleSnapDistance * handleSnapDistance;
        float segmentMaxDistanceSqr = wallSegmentSnapDistance * wallSegmentSnapDistance;

        Vector3 pointSnap = Vector3.zero;
        Vector3 segmentSnapPoint = Vector3.zero;
        bool hasPointSnap = canPointSnap && TryGetClosestPointSnap(rawPoint, handlePoints, pointMaxDistanceSqr, camera, out pointSnap);
        bool hasSegmentSnap = canSegmentSnap && TryGetClosestSegmentSnap(rawPoint, wallSegments, segmentMaxDistanceSqr, out segmentSnapPoint);

        if (!hasPointSnap && !hasSegmentSnap)
        {
            return baseSnappedPoint;
        }

        Vector3 chosenSnap = hasPointSnap ? pointSnap : segmentSnapPoint;
        bool choseSegmentSnap = !hasPointSnap && hasSegmentSnap;
        if (hasPointSnap && hasSegmentSnap)
        {
            if (preferWallSegmentSnap)
            {
                choseSegmentSnap = true;
            }
            else
            {
                float pointMetric = GetSnapDistanceMetric(rawPoint, pointSnap, camera);
                float segmentMetric = GetSnapDistanceMetric(rawPoint, segmentSnapPoint, camera);
                choseSegmentSnap = segmentMetric < pointMetric;
            }

            chosenSnap = choseSegmentSnap ? segmentSnapPoint : pointSnap;
        }

        snappedByWallSegment = choseSegmentSnap;
        snappedByHandlePoint = hasPointSnap && !choseSegmentSnap;
        baseSnappedPoint.x = chosenSnap.x;
        baseSnappedPoint.z = chosenSnap.z;
        return baseSnappedPoint;
    }

    public bool TryGetClosestHandleSnapPoint(Vector3 rawPoint, List<Vector3> handlePoints, Camera camera, out Vector3 snapPoint)
    {
        snapPoint = Vector3.zero;

        if (!enableHandleSnap || handlePoints == null || handlePoints.Count == 0)
        {
            return false;
        }

        float maxDistanceSqr = handleSnapDistance * handleSnapDistance;
        return TryGetClosestPointSnap(rawPoint, handlePoints, maxDistanceSqr, camera, out snapPoint);
    }

    public bool TryGetClosestWallSegmentSnapPoint(Vector3 rawPoint, List<WallSnapSegment> wallSegments, out Vector3 snapPoint)
    {
        snapPoint = Vector3.zero;

        if (!enableWallSegmentSnap || wallSegments == null || wallSegments.Count == 0)
        {
            return false;
        }

        float maxDistanceSqr = wallSegmentSnapDistance * wallSegmentSnapDistance;
        return TryGetClosestSegmentSnap(rawPoint, wallSegments, maxDistanceSqr, out snapPoint);
    }

    private bool ShouldApplyHandleSnap()
    {
        return !enableHandleSnapModifier || IsModifierPressed(handleSnapModifier);
    }

    private bool TryGetClosestPointSnap(Vector3 currentPoint, List<Vector3> handlePoints, float maxDistanceSqr, Camera camera, out Vector3 snapPoint)
    {
        snapPoint = Vector3.zero;
        float closestDistanceSqr = useScreenPixelHandleSnap && camera != null
            ? handleSnapPixelDistance * handleSnapPixelDistance
            : maxDistanceSqr;

        bool found = false;
        Vector3 currentScreenPoint = Vector3.zero;
        bool useScreenDistance = useScreenPixelHandleSnap && camera != null;
        if (useScreenDistance)
        {
            currentScreenPoint = camera.WorldToScreenPoint(currentPoint);
            if (currentScreenPoint.z <= 0f)
            {
                useScreenDistance = false;
                closestDistanceSqr = maxDistanceSqr;
            }
        }

        for (int i = 0; i < handlePoints.Count; i++)
        {
            Vector3 candidate = handlePoints[i];
            float distanceSqr;

            if (useScreenDistance)
            {
                Vector3 candidateScreenPoint = camera.WorldToScreenPoint(candidate);
                if (candidateScreenPoint.z <= 0f)
                {
                    continue;
                }

                float dx = currentScreenPoint.x - candidateScreenPoint.x;
                float dy = currentScreenPoint.y - candidateScreenPoint.y;
                distanceSqr = dx * dx + dy * dy;
            }
            else
            {
                float dx = currentPoint.x - candidate.x;
                float dz = currentPoint.z - candidate.z;
                distanceSqr = dx * dx + dz * dz;
            }

            if (distanceSqr > closestDistanceSqr)
            {
                continue;
            }

            closestDistanceSqr = distanceSqr;
            snapPoint = candidate;
            found = true;
        }

        return found;
    }

    private bool TryGetClosestSegmentSnap(Vector3 currentPoint, List<WallSnapSegment> segments, float maxDistanceSqr, out Vector3 snapPoint)
    {
        snapPoint = Vector3.zero;
        float closestDistanceSqr = maxDistanceSqr;

        bool found = false;

        for (int i = 0; i < segments.Count; i++)
        {
            WallSnapSegment segment = segments[i];
            Vector3 candidate = GetClosestPointOnSegmentXZ(currentPoint, segment.start, segment.end);

            float dx = currentPoint.x - candidate.x;
            float dz = currentPoint.z - candidate.z;
            float distanceSqr = dx * dx + dz * dz;

            if (distanceSqr > closestDistanceSqr)
            {
                continue;
            }

            closestDistanceSqr = distanceSqr;
            snapPoint = candidate;
            found = true;
        }

        return found;
    }

    private float GetSnapDistanceMetric(Vector3 currentPoint, Vector3 candidatePoint, Camera camera)
    {
        if (useScreenPixelHandleSnap && camera != null)
        {
            Vector3 currentScreen = camera.WorldToScreenPoint(currentPoint);
            Vector3 candidateScreen = camera.WorldToScreenPoint(candidatePoint);
            if (currentScreen.z > 0f && candidateScreen.z > 0f)
            {
                float dx = currentScreen.x - candidateScreen.x;
                float dy = currentScreen.y - candidateScreen.y;
                return dx * dx + dy * dy;
            }
        }

        float worldDx = currentPoint.x - candidatePoint.x;
        float worldDz = currentPoint.z - candidatePoint.z;
        return worldDx * worldDx + worldDz * worldDz;
    }

    private Vector3 GetClosestPointOnSegmentXZ(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 a = new Vector2(segmentStart.x, segmentStart.z);
        Vector2 b = new Vector2(segmentEnd.x, segmentEnd.z);

        Vector2 ab = b - a;
        float abSqrMagnitude = ab.sqrMagnitude;
        if (abSqrMagnitude <= 0.0000001f)
        {
            return new Vector3(segmentStart.x, point.y, segmentStart.z);
        }

        float t = Vector2.Dot(p - a, ab) / abSqrMagnitude;
        t = Mathf.Clamp01(t);
        Vector2 projected = a + ab * t;
        return new Vector3(projected.x, point.y, projected.y);
    }

    private Vector3 ApplyGridSnap(Vector3 point, float snapSize)
    {
        point.x = Mathf.Round(point.x / snapSize) * snapSize;
        point.z = Mathf.Round(point.z / snapSize) * snapSize;
        return point;
    }

    private SnapModifierKey GetEffectiveGridSnapModifier()
    {
        return gridSnapModifier == SnapModifierKey.None
            ? SnapModifierKey.Alt
            : gridSnapModifier;
    }

    private Vector3 GetSnappedPoint(Vector3 rawPoint, Vector3 anchorPoint, bool applyGridSnap)
    {
        Vector3 snappedPoint = rawPoint;
        bool isAxisSnapActive = enableAxisSnapModifier && IsModifierPressed(axisSnapModifier);

        if (isAxisSnapActive)
        {
            Vector3 delta = rawPoint - anchorPoint;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
            {
                snappedPoint.z = anchorPoint.z;
            }
            else
            {
                snappedPoint.x = anchorPoint.x;
            }
        }

        if (!applyGridSnap)
        {
            return snappedPoint;
        }

        return ApplyGridSnap(snappedPoint, gridSnapSize);
    }

    private bool IsModifierPressed(SnapModifierKey modifierKey)
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        switch (modifierKey)
        {
            case SnapModifierKey.None:
                return false;
            case SnapModifierKey.Shift:
                return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            case SnapModifierKey.Ctrl:
                return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            case SnapModifierKey.Alt:
                return Keyboard.current.leftAltKey.isPressed || Keyboard.current.rightAltKey.isPressed;
            default:
                return false;
        }
    }
}
