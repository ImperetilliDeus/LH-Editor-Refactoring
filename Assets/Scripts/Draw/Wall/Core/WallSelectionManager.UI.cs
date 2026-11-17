using System.Collections.Generic;
using UnityEngine;

public partial class WallSelectionManager
{
    private bool selectionUiPositionsDirty = true;
    private bool hasSelectionUiSignature;
    private Vector3 lastUiCameraPosition;
    private Quaternion lastUiCameraRotation;
    private float lastUiCameraOrthoSize;
    private float lastUiCameraFieldOfView;
    private Vector4 lastUiViewportSignature;
    private float lastUiCanvasScaleFactor;
    private Vector2 lastUiCanvasSize;
    private RenderMode lastUiCanvasRenderMode;
    private Camera lastUiCanvasWorldCamera;
    private int lastUiCanvasChildCount;
    private int lastUiWallRootChildCount;
    private bool lastUiInteractionEnabled;

    private void MarkSelectionUiPositionsDirty()
    {
        selectionUiPositionsDirty = true;
    }

    private void HandleSelectionUiModeChanged(EditorMode mode)
    {
        selectionUiPositionsDirty = true;
    }

    private void HandleSelectionUiOpeningSelectionChanged(WallOpening opening)
    {
        selectionUiPositionsDirty = true;
    }

    private void BindSelectionUiRefreshEvents()
    {
        WallRegistry.RegistryChanged -= MarkSelectionUiPositionsDirty;
        WallRegistry.RegistryChanged += MarkSelectionUiPositionsDirty;
        EditorVisualEvents.OpeningMarkerRefreshRequested -= MarkSelectionUiPositionsDirty;
        EditorVisualEvents.OpeningMarkerRefreshRequested += MarkSelectionUiPositionsDirty;

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleSelectionUiModeChanged;
            modeManager.ModeChanged += HandleSelectionUiModeChanged;
        }

