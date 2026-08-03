using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class WallOpeningPlacementManager
{
    public void SplitSelectedWall()
    {
        if (!CanEditOpenings())
        {
            return;
        }

        Wall selectedWallComponent = GetSelectedWallComponent();
        if (selectedWallComponent == null || wallRoot == null)
        {
            return;
        }

        WallOpeningContainer selectedContainer = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (selectedContainer != null)
        {
            SplitContainerWallSegment(selectedContainer, selectedWallComponent);
            return;
        }

        Vector3 startPoint = selectedWallComponent.Data.startPoint;
        Vector3 endPoint = selectedWallComponent.Data.endPoint;
        Vector3 midpoint = (startPoint + endPoint) * 0.5f;
        midpoint.y = startPoint.y;

        if (!CanSplitStandaloneWallAtMidpoint(startPoint, midpoint, endPoint))
        {
            return;
        }

        EnsureWallRoot();

        WallVisualState visualState = WallVisualState.Capture(selectedWallComponent.gameObject);
        float thickness = selectedWallComponent.transform.localScale.x;
        float height = selectedWallComponent.transform.localScale.y;
        float centerY = selectedWallComponent.transform.position.y;

        GameObject firstWall = CreateStandaloneWallSegment(
            $"{selectedWallComponent.name}_A",
            startPoint,
            midpoint,
            thickness,
            height,
            centerY,
            selectedWallComponent.StartVertexId,
            0,
            selectedWallComponent.SuppressStartHandle,
            false,
            selectedWallComponent.IsStartSplitPoint,
            true,
            visualState);

        if (firstWall == null)
        {
            return;
        }

        GameObject secondWall = CreateStandaloneWallSegment(
            $"{selectedWallComponent.name}_B",
            midpoint,
            endPoint,
            thickness,
            height,
            centerY,
            0,
            selectedWallComponent.EndVertexId,
            false,
            selectedWallComponent.SuppressEndHandle,
            true,
            selectedWallComponent.IsEndSplitPoint,
            visualState);

        if (secondWall == null)
        {
            DestroyCreatedStandaloneSplitWall(firstWall);
            return;
        }

        List<Wall> createdWalls = CollectCreatedStandaloneSplitWalls(firstWall, secondWall);

        RecordStandaloneSplitReplacement(selectedWallComponent, firstWall, secondWall);

        Wall removedWall = selectedWallComponent;
        RemoveStandaloneSplitSourceWall(selectedWallComponent);

        RequestStandaloneSplitTopologyRefresh(removedWall, createdWalls);

        StartCoroutine(RefreshWallRegistryAfterSplit(false));

        SelectStandaloneSplitFirstWall(firstWall);
    }

    private IEnumerator RefreshWallRegistryAfterSplit(bool isDragging)
    {
        if (isDragging) yield return null; // Only defer if dragging, otherwise refresh immediately
        handleManager?.RefreshRegisteredWalls();
    }

    private Wall ResolveDefaultSplitSegment(WallOpeningContainer container)
    {
        if (container == null)
        {
            return null;
        }

        Wall[] walls = container.GetComponentsInChildren<Wall>(true);
        Wall bestWall = null;
        float bestLength = 0f;
        for (int i = 0; i < walls.Length; i++)
        {
            Wall wall = walls[i];
            if (!CanUseWallAsDefaultSplitSegment(container, wall, out float length))
            {
                continue;
            }

            if (length <= bestLength)
            {
                continue;
            }

            bestLength = length;
            bestWall = wall;
        }

        return bestWall;
    }

    private static bool CanUseWallAsDefaultSplitSegment(WallOpeningContainer container, Wall wall, out float length)
    {
        length = 0f;
        if (container == null || wall == null || WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
        {
            return false;
        }

        Vector3 startPoint = wall.Data.startPoint;
        Vector3 endPoint = wall.Data.endPoint;
        length = Vector3.Distance(startPoint, endPoint);
        if (length <= MinimumWallSegmentLength * 2f)
        {
            return false;
        }

        Vector3 midpoint = (startPoint + endPoint) * 0.5f;
        float splitDistance = Vector3.Dot(midpoint - container.WallStart, container.WallDirection);
        return splitDistance > MinimumWallSegmentLength &&
            splitDistance < container.WallLength - MinimumWallSegmentLength;
    }

    private static bool CanSplitStandaloneWallAtMidpoint(Vector3 startPoint, Vector3 midpoint, Vector3 endPoint)
    {
        return (midpoint - startPoint).sqrMagnitude > MinimumWallSegmentLength * MinimumWallSegmentLength &&
            (endPoint - midpoint).sqrMagnitude > MinimumWallSegmentLength * MinimumWallSegmentLength;
    }

    private void SelectStandaloneSplitFirstWall(GameObject firstWall)
    {
        if (wallSelectionManager != null)
        {
            wallSelectionManager.SetSelectedWall(firstWall);
        }
    }

    private static void RequestStandaloneSplitTopologyRefresh(Wall removedWall, List<Wall> createdWalls)
    {
        if (createdWalls.Count > 0)
        {
            RoomTopologyEvents.RequestRefreshForWallReplacement(new[] { removedWall }, createdWalls);
        }
    }

    private void RemoveStandaloneSplitSourceWall(Wall selectedWallComponent)
    {
        selectedWallComponent.ClearLengthDisplay(wallLengthDisplay);
        Destroy(selectedWallComponent.gameObject);
    }

    private void RecordStandaloneSplitReplacement(Wall selectedWallComponent, GameObject firstWall, GameObject secondWall)
    {
        if (handleManager != null)
        {
            handleManager.UnregisterWall(selectedWallComponent.gameObject);
        }

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordWallSplit(selectedWallComponent.gameObject, firstWall, secondWall);
        }
    }

    private static List<Wall> CollectCreatedStandaloneSplitWalls(GameObject firstWall, GameObject secondWall)
    {
        Wall firstWallComponent = firstWall.GetComponent<Wall>();
        Wall secondWallComponent = secondWall.GetComponent<Wall>();
        List<Wall> createdWalls = new List<Wall>();
        if (firstWallComponent != null)
        {
            createdWalls.Add(firstWallComponent);
        }

        if (secondWallComponent != null)
        {
            createdWalls.Add(secondWallComponent);
        }

        return createdWalls;
    }

    private void DestroyCreatedStandaloneSplitWall(GameObject wallObject)
    {
        if (handleManager != null)
        {
            handleManager.UnregisterWall(wallObject);
        }

        Destroy(wallObject);
    }
}
