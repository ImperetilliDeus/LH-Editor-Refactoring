using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class WallSelectionPresentationController
{
    public void PrepareSelectUI(GameObject selectUIObject)
    {
        if (selectUIObject == null)
        {
            return;
        }

        selectUIObject.SetActive(false);
    }

    public void SetSelectUIVisible(GameObject selectUIObject, bool visible)
    {
        if (selectUIObject == null)
        {
            return;
        }

        if (selectUIObject.activeSelf != visible)
        {
            selectUIObject.SetActive(visible);
        }
    }

    public Canvas EnsureSelectionCanvas(Canvas currentCanvas)
    {
        return currentCanvas != null
            ? currentCanvas
            : LayerUtility.FindCanvasByNameOrFirst(LayerUtility.DefaultCanvasName);
    }

    public void RefreshWallSelectionUIStates(
        Transform wallRoot,
        List<Wall> walls,
        Func<Wall, bool> shouldDisplaySelectionProxy,
        Func<Wall, bool> isWallOrContainerSelected,
        WallSelectionManager selectionManager)
    {
        if (wallRoot == null || walls == null || shouldDisplaySelectionProxy == null || isWallOrContainerSelected == null || selectionManager == null)
        {
            return;
        }

        for (int i = 0; i < walls.Count; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            WallSelectionUIProxy proxy = wall.GetComponent<WallSelectionUIProxy>();
            if (!shouldDisplaySelectionProxy(wall))
            {
                DestroyProxy(proxy);
                continue;
            }

            proxy = GetOrCreateProxy(wall, selectionManager, proxy);
            proxy.SetSelected(isWallOrContainerSelected(wall));
        }
    }

    public void RefreshWallSelectionUIPositions(
        Transform wallRoot,
        List<Wall> walls,
        Func<Wall, bool> shouldDisplaySelectionProxy,
        WallSelectionManager selectionManager)
    {
        if (wallRoot == null || walls == null || shouldDisplaySelectionProxy == null || selectionManager == null)
        {
            return;
        }

        for (int i = 0; i < walls.Count; i++)
        {
            Wall wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            WallSelectionUIProxy proxy = wall.GetComponent<WallSelectionUIProxy>();
            if (!shouldDisplaySelectionProxy(wall))
            {
                DestroyProxy(proxy);
                continue;
            }

            proxy = GetOrCreateProxy(wall, selectionManager, proxy);
            proxy.RefreshVisual();
        }
    }

    private static WallSelectionUIProxy GetOrCreateProxy(
        Wall wall,
        WallSelectionManager selectionManager,
        WallSelectionUIProxy existingProxy)
    {
        WallSelectionUIProxy proxy = existingProxy != null
            ? existingProxy
            : wall.gameObject.AddComponent<WallSelectionUIProxy>();
        proxy.Initialize(selectionManager);
        return proxy;
    }

    private static void DestroyProxy(WallSelectionUIProxy proxy)
    {
        if (proxy == null)
        {
            return;
        }

        proxy.DestroyUI();
        UnityEngine.Object.Destroy(proxy);
    }
}
