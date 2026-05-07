using System.Collections.Generic;

namespace FurnitureAuthoring.Domain.Entities;

public sealed class FurnitureCatalogEntry
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ExportCode { get; set; } = string.Empty;
    public string NativeCode { get; set; } = string.Empty;
    public string PrefabSourcePath { get; set; } = string.Empty;
    public string ThumbnailSourcePath { get; set; } = string.Empty;
    public List<FurnitureDefect> Defects { get; set; } = new();
}
