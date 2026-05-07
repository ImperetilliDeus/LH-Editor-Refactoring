using System;
using System.Collections.Generic;

namespace FurnitureAuthoring.Tool.Models;

public sealed class PatchBuildResult
{
    public string OutputDirectory { get; init; } = string.Empty;

    public string ManifestPath { get; init; } = string.Empty;

    public string CatalogPath { get; init; } = string.Empty;

    public string BuildReportPath { get; init; } = string.Empty;

    public int ItemCount { get; init; }

    public int CopiedPrefabCount { get; init; }

    public int CopiedThumbnailCount { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
