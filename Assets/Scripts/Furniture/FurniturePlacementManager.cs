using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FurniturePlacementManager : MonoBehaviour, IEditorModeInputHandler
{
    private const bool ForceValidationDebugLogs = true;

    private enum PlacementState
    {
        Idle,
        PreviewFollowing,
        PlacedSelected,
    }

    [Header("References")]
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private EditorViewModeManager viewModeManager;
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
    [SerializeField] private bool enableValidationDebugLogs = true;
    [SerializeField] private float validationDebugLogIntervalSeconds = 0.25f;

    private readonly List<Renderer> previewRenderers = new List<Renderer>();
    private readonly List<Collider> currentColliders = new List<Collider>();
    private readonly List<Room> cachedRooms = new List<Room>();
    private readonly List<Vector3> cachedRoomFootprint = new List<Vector3>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private MaterialPropertyBlock propertyBlock;
    private RaycastHit[] placementHitsBuffer = new RaycastHit[16];
    private Collider[] overlapResultsBuffer = new Collider[16];

    private PlacementState state;
    private FurnitureCatalogItem activeItem;
    private FurnitureInstance activeInstance;
    private float currentYaw;
    private bool lastPlacementValidity;
    private string lastValidationDebugMessage = string.Empty;
    private float nextValidationDebugLogTime;
    private int furnishLayer = -1;
    private float nextQRotationTime;
    private float nextERotationTime;
    private bool isFurniturePlaceModeActive;
    private IEditorInputProvider inputProvider;
    private EditorInputFrame lastInputFrame;

    private void Awake()
    {
        inputProvider = EditorInputManager.Instance.InputProvider;
        propertyBlock = new MaterialPropertyBlock();
        ResolveReferences();
        EnsureFurnitureRoot();
        EnsureCameraCulling();
        EnsurePlacementSurfaceMask();
        BindModeEvents();
        SyncModeState();
        EditorInputManager.Instance.RegisterHandler(EditorMode.FurniturePlace, this);
        ValidateConfiguration();
    }

    private void Update()
    {
        if (!isFurniturePlaceModeActive)
        {
            if (state != PlacementState.Idle)
            {
                CancelCurrentPlacement(false);
            }

            return;
        }

        if (targetCamera == null || inputProvider == null || !inputProvider.IsPointerAvailable)
        {
            return;
        }
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        lastInputFrame = inputFrame;
        if (!isFurniturePlaceModeActive || targetCamera == null || inputProvider == null || !inputFrame.IsPointerAvailable)
        {
            return;
        }

        HandleRotationInput(inputFrame);
        HandlePointerInput(inputFrame);
        HandleKeyboardActions(inputFrame);
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
        EnsurePlacementSurfaceMask();
        RefreshRoomFloorPlacementSurfaces();

        if (viewModeManager != null && viewModeManager.CurrentViewMode != EditorViewMode.Top)
        {
            viewModeManager.SetTopView();
        }

        if (modeManager != null && !modeManager.IsMode(EditorMode.FurniturePlace))
        {
            modeManager.SetMode(EditorMode.FurniturePlace);
        }

        activeItem = item;
        DestroyActivePreviewIfNeeded();
        activeInstance = CreateFurnitureInstance(item);
        if (activeInstance == null)
        {
            return;
        }

        EmitPlacementLifecycleDebug($"BeginPlacement item={item.code} prefab={item.prefab.name}");

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

    private void HandlePointerInput(EditorInputFrame inputFrame)
    {
        if (!TryGetPlacementPoint(out Vector3 placementPoint, out _))
        {
            return;
        }

        if (state == PlacementState.PreviewFollowing && activeInstance != null)
        {
            ApplyPreviewTransform(placementPoint, false);
        }

        if (!inputFrame.LeftPressedThisFrame || IsPlacementPointerBlockedByUI(inputFrame))
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

    private void HandleRotationInput(EditorInputFrame inputFrame)
    {
        if (state == PlacementState.Idle || inputProvider == null)
        {
            return;
        }

        if (inputFrame.RotateNegativePressedThisFrame)
        {
            RotateByStep(-rotationStepDegrees);
            nextQRotationTime = Time.unscaledTime + rotationRepeatDelay;
        }
        else if (inputFrame.RotateNegativePressed && Time.unscaledTime >= nextQRotationTime)
        {
            RotateByStep(-rotationStepDegrees);
            nextQRotationTime = Time.unscaledTime + rotationRepeatInterval;
        }
        else if (!inputFrame.RotateNegativePressed)
        {
            nextQRotationTime = 0f;
        }

        if (inputFrame.RotatePositivePressedThisFrame)
        {
            RotateByStep(rotationStepDegrees);
            nextERotationTime = Time.unscaledTime + rotationRepeatDelay;
        }
        else if (inputFrame.RotatePositivePressed && Time.unscaledTime >= nextERotationTime)
        {
            RotateByStep(rotationStepDegrees);
            nextERotationTime = Time.unscaledTime + rotationRepeatInterval;
        }
        else if (!inputFrame.RotatePositivePressed)
        {
            nextERotationTime = 0f;
        }
    }

    private void HandleKeyboardActions(EditorInputFrame inputFrame)
    {
        if (inputProvider == null)
        {
            return;
        }

        if (inputFrame.EscapePressedThisFrame)
        {
            CancelCurrentPlacement(false);
        }

        if (inputFrame.DeletePressedThisFrame && state == PlacementState.PlacedSelected && activeInstance != null)
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
            EmitPlacementLifecycleDebug("UpdatePlacementValidity skipped activeInstance=<null>");
            return;
        }

        bool hasSurface = TryGetPlacementPoint(out Vector3 placementPoint, out Room placementPointRoom);
        Bounds bounds = activeInstance.CalculateWorldBounds();
        Room boundsRoom = ResolveRoomForBounds(bounds);
        Room room = ResolvePlacementRoom(bounds, placementPointRoom, boundsRoom);
        bool overlaps = CheckOverlaps(activeInstance, bounds, out Collider blockingCollider);
        lastPlacementValidity = hasSurface && room != null && !overlaps;
        EmitValidationDebug(hasSurface, placementPoint, placementPointRoom, boundsRoom, room, overlaps, blockingCollider, bounds);
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

        if (LayerUtility.TryGetLayer(LayerUtility.WallLayerName, out int wallLayer))
        {
            queryMask &= ~(1 << wallLayer);
        }

        // Renderer.bounds is already a world-space AABB. Applying the furniture rotation again
        // inflates the overlap volume and causes false wall hits near angled boundaries.
        int overlapCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            halfExtents,
            overlapResultsBuffer,
            Quaternion.identity,
            queryMask,
            QueryTriggerInteraction.Ignore);
        while (overlapCount == overlapResultsBuffer.Length)
        {
            Array.Resize(ref overlapResultsBuffer, overlapResultsBuffer.Length * 2);
            overlapCount = Physics.OverlapBoxNonAlloc(
                bounds.center,
                halfExtents,
                overlapResultsBuffer,
                Quaternion.identity,
                queryMask,
                QueryTriggerInteraction.Ignore);
        }

        for (int i = 0; i < overlapCount; i++)
        {
            Collider current = overlapResultsBuffer[i];
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
        if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        Ray ray = EditorScreenCoordinateUtility.ScreenPointToRay(targetCamera, pointerScreenPosition);
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
        CachePreviewRenderers(activeInstance.gameObject);
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
        CachePreviewRenderers(activeInstance.gameObject);
        activeInstance.SetPlaced(false);
        state = PlacementState.PreviewFollowing;
        ApplyPreviewTransform(placementPoint, false);
    }

    public void RefreshRestoredFurniture()
    {
        ResolveReferences();
        EnsureFurnitureRoot();
        EnsureCameraCulling();
        EnsurePlacementSurfaceMask();
        RefreshRoomFloorPlacementSurfaces();
        previewRenderers.Clear();
        if (activeInstance != null)
        {
            activeInstance = null;
            activeItem = null;
            state = PlacementState.Idle;
        }
    }

    private bool TryGetPlacementPoint(out Vector3 point, out Room room)
    {
        point = Vector3.zero;
        room = null;

        if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        Ray ray = EditorScreenCoordinateUtility.ScreenPointToRay(targetCamera, pointerScreenPosition);
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            placementHitsBuffer,
            float.MaxValue,
            placementSurfaceMask.value,
            QueryTriggerInteraction.Ignore);
        while (hitCount == placementHitsBuffer.Length)
        {
            Array.Resize(ref placementHitsBuffer, placementHitsBuffer.Length * 2);
            hitCount = Physics.RaycastNonAlloc(
                ray,
                placementHitsBuffer,
                float.MaxValue,
                placementSurfaceMask.value,
                QueryTriggerInteraction.Ignore);
        }

        if (hitCount > 0)
        {
            bool found = false;
            RaycastHit bestHit = default;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit currentHit = placementHitsBuffer[i];
                if (!IsFloorPlacementHit(currentHit, out Room hitRoom))
                {
                    continue;
                }

                if (!found ||
                    currentHit.point.y < bestHit.point.y ||
                    (Mathf.Approximately(currentHit.point.y, bestHit.point.y) && currentHit.distance < bestHit.distance))
                {
                    bestHit = currentHit;
                    room = hitRoom;
                    found = true;
                }
            }

            if (found)
            {
                point = bestHit.point;
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

    private static bool IsFloorPlacementHit(RaycastHit hit, out Room room)
    {
        room = null;
        Collider hitCollider = hit.collider;
        if (hitCollider == null)
        {
            return false;
        }

        if (!LayerUtility.IsLayer(hitCollider.gameObject, LayerUtility.FloorLayerName))
        {
            return false;
        }

        room = hitCollider.GetComponentInParent<Room>();
        return room != null;
    }

    private Room ResolveRoomAtPosition(Vector3 worldPosition)
    {
        if (roomManager == null)
        {
            return null;
        }

        roomManager.GetAllRooms(cachedRooms);
        for (int i = 0; i < cachedRooms.Count; i++)
        {
            Room room = cachedRooms[i];
            if (room == null)
            {
                continue;
            }

            if (!TryBuildRoomPlacementFootprint(room, cachedRoomFootprint))
            {
                continue;
            }

            if (IsPointInsidePolygonXZ(worldPosition, cachedRoomFootprint))
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

        roomManager.GetAllRooms(cachedRooms);
        for (int i = 0; i < cachedRooms.Count; i++)
        {
            Room room = cachedRooms[i];
            if (room == null)
            {
                continue;
            }

            if (!TryBuildRoomPlacementFootprint(room, cachedRoomFootprint))
            {
                continue;
            }

            if (IsBoundsFootprintInsidePolygonXZ(bounds, cachedRoomFootprint))
            {
                return room;
            }
        }

        return null;
    }

    private Room ResolvePlacementRoom(Bounds bounds, Room placementPointRoom, Room boundsRoom)
    {
        if (placementPointRoom != null)
        {
            return placementPointRoom;
        }

        return boundsRoom;
    }

    private bool TryBuildRoomPlacementFootprint(Room room, List<Vector3> results)
    {
        if (results == null)
        {
            return false;
        }

        results.Clear();
        if (room == null)
        {
            return false;
        }

        IReadOnlyList<Vector3> vertices = room.BoundaryVertices;
        if (vertices == null || vertices.Count < 3)
        {
            return false;
        }

        Vector3 placementOffset = room.Data != null ? room.Data.PlacementOffset : Vector3.zero;
        for (int i = 0; i < vertices.Count; i++)
        {
            results.Add(vertices[i] + placementOffset);
        }

        return results.Count >= 3;
    }

    private void RefreshRoomFloorPlacementSurfaces()
    {
        if (roomManager == null)
        {
            return;
        }

        roomManager.GetAllRooms(cachedRooms);
        foreach (Room room in cachedRooms)
        {
            room?.RefreshVisual();
        }
    }

    private static bool IsPointInsidePolygonXZ(Vector3 point, IReadOnlyList<Vector3> polygon)
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

    private static bool IsBoundsFootprintInsidePolygonXZ(Bounds bounds, IReadOnlyList<Vector3> polygon)
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

    private void EmitValidationDebug(
        bool hasSurface,
        Vector3 placementPoint,
        Room pointerRoom,
        Room boundsRoom,
        Room placementRoom,
        bool overlaps,
        Collider blockingCollider,
        Bounds bounds)
    {
        if ((!ForceValidationDebugLogs && !enableValidationDebugLogs) || activeInstance == null)
        {
            return;
        }

        string pointerRoomName = pointerRoom != null ? pointerRoom.name : "<none>";
        string boundsRoomName = boundsRoom != null ? boundsRoom.name : "<none>";
        string placementRoomName = placementRoom != null ? placementRoom.name : "<none>";
        string blockerName = blockingCollider != null ? blockingCollider.name : "<none>";
        string blockerLayer = blockingCollider != null ? LayerMask.LayerToName(blockingCollider.gameObject.layer) : "<none>";
        string reason = ResolvePlacementDebugReason(hasSurface, pointerRoom, boundsRoom, placementRoom, overlaps);
        string message =
            $"[FurniturePlacement] item={activeInstance.name} valid={lastPlacementValidity} reason={reason} " +
            $"surface={hasSurface} pointer={FormatVector(placementPoint)} pointerRoom={pointerRoomName} " +
            $"boundsRoom={boundsRoomName} placementRoom={placementRoomName} overlaps={overlaps} " +
            $"blocker={blockerName} blockerLayer={blockerLayer} pos={FormatVector(activeInstance.transform.position)} " +
            $"boundsCenter={FormatVector(bounds.center)} boundsSize={FormatVector(bounds.size)} " +
            $"corners={FormatBoundsCorners(bounds)} rooms={BuildRoomContainmentDebug(placementPoint, bounds)}";

        if (message == lastValidationDebugMessage)
        {
            return;
        }

        if (Time.unscaledTime < nextValidationDebugLogTime)
        {
            return;
        }

        nextValidationDebugLogTime = Time.unscaledTime + Mathf.Max(0.02f, validationDebugLogIntervalSeconds);
        lastValidationDebugMessage = message;
        Debug.Log(message, activeInstance);
    }

    private void EmitPlacementLifecycleDebug(string message)
    {
        if (!ForceValidationDebugLogs && !enableValidationDebugLogs)
        {
            return;
        }

        Debug.Log($"[FurniturePlacement] {message} modeActive={isFurniturePlaceModeActive} state={state}", this);
    }

    private static string ResolvePlacementDebugReason(
        bool hasSurface,
        Room pointerRoom,
        Room boundsRoom,
        Room placementRoom,
        bool overlaps)
    {
        if (!hasSurface)
        {
            return "NoSurface";
        }

        if (placementRoom == null)
        {
            if (pointerRoom == null && boundsRoom == null)
            {
                return "PointerAndBoundsOutsideRooms";
            }

            if (pointerRoom != null && boundsRoom == null)
            {
                return "BoundsOutsidePointerRoom";
            }

            if (pointerRoom != null && boundsRoom != null && pointerRoom != boundsRoom)
            {
                return "PointerRoomAndBoundsRoomMismatch";
            }

            return "RoomRejected";
        }

        if (overlaps)
        {
            return "Overlap";
        }

        return "Valid";
    }

    private string BuildRoomContainmentDebug(Vector3 placementPoint, Bounds bounds)
    {
        if (roomManager == null)
        {
            return "<no-room-manager>";
        }

        roomManager.GetAllRooms(cachedRooms);
        if (cachedRooms.Count == 0)
        {
            return "<none>";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < cachedRooms.Count; i++)
        {
            Room room = cachedRooms[i];
            if (room == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("; ");
            }

            if (!TryBuildRoomPlacementFootprint(room, cachedRoomFootprint))
            {
                builder.Append(room.name).Append("(invalidPolygon)");
                continue;
            }

            bool pointInside = IsPointInsidePolygonXZ(placementPoint, cachedRoomFootprint);
            bool boundsInside = IsBoundsFootprintInsidePolygonXZ(bounds, cachedRoomFootprint);
            builder.Append(room.name)
                .Append("(point=").Append(pointInside)
                .Append(",bounds=").Append(boundsInside)
                .Append(",offset=").Append(FormatVector(room.Data != null ? room.Data.PlacementOffset : Vector3.zero))
                .Append(",verts=").Append(cachedRoomFootprint.Count)
                .Append(",cornerMask=").Append(BuildBoundsCornerMask(bounds, cachedRoomFootprint))
                .Append(")");
        }

        return builder.Length > 0 ? builder.ToString() : "<none>";
    }

    private static string BuildBoundsCornerMask(Bounds bounds, IReadOnlyList<Vector3> polygon)
    {
        Vector3[] corners = GetBoundsFootprintPoints(bounds);
        char[] mask = new char[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            mask[i] = IsPointInsidePolygonXZ(corners[i], polygon) ? '1' : '0';
        }

        return new string(mask);
    }

    private static string FormatBoundsCorners(Bounds bounds)
    {
        Vector3[] corners = GetBoundsFootprintPoints(bounds);
        return $"{FormatVector(corners[0])}|{FormatVector(corners[1])}|{FormatVector(corners[2])}|{FormatVector(corners[3])}|{FormatVector(corners[4])}";
    }

    private static Vector3[] GetBoundsFootprintPoints(Bounds bounds)
    {
        float y = bounds.center.y;
        return new[]
        {
            new Vector3(bounds.center.x, y, bounds.center.z),
            new Vector3(bounds.min.x, y, bounds.min.z),
            new Vector3(bounds.min.x, y, bounds.max.z),
            new Vector3(bounds.max.x, y, bounds.min.z),
            new Vector3(bounds.max.x, y, bounds.max.z),
        };
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:F2},{value.y:F2},{value.z:F2})";
    }

    private bool IsPlacementPointerBlockedByUI(EditorInputFrame inputFrame)
    {
        if (!inputFrame.PointerOverUI)
        {
            return false;
        }

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return true;
        }

        Vector2 pointerScreenPosition = inputFrame.IsPointerAvailable
            ? inputFrame.PointerScreenPosition
            : Vector2.zero;
        if (!inputFrame.IsPointerAvailable && !TryGetPointerScreenPosition(out pointerScreenPosition))
        {
            return true;
        }

        PointerEventData eventData = new PointerEventData(eventSystem)
        {
            position = pointerScreenPosition,
        };

        uiRaycastResults.Clear();
        eventSystem.RaycastAll(eventData, uiRaycastResults);
        if (uiRaycastResults.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject == null)
            {
                continue;
            }

            if (!IsNonBlockingPlacementLabel(hitObject))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsNonBlockingPlacementLabel(GameObject hitObject)
    {
        if (hitObject.GetComponentInParent<WallLengthLabelClickHandler>() != null)
        {
            return true;
        }

        string objectName = hitObject.name;
        if (objectName.StartsWith("LengthLabel_", StringComparison.Ordinal) ||
            objectName == "RoomTypeLabel" ||
            objectName == "RoomTypeLabel(Clone)")
        {
            return true;
        }

        if (hitObject.GetComponentInParent<Selectable>() != null)
        {
            return false;
        }

        bool hasTextGraphic =
            hitObject.GetComponent<Text>() != null ||
            hitObject.GetComponent("TextMeshProUGUI") != null ||
            hitObject.GetComponent("TMP_Text") != null;
        if (!hasTextGraphic)
        {
            return false;
        }

        Transform current = hitObject.transform.parent;
        while (current != null)
        {
            if (current.GetComponent<Selectable>() != null)
            {
                return false;
            }

            current = current.parent;
        }

        return objectName.IndexOf("Label", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool TryGetPointerScreenPosition(out Vector2 pointerScreenPosition)
    {
        if (lastInputFrame.IsPointerAvailable)
        {
            pointerScreenPosition = lastInputFrame.PointerScreenPosition;
            return true;
        }

        if (inputProvider != null && inputProvider.TryGetPointerScreenPosition(out pointerScreenPosition))
        {
            return true;
        }

        pointerScreenPosition = Vector2.zero;
        return false;
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            LayerUtility.ResolveObject(ref modeManager);
        }

        if (viewModeManager == null)
        {
            LayerUtility.ResolveObject(ref viewModeManager);
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (roomManager == null)
        {
            LayerUtility.ResolveObject(ref roomManager);
        }
    }

    private void EnsurePlacementSurfaceMask()
    {
        if (!LayerUtility.TryGetLayer(LayerUtility.FloorLayerName, out int floorLayer))
        {
            return;
        }

        placementSurfaceMask = placementSurfaceMask.value | (1 << floorLayer);
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
        if (isFurniturePlaceModeActive)
        {
            EnsureCameraCulling();
        }

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
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterHandler(EditorMode.FurniturePlace, this);
        }
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
