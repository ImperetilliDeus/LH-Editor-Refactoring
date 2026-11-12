using UnityEngine;

internal static class OpeningContainerSnapshotBuilder
{
    public static WallStateSnapshot BuildWallSnapshot(WallOpeningContainer container)
    {
        return BuildWallSnapshot(BuildLayoutSnapshot(container));
    }

    public static WallStateSnapshot BuildWallSnapshot(OpeningLayoutSnapshot snapshot)
    {
        return new WallStateSnapshot
        {
            name = snapshot.layoutName,
            visualState = snapshot.visualState,
            wallData = BuildWallData(snapshot),
            startVertexId = snapshot.outerStartVertexId,
            endVertexId = snapshot.outerEndVertexId,
            suppressStartHandle = snapshot.suppressOuterStartHandle,
            suppressEndHandle = snapshot.suppressOuterEndHandle,
            startSplitPoint = snapshot.outerStartSplitPoint,
            endSplitPoint = snapshot.outerEndSplitPoint,
        };
    }

    private static OpeningLayoutSnapshot BuildLayoutSnapshot(WallOpeningContainer container)
    {
        return new OpeningLayoutSnapshot
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
        };
    }

    private static WallData BuildWallData(OpeningLayoutSnapshot snapshot)
    {
        return new WallData(snapshot.wallStart, snapshot.wallEnd, snapshot.wallThickness, snapshot.wallHeight, snapshot.centerY);
    }
}
