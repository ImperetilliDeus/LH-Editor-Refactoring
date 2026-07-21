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

    private static Vector3 ResolveScaleForSize(PartState part, Vector3 localSize)
    {
        return new Vector3(
            ResolveScaleAxis(part.LocalScale.x, part.Size.x, localSize.x),
            ResolveScaleAxis(part.LocalScale.y, part.Size.y, localSize.y),
            ResolveScaleAxis(part.LocalScale.z, part.Size.z, localSize.z));
    }

    private static float ResolveScaleAxis(float originalScale, float originalSize, float targetSize)
    {
        float safeOriginalSize = Mathf.Max(MinimumSize, Mathf.Abs(originalSize));
        return originalScale * Mathf.Max(MinimumSize, Mathf.Abs(targetSize)) / safeOriginalSize;
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

    private static void ApplyPart(PartState part, Vector3 referenceSize, Vector3 targetSize, Vector3 ratio)
    {
        if (part == null || part.Transform == null)
        {
            return;
        }

        Vector3 nextScale = part.LocalScale;
        if (part.StretchX)
        {
            nextScale.x = ResolveStretchScale(part.LocalScale.x, part.Size.x, referenceSize.x, targetSize.x, ratio.x, part.UseAdditiveStretchX);
        }

        if (part.StretchY)
        {
            nextScale.y = ResolveStretchScale(part.LocalScale.y, part.Size.y, referenceSize.y, targetSize.y, ratio.y, part.UseAdditiveStretchY);
        }

        if (part.StretchZ)
        {
            nextScale.z = ResolveStretchScale(part.LocalScale.z, part.Size.z, referenceSize.z, targetSize.z, ratio.z, part.UseAdditiveStretchZ);
        }

        Vector3 nextSize = ResolveNextSize(part, nextScale);
        Vector3 nextPosition = part.LocalPosition;
        nextPosition.x = ResolveAxisPosition(
            part.AnchorX,
            part.LocalPosition.x,
            part.Size.x,
            nextSize.x,
            referenceSize.x,
            targetSize.x,
            ratio.x,
            part.StretchX,
            true);
        nextPosition.y = ResolveAxisPosition(
            part.AnchorY,
            part.LocalPosition.y,
            part.Size.y,
            nextSize.y,
            referenceSize.y,
            targetSize.y,
            ratio.y,
            part.StretchY,
            false);
        nextPosition.z = ResolveAxisPosition(
            part.AnchorZ,
            part.LocalPosition.z,
            part.Size.z,
            nextSize.z,
            referenceSize.z,
            targetSize.z,
            ratio.z,
            part.StretchZ,
            false);

        part.Transform.localScale = nextScale;
        part.Transform.localPosition = nextPosition;
        part.Transform.localRotation = part.LocalRotation;
    }

    private static Vector3 ResolveNextSize(PartState part, Vector3 nextScale)
    {
        return new Vector3(
            ResolveNextAxisSize(part.Size.x, part.LocalScale.x, nextScale.x),
            ResolveNextAxisSize(part.Size.y, part.LocalScale.y, nextScale.y),
            ResolveNextAxisSize(part.Size.z, part.LocalScale.z, nextScale.z));
    }

    private static float ResolveNextAxisSize(float originalSize, float originalScale, float nextScale)
    {
        float safeOriginalScale = Mathf.Max(MinimumSize, Mathf.Abs(originalScale));
        return Mathf.Abs(originalSize) * Mathf.Abs(nextScale) / safeOriginalScale;
    }

    private static float ResolveStretchScale(
        float originalScale,
        float originalSize,
        float referenceSize,
        float targetSize,
        float ratio,
        bool useAdditiveStretch)
    {
        if (!useAdditiveStretch)
        {
            return originalScale * ratio;
        }

        float safeOriginalSize = Mathf.Max(MinimumSize, Mathf.Abs(originalSize));
        float desiredSize = Mathf.Max(MinimumSize, safeOriginalSize + targetSize - referenceSize);
        return originalScale * desiredSize / safeOriginalSize;
    }

    private static float ResolveAxisPosition(
        AxisAnchor anchor,
        float originalPosition,
        float originalSize,
        float nextSize,
        float referenceSize,
        float targetSize,
        float ratio,
        bool stretchesAxis,
        bool scaleCenteredFixedParts)
    {
        float referenceHalf = referenceSize * 0.5f;
        float targetHalf = targetSize * 0.5f;
        switch (anchor)
        {
            case AxisAnchor.Negative:
                return -targetHalf + (originalPosition - originalSize * 0.5f + referenceHalf) + nextSize * 0.5f;
            case AxisAnchor.Positive:
                return targetHalf - (referenceHalf - originalPosition - originalSize * 0.5f) - nextSize * 0.5f;
            case AxisAnchor.Proportional:
                return originalPosition * ratio;
            default:
                return stretchesAxis || scaleCenteredFixedParts ? originalPosition * ratio : originalPosition;
        }
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

    private static bool ShouldStretchX(string partName, string activeProfileKey)
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

    private static bool ShouldStretchY(string partName, string activeProfileKey, bool activeUsesBlenderLocalAxes)
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

    private static bool ShouldStretchZ(string partName, string activeProfileKey, bool activeUsesBlenderLocalAxes)
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

    private static AxisAnchor ResolveXAnchor(string partName, string activeProfileKey)
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

    private static AxisAnchor ResolveYAnchor(string partName, string activeProfileKey, bool activeUsesBlenderLocalAxes)
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

    private static AxisAnchor ResolveZAnchor(string partName, string activeProfileKey, bool activeUsesBlenderLocalAxes)
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

    private void ApplyRailingVerticalBars(Vector3 referenceSize, Vector3 targetSize)
    {
        if (railingVerticalBars.Count == 0)
        {
            return;
        }

        List<PartState> authoredBars = new List<PartState>();
        for (int i = 0; i < railingVerticalBars.Count; i++)
        {
            if (railingVerticalBars[i].OriginalActive)
            {
                authoredBars.Add(railingVerticalBars[i]);
            }
        }

        if (authoredBars.Count == 0)
        {
            authoredBars.AddRange(railingVerticalBars);
        }

        float referenceHalf = referenceSize.x * 0.5f;
        float targetHalf = targetSize.x * 0.5f;
        float leftInset = authoredBars[0].LocalPosition.x + referenceHalf;
        float rightInset = referenceHalf - authoredBars[authoredBars.Count - 1].LocalPosition.x;
        float span = Mathf.Max(MinimumSize, targetSize.x - leftInset - rightInset);
        float authoredSpacing = ResolveAuthoredBarSpacing(authoredBars);
        int desiredCount = Mathf.Clamp(Mathf.RoundToInt(span / authoredSpacing) + 1, 2, railingVerticalBars.Count);

        float startX = -targetHalf + leftInset;
        float spacing = desiredCount > 1 ? span / (desiredCount - 1) : 0f;
        for (int i = 0; i < railingVerticalBars.Count; i++)
        {
            PartState bar = railingVerticalBars[i];
            bool active = i < desiredCount;
            bar.Transform.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            Vector3 position = bar.Transform.localPosition;
            position.x = startX + spacing * i;
            bar.Transform.localPosition = position;
            bar.Transform.localScale = bar.LocalScale;
            bar.Transform.localRotation = bar.LocalRotation;
        }
    }

    private static float ResolveAuthoredBarSpacing(List<PartState> authoredBars)
    {
        if (authoredBars.Count < 2)
        {
            return 1f;
        }

        float total = 0f;
        int count = 0;
        for (int i = 1; i < authoredBars.Count; i++)
        {
            float spacing = Mathf.Abs(authoredBars[i].LocalPosition.x - authoredBars[i - 1].LocalPosition.x);
            if (spacing > MinimumSize)
            {
                total += spacing;
                count++;
            }
        }

        return count > 0 ? total / count : 1f;
    }

    private enum AxisAnchor
    {
        Center,
        Negative,
        Positive,
        Proportional,
    }

    private sealed partial class PartState
    {
        public readonly Transform Transform;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;
        public readonly Vector3 Size;
        public readonly bool StretchX;
        public readonly bool StretchY;
        public readonly bool StretchZ;
        public readonly bool UseAdditiveStretchX;
        public readonly bool UseAdditiveStretchY;
        public readonly bool UseAdditiveStretchZ;
        public readonly bool IsRailingVerticalBar;
        public readonly bool OriginalActive;
        public readonly AxisAnchor AnchorX;
        public readonly AxisAnchor AnchorY;
        public readonly AxisAnchor AnchorZ;

        public PartState(
            Transform transform,
            Vector3 referenceSize,
            string activeProfileKey,
            bool activeUsesBlenderLocalAxes)
        {
            Transform = transform;
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
            Size = ResolveAuthoredPartSize(transform, referenceSize, activeProfileKey);
            StretchX = ShouldStretchX(transform.name, activeProfileKey);
            StretchY = ShouldStretchY(transform.name, activeProfileKey, activeUsesBlenderLocalAxes);
            StretchZ = ShouldStretchZ(transform.name, activeProfileKey, activeUsesBlenderLocalAxes);
            bool isDoorProfile = IsDoorProfile(activeProfileKey);
            UseAdditiveStretchX = isDoorProfile && StretchX;
            UseAdditiveStretchY = isDoorProfile && StretchY;
            UseAdditiveStretchZ = isDoorProfile && StretchZ;
            IsRailingVerticalBar = transform.name.Contains("Railing_Vertical_Bar");
            OriginalActive = transform.gameObject.activeSelf;
            AnchorX = ResolveXAnchor(transform.name, activeProfileKey);
            AnchorY = ResolveYAnchor(transform.name, activeProfileKey, activeUsesBlenderLocalAxes);
            AnchorZ = ResolveZAnchor(transform.name, activeProfileKey, activeUsesBlenderLocalAxes);
        }

        private static Vector3 ResolveAuthoredPartSize(Transform transform, Vector3 referenceSize, string activeProfileKey)
        {
            if (IsDoorProfile(activeProfileKey))
            {
                if (IsSingleEntranceDoorProfile(activeProfileKey))
                {
                    return EntranceDoorOpeningPartSizes.ResolveSingleEntranceDoor(transform.name, referenceSize, MinimumSize);
                }

                if (IsEntranceDoorSidePanelProfile(activeProfileKey))
                {
                    return EntranceDoorOpeningPartSizes.ResolveEntranceDoorSidePanel(transform.name, referenceSize, MinimumSize);
                }

                if (string.Equals(activeProfileKey, WhiteOneSlidingDoorProfile, StringComparison.Ordinal))
                {
                    return WhiteDoorOpeningPartSizes.ResolveOneSlidingDoor(transform.name, referenceSize, MinimumSize);
                }

                if (string.Equals(activeProfileKey, WhiteSwingDoorProfile, StringComparison.Ordinal))
                {
                    return WhiteDoorOpeningPartSizes.ResolveSwingDoor(transform.name, referenceSize, MinimumSize);
                }

                if (string.Equals(activeProfileKey, WhiteGlassFrameDoorProfile, StringComparison.Ordinal))
                {
                    return WhiteDoorOpeningPartSizes.ResolveGlassFrameDoor(transform.name, referenceSize, MinimumSize);
                }

                return EntranceDoorOpeningPartSizes.ResolveEntranceDoorLockset(transform.name, referenceSize, MinimumSize);
            }

            if (string.Equals(activeProfileKey, WhiteTwoPanelWindowProfile, StringComparison.Ordinal))
            {
                return WhiteWindowOpeningPartSizes.ResolveTwoPanelWindow(transform.name, referenceSize, MinimumSize);
            }

            return CalculateApproximateLocalSize(transform);
        }

        private static Vector3 CalculateApproximateLocalSize(Transform transform)
        {
            MeshFilter meshFilter = transform.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.sharedMesh != null)
            {
                Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
                return new Vector3(
                    Mathf.Abs(meshSize.x * transform.localScale.x),
                    Mathf.Abs(meshSize.y * transform.localScale.y),
                    Mathf.Abs(meshSize.z * transform.localScale.z));
            }

            return Vector3.one * MinimumSize;
        }
    }
}
