using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class TopViewRenderManager
{
    private void RefreshAllVisuals()
    {
        if (topViewCamera == null || contentRoot == null || wallRoot == null)
        {
            return;
        }

        RefreshFloorVisuals();
        RefreshWallVisuals();
        RefreshVirtualBoundaryVisuals();
        RefreshOpeningVisuals();
    }

    private void RefreshFloorVisuals()
    {
        TopPlanPolygonBatchGraphic batchGraphic = GetOrCreateFloorBatchGraphic();
        cachedFloorPolygons.Clear();
        List<Room> rooms = roomManager != null ? roomManager.GetAllRooms() : new List<Room>();

        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null || !room.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (TryBuildFloorPolygon(room, out TopPlanPolygonBatchGraphic.PolygonData polygon))
            {
                cachedFloorPolygons.Add(polygon);
            }
        }

        batchGraphic.SetPolygons(cachedFloorPolygons);
        batchGraphic.gameObject.SetActive(cachedFloorPolygons.Count > 0);
    }

    private void RefreshWallVisuals()
    {
        TopPlanSegmentBatchGraphic batchGraphic = GetOrCreateBatchGraphic(ref wallBatchGraphic, "TopPlanWallsBatch");
        cachedWallSegments.Clear();
        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls, true);

        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (TryBuildScreenSegmentData(
                    wall.Data.startPoint,
                    wall.Data.endPoint,
                    wall.transform.localScale.x,
                    GetTopPlanWallColor(wall),
                    false,
                    0f,
                    0f,
                    out TopPlanSegmentBatchGraphic.SegmentData segment))
            {
                cachedWallSegments.Add(segment);
            }
        }

        batchGraphic.SetSegments(cachedWallSegments);
        batchGraphic.gameObject.SetActive(cachedWallSegments.Count > 0);
    }

    private void RefreshVirtualBoundaryVisuals()
    {
        VirtualBoundary[] boundaries = FindObjectsByType<VirtualBoundary>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        TopPlanSegmentBatchGraphic batchGraphic = GetOrCreateBatchGraphic(ref virtualBoundaryBatchGraphic, "TopPlanVirtualBoundariesBatch");
        cachedVirtualBoundarySegments.Clear();

        for (int i = 0; i < boundaries.Length; i++)
        {
            VirtualBoundary boundary = boundaries[i];
            if (boundary == null || !boundary.isActiveAndEnabled || !boundary.VisibleInTopView)
            {
                continue;
            }

            if (!boundary.TryGetResolvedEndpoints(out Vector3 startPoint, out Vector3 endPoint))
            {
                continue;
            }

            if (TryBuildScreenSegmentData(
                    startPoint,
                    endPoint,
                    virtualBoundaryThickness,
                    virtualBoundaryColor,
                    true,
                    virtualBoundaryDashLength,
                    virtualBoundaryGapLength,
                    out TopPlanSegmentBatchGraphic.SegmentData segment))
            {
                cachedVirtualBoundarySegments.Add(segment);
            }
        }

        batchGraphic.SetSegments(cachedVirtualBoundarySegments);
        batchGraphic.gameObject.SetActive(cachedVirtualBoundarySegments.Count > 0);
    }

    private void RefreshOpeningVisuals()
    {
        WallOpening[] openings = wallRoot.GetComponentsInChildren<WallOpening>(true);
        removedKeys.Clear();
        foreach (KeyValuePair<Transform, Image> pair in openingImages)
        {
            removedKeys.Add(pair.Key);
        }

        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null || !opening.gameObject.activeInHierarchy)
            {
                continue;
            }

            removedKeys.Remove(opening.transform);
            Color baseColor = opening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door ? doorColor : windowColor;
            Image image = GetOrCreateVisual(openingImages, opening.transform, "TopPlanOpening", baseColor);
            UpdateOpeningVisual(opening, image);
        }

        RemoveUnusedVisuals(openingImages, removedKeys);
    }

    private bool TryBuildFloorPolygon(Room room, out TopPlanPolygonBatchGraphic.PolygonData polygon)
    {
        polygon = null;
        if (room == null)
        {
            return false;
        }

        List<Vector3> worldVertices = new List<Vector3>();
        if (!room.TryGetOrderedVertices(worldVertices) || worldVertices.Count < 3)
        {
            return false;
        }

        cachedPolygonPoints.Clear();
        Camera uiCamera = targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas != null ? targetCanvas.worldCamera : null;

        for (int i = 0; i < worldVertices.Count; i++)
        {
            Vector2 screenPoint = topViewCamera.WorldToScreenPoint(worldVertices[i]);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, screenPoint, uiCamera, out Vector2 localPoint))
            {
                cachedPolygonPoints.Add(localPoint);
            }
        }

        if (cachedPolygonPoints.Count < 3)
        {
            return false;
        }

        polygon = new TopPlanPolygonBatchGraphic.PolygonData(
            cachedPolygonPoints,
            highlightedRoom == room
                ? selectedFloorColor
                : floorColor);
        return true;
    }

    private bool IsWallSelectedInTopPlan(Wall wall)
    {
        if (wall == null)
        {
            return false;
        }

        if (wallSelectionManager == null)
        {
            return false;
        }

        if (wallSelectionManager.SelectedWall == wall.gameObject)
        {
            return true;
        }

        wallSelectionManager.GetSelectedWalls(cachedSelectedWalls);
        for (int i = 0; i < cachedSelectedWalls.Count; i++)
        {
            GameObject selectedWall = cachedSelectedWalls[i];
            if (selectedWall == wall.gameObject)
            {
                return true;
            }

            if (selectedWall == null)
            {
                continue;
            }

            WallOpeningContainer selectedContainer = selectedWall.GetComponentInParent<WallOpeningContainer>();
            WallOpeningContainer currentContainer = wall.GetComponentInParent<WallOpeningContainer>();
            if (selectedContainer != null && selectedContainer == currentContainer)
            {
                return true;
            }
        }

        return false;
    }

    private Color GetTopPlanWallColor(Wall wall)
    {
        if (drawManager != null && drawManager.PreviewWall == wall.gameObject)
        {
            return previewWallColor;
        }

        if (roomWallAuthoringPanelController != null)
        {
            if (roomWallAuthoringPanelController.IsWallSelectedForAuthoring(wall))
            {
                return authoringSelectedWallColor;
            }

            if (roomWallAuthoringPanelController.IsWallHoveredForAuthoring(wall))
            {
                return authoringHoveredWallColor;
            }
        }

        return IsWallSelectedInTopPlan(wall) ? selectedWallColor : wallColor;
    }

    private void UpdateOpeningVisual(WallOpening opening, Image image)
    {
        if (opening == null || image == null)
        {
            return;
        }

        WallOpeningContainer container = opening.Container;
        if (container == null)
        {
            image.gameObject.SetActive(false);
            return;
        }

        Vector3 direction = container.WallDirection;
        Vector3 center = container.WallStart + direction * opening.CenterDistance;
        Vector3 start = center - direction * (opening.Width * 0.5f);
        Vector3 end = center + direction * (opening.Width * 0.5f);

        bool isSelected = wallOpeningPlacementManager != null && wallOpeningPlacementManager.SelectedOpening == opening;
        Color baseColor;
        Color selectedColor;
        if (opening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door)
        {
            baseColor = doorColor;
            selectedColor = selectedDoorColor;
        }
        else
        {
            baseColor = windowColor;
            selectedColor = selectedWindowColor;
        }

        GameObject markerPrefab = wallOpeningPlacementManager != null
            ? wallOpeningPlacementManager.GetMarkerPrefab(opening)
            : null;
        Vector2 scaleMultiplier = wallOpeningPlacementManager != null
            ? wallOpeningPlacementManager.GetMarkerScaleMultiplier(opening)
            : Vector2.one;

        if (markerPrefab != null)
        {
            UpdateOpeningMarkerVisual(
                opening,
                image,
                start,
                end,
                scaleMultiplier,
                isSelected ? selectedColor : baseColor,
                markerPrefab);
        }
        else
        {
            ClearOpeningMarkerContent(image.rectTransform);
            ApplySegmentRect(image.rectTransform, start, end, opening.Depth);
            image.color = isSelected ? selectedColor : baseColor;
        }

        image.gameObject.SetActive(true);
    }

    private void UpdateOpeningMarkerVisual(
        WallOpening opening,
        Image image,
        Vector3 startWorld,
        Vector3 endWorld,
        Vector2 scaleMultiplier,
        Color tintColor,
        GameObject markerPrefab)
    {
        RectTransform rectTransform = image.rectTransform;
        RectTransform markerContent = EnsureOpeningMarkerContent(rectTransform, markerPrefab);
        if (markerContent == null)
        {
            ApplySegmentRect(rectTransform, startWorld, endWorld, opening.Depth);
            image.color = tintColor;
            return;
        }

        Vector3 startScreenWorld = topViewCamera.WorldToScreenPoint(startWorld);
        Vector3 endScreenWorld = topViewCamera.WorldToScreenPoint(endWorld);
        bool visible = startScreenWorld.z > 0f && endScreenWorld.z > 0f;
        rectTransform.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        Vector2 startScreen = startScreenWorld;
        Vector2 endScreen = endScreenWorld;
        Vector2 midpointScreen = (startScreen + endScreen) * 0.5f;
        Vector2 delta = endScreen - startScreen;
        float width = delta.magnitude;
        float scaledWidth = Mathf.Max(1f, width * Mathf.Max(0.01f, scaleMultiplier.x));
        float scaledHeight = Mathf.Max(1f, scaledWidth * Mathf.Max(0.01f, scaleMultiplier.y));
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        WallOpeningPlacementManager.MarkerPlacementMode placementMode =
            wallOpeningPlacementManager != null
                ? wallOpeningPlacementManager.GetMarkerPlacementMode(opening)
                : WallOpeningPlacementManager.MarkerPlacementMode.OffsetFromOpening;

        if (placementMode == WallOpeningPlacementManager.MarkerPlacementMode.OffsetFromOpening)
        {
            Vector2 normal = width > 0.0001f
                ? new Vector2(-delta.y, delta.x).normalized
                : Vector2.up;
            float offsetDirection = opening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door &&
                                    opening.DoorVerticalFlip
                ? -1f
                : 1f;
            midpointScreen += normal * (scaledHeight * 0.5f * offsetDirection);
        }

        Camera uiCamera = targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas != null ? targetCanvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, midpointScreen, uiCamera, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }
        else
        {
            rectTransform.position = midpointScreen;
        }

        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        rectTransform.sizeDelta = new Vector2(scaledWidth, scaledHeight);
        rectTransform.localScale = Vector3.one;

        image.color = Color.clear;
        image.raycastTarget = false;
        ApplyMarkerTint(markerContent, tintColor);
        ApplyDoorMarkerFlip(markerContent, opening);
    }

    private void ApplySegmentRect(RectTransform rectTransform, Vector3 startWorld, Vector3 endWorld, float worldThickness)
    {
        Vector3 direction = endWorld - startWorld;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.000001f)
        {
            rectTransform.gameObject.SetActive(false);
            return;
        }

        Vector3 midpoint = (startWorld + endWorld) * 0.5f;
        Vector3 normal = Vector3.Cross(Vector3.up, direction.normalized);
        Vector3 thicknessOffset = normal * (worldThickness * 0.5f);

        Vector2 startScreen = topViewCamera.WorldToScreenPoint(startWorld);
        Vector2 endScreen = topViewCamera.WorldToScreenPoint(endWorld);
        Vector2 positiveThicknessScreen = topViewCamera.WorldToScreenPoint(midpoint + thicknessOffset);
        Vector2 negativeThicknessScreen = topViewCamera.WorldToScreenPoint(midpoint - thicknessOffset);

        float width = Vector2.Distance(startScreen, endScreen);
        float thickness = Mathf.Max(1f, Vector2.Distance(positiveThicknessScreen, negativeThicknessScreen));
        float angle = Mathf.Atan2(endScreen.y - startScreen.y, endScreen.x - startScreen.x) * Mathf.Rad2Deg;
        Vector2 midpointScreen = (startScreen + endScreen) * 0.5f;

        Camera uiCamera = targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas != null ? targetCanvas.worldCamera : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, midpointScreen, uiCamera, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }
        else
        {
            rectTransform.position = midpointScreen;
        }

        rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        rectTransform.sizeDelta = new Vector2(width, thickness);
    }

    private Image GetOrCreateVisual(Dictionary<Transform, Image> map, Transform key, string namePrefix, Color defaultColor)
    {
        if (map.TryGetValue(key, out Image existing) && existing != null)
        {
            return existing;
        }

        GameObject visualObject = new GameObject($"{namePrefix}_{key.name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        visualObject.transform.SetParent(contentRoot, false);
        LayerUtility.ApplyLayer(visualObject, LayerUtility.TopPlanUILayerName, true);

        Image image = visualObject.GetComponent<Image>();
        image.color = defaultColor;
        image.raycastTarget = false;

        RectTransform rectTransform = image.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        map[key] = image;
        return image;
    }

    private RectTransform EnsureOpeningMarkerContent(RectTransform parent, GameObject markerPrefab)
    {
        if (parent == null || markerPrefab == null)
        {
            return null;
        }

        RectTransform existing = parent.childCount > 0 ? parent.GetChild(0) as RectTransform : null;
        if (existing != null && existing.name == markerPrefab.name)
        {
            return existing;
        }

        ClearOpeningMarkerContent(parent);

        GameObject markerObject = Instantiate(markerPrefab, parent);
        markerObject.name = markerPrefab.name;
        LayerUtility.ApplyLayer(markerObject, LayerUtility.TopPlanUILayerName, true);

        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        if (markerRect == null)
        {
            markerRect = markerObject.AddComponent<RectTransform>();
        }

        markerRect.SetParent(parent, false);
        markerRect.anchorMin = Vector2.zero;
        markerRect.anchorMax = Vector2.one;
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.offsetMin = Vector2.zero;
        markerRect.offsetMax = Vector2.zero;
        markerRect.anchoredPosition = Vector2.zero;
        markerRect.localScale = Vector3.one;
        markerRect.localRotation = Quaternion.identity;
        return markerRect;
    }

    private void ClearOpeningMarkerContent(RectTransform parent)
    {
        if (parent == null)
        {
            return;
        }

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private static void ApplyMarkerTint(RectTransform markerRoot, Color tintColor)
    {
        if (markerRoot == null)
        {
            return;
        }

        Graphic[] graphics = markerRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            graphic.color = tintColor;
            graphic.raycastTarget = false;
        }
    }

    private static void ApplyDoorMarkerFlip(RectTransform markerRoot, WallOpening opening)
    {
        if (markerRoot == null || opening == null || opening.Type != WallOpeningPlacementManager.OpeningPlacementType.Door)
        {
            return;
        }

        Vector3 scale = markerRoot.localScale;
        scale.x = Mathf.Abs(scale.x) * (opening.DoorOpensRight ? -1f : 1f);
        scale.y = Mathf.Abs(scale.y) * (opening.DoorVerticalFlip ? -1f : 1f);
        markerRoot.localScale = scale;
    }

    private TopPlanPolygonBatchGraphic GetOrCreateFloorBatchGraphic()
    {
        if (floorBatchGraphic != null)
        {
            return floorBatchGraphic;
        }

        GameObject visualObject = new GameObject("TopPlanFloorsBatch", typeof(RectTransform), typeof(CanvasRenderer), typeof(TopPlanPolygonBatchGraphic));
        visualObject.transform.SetParent(contentRoot, false);
        visualObject.transform.SetAsFirstSibling();
        LayerUtility.ApplyLayer(visualObject, LayerUtility.TopPlanUILayerName, true);

        floorBatchGraphic = visualObject.GetComponent<TopPlanPolygonBatchGraphic>();
        floorBatchGraphic.raycastTarget = false;

        RectTransform rectTransform = floorBatchGraphic.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return floorBatchGraphic;
    }

    private void RemoveUnusedVisuals(Dictionary<Transform, Image> map, List<Transform> keys)
    {
        for (int i = 0; i < keys.Count; i++)
        {
            Transform key = keys[i];
            if (!map.TryGetValue(key, out Image image))
            {
                continue;
            }

            if (image != null)
            {
                Destroy(image.gameObject);
            }

            map.Remove(key);
        }
    }

    private void ClearVisuals(Dictionary<Transform, Image> map)
    {
        foreach (KeyValuePair<Transform, Image> pair in map)
        {
            if (pair.Value != null)
            {
                Destroy(pair.Value.gameObject);
            }
        }

        map.Clear();
    }

    private void ClearPolygonBatchGraphic(ref TopPlanPolygonBatchGraphic graphic)
    {
        if (graphic == null)
        {
            return;
        }

        Destroy(graphic.gameObject);
        graphic = null;
    }

    private TopPlanSegmentBatchGraphic GetOrCreateBatchGraphic(ref TopPlanSegmentBatchGraphic graphic, string objectName)
    {
        if (graphic != null)
        {
            return graphic;
        }

        GameObject visualObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TopPlanSegmentBatchGraphic));
        visualObject.transform.SetParent(contentRoot, false);
        LayerUtility.ApplyLayer(visualObject, LayerUtility.TopPlanUILayerName, true);
        graphic = visualObject.GetComponent<TopPlanSegmentBatchGraphic>();
        graphic.raycastTarget = false;

        RectTransform rectTransform = graphic.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return graphic;
    }

    private bool TryBuildScreenSegmentData(
        Vector3 startWorld,
        Vector3 endWorld,
        float worldThickness,
        Color segmentColor,
        bool dashed,
        float dashLength,
        float gapLength,
        out TopPlanSegmentBatchGraphic.SegmentData segment)
    {
        segment = default;

        Vector3 direction = endWorld - startWorld;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3 startScreenWorld = topViewCamera.WorldToScreenPoint(startWorld);
        Vector3 endScreenWorld = topViewCamera.WorldToScreenPoint(endWorld);
        if (startScreenWorld.z <= 0f || endScreenWorld.z <= 0f)
        {
            return false;
        }

        Camera uiCamera = targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas != null ? targetCanvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, startScreenWorld, uiCamera, out Vector2 startLocal))
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, endScreenWorld, uiCamera, out Vector2 endLocal))
        {
            return false;
        }

        Vector3 midpoint = (startWorld + endWorld) * 0.5f;
        Vector3 normal = Vector3.Cross(Vector3.up, direction.normalized);
        Vector3 thicknessOffset = normal * (worldThickness * 0.5f);
        Vector2 positiveThicknessScreen = topViewCamera.WorldToScreenPoint(midpoint + thicknessOffset);
        Vector2 negativeThicknessScreen = topViewCamera.WorldToScreenPoint(midpoint - thicknessOffset);
        float thickness = Mathf.Max(1f, Vector2.Distance(positiveThicknessScreen, negativeThicknessScreen));

        segment = new TopPlanSegmentBatchGraphic.SegmentData
        {
            start = startLocal,
            end = endLocal,
            thickness = thickness,
            color = segmentColor,
            dashed = dashed,
            dashLength = dashLength,
            gapLength = gapLength,
        };
        return true;
    }

    private void ClearBatchGraphic(ref TopPlanSegmentBatchGraphic graphic)
    {
        if (graphic == null)
        {
            return;
        }

        Destroy(graphic.gameObject);
        graphic = null;
    }
}

