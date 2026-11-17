using NUnit.Framework;

public class NativeFileDialogTests
{
    [Test]
    public void BuildFilterString_JoinsNamedFiltersAndMapsWildcard()
    {
        string filter = NativeFileDialog.BuildFilterString(new[]
        {
            new FileDialogFilter("CAD Files", "dwg", "dxf"),
            new FileDialogFilter("DWG Files", ".dwg"),
            new FileDialogFilter("All Files", "*"),
        });

        Assert.That(filter, Is.EqualTo("CAD Files (*.dwg;*.dxf)|*.dwg;*.dxf|DWG Files (*.dwg)|*.dwg|All Files (*.*)|*.*"));
    }

    [Test]
    public void BuildFilterString_FallsBackToAllFilesWhenEmpty()
    {
        Assert.That(NativeFileDialog.BuildFilterString(null), Is.EqualTo("All Files (*.*)|*.*"));
        Assert.That(NativeFileDialog.BuildFilterString(new FileDialogFilter[0]), Is.EqualTo("All Files (*.*)|*.*"));
    }

    [Test]
    public void EscapePowerShellSingleQuoted_DoublesQuotesAndKeepsNonAscii()
    {
        Assert.That(NativeFileDialog.EscapePowerShellSingleQuoted(@"C:\도면\it's.dwg"), Is.EqualTo(@"C:\도면\it''s.dwg"));
        Assert.That(NativeFileDialog.EscapePowerShellSingleQuoted(null), Is.Empty);
    }

    [Test]
    public void BuildPowerShellScript_SaveDialogIncludesDirectoryFileNameAndOverwritePrompt()
    {
        string script = NativeFileDialog.BuildPowerShellScript(
            true,
            "Export LH Scene JSON",
            @"C:\작업's",
            "lh_scene.json",
            new[] { new FileDialogFilter("JSON Files", "json") });

        Assert.That(script, Does.Contain("New-Object System.Windows.Forms.SaveFileDialog;"));
        Assert.That(script, Does.Contain("$dialog.Filter = 'JSON Files (*.json)|*.json';"));
        Assert.That(script, Does.Contain("$dialog.Title = 'Export LH Scene JSON';"));
        Assert.That(script, Does.Contain(@"$dialog.InitialDirectory = 'C:\작업''s';"));
        Assert.That(script, Does.Contain("$dialog.FileName = 'lh_scene.json';"));
        Assert.That(script, Does.Contain("$dialog.OverwritePrompt = $true;"));
        Assert.That(script, Does.Not.Contain("Multiselect"));
        Assert.That(script, Does.Contain("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8"));
    }

    [Test]
    public void BuildPowerShellScript_OpenDialogDisablesMultiselectAndSkipsEmptyPaths()
    {
        string script = NativeFileDialog.BuildPowerShellScript(
            false,
            "Select DWG/DXF File",
            string.Empty,
            null,
            new[] { new FileDialogFilter("CAD Files", "dwg", "dxf") });

        Assert.That(script, Does.Contain("New-Object System.Windows.Forms.OpenFileDialog;"));
        Assert.That(script, Does.Contain("$dialog.Multiselect = $false;"));
        Assert.That(script, Does.Not.Contain("InitialDirectory"));
        Assert.That(script, Does.Not.Contain("$dialog.FileName ="));
        Assert.That(script, Does.Not.Contain("OverwritePrompt"));
    }
}
