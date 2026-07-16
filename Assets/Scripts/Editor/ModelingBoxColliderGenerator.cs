#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ModelingBoxColliderGenerator
{
    private const string UndoName = "Generate Modeling Box Colliders";
    private const float MinimumColliderSize = 0.0001f;

    public static Result GenerateForPrefabAsset(
        string prefabAssetPath,
        Options options)
    {
        if (string.IsNullOrWhiteSpace(prefabAssetPath))
        {
            return Result.Empty;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabAssetPath);
        try
        {
            Result result = Generate(prefabRoot, options, false);
            if (result.changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabAssetPath);
            }

            return result;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    public static Result Generate(
        GameObject root,
        Options options,
        bool recordUndo)
    {
        if (root == null)
        {
            return Result.Empty;
        }

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        Result result = Result.Empty;
        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter meshFilter = meshFilters[i];
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            GameObject target = meshFilter.gameObject;
            if (!ShouldProcess(target, options))
            {
                result.skipped++;
                continue;
            }

            BoxCollider collider = target.GetComponent<BoxCollider>();
            if (collider == null)
            {
                if (recordUndo)
                {
                    collider = Undo.AddComponent<BoxCollider>(target);
                }
                else
                {
                    collider = target.AddComponent<BoxCollider>();
                }

                result.added++;
            }
            else
            {
                if (!options.overwriteExisting)
                {
                    result.skipped++;
                    continue;
                }

                if (recordUndo)
                {
                    Undo.RecordObject(collider, UndoName);
                }

                result.updated++;
            }

            ApplyMeshBounds(collider, meshFilter.sharedMesh.bounds);
            collider.isTrigger = options.isTrigger;
            EditorUtility.SetDirty(target);
            result.changed = true;
        }

        return result;
    }

    private static bool ShouldProcess(GameObject target, Options options)
    {
        if (target == null)
        {
            return false;
        }

        if (options.parametricPartsOnly && !IsParametricPartName(target.name))
        {
            return false;
        }

        if (options.skipInactiveObjects && !target.activeInHierarchy)
        {
            return false;
        }

        if (options.skipGlassParts && target.name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsParametricPartName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName) &&
               (objectName.StartsWith("Fixed_", StringComparison.Ordinal) ||
                objectName.StartsWith("Stretch_", StringComparison.Ordinal));
    }

    private static void ApplyMeshBounds(BoxCollider collider, Bounds meshBounds)
    {
        collider.center = meshBounds.center;
        collider.size = new Vector3(
            Mathf.Max(MinimumColliderSize, Mathf.Abs(meshBounds.size.x)),
            Mathf.Max(MinimumColliderSize, Mathf.Abs(meshBounds.size.y)),
            Mathf.Max(MinimumColliderSize, Mathf.Abs(meshBounds.size.z)));
    }

    [Serializable]
    public struct Options
    {
        public bool parametricPartsOnly;
        public bool skipGlassParts;
        public bool skipInactiveObjects;
        public bool overwriteExisting;
        public bool isTrigger;

        public static Options Default => new Options
        {
            parametricPartsOnly = true,
            skipGlassParts = true,
            skipInactiveObjects = false,
            overwriteExisting = true,
            isTrigger = false,
        };
    }

    public struct Result
    {
        public int added;
        public int updated;
        public int skipped;
        public bool changed;

        public static Result Empty => default;

        public void Add(Result other)
        {
            added += other.added;
            updated += other.updated;
            skipped += other.skipped;
            changed |= other.changed;
        }
    }
}
#endif
