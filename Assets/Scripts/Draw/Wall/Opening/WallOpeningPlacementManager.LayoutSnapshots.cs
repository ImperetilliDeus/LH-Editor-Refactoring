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

        if (!ResolveNewOpeningDimensions(
                type,
                container,
                out float openingWidth,
                out float openingHeight,
                out float openingDepth,
                out float openingBottomY))
        {
            return;
        }

        if (!TryResolveNewOpeningSpan(container, openingWidth, out float centerDistance, out openingWidth))
        {
            return;
        }

        WallOpening opening = CreateInitializedOpening(
            container,
            type,
            centerDistance,
            openingWidth,
            openingHeight,
            openingDepth,
            openingBottomY);

        FinalizeCreatedOpening(container, opening, beforeSnapshot);
    }

    private void FinalizeCreatedOpening(
        WallOpeningContainer container,
        WallOpening opening,
        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot)
    {
        SelectOpening(opening);
        RebuildContainer(container, false);
        RefreshSelectedWallForContainer(container, opening.CenterDistance);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, CaptureLayoutSnapshot(container));
        }

        StartCoroutine(RefreshWallRegistryAfterSplit(false));
    }

    private WallOpening CreateInitializedOpening(
        WallOpeningContainer container,
        OpeningPlacementType type,
        float centerDistance,
        float openingWidth,
        float openingHeight,
        float openingDepth,
        float openingBottomY)
    {
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
        return opening;
    }

    private bool ResolveNewOpeningDimensions(
        OpeningPlacementType type,
        WallOpeningContainer container,
        out float openingWidth,
        out float openingHeight,
        out float openingDepth,
        out float openingBottomY)
    {
        openingWidth = 0f;
        openingHeight = 0f;
        openingDepth = 0f;
        openingBottomY = 0f;
        if (container == null)
        {
            return false;
        }

        openingWidth = MillimetersToUnits(type == OpeningPlacementType.Door ? defaultDoorWidthMillimeters : defaultWindowWidthMillimeters);
        openingHeight = MillimetersToUnits(type == OpeningPlacementType.Door ? defaultDoorHeightMillimeters : defaultWindowHeightMillimeters);
        float defaultDepthMillimeters = type == OpeningPlacementType.Door
            ? defaultDoorDepthMillimeters
            : defaultWindowDepthMillimeters;
        float defaultBottomOffsetMillimeters = type == OpeningPlacementType.Door
            ? defaultDoorBottomOffsetMillimeters
            : defaultWindowBottomOffsetMillimeters;

        openingDepth = Mathf.Min(MillimetersToUnits(defaultDepthMillimeters), container.WallThickness);
        float bottomOffset = MillimetersToUnits(defaultBottomOffsetMillimeters);
        openingBottomY = container.WallBottomY + bottomOffset;

        float maxAllowedHeight = container.WallTopY - openingBottomY;
        openingHeight = Mathf.Min(openingHeight, maxAllowedHeight);
        return openingHeight > 0.01f;
    }

    private bool TryResolveNewOpeningSpan(
        WallOpeningContainer container,
        float desiredWidth,
        out float centerDistance,
        out float resolvedWidth)
    {
        centerDistance = 0f;
        resolvedWidth = 0f;
        if (container == null)
        {
            return false;
        }

        float minimumSideWall = Mathf.Max(MillimetersToUnits(minimumSideWallMillimeters), MinimumWallSegmentLength);
        CollectOpenings(container, cachedOpenings);
        cachedOpenings.Sort((a, b) => a.CenterDistance.CompareTo(b.CenterDistance));
        return OpeningLayoutService.TryResolveNewOpeningSpan(
            container,
            cachedOpenings,
            desiredWidth,
            minimumSideWall,
            MinimumWallSegmentLength,
            out centerDistance,
            out resolvedWidth);
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

        WallOpeningContainer container = CreateContainerFromSnapshot(target);

        for (int i = 0; i < openingCount; i++)
        {
            CreateOpeningFromSnapshot(container, target.openings[i]);
        }

        RebuildContainer(container, false);
    }

    private WallOpeningContainer CreateContainerFromSnapshot(UndoRedoManager.OpeningLayoutSnapshot layoutSnapshot)
    {
        GameObject containerObject = new GameObject(layoutSnapshot.layoutName);
        containerObject.transform.SetParent(wallRoot, false);
        containerObject.transform.position = Vector3.zero;
        containerObject.transform.rotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;

        WallOpeningContainer container = containerObject.AddComponent<WallOpeningContainer>();
        container.Initialize(
            layoutSnapshot.wallStart,
            layoutSnapshot.wallEnd,
            layoutSnapshot.wallThickness,
            layoutSnapshot.wallHeight,
            layoutSnapshot.centerY,
            layoutSnapshot.visualState,
            layoutSnapshot.outerStartVertexId,
            layoutSnapshot.outerEndVertexId,
            layoutSnapshot.suppressOuterStartHandle,
            layoutSnapshot.suppressOuterEndHandle,
            layoutSnapshot.outerStartSplitPoint,
            layoutSnapshot.outerEndSplitPoint);
        return container;
    }

    private WallOpening CreateOpeningFromSnapshot(
        WallOpeningContainer container,
        UndoRedoManager.OpeningStateSnapshot openingSnapshot)
    {
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
        return opening;
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
