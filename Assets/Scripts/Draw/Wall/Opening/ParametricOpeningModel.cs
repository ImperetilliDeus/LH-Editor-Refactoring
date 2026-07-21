using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class ParametricOpeningModel : MonoBehaviour
{
    public const string BalconyRailingWindowProfile = ParametricOpeningProfileKeys.BalconyRailingWindow;
    public const string SingleEntranceDoorProfile = ParametricOpeningProfileKeys.SingleEntranceDoor;
    public const string EntranceDoorLocksetProfile = ParametricOpeningProfileKeys.EntranceDoorLockset;
    public const string EntranceDoorSidePanelProfile = ParametricOpeningProfileKeys.EntranceDoorSidePanel;
    public const string WhiteOneSlidingDoorProfile = ParametricOpeningProfileKeys.WhiteOneSlidingDoor;
    public const string WhiteSwingDoorProfile = ParametricOpeningProfileKeys.WhiteSwingDoor;
    public const string WhiteGlassFrameDoorProfile = ParametricOpeningProfileKeys.WhiteGlassFrameDoor;
    public const string WhiteTwoPanelWindowProfile = ParametricOpeningProfileKeys.WhiteTwoPanelWindow;

    private const float MinimumSize = 0.0001f;
    [SerializeField] private Vector3 authoredSize = new Vector3(1.8f, 2.1f, 0.14f);
    [SerializeField] private bool preferAuthoredSizeWhenCatalogReferenceIsDefault = true;
    [SerializeField] private string profileKey = string.Empty;

    private readonly List<PartState> parts = new List<PartState>();
    private readonly List<PartState> railingVerticalBars = new List<PartState>();
    private bool cached;
    private bool usesBlenderLocalAxes;
    private bool cachedUsesBlenderLocalAxes;
    private string cachedProfileKey = string.Empty;

    public static bool HasParametricParts(Transform root)
    {
        if (root == null)
        {
            return false;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            string childName = children[i] != null ? children[i].name : string.Empty;
            if (IsParametricPartName(childName))
            {
                return true;
            }
        }

        return false;
    }

    public void ApplyOpeningSize(Vector3 targetSize, Vector3 referenceSize)
    {
        ApplyOpeningSize(targetSize, referenceSize, false, profileKey);
    }

    public void ApplyOpeningSize(Vector3 targetSize, Vector3 referenceSize, bool useBlenderLocalAxes)
    {
        ApplyOpeningSize(targetSize, referenceSize, useBlenderLocalAxes, profileKey);
    }

    public void ApplyOpeningSize(
        Vector3 targetSize,
        Vector3 referenceSize,
        bool useBlenderLocalAxes,
        string parametricProfileKey)
    {
        profileKey = NormalizeProfileKey(parametricProfileKey);
        Vector3 effectiveReferenceSize = ResolveReferenceSize(referenceSize);
        usesBlenderLocalAxes = useBlenderLocalAxes;

        Vector3 targetLocalSize = ToRootLocalSize(ToModelLocalSize(targetSize));
        Vector3 referenceLocalSize = ToRootLocalSize(ToModelLocalSize(effectiveReferenceSize));

        EnsureCached(referenceLocalSize, profileKey, usesBlenderLocalAxes);
        if (parts.Count == 0)
        {
            return;
        }

        ApplyProfileMaterials(profileKey);

        Vector3 safeReference = ClampSize(referenceLocalSize);
        Vector3 safeTarget = ClampSize(targetLocalSize);
        Vector3 ratio = new Vector3(
            safeTarget.x / safeReference.x,
            safeTarget.y / safeReference.y,
            safeTarget.z / safeReference.z);

        if (IsSingleEntranceDoorProfile(profileKey))
        {
            ApplySingleEntranceDoorLayout(safeTarget);
            return;
        }

        if (string.Equals(profileKey, WhiteOneSlidingDoorProfile, StringComparison.Ordinal))
        {
            ApplyWhiteOneSlidingDoorLayout(safeTarget);
            return;
        }

        if (string.Equals(profileKey, WhiteSwingDoorProfile, StringComparison.Ordinal))
        {
            ApplyWhiteSwingDoorLayout(safeTarget);
            return;
        }

        if (string.Equals(profileKey, WhiteGlassFrameDoorProfile, StringComparison.Ordinal))
        {
            ApplyWhiteGlassFrameDoorLayout(safeTarget);
            return;
        }

        if (string.Equals(profileKey, WhiteTwoPanelWindowProfile, StringComparison.Ordinal))
        {
            ApplyWhiteTwoPanelWindowLayout(safeTarget);
            return;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            ApplyPart(parts[i], safeReference, safeTarget, ratio);
        }

        ApplyRailingVerticalBars(safeReference, safeTarget);
    }

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

    private void ApplyKnownPart(string partName, Vector3 localPosition, Vector3 localSize)
    {
        PartState part = FindPart(partName);
        if (part == null || part.Transform == null)
        {
            return;
        }

        part.Transform.localPosition = localPosition;
        part.Transform.localScale = ResolveScaleForSize(part, localSize);
        part.Transform.localRotation = part.LocalRotation;
    }

    private PartState FindPart(string partName)
    {
        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i] != null && parts[i].Transform != null && string.Equals(parts[i].Transform.name, partName, StringComparison.Ordinal))
            {
                return parts[i];
            }
        }

        return null;
    }

    private void EnsureCached(Vector3 referenceLocalSize, string activeProfileKey, bool activeUsesBlenderLocalAxes)
    {
        if (cached &&
            cachedUsesBlenderLocalAxes == activeUsesBlenderLocalAxes &&
            string.Equals(cachedProfileKey, activeProfileKey, StringComparison.Ordinal))
        {
            return;
        }

        cached = true;
        cachedProfileKey = activeProfileKey;
        cachedUsesBlenderLocalAxes = activeUsesBlenderLocalAxes;
        parts.Clear();
        railingVerticalBars.Clear();

        Vector3 safeReference = ClampSize(referenceLocalSize);
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform || !IsParametricPartName(child.name))
            {
                continue;
            }

            PartState part = new PartState(child, safeReference, activeProfileKey, activeUsesBlenderLocalAxes);
            parts.Add(part);
            if (part.IsRailingVerticalBar)
            {
                railingVerticalBars.Add(part);
            }
        }

        railingVerticalBars.Sort((left, right) => left.LocalPosition.x.CompareTo(right.LocalPosition.x));
    }

    private static Vector3 ClampSize(Vector3 value)
    {
        return new Vector3(
            Mathf.Max(MinimumSize, Mathf.Abs(value.x)),
            Mathf.Max(MinimumSize, Mathf.Abs(value.y)),
            Mathf.Max(MinimumSize, Mathf.Abs(value.z)));
    }

    private Vector3 ToModelLocalSize(Vector3 openingModelSize)
    {
        return usesBlenderLocalAxes
            ? new Vector3(openingModelSize.x, openingModelSize.z, openingModelSize.y)
            : openingModelSize;
    }

    private Vector3 ResolveReferenceSize(Vector3 catalogReferenceSize)
    {
        if (preferAuthoredSizeWhenCatalogReferenceIsDefault && IsDefaultReferenceSize(catalogReferenceSize))
        {
            return ClampSize(authoredSize);
        }

        return ClampSize(catalogReferenceSize);
    }

    private static bool IsDefaultReferenceSize(Vector3 referenceSize)
    {
        return Mathf.Abs(referenceSize.x - 1f) <= 0.001f &&
               Mathf.Abs(referenceSize.y - 1f) <= 0.001f &&
               Mathf.Abs(referenceSize.z - 1f) <= 0.001f;
    }

    private Vector3 ToRootLocalSize(Vector3 modelSize)
    {
        Vector3 rootScale = transform.localScale;
        return new Vector3(
            modelSize.x / Mathf.Max(MinimumSize, Mathf.Abs(rootScale.x)),
            modelSize.y / Mathf.Max(MinimumSize, Mathf.Abs(rootScale.y)),
            modelSize.z / Mathf.Max(MinimumSize, Mathf.Abs(rootScale.z)));
    }

    private static bool IsParametricPartName(string partName)
    {
        return !string.IsNullOrEmpty(partName) &&
               (partName.StartsWith("Fixed_", StringComparison.Ordinal) ||
                partName.StartsWith("Stretch_", StringComparison.Ordinal));
    }

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

    private void ApplyProfileMaterials(string activeProfileKey)
    {
        if (!ParametricOpeningMaterialRules.ShouldApply(activeProfileKey))
        {
            return;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            Transform partTransform = parts[i].Transform;
            if (partTransform == null)
            {
                continue;
            }

            Renderer renderer = partTransform.GetComponent<Renderer>();
            if (renderer == null)
            {
                continue;
            }

            Material material = ParametricOpeningMaterialRules.ResolveMaterial(partTransform.name, activeProfileKey);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }

}
