using System;

internal enum AxisAnchor
{
    Center,
    Negative,
    Positive,
    Proportional,
}

internal static class ParametricOpeningPartTransformRules
{
    public static bool ShouldStretchX(string partName, string activeProfileKey)
    {
        if (IsDoorProfile(activeProfileKey))
        {
            if (IsSingleEntranceDoorProfile(activeProfileKey))
            {
                return ContainsAny(
                    partName,
                    "DoorLeaf_Center",
                    "Frame_Top",
                    "Frame_Bottom",
                    "Seal_Top",
                    "Seal_Bottom",
                    "Threshold",
                    "RecessLine_Horizontal");
            }

            if (IsEntranceDoorSidePanelProfile(activeProfileKey))
            {
                return ContainsAny(
                    partName,
                    "SidePanel",
                    "Frame_Top",
                    "Frame_Bottom",
                    "Seal_Top",
                    "Seal_Bottom",
                    "Threshold");
            }

            return ContainsAny(
                partName,
                "DoorSlab_MainPanel",
                "DoorPanel",
                "Frame_Top",
                "Frame_Bottom",
                "Seal_Top",
                "Seal_Bottom",
                "Threshold",
                "RecessLine_Horizontal_MainPanel");
        }

        if (IsWindowProfile(activeProfileKey))
        {
            return partName.StartsWith("Stretch_", StringComparison.Ordinal) ||
                   ContainsAny(
                       partName,
                       "Frame_Top",
                       "Frame_Bottom",
                       "BottomRail",
                       "Top_Rail",
                       "Mid_Rail",
                       "Bottom_Rail");
        }

        return partName.StartsWith("Stretch_", StringComparison.Ordinal) ||
               ContainsAny(partName, "Frame_Top", "Frame_Bottom", "BottomRail");
    }

    public static bool ShouldStretchY(string partName, string activeProfileKey, bool activeUsesBlenderLocalAxes)
    {
        if (activeUsesBlenderLocalAxes)
        {
            return false;
        }

        if (IsDoorProfile(activeProfileKey))
        {
            return IsDoorHeightStretchPart(partName);
        }

        if (IsWindowProfile(activeProfileKey))
        {
            return IsWindowHeightStretchPart(partName);
        }

        return IsWindowHeightStretchPart(partName);
    }

    public static bool ShouldStretchZ(string partName, string activeProfileKey, bool activeUsesBlenderLocalAxes)
    {
        if (!activeUsesBlenderLocalAxes)
        {
            return false;
        }

        if (IsDoorProfile(activeProfileKey))
        {
            return IsDoorHeightStretchPart(partName);
        }

        if (IsWindowProfile(activeProfileKey))
        {
            return IsWindowHeightStretchPart(partName);
        }

        return IsWindowHeightStretchPart(partName);
    }

    public static AxisAnchor ResolveXAnchor(string partName, string activeProfileKey)
    {
        if (IsDoorProfile(activeProfileKey))
        {
            if (IsSingleEntranceDoorProfile(activeProfileKey))
            {
                if (ContainsAny(partName, "Frame_Left", "Seal_Left", "Lock", "Handle"))
                {
                    return AxisAnchor.Negative;
                }

                if (ContainsAny(partName, "Frame_Right", "Seal_Right", "Hinge", "TopCloser"))
                {
                    return AxisAnchor.Positive;
                }

                return AxisAnchor.Center;
            }

            if (IsEntranceDoorSidePanelProfile(activeProfileKey))
            {
                if (ContainsAny(partName, "Frame_Left", "Seal_Left", "SidePanel"))
                {
                    return AxisAnchor.Negative;
                }

                if (ContainsAny(partName, "Frame_Right", "Seal_Right", "DoorLeaf", "Lock", "Handle", "Hinge", "TopCloser"))
                {
                    return AxisAnchor.Positive;
                }

                return AxisAnchor.Center;
            }

            if (partName.Contains("VerticalTrim_BetweenPanels"))
            {
                return AxisAnchor.Negative;
            }

            if (ContainsAny(partName, "_Left", "LeftPanel", "Frame_Left", "Seal_Left", "Lock", "Handle"))
            {
                return AxisAnchor.Negative;
            }

            if (ContainsAny(partName, "_Right", "MainPanel", "Frame_Right", "Seal_Right", "TopCloser", "Hinge"))
            {
                return AxisAnchor.Positive;
            }

            return AxisAnchor.Center;
        }

        if (partName.Contains("Glass"))
        {
            return AxisAnchor.Center;
        }

        if (partName.Contains("_Left") || partName.Contains("Left_Post"))
        {
            return AxisAnchor.Negative;
        }

        if (partName.Contains("_Right") || partName.Contains("Right_Post"))
        {
            return AxisAnchor.Positive;
        }

        return AxisAnchor.Center;
    }

