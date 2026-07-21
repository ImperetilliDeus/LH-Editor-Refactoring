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

    private static bool IsParametricPartName(string partName)
    {
        return !string.IsNullOrEmpty(partName) &&
               (partName.StartsWith("Fixed_", StringComparison.Ordinal) ||
                partName.StartsWith("Stretch_", StringComparison.Ordinal));
    }

}
