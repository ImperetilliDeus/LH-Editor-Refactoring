using UnityEngine;

// Railing vertical bars keep a constant pitch for any railing length.
// The leftover length is split evenly between both ends, so each end gap is in (pitch / 2, pitch].
public static class RailingBarLayout
{
    private const float CountTolerance = 0.0001f;

    public static int ResolveBarCount(float span, float pitch)
    {
        if (span <= 0f || pitch <= 0f)
        {
            return 0;
        }

        return Mathf.Max(0, Mathf.CeilToInt(span / pitch - CountTolerance) - 1);
    }

    public static float ResolveEndGap(float span, float pitch, int barCount)
    {
        if (barCount <= 0)
        {
            return span;
        }

        return (span - (barCount - 1) * pitch) * 0.5f;
    }
}