    public static AxisAnchor ResolveYAnchor(string partName, string activeProfileKey, bool activeUsesBlenderLocalAxes)
    {
        if (activeUsesBlenderLocalAxes)
        {
            return AxisAnchor.Center;
        }

        if (IsDoorProfile(activeProfileKey))
        {
            return ResolveDoorHeightAnchor(partName);
        }

        if (partName.Contains("Railing") ||
            partName.Contains("_Bottom") ||
            partName.Contains("BottomRail") ||
            partName.Contains("Bottom_Rail") ||
            partName.Contains("Balcony_Floor"))
        {
            return AxisAnchor.Negative;
        }

        if (partName.Contains("_Top"))
        {
            return AxisAnchor.Positive;
        }

        return AxisAnchor.Center;
    }

    public static AxisAnchor ResolveZAnchor(string partName, string activeProfileKey, bool activeUsesBlenderLocalAxes)
    {
        if (activeUsesBlenderLocalAxes && IsDoorProfile(activeProfileKey))
        {
            return ResolveDoorHeightAnchor(partName);
        }

        if (activeUsesBlenderLocalAxes && IsWindowProfile(activeProfileKey))
        {
            return ResolveWindowHeightAnchor(partName);
        }

        return AxisAnchor.Center;
    }

    private static bool IsDoorHeightStretchPart(string partName)
    {
        return ContainsAny(
            partName,
            "DoorSlab",
            "DoorPanel",
            "DoorLeaf",
            "SidePanel",
            "VerticalAccent",
            "VerticalTrim_BetweenPanels",
            "Frame_Left",
            "Frame_Right",
            "Seal_Left",
            "Seal_Right");
    }

    private static bool IsWindowHeightStretchPart(string partName)
    {
        return ContainsAny(
            partName,
            "Glass",
            "Frame_Left",
            "Frame_Right",
            "BalconyFrame_Left",
            "BalconyFrame_Right",
            "Center_Mullion");
    }

    private static AxisAnchor ResolveDoorHeightAnchor(string partName)
    {
        if (ContainsAny(partName, "_Bottom", "Bottom", "Threshold"))
        {
            return AxisAnchor.Negative;
        }

        if (ContainsAny(partName, "_Top", "TopCloser", "UpperMainPanel"))
        {
            return AxisAnchor.Positive;
        }

        if (ContainsAny(partName, "Lock", "Handle"))
        {
            return AxisAnchor.Negative;
        }

        if (partName.Contains("Hinge"))
        {
            return AxisAnchor.Proportional;
        }

        return AxisAnchor.Center;
    }

    private static AxisAnchor ResolveWindowHeightAnchor(string partName)
    {
        if (partName.Contains("Railing") ||
            partName.Contains("_Bottom") ||
            partName.Contains("BottomRail") ||
            partName.Contains("Bottom_Rail") ||
            partName.Contains("Balcony_Floor"))
        {
            return AxisAnchor.Negative;
        }

        if (partName.Contains("_Top"))
        {
            return AxisAnchor.Positive;
        }

        return AxisAnchor.Center;
    }

    private static bool IsDoorProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, ParametricOpeningProfileKeys.EntranceDoorLockset, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, ParametricOpeningProfileKeys.SingleEntranceDoor, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, ParametricOpeningProfileKeys.EntranceDoorSidePanel, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, ParametricOpeningProfileKeys.WhiteGlassFrameDoor, StringComparison.Ordinal) ||
               (!string.IsNullOrEmpty(activeProfileKey) && activeProfileKey.Contains("DOOR"));
    }

    private static bool IsSingleEntranceDoorProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, ParametricOpeningProfileKeys.SingleEntranceDoor, StringComparison.Ordinal);
    }

    private static bool IsEntranceDoorSidePanelProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, ParametricOpeningProfileKeys.EntranceDoorSidePanel, StringComparison.Ordinal);
    }

    private static bool IsWindowProfile(string activeProfileKey)
    {
        return string.Equals(activeProfileKey, ParametricOpeningProfileKeys.BalconyRailingWindow, StringComparison.Ordinal) ||
               string.Equals(activeProfileKey, ParametricOpeningProfileKeys.WhiteTwoPanelWindow, StringComparison.Ordinal) ||
               (!string.IsNullOrEmpty(activeProfileKey) &&
                (activeProfileKey.Contains("WINDOW") || activeProfileKey.Contains("BALCONY")));
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
