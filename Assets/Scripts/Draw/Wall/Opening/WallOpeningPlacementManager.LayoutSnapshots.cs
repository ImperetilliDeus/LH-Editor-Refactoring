using UnityEngine;

public partial class WallOpeningPlacementManager
{
    private void CreateOpeningOnSelectedWall(OpeningPlacementType type)
    {
        if (!CanEditOpenings())
        {
            return;
        }

        Wall selectedWall = GetSelectedWallComponent();
        if (selectedWall == null)
        {
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = CaptureLayoutSnapshot(selectedWall);

        Transform containerTransform = GetOrCreateOpeningContainer(selectedWall);
        WallOpeningContainer container = containerTransform != null ? containerTransform.GetComponent<WallOpeningContainer>() : null;
        if (container == null)
        {
            return;
        }

        float openingWidth = MillimetersToUnits(type == OpeningPlacementType.Door ? defaultDoorWidthMillimeters : defaultWindowWidthMillimeters);
        float openingHeight = MillimetersToUnits(type == OpeningPlacementType.Door ? defaultDoorHeightMillimeters : defaultWindowHeightMillimeters);
        float defaultDepthMillimeters = type == OpeningPlacementType.Door
            ? defaultDoorDepthMillimeters
            : defaultWindowDepthMillimeters;
        float defaultBottomOffsetMillimeters = type == OpeningPlacementType.Door
            ? defaultDoorBottomOffsetMillimeters
            : defaultWindowBottomOffsetMillimeters;

        float openingDepth = Mathf.Min(MillimetersToUnits(defaultDepthMillimeters), container.WallThickness);
        float bottomOffset = MillimetersToUnits(defaultBottomOffsetMillimeters);
        float minimumSideWall = MillimetersToUnits(minimumSideWallMillimeters);

        if (container.WallLength < openingWidth + minimumSideWall * 2f)
        {
            return;
        }

        float openingBottomY = container.WallBottomY + bottomOffset;

        float maxAllowedHeight = container.WallTopY - openingBottomY;
        openingHeight = Mathf.Min(openingHeight, maxAllowedHeight);
        if (openingHeight <= 0.01f)
        {
            return;
        }

        float centerDistance = ClampOpeningCenterDistance(container, null, container.WallLength * 0.5f, openingWidth);

        GameObject openingObject = new GameObject(type == OpeningPlacementType.Door ? "Door" : "Window");
        openingObject.transform.SetParent(container.transform, false);
        LayerUtility.ApplyLayer(
            openingObject,
            type == OpeningPlacementType.Door ? LayerUtility.DoorLayerName : LayerUtility.WindowLayerName,
            false);
        WallOpening opening = openingObject.AddComponent<WallOpening>();
        opening.Initialize(
            this,
            container,
            type,
            type == OpeningPlacementType.Door ? GetCurrentDoorTypeKey() : string.Empty,
            type == OpeningPlacementType.Window ? GetCurrentWindowTypeKey() : string.Empty,
            false,
            false,
            centerDistance,
            openingWidth,
            openingHeight,
            openingDepth,
            openingBottomY);

        SelectOpening(opening);
        RebuildContainer(container, false);
        RefreshSelectedWallForContainer(container, opening.CenterDistance);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, CaptureLayoutSnapshot(container));
        }

