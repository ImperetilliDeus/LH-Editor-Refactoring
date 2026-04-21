using UnityEngine;

public struct WallVisualState
{
    public Material wallMaterial;
    public Material topMaterial;
    public float topFaceOffset;

    public static WallVisualState Capture(GameObject wallObject)
    {
        MeshRenderer renderer = wallObject != null ? wallObject.GetComponent<MeshRenderer>() : null;
        Wall wall = wallObject != null ? wallObject.GetComponent<Wall>() : null;
        return new WallVisualState
        {
            wallMaterial = renderer != null ? renderer.sharedMaterial : null,
            topMaterial = wall != null ? wall.GetTopMaterial() : null,
            topFaceOffset = Wall.DefaultTopFaceOffset,
        };
    }
}
