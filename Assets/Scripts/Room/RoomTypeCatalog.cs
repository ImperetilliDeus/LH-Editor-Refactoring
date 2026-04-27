using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class RoomTypeCatalog
{
    public const string DefaultAssetPath = "Assets/Prefabs/RoomType.asset";
    public const string DefaultJsonFileName = "RoomType.json";

    [Serializable]
    private sealed class RoomTypeListDto
    {
        public List<RoomTypeEntryDto> presets = new List<RoomTypeEntryDto>();
    }

    [Serializable]
    private sealed class RoomTypeEntryDto
    {
        public string name;
        public int code;
    }

    public readonly struct Entry
    {
        public Entry(string name, int code)
        {
            Name = name ?? string.Empty;
            Code = code;
        }

        public string Name { get; }
        public int Code { get; }
    }

    public static IReadOnlyList<Entry> LoadEntries(RoomTypePreset presetOverride = null, string jsonFileName = DefaultJsonFileName)
    {
        if (TryLoadFromJson(jsonFileName, out List<Entry> jsonEntries))
        {
            return jsonEntries;
        }

        RoomTypePreset preset = presetOverride != null ? presetOverride : LoadDefaultPreset();
        return BuildEntriesFromPreset(preset);
    }

    private static bool TryLoadFromJson(string jsonFileName, out List<Entry> entries)
    {
        entries = null;

        string fileName = string.IsNullOrWhiteSpace(jsonFileName) ? DefaultJsonFileName : jsonFileName;
        string jsonPath = Path.Combine(Application.streamingAssetsPath, fileName);
        if (!File.Exists(jsonPath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(jsonPath);
            RoomTypeListDto dto = JsonUtility.FromJson<RoomTypeListDto>(json);
            entries = BuildEntries(dto != null ? dto.presets : null);
            return entries.Count > 0;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to load room types from json: {jsonPath}\n{exception.Message}");
            entries = new List<Entry>();
            return false;
        }
    }

    private static IReadOnlyList<Entry> BuildEntriesFromPreset(RoomTypePreset preset)
    {
        return BuildEntries(preset != null ? preset.presets : null);
    }

    private static List<Entry> BuildEntries(IReadOnlyList<RoomTypePreset.RoomType> source)
    {
        List<Entry> entries = new List<Entry>();
        if (source == null)
        {
            return entries;
        }

        HashSet<string> uniqueNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < source.Count; i++)
        {
            RoomTypePreset.RoomType item = source[i];
            if (item == null || string.IsNullOrWhiteSpace(item.name))
            {
                continue;
            }

            string normalizedName = item.name.Trim();
            if (!uniqueNames.Add(normalizedName))
            {
                continue;
            }

            entries.Add(new Entry(normalizedName, item.code));
        }

        return entries;
    }

    private static List<Entry> BuildEntries(IReadOnlyList<RoomTypeEntryDto> source)
    {
        List<Entry> entries = new List<Entry>();
        if (source == null)
        {
            return entries;
        }

        HashSet<string> uniqueNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < source.Count; i++)
        {
            RoomTypeEntryDto item = source[i];
            if (item == null || string.IsNullOrWhiteSpace(item.name))
            {
                continue;
            }

            string normalizedName = item.name.Trim();
            if (!uniqueNames.Add(normalizedName))
            {
                continue;
            }

            entries.Add(new Entry(normalizedName, item.code));
        }

        return entries;
    }

    private static RoomTypePreset LoadDefaultPreset()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<RoomTypePreset>(DefaultAssetPath);
#else
        return null;
#endif
    }
}
