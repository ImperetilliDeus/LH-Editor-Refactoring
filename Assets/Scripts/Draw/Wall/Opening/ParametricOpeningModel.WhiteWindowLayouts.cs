using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private void ApplyWhiteTwoPanelWindowLayout(Vector3 targetSize)
    {
        const float frameThickness = 0.62f;
        const float frameDepth = 0.55f;
        const float glassDepth = 0.08f;
        const float mullionWidth = 0.34f;
        const float sashRail = 0.18f;
        const float handleWidth = 0.12f;
        const float handleHeight = 1.1f;

        float width = targetSize.x;
        float depth = targetSize.y;
        float height = targetSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = Mathf.Max(MinimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(MinimumSize, height - frameThickness * 2f);
        float paneWidth = Mathf.Max(MinimumSize, (innerWidth - mullionWidth) * 0.5f);
        float paneHeight = Mathf.Max(MinimumSize, innerHeight - sashRail * 2f);
        float paneOffset = mullionWidth * 0.5f + paneWidth * 0.5f;
        float glassY = -depth * 0.5f - glassDepth * 0.1f;
        float frameFaceY = glassY - 0.12f;

        ApplyKnownPart("Fixed_Frame_Left", new Vector3(-halfWidth + frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Fixed_Frame_Right", new Vector3(halfWidth - frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Stretch_Frame_Top", new Vector3(0f, 0f, halfHeight - frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));
        ApplyKnownPart("Stretch_Frame_Bottom", new Vector3(0f, 0f, -halfHeight + frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));

        ApplyKnownPart("Fixed_VerticalTrim_CenterMullion", new Vector3(0f, frameFaceY, 0f), new Vector3(mullionWidth, sashRail, innerHeight));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_DoorPanel_01_Top", new Vector3(-paneOffset, frameFaceY, halfHeight - frameThickness - sashRail * 0.5f), new Vector3(paneWidth, sashRail, sashRail));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_DoorPanel_01_Bottom", new Vector3(-paneOffset, frameFaceY, -halfHeight + frameThickness + sashRail * 0.5f), new Vector3(paneWidth, sashRail, sashRail));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_DoorPanel_02_Top", new Vector3(paneOffset, frameFaceY, halfHeight - frameThickness - sashRail * 0.5f), new Vector3(paneWidth, sashRail, sashRail));
        ApplyKnownPart("Stretch_RecessLine_Horizontal_DoorPanel_02_Bottom", new Vector3(paneOffset, frameFaceY, -halfHeight + frameThickness + sashRail * 0.5f), new Vector3(paneWidth, sashRail, sashRail));

        ApplyKnownPart("Stretch_DoorPanel_LeftGlass", new Vector3(-paneOffset, glassY, 0f), new Vector3(paneWidth, glassDepth, paneHeight));
        ApplyKnownPart("Stretch_DoorPanel_RightGlass", new Vector3(paneOffset, glassY, 0f), new Vector3(paneWidth, glassDepth, paneHeight));
        ApplyKnownPart("Fixed_Right_Handle_Slim", new Vector3(halfWidth - frameThickness - 0.22f, frameFaceY - 0.08f, -halfHeight + height * 0.47f), new Vector3(handleWidth, handleWidth, handleHeight));
        ApplyKnownPart("Stretch_Threshold_FrontStrip", new Vector3(0f, depth * 0.5f + 0.08f, -halfHeight + frameThickness * 0.2f), new Vector3(width, 0.16f, 0.12f));
    }
}
