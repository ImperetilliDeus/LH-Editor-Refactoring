using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DwgImportedWallOwnership : MonoBehaviour
{
    [SerializeField] private string importerId = string.Empty;

    public string ImporterId => importerId;

    public void SetImporterId(string value)
    {
        importerId = value ?? string.Empty;
    }
}

public sealed class DwgWallImportSceneApplyContext
{
    public string ImporterId { get; set; } = string.Empty;
    public Transform WallRoot { get; set; }
    public HandleManager HandleManager { get; set; }
    public WallSelectionManager WallSelectionManager { get; set; }
    public RoomManager RoomManager { get; set; }
    public DrawingOverlayManager DrawingOverlayManager { get; set; }
    public WallLengthDisplay WallLengthDisplay { get; set; }
    public Material WallMaterial { get; set; }
    public Material TopMaterial { get; set; }
    public UnityEngine.Mesh WallMesh { get; set; }
    public float DrawingPlaneY { get; set; }
    public float WallHeight { get; set; }
    public float WallThickness { get; set; }
    public float WallSurfaceOffset { get; set; }
    public float MinimumWallLength { get; set; }
    public bool ClearExistingWalls { get; set; }
    public bool ClearExistingRooms { get; set; }
    public bool RefreshRoomsAfterImport { get; set; }
    public Action<UnityEngine.Object> DestroyObject { get; set; }
}

public sealed class DwgWallImportSceneApplyResult
{
    public int CreatedWallCount { get; set; }
    public int RemovedWallCount { get; set; }
    public int RemovedRoomCount { get; set; }
}

public static class DwgWallImportSceneApplier
{
    public static DwgWallImportSceneApplyResult Apply(IReadOnlyList<CadWallSegment> segments, DwgWallImportSceneApplyContext context)
    {
        if (segments == null)
        {
            throw new ArgumentNullException(nameof(segments));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.WallRoot == null)
        {
            throw new InvalidOperationException("WallRoot is required.");
        }

        if (context.DestroyObject == null)
        {
            throw new InvalidOperationException("DestroyObject callback is required.");
        }

        DwgWallImportSceneApplyResult result = new DwgWallImportSceneApplyResult();
        context.DrawingOverlayManager?.ClearOverlay();

        if (context.ClearExistingWalls)
        {
            context.WallSelectionManager?.ClearSelectionForSceneReset();
            context.WallLengthDisplay?.ClearAllLabels();
            result.RemovedWallCount = RemoveAllWalls(context);
            context.WallSelectionManager?.ClearSelectionForSceneReset();
            context.WallLengthDisplay?.ClearAllLabels();
        }

        if (context.ClearExistingRooms)
        {
            result.RemovedRoomCount = RemoveAllRooms(context);
        }

        for (int i = 0; i < segments.Count; i++)
        {
            if (TryCreateWall(segments[i], context, out GameObject wallObject))
            {
                result.CreatedWallCount++;
                context.HandleManager?.RegisterWall(wallObject);
            }
        }

        WallNamingUtility.NormalizeWallNames(context.WallRoot);
        context.HandleManager?.RefreshRegisteredWalls();

        if (context.RefreshRoomsAfterImport)
        {
            context.RoomManager?.MarkGraphDirty();
            RoomTopologyEvents.RequestRefreshAll();
        }

        return result;
    }

    private static int RemoveAllWalls(DwgWallImportSceneApplyContext context)
    {
        int removedCount = 0;
        for (int i = context.WallRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = context.WallRoot.GetChild(i);
            GameObject wallObject = child != null ? child.gameObject : null;
            UnregisterWallsInHierarchy(wallObject, context.HandleManager);
            context.DestroyObject(wallObject);
            removedCount++;
        }

        return removedCount;
    }

    private static void UnregisterWallsInHierarchy(GameObject root, HandleManager handleManager)
    {
        if (root == null || handleManager == null)
        {
            return;
        }

        Wall[] walls = root.GetComponentsInChildren<Wall>(true);
        for (int i = 0; i < walls.Length; i++)
        {
            if (walls[i] != null)
            {
                handleManager.UnregisterWall(walls[i].gameObject);
            }
        }
    }

    private static int RemoveAllRooms(DwgWallImportSceneApplyContext context)
    {
        if (context.RoomManager == null)
        {
            return 0;
        }

        List<Room> rooms = context.RoomManager.GetAllRooms();
        int removedCount = rooms.Count;
        context.RoomManager.ClearAllRoomsForWorkStateLoad();
        return removedCount;
    }

    private static bool TryCreateWall(CadWallSegment segment, DwgWallImportSceneApplyContext context, out GameObject wallObject)
    {
        wallObject = WallObjectFactory.CreateWallObject(
            "DWG_Wall",
            context.WallRoot,
            context.WallMesh,
            new WallVisualState
            {
                wallMaterial = context.WallMaterial,
                topMaterial = context.TopMaterial,
                topFaceOffset = Wall.DefaultTopFaceOffset,
            });

        DwgImportedWallOwnership ownership = wallObject.AddComponent<DwgImportedWallOwnership>();
        ownership.SetImporterId(context.ImporterId);

        wallObject.name = $"{segment.SourceType}_{segment.LayerName}";

        float centerY = context.DrawingPlaneY + context.WallHeight * 0.5f + context.WallSurfaceOffset;
        if (!WallObjectFactory.ConfigureWall(
                wallObject,
                new WallData(segment.Start, segment.End, context.WallThickness, context.WallHeight, centerY),
                0,
                0,
                false,
                false,
                false,
                false,
                context.MinimumWallLength,
                context.WallLengthDisplay,
                false))
        {
            context.DestroyObject(wallObject);
            wallObject = null;
            return false;
        }

        return true;
    }
}
