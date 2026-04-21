using UnityEngine;

/// <summary>
/// Represents a wall segment in the editor.
/// </summary>
public class Wall : MonoBehaviour
{
    public const float DefaultTopFaceOffset = 0.01f;

    [SerializeField] private WallData data = new WallData();
    private int startVertexId;
    private int endVertexId;
    private bool suppressStartHandle;
    private bool suppressEndHandle;
    private bool startSplitPoint;
    private bool endSplitPoint;
    private static readonly System.Collections.Generic.List<Wall> connectedWalls = new System.Collections.Generic.List<Wall>();

    public int StartVertexId
    {
        get => startVertexId;
        set => startVertexId = value;
    }

    public int EndVertexId
    {
        get => endVertexId;
        set => endVertexId = value;
    }

    public WallData Data => EnsureData();
    public bool HasValidVertexIds => startVertexId > 0 && endVertexId > 0;
    public bool SuppressStartHandle => suppressStartHandle;
    public bool SuppressEndHandle => suppressEndHandle;
    public bool IsStartSplitPoint => startSplitPoint;
    public bool IsEndSplitPoint => endSplitPoint;

    public float Thickness
    {
        get => Data.thickness;
        set => Data.thickness = value;
    }

    public float Height
    {
        get => Data.height;
        set => Data.height = value;
    }

    public float CenterY
    {
        get => Data.centerY;
        set => Data.centerY = value;
    }

    public void Initialize(WallData wallData)
    {
        data = wallData ?? new WallData();
        EnsureWallId();
        WallRegistry.NotifyWallChanged(this);
    }

    public void CopyDataFrom(WallData wallData)
    {
        EnsureData().CopyFrom(wallData);
        EnsureWallId();
        WallRegistry.NotifyWallChanged(this);
    }

    public void SetVertexIds(int startId, int endId)
    {
        startVertexId = startId;
        endVertexId = endId;
        RefreshEndCapVisuals();
    }

    public void SetHandleSuppressed(bool suppressStart, bool suppressEnd)
    {
        suppressStartHandle = suppressStart;
        suppressEndHandle = suppressEnd;
        RefreshEndCapVisuals();
    }

    public void SetSplitPointFlags(bool isStartSplitPoint, bool isEndSplitPoint)
    {
        startSplitPoint = isStartSplitPoint;
        endSplitPoint = isEndSplitPoint;
        RefreshEndCapVisuals();
    }

    public bool IsSplitPointVertex(int vertexId)
    {
        return (startVertexId == vertexId && startSplitPoint) ||
               (endVertexId == vertexId && endSplitPoint);
    }

    public bool ContainsVertexId(int vertexId)
    {
        return startVertexId == vertexId || endVertexId == vertexId;
    }

    public int GetOppositeVertexId(int vertexId)
    {
        if (startVertexId == vertexId)
        {
            return endVertexId;
        }

        if (endVertexId == vertexId)
        {
            return startVertexId;
        }

        return 0;
    }

    public bool TryApplyGeometry(Vector3 start, Vector3 end, float thickness, float height, float centerY, float minimumLength)
    {
        WallData wallData = EnsureData();
        wallData.startPoint = start;
        wallData.endPoint = end;
        wallData.thickness = thickness;
        wallData.height = height;
        wallData.centerY = centerY;

        if (!GetVisual(true).TryApplyWallData(wallData, minimumLength))
        {
            return false;
        }

        WallRegistry.NotifyWallChanged(this);
        return true;
    }

    public bool TryApplyGeometryAndRefresh(
        Vector3 start,
        Vector3 end,
        float thickness,
        float height,
        float centerY,
        float minimumLength,
        WallLengthDisplay wallLengthDisplay,
        bool isPreview)
    {
        if (!TryApplyGeometry(start, end, thickness, height, centerY, minimumLength))
        {
            ClearLengthDisplay(wallLengthDisplay);
            return false;
        }

        RefreshLengthDisplay(wallLengthDisplay, isPreview);
        return true;
    }

    public bool TryApplyCurrentProfile(Vector3 start, Vector3 end, float minimumLength)
    {
        return TryApplyGeometry(
            start,
            end,
            Mathf.Max(Data.thickness, transform.localScale.x),
            Mathf.Max(Data.height, transform.localScale.y),
            Mathf.Abs(Data.centerY) > 0.000001f ? Data.centerY : transform.position.y,
            minimumLength);
    }

