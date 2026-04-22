using System;
using UnityEngine;

internal sealed class DwgWallImportExecutionBuilder
{
    public CadWallImportSettings CreateImportSettings(
        float cadUnitToWorldScale,
        bool invertCadY,
        float drawingPlaneY,
        Vector3 importOffset,
        float minimumWallLength,
        bool includeInvisibleEntities,
        bool deduplicateSegments,
        float deduplicateTolerance,
        string[] includedLayers,
        string[] excludedLayers,
        string targetLayerKeyword)
    {
        return new CadWallImportSettings
        {
            CadUnitToWorldScale = cadUnitToWorldScale,
            InvertCadY = invertCadY,
            DrawingPlaneY = drawingPlaneY,
            ImportOffset = importOffset,
            MinimumWallLength = minimumWallLength,
            IncludeInvisibleEntities = includeInvisibleEntities,
            DeduplicateSegments = deduplicateSegments,
            DeduplicateTolerance = deduplicateTolerance,
            IncludedLayers = includedLayers ?? Array.Empty<string>(),
            ExcludedLayers = excludedLayers ?? Array.Empty<string>(),
            TargetLayerKeyword = targetLayerKeyword ?? string.Empty,
        };
    }

    public Transform EnsureWallRoot(Transform wallRoot)
    {
        if (wallRoot != null)
        {
            return wallRoot;
        }

        Transform resolvedWallRoot = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        if (resolvedWallRoot != null)
        {
            return resolvedWallRoot;
        }

        return new GameObject(LayerUtility.DefaultWallRootName).transform;
    }

    public Mesh EnsureCachedCubeMesh(Mesh cachedCubeMesh, Action<UnityEngine.Object> destroyObject)
    {
        if (cachedCubeMesh != null)
        {
            return cachedCubeMesh;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter filter = cube.GetComponent<MeshFilter>();
        Mesh resolvedMesh = filter != null ? filter.sharedMesh : null;
        destroyObject?.Invoke(cube);
        return resolvedMesh;
    }

    public Material ResolveWallMaterial(Transform wallRoot, Material wallMaterial, Color fallbackWallColor)
    {
        if (wallMaterial != null)
        {
            return wallMaterial;
        }

        if (wallRoot != null)
        {
            MeshRenderer existingRenderer = wallRoot.GetComponentInChildren<MeshRenderer>(true);
            if (existingRenderer != null && existingRenderer.sharedMaterial != null)
            {
                return existingRenderer.sharedMaterial;
            }
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        return new Material(shader)
        {
            color = fallbackWallColor,
        };
    }

    public Material ResolveTopMaterial(Transform wallRoot, Material wallTopMaterial, Material fallbackWallMaterial)
    {
        if (wallTopMaterial != null)
        {
            return wallTopMaterial;
        }

        if (wallRoot != null)
        {
            Wall existingWall = wallRoot.GetComponentInChildren<Wall>(true);
            if (existingWall != null)
            {
                WallTopFaceVisual topFace = existingWall.GetComponentInChildren<WallTopFaceVisual>(true);
                if (topFace != null && topFace.TryGetComponent(out MeshRenderer renderer) && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }
        }

        return fallbackWallMaterial;
    }

    public DwgWallImportSceneApplyContext CreateSceneApplyContext(
        string importerOwnershipId,
        Transform wallRoot,
        HandleManager handleManager,
        RoomManager roomManager,
        WallLengthDisplay wallLengthDisplay,
        Material resolvedWallMaterial,
        Material resolvedTopMaterial,
        Mesh cachedCubeMesh,
        float drawingPlaneY,
        float wallHeight,
        float wallThickness,
        float wallSurfaceOffset,
        float minimumWallLength,
        bool clearExistingWalls,
        bool clearExistingRooms,
        bool refreshRoomsAfterImport,
        Action<UnityEngine.Object> destroyObject)
    {
        return new DwgWallImportSceneApplyContext
        {
            ImporterId = importerOwnershipId,
            WallRoot = wallRoot,
            HandleManager = handleManager,
            RoomManager = roomManager,
            WallLengthDisplay = wallLengthDisplay,
            WallMaterial = resolvedWallMaterial,
            TopMaterial = resolvedTopMaterial,
            WallMesh = cachedCubeMesh,
            DrawingPlaneY = drawingPlaneY,
            WallHeight = wallHeight,
            WallThickness = wallThickness,
            WallSurfaceOffset = wallSurfaceOffset,
            MinimumWallLength = minimumWallLength,
            ClearExistingWalls = clearExistingWalls,
            ClearExistingRooms = clearExistingRooms,
            RefreshRoomsAfterImport = refreshRoomsAfterImport,
            DestroyObject = destroyObject,
        };
    }
}
