using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class LhWorkStatePersistenceController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private Transform furnitureRoot;
    [SerializeField] private FurnitureCatalog furnitureCatalog;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;

    [Header("Persistence")]
    [SerializeField] private string defaultFilePath = "WorkStates/lh_work_state.json";
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

    public LhWorkStateLoadResult LoadFromConfiguredPath()
    {
        return LoadFromPath(ResolveDefaultPath());
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

        LhWorkStateLoadResult result = LhWorkStateLoader.Load(state, wallRoot, roomManager, furnitureRoot, furnitureCatalog);
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
    }

    private void BindButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(SaveToConfiguredPath);
            saveButton.onClick.AddListener(SaveToConfiguredPath);
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
        }

        if (loadButton != null)
        {
            loadButton.onClick.RemoveListener(HandleLoadButtonClicked);
        }
    }

    private void HandleLoadButtonClicked()
    {
        LoadFromConfiguredPath();
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
}
