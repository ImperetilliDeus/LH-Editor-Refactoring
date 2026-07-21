using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private void ApplySingleEntranceDoorLayout(Vector3 targetSize)
    {
        const float frameThickness = 0.55f;
        const float frameDepth = 1.15f;
        const float slabDepth = 0.42f;
        const float trimWidth = 0.16f;
        const float sealWidth = trimWidth * 0.55f;
        const float lockWidth = 0.72f;
        const float lockHeight = 2.3f;
        const float handleLength = 1.15f;

        float width = targetSize.x;
        float depth = targetSize.y;
        float height = targetSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = Mathf.Max(MinimumSize, width - frameThickness * 2f);
        float innerHeight = Mathf.Max(MinimumSize, height - frameThickness * 2f);
        float innerLeft = -halfWidth + frameThickness;
        float innerRight = halfWidth - frameThickness;
        float innerBottom = -halfHeight + frameThickness;
        float innerTop = halfHeight - frameThickness;
        float doorFaceY = -depth * 0.5f - slabDepth * 0.25f;
        float trimY = doorFaceY - slabDepth * 0.55f;
        float lockX = innerLeft + 1.8f;
        float lockZ = -halfHeight + height * 0.46f;
        float lockY = doorFaceY - slabDepth * 1.1f;

        ApplyKnownPart("Fixed_Frame_Left", new Vector3(-halfWidth + frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Fixed_Frame_Right", new Vector3(halfWidth - frameThickness * 0.5f, 0f, 0f), new Vector3(frameThickness, frameDepth, height));
        ApplyKnownPart("Stretch_Frame_Top", new Vector3(0f, 0f, halfHeight - frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));
        ApplyKnownPart("Stretch_Frame_Bottom", new Vector3(0f, 0f, -halfHeight + frameThickness * 0.5f), new Vector3(width, frameDepth, frameThickness));

        ApplyKnownPart("Fixed_Seal_Left", new Vector3(innerLeft + sealWidth * 0.5f, doorFaceY - 0.03f, 0f), new Vector3(sealWidth, sealWidth, innerHeight));
        ApplyKnownPart("Fixed_Seal_Right", new Vector3(innerRight - sealWidth * 0.5f, doorFaceY - 0.03f, 0f), new Vector3(sealWidth, sealWidth, innerHeight));
        ApplyKnownPart("Stretch_Seal_Top", new Vector3(0f, doorFaceY - 0.03f, innerTop - sealWidth * 0.5f), new Vector3(innerWidth, sealWidth, sealWidth));
        ApplyKnownPart("Stretch_Seal_Bottom", new Vector3(0f, doorFaceY - 0.03f, innerBottom + sealWidth * 0.5f), new Vector3(innerWidth, sealWidth, sealWidth));

        ApplyKnownPart("Stretch_DoorLeaf_Center", new Vector3(0f, doorFaceY, 0f), new Vector3(innerWidth, slabDepth, innerHeight));
        ApplyKnownPart("Fixed_EdgeTrim_Left", new Vector3(innerLeft + trimWidth * 0.5f, trimY, 0f), new Vector3(trimWidth, trimWidth, innerHeight));
        ApplyKnownPart("Fixed_EdgeTrim_Right", new Vector3(innerRight - trimWidth * 0.5f, trimY, 0f), new Vector3(trimWidth, trimWidth, innerHeight));

        ApplyKnownPart("Stretch_RecessLine_Horizontal_Center", new Vector3(0f, doorFaceY - slabDepth * 0.62f, -halfHeight + height * 0.52f), new Vector3(innerWidth * 0.78f, 0.035f, 0.035f));
        ApplyKnownPart("Fixed_DoorPlaque_Upper", new Vector3(innerRight - innerWidth * 0.33f, doorFaceY - slabDepth * 0.9f, -halfHeight + height * 0.68f), new Vector3(1.05f, 0.08f, 0.42f));

        ApplyKnownPart("Fixed_Lock_BackPlate", new Vector3(lockX, lockY, lockZ), new Vector3(lockWidth, 0.22f, lockHeight));
        ApplyKnownPart("Fixed_Lock_Keypad", new Vector3(lockX, lockY - 0.13f, lockZ + lockHeight * 0.19f), new Vector3(lockWidth * 0.72f, 0.08f, lockHeight * 0.55f));
        ApplyKnownPart("Fixed_Lock_LowerCover", new Vector3(lockX, lockY - 0.13f, lockZ - lockHeight * 0.30f), new Vector3(lockWidth * 0.62f, 0.06f, lockHeight * 0.22f));
        ApplyKnownPart("Fixed_Handle_RoundHub", new Vector3(lockX + lockWidth * 0.42f, lockY - 0.22f, lockZ - lockHeight * 0.38f), new Vector3(0.36f, 0.16f, 0.36f));
        ApplyKnownPart("Fixed_Handle_Lever", new Vector3(lockX + lockWidth * 0.42f + handleLength * 0.5f, lockY - 0.22f, lockZ - lockHeight * 0.38f), new Vector3(handleLength, 0.15f, 0.15f));

        float hingeX = innerRight - frameThickness * 0.35f;
        float hingeY = depth * 0.5f + 0.18f;
        for (int i = 1; i <= 3; i++)
        {
            float t = i / 4f;
            float hingeZ = innerBottom + innerHeight * t;
            ApplyKnownPart($"Fixed_Hinge_Cylinder_{i:00}", new Vector3(hingeX, hingeY, hingeZ), new Vector3(0.4f, 0.72f, 0.4f));
            ApplyKnownPart($"Fixed_Hinge_Cap_{i:00}", new Vector3(hingeX, hingeY - 0.05f, hingeZ), new Vector3(0.52f, 0.22f, 0.32f));
        }

        ApplyKnownPart("Fixed_TopCloser_RightBlock", new Vector3(innerRight - frameThickness * 0.35f, depth * 0.5f + 0.26f, halfHeight - frameThickness * 0.62f), new Vector3(0.85f, 0.32f, 0.35f));
        ApplyKnownPart("Stretch_Threshold_FrontStrip", new Vector3(0f, depth * 0.5f + 0.14f, -halfHeight + frameThickness * 0.22f), new Vector3(width, 0.24f, 0.13f));
    }
}
