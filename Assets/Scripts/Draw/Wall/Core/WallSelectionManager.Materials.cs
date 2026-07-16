using System.Collections.Generic;
using UnityEngine;

public partial class WallSelectionManager
{
    private readonly List<GameObject> selectedWallsForMaterial = new List<GameObject>();

    public int ApplyMaterialToSelectedWalls(Material material)
    {
        return ApplyMaterialToSelectedWalls(material, material != null ? material.name : string.Empty);
    }

    public int ApplyMaterialToSelectedWalls(Material material, string textureCode)
    {
        if (material == null)
        {
            return 0;
        }

        GetSelectedWalls(selectedWallsForMaterial);
        int appliedCount = 0;
        for (int i = 0; i < selectedWallsForMaterial.Count; i++)
        {
            appliedCount += ApplyMaterialToWallSelectionObject(selectedWallsForMaterial[i], material, textureCode);
        }

        if (appliedCount > 0)
        {
            EditorVisualEvents.RequestTopViewRefresh();
        }

        return appliedCount;
    }

    private static int ApplyMaterialToWallSelectionObject(GameObject selectionObject, Material material, string textureCode)
    {
        if (selectionObject == null || material == null)
        {
            return 0;
        }

        if (selectionObject.TryGetComponent(out WallOpeningContainer container))
        {
            int containerAppliedCount = 0;
            Wall[] walls = container.GetComponentsInChildren<Wall>(true);
            for (int i = 0; i < walls.Length; i++)
            {
                containerAppliedCount += ApplyMaterialToWall(walls[i], material, textureCode);
            }

            return containerAppliedCount;
        }

        return ApplyMaterialToWall(selectionObject.GetComponent<Wall>(), material, textureCode);
    }

    private static int ApplyMaterialToWall(Wall wall, Material material, string textureCode)
    {
        if (wall == null || material == null)
        {
            return 0;
        }

        MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return 0;
        }

        renderer.sharedMaterial = material;
        wall.Data.TextureCode = textureCode ?? string.Empty;
        wall.RefreshEndCapVisuals();
        return 1;
    }
}
