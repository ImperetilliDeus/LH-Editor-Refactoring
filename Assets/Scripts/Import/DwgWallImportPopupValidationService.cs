using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

internal sealed class DwgWallImportPopupValidationService
{
    public bool TryApplyCadScale(
        InputField popupCadScaleInputField,
        DwgWallImporter importer,
        out float parsedScale)
    {
        parsedScale = 0f;
        if (popupCadScaleInputField == null)
        {
            return true;
        }

        string scaleText = popupCadScaleInputField.text?.Trim();
        if (!float.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedScale) ||
            parsedScale <= 0f)
        {
            Debug.LogWarning($"[{nameof(DwgWallImporter)}] Invalid CAD unit scale: '{scaleText}'.", importer);
            popupCadScaleInputField.ActivateInputField();
            return false;
        }

        return true;
    }

    public bool ValidateLayerSelection(
        DwgWallImportPopupController popupController,
        int visibleLayerToggleCount,
        DwgWallImporter importer)
    {
        if (popupController.AvailableLayerCount == 0 || visibleLayerToggleCount == 0)
        {
            Debug.LogWarning($"[{nameof(DwgWallImporter)}] No visible layer entries are available in the popup. Import was cancelled.", importer);
            return false;
        }

        if (!popupController.HasAnyPopupLayerSelected())
        {
            Debug.LogWarning($"[{nameof(DwgWallImporter)}] Select at least one layer before importing.", importer);
            return false;
        }

        return true;
    }
}
