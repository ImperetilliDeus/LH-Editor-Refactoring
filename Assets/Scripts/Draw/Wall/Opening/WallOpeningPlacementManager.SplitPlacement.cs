using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public partial class WallOpeningPlacementManager
{
    private readonly List<RaycastResult> splitPlacementUiRaycastResults = new List<RaycastResult>();
    private bool splitPointPlacementMode;
    private bool splitPointPlacementAwaitingPointerRelease;

    private void HandleSplitPointPlacementInput(EditorInputFrame inputFrame)
    {
        if (!CanEditOpenings())
        {
            CancelSplitPointPlacementMode();
            return;
        }

        if (inputFrame.EscapePressedThisFrame || inputFrame.RightPressedThisFrame)
        {
            CancelSplitPointPlacementMode();
            return;
        }

        if (splitPointPlacementAwaitingPointerRelease)
        {
            if (!inputFrame.LeftPressed)
            {
                splitPointPlacementAwaitingPointerRelease = false;
            }

            return;
        }

        if (!inputFrame.IsPointerAvailable || !inputFrame.LeftPressedThisFrame)
        {
            return;
        }

        if (IsSplitPlacementBlockedByNonWallUI(inputFrame.PointerScreenPosition))
        {
            return;
        }

        if (!TryGetSplitPlacementWallHit(inputFrame.PointerScreenPosition, out Wall wall, out Vector3 hitPoint))
        {
            return;
        }

        SplitWallAtPoint(wall, hitPoint);
        CancelSplitPointPlacementMode();
    }

    private bool IsSplitPlacementBlockedByNonWallUI(Vector2 pointerScreenPosition)
    {
        if (wallSelectionManager != null)
        {
            return wallSelectionManager.IsPointerBlockedByNonWallUI();
        }

        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = pointerScreenPosition,
        };

        splitPlacementUiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, splitPlacementUiRaycastResults);
        for (int i = 0; i < splitPlacementUiRaycastResults.Count; i++)
        {
            GameObject hitObject = splitPlacementUiRaycastResults[i].gameObject;
            if (hitObject != null && !LayerUtility.IsLayer(hitObject, LayerUtility.WallUILayerName))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryGetSplitPlacementWallHit(Vector2 pointerScreenPosition, out Wall wall, out Vector3 hitPoint)
    {
        wall = null;
        hitPoint = Vector3.zero;
        if (mainCamera == null)
        {
            return false;
        }

        Ray ray = EditorScreenCoordinateUtility.ScreenPointToRay(mainCamera, pointerScreenPosition);
        int wallMask = LayerUtility.GetMaskOrDefault(LayerUtility.WallLayerName);
        if (!Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, wallMask))
        {
            return false;
        }

        GameObject hitObject = hitInfo.collider != null ? hitInfo.collider.gameObject : null;
        if (hitObject == null)
        {
            return false;
        }

        Wall hitWall = hitObject.GetComponentInParent<Wall>();
        if (hitWall != null && hitWall.gameObject.activeInHierarchy)
        {
            wall = hitWall;
            hitPoint = hitInfo.point;
            return true;
        }

        WallOpeningContainer container = hitObject.GetComponentInParent<WallOpeningContainer>();
        if (TryFindClosestContainerWall(container, hitInfo.point, out hitWall))
        {
            wall = hitWall;
            hitPoint = hitInfo.point;
            return true;
        }

        return false;
    }

    private static bool TryFindClosestContainerWall(WallOpeningContainer container, Vector3 worldPoint, out Wall wall)
    {
        wall = null;
        if (container == null)
        {
            return false;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        float bestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < walls.Length; i++)
        {
            Wall candidate = walls[i];
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy ||
                WallHierarchyUtility.IsHiddenOpeningBaseSegment(candidate))
            {
                continue;
            }

            float distanceSqr = DistancePointToSegmentSqrXZ(worldPoint, candidate.Data.startPoint, candidate.Data.endPoint);
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            wall = candidate;
        }

        return wall != null;
    }

    private static float DistancePointToSegmentSqrXZ(Vector3 point, Vector3 startPoint, Vector3 endPoint)
    {
        Vector3 direction = endPoint - startPoint;
        direction.y = 0f;
        float lengthSqr = direction.sqrMagnitude;
        if (lengthSqr <= 0.000001f)
        {
            Vector3 offset = point - startPoint;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }

        Vector3 pointOffset = point - startPoint;
        pointOffset.y = 0f;
        float t = Mathf.Clamp01(Vector3.Dot(pointOffset, direction) / lengthSqr);
        Vector3 closestPoint = startPoint + direction * t;
        Vector3 closestOffset = point - closestPoint;
        closestOffset.y = 0f;
        return closestOffset.sqrMagnitude;
    }
}
