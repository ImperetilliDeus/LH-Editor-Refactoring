using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurnitureAuthoring.Tool.Models;

namespace FurnitureAuthoring.Tool.Services;

public sealed class ToolSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string settingsFilePath;

    public ToolSettingsStore()
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FurnitureAuthoringTool");
        settingsFilePath = Path.Combine(root, "tool-settings.json");
    }

    public async Task<ToolSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsFilePath))
        {
            return new ToolSettings();
        }

        await using FileStream stream = File.OpenRead(settingsFilePath);
        ToolSettings? settings = await JsonSerializer.DeserializeAsync<ToolSettings>(stream, SerializerOptions, cancellationToken);
        return settings ?? new ToolSettings();
    }

    public async Task SaveAsync(ToolSettings settings, CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(settingsFilePath);
        await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
    }
}
