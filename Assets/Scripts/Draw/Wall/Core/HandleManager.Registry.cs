using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class HandleManager
{
    private void EnsureWallVertexIds(Wall wall)
    {
        if (wall == null)
        {
            return;
        }

        Vector3 startPoint = wall.Data.startPoint;
        Vector3 endPoint = wall.Data.endPoint;

        int startId = wall.StartVertexId;
        int endId = wall.EndVertexId;

        if (!wall.SuppressStartHandle && startId <= 0)
        {
            startId = FindNearestVertexId(startPoint);
            if (startId <= 0)
            {
                startId = AllocateVertexId();
            }
        }

        if (!wall.SuppressEndHandle && endId <= 0)
        {
            endId = FindNearestVertexId(endPoint);
            if (endId <= 0 || endId == startId)
            {
                endId = AllocateVertexId();
            }
        }

        wall.SetVertexIds(startId, endId);
    }

    private int FindNearestVertexId(Vector3 point)
    {
        float thresholdSqr = endpointMergeThreshold * endpointMergeThreshold;
        int foundId = -1;
        float closestSqr = thresholdSqr;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            float distanceSqr = (new Vector2(group.worldPoint.x - point.x, group.worldPoint.z - point.z)).sqrMagnitude;
            if (distanceSqr > closestSqr)
            {
                continue;
            }

            closestSqr = distanceSqr;
            foundId = group.vertexId;
        }

        return foundId;
    }

    private int AllocateVertexId()
    {
        while (groupsByVertexId.ContainsKey(nextVertexId))
        {
            nextVertexId++;
        }

        return nextVertexId++;
    }

    private void AddEntryToVertexGroup(WallHandleEntry entry, bool isStart)
    {
        if (entry?.wallComponent == null)
        {
            return;
        }

        if ((isStart && entry.wallComponent.SuppressStartHandle) ||
            (!isStart && entry.wallComponent.SuppressEndHandle))
        {
            return;
        }

        int vertexId = isStart ? entry.wallComponent.StartVertexId : entry.wallComponent.EndVertexId;
        if (vertexId <= 0)
        {
            return;
        }

        Vector3 point = isStart ? entry.wallComponent.Data.startPoint : entry.wallComponent.Data.endPoint;

        VertexGroup group = GetOrCreateGroup(vertexId, point);
        group.endpoints.Add(new EndpointRef
        {
            entry = entry,
            isStart = isStart,
        });

        UpdateGroupWorldPoint(group);
        SetGroupColor(group, GetBaseColor(group));
    }

    private VertexGroup GetOrCreateGroup(int vertexId, Vector3 initialPoint)
    {
        if (groupsByVertexId.TryGetValue(vertexId, out VertexGroup existing))
        {
            return existing;
        }

        EnsureCanvas();
        RectTransform rect = CreateHandleRect($"Handle_Vertex_{vertexId}", out Image image);

        VertexGroup group = new VertexGroup
        {
            vertexId = vertexId,
            handleRect = rect,
            image = image,
            worldPoint = initialPoint,
        };

        groupsByVertexId[vertexId] = group;
        vertexGroups.Add(group);
        SetGroupColor(group, GetBaseColor(group));

        return group;
    }

    private void RemoveEntryFromAllGroups(WallHandleEntry entry)
    {
        for (int i = vertexGroups.Count - 1; i >= 0; i--)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null)
            {
                continue;
            }

            for (int j = group.endpoints.Count - 1; j >= 0; j--)
            {
                if (group.endpoints[j].entry == entry)
                {
                    group.endpoints.RemoveAt(j);
                }
            }

            if (group.endpoints.Count > 0)
            {
                UpdateGroupWorldPoint(group);
                SetGroupColor(group, GetBaseColor(group));
                continue;
            }

            if (group.handleRect != null)
            {
                Destroy(group.handleRect.gameObject);
            }

            groupsByVertexId.Remove(group.vertexId);
            vertexGroups.RemoveAt(i);
        }
    }

    private void UpdateGroupWorldPoint(VertexGroup group)
    {
        if (group == null || group.endpoints.Count == 0)
        {
            return;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;

        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            if (endpointRef?.entry?.wallComponent == null)
            {
                continue;
            }

            Vector3 point = endpointRef.isStart
                ? endpointRef.entry.wallComponent.Data.startPoint
                : endpointRef.entry.wallComponent.Data.endPoint;

            sum += point;
            count++;
        }

        if (count == 0)
        {
            return;
        }

        group.worldPoint = sum / count;
        group.worldPoint.y = dragPlaneHeight;
    }

    private void RefreshAllGroupWorldPoints()
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            UpdateGroupWorldPoint(vertexGroups[i]);
        }
    }

    private VertexGroup TryMergeDraggedGroupToNearby(VertexGroup source)
    {
        if (source == null)
        {
            return null;
        }

        float thresholdSqr = endpointMergeThreshold * endpointMergeThreshold;
        VertexGroup target = null;
        float closestSqr = thresholdSqr;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup candidate = vertexGroups[i];
            if (candidate == null || candidate == source)
            {
                continue;
            }

            float distanceSqr = (new Vector2(candidate.worldPoint.x - source.worldPoint.x, candidate.worldPoint.z - source.worldPoint.z)).sqrMagnitude;
            if (distanceSqr > closestSqr)
            {
                continue;
            }

            closestSqr = distanceSqr;
            target = candidate;
        }

        if (target == null)
        {
            return source;
        }

        int oldVertexId = source.vertexId;
        int newVertexId = target.vertexId;

        for (int i = 0; i < source.endpoints.Count; i++)
        {
            EndpointRef endpointRef = source.endpoints[i];
            if (endpointRef?.entry?.wallComponent == null)
            {
                continue;
            }

            Wall wall = endpointRef.entry.wallComponent;
            if (wall.StartVertexId == oldVertexId)
            {
                wall.StartVertexId = newVertexId;
            }

            if (wall.EndVertexId == oldVertexId)
            {
                wall.EndVertexId = newVertexId;
            }
        }

        RemoveGroupById(oldVertexId);
        RebuildGroupsFromEntries();
        if (groupsByVertexId.TryGetValue(newVertexId, out VertexGroup rebuiltTarget))
        {
            return rebuiltTarget;
        }

        return source;
    }

    private VertexGroup FindClosestGroupByPoint(Vector3 point, float thresholdSqr)
    {
        VertexGroup found = null;
        float closestSqr = thresholdSqr;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null)
            {
                continue;
            }

            float dx = group.worldPoint.x - point.x;
            float dz = group.worldPoint.z - point.z;
            float distanceSqr = dx * dx + dz * dz;
            if (distanceSqr > closestSqr)
            {
                continue;
            }

            closestSqr = distanceSqr;
            found = group;
        }

        return found;
    }

    private void RebuildGroupsFromEntries()
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            if (vertexGroups[i]?.handleRect != null)
            {
                Destroy(vertexGroups[i].handleRect.gameObject);
            }
        }

        groupsByVertexId.Clear();
        vertexGroups.Clear();

        foreach (KeyValuePair<int, WallHandleEntry> pair in wallEntries)
        {
            WallHandleEntry entry = pair.Value;
            if (entry?.wallComponent == null)
            {
                continue;
            }

            AddEntryToVertexGroup(entry, true);
            AddEntryToVertexGroup(entry, false);
        }

        RefreshAllGroupWorldPoints();
    }

    private void RemoveGroupById(int vertexId)
    {
        if (!groupsByVertexId.TryGetValue(vertexId, out VertexGroup group))
        {
            return;
        }

        if (group.handleRect != null)
        {
            Destroy(group.handleRect.gameObject);
        }

        groupsByVertexId.Remove(vertexId);
        vertexGroups.Remove(group);
    }

    private void CollectAffectedWallsForGroup(VertexGroup group, HashSet<GameObject> wallObjects, List<Wall> walls)
    {
        if (wallObjects == null || walls == null)
        {
            return;
        }

        wallObjects.Clear();
        walls.Clear();

        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.endpoints.Count; i++)
        {
            EndpointRef endpointRef = group.endpoints[i];
            if (endpointRef?.entry?.wall != null)
            {
                wallObjects.Add(endpointRef.entry.wall);
            }
        }

        if (!IsSplitPointGroup(group) && TryBuildSplitChainFromEndpoint(group.vertexId, splitChainWalls, splitChainVertexIds, splitChainPoints))
        {
            for (int i = 0; i < splitChainWalls.Count; i++)
            {
                Wall chainWall = splitChainWalls[i];
                if (chainWall != null)
                {
                    wallObjects.Add(chainWall.gameObject);
                }
            }
        }

        foreach (GameObject wallObject in wallObjects)
        {
            if (wallObject == null)
            {
                continue;
            }

            Wall wall = wallObject.GetComponent<Wall>();
            if (wall != null)
            {
                walls.Add(wall);
            }
        }
    }
}
