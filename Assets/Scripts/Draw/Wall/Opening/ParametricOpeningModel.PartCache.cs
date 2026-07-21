using System;
using UnityEngine;

public sealed partial class ParametricOpeningModel
{
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
}
