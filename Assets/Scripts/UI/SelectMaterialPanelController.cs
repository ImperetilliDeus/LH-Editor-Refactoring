using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class SelectMaterialPanelController : MonoBehaviour
{
    private enum MaterialCategory
    {
        Floor,
        Ceiling,
    }

    [Header("References")]
    [SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private UiReferenceSettings uiReferenceSettings;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform textureButtonTemplate;
    [SerializeField] private Button floorCategoryButton;
    [SerializeField] private Button ceilingCategoryButton;

    [Header("State")]
    [SerializeField] private MaterialCategory activeCategory = MaterialCategory.Floor;

    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private readonly List<Sprite> generatedSprites = new List<Sprite>();

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        CacheTemplate();
        SetTemplateVisible(false);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToRoomSelection();
        RefreshVisibleButtons();
    }

    private void OnDisable()
    {
        UnsubscribeFromRoomSelection();
    }

    private void OnDestroy()
    {
        UnsubscribeFromRoomSelection();
    }

    public void ShowFloorMaterials()
    {
        activeCategory = MaterialCategory.Floor;
        RefreshVisibleButtons();
    }

    public void ShowCeilingMaterials()
    {
        activeCategory = MaterialCategory.Ceiling;
        RefreshVisibleButtons();
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref roomAuthoringPanelManager);
        LayerUtility.ResolveObject(ref roomManager);

        if (contentRoot == null)
        {
            Transform scrollView = LayerUtility.FindChildByName(transform, GetMaterialScrollViewName());
            Transform content = LayerUtility.FindChildByName(scrollView, GetMaterialContentName());
            contentRoot = content as RectTransform;
        }

        if (textureButtonTemplate == null && contentRoot != null && contentRoot.childCount > 0)
        {
            textureButtonTemplate = contentRoot.GetChild(0) as RectTransform;
        }

        if (floorCategoryButton == null)
        {
            Transform left = LayerUtility.FindChildByName(transform, GetMaterialFloorButtonName());
            if (left != null)
            {
                floorCategoryButton = left.GetComponent<Button>();
            }
        }

        if (ceilingCategoryButton == null)
        {
            Transform right = LayerUtility.FindChildByName(transform, GetMaterialCeilingButtonName());
            if (right != null)
            {
                ceilingCategoryButton = right.GetComponent<Button>();
            }
        }
    }

    private void BindButtons()
    {
        if (floorCategoryButton != null)
        {
            floorCategoryButton.onClick.RemoveListener(ShowFloorMaterials);
            floorCategoryButton.onClick.AddListener(ShowFloorMaterials);
        }

        if (ceilingCategoryButton != null)
        {
            ceilingCategoryButton.onClick.RemoveListener(ShowCeilingMaterials);
            ceilingCategoryButton.onClick.AddListener(ShowCeilingMaterials);
        }
    }

    private void CacheTemplate()
    {
        if (textureButtonTemplate == null && contentRoot != null && contentRoot.childCount > 0)
        {
            textureButtonTemplate = contentRoot.GetChild(0) as RectTransform;
        }
    }

    private void SubscribeToRoomSelection()
    {
        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged -= HandleSelectedRoomChanged;
            roomAuthoringPanelManager.SelectedRoomChanged += HandleSelectedRoomChanged;
        }
    }

    private void UnsubscribeFromRoomSelection()
    {
        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged -= HandleSelectedRoomChanged;
        }
    }

    private string GetMaterialScrollViewName()
    {
        return uiReferenceSettings != null && !string.IsNullOrWhiteSpace(uiReferenceSettings.materialScrollViewName)
            ? uiReferenceSettings.materialScrollViewName
            : "Scroll View";
    }

    private string GetMaterialContentName()
    {
        return uiReferenceSettings != null && !string.IsNullOrWhiteSpace(uiReferenceSettings.materialContentName)
            ? uiReferenceSettings.materialContentName
            : "Content";
    }

    private string GetMaterialFloorButtonName()
    {
        return uiReferenceSettings != null && !string.IsNullOrWhiteSpace(uiReferenceSettings.materialFloorButtonName)
            ? uiReferenceSettings.materialFloorButtonName
            : "_Left";
    }

    private string GetMaterialCeilingButtonName()
    {
        return uiReferenceSettings != null && !string.IsNullOrWhiteSpace(uiReferenceSettings.materialCeilingButtonName)
            ? uiReferenceSettings.materialCeilingButtonName
            : "_Right";
    }

    private void HandleSelectedRoomChanged(Room room)
    {
        RefreshVisibleButtons();
    }

    private void RefreshVisibleButtons()
    {
        ClearSpawnedButtons();
        SetTemplateVisible(false);

        if (contentRoot == null || textureButtonTemplate == null || roomManager == null)
        {
            return;
        }

        IReadOnlyList<Material> materials = activeCategory == MaterialCategory.Floor
            ? roomManager.GetFloorMaterials()
            : roomManager.GetCeilingMaterials();

        if (materials == null)
        {
            return;
        }

        Room selectedRoom = roomAuthoringPanelManager != null ? roomAuthoringPanelManager.SelectedRoom : null;
        bool canApply = selectedRoom != null;

        for (int i = 0; i < materials.Count; i++)
        {
            Material material = materials[i];
            if (material == null)
            {
                continue;
            }

            RectTransform instance = Instantiate(textureButtonTemplate, contentRoot);
            instance.gameObject.SetActive(true);
            instance.name = "_TextureButton";

            MaterialSelectionButton selectionButton = instance.GetComponent<MaterialSelectionButton>();
            if (selectionButton == null)
            {
                selectionButton = instance.gameObject.AddComponent<MaterialSelectionButton>();
            }

            string materialCode = material.name;
            selectionButton.Initialize(CreateThumbnailSprite(material), () => ApplySelectedMaterial(materialCode));

            Button button = instance.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = canApply;
            }

            spawnedButtons.Add(instance.gameObject);
        }
    }

    private void ApplySelectedMaterial(string materialCode)
    {
        if (roomManager == null || roomAuthoringPanelManager == null)
        {
            return;
        }

        Room selectedRoom = roomAuthoringPanelManager.SelectedRoom;
        if (selectedRoom == null)
        {
            return;
        }

        roomManager.UpdateRoomMetadata(
            selectedRoom,
            selectedRoom.RoomName,
            selectedRoom.RoomTypeKey,
            selectedRoom.RoomCode,
            selectedRoom.RoomNativeCode,
            activeCategory == MaterialCategory.Floor ? materialCode : roomManager.GetEffectiveFloorTextureCode(selectedRoom),
            activeCategory == MaterialCategory.Ceiling ? materialCode : roomManager.GetEffectiveCeilingTextureCode(selectedRoom));
    }

    private void ClearSpawnedButtons()
    {
        for (int i = 0; i < spawnedButtons.Count; i++)
        {
            GameObject spawned = spawnedButtons[i];
            if (spawned != null)
            {
                Destroy(spawned);
            }
        }

        spawnedButtons.Clear();

        for (int i = 0; i < generatedSprites.Count; i++)
        {
            Sprite generatedSprite = generatedSprites[i];
            if (generatedSprite != null)
            {
                Destroy(generatedSprite);
            }
        }

        generatedSprites.Clear();
    }

    private void SetTemplateVisible(bool visible)
    {
        if (textureButtonTemplate != null && textureButtonTemplate.gameObject.activeSelf != visible)
        {
            textureButtonTemplate.gameObject.SetActive(visible);
        }
    }

    private Sprite CreateThumbnailSprite(Material material)
    {
        if (material == null)
        {
            return null;
        }

        Texture2D texture = FindThumbnailTexture(material);
        if (texture == null)
        {
            return null;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f));
        generatedSprites.Add(sprite);
        return sprite;
    }

    private static Texture2D FindThumbnailTexture(Material material)
    {
        Texture2D texture = FindTextureAssetByMaterialName(material);
        if (texture != null)
        {
            return texture;
        }

        return ExtractThumbnailTexture(material);
    }

    private static Texture2D ExtractThumbnailTexture(Material material)
    {
        if (material == null)
        {
            return null;
        }

        if (material.mainTexture is Texture2D mainTexture)
        {
            return mainTexture;
        }

        if (material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") is Texture2D baseMapTexture)
        {
            return baseMapTexture;
        }

        if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") is Texture2D mainTexTexture)
        {
            return mainTexTexture;
        }

        return null;
    }

    private static Texture2D FindTextureAssetByMaterialName(Material material)
    {
        if (material == null)
        {
            return null;
        }

#if UNITY_EDITOR
        string materialPath = UnityEditor.AssetDatabase.GetAssetPath(material);
        if (string.IsNullOrWhiteSpace(materialPath))
        {
            return null;
        }

        string normalizedPath = materialPath.Replace('\\', '/');
        string materialName = material.name;
        string textureRoot = normalizedPath.Replace("/Materials/", "/Textures/");
        textureRoot = Path.GetDirectoryName(textureRoot)?.Replace('\\', '/');

        Texture2D texture = LoadTextureByExactName(textureRoot, "UI", materialName);
        if (texture != null)
        {
            return texture;
        }

        texture = LoadTextureByExactName(textureRoot, null, materialName);
        if (texture != null)
        {
            return texture;
        }

        return LoadTextureBySearch(materialName, "Assets/Prefabs/Furniture/Models/Textures");
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    private static Texture2D LoadTextureByExactName(string baseFolder, string childFolder, string fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(baseFolder) || string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return null;
        }

        string folder = string.IsNullOrWhiteSpace(childFolder)
            ? baseFolder
            : $"{baseFolder}/{childFolder}";

        if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
        {
            return null;
        }

        string[] extensions = { ".png", ".jpg", ".jpeg", ".tga" };
        for (int i = 0; i < extensions.Length; i++)
        {
            string assetPath = $"{folder}/{fileNameWithoutExtension}{extensions[i]}";
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }

    private static Texture2D LoadTextureBySearch(string materialName, string searchRoot)
    {
        if (string.IsNullOrWhiteSpace(materialName) || !UnityEditor.AssetDatabase.IsValidFolder(searchRoot))
        {
            return null;
        }

        string[] textureGuids = UnityEditor.AssetDatabase.FindAssets($"{materialName} t:Texture2D", new[] { searchRoot });
        for (int i = 0; i < textureGuids.Length; i++)
        {
            string texturePath = UnityEditor.AssetDatabase.GUIDToAssetPath(textureGuids[i]);
            string textureFileName = Path.GetFileNameWithoutExtension(texturePath);
            if (!string.Equals(textureFileName, materialName, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture != null)
            {
                return texture;
            }
        }

        return null;
    }
#endif
}
