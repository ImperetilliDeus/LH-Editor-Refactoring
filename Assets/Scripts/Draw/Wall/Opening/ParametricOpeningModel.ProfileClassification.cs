using System;

public sealed partial class ParametricOpeningModel
{
    private static string NormalizeProfileKey(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static bool IsDoorProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, EntranceDoorLocksetProfile, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, SingleEntranceDoorProfile, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, EntranceDoorSidePanelProfile, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, WhiteGlassFrameDoorProfile, StringComparison.Ordinal) ||
               activeProfileKey.Contains("DOOR");
    }

    private static bool IsSingleEntranceDoorProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, SingleEntranceDoorProfile, StringComparison.Ordinal);
    }

    private static bool IsEntranceDoorSidePanelProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, EntranceDoorSidePanelProfile, StringComparison.Ordinal);
    }

    private static bool IsWindowProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, BalconyRailingWindowProfile, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, WhiteTwoPanelWindowProfile, StringComparison.Ordinal) ||
               activeProfileKey.Contains("WINDOW") ||
               activeProfileKey.Contains("BALCONY");
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        if (string.IsNullOrEmpty(value) || patterns == null)
        {
            return false;
        }

        for (int i = 0; i < patterns.Length; i++)
        {
            if (!string.IsNullOrEmpty(patterns[i]) &&
                value.IndexOf(patterns[i], StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
