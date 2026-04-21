using UnityEngine;

public partial class DrawManager
{
    private void OnDestroy()
    {
        UnbindModeEvents();
        ClearPreviewWallDisplay();

        if (previewWall != null)
        {
            Destroy(previewWall);
            previewWall = null;
        }

        if (previewMaterial != null)
        {
            Destroy(previewMaterial);
            previewMaterial = null;
        }

        if (wallMaterial != null)
        {
            Destroy(wallMaterial);
            wallMaterial = null;
        }
    }

    private void CommitCurrentSegment()
    {
        if (!TryGetMouseWorldPoint(out Vector3 endPoint))
        {
            return;
        }

        if (!TryBuildWallSegment(currentSegmentStart, endPoint, false, out GameObject wallSegment))
        {
            return;
        }

        wallSegment.name = $"Wall_{wallSequence++:000}";
        if (handleManager != null)
        {
            handleManager.RegisterWall(wallSegment);
        }

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordWallCreated(wallSegment);
        }

        currentSegmentStart = endPoint;
        UpdatePreviewWall();
    }

    private void UpdatePreviewWall()
    {
        if (!enablePreviewWall)
        {
            return;
        }

        EnsurePreviewWall();

        if (!TryGetMouseWorldPoint(out Vector3 currentPoint))
        {
            previewWall.SetActive(false);
            return;
        }

        if (!TryBuildWallSegment(currentSegmentStart, currentPoint, true, out _))
        {
            ClearPreviewWallDisplay();
            previewWall.SetActive(false);
            return;
        }

        previewWall.SetActive(true);
    }

    private void ExitWallCreationMode()
    {
        isWallCreationMode = false;

        if (handleManager != null)
        {
            handleManager.ClearPreviewSnappedHandle();
        }

        ClearPreviewWallDisplay();
        if (previewWall != null)
        {
            previewWall.SetActive(false);
        }
    }

    private bool TryBuildWallSegment(Vector3 startPoint, Vector3 endPoint, bool isPreview, out GameObject wallObject)
    {
        wallObject = isPreview ? previewWall : CreateWallObject();

        Wall wallComponent = wallObject.GetComponent<Wall>();
        bool applied = wallComponent != null &&
            wallComponent.TryApplyGeometryAndRefresh(
                startPoint,
                endPoint,
                wallThickness,
                wallHeight,
                drawingPlaneHeight + wallHeight * 0.5f + wallSurfaceOffset,
                MinimumWallLength,
                wallLengthDisplay,
                isPreview);

        if (!applied)
        {
            if (!isPreview && wallObject != null)
            {
                wallComponent?.ClearLengthDisplay(wallLengthDisplay);

                if (handleManager != null)
                {
                    handleManager.UnregisterWall(wallObject);
                }

                Destroy(wallObject);
            }
            else if (isPreview)
            {
                ClearPreviewWallDisplay();
            }

            return false;
        }

        return true;
    }

    private void EnsurePreviewWall()
    {
        if (!enablePreviewWall || previewWall != null)
        {
            return;
        }

        previewWall = CreateWallObject();
        previewWall.name = "WallPreview";

        Collider previewCollider = previewWall.GetComponent<Collider>();
        if (previewCollider != null)
        {
            Destroy(previewCollider);
        }

        MeshRenderer previewRenderer = previewWall.GetComponent<MeshRenderer>();
        if (previewRenderer != null && previewMaterial != null)
        {
            previewRenderer.sharedMaterial = previewMaterial;
        }

        Wall previewWallComponent = previewWall.GetComponent<Wall>();
        if (previewWallComponent != null)
        {
            previewWallComponent.SetTopMaterial(previewMaterial);
            previewWallComponent.SetTopFaceOffset(Wall.DefaultTopFaceOffset);
        }

        previewWall.SetActive(false);
    }

    private void ClearPreviewWallDisplay()
    {
        if (previewWall == null)
        {
            return;
        }

        Wall previewComponent = previewWall.GetComponent<Wall>();
        if (previewComponent != null)
        {
            previewComponent.ClearLengthDisplay(wallLengthDisplay);
        }
    }

    private GameObject CreateWallObject()
    {
        EnsureWallRoot();
        EnsureCachedResources();
        return WallObjectFactory.CreateWallObject("Wall", wallRoot, cachedCubeMesh, wallMaterial, wallTopMaterial);
    }

    private void EnsureCachedResources()
    {
        if (cachedCubeMesh != null)
        {
            return;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter filter = cube.GetComponent<MeshFilter>();
        if (filter != null)
        {
            cachedCubeMesh = filter.sharedMesh;
        }

        Destroy(cube);
    }

    private Material CreateWallMaterial(Color color, bool transparent)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader)
        {
            color = color,
        };

        if (!transparent)
        {
            return material;
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return material;
    }
}
