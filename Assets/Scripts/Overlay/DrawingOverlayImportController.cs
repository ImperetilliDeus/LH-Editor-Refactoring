using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class DrawingOverlayImportController : MonoBehaviour
{
    [SerializeField] private Button importButton;
    [SerializeField] private DrawingOverlayManager overlayManager;

    public void Initialize(Button resolvedImportButton, DrawingOverlayManager resolvedOverlayManager)
    {
        if (importButton != null)
        {
            importButton.onClick.RemoveListener(HandleImportButtonClicked);
        }

        importButton = resolvedImportButton;
        overlayManager = resolvedOverlayManager;

        if (importButton != null)
        {
            importButton.onClick.RemoveListener(HandleImportButtonClicked);
            importButton.onClick.AddListener(HandleImportButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (importButton != null)
        {
            importButton.onClick.RemoveListener(HandleImportButtonClicked);
        }
    }

    private void HandleImportButtonClicked()
    {
        if (overlayManager == null)
        {
            return;
        }

        DrawingOverlaySceneBootstrap.EnsurePanel(overlayManager);
        string path = ShowOpenFileDialog();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        switch (extension)
        {
            case ".png":
            case ".jpg":
            case ".jpeg":
                ImportImage(path);
                break;
            case ".pdf":
                ImportPdf(path);
                break;
            default:
                overlayManager.ShowStatusOnly("Unsupported file format.");
                break;
        }
    }

    private void ImportImage(string path)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(texture, bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                overlayManager.ShowStatusOnly("Failed to load the image file.");
                return;
            }

            texture.name = Path.GetFileName(path);
            overlayManager.BeginCalibration(texture, path, OverlaySourceType.Image, 0);
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception, this);
            overlayManager.ShowStatusOnly("An error occurred while importing the image.");
        }
    }

    private void ImportPdf(string path)
    {
        if (PdfThumbnailLoader.TryLoadFirstPageThumbnail(path, 2048, out Texture2D texture, out string error))
        {
            texture.name = Path.GetFileName(path);
            overlayManager.BeginCalibration(texture, path, OverlaySourceType.PdfPage, 0);
            return;
        }

        overlayManager.ShowStatusOnly($"Failed to create a PDF preview.\n{error}");
    }

    private static string ShowOpenFileDialog()
    {
#if UNITY_EDITOR
        return EditorUtility.OpenFilePanel("Select Overlay File", string.Empty, "png,jpg,jpeg,pdf");
#else
        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            return string.Empty;
        }

        try
        {
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = BuildPowerShellArguments(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
            return string.Empty;
        }
#endif
    }

    private static string BuildPowerShellArguments()
    {
        string script = @"
Add-Type -AssemblyName System.Windows.Forms
$dialog = New-Object System.Windows.Forms.OpenFileDialog
$dialog.Filter = 'Supported Files (*.png;*.jpg;*.jpeg;*.pdf)|*.png;*.jpg;*.jpeg;*.pdf|Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*'
$dialog.Multiselect = $false
$dialog.Title = 'Select Overlay File'
if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    Write-Output $dialog.FileName
}";
        return "-NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
    }
}
