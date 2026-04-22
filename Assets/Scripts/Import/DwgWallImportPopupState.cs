internal sealed class DwgWallImportPopupState
{
    public string PendingImportPath { get; set; } = string.Empty;

    public bool OwnsRuntimeImportSettingsPopup { get; set; }

    public void Reset()
    {
        PendingImportPath = string.Empty;
        OwnsRuntimeImportSettingsPopup = false;
    }
}
