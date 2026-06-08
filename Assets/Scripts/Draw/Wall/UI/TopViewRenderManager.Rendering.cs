using System.Collections.Generic;
using UnityEngine;

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
        batchGraphic.SegmentClicked -= HandleTopPlanWallSegmentClicked;
        batchGraphic.SegmentClicked += HandleTopPlanWallSegmentClicked;
        batchGraphic.raycastTarget = IsRoomWallAuthoringInteractionEnabled();

        cachedWallSegments.Clear();
        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls, true, true);

        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null || !wall.gameObject.activeInHierarchy || WallHierarchyUtility.IsHiddenOpeningBaseSegment(wall))
            {
                continue;
            }

            if (TryBuildScreenSegmentData(
                    wall.Data.startPoint,
                    wall.Data.endPoint,
                    GetTopPlanWallWorldThickness(wall),
                    GetTopPlanWallColor(wall),
                    wall,
                    null,
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
                    null,
                    null,
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
        TopPlanSegmentBatchGraphic batchGraphic = GetOrCreateBatchGraphic(ref openingBatchGraphic, "TopPlanOpeningsBatch");
        batchGraphic.SegmentClicked -= HandleTopPlanOpeningSegmentClicked;
        batchGraphic.SegmentClicked += HandleTopPlanOpeningSegmentClicked;
        batchGraphic.raycastTarget = IsOpeningInteractionEnabled();
        cachedOpeningSegments.Clear();

        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null || !opening.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (TryBuildOpeningSegmentData(opening, out TopPlanSegmentBatchGraphic.SegmentData segment))
            {
                cachedOpeningSegments.Add(segment);
            }
        }

        batchGraphic.SetSegments(cachedOpeningSegments);
        batchGraphic.gameObject.SetActive(cachedOpeningSegments.Count > 0);
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

        if (wallOpeningPlacementManager != null && wallOpeningPlacementManager.SelectedOpening != null)
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

    private float GetTopPlanWallWorldThickness(Wall wall)
    {
        if (wall == null)
        {
            return 0f;
        }

        float worldThickness = wall.transform.localScale.x;
        if (WallHierarchyUtility.IsPreviewWall(wall))
        {
            worldThickness *= Mathf.Max(0.01f, previewWallThicknessMultiplier);
        }

        return worldThickness;
    }

    private bool TryBuildOpeningSegmentData(WallOpening opening, out TopPlanSegmentBatchGraphic.SegmentData segment)
    {
        segment = default;
        if (opening == null)
        {
            return false;
        }

        WallOpeningContainer container = opening.Container;
        if (container == null)
        {
            return false;
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

        return TryBuildScreenSegmentData(
            start,
            end,
            container.WallThickness,
            isSelected ? selectedColor : baseColor,
            null,
            opening,
            false,
            0f,
            0f,
            out segment);
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
        Wall ownerWall,
        WallOpening ownerOpening,
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
            wall = ownerWall,
            opening = ownerOpening,
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
