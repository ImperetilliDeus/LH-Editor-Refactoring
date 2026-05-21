using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FurnitureAuthoring.Application.Services;
using FurnitureAuthoring.Contracts.Models;
using FurnitureAuthoring.Infrastructure.Persistence;
using FurnitureAuthoring.Tool.Models;
using FurnitureAuthoring.Tool.Services;
using FurnitureAuthoring.Tool.ViewModels;
using Microsoft.Win32;

namespace FurnitureAuthoring.Tool;

public partial class MainWindow : Window
{
    private const string UnsavedFilePathLabel = "저장되지 않음";

    private readonly JsonFurnitureManifestStore manifestStore = new();
    private readonly FurnitureManifestValidator validator = new();
    private readonly FurniturePatchBuildWorker patchBuildWorker;
    private readonly ToolSettingsStore toolSettingsStore = new();
    private readonly UnityBatchModeRunner unityBatchModeRunner = new();
    private readonly WindowViewModel viewModel = new();

    public MainWindow()
    {
        patchBuildWorker = new FurniturePatchBuildWorker(manifestStore);
        InitializeComponent();
        DataContext = viewModel;
        LoadToolSettings();
        ResetToNewManifest();
    }

    private async void LoadSample_Click(object sender, RoutedEventArgs e)
    {
        string samplePath = Path.Combine(AppContext.BaseDirectory, "samples", "furniture-manifest.sample.json");
        if (!File.Exists(samplePath))
        {
            UpdateStatus("샘플 매니페스트 파일을 찾지 못했습니다.");
            MessageBox.Show(this, $"샘플 파일을 찾지 못했습니다.\n{samplePath}", "샘플 불러오기", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await LoadManifestAsync(samplePath);
    }

    private async void OpenManifest_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "매니페스트 JSON (*.json)|*.json|모든 파일 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            await LoadManifestAsync(dialog.FileName);
        }
    }

    private async void SaveManifest_Click(object sender, RoutedEventArgs e)
    {
        if (string.Equals(viewModel.CurrentFilePath, UnsavedFilePathLabel, StringComparison.OrdinalIgnoreCase))
        {
            await SaveManifestAsAsync();
            return;
        }

        await SaveManifestCoreAsync(viewModel.CurrentFilePath);
    }

    private async void SaveManifestAs_Click(object sender, RoutedEventArgs e)
    {
        await SaveManifestAsAsync();
    }

