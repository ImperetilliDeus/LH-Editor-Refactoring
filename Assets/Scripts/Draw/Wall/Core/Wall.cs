using UnityEngine;

/// <summary>
/// Represents a wall segment in the editor.
/// </summary>
public class Wall : MonoBehaviour
{
    private Vector3 startPoint;
    private Vector3 endPoint;
    private int startVertexId;
    private int endVertexId;
    private bool suppressStartHandle;
    private bool suppressEndHandle;
    private bool startSplitPoint;
    private bool endSplitPoint;

    public Vector3 StartPoint
    {
        get => startPoint;
        set => startPoint = value;
    }

    public Vector3 EndPoint
    {
        get => endPoint;
        set => endPoint = value;
    }

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

    public float Length => Vector3.Distance(startPoint, endPoint);
    public bool HasValidVertexIds => startVertexId > 0 && endVertexId > 0;
    public bool SuppressStartHandle => suppressStartHandle;
    public bool SuppressEndHandle => suppressEndHandle;
    public bool IsStartSplitPoint => startSplitPoint;
    public bool IsEndSplitPoint => endSplitPoint;

    public void Initialize(Vector3 start, Vector3 end)
    {
        startPoint = start;
        endPoint = end;
    }

    public void SetVertexIds(int startId, int endId)
    {
        startVertexId = startId;
        endVertexId = endId;
    }

    public void SetHandleSuppressed(bool suppressStart, bool suppressEnd)
    {
        suppressStartHandle = suppressStart;
        suppressEndHandle = suppressEnd;
    }

    public void SetSplitPointFlags(bool isStartSplitPoint, bool isEndSplitPoint)
    {
        startSplitPoint = isStartSplitPoint;
        endSplitPoint = isEndSplitPoint;
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

    public float GetLength()
    {
        return Length;
    }

    public Vector3 GetDirection()
    {
        Vector3 dir = endPoint - startPoint;
        if (dir.magnitude > 0)
            return dir.normalized;
        return Vector3.zero;
    }

    public bool TryApplyGeometry(Vector3 start, Vector3 end, float thickness, float height, float centerY, float minimumLength)
    {
        if (!TryGetFlatGeometry(start, end, minimumLength, out Vector3 flatDirection, out float length))
        {
            return false;
        }

        Vector3 midpoint = (start + end) * 0.5f;
        midpoint.y = centerY;

        transform.SetPositionAndRotation(
            midpoint,
            Quaternion.LookRotation(flatDirection.normalized, Vector3.up));
        transform.localScale = new Vector3(thickness, height, length);

        Initialize(start, end);
        RefreshTopFaceVisual();
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
            transform.localScale.x,
            transform.localScale.y,
            transform.position.y,
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
        Initialize(start, end);
    }

    public bool TryGetSnapSegment(float planeY, float minimumLength, out SnapManager.WallSnapSegment segment)
    {
        segment = default;
        GetEndpointsFromTransform(planeY, out Vector3 start, out Vector3 end);
        if (!TryGetFlatGeometry(start, end, minimumLength, out _, out _))
        {
            return false;
        }

        segment = new SnapManager.WallSnapSegment
        {
            start = start,
            end = end,
        };
        return true;
    }

    public void RefreshLengthDisplay(WallLengthDisplay wallLengthDisplay, bool isPreview)
    {
        if (wallLengthDisplay == null)
        {
            return;
        }

        wallLengthDisplay.SetWallLength(transform, Length, transform.localScale.y, isPreview);
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
        float halfLength = transform.localScale.z * 0.5f;
        Vector3 direction = transform.forward;

        start = centerPosition - direction * halfLength;
        end = centerPosition + direction * halfLength;
        start.y = planeY;
        end.y = planeY;
    }

    public Material GetTopMaterial()
    {
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        return topFaceVisual != null ? topFaceVisual.TopMaterial : null;
    }

    public void SetTopMaterial(Material material)
    {
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        if (topFaceVisual == null)
        {
            topFaceVisual = gameObject.AddComponent<WallTopFaceVisual>();
        }

        topFaceVisual.SetTopMaterial(material);
    }

    public void SetTopFaceOffset(float offset)
    {
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        if (topFaceVisual == null)
        {
            topFaceVisual = gameObject.AddComponent<WallTopFaceVisual>();
        }

        topFaceVisual.SetWorldOffset(offset);
    }

    public void RefreshTopFaceVisual()
    {
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        if (topFaceVisual != null)
        {
            topFaceVisual.Refresh();
        }
    }

    private static bool TryGetFlatGeometry(Vector3 start, Vector3 end, float minimumLength, out Vector3 flatDirection, out float length)
    {
        flatDirection = end - start;
        flatDirection.y = 0f;
        length = flatDirection.magnitude;
        return length >= minimumLength;
    }

    private void OnDrawGizmosSelected()
    {
        // Draw wall endpoints
        Gizmos.color = Color.yellow;
        if (Application.isPlaying)
        {
            Gizmos.DrawWireSphere(startPoint, 0.1f);
            Gizmos.DrawWireSphere(endPoint, 0.1f);
            Gizmos.DrawLine(startPoint, endPoint);
        }
    }

    private void OnEnable()
    {
        WallRegistry.Register(this);

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
}
