using System.Collections.Generic;
using UnityEngine;

public partial class WallOpeningPlacementManager
{
    private const string SegmentGroupName = "Segments";
    private const string SegmentObjectName = "Segment";

    private void RemoveLayout(UndoRedoManager.OpeningLayoutSnapshot snapshot)
    {
        if (wallRoot == null)
        {
            return;
        }

        if (snapshot.hasContainer)
        {
            RemoveContainerLayout(snapshot.layoutName);
            return;
        }

        RemoveStandaloneLayout(snapshot.wallSnapshot);
    }

    private void RemoveContainerLayout(string layoutName)
    {
        Transform container = wallRoot.Find(layoutName);
        if (container == null)
        {
            return;
        }

        ClearContainerWallRegistrations(container);

        DestroyContainerObject(container);
    }

    private void DestroyContainerObject(Transform container)
    {
        if (container == null)
        {
            return;
        }

        container.gameObject.SetActive(false);
        Destroy(container.gameObject);
    }

    private void ClearContainerWallRegistrations(Transform container)
    {
        WallHierarchyUtility.CollectWalls(container, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            if (cachedWalls[i] == null)
            {
                continue;
            }

            cachedWalls[i].ClearLengthDisplay(wallLengthDisplay);
            if (handleManager != null)
            {
                handleManager.UnregisterWall(cachedWalls[i].gameObject);
            }
        }
    }

    private void RemoveStandaloneLayout(UndoRedoManager.WallStateSnapshot wallSnapshot)
    {
        Wall wall = OpeningWallLookup.FindMatchingStandaloneWall(wallRoot, cachedWalls, wallSnapshot);
        if (wall == null)
        {
            return;
        }

        if (handleManager != null)
        {
            handleManager.UnregisterWall(wall.gameObject);
        }

        wall.ClearLengthDisplay(wallLengthDisplay);
        DestroyWallObject(wall);
    }

