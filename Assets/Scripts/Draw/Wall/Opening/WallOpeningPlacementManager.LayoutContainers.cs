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

        SplitContainerWallAtDistance(container, selectedSegment, splitDistance);
    }

    private void SplitContainerWallSegment(WallOpeningContainer container, Vector3 worldPoint)
    {
        if (container == null || wallRoot == null)
        {
            return;
        }

        if (!OpeningContainerSplitPlanner.TryGetSplitDistance(
                container,
                worldPoint,
                MinimumWallSegmentLength,
                out float splitDistance))
        {
            return;
        }

        SplitContainerWallAtDistance(container, container, splitDistance);
    }

    private void SplitContainerWallAtDistance(WallOpeningContainer container, Object logContext, float splitDistance)
    {
        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));

        if (OpeningContainerSplitPlanner.SplitDistanceOverlapsOpeningSpan(cachedOpenings, splitDistance))
        {
            Debug.LogWarning("Wall split point overlaps an opening span.", logContext);
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

    public bool TryRepartitionContainerWithAdjacentWallAtSplit(
        WallOpeningContainer container,
        UndoRedoManager.OpeningLayoutSnapshot containerStartSnapshot,
        Wall adjacentWall,
        UndoRedoManager.OpeningLayoutSnapshot adjacentCurrentSnapshot,
        int splitVertexId,
        Vector3 splitPoint)
    {
        if (container == null ||
            adjacentWall == null ||
            !containerStartSnapshot.hasContainer ||
            adjacentCurrentSnapshot.hasContainer ||
            containerStartSnapshot.openings == null ||
            containerStartSnapshot.openings.Length == 0)
        {
            return false;
        }

        bool draggingStart = splitVertexId == containerStartSnapshot.outerStartVertexId;
        bool draggingEnd = splitVertexId == containerStartSnapshot.outerEndVertexId;
        if (!draggingStart && !draggingEnd)
        {
            return false;
        }

        if (!TryResolveAdjacentSplitEndpoint(adjacentWall, splitVertexId, out Vector3 adjacentOuterPoint, out int adjacentOuterVertexId, out bool suppressAdjacentOuterHandle, out bool adjacentOuterSplitPoint))
        {
            return false;
        }

        Vector3 leftStart;
        Vector3 leftEnd;
        Vector3 rightStart;
        Vector3 rightEnd;
        int leftStartVertexId;
        int rightEndVertexId;
        bool suppressLeftStartHandle;
        bool suppressRightEndHandle;
        bool leftStartSplitPoint;
        bool rightEndSplitPoint;

        if (draggingStart)
        {
            leftStart = adjacentOuterPoint;
            leftEnd = splitPoint;
            rightStart = splitPoint;
            rightEnd = container.WallEnd;
            leftStartVertexId = adjacentOuterVertexId;
            rightEndVertexId = container.OuterEndVertexId;
            suppressLeftStartHandle = suppressAdjacentOuterHandle;
            suppressRightEndHandle = container.SuppressOuterEndHandle;
            leftStartSplitPoint = adjacentOuterSplitPoint;
            rightEndSplitPoint = container.OuterEndSplitPoint;
        }
        else
        {
            leftStart = container.WallStart;
            leftEnd = splitPoint;
            rightStart = splitPoint;
            rightEnd = adjacentOuterPoint;
            leftStartVertexId = container.OuterStartVertexId;
            rightEndVertexId = adjacentOuterVertexId;
            suppressLeftStartHandle = container.SuppressOuterStartHandle;
            suppressRightEndHandle = suppressAdjacentOuterHandle;
            leftStartSplitPoint = container.OuterStartSplitPoint;
            rightEndSplitPoint = adjacentOuterSplitPoint;
        }

        if (!TryPartitionStartSnapshotOpeningsForSplit(
                containerStartSnapshot,
                leftStart,
                rightEnd,
                splitPoint,
                draggingStart,
                out List<UndoRedoManager.OpeningStateSnapshot> leftOpenings,
                out List<UndoRedoManager.OpeningStateSnapshot> rightOpenings))
        {
            return false;
        }

        UndoRedoManager.OpeningLayoutSnapshot currentContainerSnapshot = CaptureLayoutSnapshot(container);
        RemoveLayout(currentContainerSnapshot);
        RemoveLayout(adjacentCurrentSnapshot);

        CreateSplitContainerLayout(
            containerStartSnapshot,
            containerStartSnapshot.layoutName + "_A",
            leftStart,
            leftEnd,
            leftStartVertexId,
            splitVertexId,
            suppressLeftStartHandle,
            false,
            leftStartSplitPoint,
            true,
            leftOpenings);
        CreateSplitContainerLayout(
            containerStartSnapshot,
            containerStartSnapshot.layoutName + "_B",
            rightStart,
            rightEnd,
            splitVertexId,
            rightEndVertexId,
            false,
            suppressRightEndHandle,
            true,
            rightEndSplitPoint,
            rightOpenings);

        RoomTopologyEvents.RequestRefreshAll();
        MarkMarkerVisualsDirty();
        StartCoroutine(RefreshWallRegistryAfterSplit(false));
        return true;
    }

    private static bool TryResolveAdjacentSplitEndpoint(
        Wall adjacentWall,
        int splitVertexId,
        out Vector3 adjacentOuterPoint,
        out int adjacentOuterVertexId,
        out bool suppressAdjacentOuterHandle,
        out bool adjacentOuterSplitPoint)
    {
        adjacentOuterPoint = Vector3.zero;
        adjacentOuterVertexId = 0;
        suppressAdjacentOuterHandle = false;
        adjacentOuterSplitPoint = false;

        if (adjacentWall == null)
        {
            return false;
        }

        if (adjacentWall.StartVertexId == splitVertexId)
        {
            adjacentOuterPoint = adjacentWall.Data.endPoint;
            adjacentOuterVertexId = adjacentWall.EndVertexId;
            suppressAdjacentOuterHandle = adjacentWall.SuppressEndHandle;
            adjacentOuterSplitPoint = adjacentWall.IsEndSplitPoint;
            return true;
        }

        if (adjacentWall.EndVertexId == splitVertexId)
        {
            adjacentOuterPoint = adjacentWall.Data.startPoint;
            adjacentOuterVertexId = adjacentWall.StartVertexId;
            suppressAdjacentOuterHandle = adjacentWall.SuppressStartHandle;
            adjacentOuterSplitPoint = adjacentWall.IsStartSplitPoint;
            return true;
        }

        return false;
    }

    private static bool TryPartitionStartSnapshotOpeningsForSplit(
        UndoRedoManager.OpeningLayoutSnapshot containerStartSnapshot,
        Vector3 combinedStart,
        Vector3 combinedEnd,
        Vector3 splitPoint,
        bool draggingStart,
        out List<UndoRedoManager.OpeningStateSnapshot> leftOpenings,
        out List<UndoRedoManager.OpeningStateSnapshot> rightOpenings)
    {
        leftOpenings = new List<UndoRedoManager.OpeningStateSnapshot>();
        rightOpenings = new List<UndoRedoManager.OpeningStateSnapshot>();

        Vector3 combinedDirection = combinedEnd - combinedStart;
        combinedDirection.y = 0f;
        float combinedLength = combinedDirection.magnitude;
        if (combinedLength <= MinimumWallSegmentLength)
        {
            return false;
        }

        combinedDirection /= combinedLength;
        float splitDistance = Vector3.Dot(splitPoint - combinedStart, combinedDirection);
        if (splitDistance <= MinimumWallSegmentLength || splitDistance >= combinedLength - MinimumWallSegmentLength)
        {
            return false;
        }

        Vector3 sourceDirection = containerStartSnapshot.wallEnd - containerStartSnapshot.wallStart;
        sourceDirection.y = 0f;
        float sourceLength = sourceDirection.magnitude;
        if (sourceLength <= MinimumWallSegmentLength)
        {
            return false;
        }

        sourceDirection /= sourceLength;

        bool movedOpeningAcrossSplit = false;
        for (int i = 0; i < containerStartSnapshot.openings.Length; i++)
        {
            UndoRedoManager.OpeningStateSnapshot openingSnapshot = containerStartSnapshot.openings[i];
            Vector3 openingWorldPoint = containerStartSnapshot.wallStart + sourceDirection * openingSnapshot.centerDistance;
            float openingDistance = Vector3.Dot(openingWorldPoint - combinedStart, combinedDirection);
            float openingStart = openingDistance - openingSnapshot.width * 0.5f;
            float openingEnd = openingDistance + openingSnapshot.width * 0.5f;
            if (splitDistance >= openingStart - 0.0001f && splitDistance <= openingEnd + 0.0001f)
            {
                return false;
            }

            UndoRedoManager.OpeningStateSnapshot nextSnapshot = openingSnapshot;
            if (openingDistance < splitDistance)
            {
                nextSnapshot.centerDistance = openingDistance;
                leftOpenings.Add(nextSnapshot);
                movedOpeningAcrossSplit |= draggingStart;
                continue;
            }

            nextSnapshot.centerDistance = openingDistance - splitDistance;
            rightOpenings.Add(nextSnapshot);
            movedOpeningAcrossSplit |= !draggingStart;
        }

        return movedOpeningAcrossSplit;
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
