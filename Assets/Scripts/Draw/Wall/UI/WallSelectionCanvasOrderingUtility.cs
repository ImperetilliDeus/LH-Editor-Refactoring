using UnityEngine;
using UnityEngine.UI;

public static class WallSelectionCanvasOrderingUtility
{
    public static void PlaceBelowSelectableControls(RectTransform wallUiRoot, Transform canvasTransform)
    {
        if (wallUiRoot == null || canvasTransform == null || wallUiRoot.parent != canvasTransform)
        {
            return;
        }

        wallUiRoot.SetAsLastSibling();

        int firstSelectableSiblingIndex = int.MaxValue;
        for (int i = 0; i < canvasTransform.childCount; i++)
        {
            Transform child = canvasTransform.GetChild(i);
            if (child == null || child == wallUiRoot)
            {
                continue;
            }

            if (child.GetComponentInChildren<Selectable>(true) != null)
            {
                firstSelectableSiblingIndex = Mathf.Min(firstSelectableSiblingIndex, child.GetSiblingIndex());
            }
        }

        if (firstSelectableSiblingIndex != int.MaxValue && wallUiRoot.GetSiblingIndex() > firstSelectableSiblingIndex)
        {
            wallUiRoot.SetSiblingIndex(firstSelectableSiblingIndex);
        }
    }
}
