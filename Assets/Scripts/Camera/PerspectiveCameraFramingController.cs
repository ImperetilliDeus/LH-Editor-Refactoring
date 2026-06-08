using System.Collections.Generic;
using UnityEngine;

public sealed class PerspectiveCameraFramingController : MonoBehaviour
{
    private const string DefaultFurnitureRootName = "FurnitureRoot";
    private const float BoundsEpsilon = 0.0001f;
    private const float SelectionMinimumHeight = 0.1f;
    private const float SelectionMinimumThickness = 0.1f;

    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private Camera perspectiveCamera;
    [SerializeField] private CameraManager_3D perspectiveCameraManager;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private GameObject gridObject;
    [SerializeField] private bool includeGridInSceneBounds;
    [SerializeField] private Vector3 emptySceneFallbackBoundsCenter = Vector3.zero;
    [SerializeField] private Vector3 emptySceneFallbackBoundsSize = new Vector3(100f, 10f, 100f);
    [SerializeField] private float defaultYaw = -35f;
    [SerializeField] private float defaultPitch = 45f;
    [SerializeField] private float distancePadding = 1.2f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 2500f;

    private readonly List<Room> cachedRooms = new List<Room>();
    private readonly List<Vector3> cachedRoomVertices = new List<Vector3>();
    private readonly List<GameObject> cachedSelectedWalls = new List<GameObject>();
    private bool warnedMissingBounds;
    private bool eventsBound;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnValidate()
    {
        distancePadding = Mathf.Max(0.01f, distancePadding);
        minDistance = Mathf.Max(0.01f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        emptySceneFallbackBoundsSize = new Vector3(
            Mathf.Max(0.01f, emptySceneFallbackBoundsSize.x),
            Mathf.Max(0.01f, emptySceneFallbackBoundsSize.y),
            Mathf.Max(0.01f, emptySceneFallbackBoundsSize.z));
    }

    public bool FocusCurrentSelectionOrScene()
    {
        bool hasSelectionBounds = TryGetSelectionBounds(out Bounds selectionBounds);
        if (hasSelectionBounds)
        {
            return FrameSelectionOrSceneBoundsForTests(selectionBounds, true, default, false);
        }

        bool hasSceneBounds = TryGetSceneBounds(out Bounds sceneBounds);
        return FrameSelectionOrSceneBoundsForTests(selectionBounds, hasSelectionBounds, sceneBounds, hasSceneBounds);
    }

    public bool FrameBounds(Bounds bounds)
    {
        if (perspectiveCamera == null || bounds.extents.sqrMagnitude <= BoundsEpsilon * BoundsEpsilon)
        {
            return false;
        }

        float verticalHalfFovRadians = perspectiveCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float horizontalHalfFovRadians = Mathf.Atan(Mathf.Tan(verticalHalfFovRadians) * Mathf.Max(perspectiveCamera.aspect, BoundsEpsilon));
        float limitingHalfFovRadians = Mathf.Min(verticalHalfFovRadians, horizontalHalfFovRadians);
        float sinHalfFov = Mathf.Sin(limitingHalfFovRadians);
        if (sinHalfFov <= BoundsEpsilon)
        {
            return false;
        }

        Vector3 center = bounds.center;
        Quaternion framingRotation = Quaternion.Euler(defaultPitch, defaultYaw, 0f);
        Vector3 forward = framingRotation * Vector3.forward;
        float unclampedDistance = bounds.extents.magnitude / sinHalfFov * distancePadding;
        float distance = Mathf.Clamp(unclampedDistance, minDistance, maxDistance);
        Transform cameraTransform = perspectiveCamera.transform;

        cameraTransform.position = center - forward * distance;
        cameraTransform.LookAt(center, Vector3.up);
        SyncPerspectiveCameraManagerRotation();
        return true;
    }

    public bool TryGetSceneBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        EncapsulateHierarchyBounds(wallRoot, ref bounds, ref hasBounds);
        EncapsulateRoomBounds(ref bounds, ref hasBounds);
        EncapsulateHierarchyBounds(furnitureRoot, ref bounds, ref hasBounds);

        if (includeGridInSceneBounds)
        {
            EncapsulateGameObjectBounds(gridObject, ref bounds, ref hasBounds);
        }

        return hasBounds || TryGetEmptySceneFallbackBounds(out bounds);
    }

    public bool TryGetSelectionBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (TryGetSelectedRoomBounds(out Bounds roomBounds))
        {
            bounds = roomBounds;
            return true;
        }

        if (wallSelectionManager == null)
        {
            return false;
        }

        GameObject primaryWall = wallSelectionManager.SelectedWall;
        EncapsulateSelectedWallBounds(primaryWall, ref bounds, ref hasBounds);

