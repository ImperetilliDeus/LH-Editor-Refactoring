namespace FurnitureAuthoring.Tool.Models;

public sealed class UnityBatchBuildResult
{
    public int ExitCode { get; init; }

    public string LogFilePath { get; init; } = string.Empty;

    public string CatalogAssetPath { get; init; } = string.Empty;

    public string ImportRootAssetPath { get; init; } = string.Empty;
}
