using System.Collections.Generic;
using UnityEngine;

public sealed partial class ParametricOpeningModel
{
    private const string GeneratedRailingBarPrefix = "Generated_Railing_Vertical_Bar_";

    [Tooltip("Center-to-center spacing of railing vertical bars in model local units. 0 uses the authored bar spacing.")]
    [SerializeField] private float railingBarPitch;

    // Clones are named without the Fixed_/Stretch_ prefix so the part cache never treats them as authored parts.
    private readonly List<Transform> generatedRailingBars = new List<Transform>();
    private readonly List<Transform> railingBarSlots = new List<Transform>();

    private void ApplyRailingVerticalBars(Vector3 referenceSize, Vector3 targetSize)
    {
        if (railingVerticalBars.Count == 0)
        {
            return;
        }

        float pitch = ResolveRailingBarPitch();
        if (pitch <= MinimumSize)
        {
            return;
        }

        ResolveRailingBounds(referenceSize.x * 0.5f, targetSize.x * 0.5f, out float left, out float right);
        float span = right - left;
        int barCount = RailingBarLayout.ResolveBarCount(span, pitch);
        float endGap = RailingBarLayout.ResolveEndGap(span, pitch, barCount);

        PartState template = railingVerticalBars[0];
        CollectRailingBarSlots(template.Transform, barCount);
        for (int i = 0; i < railingBarSlots.Count; i++)
        {
            Transform bar = railingBarSlots[i];
            bool active = i < barCount;
            bar.gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            // Bars are never scaled; y/z follow the template bar that ApplyPart already placed.
            Vector3 position = template.Transform.localPosition;
            position.x = left + endGap + pitch * i;
            bar.localPosition = position;
            bar.localRotation = template.LocalRotation;
            bar.localScale = template.LocalScale;
        }
    }

    private float ResolveRailingBarPitch()
    {
        if (railingBarPitch > MinimumSize)
        {
            return railingBarPitch;
        }

        float total = 0f;
        int count = 0;
        for (int i = 1; i < railingVerticalBars.Count; i++)
        {
            float spacing = Mathf.Abs(railingVerticalBars[i].LocalPosition.x - railingVerticalBars[i - 1].LocalPosition.x);
            if (spacing > MinimumSize)
            {
                total += spacing;
                count++;
            }
        }

        return count > 0 ? total / count : 0f;
    }

    // The railing spans between the left and right posts, which stay anchored to the opening edges.
    // Without posts the railing spans the full opening width.
    private void ResolveRailingBounds(float referenceHalf, float targetHalf, out float left, out float right)
    {
        left = -targetHalf;
        right = targetHalf;
        for (int i = 0; i < parts.Count; i++)
        {
            PartState part = parts[i];
            if (part == null || part.Transform == null)
            {
                continue;
            }

            string partName = part.Transform.name;
            if (partName.Contains("Railing_Left_Post"))
            {
                left = -targetHalf + (part.LocalPosition.x + referenceHalf);
            }
            else if (partName.Contains("Railing_Right_Post"))
            {
                right = targetHalf - (referenceHalf - part.LocalPosition.x);
            }
        }
    }

    private void CollectRailingBarSlots(Transform template, int requiredCount)
    {
        railingBarSlots.Clear();
        for (int i = 0; i < railingVerticalBars.Count; i++)
        {
            if (railingVerticalBars[i].Transform != null)
            {
                railingBarSlots.Add(railingVerticalBars[i].Transform);
            }
        }

        generatedRailingBars.RemoveAll(bar => bar == null);
        while (railingBarSlots.Count + generatedRailingBars.Count < requiredCount)
        {
            GameObject clone = Instantiate(template.gameObject, template.parent);
            clone.name = GeneratedRailingBarPrefix + (railingBarSlots.Count + generatedRailingBars.Count + 1).ToString("00");
            generatedRailingBars.Add(clone.transform);
        }

        railingBarSlots.AddRange(generatedRailingBars);
    }
}
