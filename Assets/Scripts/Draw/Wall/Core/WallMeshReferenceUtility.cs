using UnityEngine;

public static class WallMeshReferenceUtility
{
    private static Mesh sharedCubeMesh;

    public static Mesh GetSharedCubeMesh()
    {
        if (sharedCubeMesh != null)
        {
            return sharedCubeMesh;
        }

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        MeshFilter meshFilter = cube.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            sharedCubeMesh = meshFilter.sharedMesh;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(cube);
        }
        else
        {
            Object.DestroyImmediate(cube);
        }

        return sharedCubeMesh;
    }

    public static void ApplyDefaultMeshIfMissing(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = target.AddComponent<MeshFilter>();
        }

        if (meshFilter.sharedMesh == null)
        {
            meshFilter.sharedMesh = GetSharedCubeMesh();
        }
    }
}
