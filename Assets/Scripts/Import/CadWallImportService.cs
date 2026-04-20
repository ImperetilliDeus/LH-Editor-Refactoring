using System;
using System.Collections.Generic;
using System.IO;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using UnityEngine;

public readonly struct CadWallSegment
{
    public CadWallSegment(Vector3 start, Vector3 end, string layerName, string sourceType)
    {
        Start = start;
        End = end;
        LayerName = layerName ?? string.Empty;
        SourceType = sourceType ?? string.Empty;
    }

    public Vector3 Start { get; }
    public Vector3 End { get; }
    public string LayerName { get; }
    public string SourceType { get; }
}

public sealed class CadWallImportSettings
{
    public float CadUnitToWorldScale { get; set; }
    public bool InvertCadY { get; set; }
    public float DrawingPlaneY { get; set; }
    public Vector3 ImportOffset { get; set; }
    public float MinimumWallLength { get; set; }
    public bool IncludeInvisibleEntities { get; set; }
    public bool DeduplicateSegments { get; set; }
    public float DeduplicateTolerance { get; set; }
    public string[] IncludedLayers { get; set; } = Array.Empty<string>();
    public string[] ExcludedLayers { get; set; } = Array.Empty<string>();
    public string TargetLayerKeyword { get; set; } = string.Empty;
}

public sealed class CadWallImportParseResult
{
    public string ResolvedPath { get; set; } = string.Empty;
    public List<CadWallSegment> Segments { get; set; } = new List<CadWallSegment>();
    public List<string> AvailableLayers { get; set; } = new List<string>();
    public List<string> Warnings { get; set; } = new List<string>();
}

