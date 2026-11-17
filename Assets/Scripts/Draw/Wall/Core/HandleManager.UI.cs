using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class HandleManager
{
    private readonly VertexHandleViews handleViews = new VertexHandleViews();
    private readonly List<RectTransform> handleRectBuffer = new List<RectTransform>();

    public bool IsPointerOverHandle(Vector2 screenPoint)
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null || group.handleRect == null)
            {
                continue;
            }

            if (VertexHandleViews.ContainsScreenPoint(group.handleRect, targetCanvas, screenPoint))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateHandlePositions()
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group == null || group.handleRect == null)
            {
                continue;
            }

            if (group.endpoints.Count == 0 || !HandleGeometryUtility.IsValidWorldPoint(group.worldPoint))
            {
                group.handleRect.gameObject.SetActive(false);
                continue;
            }

            PlaceHandle(group);
        }
    }

    private void SetHandlesVisibleForActiveMode()
    {
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            VertexGroup group = vertexGroups[i];
            if (group?.handleRect != null)
            {
                group.handleRect.gameObject.SetActive(IsHandleInteractionModeActive() && ShouldShowHandle(group));
            }
        }
    }

    private void PlaceHandle(VertexGroup group)
    {
        VertexHandleViews.Place(
            group.handleRect,
            group.worldPoint,
            IsHandleInteractionModeActive() && ShouldShowHandle(group),
            mainCamera,
            targetCanvas);
    }

    private void SetGroupColor(VertexGroup group, Color color)
    {
        if (group == null || group.image == null)
        {
            return;
        }

        group.image.color = color;
    }

    private void EnsureCanvas()
    {
        targetCanvas = VertexHandleViews.ResolveCanvas(targetCanvas, HandleCanvasName);
    }

    private void DestroyAllHandleRects()
    {
        handleRectBuffer.Clear();
        for (int i = 0; i < vertexGroups.Count; i++)
        {
            handleRectBuffer.Add(vertexGroups[i]?.handleRect);
        }

        VertexHandleViews.DestroyAll(handleRectBuffer, targetCanvas, HandleObjectNamePrefix);
        handleRectBuffer.Clear();
    }

    private void OnDestroy()
    {
        UnbindModeEvents();
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterGlobalHandler(this);
        }

        previewSnappedGroup = null;

        for (int i = 0; i < vertexGroups.Count; i++)
        {
            if (vertexGroups[i]?.handleRect != null)
            {
                VertexHandleViews.Destroy(vertexGroups[i].handleRect);
            }
        }

        handleViews.ReleaseSprite();

        vertexGroups.Clear();
        groupsByVertexId.Clear();
        wallEntries.Clear();
    }
}