    public bool TryApplyCurrentProfileAndRefresh(
        Vector3 start,
        Vector3 end,
        float minimumLength,
        WallLengthDisplay wallLengthDisplay,
        bool isPreview)
    {
        if (!TryApplyCurrentProfile(start, end, minimumLength))
        {
            ClearLengthDisplay(wallLengthDisplay);
            return false;
        }

        RefreshLengthDisplay(wallLengthDisplay, isPreview);
        return true;
    }

    public void SyncEndpointsFromTransform(float planeY)
    {
        GetEndpointsFromTransform(planeY, out Vector3 start, out Vector3 end);
        WallData wallData = EnsureData();
        wallData.startPoint = start;
        wallData.endPoint = end;
        wallData.thickness = transform.localScale.x;
        wallData.height = transform.localScale.y;
        wallData.centerY = transform.position.y;
        WallRegistry.NotifyWallChanged(this);
    }

    public bool UpdateView(float minimumLength)
    {
        WallData wallData = EnsureData();
        if (!GetVisual(true).TryApplyWallData(wallData, minimumLength))
        {
            return false;
        }

        WallRegistry.NotifyWallChanged(this);
        return true;
    }

    public bool TryGetSnapSegment(float planeY, float minimumLength, out SnapManager.WallSnapSegment segment)
    {
        return WallTopologyService.TryGetSnapSegment(transform, planeY, minimumLength, out segment);
    }

    public void RefreshLengthDisplay(WallLengthDisplay wallLengthDisplay, bool isPreview)
    {
        if (wallLengthDisplay == null)
        {
            return;
        }

        wallLengthDisplay.SetWallLength(transform, Data.GetLength(), transform.localScale.y, isPreview);
    }

    public void ClearLengthDisplay(WallLengthDisplay wallLengthDisplay)
    {
        if (wallLengthDisplay == null)
        {
            return;
        }

        wallLengthDisplay.RemoveWallLabel(transform);
    }

    public void GetEndpointsFromTransform(float planeY, out Vector3 start, out Vector3 end)
    {
        GetEndpointsForCenterPosition(transform.position, planeY, out start, out end);
    }

    public void GetEndpointsForCenterPosition(Vector3 centerPosition, float planeY, out Vector3 start, out Vector3 end)
    {
        WallTopologyService.GetEndpointsForCenterPosition(transform, centerPosition, planeY, out start, out end);
    }

    public Material GetTopMaterial()
    {
        WallVisual visual = GetVisual(false);
        return visual != null ? visual.GetTopMaterial() : null;
    }

    public void SetTopMaterial(Material material)
    {
        GetVisual(true).SetTopMaterial(material);
    }

    public void SetTopFaceOffset(float offset)
    {
        GetVisual(true).SetTopFaceOffset(offset);
    }

    public void RefreshTopFaceVisual()
    {
        WallVisual visual = GetVisual(false);
        if (visual != null)
        {
            visual.RefreshTopFaceVisual();
        }
    }

    public void RefreshEndCapVisuals()
    {
        GetVisual(true).RefreshEndCapVisuals();
    }

    private void OnDrawGizmosSelected()
    {
        // Draw wall endpoints
        Gizmos.color = Color.yellow;
        if (Application.isPlaying)
        {
            Gizmos.DrawWireSphere(Data.startPoint, 0.1f);
            Gizmos.DrawWireSphere(Data.endPoint, 0.1f);
            Gizmos.DrawLine(Data.startPoint, Data.endPoint);
        }
    }

    private void OnEnable()
    {
        WallRegistry.Register(this);
        RefreshEndCapVisuals();

        if (GetComponent<Collider>() == null || GetComponent<WallSelectionUIProxy>() != null)
        {
            return;
        }

        gameObject.AddComponent<WallSelectionUIProxy>();
    }

    private void OnDisable()
    {
        WallRegistry.Unregister(this);
    }

    internal float CalculateEndpointExtension(bool isStartEndpoint)
    {
        int vertexId = isStartEndpoint ? startVertexId : endVertexId;
        WallRegistry.CollectWalls(connectedWalls, transform.parent);
        return WallTopologyService.CalculateEndpointExtension(this, vertexId, isStartEndpoint, connectedWalls);
    }

    private WallData EnsureData()
    {
        if (data == null)
        {
            data = new WallData();
        }

        EnsureWallId();
        return data;
    }

    private void EnsureWallId()
    {
        if (data != null && string.IsNullOrEmpty(data.id))
        {
            data.id = System.Guid.NewGuid().ToString("N");
        }
    }

    private WallVisual GetVisual(bool createIfMissing)
    {
        WallVisual visual = GetComponent<WallVisual>();
        if (visual == null && createIfMissing)
        {
            visual = gameObject.AddComponent<WallVisual>();
        }

        return visual;
    }
}
