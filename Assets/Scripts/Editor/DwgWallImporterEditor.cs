#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DwgWallImporter))]
public sealed class DwgWallImporterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "CAD XY is mapped to Unity XZ. Default scale 0.01 assumes millimeter-based drawings imported into the current wall unit system.",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "If you assign Import Settings Popup references in the inspector, the importer will use that UI first. If not assigned, it falls back to generating a runtime popup.",
            MessageType.None);

        DwgWallImporter importer = (DwgWallImporter)target;

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Pick CAD File"))
            {
                string initialPath = string.IsNullOrWhiteSpace(importer.CadFilePath)
                    ? Application.dataPath
                    : importer.CadFilePath;

                string selectedPath = EditorUtility.OpenFilePanel("Select DWG or DXF", initialPath, string.Empty);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    Undo.RecordObject(importer, "Select CAD File");
                    importer.CadFilePath = selectedPath;
                    EditorUtility.SetDirty(importer);
                }
            }

            if (GUILayout.Button("Import Walls"))
            {
                importer.ImportFromConfiguredFile();
                EditorUtility.SetDirty(importer.gameObject);
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Create Popup UI In Scene"))
        {
            DwgWallImportPopupBuilder.CreatePopupForImporter(importer);
        }
    }
}
#endif
