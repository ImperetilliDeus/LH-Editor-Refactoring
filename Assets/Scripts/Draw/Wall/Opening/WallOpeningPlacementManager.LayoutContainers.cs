using UnityEngine;

public partial class WallOpeningPlacementManager
{
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
        GameObject restoredWall = CreateRestoredWall(wallSnapshot);
        if (restoredWall != null && handleManager != null)
        {
            handleManager.RegisterWall(restoredWall);
        }

        RoomTopologyEvents.RequestRefreshAll();

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
            wallMaterial = container != null ? container.WallMaterial : null,
            wallTopMaterial = container != null ? container.WallTopMaterial : null,
            outerStartVertexId = container != null ? container.OuterStartVertexId : 0,
            outerEndVertexId = container != null ? container.OuterEndVertexId : 0,
            suppressOuterStartHandle = container != null && container.SuppressOuterStartHandle,
            suppressOuterEndHandle = container != null && container.SuppressOuterEndHandle,
        });
    }

    private UndoRedoManager.WallStateSnapshot BuildWallSnapshotFromContainer(UndoRedoManager.OpeningLayoutSnapshot snapshot)
    {
        Vector3 center = (snapshot.wallStart + snapshot.wallEnd) * 0.5f;
        Vector3 direction = snapshot.wallEnd - snapshot.wallStart;
        direction.y = 0f;
        float length = direction.magnitude;
        Quaternion rotation = length > MinimumWallSegmentLength
            ? Quaternion.LookRotation(direction / length, Vector3.up)
            : Quaternion.identity;

        return new UndoRedoManager.WallStateSnapshot
        {
            name = snapshot.layoutName,
            position = new Vector3(center.x, snapshot.centerY, center.z),
            rotation = rotation,
            scale = new Vector3(snapshot.wallThickness, snapshot.wallHeight, Mathf.Max(length, MinimumWallSegmentLength)),
            sharedMaterial = snapshot.wallMaterial,
            topMaterial = snapshot.wallTopMaterial,
            wallData = new WallData(snapshot.wallStart, snapshot.wallEnd, snapshot.wallThickness, snapshot.wallHeight, snapshot.centerY),
            startVertexId = snapshot.outerStartVertexId,
            endVertexId = snapshot.outerEndVertexId,
            suppressStartHandle = snapshot.suppressOuterStartHandle,
            suppressEndHandle = snapshot.suppressOuterEndHandle,
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
            cachedCubeMesh,
            snapshot.sharedMaterial,
            snapshot.topMaterial);
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
        GameObject containerObject = new GameObject(selectedWall.name);
        containerObject.transform.SetParent(wallRoot, false);
        containerObject.transform.position = Vector3.zero;
        containerObject.transform.rotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;
        LayerUtility.ApplyLayer(containerObject, LayerUtility.WallLayerName, false);

        WallOpeningContainer container = containerObject.AddComponent<WallOpeningContainer>();
        container.Initialize(
            geometry.wallStart,
            geometry.wallEnd,
            geometry.wallThickness,
            geometry.wallHeight,
            geometry.centerY,
            geometry.wallMaterial,
            geometry.wallTopMaterial,
            geometry.outerStartVertexId,
            geometry.outerEndVertexId,
            selectedWall.SuppressStartHandle,
            selectedWall.SuppressEndHandle);

        if (handleManager != null)
        {
            handleManager.UnregisterWall(selectedWall.gameObject);
        }

        pendingRoomRefreshRemovedWalls.Add(selectedWall);

        selectedWall.ClearLengthDisplay(wallLengthDisplay);
        Destroy(selectedWall.gameObject);
        return container.transform;
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

        MeshRenderer wallRenderer = wall.GetComponent<MeshRenderer>();
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
            wallMaterial = wallRenderer != null ? wallRenderer.sharedMaterial : null,
            wallTopMaterial = wall.GetTopMaterial(),
        };
    }

    private void CreateWallSegment(
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
        Material wallMaterial)
    {
        Vector3 direction = endPoint - startPoint;
        direction.y = 0f;
        if (direction.magnitude < MinimumWallSegmentLength)
        {
            return;
        }

        Material topMaterial = parent.TryGetComponent(out WallOpeningContainer container) ? container.WallTopMaterial : null;
        GameObject wallObject = WallObjectFactory.CreateWallObject(
            segmentName,
            parent,
            cachedCubeMesh,
            wallMaterial,
            topMaterial);
        bool applied = WallObjectFactory.ConfigureWall(
            wallObject,
            new WallData(startPoint, endPoint, thickness, height, centerY),
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            false,
            false,
            MinimumWallSegmentLength,
            wallLengthDisplay,
            false);

        if (!applied)
        {
            Destroy(wallObject);
            return;
        }

        if (handleManager != null)
        {
            handleManager.RegisterWall(wallObject);
        }
    }
}