    private void ValidateManifest_Click(object sender, RoutedEventArgs e)
    {
        NormalizeManifest();
        string[] errors = validator.Validate(viewModel.CurrentManifest).ToArray();
        if (errors.Length == 0)
        {
            UpdateStatus("검증이 완료되었습니다.");
            MessageBox.Show(this, "매니페스트 검증이 완료되었습니다.", "검증", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string message = string.Join(Environment.NewLine, errors);
        UpdateStatus($"검증 실패: {errors.Length}건의 문제가 있습니다.");
        MessageBox.Show(this, message, "검증", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async void PatchBuild_Click(object sender, RoutedEventArgs e)
    {
        NormalizeManifest();
        string[] errors = validator.Validate(viewModel.CurrentManifest).ToArray();
        if (errors.Length > 0)
        {
            string message = string.Join(Environment.NewLine, errors);
            UpdateStatus("검증 오류로 인해 패치 빌드가 중단되었습니다.");
            MessageBox.Show(this, message, "패치 빌드", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string outputRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FurnitureAuthoringTool", "BuildOutput");
        Directory.CreateDirectory(outputRoot);

        try
        {
            ToolSettings toolSettings = CollectToolSettings();
            await toolSettingsStore.SaveAsync(toolSettings);

            PatchBuildResult result = await patchBuildWorker.BuildAsync(outputRoot, viewModel.CurrentManifest);

            UpdateStatus("Unity batchmode 빌드를 실행 중입니다...");
            UnityBatchBuildResult unityResult = await unityBatchModeRunner.RunAsync(toolSettings, result);
            if (unityResult.ExitCode != 0)
            {
                string failedMessage =
                    $"Unity batchmode 실행이 실패했습니다.\n" +
                    $"종료 코드: {unityResult.ExitCode}\n" +
                    $"로그: {unityResult.LogFilePath}";
                UpdateStatus("Unity batchmode 빌드가 실패했습니다.");
                MessageBox.Show(this, failedMessage, "패치 빌드", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string summary =
                $"출력 폴더: {result.OutputDirectory}\n" +
                $"매니페스트: {result.ManifestPath}\n" +
                $"패치 카탈로그: {result.CatalogPath}\n" +
                $"프리팹 복사: {result.CopiedPrefabCount}건\n" +
                $"썸네일 복사: {result.CopiedThumbnailCount}건\n" +
                $"Unity 카탈로그 에셋: {unityResult.CatalogAssetPath}\n" +
                $"Unity 로그: {unityResult.LogFilePath}";

            if (result.Warnings.Count > 0)
            {
                summary += $"\n경고: {result.Warnings.Count}건\n{string.Join('\n', result.Warnings)}";
            }

            UpdateStatus($"패치 빌드를 완료했습니다: {result.OutputDirectory}");
            MessageBox.Show(this, summary, "패치 빌드", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            UpdateStatus("패치 빌드 중 오류가 발생했습니다.");
            MessageBox.Show(this, exception.Message, "패치 빌드", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NewManifest_Click(object sender, RoutedEventArgs e)
    {
        ResetToNewManifest();
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        FurnitureItemDto newItem = CreateNewItem();
        viewModel.Items.Add(newItem);
        viewModel.SelectedItem = newItem;
        UpdateStatus($"가구 항목을 추가했습니다: {newItem.Code}");
    }

    private void CloneItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedItem == null)
        {
            return;
        }

        FurnitureItemDto clone = CloneItem(viewModel.SelectedItem);
        clone.Code = GenerateNextCode(clone.Code);
        clone.DisplayName = $"{clone.DisplayName} 복사본";
        viewModel.Items.Add(clone);
        viewModel.SelectedItem = clone;
        UpdateStatus($"가구 항목을 복제했습니다: {clone.Code}");
    }

    private void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedItem == null)
        {
            return;
        }

        string removedCode = viewModel.SelectedItem.Code;
        int currentIndex = viewModel.Items.IndexOf(viewModel.SelectedItem);
        viewModel.Items.Remove(viewModel.SelectedItem);
        viewModel.SelectedItem = viewModel.Items.Count == 0
            ? null
            : viewModel.Items[Math.Max(0, Math.Min(currentIndex, viewModel.Items.Count - 1))];
        viewModel.SelectedDefect = null;
        UpdateStatus($"가구 항목을 삭제했습니다: {removedCode}");
    }

    private void AddDefect_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedItem == null)
        {
            return;
        }

        FurnitureDefectDto defect = new()
        {
            MntnCd = "901",
            LocCd = "2",
            MtrlCd = string.Empty
        };

        viewModel.SelectedItem.Defects.Add(defect);
        viewModel.SelectedDefect = defect;
        UpdateStatus($"Defect 행을 추가했습니다: {viewModel.SelectedItem.Code}");
    }

    private void DeleteDefect_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedItem == null || viewModel.SelectedDefect == null)
        {
            return;
        }

        viewModel.SelectedItem.Defects.Remove(viewModel.SelectedDefect);
        viewModel.SelectedDefect = null;
        UpdateStatus($"Defect 행을 삭제했습니다: {viewModel.SelectedItem.Code}");
    }

    private void BrowsePrefab_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedItem == null)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Filter = "Unity 프리팹 (*.prefab)|*.prefab|모든 파일 (*.*)|*.*",
            CheckFileExists = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            viewModel.SelectedItem.PrefabSourcePath = dialog.FileName;
            UpdateStatus($"프리팹 경로를 선택했습니다: {viewModel.SelectedItem.Code}");
        }
    }

    private void BrowseThumbnail_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.SelectedItem == null)
        {
            return;
        }

