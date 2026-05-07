using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurnitureAuthoring.Contracts.Models;

namespace FurnitureAuthoring.Tool.Services;

public sealed class JsonFurnitureManifestStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<FurnitureManifestDto> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using FileStream stream = File.OpenRead(path);
        FurnitureManifestDto? manifest = await JsonSerializer.DeserializeAsync<FurnitureManifestDto>(
            stream,
            SerializerOptions,
            cancellationToken);

        return manifest ?? new FurnitureManifestDto();
    }

    public async Task SaveAsync(string path, FurnitureManifestDto manifest, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, manifest, SerializerOptions, cancellationToken);
    }
}
