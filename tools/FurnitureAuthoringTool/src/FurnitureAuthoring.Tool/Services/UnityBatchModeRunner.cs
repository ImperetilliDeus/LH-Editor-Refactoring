using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FurnitureAuthoring.Tool.Models;

namespace FurnitureAuthoring.Tool.Services;

public sealed class UnityBatchModeRunner
{
    private const string ExecuteMethodName = "FurniturePatchBatchBuilder.RunFromCommandLine";

    public async Task<UnityBatchBuildResult> RunAsync(
        ToolSettings settings,
        PatchBuildResult patchBuildResult,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(patchBuildResult);

        if (string.IsNullOrWhiteSpace(settings.UnityEditorPath))
        {
            throw new InvalidOperationException("Unity Editor 실행 파일 경로가 비어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(settings.UnityProjectPath))
        {
            throw new InvalidOperationException("Unity 프로젝트 경로가 비어 있습니다.");
        }

        string unityEditorPath = Path.GetFullPath(settings.UnityEditorPath);
        string unityProjectPath = Path.GetFullPath(settings.UnityProjectPath);

        if (!File.Exists(unityEditorPath))
        {
            throw new FileNotFoundException("Unity Editor 실행 파일을 찾지 못했습니다.", unityEditorPath);
        }

        if (!Directory.Exists(unityProjectPath))
        {
            throw new DirectoryNotFoundException($"Unity 프로젝트 폴더를 찾지 못했습니다: {unityProjectPath}");
        }

        string importRootAssetPath = BuildImportRootAssetPath(patchBuildResult);
        string catalogAssetPath = $"{importRootAssetPath}/FurnitureCatalog.asset";
        string logFilePath = Path.Combine(patchBuildResult.OutputDirectory, "unity-batchmode.log");

        ProcessStartInfo startInfo = new()
        {
            FileName = unityEditorPath,
            Arguments = BuildArguments(unityProjectPath, patchBuildResult.OutputDirectory, importRootAssetPath, catalogAssetPath, logFilePath),
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = new()
        {
            StartInfo = startInfo
        };

        process.Start();
        await process.WaitForExitAsync(cancellationToken);

        return new UnityBatchBuildResult
        {
            ExitCode = process.ExitCode,
            LogFilePath = logFilePath,
            CatalogAssetPath = catalogAssetPath,
            ImportRootAssetPath = importRootAssetPath
        };
    }

    private static string BuildImportRootAssetPath(PatchBuildResult patchBuildResult)
    {
        string folderName = Path.GetFileName(patchBuildResult.OutputDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return $"Assets/Generated/FurniturePatches/{folderName}".Replace('\\', '/');
    }

    private static string BuildArguments(
        string unityProjectPath,
        string patchInputDirectory,
        string importRootAssetPath,
        string catalogAssetPath,
        string logFilePath)
    {
        return string.Join(
            " ",
            "-batchmode",
            "-quit",
            $"-projectPath {Quote(unityProjectPath)}",
            $"-executeMethod {ExecuteMethodName}",
            $"-logFile {Quote(logFilePath)}",
            $"--lh-patchInput {Quote(patchInputDirectory)}",
            $"--lh-importRoot {Quote(importRootAssetPath)}",
            $"--lh-catalogAsset {Quote(catalogAssetPath)}");
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }
}
