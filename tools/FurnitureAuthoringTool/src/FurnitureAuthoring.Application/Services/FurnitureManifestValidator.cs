using System.Collections.Generic;
using FurnitureAuthoring.Contracts.Models;

namespace FurnitureAuthoring.Application.Services;

public sealed class FurnitureManifestValidator
{
    public IReadOnlyList<string> Validate(FurnitureManifestDto manifest)
    {
        List<string> errors = new();
        HashSet<string> codes = new(System.StringComparer.OrdinalIgnoreCase);

        if (manifest.ManifestVersion <= 0)
        {
            errors.Add("ManifestVersion must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(manifest.CatalogVersion))
        {
            errors.Add("CatalogVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Author))
        {
            errors.Add("Author is required.");
        }

        for (int i = 0; i < manifest.Items.Count; i++)
        {
            FurnitureItemDto item = manifest.Items[i];
            if (string.IsNullOrWhiteSpace(item.Code))
            {
                errors.Add($"Item[{i}] code is required.");
            }
            else if (!codes.Add(item.Code))
            {
                errors.Add($"Duplicate furniture code: {item.Code}");
            }

            if (string.IsNullOrWhiteSpace(item.DisplayName))
            {
                errors.Add($"Item[{i}] displayName is required.");
            }

            if (string.IsNullOrWhiteSpace(item.ExportCode))
            {
                errors.Add($"Item[{i}] exportCode is required.");
            }

            if (string.IsNullOrWhiteSpace(item.PrefabSourcePath))
            {
                errors.Add($"Item[{i}] prefabSourcePath is required.");
            }

            for (int defectIndex = 0; defectIndex < item.Defects.Count; defectIndex++)
            {
                FurnitureDefectDto defect = item.Defects[defectIndex];
                if (string.IsNullOrWhiteSpace(defect.MntnCd) ||
                    string.IsNullOrWhiteSpace(defect.LocCd) ||
                    string.IsNullOrWhiteSpace(defect.MtrlCd))
                {
                    errors.Add($"Item[{i}] defect[{defectIndex}] has an empty code.");
                }
            }
        }

        return errors;
    }
}
