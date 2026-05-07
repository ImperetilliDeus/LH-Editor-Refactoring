using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MaterialSelectionButton : MonoBehaviour
{
    [SerializeField] private Image thumbnailImage;

    private Button cachedButton;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Initialize(Sprite thumbnail, Action onClick)
    {
        ResolveReferences();

        if (thumbnailImage != null)
        {
            thumbnailImage.sprite = thumbnail;
            thumbnailImage.enabled = thumbnail != null;
        }

        cachedButton.onClick.RemoveAllListeners();
        if (onClick != null)
        {
            cachedButton.onClick.AddListener(() => onClick.Invoke());
        }
    }

    private void ResolveReferences()
    {
        if (cachedButton == null)
        {
            cachedButton = GetComponent<Button>();
        }

        if (thumbnailImage == null)
        {
            thumbnailImage = GetComponentInChildren<Image>(true);
        }
    }
}
