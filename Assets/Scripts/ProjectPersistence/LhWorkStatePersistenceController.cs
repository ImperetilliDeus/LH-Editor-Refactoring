using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class LhWorkStatePersistenceController : MonoBehaviour
{
    private const string WorkStateExtension = "lhscene";
    private const string WorkStateDialogFilter = "LH Scene Files (*.lhscene)|*.lhscene|JSON Files (*.json)|*.json|All Files (*.*)|*.*";

    [Header("References")]
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private FurnitureCatalog furnitureCatalog;
    [SerializeField] private DrawManager drawManager;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;
    [SerializeField] private FurniturePlacementManager furniturePlacementManager;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;

    [Header("Persistence")]
    [SerializeField] private string defaultFilePath = "WorkStates/lh_work_state.lhscene";
    [SerializeField] private bool prettyPrint = true;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    public void SaveToConfiguredPath()
    {
        SaveToPath(ResolveDefaultPath());
    }

    public void OpenSaveDialogAndSave()
    {
        string selectedPath = ShowSaveFileDialog(ResolveDefaultPath());
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        SaveToPath(EnsureWorkStateExtension(selectedPath));
    }

    public LhWorkStateLoadResult LoadFromConfiguredPath()
    {
        return LoadFromPath(ResolveDefaultPath());
    }

    public LhWorkStateLoadResult OpenLoadDialogAndLoad()
    {
        string selectedPath = ShowOpenFileDialog(ResolveDefaultPath());
        return string.IsNullOrWhiteSpace(selectedPath)
            ? LhWorkStateLoadResult.Ok()
            : LoadFromPath(selectedPath);
    }

    public void SaveToPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            Debug.LogWarning("Work-state save skipped: path is empty.", this);
            return;
        }

        ResolveReferences();
        LhWorkStateDto state = LhWorkStateBuilder.Build(wallRoot, roomManager, furnitureRoot);
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(state, prettyPrint);
        File.WriteAllText(path, json, Encoding.UTF8);
        Debug.Log($"Work state saved: {path}", this);
    }

    public LhWorkStateLoadResult LoadFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return LogLoadFailure("Work-state load skipped: path is empty.");
        }

        if (!File.Exists(path))
        {
            return LogLoadFailure($"Work-state file not found: {path}");
        }

        ResolveReferences();
        LhWorkStateDto state;
        try
        {
            state = JsonUtility.FromJson<LhWorkStateDto>(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return LhWorkStateLoadResult.Fail($"Failed to read work-state file: {path}");
        }

        LhWorkStateLoadResult result = LhWorkStateLoader.Load(
            state,
            wallRoot,
            roomManager,
            furnitureRoot,
            furnitureCatalog,
            BuildLoadServices());
        if (!result.Success)
        {
            Debug.LogError(result.Message, this);
            return result;
        }

        Debug.Log($"Work state loaded: {path}", this);
        return result;
    }

    public void SetReferencesForTests(
        Transform testWallRoot,
        RoomManager testRoomManager,
        Transform testFurnitureRoot,
        FurnitureCatalog testFurnitureCatalog)
    {
        wallRoot = testWallRoot;
        roomManager = testRoomManager;
        furnitureRoot = testFurnitureRoot;
        furnitureCatalog = testFurnitureCatalog;
    }

    public void SetRuntimeManagersForTests(
        HandleManager testHandleManager,
        WallLengthDisplay testWallLengthDisplay,
        WallOpeningPlacementManager testWallOpeningPlacementManager,
        FurniturePlacementManager testFurniturePlacementManager)
    {
        handleManager = testHandleManager;
        wallLengthDisplay = testWallLengthDisplay;
        wallOpeningPlacementManager = testWallOpeningPlacementManager;
        furniturePlacementManager = testFurniturePlacementManager;
    }

    private void ResolveReferences()
    {
        if (wallRoot == null)
        {
            wallRoot = LayerUtility.FindTransformByName(LayerUtility.DefaultWallRootName, true);
        }

        if (roomManager == null)
        {
            LayerUtility.ResolveObject(ref roomManager);
        }

        if (furnitureRoot == null)
        {
            furnitureRoot = LayerUtility.FindTransformByName("FurnitureRoot", true);
        }

        if (handleManager == null)
        {
            LayerUtility.ResolveObject(ref handleManager);
        }

        if (drawManager == null)
        {
            LayerUtility.ResolveObject(ref drawManager);
        }

        if (wallLengthDisplay == null)
        {
            LayerUtility.ResolveObject(ref wallLengthDisplay);
        }

        if (wallOpeningPlacementManager == null)
        {
            LayerUtility.ResolveObject(ref wallOpeningPlacementManager);
        }

        if (furniturePlacementManager == null)
        {
            LayerUtility.ResolveObject(ref furniturePlacementManager);
        }
    }

    private void BindButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SaveToConfiguredPath);
            saveButton.onClick.RemoveListener(OpenSaveDialogAndSave);
            saveButton.onClick.AddListener(OpenSaveDialogAndSave);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(HandleLoadButtonClicked);
            loadButton.onClick.AddListener(HandleLoadButtonClicked);
        }
    }

    private void UnbindButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SaveToConfiguredPath);
            saveButton.onClick.RemoveListener(OpenSaveDialogAndSave);
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(HandleLoadButtonClicked);
        }
    }

    private void HandleLoadButtonClicked()
    {
        OpenLoadDialogAndLoad();
    }

    private LhWorkStateLoadServices BuildLoadServices()
    {
        return new LhWorkStateLoadServices(
            handleManager,
            wallLengthDisplay,
            wallOpeningPlacementManager,
            furniturePlacementManager,
            drawManager);
    }

    private string ResolveDefaultPath()
    {
        if (Path.IsPathRooted(defaultFilePath))
        {
            return defaultFilePath;
        }

        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", defaultFilePath));
    }

    private LhWorkStateLoadResult LogLoadFailure(string message)
    {
        Debug.LogWarning(message, this);
        return LhWorkStateLoadResult.Fail(message);
    }

    private static string EnsureWorkStateExtension(string path)
    {
        return string.IsNullOrWhiteSpace(Path.GetExtension(path))
            ? Path.ChangeExtension(path, WorkStateExtension)
            : path;
    }

    private static string ShowSaveFileDialog(string defaultPath)
    {
#if UNITY_EDITOR
        string directory = ResolveDialogDirectory(defaultPath);
        string defaultName = Path.GetFileName(defaultPath);
        return UnityEditor.EditorUtility.SaveFilePanel("Save Work State", directory, defaultName, WorkStateExtension);
#else
        return ShowWindowsFileDialog(defaultPath, true);
#endif
    }

    private static string ShowOpenFileDialog(string defaultPath)
    {
#if UNITY_EDITOR
        string directory = ResolveDialogDirectory(defaultPath);
        return UnityEditor.EditorUtility.OpenFilePanelWithFilters(
            "Load Work State",
            directory,
            new[] { "Work State Files", "lhscene,json", "All Files", "*" });
#else
        return ShowWindowsFileDialog(defaultPath, false);
#endif
    }

    private static string ResolveDialogDirectory(string path)
    {
        string directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory) ? Application.dataPath : directory;
    }

    private static string ShowWindowsFileDialog(string defaultPath, bool saveDialog)
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
        {
            Debug.LogWarning("Native file dialog is only available on Windows runtime builds.");
            return string.Empty;
        }

        string dialogType = saveDialog ? "SaveFileDialog" : "OpenFileDialog";
        string script =
            "Add-Type -AssemblyName System.Windows.Forms;" +
            $"$dialog = New-Object System.Windows.Forms.{dialogType};" +
            $"$dialog.Filter = '{WorkStateDialogFilter}';" +
            $"$dialog.InitialDirectory = '{EscapePowerShellSingleQuoted(ResolveDialogDirectory(defaultPath))}';" +
            $"$dialog.FileName = '{EscapePowerShellSingleQuoted(Path.GetFileName(defaultPath))}';" +
            "$dialog.RestoreDirectory = $true;" +
            "if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Write-Output $dialog.FileName }";

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -STA -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script)),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

    private static string EscapePowerShellSingleQuoted(string value)
    {
        return (value ?? string.Empty).Replace("'", "''");
    }
}
