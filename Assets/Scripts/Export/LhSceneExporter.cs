using System.IO;
using LH.Schema;
using UnityEngine;
using UnityEngine.UI;

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
        [SerializeField] private Vector3 startPoint = Vector3.zero;
        [SerializeField] private bool prettyPrint = true;

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

        public void ExportToPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                Debug.LogWarning("LH export skipped: path is empty.");
                return;
            }

            Wall[] walls = wallRoot != null ? wallRoot.GetComponentsInChildren<Wall>(true) : new Wall[0];
            Room[] rooms = roomManager != null ? roomManager.GetAllRooms().ToArray() : FindObjectsByType<Room>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            LhSceneDto sceneDto = LhSceneExportBuilder.Build(startPoint, walls, rooms);

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
                Transform existing = LayerUtility.FindTransformByName("Walls", true);
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

            exportButton.onClick.RemoveListener(ExportToConfiguredPath);
            exportButton.onClick.AddListener(ExportToConfiguredPath);
        }

        private void UnbindEvents()
        {
            if (exportButton == null)
            {
                return;
            }

            exportButton.onClick.RemoveListener(ExportToConfiguredPath);
        }

        private string ResolveExportPath()
        {
            if (Path.IsPathRooted(exportFilePath))
            {
                return exportFilePath;
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", exportFilePath));
        }
    }
}
