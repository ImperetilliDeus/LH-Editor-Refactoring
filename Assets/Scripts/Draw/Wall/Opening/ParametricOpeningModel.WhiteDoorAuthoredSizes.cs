using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private sealed partial class PartState
    {
        private static Vector3 ResolveWhiteOneSlidingDoorAuthoredPartSize(string partName, Vector3 referenceSize)
        {
            const float frameThickness = 0.52f;
            const float frameDepth = 0.52f;
            const float glassDepth = 0.08f;
            const float sealThickness = 0.10f;
            const float guideThickness = 0.16f;
            const float handleWidth = 0.12f;
            const float handleDepth = 0.10f;

            float width = Mathf.Max(MinimumSize, referenceSize.x);
            float height = Mathf.Max(MinimumSize, referenceSize.z);
            float innerWidth = Mathf.Max(MinimumSize, width - frameThickness * 2f);
            float innerHeight = Mathf.Max(MinimumSize, height - frameThickness * 2f);
            float glassHeight = Mathf.Max(MinimumSize, innerHeight - sealThickness * 2f);
            float handleHeight = Mathf.Max(1.2f, height * 0.34f);

            if (ContainsAny(partName, "Frame_Left", "Frame_Right"))
            {
                return new Vector3(frameThickness, frameDepth, height);
            }

            if (ContainsAny(partName, "Frame_Top", "Frame_Bottom"))
            {
                return new Vector3(width, frameDepth, frameThickness);
            }

            if (partName.Contains("TranslucentGlass"))
            {
                return new Vector3(innerWidth, glassDepth, glassHeight);
            }

            if (ContainsAny(partName, "Seal_Top", "Seal_Bottom"))
            {
                return new Vector3(innerWidth, sealThickness, sealThickness);
            }

            if (partName.Contains("TopGuide_Rail"))
            {
                return new Vector3(width, guideThickness, guideThickness);
            }

            if (partName.Contains("Threshold"))
            {
                return new Vector3(width, 0.16f, 0.12f);
            }

            if (partName.Contains("Handle_VerticalBar"))
            {
                return new Vector3(handleWidth, handleDepth, handleHeight);
            }

            if (ContainsAny(partName, "Handle_TopCap", "Handle_BottomCap"))
            {
                return new Vector3(handleWidth * 2.2f, handleDepth * 1.5f, handleWidth);
            }

            return Vector3.one * MinimumSize;
        }

        private static Vector3 ResolveWhiteSwingDoorAuthoredPartSize(string partName, Vector3 referenceSize)
        {
            const float frameThickness = 0.48f;
            const float frameDepth = 0.9f;
            const float slabDepth = 0.36f;
            const float inset = 0.42f;
            const float trimThickness = 0.055f;
            const float trimDepth = 0.035f;

            float width = Mathf.Max(MinimumSize, referenceSize.x);
            float height = Mathf.Max(MinimumSize, referenceSize.z);
            float innerWidth = Mathf.Max(MinimumSize, width - frameThickness * 2f);
            float innerHeight = Mathf.Max(MinimumSize, height - frameThickness * 2f);

            if (ContainsAny(partName, "Frame_Left", "Frame_Right"))
            {
                return new Vector3(frameThickness, frameDepth, height);
            }

            if (ContainsAny(partName, "Frame_Top", "Frame_Bottom"))
            {
                return new Vector3(width, frameDepth, frameThickness);
            }

            if (partName.Contains("DoorPanel_MainPanel"))
            {
                return new Vector3(innerWidth, slabDepth, innerHeight);
            }

            if (ContainsAny(partName, "Trim_Left", "Trim_Right"))
            {
                return new Vector3(trimThickness, trimDepth, Mathf.Max(MinimumSize, innerHeight - inset * 2f));
            }

            if (partName.Contains("RecessLine_Horizontal"))
            {
                return new Vector3(Mathf.Max(MinimumSize, innerWidth - inset * 2f), trimDepth, trimThickness);
            }

            if (partName.Contains("Knob_Round"))
            {
                return new Vector3(0.4f, 0.18f, 0.4f);
            }

            if (partName.Contains("Handle_Lever"))
            {
                return new Vector3(0.95f, 0.11f, 0.11f);
            }

            if (partName.Contains("Hinge_Cylinder"))
            {
                return new Vector3(0.26f, 0.54f, 0.26f);
            }

            if (partName.Contains("Threshold"))
            {
                return new Vector3(width, 0.18f, 0.10f);
            }

            return Vector3.one * MinimumSize;
        }

        private static Vector3 ResolveWhiteGlassFrameDoorAuthoredPartSize(string partName, Vector3 referenceSize)
        {
            const float frameThickness = 0.45f;
            const float frameDepth = 0.55f;
            const float panelDepth = 0.12f;
            const float railThickness = 0.18f;
            const float mullionWidth = 0.34f;

            float width = Mathf.Max(MinimumSize, referenceSize.x);
            float height = Mathf.Max(MinimumSize, referenceSize.z);
            float innerWidth = Mathf.Max(MinimumSize, width - frameThickness * 2f);
            float innerHeight = Mathf.Max(MinimumSize, height - frameThickness * 2f);
            float panelWidth = Mathf.Max(MinimumSize, (innerWidth - mullionWidth) * 0.5f);

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
                return new Vector3(mullionWidth, railThickness, innerHeight);
            }

            if (ContainsAny(partName, "Top_Rail", "Bottom_Rail"))
            {
                return new Vector3(innerWidth, railThickness, railThickness);
            }

            if (partName.Contains("DoorPanel") && partName.Contains("Glass"))
            {
                return new Vector3(panelWidth, panelDepth, innerHeight - railThickness * 2f);
            }

            if (partName.Contains("RecessLine_Horizontal"))
            {
                return new Vector3(panelWidth, 0.045f, 0.045f);
            }

            if (partName.Contains("Threshold"))
            {
                return new Vector3(width, 0.16f, 0.12f);
            }

            if (partName.Contains("Handle"))
            {
                return new Vector3(0.12f, 0.12f, 1.1f);
            }

            return Vector3.one * MinimumSize;
        }
    }
}
