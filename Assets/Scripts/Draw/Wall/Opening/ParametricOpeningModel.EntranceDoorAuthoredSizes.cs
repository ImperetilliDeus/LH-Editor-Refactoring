using UnityEngine;

internal static class EntranceDoorOpeningPartSizes
{
    public static Vector3 ResolveSingleEntranceDoor(string partName, Vector3 referenceSize, float minimumSize)
    {
        const float frameThickness = 0.55f;
        const float frameDepth = 1.15f;
        const float slabDepth = 0.42f;
        const float trimWidth = 0.16f;
        const float sealWidth = trimWidth * 0.55f;
        const float lockWidth = 0.72f;
        const float lockDepth = 0.22f;
        const float lockHeight = 2.3f;
        const float handleLength = 1.15f;
        const float hubDiameter = 0.36f;

        float width = Mathf.Max(minimumSize, referenceSize.x);
        float height = Mathf.Max(minimumSize, referenceSize.z);
        float innerWidth = Mathf.Max(minimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(minimumSize, height - frameThickness * 2f);

        if (ContainsAny(partName, "Frame_Left", "Frame_Right"))
        {
            return new Vector3(frameThickness, frameDepth, height);
        }

        if (ContainsAny(partName, "Frame_Top", "Frame_Bottom"))
        {
            return new Vector3(width, frameDepth, frameThickness);
        }

        if (ContainsAny(partName, "Seal_Left", "Seal_Right"))
        {
            return new Vector3(sealWidth, sealWidth, innerHeight);
        }

        if (ContainsAny(partName, "Seal_Top", "Seal_Bottom"))
        {
            return new Vector3(innerWidth, sealWidth, sealWidth);
        }

        if (partName.Contains("DoorLeaf_Center"))
        {
            return new Vector3(innerWidth, slabDepth, innerHeight);
        }

        if (partName.Contains("VerticalTrim") || partName.Contains("EdgeTrim"))
        {
            return new Vector3(trimWidth, trimWidth, innerHeight);
        }

        if (partName.Contains("RecessLine_Horizontal"))
        {
            return new Vector3(innerWidth * 0.78f, 0.035f, 0.035f);
        }

        if (partName.Contains("DoorPlaque"))
        {
            return new Vector3(1.05f, 0.08f, 0.42f);
        }

        if (partName.Contains("Lock_BackPlate"))
        {
            return new Vector3(lockWidth, lockDepth, lockHeight);
        }

        if (partName.Contains("Lock_Keypad"))
        {
            return new Vector3(lockWidth * 0.72f, 0.08f, lockHeight * 0.55f);
        }

        if (partName.Contains("Lock_LowerCover"))
        {
            return new Vector3(lockWidth * 0.62f, 0.06f, lockHeight * 0.22f);
        }

        if (partName.Contains("Handle_RoundHub"))
        {
            return new Vector3(hubDiameter, 0.16f, hubDiameter);
        }

        if (partName.Contains("Handle_Lever"))
        {
            return new Vector3(handleLength, 0.15f, 0.15f);
        }

        if (partName.Contains("Hinge_Cylinder"))
        {
            return new Vector3(0.4f, 0.72f, 0.4f);
        }

        if (partName.Contains("Hinge_Cap"))
        {
            return new Vector3(0.52f, 0.22f, 0.32f);
        }

        if (partName.Contains("TopCloser"))
        {
            return new Vector3(0.85f, 0.32f, 0.35f);
        }

        if (partName.Contains("Threshold"))
        {
            return new Vector3(width, 0.24f, 0.13f);
        }

        return Vector3.one * minimumSize;
    }

    public static Vector3 ResolveEntranceDoorLockset(string partName, Vector3 referenceSize, float minimumSize)
    {
        const float frameThickness = 0.55f;
        const float frameDepth = 1.15f;
        const float slabDepth = 0.42f;
        const float sidePanelWidth = 2.25f;
        const float revealGap = 0.08f;
        const float trimWidth = 0.16f;
        const float sealWidth = trimWidth * 0.55f;
        const float lockWidth = 0.72f;
        const float lockDepth = 0.22f;
        const float lockHeight = 2.3f;
        const float handleLength = 1.15f;
        const float hubDiameter = 0.36f;

        float width = Mathf.Max(minimumSize, referenceSize.x);
        float height = Mathf.Max(minimumSize, referenceSize.z);
        float innerWidth = Mathf.Max(minimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(minimumSize, height - frameThickness * 2f);
        float mainPanelWidth = Mathf.Max(minimumSize, innerWidth - sidePanelWidth - revealGap);

        if (ContainsAny(partName, "Frame_Left", "Frame_Right"))
        {
            return new Vector3(frameThickness, frameDepth, height);
        }

        if (ContainsAny(partName, "Frame_Top", "Frame_Bottom"))
        {
            return new Vector3(width, frameDepth, frameThickness);
        }

        if (ContainsAny(partName, "Seal_Left", "Seal_Right"))
        {
            return new Vector3(sealWidth, sealWidth, innerHeight);
        }

        if (ContainsAny(partName, "Seal_Top", "Seal_Bottom"))
        {
            return new Vector3(innerWidth, sealWidth, sealWidth);
        }

        if (partName.Contains("VerticalAccent_LeftPanel"))
        {
            return new Vector3(sidePanelWidth, slabDepth, innerHeight);
        }

        if (partName.Contains("DoorSlab_MainPanel"))
        {
            return new Vector3(mainPanelWidth, slabDepth, innerHeight);
        }

        if (partName.Contains("VerticalTrim_BetweenPanels"))
        {
            return new Vector3(trimWidth, trimWidth, innerHeight);
        }

        if (partName.Contains("RecessLine_Horizontal_LeftPanel"))
        {
            return new Vector3(sidePanelWidth * 0.96f, 0.035f, 0.035f);
        }

        if (partName.Contains("RecessLine_Horizontal_MainPanel"))
        {
            return new Vector3(mainPanelWidth * 0.96f, 0.035f, 0.035f);
        }

        if (partName.Contains("DoorPlaque"))
        {
            return new Vector3(1.05f, 0.08f, 0.42f);
        }

        if (partName.Contains("Lock_BackPlate"))
        {
            return new Vector3(lockWidth, lockDepth, lockHeight);
        }

        if (partName.Contains("Lock_Keypad"))
        {
            return new Vector3(lockWidth * 0.72f, 0.08f, lockHeight * 0.55f);
        }

        if (partName.Contains("Lock_LowerCover"))
        {
            return new Vector3(lockWidth * 0.62f, 0.06f, lockHeight * 0.22f);
        }

        if (partName.Contains("Handle_RoundHub"))
        {
            return new Vector3(hubDiameter, 0.16f, hubDiameter);
        }

        if (partName.Contains("Handle_Lever"))
        {
            return new Vector3(handleLength, 0.15f, 0.15f);
        }

        if (partName.Contains("Hinge_Cylinder"))
        {
            return new Vector3(0.4f, 0.72f, 0.4f);
        }

        if (partName.Contains("Hinge_Cap"))
        {
            return new Vector3(0.52f, 0.22f, 0.32f);
        }

        if (partName.Contains("TopCloser"))
        {
            return new Vector3(0.85f, 0.32f, 0.35f);
        }

        if (partName.Contains("Threshold"))
        {
            return new Vector3(width, 0.24f, 0.13f);
        }

        return Vector3.one * minimumSize;
    }

    public static Vector3 ResolveEntranceDoorSidePanel(string partName, Vector3 referenceSize, float minimumSize)
    {
        const float frameThickness = 0.55f;
        const float frameDepth = 1.15f;
        const float slabDepth = 0.42f;
        const float doorLeafWidth = 9.0f;
        const float revealGap = 0.10f;
        const float trimWidth = 0.16f;
        const float sealWidth = trimWidth * 0.55f;
        const float lockWidth = 0.72f;
        const float lockDepth = 0.22f;
        const float lockHeight = 2.3f;
        const float handleLength = 1.15f;
        const float hubDiameter = 0.36f;

        float width = Mathf.Max(minimumSize, referenceSize.x);
        float height = Mathf.Max(minimumSize, referenceSize.z);
        float innerWidth = Mathf.Max(minimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(minimumSize, height - frameThickness * 2f);
        float sidePanelWidth = Mathf.Max(minimumSize, innerWidth - doorLeafWidth - revealGap);

        if (ContainsAny(partName, "Frame_Left", "Frame_Right"))
        {
            return new Vector3(frameThickness, frameDepth, height);
        }

        if (ContainsAny(partName, "Frame_Top", "Frame_Bottom"))
        {
            return new Vector3(width, frameDepth, frameThickness);
        }

        if (ContainsAny(partName, "Seal_Left", "Seal_Right"))
        {
            return new Vector3(sealWidth, sealWidth, innerHeight);
        }

        if (ContainsAny(partName, "Seal_Top", "Seal_Bottom"))
        {
            return new Vector3(innerWidth, sealWidth, sealWidth);
        }

        if (partName.Contains("SidePanel"))
        {
            return new Vector3(sidePanelWidth, slabDepth, innerHeight);
        }

        if (partName.Contains("DoorLeaf"))
        {
            return new Vector3(doorLeafWidth, slabDepth, innerHeight);
        }

        if (partName.Contains("Meeting_Stile") || partName.Contains("Divider"))
        {
            return new Vector3(trimWidth, trimWidth, innerHeight);
        }

        if (partName.Contains("Lock_BackPlate"))
        {
            return new Vector3(lockWidth, lockDepth, lockHeight);
        }

        if (partName.Contains("Lock_Keypad"))
        {
            return new Vector3(lockWidth * 0.72f, 0.08f, lockHeight * 0.55f);
        }

        if (partName.Contains("Lock_LowerCover"))
        {
            return new Vector3(lockWidth * 0.62f, 0.06f, lockHeight * 0.22f);
        }

        if (partName.Contains("Handle_RoundHub"))
        {
            return new Vector3(hubDiameter, 0.16f, hubDiameter);
        }

        if (partName.Contains("Handle_Lever"))
        {
            return new Vector3(handleLength, 0.15f, 0.15f);
        }

        if (partName.Contains("Hinge_Cylinder"))
        {
            return new Vector3(0.4f, 0.72f, 0.4f);
        }

        if (partName.Contains("Hinge_Cap"))
        {
            return new Vector3(0.52f, 0.22f, 0.32f);
        }

        if (partName.Contains("TopCloser"))
        {
            return new Vector3(0.85f, 0.32f, 0.35f);
        }

        if (partName.Contains("Threshold"))
        {
            return new Vector3(width, 0.24f, 0.13f);
        }

        return Vector3.one * minimumSize;
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        if (string.IsNullOrEmpty(value) || patterns == null)
        {
            return false;
        }

        for (int i = 0; i < patterns.Length; i++)
        {
            if (!string.IsNullOrEmpty(patterns[i]) && value.Contains(patterns[i]))
            {
                return true;
            }
        }

        return false;
    }
}
