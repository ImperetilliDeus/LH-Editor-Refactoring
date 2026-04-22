using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

internal sealed class DwgWallImportPopupController
{
    private readonly List<Toggle> popupLayerToggles = new List<Toggle>();
    private readonly List<string> popupAvailableLayers = new List<string>();

    public int AvailableLayerCount => popupAvailableLayers.Count;

    public void SetAvailableLayers(IEnumerable<string> layers)
    {
        popupAvailableLayers.Clear();
        if (layers == null)
        {
            return;
        }

        popupAvailableLayers.AddRange(layers);
    }

    public void PopulateLayerToggleList(
        Transform popupLayerToggleContainer,
        Toggle popupLayerTogglePrefab,
        string searchText,
        Func<string, bool> isSelectedByDefault,
        Action<UnityEngine.Object> destroySafely)
    {
        ClearPopupLayerToggles(destroySafely);

        if (popupLayerToggleContainer == null || popupLayerTogglePrefab == null)
        {
            return;
        }

        for (int i = 0; i < popupAvailableLayers.Count; i++)
        {
            string layerName = popupAvailableLayers[i];
            Toggle toggle = UnityEngine.Object.Instantiate(popupLayerTogglePrefab, popupLayerToggleContainer);
            toggle.name = $"LayerToggle_{layerName}";
            toggle.gameObject.SetActive(true);
            toggle.transform.localScale = Vector3.one;
            LayoutElement layoutElement = toggle.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = toggle.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.minHeight = 28f;
            layoutElement.preferredHeight = 28f;

            if (toggle.transform is RectTransform toggleRect)
            {
                toggleRect.anchorMin = new Vector2(0f, 1f);
                toggleRect.anchorMax = new Vector2(1f, 1f);
                toggleRect.pivot = new Vector2(0.5f, 1f);
                toggleRect.sizeDelta = new Vector2(0f, 28f);
                toggleRect.anchoredPosition = new Vector2(0f, -(i * 36f));
                toggleRect.offsetMin = new Vector2(0f, toggleRect.offsetMin.y);
                toggleRect.offsetMax = new Vector2(0f, toggleRect.offsetMax.y);
            }

            Text label = toggle.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = layerName;
            }

            toggle.isOn = isSelectedByDefault?.Invoke(layerName) ?? false;
            popupLayerToggles.Add(toggle);
        }

        ApplyPopupLayerSearchFilter(popupLayerToggleContainer, searchText);
    }

    public void ClearPopupLayerToggles(Action<UnityEngine.Object> destroySafely)
    {
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            if (popupLayerToggles[i] != null)
            {
                destroySafely?.Invoke(popupLayerToggles[i].gameObject);
            }
        }

        popupLayerToggles.Clear();
    }

    public string[] GetSelectedLayers()
    {
        List<string> selectedLayers = new List<string>();
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            Toggle toggle = popupLayerToggles[i];
            if (toggle == null || !toggle.isOn)
            {
                continue;
            }

            Text label = toggle.GetComponentInChildren<Text>(true);
            if (label != null && !string.IsNullOrWhiteSpace(label.text))
            {
                selectedLayers.Add(label.text);
            }
        }

        return selectedLayers.ToArray();
    }

    public void SetPopupLayerSelectionState(bool selected, string searchText)
    {
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            Toggle toggle = popupLayerToggles[i];
            if (toggle == null || !toggle.gameObject.activeSelf)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                Text label = toggle.GetComponentInChildren<Text>(true);
                string layerName = label != null ? label.text : string.Empty;
                if (layerName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
            }

            toggle.isOn = selected;
        }
    }

    public void ApplyPopupLayerSearchFilter(Transform popupLayerToggleContainer, string searchText)
    {
        int visibleCount = 0;
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            Toggle toggle = popupLayerToggles[i];
            if (toggle == null)
            {
                continue;
            }

            Text label = toggle.GetComponentInChildren<Text>(true);
            string layerName = label != null ? label.text : string.Empty;
            bool visible = string.IsNullOrWhiteSpace(searchText) ||
                           layerName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
            toggle.gameObject.SetActive(visible);
            if (visible)
            {
                if (toggle.transform is RectTransform toggleRect)
                {
                    toggleRect.anchoredPosition = new Vector2(0f, -(visibleCount * 36f));
                }

                visibleCount++;
            }
        }

        if (popupLayerToggleContainer is RectTransform rectTransform)
        {
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, Mathf.Max(0f, visibleCount * 36f + 12f));
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    public bool HasAnyPopupLayerSelected()
    {
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            if (popupLayerToggles[i] != null && popupLayerToggles[i].isOn)
            {
                return true;
            }
        }

        return false;
    }

    public int CountVisiblePopupLayerToggles()
    {
        int count = 0;
        for (int i = 0; i < popupLayerToggles.Count; i++)
        {
            Toggle toggle = popupLayerToggles[i];
            if (toggle != null && toggle.gameObject.activeInHierarchy)
            {
                count++;
            }
        }

        return count;
    }

    public void Reset()
    {
        popupLayerToggles.Clear();
        popupAvailableLayers.Clear();
    }
}
