using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class LhWorkStatePersistenceController : MonoBehaviour
{
    private const string WorkStateExtension = "lhscene";
    private static readonly FileDialogFilter[] WorkStateOpenFilters =
    {
        new FileDialogFilter("Work State Files", WorkStateExtension, "json"),
        new FileDialogFilter("All Files", "*"),
    };

    private static readonly FileDialogFilter[] WorkStateSaveFilters =
    {
        new FileDialogFilter("LH Scene Files", WorkStateExtension),
        new FileDialogFilter("JSON Files", "json"),
        new FileDialogFilter("All Files", "*"),
    };

    [Header("References")]
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private FurnitureCatalog furnitureCatalog;
    [SerializeField] private DrawManager drawManager;
    [SerializeField] private HandleManager handleManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private WallLengthDisplay wallLengthDisplay;
    [SerializeField] private WallOpeningPlacementManager wallOpeningPlacementManager;
    [SerializeField] private FurniturePlacementManager furniturePlacementManager;
    [SerializeField] private DrawingOverlayManager drawingOverlayManager;
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

    private void ResolveReferences()
    {
        if (wallRoot == null)
        {
            wallRoot = LayerUtility.FindWallRoot(true);
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

        if (wallSelectionManager == null)
        {
            LayerUtility.ResolveObject(ref wallSelectionManager);
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

        if (drawingOverlayManager == null)
        {
            LayerUtility.ResolveObject(ref drawingOverlayManager);
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
            wallSelectionManager,
            wallLengthDisplay,
            wallOpeningPlacementManager,
            furniturePlacementManager,
            drawManager,
            drawingOverlayManager);
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
        return NativeFileDialog.SaveFile(
            "Save Work State",
            ResolveDialogDirectory(defaultPath),
            Path.GetFileName(defaultPath),
            WorkStateExtension,
            WorkStateSaveFilters);
    }

    private static string ShowOpenFileDialog(string defaultPath)
    {
        return NativeFileDialog.OpenFile("Load Work State", ResolveDialogDirectory(defaultPath), WorkStateOpenFilters);
    }

    private static string ResolveDialogDirectory(string path)
    {
        string directory = Path.GetDirectoryName(path);
        return string.IsNullOrWhiteSpace(directory) ? Application.dataPath : directory;
    }
}
