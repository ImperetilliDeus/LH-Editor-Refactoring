using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public readonly struct FileDialogFilter
{
    public FileDialogFilter(string name, params string[] extensions)
    {
        Name = name ?? string.Empty;
        Extensions = extensions ?? Array.Empty<string>();
    }

    public string Name { get; }

    // Extensions without the leading dot; "*" matches every file.
    public string[] Extensions { get; }
}

public static class NativeFileDialog
{
    // Editor builds use the Unity file panels; Windows players run a WinForms dialog through PowerShell.
    public static string OpenFile(string title, string initialDirectory, IReadOnlyList<FileDialogFilter> filters)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanelWithFilters(
            title,
            initialDirectory ?? string.Empty,
            BuildEditorFilters(filters));
#else
        return RunWindowsDialog(BuildPowerShellScript(false, title, initialDirectory, null, filters));
#endif
    }

    public static string SaveFile(
        string title,
        string initialDirectory,
        string defaultFileName,
        string defaultExtension,
        IReadOnlyList<FileDialogFilter> filters)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.SaveFilePanel(
            title,
            initialDirectory ?? string.Empty,
            defaultFileName ?? string.Empty,
            defaultExtension ?? string.Empty);
#else
        string fileName = string.IsNullOrEmpty(defaultExtension) || string.IsNullOrEmpty(defaultFileName) || defaultFileName.EndsWith("." + defaultExtension, StringComparison.OrdinalIgnoreCase)
            ? defaultFileName
            : defaultFileName + "." + defaultExtension;
        return RunWindowsDialog(BuildPowerShellScript(true, title, initialDirectory, fileName, filters));
#endif
    }

    public static string EscapePowerShellSingleQuoted(string value)
    {
        return (value ?? string.Empty).Replace("'", "''");
    }

    internal static string BuildFilterString(IReadOnlyList<FileDialogFilter> filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return "All Files (*.*)|*.*";
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < filters.Count; i++)
        {
            string patterns = BuildPatternList(filters[i].Extensions);
            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder.Append(filters[i].Name).Append(" (").Append(patterns).Append(")|").Append(patterns);
        }

        return builder.ToString();
    }

    internal static string BuildPowerShellScript(
        bool saveDialog,
        string title,
        string initialDirectory,
        string fileName,
        IReadOnlyList<FileDialogFilter> filters)
    {
        StringBuilder script = new StringBuilder();
        script.Append("Add-Type -AssemblyName System.Windows.Forms;");
        script.Append("$dialog = New-Object System.Windows.Forms.").Append(saveDialog ? "SaveFileDialog" : "OpenFileDialog").Append(';');
        script.Append("$dialog.Filter = '").Append(EscapePowerShellSingleQuoted(BuildFilterString(filters))).Append("';");
        script.Append("$dialog.Title = '").Append(EscapePowerShellSingleQuoted(title)).Append("';");
        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            script.Append("$dialog.InitialDirectory = '").Append(EscapePowerShellSingleQuoted(initialDirectory)).Append("';");
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            script.Append("$dialog.FileName = '").Append(EscapePowerShellSingleQuoted(fileName)).Append("';");
        }

        script.Append(saveDialog ? "$dialog.OverwritePrompt = $true;" : "$dialog.Multiselect = $false;");
        script.Append("$dialog.RestoreDirectory = $true;");
        script.Append("if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Write-Output $dialog.FileName }");
        return script.ToString();
    }

    private static string BuildPatternList(string[] extensions)
    {
        if (extensions == null || extensions.Length == 0)
        {
            return "*.*";
        }

        string[] patterns = new string[extensions.Length];
        for (int i = 0; i < extensions.Length; i++)
        {
            string extension = (extensions[i] ?? string.Empty).TrimStart('.');
            patterns[i] = "*" == extension ? "*.*" : "*." + extension;
        }

        return string.Join(";", patterns);
    }

#if UNITY_EDITOR
    private static string[] BuildEditorFilters(IReadOnlyList<FileDialogFilter> filters)
    {
        if (filters == null || filters.Count == 0)
        {
            return new[] { "All Files", "*" };
        }

        string[] result = new string[filters.Count * 2];
        for (int i = 0; i < filters.Count; i++)
        {
            result[i * 2] = filters[i].Name;
            result[i * 2 + 1] = string.Join(",", filters[i].Extensions);
        }

        return result;
    }
#endif

    private static string RunWindowsDialog(string script)
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            Debug.LogWarning("Native file dialog is only available on Windows player builds.");
            return string.Empty;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // The script writes UTF-8; without this, non-ASCII paths are decoded with the system code page.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        try
        {
            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                return string.Empty;
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                Debug.LogWarning($"Native file dialog failed: {error.Trim()}");
            }

            return output.Trim();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return string.Empty;
        }
    }
}
