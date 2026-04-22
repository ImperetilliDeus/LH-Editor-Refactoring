using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class WallPropertyMutationService
{
    public void RecordAndRefresh(
        List<UndoRedoManager.WallStateChangeRecord> records,
        GameObject selectedWall,
        UndoRedoManager undoRedoManager,
        HandleManager handleManager,
        WallSelectionManager wallSelectionManager,
        Action markTopViewDirty,
        Action<bool> updateInputFieldValues)
    {
        if (records != null && records.Count > 0 && undoRedoManager != null)
        {
            undoRedoManager.RecordMoveConnectedWalls(records);
        }

        handleManager?.RefreshRegisteredWalls();
        RoomTopologyEvents.RequestRefreshAll();
        markTopViewDirty?.Invoke();
        updateInputFieldValues?.Invoke(true);

        if (selectedWall != null && wallSelectionManager != null)
        {
            wallSelectionManager.SetSelectedWall(selectedWall);
        }
    }

    public void ApplyContainerHeight(
        WallOpeningContainer container,
        float targetHeightUnits,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        UndoRedoManager undoRedoManager,
        HandleManager handleManager,
        Action markTopViewDirty,
        Action<bool> updateInputFieldValues,
        bool refreshInputFields)
    {
        if (container == null || wallOpeningPlacementManager == null)
        {
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
        container.SetWallHeightKeepingBottom(targetHeightUnits);
        wallOpeningPlacementManager.RebuildOpeningContainer(container);
        wallOpeningPlacementManager.SelectPreferredWallForContainer(container, container.WallLength * 0.5f);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, wallOpeningPlacementManager.CaptureLayoutSnapshot(container));
        }

        handleManager?.RefreshRegisteredWalls();
        RoomTopologyEvents.RequestRefreshAll();
        markTopViewDirty?.Invoke();
        if (refreshInputFields)
        {
            updateInputFieldValues?.Invoke(true);
        }
    }

    public bool TryApplyContainerHeightFromSelectedWall(
        Wall selectedWallComponent,
        float targetHeightUnits,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        UndoRedoManager undoRedoManager,
        HandleManager handleManager,
        Action markTopViewDirty,
        Action<bool> updateInputFieldValues)
    {
        if (selectedWallComponent == null || wallOpeningPlacementManager == null)
        {
            return false;
        }

        WallOpeningContainer container = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        ApplyContainerHeight(
            container,
            targetHeightUnits,
            wallOpeningPlacementManager,
            undoRedoManager,
            handleManager,
            markTopViewDirty,
            updateInputFieldValues,
            true);
        return true;
    }

    public void ApplyContainerThickness(
        WallOpeningContainer container,
        float targetThicknessUnits,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        UndoRedoManager undoRedoManager,
        HandleManager handleManager,
        Action markTopViewDirty,
        Action<bool> updateInputFieldValues,
        bool refreshInputFields)
    {
        if (container == null || wallOpeningPlacementManager == null)
        {
            return;
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
        container.SetWallThickness(targetThicknessUnits);
        wallOpeningPlacementManager.RebuildOpeningContainer(container);
        wallOpeningPlacementManager.SelectPreferredWallForContainer(container, container.WallLength * 0.5f);

        if (undoRedoManager != null)
        {
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, wallOpeningPlacementManager.CaptureLayoutSnapshot(container));
        }

        handleManager?.RefreshRegisteredWalls();
        RoomTopologyEvents.RequestRefreshAll();
        markTopViewDirty?.Invoke();
        if (refreshInputFields)
        {
            updateInputFieldValues?.Invoke(true);
        }
    }

    public bool TryApplyContainerThicknessFromSelectedWall(
        Wall selectedWallComponent,
        float targetThicknessUnits,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        UndoRedoManager undoRedoManager,
        HandleManager handleManager,
        Action markTopViewDirty,
        Action<bool> updateInputFieldValues)
    {
        if (selectedWallComponent == null || wallOpeningPlacementManager == null)
        {
            return false;
        }

        WallOpeningContainer container = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        ApplyContainerThickness(
            container,
            targetThicknessUnits,
            wallOpeningPlacementManager,
            undoRedoManager,
            handleManager,
            markTopViewDirty,
            updateInputFieldValues,
            true);
        return true;
    }

    public void ApplyWallHeight(
        GameObject selectedWall,
        Wall selectedWallComponent,
        float targetHeightUnits,
        WallLengthDisplay wallLengthDisplay,
        Action<List<UndoRedoManager.WallStateChangeRecord>> recordAndRefresh)
    {
        if (selectedWall == null || selectedWallComponent == null)
        {
            return;
        }

        UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(selectedWall);
        Transform wallTransform = selectedWall.transform;
        Vector3 scale = wallTransform.localScale;
        float bottomY = wallTransform.position.y - scale.y * 0.5f;

        scale.y = targetHeightUnits;
        wallTransform.localScale = scale;

        Vector3 position = wallTransform.position;
        position.y = bottomY + targetHeightUnits * 0.5f;
        wallTransform.position = position;

        selectedWallComponent.SyncEndpointsFromTransform(selectedWallComponent.Data.startPoint.y);
        selectedWallComponent.RefreshLengthDisplay(wallLengthDisplay, false);

        recordAndRefresh?.Invoke(new List<UndoRedoManager.WallStateChangeRecord>
        {
            new UndoRedoManager.WallStateChangeRecord
            {
                before = before,
                after = UndoRedoManager.WallStateSnapshot.Capture(selectedWall),
            }
        });
    }

    public void ApplyWallThickness(
        GameObject selectedWall,
        Wall selectedWallComponent,
        float targetThicknessUnits,
        WallLengthDisplay wallLengthDisplay,
        Action<List<UndoRedoManager.WallStateChangeRecord>> recordAndRefresh)
    {
        if (selectedWall == null || selectedWallComponent == null)
        {
            return;
        }

        UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(selectedWall);
        Vector3 scale = selectedWall.transform.localScale;
        scale.x = targetThicknessUnits;
        selectedWall.transform.localScale = scale;

        selectedWallComponent.SyncEndpointsFromTransform(selectedWallComponent.Data.startPoint.y);
        selectedWallComponent.RefreshLengthDisplay(wallLengthDisplay, false);

        recordAndRefresh?.Invoke(new List<UndoRedoManager.WallStateChangeRecord>
        {
            new UndoRedoManager.WallStateChangeRecord
            {
                before = before,
                after = UndoRedoManager.WallStateSnapshot.Capture(selectedWall),
            }
        });
    }

    public void ApplyWallLength(
        Wall selectedWallComponent,
        float targetLengthUnits,
        bool keepsStartFixed,
        float minimumWallLength,
        WallLengthDisplay wallLengthDisplay,
        List<Wall> resizeAffectedWalls,
        Action<int, List<Wall>> collectWallsSharingVertex,
        Action<List<UndoRedoManager.WallStateChangeRecord>> recordAndRefresh)
    {
        recordAndRefresh?.Invoke(
            BuildWallLengthChangeRecords(
                selectedWallComponent,
                targetLengthUnits,
                keepsStartFixed,
                minimumWallLength,
                wallLengthDisplay,
                resizeAffectedWalls,
                collectWallsSharingVertex));
    }

    public void AppendWallLengthChangeRecords(
        List<UndoRedoManager.WallStateChangeRecord> records,
        Wall selectedWallComponent,
        float targetLengthUnits,
        bool keepsStartFixed,
        float minimumWallLength,
        WallLengthDisplay wallLengthDisplay,
        List<Wall> resizeAffectedWalls,
        Action<int, List<Wall>> collectWallsSharingVertex)
    {
        if (records == null)
        {
            return;
        }

        records.AddRange(
            BuildWallLengthChangeRecords(
                selectedWallComponent,
                targetLengthUnits,
                keepsStartFixed,
                minimumWallLength,
                wallLengthDisplay,
                resizeAffectedWalls,
                collectWallsSharingVertex));
    }

    public bool TryApplyContainerLengthFromSelectedWall(
        Wall selectedWallComponent,
        float targetLengthUnits,
        bool keepsStartFixed,
        float minimumWallLength,
        WallLengthDisplay wallLengthDisplay,
        WallOpeningPlacementManager wallOpeningPlacementManager,
        UndoRedoManager undoRedoManager,
        HandleManager handleManager,
        Action markTopViewDirty,
        Action<bool> updateInputFieldValues,
        List<Wall> resizeAffectedWalls,
        List<UndoRedoManager.WallStateChangeRecord> resizeStateRecords,
        Action<int, List<Wall>> collectWallsSharingVertex)
    {
        if (selectedWallComponent == null || wallOpeningPlacementManager == null)
        {
            return false;
        }

        WallOpeningContainer container = selectedWallComponent.GetComponentInParent<WallOpeningContainer>();
        if (container == null)
        {
            return false;
        }

        Vector3 direction = container.WallDirection;
        Vector3 oldStart = container.WallStart;
        Vector3 oldEnd = container.WallEnd;
        Vector3 newStart = oldStart;
        Vector3 newEnd = oldEnd;
        float openingShift = 0f;

        if (keepsStartFixed)
        {
            newEnd = oldStart + direction * targetLengthUnits;
        }
        else
        {
            newStart = oldEnd - direction * targetLengthUnits;
            openingShift = Vector3.Dot(oldStart - newStart, direction);
        }

        WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
        float minimumSideWallUnits = wallOpeningPlacementManager.MinimumSideWallUnits;
        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            float nextCenterDistance = opening.CenterDistance + openingShift;
            float halfWidth = opening.Width * 0.5f;
            if (nextCenterDistance - halfWidth < minimumSideWallUnits ||
                nextCenterDistance + halfWidth > targetLengthUnits - minimumSideWallUnits)
            {
                updateInputFieldValues?.Invoke(true);
                return true;
            }
        }

        UndoRedoManager.OpeningLayoutSnapshot beforeSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
        resizeStateRecords?.Clear();

        int movedVertexId = keepsStartFixed ? container.OuterEndVertexId : container.OuterStartVertexId;
        Vector3 movedPoint = keepsStartFixed ? newEnd : newStart;
        collectWallsSharingVertex?.Invoke(movedVertexId, resizeAffectedWalls);
        for (int i = resizeAffectedWalls.Count - 1; i >= 0; i--)
        {
            Wall wall = resizeAffectedWalls[i];
            if (wall == null)
            {
                resizeAffectedWalls.RemoveAt(i);
                continue;
            }

            if (wall.GetComponentInParent<WallOpeningContainer>() == container)
            {
                resizeAffectedWalls.RemoveAt(i);
                continue;
            }

            resizeStateRecords?.Add(new UndoRedoManager.WallStateChangeRecord
            {
                before = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject),
            });
        }

        container.SetWallSpan(newStart, newEnd);

        for (int i = 0; i < openings.Length; i++)
        {
            WallOpening opening = openings[i];
            if (opening == null)
            {
                continue;
            }

            opening.SetCenterDistance(opening.CenterDistance + openingShift);
        }

        wallOpeningPlacementManager.RebuildOpeningContainer(container);
        wallOpeningPlacementManager.SelectPreferredWallForContainer(container, targetLengthUnits * 0.5f);
        if (resizeAffectedWalls.Count > 0)
        {
            WallGeometryService.ApplyVertexMove(
                resizeAffectedWalls,
                movedVertexId,
                movedPoint,
                movedPoint.y,
                minimumWallLength,
                wallLengthDisplay);
        }

        if (undoRedoManager != null)
        {
            UndoRedoManager.OpeningLayoutSnapshot afterSnapshot = wallOpeningPlacementManager.CaptureLayoutSnapshot(container);
            undoRedoManager.RecordOpeningLayoutChange(beforeSnapshot, afterSnapshot);

            if (resizeStateRecords != null)
            {
                for (int i = 0; i < resizeStateRecords.Count; i++)
                {
                    UndoRedoManager.WallStateChangeRecord record = resizeStateRecords[i];
                    if (record.before.wallObject == null)
                    {
                        continue;
                    }

                    record.after = UndoRedoManager.WallStateSnapshot.Capture(record.before.wallObject);
                    resizeStateRecords[i] = record;
                }

                undoRedoManager.RecordMoveConnectedWalls(resizeStateRecords);
            }
        }

        handleManager?.RefreshRegisteredWalls();
        RoomTopologyEvents.RequestRefreshAll();
        markTopViewDirty?.Invoke();
        updateInputFieldValues?.Invoke(true);

        return true;
    }

    public List<UndoRedoManager.WallStateChangeRecord> BuildWallLengthChangeRecords(
        Wall selectedWallComponent,
        float targetLengthUnits,
        bool keepsStartFixed,
        float minimumWallLength,
        WallLengthDisplay wallLengthDisplay,
        List<Wall> resizeAffectedWalls,
        Action<int, List<Wall>> collectWallsSharingVertex)
    {
        List<UndoRedoManager.WallStateChangeRecord> records = new List<UndoRedoManager.WallStateChangeRecord>();
        if (selectedWallComponent == null)
        {
            return records;
        }

        Vector3 startPoint = selectedWallComponent.Data.startPoint;
        Vector3 currentEndPoint = selectedWallComponent.Data.endPoint;
        Vector3 direction = currentEndPoint - startPoint;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.000001f)
        {
            return records;
        }

        direction.Normalize();
        Vector3 targetStartPoint = keepsStartFixed ? startPoint : currentEndPoint - direction * targetLengthUnits;
        Vector3 targetEndPoint = keepsStartFixed ? startPoint + direction * targetLengthUnits : currentEndPoint;
        targetStartPoint.y = startPoint.y;
        targetEndPoint.y = startPoint.y;

        GameObject wallObject = selectedWallComponent.gameObject;
        int movedVertexId = keepsStartFixed ? selectedWallComponent.EndVertexId : selectedWallComponent.StartVertexId;
        Vector3 movedPoint = keepsStartFixed ? targetEndPoint : targetStartPoint;

        if (movedVertexId > 0)
        {
            collectWallsSharingVertex?.Invoke(movedVertexId, resizeAffectedWalls);
            for (int i = 0; i < resizeAffectedWalls.Count; i++)
            {
                Wall wall = resizeAffectedWalls[i];
                if (wall == null)
                {
                    continue;
                }

                records.Add(new UndoRedoManager.WallStateChangeRecord
                {
                    before = UndoRedoManager.WallStateSnapshot.Capture(wall.gameObject),
                });
            }

            WallGeometryService.ApplyVertexMove(
                resizeAffectedWalls,
                movedVertexId,
                movedPoint,
                startPoint.y,
                minimumWallLength,
                wallLengthDisplay);

            for (int i = records.Count - 1; i >= 0; i--)
            {
                UndoRedoManager.WallStateChangeRecord record = records[i];
                if (record.before.wallObject == null)
                {
                    records.RemoveAt(i);
                    continue;
                }

                record.after = UndoRedoManager.WallStateSnapshot.Capture(record.before.wallObject);
                records[i] = record;
            }

            return records;
        }

        UndoRedoManager.WallStateSnapshot before = UndoRedoManager.WallStateSnapshot.Capture(wallObject);
        bool applied = selectedWallComponent.TryApplyCurrentProfileAndRefresh(
            targetStartPoint,
            targetEndPoint,
            minimumWallLength,
            wallLengthDisplay,
            false);
        if (!applied)
        {
            return records;
        }

        records.Add(new UndoRedoManager.WallStateChangeRecord
        {
            before = before,
            after = UndoRedoManager.WallStateSnapshot.Capture(wallObject),
        });
        return records;
    }
}
