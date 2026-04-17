using System.Collections.Generic;
using UnityEngine;

public class WallSpatialHash
{
    private readonly float cellSize;
    private readonly Dictionary<Vector2Int, HashSet<Wall>> cells = new Dictionary<Vector2Int, HashSet<Wall>>();

    public WallSpatialHash(float cellSize)
    {
        this.cellSize = Mathf.Max(0.01f, cellSize);
    }

    public void Clear()
    {
        cells.Clear();
    }

    public void Insert(Wall wall)
    {
        if (wall == null || wall.Data == null)
        {
            return;
        }

        Vector3 start = wall.Data.startPoint;
        Vector3 end = wall.Data.endPoint;

        int minX = Mathf.FloorToInt(Mathf.Min(start.x, end.x) / cellSize);
        int maxX = Mathf.FloorToInt(Mathf.Max(start.x, end.x) / cellSize);
        int minZ = Mathf.FloorToInt(Mathf.Min(start.z, end.z) / cellSize);
        int maxZ = Mathf.FloorToInt(Mathf.Max(start.z, end.z) / cellSize);

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2Int key = new Vector2Int(x, z);
                if (!cells.TryGetValue(key, out HashSet<Wall> bucket))
                {
                    bucket = new HashSet<Wall>();
                    cells[key] = bucket;
                }

                bucket.Add(wall);
            }
        }
    }

    public void CollectCandidates(Vector3 position, float radius, List<Wall> results, Transform root = null)
    {
        if (results == null)
        {
            return;
        }

        results.Clear();

        float queryRadius = Mathf.Max(0.01f, radius);
        int minX = Mathf.FloorToInt((position.x - queryRadius) / cellSize);
        int maxX = Mathf.FloorToInt((position.x + queryRadius) / cellSize);
        int minZ = Mathf.FloorToInt((position.z - queryRadius) / cellSize);
        int maxZ = Mathf.FloorToInt((position.z + queryRadius) / cellSize);

        HashSet<Wall> uniqueWalls = new HashSet<Wall>();
        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2Int key = new Vector2Int(x, z);
                if (!cells.TryGetValue(key, out HashSet<Wall> bucket))
                {
                    continue;
                }

                foreach (Wall wall in bucket)
                {
                    if (wall == null || !wall.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (root != null && !wall.transform.IsChildOf(root))
                    {
                        continue;
                    }

                    if (uniqueWalls.Add(wall))
                    {
                        results.Add(wall);
                    }
                }
            }
        }
    }
}
