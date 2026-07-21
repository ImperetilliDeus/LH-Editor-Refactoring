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
