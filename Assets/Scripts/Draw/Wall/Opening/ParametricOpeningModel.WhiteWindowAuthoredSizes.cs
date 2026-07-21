using UnityEngine;

internal static class WhiteWindowOpeningPartSizes
{
    public static Vector3 ResolveTwoPanelWindow(string partName, Vector3 referenceSize, float minimumSize)
    {
        const float frameThickness = 0.62f;
        const float frameDepth = 0.55f;
        const float glassDepth = 0.08f;
        const float mullionWidth = 0.34f;
        const float sashRail = 0.18f;

        float width = Mathf.Max(minimumSize, referenceSize.x);
        float height = Mathf.Max(minimumSize, referenceSize.z);
        float innerWidth = Mathf.Max(minimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(minimumSize, height - frameThickness * 2f);
        float paneWidth = Mathf.Max(minimumSize, (innerWidth - mullionWidth) * 0.5f);
        float paneHeight = Mathf.Max(minimumSize, innerHeight - sashRail * 2f);

        if (ContainsAny(partName, "Frame_Left", "Frame_Right"))
        {
            return new Vector3(frameThickness, frameDepth, height);
        }

        if (ContainsAny(partName, "Frame_Top", "Frame_Bottom"))
        {
            return new Vector3(width, frameDepth, frameThickness);
        }

        if (partName.Contains("CenterMullion"))
        {
            return new Vector3(mullionWidth, sashRail, innerHeight);
        }

        if (partName.Contains("Glass"))
        {
            return new Vector3(paneWidth, glassDepth, paneHeight);
        }

        if (partName.Contains("RecessLine_Horizontal"))
        {
            return new Vector3(paneWidth, sashRail, sashRail);
        }

        if (partName.Contains("Threshold"))
        {
            return new Vector3(width, 0.16f, 0.12f);
        }

        if (partName.Contains("Handle"))
        {
            return new Vector3(0.12f, 0.12f, 1.1f);
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