        if (wallOpeningPlacementManager != null)
        {
            wallOpeningPlacementManager.OpeningSelectionChanged -= HandleSelectionUiOpeningSelectionChanged;
            wallOpeningPlacementManager.OpeningSelectionChanged += HandleSelectionUiOpeningSelectionChanged;
        }
    }

    private void UnbindSelectionUiRefreshEvents()
    {
        WallRegistry.RegistryChanged -= MarkSelectionUiPositionsDirty;
        EditorVisualEvents.OpeningMarkerRefreshRequested -= MarkSelectionUiPositionsDirty;

        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleSelectionUiModeChanged;
        }

        if (wallOpeningPlacementManager != null)
        {
            wallOpeningPlacementManager.OpeningSelectionChanged -= HandleSelectionUiOpeningSelectionChanged;
        }
    }

    private bool ShouldRefreshSelectionUiPositions()
    {
        if (selectionUiPositionsDirty || !hasSelectionUiSignature)
        {
            return true;
        }

        // Wall endpoints are covered by events; everything that only changes the projection is polled here.
        if (mainCamera != null)
        {
            Transform cameraTransform = mainCamera.transform;
            if (cameraTransform.position != lastUiCameraPosition ||
                cameraTransform.rotation != lastUiCameraRotation ||
                !Mathf.Approximately(mainCamera.orthographicSize, lastUiCameraOrthoSize) ||
                !Mathf.Approximately(mainCamera.fieldOfView, lastUiCameraFieldOfView))
            {
                return true;
            }
        }

        if (EditorScreenCoordinateUtility.ViewportSignatureChanged(
                lastUiViewportSignature,
                EditorScreenCoordinateUtility.GetViewportSignature(mainCamera)))
        {
            return true;
        }

        if (wallSelectionCanvas != null)
        {
            RectTransform canvasRect = wallSelectionCanvas.transform as RectTransform;
            Vector2 canvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
            if (!Mathf.Approximately(wallSelectionCanvas.scaleFactor, lastUiCanvasScaleFactor) ||
                canvasSize != lastUiCanvasSize ||
                wallSelectionCanvas.renderMode != lastUiCanvasRenderMode ||
                wallSelectionCanvas.worldCamera != lastUiCanvasWorldCamera ||
                wallSelectionCanvas.transform.childCount != lastUiCanvasChildCount)
            {
                return true;
            }
        }

        int wallRootChildCount = wallRoot != null ? wallRoot.childCount : 0;
        return wallRootChildCount != lastUiWallRootChildCount ||
               IsWallUIInteractionEnabled != lastUiInteractionEnabled;
    }

    private void CacheSelectionUiSignature()
    {
        if (mainCamera != null)
        {
            Transform cameraTransform = mainCamera.transform;
            lastUiCameraPosition = cameraTransform.position;
            lastUiCameraRotation = cameraTransform.rotation;
            lastUiCameraOrthoSize = mainCamera.orthographicSize;
            lastUiCameraFieldOfView = mainCamera.fieldOfView;
        }

        lastUiViewportSignature = EditorScreenCoordinateUtility.GetViewportSignature(mainCamera);
        if (wallSelectionCanvas != null)
        {
            RectTransform canvasRect = wallSelectionCanvas.transform as RectTransform;
            lastUiCanvasScaleFactor = wallSelectionCanvas.scaleFactor;
            lastUiCanvasSize = canvasRect != null ? canvasRect.rect.size : Vector2.zero;
            lastUiCanvasRenderMode = wallSelectionCanvas.renderMode;
            lastUiCanvasWorldCamera = wallSelectionCanvas.worldCamera;
            lastUiCanvasChildCount = wallSelectionCanvas.transform.childCount;
        }

        lastUiWallRootChildCount = wallRoot != null ? wallRoot.childCount : 0;
        lastUiInteractionEnabled = IsWallUIInteractionEnabled;
        hasSelectionUiSignature = true;
        selectionUiPositionsDirty = false;
    }

    private void PrepareSelectUI()
    {
        if (selectUIObject != null)
        {
            selectUIObject.SetActive(false);
        }
    }

    private void SetSelectUIVisible(bool visible)
    {
        if (selectUIObject != null && selectUIObject.activeSelf != visible)
        {
            selectUIObject.SetActive(visible);
        }
    }

    private void RefreshWallSelectionUIStates()
    {
        if (isShuttingDown)
        {
            return;
        }

        // Proxies may be created or destroyed below, so positions must be recalculated afterwards.
        selectionUiPositionsDirty = true;
        CollectLogicalWallRoots(wallRoot, logicalWallRoots);

        presentationController.RefreshWallSelectionUIStates(
            wallRoot,
            logicalWallRoots,
            IsWallOrContainerSelected,
            IsSelected,
            this);
    }

    private void RefreshWallSelectionUIPositions()
    {
        if (isShuttingDown)
        {
            return;
        }

        presentationController.RefreshWallSelectionUIPositions(wallRoot, this);
        CacheSelectionUiSignature();
    }

    private bool ShouldDisplaySelectionProxy(Wall wall)
    {
        return ShouldDisplaySelectionProxyInternal(wall);
    }

    private Wall GetRepresentativeWallForContainer(WallOpeningContainer container)
    {
        return GetRepresentativeWallForContainerInternal(container);
    }

    private bool IsWallOrContainerSelected(Wall wall)
    {
        if (wallOpeningPlacementManager != null && wallOpeningPlacementManager.SelectedOpening != null)
        {
            return false;
        }

        return IsWallOrContainerSelectedInternal(wall);
    }

    private void CollectLogicalWallRoots(Transform currentWallRoot, List<Transform> results)
    {
        results.Clear();
        if (currentWallRoot == null)
        {
            return;
        }

        for (int i = 0; i < currentWallRoot.childCount; i++)
        {
            Transform child = currentWallRoot.GetChild(i);
            if (child != null && child.gameObject.activeInHierarchy &&
                (child.GetComponent<WallOpeningContainer>() != null || child.GetComponent<Wall>() != null))
            {
                results.Add(child);
            }
        }
    }
}
