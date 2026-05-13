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
        return currentCanvas;
    }

    public void RefreshWallSelectionUIStates(
        Transform wallRoot,
        List<Transform> logicalWallRoots,
        Func<Wall, bool> isWallOrContainerSelected,
        Func<WallOpeningContainer, bool> isContainerSelected,
        WallSelectionManager selectionManager)
    {
        if (wallRoot == null || logicalWallRoots == null || isWallOrContainerSelected == null || isContainerSelected == null || selectionManager == null || selectionManager.WallSelectionCanvas == null)
        {
            return;
        }

        var activeStandaloneWalls = new HashSet<Wall>();
        var activeContainers = new HashSet<WallOpeningContainer>();
        foreach (var root in logicalWallRoots)
        {
            if (root.TryGetComponent(out WallOpeningContainer container))
                activeContainers.Add(container);
            else if (root.TryGetComponent(out Wall wall))
                activeStandaloneWalls.Add(wall);
        }

        // Handle standalone Wall proxies
        var existingWallProxies = wallRoot.GetComponentsInChildren<WallSelectionUIProxy>(true);
        foreach (var proxy in existingWallProxies)
        {
            if (proxy != null && !activeStandaloneWalls.Contains(proxy.GetComponent<Wall>()))
                DestroyProxy(proxy);
        }

        foreach (var wall in activeStandaloneWalls)
        {
            var proxy = GetOrCreateProxy(wall, selectionManager, wall.GetComponent<WallSelectionUIProxy>());
            proxy.SetSelected(isWallOrContainerSelected(wall));
        }

        // Handle WallOpeningContainer proxies
        var existingContainerProxies = selectionManager.WallSelectionCanvas.GetComponentsInChildren<WallContainerUIProxy>(true);
        var proxyMap = new Dictionary<WallOpeningContainer, WallContainerUIProxy>();
        foreach (var proxy in existingContainerProxies)
        {
            if (proxy != null && proxy.Container != null && activeContainers.Contains(proxy.Container))
            {
                if (!proxyMap.ContainsKey(proxy.Container))
                    proxyMap.Add(proxy.Container, proxy);
                else
                    DestroyProxy(proxy); // Destroy duplicates
            }
            else if (proxy != null)
            {
                DestroyProxy(proxy); // Destroy stale proxies
            }
        }

        foreach (var container in activeContainers)
        {
            if (!proxyMap.TryGetValue(container, out var proxy))
            {
                proxy = CreateContainerProxy(container, selectionManager);
            }
            proxy.SetSelected(isContainerSelected(container));
        }
    }

    public void RefreshWallSelectionUIPositions(
        Transform wallRoot,
        List<Transform> logicalWallRoots,
        WallSelectionManager selectionManager)
    {
        if (wallRoot == null || logicalWallRoots == null || selectionManager == null || selectionManager.WallSelectionCanvas == null)
        {
            return;
        }

        var wallProxies = wallRoot.GetComponentsInChildren<WallSelectionUIProxy>(true);
        foreach (var proxy in wallProxies)
        {
            if (proxy != null)
                proxy.RefreshVisual();
        }

        var containerProxies = selectionManager.WallSelectionCanvas.GetComponentsInChildren<WallContainerUIProxy>(true);
        foreach (var proxy in containerProxies)
        {
            if (proxy != null)
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

    private static WallContainerUIProxy CreateContainerProxy(
        WallOpeningContainer container,
        WallSelectionManager selectionManager)
    {
        GameObject proxyObject = new GameObject($"ContainerUI_{container.name}", typeof(RectTransform));
        proxyObject.transform.SetParent(selectionManager.WallSelectionCanvas.transform, false);
        var proxy = proxyObject.AddComponent<WallContainerUIProxy>();
        proxy.Initialize(selectionManager, container);
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

    private static void DestroyProxy(WallContainerUIProxy proxy)
    {
        if (proxy == null)
        {
            return;
        }

        proxy.DestroyUI();
        UnityEngine.Object.Destroy(proxy);
    }
}
