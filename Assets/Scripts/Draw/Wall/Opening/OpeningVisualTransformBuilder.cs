using UnityEngine;

internal static class OpeningVisualTransformBuilder
{
    public static Transform Apply(WallOpeningContainer container, WallOpening opening, Transform segmentRoot)
    {
        if (container == null || opening == null)
        {
            return null;
        }

        Transform parent = segmentRoot != null ? segmentRoot : container.transform;
        if (opening.transform.parent != parent)
        {
            opening.transform.SetParent(parent, false);
        }

        Vector3 openingCenter = container.WallStart + container.WallDirection * opening.CenterDistance;
        float openingCenterY = opening.BottomY + opening.Height * 0.5f;
        if (segmentRoot != null)
        {
            Vector3 parentScale = parent.localScale;
            opening.transform.localPosition = new Vector3(
                0f,
                SafeDivide(openingCenterY - parent.position.y, parentScale.y),
                0f);
            opening.transform.localRotation = Quaternion.identity;
            opening.transform.localScale = new Vector3(
                SafeDivide(opening.Depth, parentScale.x),
                SafeDivide(opening.Height, parentScale.y),
                SafeDivide(opening.Width, parentScale.z));
        }
        else
        {
            opening.transform.position = new Vector3(
                openingCenter.x,
                openingCenterY,
                openingCenter.z);
            opening.transform.rotation = Quaternion.LookRotation(container.WallDirection, Vector3.up);
            opening.transform.localScale = new Vector3(opening.Depth, opening.Height, opening.Width);
        }

        return parent;
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        return Mathf.Abs(denominator) > 0.000001f ? numerator / denominator : 0f;
    }
}
