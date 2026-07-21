using System.Collections.Generic;
using UnityEngine;

public sealed partial class ParametricOpeningModel
{
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
}
