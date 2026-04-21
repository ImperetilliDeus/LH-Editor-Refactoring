using UnityEngine;

public static class WallObjectFactory
{
    public static GameObject CreateWallObject(
        string name,
        Transform parent,
        Mesh mesh,
        Material wallMaterial,
        Material topMaterial,
        float topFaceOffset = Wall.DefaultTopFaceOffset)
    {
        GameObject wallObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
        if (parent != null)
        {
            wallObject.transform.SetParent(parent, true);
        }

        LayerUtility.ApplyLayer(wallObject, LayerUtility.WallLayerName, false);

        MeshFilter filter = wallObject.GetComponent<MeshFilter>();
        if (filter != null && mesh != null)
        {
            filter.sharedMesh = mesh;
        }

        MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();
        if (renderer != null && wallMaterial != null)
        {
            renderer.sharedMaterial = wallMaterial;
        }

        Wall wall = wallObject.GetComponent<Wall>();
        if (wall == null)
        {
            wall = wallObject.AddComponent<Wall>();
        }

        wall.SetTopMaterial(topMaterial);
        wall.SetTopFaceOffset(topFaceOffset);
        return wallObject;
    }

    public static bool ConfigureWall(
        GameObject wallObject,
        WallData wallData,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint,
        float minimumLength,
        WallLengthDisplay wallLengthDisplay,
        bool isPreview)
    {
        if (wallObject == null)
        {
            return false;
        }

        Wall wall = wallObject.GetComponent<Wall>();
        if (wall == null)
        {
            wall = wallObject.AddComponent<Wall>();
        }

        wall.Initialize(wallData != null ? wallData.Clone() : new WallData());
        wall.SetVertexIds(startVertexId, endVertexId);
        wall.SetHandleSuppressed(suppressStartHandle, suppressEndHandle);
        wall.SetSplitPointFlags(startSplitPoint, endSplitPoint);

        bool applied = wall.UpdateView(minimumLength);
        if (!applied)
        {
            wall.ClearLengthDisplay(wallLengthDisplay);
            return false;
        }

        wall.RefreshLengthDisplay(wallLengthDisplay, isPreview);
        return true;
    }
}
