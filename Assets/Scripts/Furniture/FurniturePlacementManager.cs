using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class FurniturePlacementManager : MonoBehaviour
{
    private enum PlacementState
    {
        Idle,
        PreviewFollowing,
        PlacedSelected,
    }

    [Header("References")]
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private RoomManager roomManager;

    [Header("Placement")]
    [SerializeField] private LayerMask placementSurfaceMask = ~0;
    [SerializeField] private LayerMask furnitureSelectionMask = ~0;
    [SerializeField] private float rotationStepDegrees = 15f;
    [SerializeField] private float placementYOffset = 0f;
    [SerializeField] private bool keepPlacingSameItem;
    [SerializeField] private bool snapToGrid;
    [SerializeField] private float gridSize = 10f;
    [SerializeField] private float overlapPadding = 0.05f;
    [SerializeField] private float rotationRepeatDelay = 0.2f;
    [SerializeField] private float rotationRepeatInterval = 0.08f;

    [Header("Preview")]
    [SerializeField] private Color validTint = new Color(0.45f, 1f, 0.55f, 0.85f);
    [SerializeField] private Color invalidTint = new Color(1f, 0.4f, 0.4f, 0.85f);
    [SerializeField] private bool enableValidationDebugLogs;

    private readonly List<Renderer> previewRenderers = new List<Renderer>();
    private readonly List<Collider> currentColliders = new List<Collider>();
    private MaterialPropertyBlock propertyBlock;

    private PlacementState state;
    private FurnitureCatalogItem activeItem;
    private FurnitureInstance activeInstance;
    private float currentYaw;
    private bool lastPlacementValidity;
    private string lastValidationDebugMessage = string.Empty;
    private int furnishLayer = -1;
    private float nextQRotationTime;
    private float nextERotationTime;
    private bool isFurniturePlaceModeActive;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ResolveReferences();
        EnsureFurnitureRoot();
        EnsureCameraCulling();
        BindModeEvents();
        SyncModeState();
        ValidateConfiguration();
    }

    private void Update()
    {
        EnsureCameraCulling();
        if (!isFurniturePlaceModeActive)
        {
            if (state != PlacementState.Idle)
            {
                CancelCurrentPlacement(false);
            }

            return;
        }

        if (targetCamera == null || Mouse.current == null)
        {
            return;
        }

        HandleRotationInput();
        HandlePointerInput();
        HandleKeyboardActions();
    }

    public void BeginPlacement(FurnitureCatalogItem item)
    {
        if (item == null || item.prefab == null)
        {
            return;
        }

        ResolveReferences();
        EnsureFurnitureRoot();
        EnsureCameraCulling();

        if (modeManager != null && !modeManager.IsMode(EditorMode.FurniturePlace))
        {
            modeManager.SetFurniturePlaceMode();
        }

        activeItem = item;
        DestroyActivePreviewIfNeeded();
        activeInstance = CreateFurnitureInstance(item);
        if (activeInstance == null)
        {
            return;
        }

        currentYaw = item.defaultEulerAngles.y;
        state = PlacementState.PreviewFollowing;

        if (TryGetPlacementPoint(out Vector3 placementPoint, out _))
        {
            ApplyPreviewTransform(placementPoint, false);
        }
        else
        {
            ApplyPreviewTransform(Vector3.zero, false);
        }
    }

    private void HandlePointerInput()
    {
        if (!TryGetPlacementPoint(out Vector3 placementPoint, out _))
        {
            return;
        }

        if (state == PlacementState.PreviewFollowing && activeInstance != null)
        {
            ApplyPreviewTransform(placementPoint, false);
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame || IsPointerOverUI())
        {
            return;
        }

        if (state == PlacementState.PreviewFollowing)
        {
            TryCommitPlacement();
            return;
        }

        if (TryPickFurniture(out FurnitureInstance pickedInstance))
        {
            BeginRepositionPlacedInstance(pickedInstance, placementPoint);
            return;
        }
    }

    private void HandleRotationInput()
    {
        if (state == PlacementState.Idle || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            RotateByStep(-rotationStepDegrees);
            nextQRotationTime = Time.unscaledTime + rotationRepeatDelay;
        }
        else if (Keyboard.current.qKey.isPressed && Time.unscaledTime >= nextQRotationTime)
        {
            RotateByStep(-rotationStepDegrees);
            nextQRotationTime = Time.unscaledTime + rotationRepeatInterval;
        }
        else if (!Keyboard.current.qKey.isPressed)
        {
            nextQRotationTime = 0f;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            RotateByStep(rotationStepDegrees);
            nextERotationTime = Time.unscaledTime + rotationRepeatDelay;
        }
        else if (Keyboard.current.eKey.isPressed && Time.unscaledTime >= nextERotationTime)
        {
            RotateByStep(rotationStepDegrees);
            nextERotationTime = Time.unscaledTime + rotationRepeatInterval;
        }
        else if (!Keyboard.current.eKey.isPressed)
        {
            nextERotationTime = 0f;
        }
    }

    private void HandleKeyboardActions()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelCurrentPlacement(false);
        }

        if (Keyboard.current.deleteKey.wasPressedThisFrame && state == PlacementState.PlacedSelected && activeInstance != null)
        {
            FurnitureInstance target = activeInstance;
            ClearSelectionState();
            Destroy(target.gameObject);
        }
    }

    private void TryCommitPlacement()
    {
        if (activeInstance == null || !lastPlacementValidity)
        {
            return;
        }

        activeInstance.SetPlaced(true);
        activeInstance.SetCurrentRoom(ResolveRoomAtPosition(activeInstance.transform.position));
        ClearPreviewTint();
        state = PlacementState.PlacedSelected;

        if (keepPlacingSameItem && activeItem != null)
        {
            FurnitureCatalogItem nextItem = activeItem;
            BeginPlacement(nextItem);
        }
    }

    private void CancelCurrentPlacement(bool keepSelection)
    {
        if (state == PlacementState.PreviewFollowing && activeInstance != null && !activeInstance.IsPlaced)
        {
            Destroy(activeInstance.gameObject);
        }

        if (!keepSelection)
        {
            ClearSelectionState();
        }
        else
        {
            state = PlacementState.PlacedSelected;
        }
    }

    private void ClearSelectionState()
    {
        ClearPreviewTint();
        activeItem = null;
        activeInstance = null;
        state = PlacementState.Idle;
        currentYaw = 0f;
        lastPlacementValidity = false;
        nextQRotationTime = 0f;
        nextERotationTime = 0f;
    }

    private void DestroyActivePreviewIfNeeded()
    {
        if (activeInstance == null)
        {
            return;
        }

        if (!activeInstance.IsPlaced)
        {
            Destroy(activeInstance.gameObject);
        }

        activeInstance = null;
        previewRenderers.Clear();
    }

    private FurnitureInstance CreateFurnitureInstance(FurnitureCatalogItem item)
    {
        GameObject instanceObject = Instantiate(item.prefab, furnitureRoot);
        instanceObject.name = item.prefab.name;
        LayerUtility.ApplyLayer(instanceObject, LayerUtility.FurnishLayerName, true);

        FurnitureInstance instance = instanceObject.GetComponent<FurnitureInstance>();
        if (instance == null)
        {
            instance = instanceObject.AddComponent<FurnitureInstance>();
        }

        instance.Initialize(item);
        instance.SetPlaced(false);
        instance.SetCurrentRoom(null);
        instance.ApplyLayerRecursively();
        CachePreviewRenderers(instanceObject);
        return instance;
    }

    private void ApplyPreviewTransform(Vector3 worldPosition, bool preservePosition)
    {
        if (activeInstance == null)
        {
            return;
        }

        Vector3 offset = activeInstance.PlacementOffset;
        Vector3 targetPosition = preservePosition ? activeInstance.transform.position : worldPosition + offset;
        if (snapToGrid && gridSize > 0f)
        {
            targetPosition.x = Mathf.Round(targetPosition.x / gridSize) * gridSize;
            targetPosition.z = Mathf.Round(targetPosition.z / gridSize) * gridSize;
        }

        activeInstance.transform.position = targetPosition;
        ApplyRotation();
        AlignInstanceToSurface(worldPosition.y + placementYOffset + offset.y);
        UpdatePlacementValidity();
    }

    private void ApplyRotation()
    {
        if (activeInstance == null)
        {
            return;
        }

        Vector3 euler = activeInstance.DefaultEulerAngles;
        euler.y = currentYaw;
        activeInstance.transform.rotation = Quaternion.Euler(euler);
        UpdatePlacementValidity();
    }

    private void RotateByStep(float delta)
    {
        currentYaw += delta;
        ApplyRotation();
    }

    private void UpdatePlacementValidity()
    {
        if (activeInstance == null)
        {
            lastPlacementValidity = false;
            return;
        }

        bool hasSurface = TryGetPlacementPoint(out _, out _);
        Bounds bounds = activeInstance.CalculateWorldBounds();
        Room room = ResolveRoomForBounds(bounds);
        bool overlaps = CheckOverlaps(activeInstance, bounds, out Collider blockingCollider);
        lastPlacementValidity = hasSurface && room != null && !overlaps;
        EmitValidationDebug(hasSurface, room, overlaps, blockingCollider, bounds);
        ApplyPreviewTint(lastPlacementValidity ? validTint : invalidTint);
    }

    private bool CheckOverlaps(FurnitureInstance instance, Bounds bounds, out Collider blockingCollider)
    {
        blockingCollider = null;
        Vector3 halfExtents = bounds.extents;
        halfExtents.x = Mathf.Max(0.01f, halfExtents.x - overlapPadding);
        halfExtents.y = Mathf.Max(0.01f, halfExtents.y - overlapPadding);
        halfExtents.z = Mathf.Max(0.01f, halfExtents.z - overlapPadding);

        int queryMask = Physics.DefaultRaycastLayers & ~placementSurfaceMask.value;
        if (LayerUtility.TryGetLayer(LayerUtility.CeilLayerName, out int ceilLayer))
        {
            queryMask &= ~(1 << ceilLayer);
        }

        // Renderer.bounds is already a world-space AABB. Applying the furniture rotation again
        // inflates the overlap volume and causes false wall hits near angled boundaries.
        Collider[] overlaps = Physics.OverlapBox(bounds.center, halfExtents, Quaternion.identity, queryMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider current = overlaps[i];
            if (current == null)
            {
                continue;
            }

            if (current.transform.IsChildOf(instance.transform))
            {
                continue;
            }

            blockingCollider = current;
            return true;
        }

        return false;
    }

    private bool TryPickFurniture(out FurnitureInstance pickedInstance)
    {
        pickedInstance = null;
        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, furnitureSelectionMask.value, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        pickedInstance = hit.collider != null ? hit.collider.GetComponentInParent<FurnitureInstance>() : null;
        return pickedInstance != null;
    }

    private void SelectPlacedInstance(FurnitureInstance instance)
    {
        if (instance == null)
        {
            return;
        }

        activeItem = null;
        activeInstance = instance;
        currentYaw = activeInstance.transform.eulerAngles.y;
        state = PlacementState.PlacedSelected;
        ApplyPreviewTint(validTint);
    }

    private void BeginRepositionPlacedInstance(FurnitureInstance instance, Vector3 placementPoint)
    {
        if (instance == null)
        {
            return;
        }

        if (activeInstance != null && activeInstance != instance && !activeInstance.IsPlaced)
        {
            Destroy(activeInstance.gameObject);
        }

        activeItem = null;
        activeInstance = instance;
        currentYaw = activeInstance.transform.eulerAngles.y;
        activeInstance.SetPlaced(false);
        state = PlacementState.PreviewFollowing;
        ApplyPreviewTransform(placementPoint, false);
    }

    private bool TryGetPlacementPoint(out Vector3 point, out Room room)
    {
        point = Vector3.zero;
        room = null;

        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        RaycastHit[] hits = Physics.RaycastAll(ray, float.MaxValue, placementSurfaceMask.value, QueryTriggerInteraction.Ignore);
        if (hits != null && hits.Length > 0)
        {
            bool found = false;
            RaycastHit bestHit = default;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit currentHit = hits[i];
                if (!found ||
                    currentHit.point.y < bestHit.point.y ||
                    (Mathf.Approximately(currentHit.point.y, bestHit.point.y) && currentHit.distance < bestHit.distance))
                {
                    bestHit = currentHit;
                    found = true;
                }
            }

            if (found)
            {
                point = bestHit.point;
                room = ResolveRoomAtPosition(point);
                return true;
            }
        }

        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float enter))
        {
            return false;
        }

        point = ray.GetPoint(enter);
        room = ResolveRoomAtPosition(point);
        return true;
    }

    private Room ResolveRoomAtPosition(Vector3 worldPosition)
    {
        if (roomManager == null)
        {
            return null;
        }

        List<Room> rooms = roomManager.GetAllRooms();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null)
            {
                continue;
            }

            List<Vector3> vertices = new List<Vector3>();
            if (!room.TryGetOrderedVertices(vertices))
            {
                continue;
            }

            if (IsPointInsidePolygonXZ(worldPosition, vertices))
            {
                return room;
            }
        }

        return null;
    }

    private Room ResolveRoomForBounds(Bounds bounds)
    {
        if (roomManager == null)
        {
            return null;
        }

        List<Room> rooms = roomManager.GetAllRooms();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null)
            {
                continue;
            }

            List<Vector3> vertices = new List<Vector3>();
            if (!room.TryGetOrderedVertices(vertices))
            {
                continue;
            }

            if (IsBoundsFootprintInsidePolygonXZ(bounds, vertices))
            {
                return room;
            }
        }

        return null;
    }

    private static bool IsPointInsidePolygonXZ(Vector3 point, List<Vector3> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        bool inside = false;
        float x = point.x;
        float z = point.z;

        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector3 pi = polygon[i];
            Vector3 pj = polygon[j];

            bool intersects = ((pi.z > z) != (pj.z > z)) &&
                              (x < (pj.x - pi.x) * (z - pi.z) / Mathf.Max(0.000001f, pj.z - pi.z) + pi.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsBoundsFootprintInsidePolygonXZ(Bounds bounds, List<Vector3> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        float y = bounds.center.y;
        Vector3[] testPoints =
        {
            new Vector3(bounds.center.x, y, bounds.center.z),
            new Vector3(bounds.min.x, y, bounds.min.z),
            new Vector3(bounds.min.x, y, bounds.max.z),
            new Vector3(bounds.max.x, y, bounds.min.z),
            new Vector3(bounds.max.x, y, bounds.max.z),
        };

        for (int i = 0; i < testPoints.Length; i++)
        {
            if (!IsPointInsidePolygonXZ(testPoints[i], polygon))
            {
                return false;
            }
        }

        return true;
    }

    private void CachePreviewRenderers(GameObject root)
    {
        previewRenderers.Clear();
        previewRenderers.AddRange(root.GetComponentsInChildren<Renderer>(true));
    }

    private void ApplyPreviewTint(Color tint)
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        for (int i = 0; i < previewRenderers.Count; i++)
        {
            Renderer renderer = previewRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);
            if (renderer.sharedMaterial != null)
            {
                if (renderer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    propertyBlock.SetColor("_BaseColor", tint);
                }

                if (renderer.sharedMaterial.HasProperty("_Color"))
                {
                    propertyBlock.SetColor("_Color", tint);
                }
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void ClearPreviewTint()
    {
        for (int i = 0; i < previewRenderers.Count; i++)
        {
            Renderer renderer = previewRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.SetPropertyBlock(null);
        }
    }

    private void AlignInstanceToSurface(float surfaceY)
    {
        if (activeInstance == null)
        {
            return;
        }

        Bounds bounds = activeInstance.CalculateWorldBounds();
        Vector3 position = activeInstance.transform.position;
        position.y += surfaceY - bounds.min.y;
        activeInstance.transform.position = position;
    }

    private void EmitValidationDebug(bool hasSurface, Room room, bool overlaps, Collider blockingCollider, Bounds bounds)
    {
        if (!enableValidationDebugLogs || activeInstance == null)
        {
            return;
        }

        string roomName = room != null ? room.name : "<none>";
        string blockerName = blockingCollider != null ? blockingCollider.name : "<none>";
        string blockerLayer = blockingCollider != null ? LayerMask.LayerToName(blockingCollider.gameObject.layer) : "<none>";
        string message =
            $"[FurniturePlacement] item={activeInstance.name} valid={lastPlacementValidity} hasSurface={hasSurface} room={roomName} overlaps={overlaps} blocker={blockerName} blockerLayer={blockerLayer} pos={activeInstance.transform.position} boundsCenter={bounds.center} boundsSize={bounds.size}";

        if (message == lastValidationDebugMessage)
        {
            return;
        }

        lastValidationDebugMessage = message;
        Debug.Log(message, activeInstance);
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            modeManager = FindFirstObjectByType<ModeManager>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }
    }

    private void BindModeEvents()
    {
        if (modeManager == null)
        {
            return;
        }

        modeManager.ModeChanged -= HandleModeChanged;
        modeManager.ModeChanged += HandleModeChanged;
    }

    private void UnbindModeEvents()
    {
        if (modeManager == null)
        {
            return;
        }

        modeManager.ModeChanged -= HandleModeChanged;
    }

    private void HandleModeChanged(EditorMode mode)
    {
        isFurniturePlaceModeActive = mode == EditorMode.FurniturePlace;
        enabled = isFurniturePlaceModeActive || state != PlacementState.Idle;
    }

    private void SyncModeState()
    {
        HandleModeChanged(modeManager != null ? modeManager.CurrentMode : EditorMode.Default);
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(modeManager != null, $"{nameof(FurniturePlacementManager)} requires {nameof(modeManager)}.", this);
        Debug.Assert(targetCamera != null, $"{nameof(FurniturePlacementManager)} requires {nameof(targetCamera)}.", this);
        Debug.Assert(roomManager != null, $"{nameof(FurniturePlacementManager)} requires {nameof(roomManager)}.", this);
    }

    private void OnDestroy()
    {
        UnbindModeEvents();
    }

    private void EnsureFurnitureRoot()
    {
        if (furnitureRoot != null)
        {
            return;
        }

        Transform existing = LayerUtility.FindTransformByName("FurnitureRoot", true);
        if (existing != null)
        {
            furnitureRoot = existing;
            return;
        }

        GameObject rootObject = new GameObject("FurnitureRoot");
        furnitureRoot = rootObject.transform;
    }

    private void EnsureCameraCulling()
    {
        if (furnishLayer < 0)
        {
            furnishLayer = LayerMask.NameToLayer(LayerUtility.FurnishLayerName);
        }

        if (furnishLayer < 0)
        {
            return;
        }

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int furnishMask = 1 << furnishLayer;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
            {
                continue;
            }

            camera.cullingMask |= furnishMask;
        }

        if (furnitureSelectionMask.value == ~0)
        {
            furnitureSelectionMask = furnishMask;
        }
    }
}
