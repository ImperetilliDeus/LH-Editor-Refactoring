using UnityEngine;

/// <summary>
/// Represents a wall segment in the editor.
/// </summary>
public class Wall : MonoBehaviour
{
    public const float DefaultTopFaceOffset = 0.01f;

    private const string StartCapObjectName = "WallStartCap";
    private const string EndCapObjectName = "WallEndCap";
    private const float MinimumExtension = 0.0001f;
    private const float ParallelThreshold = 0.0001f;

    [SerializeField] private WallData data = new WallData();
    private int startVertexId;
    private int endVertexId;
    private bool suppressStartHandle;
    private bool suppressEndHandle;
    private bool startSplitPoint;
    private bool endSplitPoint;
    private static Mesh sharedCubeMesh;
    private static readonly System.Collections.Generic.List<Wall> connectedWalls = new System.Collections.Generic.List<Wall>();

    private Transform startCapTransform;
    private Transform endCapTransform;
    private MeshRenderer startCapRenderer;
    private MeshRenderer endCapRenderer;
    private MeshFilter startCapFilter;
    private MeshFilter endCapFilter;
    private float topFaceWorldOffset = DefaultTopFaceOffset;

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

        WallData wallData = EnsureData();
        wallData.startPoint = start;
        wallData.endPoint = end;
        wallData.thickness = thickness;
        wallData.height = height;
        wallData.centerY = centerY;
        RefreshTopFaceVisual();
        RefreshEndCapVisuals();
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
        return TryApplyGeometry(
            wallData.startPoint,
            wallData.endPoint,
            wallData.thickness,
            wallData.height,
            wallData.centerY,
            minimumLength);
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
        RefreshEndCapVisuals();
    }

    public void SetTopFaceOffset(float offset)
    {
        topFaceWorldOffset = Mathf.Max(0f, offset);
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        if (topFaceVisual == null)
        {
            topFaceVisual = gameObject.AddComponent<WallTopFaceVisual>();
        }

        topFaceVisual.SetWorldOffset(topFaceWorldOffset);
        RefreshEndCapVisuals();
    }

    public void RefreshTopFaceVisual()
    {
        WallTopFaceVisual topFaceVisual = GetComponent<WallTopFaceVisual>();
        if (topFaceVisual != null)
        {
            topFaceVisual.Refresh();
        }
    }

    public void RefreshEndCapVisuals()
    {
        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
        Material sharedMaterial = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
        bool visible = sharedMaterial != null;

        EnsureEndCap(ref startCapTransform, ref startCapFilter, ref startCapRenderer, StartCapObjectName);
        EnsureEndCap(ref endCapTransform, ref endCapFilter, ref endCapRenderer, EndCapObjectName);

        if (startCapRenderer != null)
        {
            startCapRenderer.sharedMaterial = sharedMaterial;
            startCapRenderer.gameObject.SetActive(visible && !suppressStartHandle);
        }

        if (endCapRenderer != null)
        {
            endCapRenderer.sharedMaterial = sharedMaterial;
            endCapRenderer.gameObject.SetActive(visible && !suppressEndHandle);
        }

        float length = Mathf.Max(0.0001f, transform.localScale.z);
        float startExtension = suppressStartHandle ? 0f : CalculateEndpointExtension(true);
        float endExtension = suppressEndHandle ? 0f : CalculateEndpointExtension(false);

        ApplyCapVisual(
            startCapTransform,
            startCapRenderer,
            startExtension,
            length,
            -0.5f,
            visible && !suppressStartHandle,
            true);

        ApplyCapVisual(
            endCapTransform,
            endCapRenderer,
            endExtension,
            length,
            0.5f,
            visible && !suppressEndHandle,
            false);
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

    private float CalculateEndpointExtension(bool isStartEndpoint)
    {
        int vertexId = isStartEndpoint ? startVertexId : endVertexId;
        Vector2 currentOutwardDirection = GetEndpointOutwardDirection2D(isStartEndpoint);
        if (vertexId <= 0 || currentOutwardDirection.sqrMagnitude <= 0.000001f)
        {
            return transform.localScale.x * 0.5f;
        }

        float extension = transform.localScale.x * 0.5f;
        WallRegistry.CollectWalls(connectedWalls, transform.parent);
        for (int i = 0; i < connectedWalls.Count; i++)
        {
            Wall other = connectedWalls[i];
            if (other == null || other == this || !other.ContainsVertexId(vertexId))
            {
                continue;
            }

            extension = Mathf.Max(extension, CalculateJoinExtensionWith(other, vertexId, currentOutwardDirection));
        }

        return extension;
    }

    private float CalculateJoinExtensionWith(Wall other, int sharedVertexId, Vector2 currentOutwardDirection)
    {
        if (!TryGetVertexWorldPoint(sharedVertexId, out Vector3 sharedPoint3))
        {
            return transform.localScale.x * 0.5f;
        }

        if (!TryGetOutwardDirectionForVertex(this, sharedVertexId, out Vector2 thisOutward) ||
            !TryGetOutwardDirectionForVertex(other, sharedVertexId, out Vector2 otherOutward))
        {
            return other.transform.localScale.x * 0.5f;
        }

        Vector2 sharedPoint = new Vector2(sharedPoint3.x, sharedPoint3.z);
        Vector2 thisNormal = new Vector2(-thisOutward.y, thisOutward.x);
        Vector2 otherNormal = new Vector2(-otherOutward.y, otherOutward.x);

        float fallbackExtension = other.transform.localScale.x * 0.5f;
        float bestExtension = fallbackExtension;

        for (int currentSign = -1; currentSign <= 1; currentSign += 2)
        {
            Vector2 currentSideOrigin = sharedPoint + thisNormal * (currentSign * transform.localScale.x * 0.5f);
            for (int otherSign = -1; otherSign <= 1; otherSign += 2)
            {
                Vector2 otherSideOrigin = sharedPoint + otherNormal * (otherSign * other.transform.localScale.x * 0.5f);
                if (!TryIntersectRays(currentSideOrigin, thisOutward, otherSideOrigin, otherOutward, out float currentDistance, out float otherDistance))
                {
                    continue;
                }

                if (currentDistance < 0f || otherDistance < 0f)
                {
                    continue;
                }

                bestExtension = Mathf.Max(bestExtension, currentDistance);
            }
        }

        return bestExtension;
    }

    private Vector2 GetEndpointOutwardDirection2D(bool isStartEndpoint)
    {
        Vector3 direction = Data.GetDirection();
        Vector2 forward = new Vector2(direction.x, direction.z);
        if (forward.sqrMagnitude <= 0.000001f)
        {
            return Vector2.zero;
        }

        return isStartEndpoint ? -forward.normalized : forward.normalized;
    }

    private bool TryGetVertexWorldPoint(int vertexId, out Vector3 point)
    {
        if (startVertexId == vertexId)
        {
            point = Data.startPoint;
            return true;
        }

        if (endVertexId == vertexId)
        {
            point = Data.endPoint;
            return true;
        }

        point = Vector3.zero;
        return false;
    }

    private static bool TryGetOutwardDirectionForVertex(Wall wall, int vertexId, out Vector2 outwardDirection)
    {
        outwardDirection = Vector2.zero;
        if (wall == null)
        {
            return false;
        }

        Vector3 direction3 = wall.Data.GetDirection();
        Vector2 direction = new Vector2(direction3.x, direction3.z);
        if (direction.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        direction.Normalize();
        if (wall.StartVertexId == vertexId)
        {
            outwardDirection = direction;
            return true;
        }

        if (wall.EndVertexId == vertexId)
        {
            outwardDirection = -direction;
            return true;
        }

        return false;
    }

    private static bool TryIntersectRays(
        Vector2 firstOrigin,
        Vector2 firstDirection,
        Vector2 secondOrigin,
        Vector2 secondDirection,
        out float firstDistance,
        out float secondDistance)
    {
        firstDistance = 0f;
        secondDistance = 0f;

        float cross = Cross(firstDirection, secondDirection);
        if (Mathf.Abs(cross) <= ParallelThreshold)
        {
            return false;
        }

        Vector2 delta = secondOrigin - firstOrigin;
        firstDistance = Cross(delta, secondDirection) / cross;
        secondDistance = Cross(delta, firstDirection) / cross;
        return true;
    }

    private void ApplyCapVisual(
        Transform capTransform,
        MeshRenderer capRenderer,
        float extension,
        float wallLength,
        float localZ,
        bool shouldBeVisible,
        bool isStartCap)
    {
        if (capTransform == null)
        {
            return;
        }

        bool hasExtension = extension > MinimumExtension;
        if (capRenderer != null)
        {
            capRenderer.gameObject.SetActive(shouldBeVisible && hasExtension);
        }

        if (!shouldBeVisible || !hasExtension)
        {
            return;
        }

        float capDepthRatio = (extension * 2f) / wallLength;
        capTransform.localPosition = new Vector3(0f, 0f, localZ);
        capTransform.localRotation = Quaternion.identity;
        capTransform.localScale = new Vector3(1f, 1f, capDepthRatio);

        WallTopFaceVisual capTopVisual = capTransform.GetComponent<WallTopFaceVisual>();
        if (capTopVisual == null)
        {
            capTopVisual = capTransform.gameObject.AddComponent<WallTopFaceVisual>();
        }

        capTopVisual.SetTopMaterial(GetTopMaterial());
        capTopVisual.SetWorldOffset(topFaceWorldOffset);
    }

    private static float Cross(Vector2 left, Vector2 right)
    {
        return left.x * right.y - left.y * right.x;
    }

    private void EnsureEndCap(
        ref Transform capTransform,
        ref MeshFilter meshFilter,
        ref MeshRenderer meshRenderer,
        string objectName)
    {
        if (capTransform == null)
        {
            Transform existing = transform.Find(objectName);
            capTransform = existing != null ? existing : new GameObject(objectName).transform;
            capTransform.SetParent(transform, false);
            capTransform.gameObject.layer = gameObject.layer;
        }

        if (meshFilter == null)
        {
            meshFilter = capTransform.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = capTransform.gameObject.AddComponent<MeshFilter>();
            }
        }

        if (meshRenderer == null)
        {
            meshRenderer = capTransform.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = capTransform.gameObject.AddComponent<MeshRenderer>();
            }
        }

        meshFilter.sharedMesh = GetSharedCubeMesh();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;
        meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
        meshRenderer.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.Object;
    }

    private static Mesh GetSharedCubeMesh()
    {
        if (sharedCubeMesh != null)
        {
            return sharedCubeMesh;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter meshFilter = cube.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            sharedCubeMesh = meshFilter.sharedMesh;
        }

        if (Application.isPlaying)
        {
            Destroy(cube);
        }
        else
        {
            DestroyImmediate(cube);
        }

        return sharedCubeMesh;
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
}
