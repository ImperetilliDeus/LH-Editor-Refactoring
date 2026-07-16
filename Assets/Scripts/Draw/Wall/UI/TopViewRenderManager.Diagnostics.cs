using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class TopViewRenderManager
{
    private const int PointerHitDiagnosticsMaxUiHits = 12;
    private const int PointerHitDiagnosticsMaxWallCandidates = 8;

    private void LogRoomWallAuthoringPointerHitDiagnosticsIfNeeded()
    {
        if (!logRoomWallAuthoringHitDiagnostics ||
            !IsRoomWallAuthoringInteractionEnabled() ||
            Mouse.current == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 pointerScreenPosition = EditorScreenCoordinateUtility.NormalizePointerScreenPosition(Mouse.current.position.ReadValue());
        StringBuilder builder = new StringBuilder(2048);
        builder.Append("[RoomWallHit] pointer down");
        builder.Append(" screen=");
        builder.Append(pointerScreenPosition);
        builder.Append(" mode=");
        builder.Append(modeManager != null ? modeManager.CurrentMode.ToString() : "<null>");
        builder.Append(" wallBatchRaycast=");
        builder.Append(wallBatchGraphic != null && wallBatchGraphic.raycastTarget);
        builder.Append(" roomAuthoring=");
        builder.Append(IsRoomWallAuthoringInteractionEnabled());
        builder.AppendLine();

        AppendUiRaycastDiagnostics(pointerScreenPosition, builder);
        AppendTopPlanWallCandidateDiagnostics(pointerScreenPosition, builder);
        Debug.Log(builder.ToString(), this);
    }

    private void LogTopPlanWallSegmentClick(TopPlanSegmentBatchGraphic.SegmentData segment)
    {
        if (!logRoomWallAuthoringHitDiagnostics)
        {
            return;
        }

        Wall wall = segment.wall;
        Debug.Log(
            "[RoomWallHit] TopPlan wall segment click event " +
            $"wall={FormatWall(wall)} " +
            $"roomAuthoring={IsRoomWallAuthoringInteractionEnabled()} " +
            $"selectedForRoom={(wall != null && roomWallAuthoringPanelController != null && roomWallAuthoringPanelController.IsWallSelectedForAuthoring(wall))}",
            this);
    }

    private void AppendUiRaycastDiagnostics(Vector2 pointerScreenPosition, StringBuilder builder)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            builder.AppendLine("  ui: EventSystem=null");
            return;
        }

        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            position = pointerScreenPosition,
        };

        pointerHitDiagnosticsResults.Clear();
        eventSystem.RaycastAll(eventData, pointerHitDiagnosticsResults);
        builder.Append("  ui hits=");
        builder.Append(pointerHitDiagnosticsResults.Count);
        builder.AppendLine();

        int count = Mathf.Min(pointerHitDiagnosticsResults.Count, PointerHitDiagnosticsMaxUiHits);
        for (int i = 0; i < count; i++)
        {
            RaycastResult result = pointerHitDiagnosticsResults[i];
            GameObject hitObject = result.gameObject;
            builder.Append("    #");
            builder.Append(i);
            builder.Append(' ');
            builder.Append(FormatObjectPath(hitObject));
            builder.Append(" layer=");
            builder.Append(hitObject != null ? LayerMask.LayerToName(hitObject.layer) : "<null>");
            builder.Append(" graphic=");
            builder.Append(FormatGraphic(hitObject));
            builder.Append(" distance=");
            builder.Append(result.distance.ToString("0.###"));
            builder.Append(" depth=");
            builder.Append(result.depth);
            builder.Append(" sorting=");
            builder.Append(result.sortingLayer);
            builder.Append('/');
            builder.Append(result.sortingOrder);
            builder.AppendLine();
        }
    }

    private void AppendTopPlanWallCandidateDiagnostics(Vector2 pointerScreenPosition, StringBuilder builder)
    {
        if (contentRoot == null)
        {
            builder.AppendLine("  top-plan: contentRoot=null");
            return;
        }

        Camera uiCamera = targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas != null ? targetCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, pointerScreenPosition, uiCamera, out Vector2 localPoint))
        {
            builder.AppendLine("  top-plan: pointer outside contentRoot");
            return;
        }

        builder.Append("  top-plan local=");
        builder.Append(localPoint);
        builder.Append(" wallSegments=");
        builder.Append(cachedWallSegments.Count);
        builder.AppendLine();

        int emitted = 0;
        for (int i = 0; i < cachedWallSegments.Count && emitted < PointerHitDiagnosticsMaxWallCandidates; i++)
        {
            TopPlanSegmentBatchGraphic.SegmentData segment = cachedWallSegments[i];
            if (segment.wall == null)
            {
                continue;
            }

            float distance = Mathf.Sqrt(DistanceToSegmentSqr(localPoint, segment.start, segment.end));
            float hitDistance = Mathf.Max(6f, segment.thickness * 0.75f);
            if (distance > hitDistance * 3f)
            {
                continue;
            }

            builder.Append("    wall ");
            builder.Append(FormatWall(segment.wall));
            builder.Append(" dist=");
            builder.Append(distance.ToString("0.##"));
            builder.Append(" hitLimit=");
            builder.Append(hitDistance.ToString("0.##"));
            builder.Append(" clickable=");
            builder.Append(distance <= hitDistance);
            builder.Append(" selectedForRoom=");
            builder.Append(roomWallAuthoringPanelController != null && roomWallAuthoringPanelController.IsWallSelectedForAuthoring(segment.wall));
            builder.AppendLine();
            emitted++;
        }

        if (emitted == 0)
        {
            builder.AppendLine("    no nearby wall segment candidates");
        }
    }

    private static float DistanceToSegmentSqr(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        float lengthSqr = delta.sqrMagnitude;
        if (lengthSqr <= Mathf.Epsilon)
        {
            return (point - start).sqrMagnitude;
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - start, delta) / lengthSqr);
        Vector2 projection = start + delta * t;
        return (point - projection).sqrMagnitude;
    }

    private static string FormatWall(Wall wall)
    {
        if (wall == null)
        {
            return "<null>";
        }

        string id = wall.Data != null ? wall.Data.id : null;
        return string.IsNullOrWhiteSpace(id) ? wall.name : $"{wall.name}({id})";
    }

    private static string FormatGraphic(GameObject hitObject)
    {
        if (hitObject == null)
        {
            return "<null>";
        }

        Graphic graphic = hitObject.GetComponent<Graphic>();
        if (graphic == null)
        {
            return "<none>";
        }

        return $"{graphic.GetType().Name},raycast={graphic.raycastTarget}";
    }

    private static string FormatObjectPath(GameObject hitObject)
    {
        if (hitObject == null)
        {
            return "<null>";
        }

        Transform current = hitObject.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }
}
