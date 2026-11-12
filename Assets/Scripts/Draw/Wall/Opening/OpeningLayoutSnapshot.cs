using UnityEngine;

public struct WallStateSnapshot
{
    public GameObject wallObject;
    public WallData wallData;
    public string name;
    public WallVisualState visualState;
    public int startVertexId;
    public int endVertexId;
    public bool suppressStartHandle;
    public bool suppressEndHandle;
    public bool startSplitPoint;
    public bool endSplitPoint;

    public static WallStateSnapshot Capture(GameObject wallObject)
    {
        if (wallObject == null)
        {
            return default;
        }

        Wall wallComponent = wallObject.GetComponent<Wall>();
        return new WallStateSnapshot
        {
            wallObject = wallObject,
            name = wallObject.name,
            visualState = WallVisualState.Capture(wallObject),
            wallData = wallComponent != null ? wallComponent.Data.Clone() : null,
            startVertexId = wallComponent != null ? wallComponent.StartVertexId : 0,
            endVertexId = wallComponent != null ? wallComponent.EndVertexId : 0,
            suppressStartHandle = wallComponent != null && wallComponent.SuppressStartHandle,
            suppressEndHandle = wallComponent != null && wallComponent.SuppressEndHandle,
            startSplitPoint = wallComponent != null && wallComponent.IsStartSplitPoint,
            endSplitPoint = wallComponent != null && wallComponent.IsEndSplitPoint,
        };
    }
}

public struct OpeningStateSnapshot
{
    public WallOpeningPlacementManager.OpeningPlacementType type;
    public string doorTypeKey;
    public string windowTypeKey;
    public bool doorOpensRight;
    public bool doorVerticalFlip;
    public float centerDistance;
    public float width;
    public float height;
    public float depth;
    public float bottomY;
}

public struct OpeningLayoutSnapshot
{
    public bool hasContainer;
    public string layoutName;
    public WallStateSnapshot wallSnapshot;
    public Vector3 wallStart;
    public Vector3 wallEnd;
    public float wallThickness;
    public float wallHeight;
    public float centerY;
    public WallVisualState visualState;
    public int outerStartVertexId;
    public int outerEndVertexId;
    public bool suppressOuterStartHandle;
    public bool suppressOuterEndHandle;
    public bool outerStartSplitPoint;
    public bool outerEndSplitPoint;
    public OpeningStateSnapshot[] openings;
}
