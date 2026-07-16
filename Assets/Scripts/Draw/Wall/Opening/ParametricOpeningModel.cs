using System.Collections.Generic;
using UnityEngine;

public sealed class ParametricOpeningModel : MonoBehaviour
{
    private const float MinimumSize = 0.0001f;

    [SerializeField] private Vector3 authoredSize = new Vector3(1.8f, 2.1f, 0.14f);
    [SerializeField] private bool preferAuthoredSizeWhenCatalogReferenceIsDefault = true;

    private readonly List<PartState> parts = new List<PartState>();
    private readonly List<PartState> railingVerticalBars = new List<PartState>();
    private bool cached;
    private bool usesBlenderLocalAxes;

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
        ApplyOpeningSize(targetSize, referenceSize, false);
    }

    public void ApplyOpeningSize(Vector3 targetSize, Vector3 referenceSize, bool useBlenderLocalAxes)
    {
        Vector3 effectiveReferenceSize = ResolveReferenceSize(referenceSize);
        usesBlenderLocalAxes = useBlenderLocalAxes;

        Vector3 targetLocalSize = ToRootLocalSize(ToModelLocalSize(targetSize));
        Vector3 referenceLocalSize = ToRootLocalSize(ToModelLocalSize(effectiveReferenceSize));

        EnsureCached(referenceLocalSize);
        if (parts.Count == 0)
        {
            return;
        }

        Vector3 safeReference = ClampSize(referenceLocalSize);
        Vector3 safeTarget = ClampSize(targetLocalSize);
        Vector3 ratio = new Vector3(
            safeTarget.x / safeReference.x,
            safeTarget.y / safeReference.y,
            safeTarget.z / safeReference.z);

        for (int i = 0; i < parts.Count; i++)
        {
            ApplyPart(parts[i], safeReference, safeTarget, ratio);
        }

        ApplyRailingVerticalBars(safeReference, safeTarget);
    }

    private void EnsureCached(Vector3 referenceLocalSize)
    {
        if (cached)
        {
            return;
        }

        cached = true;
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

            PartState part = new PartState(child, safeReference);
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
            nextScale.x *= ratio.x;
        }

        if (part.StretchY)
        {
            nextScale.y *= ratio.y;
        }

        if (part.StretchZ)
        {
            nextScale.z *= ratio.z;
        }

        Vector3 nextPosition = part.LocalPosition;
        nextPosition.x = ResolveAxisPosition(
            part.AnchorX,
            part.LocalPosition.x,
            part.Size.x,
            referenceSize.x,
            targetSize.x,
            ratio.x,
            part.StretchX,
            true);
        nextPosition.y = ResolveAxisPosition(
            part.AnchorY,
            part.LocalPosition.y,
            part.Size.y,
            referenceSize.y,
            targetSize.y,
            ratio.y,
            part.StretchY,
            false);
        nextPosition.z = ResolveAxisPosition(
            part.AnchorZ,
            part.LocalPosition.z,
            part.Size.z,
            referenceSize.z,
            targetSize.z,
            ratio.z,
            part.StretchZ,
            false);

        part.Transform.localScale = nextScale;
        part.Transform.localPosition = nextPosition;
        part.Transform.localRotation = part.LocalRotation;
    }

    private static float ResolveAxisPosition(
        AxisAnchor anchor,
        float originalPosition,
        float originalSize,
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
                return -targetHalf + (originalPosition + referenceHalf);
            case AxisAnchor.Positive:
                return targetHalf - (referenceHalf - originalPosition);
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
               (partName.StartsWith("Fixed_") || partName.StartsWith("Stretch_"));
    }

    private static bool ShouldStretchX(string partName)
    {
        return partName.StartsWith("Stretch_") ||
               partName.Contains("Frame_Top") ||
               partName.Contains("Frame_Bottom") ||
               partName.Contains("BottomRail") ||
               partName.Contains("Top_Rail") ||
               partName.Contains("Mid_Rail") ||
               partName.Contains("Bottom_Rail");
    }

    private static bool ShouldStretchY(string partName)
    {
        return partName.Contains("Glass") ||
               partName.Contains("Frame_Left") ||
               partName.Contains("Frame_Right") ||
               partName.Contains("BalconyFrame_Left") ||
               partName.Contains("BalconyFrame_Right") ||
               partName.Contains("Center_Mullion");
    }

    private static bool ShouldStretchZ(string partName)
    {
        return false;
    }

    private static AxisAnchor ResolveXAnchor(string partName, float originalPosition, Vector3 referenceSize)
    {
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

    private static AxisAnchor ResolveYAnchor(string partName, float originalPosition, Vector3 referenceSize)
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

    private static AxisAnchor ResolveZAnchor(string partName, float originalPosition, Vector3 referenceSize)
    {
        return AxisAnchor.Center;
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
    }

    private sealed class PartState
    {
        public readonly Transform Transform;
        public readonly Vector3 LocalPosition;
        public readonly Quaternion LocalRotation;
        public readonly Vector3 LocalScale;
        public readonly Vector3 Size;
        public readonly bool StretchX;
        public readonly bool StretchY;
        public readonly bool StretchZ;
        public readonly bool IsRailingVerticalBar;
        public readonly bool OriginalActive;
        public readonly AxisAnchor AnchorX;
        public readonly AxisAnchor AnchorY;
        public readonly AxisAnchor AnchorZ;

        public PartState(Transform transform, Vector3 referenceSize)
        {
            Transform = transform;
            LocalPosition = transform.localPosition;
            LocalRotation = transform.localRotation;
            LocalScale = transform.localScale;
            Size = CalculateApproximateLocalSize(transform);
            StretchX = ShouldStretchX(transform.name);
            StretchY = ShouldStretchY(transform.name);
            StretchZ = ShouldStretchZ(transform.name);
            IsRailingVerticalBar = transform.name.Contains("Railing_Vertical_Bar");
            OriginalActive = transform.gameObject.activeSelf;
            AnchorX = ResolveXAnchor(transform.name, LocalPosition.x, referenceSize);
            AnchorY = ResolveYAnchor(transform.name, LocalPosition.y, referenceSize);
            AnchorZ = ResolveZAnchor(transform.name, LocalPosition.z, referenceSize);
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
