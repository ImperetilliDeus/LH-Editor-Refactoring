using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FurnitureAuthoring.Contracts.Models;

public sealed class FurniturePatchCatalogDto : INotifyPropertyChanged
{
    private int manifestVersion = 1;
    private string catalogVersion = string.Empty;
    private DateTimeOffset createdAt = DateTimeOffset.Now;
    private DateTimeOffset builtAt = DateTimeOffset.Now;
    private string author = string.Empty;
    private string manifestFile = string.Empty;

    public int ManifestVersion
    {
        get => manifestVersion;
        set => SetField(ref manifestVersion, value);
    }

    public string CatalogVersion
    {
        get => catalogVersion;
        set => SetField(ref catalogVersion, value);
    }

    public DateTimeOffset CreatedAt
    {
        get => createdAt;
        set => SetField(ref createdAt, value);
    }

    public DateTimeOffset BuiltAt
    {
        get => builtAt;
        set => SetField(ref builtAt, value);
    }

    public string Author
    {
        get => author;
        set => SetField(ref author, value);
    }

    public string ManifestFile
    {
        get => manifestFile;
        set => SetField(ref manifestFile, value);
    }

    public ObservableCollection<FurniturePatchCatalogItemDto> Items { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
