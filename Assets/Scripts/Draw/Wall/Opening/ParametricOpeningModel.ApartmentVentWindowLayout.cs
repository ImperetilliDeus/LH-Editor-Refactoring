using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private void ApplyApartmentVentWindowLayout(Vector3 targetSize)
    {
        const float frame = 0.24f;
        const float rearFrame = 0.10f;
        const float frameDepth = 0.34f;
        const float bladeHeight = 0.18f;
        const float bladeDepth = 0.26f;
        const float bladePitch = 0.38f;
        const float meshDepth = 0.035f;
        const float handleWidth = 0.08f;
        const int maxBladeCount = 36;

        float width = targetSize.x;
        float height = targetSize.y;
        float depth = targetSize.z;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        float innerWidth = Mathf.Max(MinimumSize, width - frame * 2f);
        float innerHeight = Mathf.Max(MinimumSize, height - frame * 2f);
        float frontZ = -depth * 0.5f;
        float bladeWidth = Mathf.Max(MinimumSize, innerWidth - 0.12f);
        float bladeAreaHeight = Mathf.Max(MinimumSize, innerHeight - 0.16f);
        int activeBladeCount = Mathf.Clamp(Mathf.FloorToInt(bladeAreaHeight / bladePitch) + 1, 3, maxBladeCount);
        EnsureApartmentVentWindowBladeParts(activeBladeCount);
        float bladeGap = activeBladeCount > 1
            ? Mathf.Min(bladePitch, (bladeAreaHeight - bladeHeight) / (activeBladeCount - 1))
            : 0f;
        float firstBladeY = -bladeAreaHeight * 0.5f + bladeHeight * 0.5f;

        ApplyKnownPart("Fixed_Frame_Left", new Vector3(-halfWidth + frame * 0.5f, 0f, 0f), new Vector3(frame, height, frameDepth));
        ApplyKnownPart("Fixed_Frame_Right", new Vector3(halfWidth - frame * 0.5f, 0f, 0f), new Vector3(frame, height, frameDepth));
        ApplyKnownPart("Stretch_Frame_Top", new Vector3(0f, halfHeight - frame * 0.5f, 0f), new Vector3(width, frame, frameDepth));
        ApplyKnownPart("Stretch_Frame_Bottom", new Vector3(0f, -halfHeight + frame * 0.5f, 0f), new Vector3(width, frame, frameDepth));

        ApplyKnownPart("Stretch_InnerLip_Top", new Vector3(0f, halfHeight - frame - rearFrame * 0.5f, frontZ - 0.02f), new Vector3(innerWidth, rearFrame, rearFrame));
        ApplyKnownPart("Stretch_InnerLip_Bottom", new Vector3(0f, -halfHeight + frame + rearFrame * 0.5f, frontZ - 0.02f), new Vector3(innerWidth, rearFrame, rearFrame));
        ApplyKnownPart("Fixed_InnerLip_Left", new Vector3(-halfWidth + frame + rearFrame * 0.5f, 0f, frontZ - 0.02f), new Vector3(rearFrame, innerHeight, rearFrame));
        ApplyKnownPart("Fixed_InnerLip_Right", new Vector3(halfWidth - frame - rearFrame * 0.5f, 0f, frontZ - 0.02f), new Vector3(rearFrame, innerHeight, rearFrame));
        ApplyKnownPart("Stretch_Back_Mesh", new Vector3(0f, 0f, depth * 0.5f - meshDepth * 0.5f), new Vector3(innerWidth, innerHeight, meshDepth));

        for (int i = 0; i < maxBladeCount; i++)
        {
            string name = $"Stretch_LouverBlade_{i + 1:00}";
            PartState part = FindPart(name);
            if (part == null || part.Transform == null)
            {
                continue;
            }

            bool active = i < activeBladeCount;
            part.Transform.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            float y = firstBladeY + bladeGap * i;
            part.Transform.localPosition = new Vector3(0f, y, frontZ + bladeDepth * 0.22f);
            part.Transform.localScale = ResolveScaleForSize(part, new Vector3(bladeWidth, bladeHeight, bladeDepth));
            part.Transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        }

        ApplyKnownPart("Fixed_RightSide_Handle", new Vector3(halfWidth + 0.05f, 0f, 0f), new Vector3(handleWidth, height * 0.64f, 0.12f));
        ApplyKnownPart("Fixed_Handle_Knob", new Vector3(halfWidth + 0.12f, 0f, frontZ - 0.02f), new Vector3(0.12f, 0.12f, 0.12f));
    }

    private void EnsureApartmentVentWindowBladeParts(int requiredBladeCount)
    {
        int existingCount = CountApartmentVentWindowBladeParts();
        if (existingCount >= requiredBladeCount)
        {
            return;
        }

        PartState sourcePart = FindPart("Stretch_LouverBlade_01");
        if (sourcePart == null || sourcePart.Transform == null)
        {
            return;
        }

        for (int i = existingCount; i < requiredBladeCount; i++)
        {
            Transform clone = Instantiate(sourcePart.Transform.gameObject, sourcePart.Transform.parent).transform;
            clone.name = $"Stretch_LouverBlade_{i + 1:00}";
            clone.gameObject.SetActive(true);
            parts.Add(new PartState(clone, authoredSize, ApartmentVentWindowProfile, false));
        }
    }

    private int CountApartmentVentWindowBladeParts()
    {
        int count = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] != null &&
                parts[i].Transform != null &&
                parts[i].Transform.name.StartsWith("Stretch_LouverBlade_", System.StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
