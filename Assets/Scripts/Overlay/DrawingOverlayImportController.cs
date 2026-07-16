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
            if (!TryLoadTexture(path, out Texture2D texture, out string error))
            {
                overlayManager.ShowStatusOnly($"Failed to load the image file.\n{error}");
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

    private static bool TryLoadTexture(string path, out Texture2D texture, out string error)
    {
        texture = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            error = "The image file could not be found.";
            return false;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            Texture2D loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (ImageConversion.LoadImage(loadedTexture, bytes, false))
            {
                ConfigureImportedTexture(loadedTexture);
                texture = loadedTexture;
                return true;
            }

            UnityEngine.Object.Destroy(loadedTexture);
        }
        catch (Exception exception)
        {
            error = exception.Message;
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if (TryLoadTextureWithWindowsDecoder(path, out texture, out string windowsDecoderError))
        {
            return true;
        }

        error = string.IsNullOrWhiteSpace(error)
            ? windowsDecoderError
            : $"{error}\n{windowsDecoderError}";
#endif

        if (string.IsNullOrWhiteSpace(error))
        {
            error = "Unity could not decode the selected image.";
        }

        return false;
    }

    private static bool TryLoadTextureWithWindowsDecoder(string path, out Texture2D texture, out string error)
    {
        texture = null;
        error = string.Empty;

        string tempPngPath = Path.Combine(
            Path.GetTempPath(),
            $"LHOverlay_{Guid.NewGuid():N}.png");

        try
        {
            if (!TryConvertImageToPngWithPowerShell(path, tempPngPath, out error))
            {
                return false;
            }

            byte[] pngBytes = File.ReadAllBytes(tempPngPath);
            Texture2D decodedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(decodedTexture, pngBytes, false))
            {
                UnityEngine.Object.Destroy(decodedTexture);
                error = "Windows converted the image, but Unity could not read the converted PNG.";
                return false;
            }

            ConfigureImportedTexture(decodedTexture);
            texture = decodedTexture;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Windows image decoder failed: {exception.Message}";
            return false;
        }
        finally
        {
            TryDeleteFile(tempPngPath);
        }
    }

    private static bool TryConvertImageToPngWithPowerShell(string sourcePath, string outputPath, out string error)
    {
        error = string.Empty;
        string script = $@"
Add-Type -AssemblyName System.Drawing
$sourcePath = '{EscapePowerShellSingleQuotedString(sourcePath)}'
$outputPath = '{EscapePowerShellSingleQuotedString(outputPath)}'
$image = [System.Drawing.Image]::FromFile($sourcePath)
try {{
    $image.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}} finally {{
    $image.Dispose()
}}";

        try
        {
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            process.Start();
            string stderr = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(stderr)
                    ? $"Windows image decoder exited with code {process.ExitCode}."
                    : stderr;
                return false;
            }

            if (!File.Exists(outputPath))
            {
                error = "Windows image decoder did not create a converted PNG.";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return (value ?? string.Empty).Replace("'", "''");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best effort cleanup only.
        }
    }

    private static void ConfigureImportedTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
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
