using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private void ApplyWhiteOneSlidingDoorLayout(Vector3 targetSize)
    {
        const float frameThickness = 0.52f;
        const float frameDepth = 0.52f;
        const float glassDepth = 0.08f;
        const float sealThickness = 0.10f;
        const float guideThickness = 0.16f;
        const float handleWidth = 0.12f;
        const float handleDepth = 0.10f;
        const float handleHeightRatio = 0.34f;
        const float handleInset = 0.62f;

        float width = targetSize.x;
        float depth = targetSize.y;
        float height = targetSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = Mathf.Max(MinimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(MinimumSize, height - frameThickness * 2f);
        float glassHeight = Mathf.Max(MinimumSize, innerHeight - sealThickness * 2f);
        float faceY = -depth * 0.5f - glassDepth * 0.25f;
        float railY = faceY - glassDepth * 0.75f;
        float handleHeight = Mathf.Max(1.2f, height * handleHeightRatio);
        float handleX = halfWidth - frameThickness - handleInset;
        float handleZ = -halfHeight + height * 0.48f;

        ApplyKnownPart("Fixed_Frame_Left", new Vector3(-halfWidth + frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Fixed_Frame_Right", new Vector3(halfWidth - frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Stretch_Frame_Top", new Vector3(0f, 0f, halfHeight - frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));
        ApplyKnownPart("Stretch_Frame_Bottom", new Vector3(0f, 0f, -halfHeight + frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));

        ApplyKnownPart("Stretch_DoorPanel_TranslucentGlass", new Vector3(0f, faceY, 0f), new Vector3(innerWidth, glassDepth, glassHeight));
        ApplyKnownPart("Stretch_Seal_Top", new Vector3(0f, railY, halfHeight - frameThickness - sealThickness * 0.5f), new Vector3(innerWidth, sealThickness, sealThickness));
        ApplyKnownPart("Stretch_Seal_Bottom", new Vector3(0f, railY, -halfHeight + frameThickness + sealThickness * 0.5f), new Vector3(innerWidth, sealThickness, sealThickness));
        ApplyKnownPart("Stretch_TopGuide_Rail", new Vector3(0f, railY, halfHeight - frameThickness * 0.45f), new Vector3(width, guideThickness, guideThickness));
        ApplyKnownPart("Stretch_Threshold_FrontStrip", new Vector3(0f, depth * 0.5f + 0.08f, -halfHeight + frameThickness * 0.2f), new Vector3(width, 0.16f, 0.12f));

        ApplyKnownPart("Fixed_Handle_VerticalBar", new Vector3(handleX, railY - 0.12f, handleZ), new Vector3(handleWidth, handleDepth, handleHeight));
        ApplyKnownPart("Fixed_Handle_TopCap", new Vector3(handleX, railY - 0.12f, handleZ + handleHeight * 0.5f), new Vector3(handleWidth * 2.2f, handleDepth * 1.5f, handleWidth));
        ApplyKnownPart("Fixed_Handle_BottomCap", new Vector3(handleX, railY - 0.12f, handleZ - handleHeight * 0.5f), new Vector3(handleWidth * 2.2f, handleDepth * 1.5f, handleWidth));
    }

    private void ApplyWhiteSwingDoorLayout(Vector3 targetSize)
    {
        const float frameThickness = 0.48f;
        const float frameDepth = 0.9f;
        const float slabDepth = 0.36f;
        const float inset = 0.42f;
        const float trimThickness = 0.055f;
        const float trimDepth = 0.035f;
        const float knobRadius = 0.20f;
        const float knobDepth = 0.18f;
        const float handleRadius = 0.055f;
        const float handleLength = 0.95f;
        const float hingeRadius = 0.13f;
        const float hingeDepth = 0.54f;

        float width = targetSize.x;
        float depth = targetSize.y;
        float height = targetSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = Mathf.Max(MinimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(MinimumSize, height - frameThickness * 2f);
        float innerLeft = -halfWidth + frameThickness;
        float innerRight = halfWidth - frameThickness;
        float slabY = -depth * 0.5f - slabDepth * 0.12f;
        float trimY = slabY - slabDepth * 0.58f;

        ApplyKnownPart("Fixed_Frame_Left", new Vector3(-halfWidth + frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Fixed_Frame_Right", new Vector3(halfWidth - frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Stretch_Frame_Top", new Vector3(0f, 0f, halfHeight - frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));
        ApplyKnownPart("Stretch_Frame_Bottom", new Vector3(0f, 0f, -halfHeight + frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));
        ApplyKnownPart("Stretch_DoorPanel_MainPanel", new Vector3(0f, slabY, 0f), new Vector3(innerWidth, slabDepth, innerHeight));

        ApplyKnownPart("Fixed_Trim_Left", new Vector3(innerLeft + inset, trimY, 0f), new Vector3(trimThickness, trimDepth, Mathf.Max(MinimumSize, innerHeight - inset * 2f)));
        ApplyKnownPart("Fixed_Trim_Right", new Vector3(innerRight - inset, trimY, 0f), new Vector3(trimThickness, trimDepth, Mathf.Max(MinimumSize, innerHeight - inset * 2f)));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_Top", new Vector3(0f, trimY, halfHeight - frameThickness - inset), new Vector3(Mathf.Max(MinimumSize, innerWidth - inset * 2f), trimDepth, trimThickness));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_Bottom", new Vector3(0f, trimY, -halfHeight + frameThickness + inset), new Vector3(Mathf.Max(MinimumSize, innerWidth - inset * 2f), trimDepth, trimThickness));

        float knobX = innerRight - 0.78f;
        float knobY = slabY - slabDepth * 0.75f;
        float knobZ = -halfHeight + height * 0.47f;
        ApplyKnownPart("Fixed_Right_Knob_Round", new Vector3(knobX, knobY, knobZ), new Vector3(knobRadius * 2f, knobDepth, knobRadius * 2f));
        ApplyKnownPart("Fixed_Right_Handle_Lever", new Vector3(knobX - 0.48f, knobY - 0.02f, knobZ), new Vector3(handleLength, handleRadius * 2f, handleRadius * 2f));

        float hingeX = innerLeft + 0.18f;
        float hingeY = depth * 0.5f + 0.12f;
        for (int i = 1; i <= 3; i++)
        {
            float hingeZ = -halfHeight + height * (i == 1 ? 0.22f : i == 2 ? 0.50f : 0.78f);
            ApplyKnownPart($"Fixed_Hinge_Cylinder_{i:00}", new Vector3(hingeX, hingeY, hingeZ), new Vector3(hingeRadius * 2f, hingeDepth, hingeRadius * 2f));
        }

        ApplyKnownPart("Stretch_Threshold_FrontStrip", new Vector3(0f, depth * 0.5f + 0.10f, -halfHeight + 0.10f), new Vector3(width, 0.18f, 0.10f));
    }

    private void ApplyWhiteGlassFrameDoorLayout(Vector3 targetSize)
    {
        const float frameThickness = 0.45f;
        const float frameDepth = 0.55f;
        const float panelDepth = 0.12f;
        const float railThickness = 0.18f;
        const float mullionWidth = 0.34f;
        const float handleWidth = 0.12f;
        const float handleHeight = 1.1f;

        float width = targetSize.x;
        float depth = targetSize.y;
        float height = targetSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = Mathf.Max(MinimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(MinimumSize, height - frameThickness * 2f);
        float panelWidth = Mathf.Max(MinimumSize, (innerWidth - mullionWidth) * 0.5f);
        float panelOffset = mullionWidth * 0.5f + panelWidth * 0.5f;
        float faceY = -depth * 0.5f - panelDepth * 0.2f;
        float railFrontY = faceY - panelDepth * 0.8f;

        ApplyKnownPart("Fixed_Frame_Left", new Vector3(-halfWidth + frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Fixed_Frame_Right", new Vector3(halfWidth - frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Stretch_Frame_Top", new Vector3(0f, 0f, halfHeight - frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));
        ApplyKnownPart("Stretch_Frame_Bottom", new Vector3(0f, 0f, -halfHeight + frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));

        ApplyKnownPart("Fixed_VerticalTrim_CenterMullion", new Vector3(0f, railFrontY, 0f), new Vector3(mullionWidth, railThickness, innerHeight));
        ApplyKnownPart("Stretch_Top_Rail", new Vector3(0f, railFrontY, halfHeight - frameThickness - railThickness * 0.5f), new Vector3(innerWidth, railThickness, railThickness));
        ApplyKnownPart("Stretch_Bottom_Rail", new Vector3(0f, railFrontY, -halfHeight + frameThickness + railThickness * 0.5f), new Vector3(innerWidth, railThickness, railThickness));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_DoorPanel_01_Top", new Vector3(-panelOffset, railFrontY - 0.03f, halfHeight - frameThickness - railThickness * 0.5f), new Vector3(panelWidth, 0.045f, 0.045f));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_DoorPanel_01_Bottom", new Vector3(-panelOffset, railFrontY - 0.03f, -halfHeight + frameThickness + railThickness * 0.5f), new Vector3(panelWidth, 0.045f, 0.045f));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_DoorPanel_02_Top", new Vector3(panelOffset, railFrontY - 0.03f, halfHeight - frameThickness - railThickness * 0.5f), new Vector3(panelWidth, 0.045f, 0.045f));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_DoorPanel_02_Bottom", new Vector3(panelOffset, railFrontY - 0.03f, -halfHeight + frameThickness + railThickness * 0.5f), new Vector3(panelWidth, 0.045f, 0.045f));

        ApplyKnownPart("Stretch_DoorPanel_LeftGlass", new Vector3(-panelOffset, faceY, 0f), new Vector3(panelWidth, panelDepth, innerHeight - railThickness * 2f));
        ApplyKnownPart("Stretch_DoorPanel_RightGlass", new Vector3(panelOffset, faceY, 0f), new Vector3(panelWidth, panelDepth, innerHeight - railThickness * 2f));

        ApplyKnownPart("Fixed_Right_Handle_Slim", new Vector3(halfWidth - frameThickness - 0.42f, railFrontY - 0.08f, -halfHeight + height * 0.48f), new Vector3(handleWidth, handleWidth, handleHeight));
        ApplyKnownPart("Stretch_Threshold_FrontStrip", new Vector3(0f, depth * 0.5f + 0.08f, -halfHeight + frameThickness * 0.2f), new Vector3(width, 0.16f, 0.12f));
    }
}