        OpenFileDialog dialog = new()
        {
            Filter = "이미지 파일 (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|모든 파일 (*.*)|*.*",
            CheckFileExists = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            viewModel.SelectedItem.ThumbnailSourcePath = dialog.FileName;
            UpdateStatus($"썸네일 경로를 선택했습니다: {viewModel.SelectedItem.Code}");
        }
    }

    private void BrowseUnityEditor_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "Unity Editor 실행 파일 (Unity.exe)|Unity.exe|실행 파일 (*.exe)|*.exe|모든 파일 (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            viewModel.UnityEditorPath = dialog.FileName;
            UpdateStatus("Unity Editor 실행 파일 경로를 설정했습니다.");
        }
    }

    private async Task SaveManifestAsAsync()
    {
        SaveFileDialog dialog = new()
        {
            Filter = "매니페스트 JSON (*.json)|*.json|모든 파일 (*.*)|*.*",
            FileName = "furniture-manifest.json"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await SaveManifestCoreAsync(dialog.FileName);
        }
    }

    private async Task SaveManifestCoreAsync(string path)
    {
        NormalizeManifest();
        await manifestStore.SaveAsync(path, viewModel.CurrentManifest);
        viewModel.CurrentFilePath = path;
        UpdateStatus($"매니페스트를 저장했습니다: {path}");
    }

    private async Task LoadManifestAsync(string path)
    {
        try
        {
            FurnitureManifestDto manifest = await manifestStore.LoadAsync(path);
            if (manifest.Items.Count == 0)
            {
                manifest.Items.Add(CreateNewItem());
            }

            viewModel.ReplaceManifest(manifest, path, $"매니페스트를 불러왔습니다: {path}");
        }
        catch (Exception exception)
        {
            UpdateStatus("매니페스트를 불러오지 못했습니다.");
            MessageBox.Show(this, exception.Message, "열기", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ResetToNewManifest()
    {
        FurnitureManifestDto manifest = new()
        {
            ManifestVersion = 1,
            CatalogVersion = DateTime.Now.ToString("yyyy.MM.dd.01"),
            CreatedAt = DateTimeOffset.Now,
            Author = Environment.UserName
        };

        manifest.Items.Add(CreateNewItem());
        viewModel.ReplaceManifest(manifest, UnsavedFilePathLabel, "새 매니페스트를 만들었습니다.");
    }

    private void LoadToolSettings()
    {
        try
        {
            ToolSettings toolSettings = toolSettingsStore.LoadAsync().GetAwaiter().GetResult();
            viewModel.UnityEditorPath = toolSettings.UnityEditorPath;
            viewModel.UnityProjectPath = string.IsNullOrWhiteSpace(toolSettings.UnityProjectPath)
                ? DetectUnityProjectPath()
                : toolSettings.UnityProjectPath;
        }
        catch
        {
            viewModel.UnityProjectPath = DetectUnityProjectPath();
        }
    }

    private static FurnitureItemDto CreateNewItem()
    {
        return new FurnitureItemDto
        {
            Code = "NEW001",
            DisplayName = "새 가구",
            ExportCode = "NEW001",
            NativeCode = string.Empty,
            PrefabSourcePath = string.Empty,
            ThumbnailSourcePath = string.Empty,
            PlacementOffset = new Vector3Value(),
            DefaultEulerAngles = new Vector3Value(),
            BoundsSize = new Vector3Value { X = 1, Y = 1, Z = 1 }
        };
    }

    private static FurnitureItemDto CloneItem(FurnitureItemDto item)
    {
        FurnitureItemDto clone = new()
        {
            Code = item.Code,
            DisplayName = item.DisplayName,
            ExportCode = item.ExportCode,
            NativeCode = item.NativeCode,
            PrefabSourcePath = item.PrefabSourcePath,
            ThumbnailSourcePath = item.ThumbnailSourcePath,
            PlacementOffset = CloneVector(item.PlacementOffset),
            DefaultEulerAngles = CloneVector(item.DefaultEulerAngles),
            BoundsSize = CloneVector(item.BoundsSize)
        };

        foreach (FurnitureDefectDto defect in item.Defects)
        {
            clone.Defects.Add(new FurnitureDefectDto
            {
                MntnCd = defect.MntnCd,
                LocCd = defect.LocCd,
                MtrlCd = defect.MtrlCd
            });
        }

        return clone;
    }

    private static Vector3Value CloneVector(Vector3Value value)
    {
        return new Vector3Value
        {
            X = value.X,
            Y = value.Y,
            Z = value.Z
        };
    }

    private string GenerateNextCode(string baseCode)
    {
        string seed = string.IsNullOrWhiteSpace(baseCode) ? "ITEM" : baseCode;
        int suffix = 2;
        string candidate = $"{seed}_{suffix:D2}";

        while (viewModel.Items.Any(item => string.Equals(item.Code, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            suffix++;
            candidate = $"{seed}_{suffix:D2}";
        }

        return candidate;
    }

    private void NormalizeManifest()
    {
        viewModel.CurrentManifest.CreatedAt = DateTimeOffset.Now;
        foreach (FurnitureItemDto item in viewModel.Items)
        {
            item.Code = item.Code.Trim();
            item.DisplayName = item.DisplayName.Trim();
            item.ExportCode = string.IsNullOrWhiteSpace(item.ExportCode) ? item.Code : item.ExportCode.Trim();
            item.NativeCode = item.NativeCode?.Trim() ?? string.Empty;
            item.PrefabSourcePath = item.PrefabSourcePath?.Trim() ?? string.Empty;
            item.ThumbnailSourcePath = item.ThumbnailSourcePath?.Trim() ?? string.Empty;

            foreach (FurnitureDefectDto defect in item.Defects)
            {
                defect.MntnCd = defect.MntnCd?.Trim() ?? string.Empty;
                defect.LocCd = defect.LocCd?.Trim() ?? string.Empty;
                defect.MtrlCd = defect.MtrlCd?.Trim() ?? string.Empty;
            }
        }
    }

    private ToolSettings CollectToolSettings()
    {
        string unityEditorPath = viewModel.UnityEditorPath?.Trim() ?? string.Empty;
        string unityProjectPath = viewModel.UnityProjectPath?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(unityEditorPath))
        {
            throw new InvalidOperationException("Unity Editor 실행 파일 경로를 먼저 설정해 주세요.");
        }

        if (string.IsNullOrWhiteSpace(unityProjectPath))
        {
            throw new InvalidOperationException("Unity 프로젝트 경로를 확인해 주세요.");
        }

        return new ToolSettings
        {
            UnityEditorPath = unityEditorPath,
            UnityProjectPath = unityProjectPath
        };
    }

    private static string DetectUnityProjectPath()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "ProjectSettings", "ProjectVersion.txt");
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return string.Empty;
    }

    private void UpdateStatus(string message)
    {
        viewModel.StatusMessage = message;
    }
}
