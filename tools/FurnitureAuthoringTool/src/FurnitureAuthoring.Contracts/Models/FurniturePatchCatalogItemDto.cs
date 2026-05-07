using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FurnitureAuthoring.Contracts.Models;

public sealed class FurniturePatchCatalogItemDto : INotifyPropertyChanged
{
    private string code = string.Empty;
    private string displayName = string.Empty;
    private string exportCode = string.Empty;
    private string nativeCode = string.Empty;
    private string prefabFile = string.Empty;
    private string thumbnailFile = string.Empty;
    private Vector3Value placementOffset = new();
    private Vector3Value defaultEulerAngles = new();
    private Vector3Value boundsSize = new();

    public string Code
    {
        get => code;
        set => SetField(ref code, value);
    }

    public string DisplayName
    {
        get => displayName;
        set => SetField(ref displayName, value);
    }

    public string ExportCode
    {
        get => exportCode;
        set => SetField(ref exportCode, value);
    }

    public string NativeCode
    {
        get => nativeCode;
        set => SetField(ref nativeCode, value);
    }

    public string PrefabFile
    {
        get => prefabFile;
        set => SetField(ref prefabFile, value);
    }

    public string ThumbnailFile
    {
        get => thumbnailFile;
        set => SetField(ref thumbnailFile, value);
    }

    public Vector3Value PlacementOffset
    {
        get => placementOffset;
        set => SetField(ref placementOffset, value);
    }

    public Vector3Value DefaultEulerAngles
    {
        get => defaultEulerAngles;
        set => SetField(ref defaultEulerAngles, value);
    }

    public Vector3Value BoundsSize
    {
        get => boundsSize;
        set => SetField(ref boundsSize, value);
    }

    public ObservableCollection<FurnitureDefectDto> Defects { get; set; } = new();

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
