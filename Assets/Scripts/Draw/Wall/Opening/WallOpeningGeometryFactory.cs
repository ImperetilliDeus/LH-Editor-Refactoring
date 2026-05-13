using UnityEngine;

internal sealed class WallOpeningGeometryFactory
{
    public GameObject CreateStandaloneWallSegment(
        Transform wallRoot,
        Mesh cubeMesh,
        WallLengthDisplay wallLengthDisplay,
        HandleManager handleManager,
        System.Action<Object> destroyObject,
        string segmentName,
        Vector3 startPoint,
        Vector3 endPoint,
        float thickness,
        float height,
        float centerY,
        int startVertexId,
        int endVertexId,
        bool suppressStartHandle,
        bool suppressEndHandle,
        bool startSplitPoint,
        bool endSplitPoint,
        WallVisualState visualState, // No change here
        float minimumWallSegmentLength,
        bool isDragging = false) // Added isDragging
    {
        Vector3 direction = endPoint - startPoint;
        direction.y = 0f;
        if (direction.magnitude < minimumWallSegmentLength)
        {
            return null;
        }

        GameObject wallObject = WallObjectFactory.CreateWallObject(
            segmentName,
            wallRoot,
            cubeMesh,
            visualState);
        bool applied = WallObjectFactory.ConfigureWall(
            wallObject,
            new WallData(startPoint, endPoint, thickness, height, centerY),
            startVertexId,
            endVertexId,
            suppressStartHandle,
            suppressEndHandle,
            startSplitPoint,
            endSplitPoint,
            minimumWallSegmentLength,
            wallLengthDisplay,
            false);

        if (!applied)
        {
            destroyObject?.Invoke(wallObject);
            return null;
        }

        if (!isDragging) // Only register if not dragging
        {
            handleManager?.RegisterWall(wallObject);
        }
        return wallObject;
    }

    public void CreateFillerSegment(
        Transform parent,
        Mesh cubeMesh,
        string fillerName,
        WallOpeningContainer container,
        WallOpening opening,
        float segmentHeight,
        float segmentBottomY)
    {
        if (segmentHeight <= 0.01f || parent == null || cubeMesh == null || container == null || opening == null)
        {
            return;
        }

        Vector3 openingCenter = container.WallStart + container.WallDirection * opening.CenterDistance;
        GameObject filler = CreateCubeObject(cubeMesh, fillerName, parent, false);
        filler.name = fillerName;
        LayerUtility.ApplyLayer(filler, LayerUtility.WallLayerName, false);
        if (parent != null && parent.GetComponent<Wall>() != null)
        {
            Vector3 parentScale = parent.localScale;
            filler.transform.localPosition = new Vector3(
                0f,
                SafeDivide((segmentBottomY + segmentHeight * 0.5f) - parent.position.y, parentScale.y),
                0f);
            filler.transform.localRotation = Quaternion.identity;
            filler.transform.localScale = new Vector3(
                SafeDivide(container.WallThickness, parentScale.x),
                SafeDivide(segmentHeight, parentScale.y),
                SafeDivide(opening.Width, parentScale.z));
        }
        else
        {
            filler.transform.position = new Vector3(
                openingCenter.x,
                segmentBottomY + segmentHeight * 0.5f,
                openingCenter.z);
            filler.transform.rotation = Quaternion.LookRotation(container.WallDirection, Vector3.up);
            filler.transform.localScale = new Vector3(container.WallThickness, segmentHeight, opening.Width);
        }

        MeshRenderer renderer = filler.GetComponent<MeshRenderer>();
        if (renderer != null && container.WallMaterial != null)
        {
            renderer.sharedMaterial = container.WallMaterial;
        }

        WallTopFaceVisual topFaceVisual = filler.GetComponent<WallTopFaceVisual>();
        if (topFaceVisual == null)
        {
            topFaceVisual = filler.AddComponent<WallTopFaceVisual>();
        }

        topFaceVisual.SetTopMaterial(container.WallTopMaterial);
        topFaceVisual.SetWorldOffset(0.01f);
    }

    public GameObject CreateCubeObject(Mesh cubeMesh, string objectName, Transform parent, bool withCollider)
    {
        GameObject cubeObject = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        cubeObject.transform.SetParent(parent, true);

        MeshFilter meshFilter = cubeObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = cubeMesh;
        }

        if (withCollider)
        {
            cubeObject.AddComponent<BoxCollider>();
        }

        return cubeObject;
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Abs(denominator) > 0.000001f ? numerator / denominator : 0f;
    }
}
