using System.IO;
using System.Collections.Generic;
using System.Text;
using LH.Schema;
using UnityEngine;
using UnityEngine.UI;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace LH.Export
{
    public class LhSceneExporter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform wallRoot;
        [SerializeField] private RoomManager roomManager;
        [SerializeField] private Button exportButton;

        [Header("Export")]
        [SerializeField] private string exportFilePath = "Exports/lh_scene.json";
        [SerializeField] private string defaultFileName = "lh_scene";
        [SerializeField] private Vector3 startPoint = Vector3.zero;
        [SerializeField] private bool prettyPrint = true;
        [SerializeField] private LhSceneExportBuilder.ExportMode exportMode = LhSceneExportBuilder.ExportMode.LegacyExact;

        private readonly List<Wall> cachedWalls = new List<Wall>();

        private void Awake()
        {
            ResolveReferences();
            BindEvents();
        }

        private void OnDestroy()
        {
            UnbindEvents();
        }

        public void ExportToConfiguredPath()
        {
            string resolvedPath = ResolveExportPath();
            ExportToPath(resolvedPath);
        }

        public void OpenSaveDialogAndExport()
        {
            string path = ShowSaveFileDialog();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ExportToPath(path);
        }

        public void ExportToPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("LH export skipped: path is empty.");
                return;
            }

            WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls, true);
            Room[] rooms = roomManager != null ? roomManager.GetAllRooms().ToArray() : FindObjectsByType<Room>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (exportMode == LhSceneExportBuilder.ExportMode.LegacyExact)
            {
                List<string> warnings = LhSceneExportBuilder.CollectLegacyWarnings(cachedWalls, rooms);
                for (int i = 0; i < warnings.Count; i++)
                {
                    Debug.LogWarning(warnings[i], this);
                }
            }

            object sceneDto = exportMode == LhSceneExportBuilder.ExportMode.LegacyExact
                ? (object)LhSceneExportBuilder.BuildLegacy(startPoint, cachedWalls, rooms)
                : LhSceneExportBuilder.Build(startPoint, cachedWalls, rooms);

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(sceneDto, prettyPrint);
            File.WriteAllText(path, json);
            Debug.Log($"LH scene exported: {path}");
        }

        private void ResolveReferences()
        {
            if (roomManager == null)
            {
                roomManager = FindFirstObjectByType<RoomManager>();
            }

            if (wallRoot == null)
            {
                Transform existing = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
                if (existing != null)
                {
                    wallRoot = existing;
                }
            }
        }

        private void BindEvents()
        {
            if (exportButton == null)
            {
                return;
            }

            exportButton.onClick.RemoveListener(OpenSaveDialogAndExport);
            exportButton.onClick.AddListener(OpenSaveDialogAndExport);
        }

        private void UnbindEvents()
        {
            if (exportButton == null)
            {
                return;
            }

            exportButton.onClick.RemoveListener(OpenSaveDialogAndExport);
        }

        private string ResolveExportPath()
        {
            if (Path.IsPathRooted(exportFilePath))
            {
                return exportFilePath;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", exportFilePath));
        }

        private string ShowSaveFileDialog()
        {
#if UNITY_EDITOR
            string initialDirectory = GetInitialDirectory();
            string initialFileName = GetInitialFileName();
            return UnityEditor.EditorUtility.SaveFilePanel(
                "Export LH Scene JSON",
                initialDirectory,
                initialFileName,
                "json");
#else
            if (Application.platform != RuntimePlatform.WindowsPlayer)
            {
                Debug.LogWarning($"[{nameof(LhSceneExporter)}] Runtime save dialog is only implemented for Windows Player.");
                return ResolveExportPath();
            }

            try
            {
                using Process process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = BuildSaveDialogPowerShellArguments(GetInitialDirectory(), GetInitialFileName()),
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
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                return string.Empty;
            }
#endif
        }

        private string GetInitialDirectory()
        {
            string resolvedPath = ResolveExportPath();
            string directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return projectRoot;
        }

        private string GetInitialFileName()
        {
            string configuredName = Path.GetFileNameWithoutExtension(exportFilePath);
            if (!string.IsNullOrWhiteSpace(configuredName))
            {
                return configuredName;
            }

            return string.IsNullOrWhiteSpace(defaultFileName) ? "lh_scene" : defaultFileName;
        }

        private static string BuildSaveDialogPowerShellArguments(string initialDirectory, string initialFileName)
        {
            string safeDirectory = EscapeForPowerShellSingleQuotedString(initialDirectory);
            string safeFileName = EscapeForPowerShellSingleQuotedString(initialFileName);
            string script = $@"
Add-Type -AssemblyName System.Windows.Forms
$dialog = New-Object System.Windows.Forms.SaveFileDialog
$dialog.Filter = 'JSON Files (*.json)|*.json|All Files (*.*)|*.*'
$dialog.Title = 'Export LH Scene JSON'
$dialog.InitialDirectory = '{safeDirectory}'
$dialog.FileName = '{safeFileName}.json'
$dialog.OverwritePrompt = $true
if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {{
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    Write-Output $dialog.FileName
}}";
            return "-NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand " + System.Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        }

        private static string EscapeForPowerShellSingleQuotedString(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("'", "''");
        }
    }
}
