using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FurnitureAuthoring.Application.Abstractions;
using FurnitureAuthoring.Contracts.Models;
using FurnitureAuthoring.Tool.Models;

namespace FurnitureAuthoring.Tool.Services;

public sealed class FurniturePatchBuildWorker
{
    private const string ManifestFileName = "manifest.json";
    private const string CatalogFileName = "patch-catalog.json";
    private const string BuildReportFileName = "build-report.txt";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IFurnitureManifestStore manifestStore;

    public FurniturePatchBuildWorker(IFurnitureManifestStore manifestStore)
    {
        this.manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
    }

    public async Task<PatchBuildResult> BuildAsync(
        string outputRoot,
        FurnitureManifestDto manifest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            throw new InvalidOperationException("출력 폴더가 비어 있습니다.");
        }

        string outputDirectory = Path.Combine(
            outputRoot,
            $"{SanitizeFileName(manifest.CatalogVersion)}_{DateTime.Now:yyyyMMdd-HHmmss}");
        string prefabsDirectory = Path.Combine(outputDirectory, "prefabs");
        string thumbnailsDirectory = Path.Combine(outputDirectory, "thumbnails");

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(prefabsDirectory);
        Directory.CreateDirectory(thumbnailsDirectory);

        Collection<string> warnings = new();
        FurniturePatchCatalogDto catalog = CreateCatalog(manifest);
        int copiedPrefabCount = 0;
        int copiedThumbnailCount = 0;

        foreach (FurnitureItemDto item in manifest.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string prefabTargetRelativePath = CopyRequiredFile(item.PrefabSourcePath, prefabsDirectory, item.Code, "프리팹");
            copiedPrefabCount++;

            string thumbnailTargetRelativePath = string.Empty;
            if (!string.IsNullOrWhiteSpace(item.ThumbnailSourcePath))
            {
                thumbnailTargetRelativePath = CopyOptionalFile(item.ThumbnailSourcePath, thumbnailsDirectory, item.Code, "썸네일");
                copiedThumbnailCount++;
            }
            else
            {
                warnings.Add($"[{item.Code}] 썸네일 경로가 비어 있어 썸네일 복사를 건너뛰었습니다.");
            }

            catalog.Items.Add(new FurniturePatchCatalogItemDto
            {
                Code = item.Code,
                DisplayName = item.DisplayName,
                ExportCode = item.ExportCode,
                NativeCode = item.NativeCode,
                PrefabFile = prefabTargetRelativePath,
                ThumbnailFile = thumbnailTargetRelativePath,
                PlacementOffset = CloneVector(item.PlacementOffset),
                DefaultEulerAngles = CloneVector(item.DefaultEulerAngles),
                BoundsSize = CloneVector(item.BoundsSize),
                Defects = CloneDefects(item.Defects)
            });
        }

        string manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        string catalogPath = Path.Combine(outputDirectory, CatalogFileName);
        string buildReportPath = Path.Combine(outputDirectory, BuildReportFileName);

        await manifestStore.SaveAsync(manifestPath, manifest, cancellationToken);
        await SaveCatalogAsync(catalogPath, catalog, cancellationToken);
        await File.WriteAllTextAsync(
            buildReportPath,
            BuildReport(outputDirectory, manifestPath, catalogPath, manifest.Items.Count, copiedPrefabCount, copiedThumbnailCount, warnings),
            Encoding.UTF8,
            cancellationToken);

        return new PatchBuildResult
        {
            OutputDirectory = outputDirectory,
            ManifestPath = manifestPath,
            CatalogPath = catalogPath,
            BuildReportPath = buildReportPath,
            ItemCount = manifest.Items.Count,
            CopiedPrefabCount = copiedPrefabCount,
            CopiedThumbnailCount = copiedThumbnailCount,
            Warnings = warnings
        };
    }

    private static FurniturePatchCatalogDto CreateCatalog(FurnitureManifestDto manifest)
    {
        return new FurniturePatchCatalogDto
        {
            ManifestVersion = manifest.ManifestVersion,
            CatalogVersion = manifest.CatalogVersion,
            CreatedAt = manifest.CreatedAt,
            BuiltAt = DateTimeOffset.Now,
            Author = manifest.Author,
            ManifestFile = ManifestFileName
        };
    }

    private static string CopyRequiredFile(string sourcePath, string destinationDirectory, string code, string assetLabel)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new InvalidOperationException($"[{code}] {assetLabel} 경로가 비어 있습니다.");
        }

        return CopyOptionalFile(sourcePath, destinationDirectory, code, assetLabel);
    }

    private static string CopyOptionalFile(string sourcePath, string destinationDirectory, string code, string assetLabel)
    {
        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(fullSourcePath))
        {
            throw new FileNotFoundException($"[{code}] {assetLabel} 파일을 찾지 못했습니다.", fullSourcePath);
        }

        string extension = Path.GetExtension(fullSourcePath);
        string fileName = $"{SanitizeFileName(code)}{extension}";
        string destinationPath = Path.Combine(destinationDirectory, fileName);
        File.Copy(fullSourcePath, destinationPath, overwrite: true);
        string folderName = Path.GetFileName(destinationDirectory);
        return $"{folderName}/{fileName}".Replace('\\', '/');
    }

    private static async Task SaveCatalogAsync(string path, FurniturePatchCatalogDto catalog, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, catalog, SerializerOptions, cancellationToken);
    }

    private static string BuildReport(
        string outputDirectory,
        string manifestPath,
        string catalogPath,
        int itemCount,
        int copiedPrefabCount,
        int copiedThumbnailCount,
        IReadOnlyList<string> warnings)
    {
        StringBuilder builder = new();
        builder.AppendLine("Furniture Patch Build Report");
        builder.AppendLine($"OutputDirectory: {outputDirectory}");
        builder.AppendLine($"ManifestPath: {manifestPath}");
        builder.AppendLine($"CatalogPath: {catalogPath}");
        builder.AppendLine($"ItemCount: {itemCount}");
        builder.AppendLine($"CopiedPrefabs: {copiedPrefabCount}");
        builder.AppendLine($"CopiedThumbnails: {copiedThumbnailCount}");

        if (warnings.Count > 0)
        {
            builder.AppendLine("Warnings:");
            foreach (string warning in warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString();
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "catalog";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            builder.Append(Array.IndexOf(invalidChars, character) >= 0 ? '_' : character);
        }

        return builder.ToString().Trim();
    }

    private static Vector3Value CloneVector(Vector3Value source)
    {
        return new Vector3Value
        {
            X = source.X,
            Y = source.Y,
            Z = source.Z
        };
    }

    private static ObservableCollection<FurnitureDefectDto> CloneDefects(ObservableCollection<FurnitureDefectDto> defects)
    {
        ObservableCollection<FurnitureDefectDto> cloned = new();
        foreach (FurnitureDefectDto defect in defects)
        {
            cloned.Add(new FurnitureDefectDto
            {
                MntnCd = defect.MntnCd,
                LocCd = defect.LocCd,
                MtrlCd = defect.MtrlCd
            });
        }

        return cloned;
    }
}
