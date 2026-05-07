using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FurnitureAuthoring.Contracts.Models;

namespace FurnitureAuthoring.Tool.ViewModels;

public sealed class WindowViewModel : INotifyPropertyChanged
{
    private FurnitureManifestDto currentManifest = new();
    private FurnitureItemDto? selectedItem;
    private FurnitureDefectDto? selectedDefect;
    private string currentFilePath = "저장되지 않음";
    private string statusMessage = "준비됨";
    private string unityEditorPath = string.Empty;
    private string unityProjectPath = string.Empty;

    public FurnitureManifestDto CurrentManifest
    {
        get => currentManifest;
        set
        {
            if (SetField(ref currentManifest, value))
            {
                OnPropertyChanged(nameof(Items));
            }
        }
    }

    public ObservableCollection<FurnitureItemDto> Items => CurrentManifest.Items;

    public FurnitureItemDto? SelectedItem
    {
        get => selectedItem;
        set => SetField(ref selectedItem, value);
    }

    public FurnitureDefectDto? SelectedDefect
    {
        get => selectedDefect;
        set => SetField(ref selectedDefect, value);
    }

    public string CurrentFilePath
    {
        get => currentFilePath;
        set => SetField(ref currentFilePath, value);
    }

    public string StatusMessage
    {
        get => statusMessage;
        set => SetField(ref statusMessage, value);
    }

    public string UnityEditorPath
    {
        get => unityEditorPath;
        set => SetField(ref unityEditorPath, value);
    }

    public string UnityProjectPath
    {
        get => unityProjectPath;
        set => SetField(ref unityProjectPath, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ReplaceManifest(FurnitureManifestDto manifest, string filePath, string status)
    {
        CurrentManifest = manifest;
        SelectedItem = manifest.Items.Count > 0 ? manifest.Items[0] : null;
        SelectedDefect = null;
        CurrentFilePath = filePath;
        StatusMessage = status;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
