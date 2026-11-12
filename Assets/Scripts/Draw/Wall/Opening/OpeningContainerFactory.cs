using UnityEngine;

internal static class OpeningContainerFactory
{
    public static WallOpeningContainer Create(
        string containerName,
        Transform parent,
        Vector3 wallStart,
        Vector3 wallEnd,
        float wallThickness,
        float wallHeight,
        float centerY,
        WallVisualState visualState,
        int outerStartVertexId,
        int outerEndVertexId,
        bool suppressOuterStartHandle,
        bool suppressOuterEndHandle,
        bool outerStartSplitPoint,
        bool outerEndSplitPoint,
        bool applyWallLayer)
    {
        GameObject containerObject = new GameObject(containerName);
        containerObject.transform.SetParent(parent, false);
        containerObject.transform.position = Vector3.zero;
        containerObject.transform.rotation = Quaternion.identity;
        containerObject.transform.localScale = Vector3.one;

        if (applyWallLayer)
        {
            LayerUtility.ApplyLayer(containerObject, LayerUtility.WallLayerName, false);
        }

        WallOpeningContainer container = containerObject.AddComponent<WallOpeningContainer>();
        container.Initialize(
            wallStart,
            wallEnd,
            wallThickness,
            wallHeight,
            centerY,
            visualState,
            outerStartVertexId,
            outerEndVertexId,
            suppressOuterStartHandle,
            suppressOuterEndHandle,
            outerStartSplitPoint,
            outerEndSplitPoint);
        return container;
    }

    public static WallOpeningContainer Create(
        OpeningLayoutSnapshot layoutSnapshot,
        Transform parent,
        bool applyWallLayer)
    {
        return Create(
            layoutSnapshot.layoutName,
            parent,
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
            layoutSnapshot.outerEndSplitPoint,
            applyWallLayer);
    }
}
