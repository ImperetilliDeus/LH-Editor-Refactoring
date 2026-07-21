using UnityEngine;

public sealed partial class ParametricOpeningModel
{
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
}