    private bool HasOtherOpenings(WallOpeningContainer container, WallOpening excludedOpening)
    {
        if (container == null)
        {
            return false;
        }

        WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening != null && opening != excludedOpening)
            {
                return true;
            }
        }

        return false;
    }

    private GameObject RestoreContainerIfEmpty(WallOpeningContainer container)
    {
        if (container == null)
        {
            return null;
        }

        if (!IsContainerEmpty(container))
        {
            return null;
        }

        UndoRedoManager.WallStateSnapshot wallSnapshot = OpeningContainerSnapshotBuilder.BuildWallSnapshot(container);
        RemoveLayout(CaptureLayoutSnapshot(container));
        GameObject restoredWall = RestoreWallFromSnapshot(wallSnapshot);

        RefreshAfterEmptyContainerRestore();
        return restoredWall;
    }

    private bool IsContainerEmpty(WallOpeningContainer container)
    {
        CollectOpenings(container, cachedOpenings);
        return cachedOpenings.Count == 0;
    }

    private void RefreshAfterEmptyContainerRestore()
    {
        RoomTopologyEvents.RequestRefreshAll();
        StartCoroutine(RefreshWallRegistryAfterSplit(false));

        MarkMarkerVisualsDirty();
    }

    private GameObject CreateRestoredWall(UndoRedoManager.WallStateSnapshot snapshot)
    {
        if (wallRoot == null)
        {
            return null;
        }

        GameObject wallObject = WallObjectFactory.CreateWallObject(
            snapshot.name,
            wallRoot,
            GetCubeMesh(),
            snapshot.visualState);
        if (!WallObjectFactory.ConfigureWall(
                wallObject,
                snapshot.wallData,
                snapshot.startVertexId,
                snapshot.endVertexId,
                snapshot.suppressStartHandle,
                snapshot.suppressEndHandle,
                snapshot.startSplitPoint,
                snapshot.endSplitPoint,
                MinimumWallSegmentLength,
                wallLengthDisplay,
                false))
        {
            Destroy(wallObject);
            return null;
        }
        return wallObject;
    }

    private Transform GetOrCreateOpeningContainer(Wall selectedWall)
    {
        if (selectedWall == null)
        {
            return null;
        }

        Transform existingParent = selectedWall.transform.parent;
        if (existingParent != null && existingParent.TryGetComponent(out WallOpeningContainer existingContainer))
        {
            return existingContainer.transform;
        }

        WallOpeningContainer container = CreateOpeningContainerFromWall(selectedWall);
        RemoveOriginalWallAfterContainerCreation(selectedWall);
        return container.transform;
    }

    private WallOpeningContainer CreateOpeningContainerFromWall(Wall selectedWall)
    {
        WallGeometryData geometry = CaptureGeometry(selectedWall);
        WallOpeningContainer container = OpeningContainerFactory.Create(
            selectedWall.name,
            wallRoot,
            geometry.wallStart,
            geometry.wallEnd,
            geometry.wallThickness,
            geometry.wallHeight,
            geometry.centerY,
            geometry.visualState,
            geometry.outerStartVertexId,
            geometry.outerEndVertexId,
            selectedWall.SuppressStartHandle,
            selectedWall.SuppressEndHandle,
            selectedWall.IsStartSplitPoint,
            selectedWall.IsEndSplitPoint,
            true);
        ApplyPersistentWallId(container, selectedWall);
        return container;
    }

    private void RemoveOriginalWallAfterContainerCreation(Wall selectedWall)
    {
        if (handleManager != null)
        {
            handleManager.UnregisterWall(selectedWall.gameObject);
        }

        pendingRoomRefreshRemovedWalls.Add(selectedWall);

        selectedWall.ClearLengthDisplay(wallLengthDisplay);
        DestroyWallObject(selectedWall);
    }

    private static void ApplyPersistentWallId(WallOpeningContainer container, Wall selectedWall)
    {
        if (container == null || selectedWall == null || selectedWall.Data == null)
        {
            return;
        }

        container.SetPersistentWallId(selectedWall.Data.id);
    }

    private void DestroyWallObject(Wall wall)
    {
        if (wall == null)
        {
            return;
        }

        wall.gameObject.SetActive(false);
        Destroy(wall.gameObject);
    }

    private void SplitContainerWallSegment(WallOpeningContainer container, Wall selectedSegment)
    {
        if (container == null || selectedSegment == null || wallRoot == null)
        {
            return;
        }

        if (!OpeningContainerSplitPlanner.TryGetSplitDistance(
                container,
                selectedSegment,
                MinimumWallSegmentLength,
                out float splitDistance))
        {
            return;
        }

        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));

        if (OpeningContainerSplitPlanner.SplitDistanceOverlapsOpeningSpan(cachedOpenings, splitDistance))
        {
            Debug.LogWarning("Wall split point overlaps an opening span.", selectedSegment);
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot sourceSnapshot = CaptureLayoutSnapshot(container);
        List<UndoRedoManager.OpeningStateSnapshot> leftOpenings = new List<UndoRedoManager.OpeningStateSnapshot>();
        List<UndoRedoManager.OpeningStateSnapshot> rightOpenings = new List<UndoRedoManager.OpeningStateSnapshot>();
        OpeningContainerSplitPlanner.PartitionOpeningsForSplit(
            cachedOpenings,
            splitDistance,
            leftOpenings,
            rightOpenings);

        RemoveLayout(sourceSnapshot);

        Vector3 splitPoint = container.WallStart + container.WallDirection * splitDistance;
        CreateSplitLayouts(container, sourceSnapshot, splitPoint, leftOpenings, rightOpenings);

        RoomTopologyEvents.RequestRefreshAll();
        StartCoroutine(RefreshWallRegistryAfterSplit(false));

        SelectClosestVisibleWallAfterContainerSplit(splitPoint);
    }

    private void SelectClosestVisibleWallAfterContainerSplit(Vector3 splitPoint)
    {
        if (wallSelectionManager == null)
        {
            return;
        }

        Wall replacementWall = OpeningWallLookup.FindClosestVisibleWallToPoint(wallRoot, cachedWalls, splitPoint);
        if (replacementWall != null)
        {
            wallSelectionManager.SetSelectedWall(replacementWall.gameObject);
        }
    }

    private void CreateSplitLayouts(
        WallOpeningContainer container,
        UndoRedoManager.OpeningLayoutSnapshot sourceSnapshot,
        Vector3 splitPoint,
        List<UndoRedoManager.OpeningStateSnapshot> leftOpenings,
        List<UndoRedoManager.OpeningStateSnapshot> rightOpenings)
    {
        CreateSplitContainerLayout(
            sourceSnapshot,
            container.name + "_A",
            container.WallStart,
            splitPoint,
            container.OuterStartVertexId,
            0,
            container.SuppressOuterStartHandle,
            false,
            container.OuterStartSplitPoint,
            true,
            leftOpenings);
        CreateSplitContainerLayout(
            sourceSnapshot,
            container.name + "_B",
            splitPoint,
            container.WallEnd,
            0,
            container.OuterEndVertexId,
            false,
            container.SuppressOuterEndHandle,
            true,
            container.OuterEndSplitPoint,
            rightOpenings);
    }

    private void CreateSplitContainerLayout(
        UndoRedoManager.OpeningLayoutSnapshot sourceSnapshot,
        string layoutName,
        Vector3 startPoint,
        Vector3 endPoint,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint,
        List<UndoRedoManager.OpeningStateSnapshot> openings)
    {
        float length = Vector3.Distance(startPoint, endPoint);
        if (length <= MinimumWallSegmentLength)
        {
            return;
        }

        if (openings == null || openings.Count == 0)
        {
            CreateStandaloneSplitWallLayout(
                layoutName,
                sourceSnapshot,
                startPoint,
                endPoint,
                startVertexId,
                endVertexId,
                suppressStartHandle,
                suppressEndHandle,
                startSplitPoint,
                endSplitPoint);
            return;
        }

        CreatePopulatedSplitOpeningContainer(
            layoutName,
            sourceSnapshot,
            startPoint,
            endPoint,
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            startSplitPoint,
            endSplitPoint,
            openings);
    }

    private void CreatePopulatedSplitOpeningContainer(
        string layoutName,
        UndoRedoManager.OpeningLayoutSnapshot sourceSnapshot,
        Vector3 startPoint,
        Vector3 endPoint,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint,
        List<UndoRedoManager.OpeningStateSnapshot> openings)
    {
        WallOpeningContainer nextContainer = CreateSplitOpeningContainer(
            layoutName,
            sourceSnapshot,
            startPoint,
            endPoint,
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            startSplitPoint,
            endSplitPoint);

        for (int i = 0; i < openings.Count; i++)
        {
            CreateOpeningFromSnapshot(nextContainer, openings[i]);
        }

        RebuildContainer(nextContainer, false);
    }

    private void CreateStandaloneSplitWallLayout(
        string layoutName,
        UndoRedoManager.OpeningLayoutSnapshot sourceSnapshot,
        Vector3 startPoint,
        Vector3 endPoint,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint)
    {
        CreateStandaloneWallSegment(
            layoutName,
            startPoint,
            endPoint,
            sourceSnapshot.wallThickness,
            sourceSnapshot.wallHeight,
            sourceSnapshot.centerY,
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            startSplitPoint,
            endSplitPoint,
            sourceSnapshot.visualState);
    }

    private GameObject CreateStandaloneWallSegment(
        string segmentName,
        Vector3 startPoint,
        Vector3 endPoint,
        float thickness,
        float height,
        float centerY,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint,
        WallVisualState visualState,
        bool isDragging = false)
    {
        return geometryFactory.CreateStandaloneWallSegment(
            wallRoot,
            GetCubeMesh(),
            wallLengthDisplay,
            handleManager,
            Destroy,
            segmentName,
            startPoint,
            endPoint,
            thickness,
            height,
            centerY,
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            startSplitPoint,
            endSplitPoint,
            visualState, // No change here
            MinimumWallSegmentLength,
            isDragging); // Pass isDragging
    }

    private WallOpeningContainer CreateSplitOpeningContainer(
        string layoutName,
        UndoRedoManager.OpeningLayoutSnapshot sourceSnapshot,
        Vector3 startPoint,
        Vector3 endPoint,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint)
    {
        return OpeningContainerFactory.Create(
            layoutName,
            wallRoot,
            startPoint,
            endPoint,
            sourceSnapshot.wallThickness,
            sourceSnapshot.wallHeight,
            sourceSnapshot.centerY,
            sourceSnapshot.visualState,
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            startSplitPoint,
            endSplitPoint,
            true);
    }

    private WallGeometryData CaptureGeometry(Wall wall)
    {
        Vector3 wallStart = wall.Data.startPoint;
        Vector3 wallEnd = wall.Data.endPoint;
        Vector3 wallDirection = wallEnd - wallStart;
        wallDirection.y = 0f;
        float wallLength = wallDirection.magnitude;
        if (wallLength > MinimumWallSegmentLength)
        {
            wallDirection /= wallLength;
        }

        float wallHeight = wall.transform.localScale.y;
        return new WallGeometryData
        {
            wallStart = wallStart,
            wallEnd = wallEnd,
            wallDirection = wallDirection,
            wallLength = wallLength,
            wallHeight = wallHeight,
            wallThickness = wall.transform.localScale.x,
            centerY = wall.transform.position.y,
            outerStartVertexId = wall.StartVertexId,
            outerEndVertexId = wall.EndVertexId,
            visualState = WallVisualState.Capture(wall.gameObject),
            outerStartSplitPoint = wall.IsStartSplitPoint,
            outerEndSplitPoint = wall.IsEndSplitPoint,
        };
    }

    private Transform CreateWallSegment(
        Transform parent,
        string segmentName,
        Vector3 startPoint,
        Vector3 endPoint,
        float thickness,
        float height,
        float centerY,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        WallVisualState visualState,
        bool hideBaseWallVisual,
        bool startSplitPoint,
        bool endSplitPoint)
    {
        Vector3 direction = endPoint - startPoint;
        direction.y = 0f;
        if (direction.magnitude < MinimumWallSegmentLength)
        {
            return null;
        }

        GameObject wallObject = WallObjectFactory.CreateWallObject(
            string.IsNullOrWhiteSpace(segmentName) ? SegmentObjectName : segmentName,
            parent,
            GetCubeMesh(),
            visualState);
        bool applied = WallObjectFactory.ConfigureWall(
            wallObject,
            new WallData(startPoint, endPoint, thickness, height, centerY),
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            startSplitPoint,
            endSplitPoint,
            MinimumWallSegmentLength,
            wallLengthDisplay,
            false);

        if (!applied)
        {
            Destroy(wallObject);
            return null;
        }

        if (hideBaseWallVisual)
        {
            ApplyHiddenBaseWallVisual(wallObject);
        }

        if (handleManager != null)
        {
            handleManager.RegisterWall(wallObject);
        }

        return wallObject.transform;
    }

    private static void ApplyHiddenBaseWallVisual(GameObject wallObject)
    {
        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = null;
            renderer.enabled = false;
        }

        Wall wallComponent = wallObject.GetComponent<Wall>();
        wallComponent?.SetTopMaterial(null);
    }

    private Transform GetOrCreateSegmentsRoot(WallOpeningContainer container)
    {
        if (container == null)
        {
            return null;
        }

        Transform existing = container.transform.Find(SegmentGroupName);
        if (existing != null)
        {
            return existing;
        }

        return CreateSegmentsRoot(container);
    }

    private static Transform CreateSegmentsRoot(WallOpeningContainer container)
    {
        GameObject segmentsObject = new GameObject(SegmentGroupName);
        segmentsObject.transform.SetParent(container.transform, false);
        segmentsObject.transform.localPosition = Vector3.zero;
        segmentsObject.transform.localRotation = Quaternion.identity;
        segmentsObject.transform.localScale = Vector3.one;
        LayerUtility.ApplyLayer(segmentsObject, LayerUtility.WallLayerName, false);
        return segmentsObject.transform;
    }

}
