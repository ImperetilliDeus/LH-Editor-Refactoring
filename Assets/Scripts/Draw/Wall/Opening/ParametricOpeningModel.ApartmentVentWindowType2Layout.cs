using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private void ApplyApartmentVentWindowType2Layout(Vector3 targetSize)
    {
        const float outerFrame = 0.28f;
        const float sash = 0.22f;
        const float frameDepth = 0.36f;
        const float sashDepth = 0.18f;
        const float glassDepth = 0.035f;
        const float handleWidth = 0.42f;
        const float handleHeight = 0.08f;
        const float handleDepth = 0.12f;

        float width = targetSize.x;
        float height = targetSize.y;
        float depth = targetSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = Mathf.Max(MinimumSize, width - outerFrame * 2f);
        float innerHeight = Mathf.Max(MinimumSize, height - outerFrame * 2f);
        float paneWidth = Mathf.Max(MinimumSize, innerWidth - sash * 2f);
        float paneHeight = Mathf.Max(MinimumSize, innerHeight - sash * 2f);
        float frontZ = -depth * 0.5f;
        float glassZ = frontZ - 0.04f;
        float sashZ = frontZ - 0.08f;
        float handleY = -halfHeight + outerFrame + sash * 0.45f;
        float handleZ = frontZ - 0.18f;

        ApplyKnownPart("Fixed_Type2OuterFrame_Left", new Vector3(-halfWidth + outerFrame * 0.5f, 0f, 0f), new Vector3(outerFrame, height, frameDepth));
        ApplyKnownPart("Fixed_Type2OuterFrame_Right", new Vector3(halfWidth - outerFrame * 0.5f, 0f, 0f), new Vector3(outerFrame, height, frameDepth));
        ApplyKnownPart("Stretch_Type2OuterFrame_Top", new Vector3(0f, halfHeight - outerFrame * 0.5f, 0f), new Vector3(width, outerFrame, frameDepth));
        ApplyKnownPart("Stretch_Type2OuterFrame_Bottom", new Vector3(0f, -halfHeight + outerFrame * 0.5f, 0f), new Vector3(width, outerFrame, frameDepth));

        ApplyKnownPart("Fixed_Type2Sash_Left", new Vector3(-innerWidth * 0.5f + sash * 0.5f, 0f, sashZ), new Vector3(sash, innerHeight, sashDepth));
        ApplyKnownPart("Fixed_Type2Sash_Right", new Vector3(innerWidth * 0.5f - sash * 0.5f, 0f, sashZ), new Vector3(sash, innerHeight, sashDepth));
        ApplyKnownPart("Stretch_Type2Sash_Top", new Vector3(0f, innerHeight * 0.5f - sash * 0.5f, sashZ), new Vector3(innerWidth, sash, sashDepth));
        ApplyKnownPart("Stretch_Type2Sash_Bottom", new Vector3(0f, -innerHeight * 0.5f + sash * 0.5f, sashZ), new Vector3(innerWidth, sash, sashDepth));

        ApplyKnownPart("Stretch_Type2_ClearGlass", new Vector3(0f, 0f, glassZ), new Vector3(paneWidth, paneHeight, glassDepth));
        ApplyKnownPart("Stretch_Type2_BottomSeal", new Vector3(0f, handleY + sash * 0.55f, handleZ + 0.02f), new Vector3(paneWidth, 0.05f, 0.08f));
        ApplyKnownPart("Fixed_Type2_Handle_Base", new Vector3(0f, handleY, handleZ), new Vector3(handleWidth, handleHeight, handleDepth));
        ApplyKnownPart("Fixed_Type2_Handle_Lever", new Vector3(0f, handleY - 0.18f, handleZ - 0.02f), new Vector3(0.12f, 0.36f, 0.10f));
    }
}
