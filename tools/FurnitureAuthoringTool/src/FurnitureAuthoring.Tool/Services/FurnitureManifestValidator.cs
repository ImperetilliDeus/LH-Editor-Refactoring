using System.Collections.Generic;
using FurnitureAuthoring.Contracts.Models;

namespace FurnitureAuthoring.Tool.Services;

public sealed class FurnitureManifestValidator
{
    public IReadOnlyList<string> Validate(FurnitureManifestDto manifest)
    {
        List<string> errors = new();
        HashSet<string> codes = new(System.StringComparer.OrdinalIgnoreCase);

        if (manifest.ManifestVersion <= 0)
        {
            errors.Add("매니페스트 버전은 0보다 커야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(manifest.CatalogVersion))
        {
            errors.Add("카탈로그 버전은 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Author))
        {
            errors.Add("작성자는 필수입니다.");
        }

        for (int i = 0; i < manifest.Items.Count; i++)
        {
            FurnitureItemDto item = manifest.Items[i];
            if (string.IsNullOrWhiteSpace(item.Code))
            {
                errors.Add($"항목[{i}]의 code는 필수입니다.");
            }
            else if (!codes.Add(item.Code))
            {
                errors.Add($"중복된 가구 코드입니다: {item.Code}");
            }

            if (string.IsNullOrWhiteSpace(item.DisplayName))
            {
                errors.Add($"항목[{i}]의 displayName은 필수입니다.");
            }

            if (string.IsNullOrWhiteSpace(item.ExportCode))
            {
                errors.Add($"항목[{i}]의 exportCode는 필수입니다.");
            }

            if (string.IsNullOrWhiteSpace(item.PrefabSourcePath))
            {
                errors.Add($"항목[{i}]의 prefabSourcePath는 필수입니다.");
            }

            for (int defectIndex = 0; defectIndex < item.Defects.Count; defectIndex++)
            {
                FurnitureDefectDto defect = item.Defects[defectIndex];
                if (string.IsNullOrWhiteSpace(defect.MntnCd) ||
                    string.IsNullOrWhiteSpace(defect.LocCd) ||
                    string.IsNullOrWhiteSpace(defect.MtrlCd))
                {
                    errors.Add($"항목[{i}]의 defect[{defectIndex}]에 빈 코드가 있습니다.");
                }
            }
        }

        return errors;
    }
}
