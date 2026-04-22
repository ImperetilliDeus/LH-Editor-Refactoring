using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

internal sealed class DwgWallImportProcessingService
{
    public bool TryResolveImportPath(string path, DwgWallImporter importer, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            Debug.LogError("[DwgWallImporter] File path is empty or invalid.", importer);
            return false;
        }

        resolvedPath = CadWallImportService.ResolveFilePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            Debug.LogError($"[{nameof(DwgWallImporter)}] CAD file not found: {path}", importer);
            return false;
        }

        return true;
    }

    public bool TryParse(
        string resolvedPath,
        CadWallImportSettings settings,
        DwgWallImporter importer,
        out CadWallImportParseResult parseResult)
    {
        parseResult = null;
        try
        {
            Debug.Log("[2/6] Reading CAD file...", importer);
            parseResult = CadWallImportService.Parse(resolvedPath, settings);
            Debug.Log($"[3/6] CAD file read succeeded. Layer count: {parseResult.AvailableLayers.Count}", importer);
            return parseResult != null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[DwgWallImporter] Failed to read CAD file.\nReason: {ex.Message}\nStack: {ex.StackTrace}", importer);
            return false;
        }
    }

    public void PopulateSegmentsAndWarnings(
        CadWallImportParseResult parseResult,
        List<CadWallSegment> segments,
        List<string> warnings)
    {
        segments.Clear();
        warnings.Clear();
        if (parseResult == null)
        {
            return;
        }

        segments.AddRange(parseResult.Segments);
        warnings.AddRange(parseResult.Warnings);
    }

    public void CenterSegmentsAtOrigin(
        List<CadWallSegment> segmentDefinitions,
        float drawingPlaneY,
        Vector3 importOffset)
    {
        if (segmentDefinitions == null || segmentDefinitions.Count == 0)
        {
            return;
        }

        Vector3 min = segmentDefinitions[0].Start;
        Vector3 max = segmentDefinitions[0].Start;
        ExpandBounds(segmentDefinitions[0].End, ref min, ref max);

        for (int i = 1; i < segmentDefinitions.Count; i++)
        {
            ExpandBounds(segmentDefinitions[i].Start, ref min, ref max);
            ExpandBounds(segmentDefinitions[i].End, ref min, ref max);
        }

        Vector3 currentCenter = new Vector3((min.x + max.x) * 0.5f, drawingPlaneY, (min.z + max.z) * 0.5f);
        Vector3 targetCenter = new Vector3(importOffset.x, drawingPlaneY, importOffset.z);
        Vector3 recenterOffset = currentCenter - targetCenter;
        if (Mathf.Abs(recenterOffset.x) <= 0.000001f && Mathf.Abs(recenterOffset.z) <= 0.000001f)
        {
            return;
        }

        for (int i = 0; i < segmentDefinitions.Count; i++)
        {
            CadWallSegment definition = segmentDefinitions[i];
            segmentDefinitions[i] = new CadWallSegment(
                definition.Start - recenterOffset,
                definition.End - recenterOffset,
                definition.LayerName,
                definition.SourceType);
        }
    }

    public string BuildAvailableLayerDebugInfo(IReadOnlyList<string> availableLayers)
    {
        StringBuilder debugInfo = new StringBuilder();
        debugInfo.AppendLine("=== Layers found in CAD document ===");
        if (availableLayers != null)
        {
            for (int i = 0; i < availableLayers.Count; i++)
            {
                debugInfo.AppendLine($"- {availableLayers[i]}");
            }
        }

        debugInfo.AppendLine("=========================================");
        return debugInfo.ToString();
    }

    private static void ExpandBounds(Vector3 point, ref Vector3 min, ref Vector3 max)
    {
        min = Vector3.Min(min, point);
        max = Vector3.Max(max, point);
    }
}
