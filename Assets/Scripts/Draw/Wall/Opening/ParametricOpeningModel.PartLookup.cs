using System;
using UnityEngine;

public sealed partial class ParametricOpeningModel
{
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
}
