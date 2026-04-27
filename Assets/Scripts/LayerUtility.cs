using UnityEngine;

public static class LayerUtility
{
    public const string DefaultCanvasName = "_Screen";
    public const string DefaultHandleCanvasName = "_Handle";
    public const string DefaultWallUICanvasName = "_WallUI";
    public const string DefaultWallRootName = "Walls";
    public const string DefaultGridName = "Grid";
    public const string DefaultImportButtonName = "_ImportButton";
    public const string WallLayerName = "Wall";
    public const string DoorLayerName = "Door";
    public const string WindowLayerName = "Window";
    public const string FloorLayerName = "Floor";
    public const string CeilLayerName = "Ceil";
    public const string WallUILayerName = "WallUI";
    public const string DoorUILayerName = "DoorUI";
    public const string WindowUILayerName = "WindowUI";
    public const string TopPlanUILayerName = "TopPlanUI";
    public const string FurnishLayerName = "Furnish";

    public static bool IsLayer(GameObject target, string layerName)
    {
        if (target == null)
        {
            return false;
        }

        return TryGetLayer(layerName, out int layer) && target.layer == layer;
    }

    public static bool TryGetLayer(string layerName, out int layer)
    {
        layer = LayerMask.NameToLayer(layerName);
        return layer >= 0;
    }

    public static int GetMaskOrDefault(string layerName, int fallbackMask = Physics.DefaultRaycastLayers)
    {
        return TryGetLayer(layerName, out int layer)
            ? 1 << layer
            : fallbackMask;
    }

    public static void ApplyLayer(GameObject target, string layerName, bool recursive = true)
    {
        if (target == null || !TryGetLayer(layerName, out int layer))
        {
            return;
        }

        ApplyLayer(target, layer, recursive);
    }

    public static void ApplyLayer(GameObject target, int layer, bool recursive = true)
    {
        if (target == null || layer < 0)
        {
            return;
        }

        target.layer = layer;
        if (!recursive)
        {
            return;
        }

        Transform root = target.transform;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child != null)
            {
                ApplyLayer(child.gameObject, layer, true);
            }
        }
    }

    public static Canvas FindCanvasByNameOrFirst(string preferredName = DefaultCanvasName)
    {
        Canvas preferredCanvas = null;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(preferredName) && canvas.name == preferredName)
            {
                preferredCanvas = canvas;
                break;
            }

            if (preferredCanvas == null && canvas.isRootCanvas)
            {
                preferredCanvas = canvas;
            }
        }

        return preferredCanvas;
    }

    public static Canvas FindCanvasByName(string canvasName)
    {
        if (string.IsNullOrWhiteSpace(canvasName))
        {
            return null;
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.name == canvasName)
            {
                return canvas;
            }
        }

        return null;
    }

    public static void ResolveObject<T>(ref T reference) where T : Object
    {
        if (reference == null)
        {
            reference = Object.FindFirstObjectByType<T>();
        }
    }

    public static void ResolveTransformByName(ref Transform reference, string objectName, bool includeInactive = true)
    {
        if (reference == null)
        {
            reference = FindTransformByName(objectName, includeInactive);
        }
    }

    public static void ResolveCanvasByNameOrFirst(ref Canvas reference, string preferredName = DefaultCanvasName)
    {
        if (reference == null)
        {
            reference = FindCanvasByNameOrFirst(preferredName);
        }
    }

    public static Transform FindTransformByName(string objectName, bool includeInactive = true)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        FindObjectsInactive inactiveMode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        Transform[] transforms = Object.FindObjectsByType<Transform>(inactiveMode, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current != null && current.name == objectName)
            {
                return current;
            }
        }

        return null;
    }

    public static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