        cachedSelectedWalls.Clear();
        wallSelectionManager.GetSelectedWalls(cachedSelectedWalls);
        for (int i = 0; i < cachedSelectedWalls.Count; i++)
        {
            GameObject selectedWall = cachedSelectedWalls[i];
            if (selectedWall == null || selectedWall == primaryWall)
            {
                continue;
            }

            EncapsulateSelectedWallBounds(selectedWall, ref bounds, ref hasBounds);
        }

        cachedSelectedWalls.Clear();
        return hasBounds;
    }

    public bool FrameSelectionOrSceneBoundsForTests(Bounds selectionBounds, bool hasSelectionBounds, Bounds sceneBounds, bool hasSceneBounds)
    {
        if (hasSelectionBounds)
        {
            return FrameBounds(selectionBounds);
        }

        return hasSceneBounds && FrameBounds(sceneBounds);
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref viewModeManager);
        LayerUtility.ResolveObject(ref perspectiveCamera);
        ResolvePerspectiveCameraManager();
        LayerUtility.ResolveWallRoot(ref wallRoot, true);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveObject(ref roomAuthoringPanelManager);
        LayerUtility.ResolveTransformByName(ref furnitureRoot, DefaultFurnitureRootName, true);

        if (gridObject == null)
        {
            Transform gridTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultGridName, true);
            if (gridTransform != null)
            {
                gridObject = gridTransform.gameObject;
            }
        }
    }

    private void ResolvePerspectiveCameraManager()
    {
        if (perspectiveCameraManager != null)
        {
            return;
        }

        if (perspectiveCamera != null)
        {
            perspectiveCameraManager = perspectiveCamera.GetComponent<CameraManager_3D>();
        }

        LayerUtility.ResolveObject(ref perspectiveCameraManager);
    }

    private void SyncPerspectiveCameraManagerRotation()
    {
        ResolvePerspectiveCameraManager();
        perspectiveCameraManager?.SyncRotationFromCameraTransform();
    }

    private void BindEvents()
    {
        if (eventsBound || viewModeManager == null)
        {
            return;
        }

        viewModeManager.ViewModeChanged += HandleViewModeChanged;
        eventsBound = true;
    }

    private void UnbindEvents()
    {
        if (!eventsBound || viewModeManager == null)
        {
            eventsBound = false;
            return;
        }

        viewModeManager.ViewModeChanged -= HandleViewModeChanged;
        eventsBound = false;
    }

    private void HandleViewModeChanged(EditorViewMode viewMode)
    {
        if (!isActiveAndEnabled || viewMode != EditorViewMode.Perspective3D)
        {
            return;
        }

        if (FocusCurrentSelectionOrScene())
        {
            warnedMissingBounds = false;
            return;
        }

        if (!warnedMissingBounds)
        {
            Debug.LogWarning($"{nameof(PerspectiveCameraFramingController)} could not find scene bounds to frame.", this);
            warnedMissingBounds = true;
        }
    }

    private bool TryGetSelectedRoomBounds(out Bounds bounds)
    {
        bounds = default;
        Room selectedRoom = roomAuthoringPanelManager != null ? roomAuthoringPanelManager.SelectedRoom : null;
        if (selectedRoom == null)
        {
            return false;
        }

        cachedRoomVertices.Clear();
        if (!selectedRoom.TryGetOrderedVertices(cachedRoomVertices))
        {
            IReadOnlyList<Vector3> boundaryVertices = selectedRoom.BoundaryVertices;
            if (boundaryVertices != null)
            {
                for (int i = 0; i < boundaryVertices.Count; i++)
                {
                    cachedRoomVertices.Add(boundaryVertices[i]);
                }
            }
        }

        if (cachedRoomVertices.Count < 3)
        {
            cachedRoomVertices.Clear();
            return false;
        }

        bool hasBounds = false;
        for (int i = 0; i < cachedRoomVertices.Count; i++)
        {
            EncapsulatePoint(cachedRoomVertices[i], ref bounds, ref hasBounds);
        }

        cachedRoomVertices.Clear();
        EnsureMinimumSelectionSize(ref bounds);
        return hasBounds;
    }

    private static void EncapsulateSelectedWallBounds(GameObject wallObject, ref Bounds bounds, ref bool hasBounds)
    {
        if (wallObject == null)
        {
            return;
        }

        if (TryEncapsulateHierarchyBounds(wallObject.transform, ref bounds, ref hasBounds))
        {
            return;
        }

        Wall wall = wallObject.GetComponent<Wall>();
        if (wall == null)
        {
            return;
        }

        EncapsulateWallDataBounds(wall.Data, ref bounds, ref hasBounds);
    }

    private static void EncapsulateWallDataBounds(WallData wallData, ref Bounds bounds, ref bool hasBounds)
    {
        if (wallData == null)
        {
            return;
        }

        float height = Mathf.Max(Mathf.Abs(wallData.height), SelectionMinimumHeight);
        float halfHeight = height * 0.5f;
        float centerY = wallData.centerY;
        Vector3 startBottom = new Vector3(wallData.startPoint.x, centerY - halfHeight, wallData.startPoint.z);
        Vector3 startTop = new Vector3(wallData.startPoint.x, centerY + halfHeight, wallData.startPoint.z);
        Vector3 endBottom = new Vector3(wallData.endPoint.x, centerY - halfHeight, wallData.endPoint.z);
        Vector3 endTop = new Vector3(wallData.endPoint.x, centerY + halfHeight, wallData.endPoint.z);
        Bounds wallBounds = default;
        bool hasWallBounds = false;

        EncapsulatePoint(startBottom, ref wallBounds, ref hasWallBounds);
        EncapsulatePoint(startTop, ref wallBounds, ref hasWallBounds);
        EncapsulatePoint(endBottom, ref wallBounds, ref hasWallBounds);
        EncapsulatePoint(endTop, ref wallBounds, ref hasWallBounds);

        if (hasWallBounds)
        {
            float thickness = Mathf.Max(Mathf.Abs(wallData.thickness), SelectionMinimumThickness);
            wallBounds.Expand(new Vector3(thickness, 0f, thickness));
            EncapsulateBounds(wallBounds, ref bounds, ref hasBounds);
        }
    }

    private void EncapsulateRoomBounds(ref Bounds bounds, ref bool hasBounds)
    {
        if (roomManager == null)
        {
            return;
        }

        roomManager.GetAllRooms(cachedRooms);
        for (int i = 0; i < cachedRooms.Count; i++)
        {
            Room room = cachedRooms[i];
            if (room == null || !room.TryGetOrderedVertices(cachedRoomVertices))
            {
                continue;
            }

            for (int j = 0; j < cachedRoomVertices.Count; j++)
            {
                EncapsulatePoint(cachedRoomVertices[j], ref bounds, ref hasBounds);
            }
        }
    }

    private bool TryGetEmptySceneFallbackBounds(out Bounds bounds)
    {
        bounds = new Bounds(emptySceneFallbackBoundsCenter, emptySceneFallbackBoundsSize);
        return bounds.extents.sqrMagnitude > BoundsEpsilon * BoundsEpsilon;
    }

    private static void EncapsulateHierarchyBounds(Transform root, ref Bounds bounds, ref bool hasBounds)
    {
        TryEncapsulateHierarchyBounds(root, ref bounds, ref hasBounds);
    }

    private static bool TryEncapsulateHierarchyBounds(Transform root, ref Bounds bounds, ref bool hasBounds)
    {
        if (root == null)
        {
            return false;
        }

        bool foundBounds = false;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                foundBounds |= EncapsulateBounds(renderer.bounds, ref bounds, ref hasBounds);
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null)
            {
                foundBounds |= EncapsulateBounds(collider.bounds, ref bounds, ref hasBounds);
            }
        }

        return foundBounds;
    }

    private static void EncapsulateGameObjectBounds(GameObject target, ref Bounds bounds, ref bool hasBounds)
    {
        if (target == null)
        {
            return;
        }

        EncapsulateHierarchyBounds(target.transform, ref bounds, ref hasBounds);
    }

    private static bool EncapsulateBounds(Bounds candidate, ref Bounds bounds, ref bool hasBounds)
    {
        if (candidate.extents.sqrMagnitude <= BoundsEpsilon * BoundsEpsilon)
        {
            return false;
        }

        if (!hasBounds)
        {
            bounds = candidate;
            hasBounds = true;
            return true;
        }

        bounds.Encapsulate(candidate);
        return true;
    }

    private static void EncapsulatePoint(Vector3 point, ref Bounds bounds, ref bool hasBounds)
    {
        if (!hasBounds)
        {
            bounds = new Bounds(point, Vector3.zero);
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(point);
    }

    private static void EnsureMinimumSelectionSize(ref Bounds bounds)
    {
        Vector3 size = bounds.size;
        bool shouldExpand = false;

        if (size.x <= BoundsEpsilon)
        {
            size.x = SelectionMinimumThickness;
            shouldExpand = true;
        }

        if (size.y <= BoundsEpsilon)
        {
            size.y = SelectionMinimumHeight;
            shouldExpand = true;
        }

        if (size.z <= BoundsEpsilon)
        {
            size.z = SelectionMinimumThickness;
            shouldExpand = true;
        }

        if (shouldExpand)
        {
            bounds.size = size;
        }
    }
}