public static class CadWallImportService
{
    public static string ResolveFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
    }

    public static bool ShouldImportLayerByDefault(string layerName, CadWallImportSettings settings)
    {
        if (string.IsNullOrWhiteSpace(layerName) || settings == null)
        {
            return false;
        }

        if (ContainsLayer(settings.ExcludedLayers, layerName))
        {
            return false;
        }

        if (settings.IncludedLayers != null && settings.IncludedLayers.Length > 0)
        {
            return ContainsLayer(settings.IncludedLayers, layerName);
        }

        if (!string.IsNullOrWhiteSpace(settings.TargetLayerKeyword))
        {
            return layerName.IndexOf(settings.TargetLayerKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        return true;
    }

    public static List<string> LoadAvailableLayers(string path)
    {
        string resolvedPath = ResolveFilePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            return new List<string>();
        }

        CadDocument document = ReadDocument(resolvedPath);
        HashSet<string> uniqueLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectLayerNamesFromEntities(document.Entities, uniqueLayers);

        if (document.Layers != null)
        {
            foreach (var layer in document.Layers)
            {
                if (layer == null || string.IsNullOrWhiteSpace(layer.Name))
                {
                    continue;
                }

                uniqueLayers.Add(layer.Name);
            }
        }

        List<string> layers = new List<string>(uniqueLayers);
        layers.Sort(StringComparer.OrdinalIgnoreCase);
        return layers;
    }

    public static CadWallImportParseResult Parse(string path, CadWallImportSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        string resolvedPath = ResolveFilePath(path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            throw new FileNotFoundException("CAD file not found.", path);
        }

        CadDocument document = ReadDocument(resolvedPath);
        HashSet<string> uniqueSegmentKeys = new HashSet<string>(StringComparer.Ordinal);
        List<string> warnings = new List<string>();
        List<CadWallSegment> segments = new List<CadWallSegment>();

        ExtractSegmentsRecursive(document.Entities, settings, uniqueSegmentKeys, warnings, segments);

        return new CadWallImportParseResult
        {
            ResolvedPath = resolvedPath,
            Segments = segments,
            AvailableLayers = LoadAvailableLayers(resolvedPath),
            Warnings = warnings,
        };
    }

    private static CadDocument ReadDocument(string path)
    {
        string extension = Path.GetExtension(path);
        if (string.Equals(extension, ".dwg", StringComparison.OrdinalIgnoreCase))
        {
            return DwgReader.Read(path);
        }

        if (string.Equals(extension, ".dxf", StringComparison.OrdinalIgnoreCase))
        {
            return DxfReader.Read(path);
        }

        throw new NotSupportedException($"Unsupported CAD extension '{extension}'. Only .dwg and .dxf are supported.");
    }

    private static void CollectLayerNamesFromEntities(IEnumerable<Entity> entities, HashSet<string> results)
    {
        if (entities == null || results == null)
        {
            return;
        }

        foreach (Entity entity in entities)
        {
            if (entity == null)
            {
                continue;
            }

            string layerName = GetLayerName(entity);
            if (!string.IsNullOrWhiteSpace(layerName))
            {
                results.Add(layerName);
            }

            if (entity is Insert insert && insert.Block != null)
            {
                CollectLayerNamesFromEntities(insert.Block.Entities, results);
            }
        }
    }

    private static void ExtractSegmentsRecursive(
        IEnumerable<Entity> entities,
        CadWallImportSettings settings,
        HashSet<string> uniqueSegmentKeys,
        List<string> warnings,
        List<CadWallSegment> results)
    {
        if (entities == null || settings == null || uniqueSegmentKeys == null || warnings == null || results == null)
        {
            return;
        }

        foreach (Entity entity in entities)
        {
            if (entity == null)
            {
                continue;
            }

            if (entity is Insert insert && insert.Block != null)
            {
                ExtractSegmentsRecursive(insert.Block.Entities, settings, uniqueSegmentKeys, warnings, results);
            }

            if (!ShouldImportEntity(entity, settings))
            {
                continue;
            }

            switch (entity)
            {
                case Line line:
                    AddSegment(line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y, GetLayerName(entity), nameof(Line), settings, uniqueSegmentKeys, results);
                    break;
                case LwPolyline lwPolyline:
                    ExtractLwPolylineSegments(lwPolyline, settings, uniqueSegmentKeys, warnings, results);
                    break;
                case Polyline2D polyline2D:
                    ExtractPolyline2DSegments(polyline2D, settings, uniqueSegmentKeys, warnings, results);
                    break;
            }
        }
    }

    private static bool ShouldImportEntity(Entity entity, CadWallImportSettings settings)
    {
        if (entity == null)
        {
            return false;
        }

        if (!settings.IncludeInvisibleEntities && entity.IsInvisible)
        {
            return false;
        }

        string layerName = GetLayerName(entity);
        if (!IsLayerIncluded(layerName, settings))
        {
            return false;
        }

        return entity is Line || entity is LwPolyline || entity is Polyline2D;
    }

    private static bool IsLayerIncluded(string layerName, CadWallImportSettings settings)
    {
        if (ContainsLayer(settings.ExcludedLayers, layerName))
        {
            return false;
        }

        if (settings.IncludedLayers == null || settings.IncludedLayers.Length == 0)
        {
            return string.IsNullOrWhiteSpace(settings.TargetLayerKeyword) ||
                   (!string.IsNullOrWhiteSpace(layerName) &&
                    layerName.IndexOf(settings.TargetLayerKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        return ContainsLayer(settings.IncludedLayers, layerName);
    }

    private static bool ContainsLayer(string[] layers, string layerName)
    {
        if (layers == null || string.IsNullOrWhiteSpace(layerName))
        {
            return false;
        }

        for (int i = 0; i < layers.Length; i++)
        {
            if (string.Equals(layers[i], layerName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void ExtractLwPolylineSegments(
        LwPolyline polyline,
        CadWallImportSettings settings,
        HashSet<string> uniqueSegmentKeys,
        List<string> warnings,
        List<CadWallSegment> results)
    {
        if (polyline == null || polyline.Vertices == null || polyline.Vertices.Count < 2)
        {
            return;
        }

        string layerName = GetLayerName(polyline);
        for (int i = 0; i < polyline.Vertices.Count; i++)
        {
            LwPolyline.Vertex current = polyline.Vertices[i];
            LwPolyline.Vertex next = i + 1 < polyline.Vertices.Count
                ? polyline.Vertices[i + 1]
                : (polyline.IsClosed ? polyline.Vertices[0] : null);

            if (next == null)
            {
                break;
            }

            if (!Mathf.Approximately((float)current.Bulge, 0f))
            {
                warnings.Add($"Skipped bulged LwPolyline segment on layer '{layerName}'.");
                continue;
            }

            AddSegment(current.Location.X, current.Location.Y, next.Location.X, next.Location.Y, layerName, nameof(LwPolyline), settings, uniqueSegmentKeys, results);
        }
    }

    private static void ExtractPolyline2DSegments(
        Polyline2D polyline,
        CadWallImportSettings settings,
        HashSet<string> uniqueSegmentKeys,
        List<string> warnings,
        List<CadWallSegment> results)
    {
        if (polyline == null || polyline.Vertices == null || polyline.Vertices.Count < 2)
        {
            return;
        }

        string layerName = GetLayerName(polyline);
        for (int i = 0; i < polyline.Vertices.Count; i++)
        {
            Vertex current = polyline.Vertices[i];
            Vertex next = i + 1 < polyline.Vertices.Count
                ? polyline.Vertices[i + 1]
                : (polyline.IsClosed ? polyline.Vertices[0] : null);

            if (next == null)
            {
                break;
            }

            if (!Mathf.Approximately((float)current.Bulge, 0f))
            {
                warnings.Add($"Skipped bulged Polyline2D segment on layer '{layerName}'.");
                continue;
            }

            AddSegment(current.Location.X, current.Location.Y, next.Location.X, next.Location.Y, layerName, nameof(Polyline2D), settings, uniqueSegmentKeys, results);
        }
    }

    private static void AddSegment(
        double startX,
        double startY,
        double endX,
        double endY,
        string layerName,
        string sourceType,
        CadWallImportSettings settings,
        HashSet<string> uniqueSegmentKeys,
        List<CadWallSegment> results)
    {
        Vector3 start = ConvertCadPoint(startX, startY, settings);
        Vector3 end = ConvertCadPoint(endX, endY, settings);

        if ((end - start).sqrMagnitude < settings.MinimumWallLength * settings.MinimumWallLength)
        {
            return;
        }

        if (settings.DeduplicateSegments)
        {
            string key = BuildSegmentKey(start, end, settings.DeduplicateTolerance);
            if (!uniqueSegmentKeys.Add(key))
            {
                return;
            }
        }

        results.Add(new CadWallSegment(start, end, layerName, sourceType));
    }

    private static Vector3 ConvertCadPoint(double x, double y, CadWallImportSettings settings)
    {
        float worldX = (float)x * settings.CadUnitToWorldScale;
        float worldZ = (float)y * settings.CadUnitToWorldScale * (settings.InvertCadY ? -1f : 1f);
        return new Vector3(worldX, settings.DrawingPlaneY, worldZ) + settings.ImportOffset;
    }

    private static string BuildSegmentKey(Vector3 start, Vector3 end, float deduplicateTolerance)
    {
        if (ComparePoints(start, end) > 0)
        {
            Vector3 temp = start;
            start = end;
            end = temp;
        }

        return $"{Quantize(start.x, deduplicateTolerance)}|{Quantize(start.z, deduplicateTolerance)}|{Quantize(end.x, deduplicateTolerance)}|{Quantize(end.z, deduplicateTolerance)}";
    }

    private static int ComparePoints(Vector3 left, Vector3 right)
    {
        int xCompare = left.x.CompareTo(right.x);
        if (xCompare != 0)
        {
            return xCompare;
        }

        return left.z.CompareTo(right.z);
    }

    private static long Quantize(float value, float deduplicateTolerance)
    {
        return Convert.ToInt64(Math.Round(value / deduplicateTolerance, MidpointRounding.AwayFromZero));
    }

    private static string GetLayerName(Entity entity)
    {
        return entity?.Layer != null ? entity.Layer.Name : string.Empty;
    }
}
