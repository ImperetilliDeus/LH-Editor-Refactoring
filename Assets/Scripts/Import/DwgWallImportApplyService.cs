using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class DwgWallImportApplyService
{
    public bool TryApply(
        IReadOnlyList<CadWallSegment> segments,
        DwgWallImportSceneApplyContext context,
        DwgWallImporter importer,
        out DwgWallImportSceneApplyResult applyResult)
    {
        applyResult = null;
        try
        {
            applyResult = DwgWallImportSceneApplier.Apply(segments, context);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[{nameof(DwgWallImporter)}] Failed while applying imported walls.\nReason: {ex.Message}\nStack: {ex.StackTrace}", importer);
            return false;
        }
    }

    public void LogWarnings(IEnumerable<string> warnings, DwgWallImporter importer)
    {
        if (warnings == null)
        {
            return;
        }

        HashSet<string> uniqueWarnings = new HashSet<string>(warnings, StringComparer.Ordinal);
        foreach (string warning in uniqueWarnings)
        {
            Debug.LogWarning($"[{nameof(DwgWallImporter)}] {warning}", importer);
        }
    }

    public void LogImportSummary(
        string resolvedPath,
        DwgWallImportSceneApplyResult applyResult,
        DwgWallImporter importer)
    {
        if (applyResult == null)
        {
            return;
        }

        Debug.Log(
            $"[{nameof(DwgWallImporter)}] Imported {applyResult.CreatedWallCount} wall segments from '{resolvedPath}'. " +
            $"Removed owned walls: {applyResult.RemovedWallCount}, removed auto rooms: {applyResult.RemovedRoomCount}.",
            importer);
    }
}
