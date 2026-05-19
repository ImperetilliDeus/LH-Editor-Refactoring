using System.Collections.Generic;
using UnityEngine;

public sealed class PerspectiveCameraFramingController : MonoBehaviour
{
    private const string DefaultFurnitureRootName = "FurnitureRoot";
    private const float BoundsEpsilon = 0.0001f;

    [SerializeField] private EditorViewModeManager viewModeManager;
    [SerializeField] private Camera perspectiveCamera;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private GameObject gridObject;
    [SerializeField] private float defaultYaw = -35f;
    [SerializeField] private float defaultPitch = 45f;
    [SerializeField] private float distancePadding = 1.2f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 2500f;

    private readonly List<Room> cachedRooms = new List<Room>();
    private readonly List<Vector3> cachedRoomVertices = new List<Vector3>();
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
    }

    public bool FocusCurrentSelectionOrScene()
    {
        return TryGetSceneBounds(out Bounds bounds) && FrameBounds(bounds);
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
        return true;
    }

    public bool TryGetSceneBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        EncapsulateHierarchyBounds(wallRoot, ref bounds, ref hasBounds);
        EncapsulateRoomBounds(ref bounds, ref hasBounds);
        EncapsulateHierarchyBounds(furnitureRoot, ref bounds, ref hasBounds);
        EncapsulateGameObjectBounds(gridObject, ref bounds, ref hasBounds);

        return hasBounds;
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref viewModeManager);
        LayerUtility.ResolveObject(ref perspectiveCamera);
        LayerUtility.ResolveTransformByName(ref wallRoot, LayerUtility.DefaultWallRootName, true);
        LayerUtility.ResolveObject(ref roomManager);
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

    private static void EncapsulateHierarchyBounds(Transform root, ref Bounds bounds, ref bool hasBounds)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer != null)
            {
                EncapsulateBounds(renderer.bounds, ref bounds, ref hasBounds);
            }
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null)
            {
                EncapsulateBounds(collider.bounds, ref bounds, ref hasBounds);
            }
        }
    }

    private static void EncapsulateGameObjectBounds(GameObject target, ref Bounds bounds, ref bool hasBounds)
    {
        if (target == null)
        {
            return;
        }

        EncapsulateHierarchyBounds(target.transform, ref bounds, ref hasBounds);
    }

    private static void EncapsulateBounds(Bounds candidate, ref Bounds bounds, ref bool hasBounds)
    {
        if (candidate.extents.sqrMagnitude <= BoundsEpsilon * BoundsEpsilon)
        {
            return;
        }

        if (!hasBounds)
        {
            bounds = candidate;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(candidate);
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
}
