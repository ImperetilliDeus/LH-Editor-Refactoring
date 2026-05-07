using FurnitureAuthoring.Contracts.Models;

namespace FurnitureAuthoring.Application.Models;

public sealed class PatchBuildRequest
{
    public string OutputRoot { get; init; } = string.Empty;

    public FurnitureManifestDto Manifest { get; init; } = new();
}
