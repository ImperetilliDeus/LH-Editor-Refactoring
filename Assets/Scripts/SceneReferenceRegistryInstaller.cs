using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public static class SceneReferenceRegistryInstaller
{
    public static void ApplyDefaults(SceneReferenceRegistry registry)
    {
        if (registry == null)
        {
            return;
        }

        SetIfNull(registry, "mainCamera", Camera.main);
        SetIfNull(registry, "modeManager", Object.FindFirstObjectByType<ModeManager>());
        SetIfNull(registry, "roomManager", Object.FindFirstObjectByType<RoomManager>());
        SetIfNull(registry, "drawManager", Object.FindFirstObjectByType<DrawManager>());
        SetIfNull(registry, "handleManager", Object.FindFirstObjectByType<HandleManager>());
        SetIfNull(registry, "snapManager", Object.FindFirstObjectByType<SnapManager>());
        SetIfNull(registry, "wallSelectionManager", Object.FindFirstObjectByType<WallSelectionManager>());
        SetIfNull(registry, "wallOpeningPlacementManager", Object.FindFirstObjectByType<WallOpeningPlacementManager>());
        SetIfNull(registry, "wallLengthDisplay", Object.FindFirstObjectByType<WallLengthDisplay>());
        SetIfNull(registry, "undoRedoManager", Object.FindFirstObjectByType<UndoRedoManager>());
        SetIfNull(registry, "topViewRenderManager", Object.FindFirstObjectByType<TopViewRenderManager>());
        SetIfNull(registry, "roomAuthoringPanelManager", Object.FindFirstObjectByType<RoomAuthoringPanelManager>());
        SetIfNull(registry, "roomWallAuthoringPanelController", Object.FindFirstObjectByType<RoomWallAuthoringPanelController>());
        SetIfNull(registry, "roomHandleManager", Object.FindFirstObjectByType<RoomHandleManager>());
        SetIfNull(registry, "drawingOverlayManager", Object.FindFirstObjectByType<DrawingOverlayManager>(FindObjectsInactive.Include));
        SetIfNull(registry, "overlayCalibrationPanelController", Object.FindFirstObjectByType<OverlayCalibrationPanelController>(FindObjectsInactive.Include));

        SetIfNull(registry, "wallRoot", LayerUtility.FindWallRoot(true));
        SetIfNull(registry, "grid", ResolveGridObject());
        SetIfNull(registry, "importButton", ResolveImportButton());
        SetIfNull(registry, "defaultCanvas", LayerUtility.FindCanvasByName(LayerUtility.DefaultCanvasName));
        SetIfNull(registry, "handleCanvas", LayerUtility.FindCanvasByName(LayerUtility.DefaultHandleCanvasName));
        SetIfNull(registry, "wallUiCanvas", LayerUtility.FindCanvasByName(LayerUtility.DefaultWallUICanvasName));
    }

    private static GameObject ResolveGridObject()
    {
        Transform transform = LayerUtility.FindTransformByName(LayerUtility.DefaultGridName, true);
        return transform != null ? transform.gameObject : null;
    }

    private static Button ResolveImportButton()
    {
        Transform transform = LayerUtility.FindTransformByName(LayerUtility.DefaultImportButtonName, true);
        return transform != null ? transform.GetComponent<Button>() : null;
    }

    private static void SetIfNull<T>(SceneReferenceRegistry registry, string fieldName, T value) where T : Object
    {
        if (value == null)
        {
            return;
        }

        FieldInfo field = typeof(SceneReferenceRegistry).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            return;
        }

        if (field.GetValue(registry) == null)
        {
            field.SetValue(registry, value);
        }
    }
}
