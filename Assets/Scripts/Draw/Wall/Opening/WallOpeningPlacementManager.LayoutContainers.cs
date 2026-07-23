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
            Transform container = wallRoot.Find(snapshot.layoutName);
            if (container != null)
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

                container.gameObject.SetActive(false);
                Destroy(container.gameObject);
            }

            return;
        }

        Wall wall = FindMatchingStandaloneWall(snapshot.wallSnapshot);
        if (wall == null)
        {
            return;
        }

        if (handleManager != null)
        {
            handleManager.UnregisterWall(wall.gameObject);
        }

        wall.ClearLengthDisplay(wallLengthDisplay);
        wall.gameObject.SetActive(false);
        Destroy(wall.gameObject);
    }

    private Wall FindMatchingStandaloneWall(UndoRedoManager.WallStateSnapshot snapshot)
    {
        if (wallRoot == null)
        {
            return null;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls);
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            if (wall.transform.parent != wallRoot)
            {
                continue;
            }

            if (wall.name == snapshot.name &&
                snapshot.wallData != null &&
                (wall.Data.startPoint - snapshot.wallData.startPoint).sqrMagnitude <= 0.0001f &&
                (wall.Data.endPoint - snapshot.wallData.endPoint).sqrMagnitude <= 0.0001f)
            {
                return wall;
            }
        }

        return null;
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

        CollectOpenings(container, cachedOpenings);
        if (cachedOpenings.Count > 0)
        {
            return null;
        }

        UndoRedoManager.WallStateSnapshot wallSnapshot = BuildWallSnapshotFromContainer(container);
        RemoveLayout(CaptureLayoutSnapshot(container));
        GameObject restoredWall = RestoreWallFromSnapshot(wallSnapshot);

        RoomTopologyEvents.RequestRefreshAll();
        StartCoroutine(RefreshWallRegistryAfterSplit(false));

        MarkMarkerVisualsDirty();
        return restoredWall;
    }

    private UndoRedoManager.WallStateSnapshot BuildWallSnapshotFromContainer(WallOpeningContainer container)
    {
        return BuildWallSnapshotFromContainer(new UndoRedoManager.OpeningLayoutSnapshot
        {
            layoutName = container != null ? container.name : "Wall",
            wallStart = container != null ? container.WallStart : Vector3.zero,
            wallEnd = container != null ? container.WallEnd : Vector3.right,
            wallThickness = container != null ? container.WallThickness : 0.1f,
            wallHeight = container != null ? container.WallHeight : 1f,
            centerY = container != null ? container.CenterY : 0.5f,
            visualState = container != null ? container.VisualState : default,
            outerStartVertexId = container != null ? container.OuterStartVertexId : 0,
            outerEndVertexId = container != null ? container.OuterEndVertexId : 0,
            suppressOuterStartHandle = container != null && container.SuppressOuterStartHandle,
            suppressOuterEndHandle = container != null && container.SuppressOuterEndHandle,
            outerStartSplitPoint = container != null && container.OuterStartSplitPoint,
            outerEndSplitPoint = container != null && container.OuterEndSplitPoint,
        });
    }

    private UndoRedoManager.WallStateSnapshot BuildWallSnapshotFromContainer(UndoRedoManager.OpeningLayoutSnapshot snapshot)
    {
        return new UndoRedoManager.WallStateSnapshot
        {
            name = snapshot.layoutName,
            visualState = snapshot.visualState,
            wallData = new WallData(snapshot.wallStart, snapshot.wallEnd, snapshot.wallThickness, snapshot.wallHeight, snapshot.centerY),
            startVertexId = snapshot.outerStartVertexId,
            endVertexId = snapshot.outerEndVertexId,
            suppressStartHandle = snapshot.suppressOuterStartHandle,
            suppressEndHandle = snapshot.suppressOuterEndHandle,
            startSplitPoint = snapshot.outerStartSplitPoint,
            endSplitPoint = snapshot.outerEndSplitPoint,
        };
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
        if (selectedWall.Data != null)
        {
            container.SetPersistentWallId(selectedWall.Data.id);
        }

        if (handleManager != null)
        {
            handleManager.UnregisterWall(selectedWall.gameObject);
        }

        pendingRoomRefreshRemovedWalls.Add(selectedWall);

        selectedWall.ClearLengthDisplay(wallLengthDisplay);
        selectedWall.gameObject.SetActive(false);
        Destroy(selectedWall.gameObject);
        return container.transform;
    }

    private void SplitContainerWallSegment(WallOpeningContainer container, Wall selectedSegment)
    {
        if (container == null || selectedSegment == null || wallRoot == null)
        {
            return;
        }

        Vector3 segmentMidpoint = (selectedSegment.Data.startPoint + selectedSegment.Data.endPoint) * 0.5f;
        float splitDistance = Vector3.Dot(segmentMidpoint - container.WallStart, container.WallDirection);
        if (splitDistance <= MinimumWallSegmentLength || splitDistance >= container.WallLength - MinimumWallSegmentLength)
        {
            return;
        }

        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));

        if (SplitDistanceOverlapsOpeningSpan(cachedOpenings, splitDistance))
        {
            Debug.LogWarning("Wall split point overlaps an opening span.", selectedSegment);
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot sourceSnapshot = CaptureLayoutSnapshot(container);
        List<UndoRedoManager.OpeningStateSnapshot> leftOpenings = new List<UndoRedoManager.OpeningStateSnapshot>();
        List<UndoRedoManager.OpeningStateSnapshot> rightOpenings = new List<UndoRedoManager.OpeningStateSnapshot>();
        PartitionOpeningsForSplit(cachedOpenings, splitDistance, leftOpenings, rightOpenings);

        RemoveLayout(sourceSnapshot);

        Vector3 splitPoint = container.WallStart + container.WallDirection * splitDistance;
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

        RoomTopologyEvents.RequestRefreshAll();
        StartCoroutine(RefreshWallRegistryAfterSplit(false));

        if (wallSelectionManager != null)
        {
            Wall replacementWall = FindClosestVisibleWallToPoint(splitPoint);
            if (replacementWall != null)
            {
                wallSelectionManager.SetSelectedWall(replacementWall.gameObject);
            }
        }
    }

    private static bool SplitDistanceOverlapsOpeningSpan(
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

    private static void PartitionOpeningsForSplit(
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
            return;
        }

        WallOpeningContainer nextContainer = OpeningContainerFactory.Create(
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

        for (int i = 0; i < openings.Count; i++)
        {
            CreateOpeningFromSnapshot(nextContainer, openings[i]);
        }

        RebuildContainer(nextContainer, false);
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
            MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = null;
                renderer.enabled = false;
            }

            Wall wallComponent = wallObject.GetComponent<Wall>();
            wallComponent?.SetTopMaterial(null);
        }

        if (handleManager != null)
        {
            handleManager.RegisterWall(wallObject);
        }

        return wallObject.transform;
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

        GameObject segmentsObject = new GameObject(SegmentGroupName);
        segmentsObject.transform.SetParent(container.transform, false);
        segmentsObject.transform.localPosition = Vector3.zero;
        segmentsObject.transform.localRotation = Quaternion.identity;
        segmentsObject.transform.localScale = Vector3.one;
        LayerUtility.ApplyLayer(segmentsObject, LayerUtility.WallLayerName, false);
        return segmentsObject.transform;
    }

    private Wall FindClosestVisibleWallToPoint(Vector3 point)
    {
        if (wallRoot == null)
        {
            return null;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls, true);
        Wall bestWall = null;
        float bestDistanceSqr = float.MaxValue;

        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy || WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                continue;
            }

            Vector3 midpoint = (wall.Data.startPoint + wall.Data.endPoint) * 0.5f;
            float distanceSqr = (midpoint - point).sqrMagnitude;
            if (distanceSqr >= bestDistanceSqr)
            {
                continue;
            }

            bestDistanceSqr = distanceSqr;
            bestWall = wall;
        }

        return bestWall;
    }
}
