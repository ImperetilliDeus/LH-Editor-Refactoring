using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionQueryService
{
    private readonly HashSet<GameObject> wallsInSelectionBounds = new HashSet<GameObject>();
    private readonly HashSet<WallOpeningContainer> processedContainers = new HashSet<WallOpeningContainer>();

    public bool ShouldDisplaySelectionProxy(Wall wall)
    {
        if (wall == null)
        {
            return false;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        return container == null || GetRepresentativeWallForContainer(container) == wall;
    }

    public Wall GetRepresentativeWallForContainer(WallOpeningContainer container)
    {
        if (container == null)
        {
            return null;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        Wall representative = null;
        float bestLengthSqr = float.MinValue;

        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (wall == null || WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                continue;
            }

            float lengthSqr = (wall.Data.endPoint - wall.Data.startPoint).sqrMagnitude;
            if (lengthSqr <= bestLengthSqr)
            {
                continue;
            }

            bestLengthSqr = lengthSqr;
            representative = wall;
        }

        return representative;
    }

    public bool IsWallOrContainerSelected(Wall wall, WallSelectionState selectionState)
    {
        if (wall == null || selectionState == null)
        {
            return false;
        }

        if (selectionState.IsSelected(wall.gameObject))
        {
            return true;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        if (selectionState.SelectedWall != null && selectionState.SelectedWall.transform.IsChildOf(container.transform))
        {
            return true;
        }

        foreach (GameObject detailWall in selectionState.DetailSelectedWalls)
        {
            if (detailWall != null && detailWall.transform.IsChildOf(container.transform))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyCollection<GameObject> CollectWallsInSelectionBounds(
        BoxCollider multiSelectBoxCollider,
        Transform wallRoot,
        List<Wall> rootWalls)
    {
        wallsInSelectionBounds.Clear();
        processedContainers.Clear();

        if (multiSelectBoxCollider == null || wallRoot == null || rootWalls == null)
        {
            return wallsInSelectionBounds;
        }

        Bounds bounds = multiSelectBoxCollider.bounds;
        for (int i = 0; i < rootWalls.Count; i++)
        {
            Wall wall = rootWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
            {
                continue;
            }

            WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
            if (container != null)
            {
                if (!processedContainers.Add(container))
                {
                    continue;
                }

                if (TryGetSelectableWallFromContainerInBounds(container, bounds, out GameObject representativeWall))
                {
                    wallsInSelectionBounds.Add(representativeWall);
                }

                continue;
            }

            if (ContainsPointXZ(bounds, wall.Data.startPoint) && ContainsPointXZ(bounds, wall.Data.endPoint))
            {
                wallsInSelectionBounds.Add(wall.gameObject);
            }
        }

        return wallsInSelectionBounds;
    }

    public bool TryGetWallFromMouseRay(
        Camera mainCamera,
        Transform wallRoot,
        Vector2 pointerScreenPosition,
        out GameObject wall)
    {
        wall = null;
        if (mainCamera == null)
        {
            return false;
        }

        Ray ray = mainCamera.ScreenPointToRay(pointerScreenPosition);
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

        GameObject wallObject = ResolveWallObject(hitObject, wallRoot);
        if (wallObject == null)
        {
            return false;
        }

        wall = wallObject;
        return true;
    }

    public bool IsWallObject(GameObject candidate, Transform wallRoot)
    {
        if (candidate == null)
        {
            return false;
        }

        if (LayerUtility.TryGetLayer(LayerUtility.WallLayerName, out int wallLayer) &&
            candidate.layer != wallLayer)
        {
            return false;
        }

        if (wallRoot == null)
        {
            return true;
        }

        return candidate.transform.IsChildOf(wallRoot);
    }

    public GameObject ResolveWallObject(GameObject candidate, Transform wallRoot)
    {
        if (candidate == null)
        {
            return null;
        }

        Wall wall = candidate.GetComponentInParent<Wall>();
        if (wall != null && IsWallObject(wall.gameObject, wallRoot))
        {
            return wall.gameObject;
        }

        return IsWallObject(candidate, wallRoot) ? candidate : null;
    }

    private bool TryGetSelectableWallFromContainerInBounds(WallOpeningContainer container, Bounds bounds, out GameObject representativeWall)
    {
        representativeWall = null;
        if (container == null)
        {
            return false;
        }

        Wall[] containerWalls = container.GetComponentsInChildren<Wall>(true);
        Wall bestWall = null;
        float bestLength = float.MinValue;

        for (int i = 0; i < containerWalls.Length; i++)
        {
            Wall wall = containerWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy || WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                continue;
            }

            if (!ContainsPointXZ(bounds, wall.Data.startPoint) || !ContainsPointXZ(bounds, wall.Data.endPoint))
            {
                return false;
            }

            float length = (wall.Data.endPoint - wall.Data.startPoint).sqrMagnitude;
            if (length <= bestLength)
            {
                continue;
            }

            bestLength = length;
            bestWall = wall;
        }

        if (bestWall == null)
        {
            return false;
        }

        representativeWall = bestWall.gameObject;
        return true;
    }

    private static bool ContainsPointXZ(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x &&
               point.x <= bounds.max.x &&
               point.z >= bounds.min.z &&
               point.z <= bounds.max.z;
    }
}
