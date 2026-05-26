using UnityEngine;

public class WallOpeningContainer : MonoBehaviour
{
    [SerializeField] private Vector3 wallStart;
    [SerializeField] private Vector3 wallEnd;
    [SerializeField] private float wallThickness;
    [SerializeField] private float wallHeight;
    [SerializeField] private float centerY;
    [SerializeField] private float wallBottomY;
    [SerializeField] private float wallTopY;
    [SerializeField] private WallVisualState visualState;
    [SerializeField] private string persistentWallId = string.Empty;
    [SerializeField] private int outerStartVertexId;
    [SerializeField] private int outerEndVertexId;
    [SerializeField] private bool suppressOuterStartHandle;
    [SerializeField] private bool suppressOuterEndHandle;
    [SerializeField] private bool outerStartSplitPoint;
    [SerializeField] private bool outerEndSplitPoint;

    public Vector3 WallStart => wallStart;
    public Vector3 WallEnd => wallEnd;
    public float WallThickness => wallThickness;
    public float WallHeight => wallHeight;
    public float CenterY => centerY;
    public float WallBottomY => wallBottomY;
    public float WallTopY => wallTopY;
    public float WallPlaneY => wallStart.y;
    public Material WallMaterial => visualState.wallMaterial;
    public Material WallTopMaterial => visualState.topMaterial;
    public WallVisualState VisualState => visualState;
    public string PersistentWallId => persistentWallId ?? string.Empty;
    public bool SuppressOuterStartHandle => suppressOuterStartHandle;
    public bool SuppressOuterEndHandle => suppressOuterEndHandle;
    public bool OuterStartSplitPoint => outerStartSplitPoint;
    public bool OuterEndSplitPoint => outerEndSplitPoint;

    public Vector3 WallDirection
    {
        get
        {
            Vector3 direction = wallEnd - wallStart;
            direction.y = 0f;
            return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
        }
    }

    public float WallLength
    {
        get
        {
            Vector3 direction = wallEnd - wallStart;
            direction.y = 0f;
            return direction.magnitude;
        }
    }

    public int OuterStartVertexId => outerStartVertexId;
    public int OuterEndVertexId => outerEndVertexId;

    public void Initialize(
        Vector3 start,
        Vector3 end,
        float thickness,
        float height,
        float center,
        WallVisualState state,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint = false,
        bool endSplitPoint = false)
    {
        wallStart = start;
        wallEnd = end;
        wallThickness = thickness;
        wallHeight = height;
        centerY = center;
        wallBottomY = center - height * 0.5f;
        wallTopY = wallBottomY + height;
        visualState = state;
        outerStartVertexId = startVertexId;
        outerEndVertexId = endVertexId;
        suppressOuterStartHandle = suppressStartHandle;
        suppressOuterEndHandle = suppressEndHandle;
        outerStartSplitPoint = startSplitPoint;
        outerEndSplitPoint = endSplitPoint;
    }

    public void SetOuterVertexIds(int startVertexId, int endVertexId)
    {
        outerStartVertexId = startVertexId;
        outerEndVertexId = endVertexId;
    }

    public void SetPersistentWallId(string wallId)
    {
        persistentWallId = wallId ?? string.Empty;
    }

    public void SetOuterSplitPointFlags(bool startSplitPoint, bool endSplitPoint)
    {
        outerStartSplitPoint = startSplitPoint;
        outerEndSplitPoint = endSplitPoint;
    }

    public void SetHandleSuppression(bool suppressStart, bool suppressEnd)
    {
        suppressOuterStartHandle = suppressStart;
        suppressOuterEndHandle = suppressEnd;
    }

    public void SetWallSpan(Vector3 start, Vector3 end)
    {
        wallStart = start;
        wallEnd = end;
    }

    public void SetWallThickness(float thickness)
    {
        wallThickness = thickness;
    }

    public void SetWallHeightKeepingBottom(float height)
    {
        wallHeight = height;
        centerY = wallBottomY + height * 0.5f;
        wallTopY = wallBottomY + height;
    }
}