        StartCoroutine(RefreshWallRegistryAfterSplit(false));
    }

    public UndoRedoManager.OpeningLayoutSnapshot CaptureLayoutSnapshot(Wall wall)
    {
        if (wall == null)
        {
            return default;
        }

        WallOpeningContainer container = wall.GetComponentInParent<WallOpeningContainer>();
        if (container != null)
        {
            return CaptureLayoutSnapshot(container);
        }

        return new UndoRedoManager.OpeningLayoutSnapshot
        {
            hasContainer = false,
            layoutName = wall.name,
            wallSnapshot = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject),
        };
    }

    public UndoRedoManager.OpeningLayoutSnapshot CaptureLayoutSnapshot(WallOpeningContainer container)
    {
        if (container == null)
        {
            return default;
        }

        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));
        UndoRedoManager.OpeningStateSnapshot[] openingSnapshots = new UndoRedoManager.OpeningStateSnapshot[cachedOpenings.Count];
        for (int i = 0; i < cachedOpenings.Count; i++)
        {
            WallOpening opening = cachedOpenings[i];
            openingSnapshots[i] = new UndoRedoManager.OpeningStateSnapshot
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
        }

        return new UndoRedoManager.OpeningLayoutSnapshot
        {
            hasContainer = true,
            layoutName = container.name,
            wallStart = container.WallStart,
            wallEnd = container.WallEnd,
            wallThickness = container.WallThickness,
            wallHeight = container.WallHeight,
            centerY = container.CenterY,
            visualState = container.VisualState,
            outerStartVertexId = container.OuterStartVertexId,
            outerEndVertexId = container.OuterEndVertexId,
            suppressOuterStartHandle = container.SuppressOuterStartHandle,
            suppressOuterEndHandle = container.SuppressOuterEndHandle,
            outerStartSplitPoint = container.OuterStartSplitPoint,
            outerEndSplitPoint = container.OuterEndSplitPoint,
            openings = openingSnapshots,
        };
    }

    public void ApplyLayoutSnapshot(UndoRedoManager.OpeningLayoutSnapshot target, UndoRedoManager.OpeningLayoutSnapshot current)
    {
        RemoveLayout(current);
        RemoveLayout(target);

        if (!target.hasContainer)
        {
            if (target.wallSnapshot.wallObject == null && string.IsNullOrEmpty(target.wallSnapshot.name))
            {
                return;
            }

            GameObject restoredWall = CreateRestoredWall(target.wallSnapshot);
            if (restoredWall != null && handleManager != null)
            {
                handleManager.RegisterWall(restoredWall);
            }

            return;
        }

        int openingCount = target.openings != null ? target.openings.Length : 0;
        if (openingCount == 0)
        {
            GameObject restoredWall = CreateRestoredWall(BuildWallSnapshotFromContainer(target));
            if (restoredWall != null && handleManager != null)
            {
                handleManager.RegisterWall(restoredWall);
            }

            return;
        }

        GameObject containerObject = new GameObject(target.layoutName);
        containerObject.transform.SetParent(wallRoot, false);
        containerObject.transform.position = Vector3.zero;
        containerObject.transform.rotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;

        WallOpeningContainer container = containerObject.AddComponent<WallOpeningContainer>();
        container.Initialize(
            target.wallStart,
            target.wallEnd,
            target.wallThickness,
            target.wallHeight,
            target.centerY,
            target.visualState,
            target.outerStartVertexId,
            target.outerEndVertexId,
            target.suppressOuterStartHandle,
            target.suppressOuterEndHandle,
            target.outerStartSplitPoint,
            target.outerEndSplitPoint);

        for (int i = 0; i < openingCount; i++)
        {
            UndoRedoManager.OpeningStateSnapshot openingSnapshot = target.openings[i];
            GameObject openingObject = new GameObject(openingSnapshot.type == OpeningPlacementType.Door ? "Door" : "Window");
            openingObject.transform.SetParent(container.transform, false);
            LayerUtility.ApplyLayer(
                openingObject,
                openingSnapshot.type == OpeningPlacementType.Door ? LayerUtility.DoorLayerName : LayerUtility.WindowLayerName,
                false);
            WallOpening opening = openingObject.AddComponent<WallOpening>();
            opening.Initialize(
                this,
                container,
                openingSnapshot.type,
                openingSnapshot.doorTypeKey,
                openingSnapshot.windowTypeKey,
                openingSnapshot.doorOpensRight,
                openingSnapshot.doorVerticalFlip,
                openingSnapshot.centerDistance,
                openingSnapshot.width,
                openingSnapshot.height,
                openingSnapshot.depth,
                openingSnapshot.bottomY);
        }

        RebuildContainer(container, false);
    }

    public void RebuildOpeningContainer(WallOpeningContainer container)
    {
        RebuildContainer(container, false);
    }

    public void SelectPreferredWallForContainer(WallOpeningContainer container, float preferredDistance)
    {
        RefreshSelectedWallForContainer(container, preferredDistance);
    }
}
