using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RoomAuthoringPanelManager))]
[DisallowMultipleComponent]
public class RoomWallAuthoringPanelController : MonoBehaviour
{
    private sealed class ToggleVisualState
    {
        public Image background;
        public Image accentBar;
        public Text primaryLabel;
        public Text secondaryLabel;
        public bool isHovered;
        public bool isSelected;
    }

    private sealed class ToggleHoverForwarder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private RoomWallAuthoringPanelController owner;
        private WallListItem item;

        public void Initialize(RoomWallAuthoringPanelController panelOwner, WallListItem wallItem)
        {
            owner = panelOwner;
            item = wallItem;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.HandleWallToggleHoverChanged(item, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HandleWallToggleHoverChanged(item, false);
        }
    }

    private sealed class WallListItem
    {
        public Transform exportRoot;
        public readonly List<Wall> walls = new List<Wall>();
        public readonly List<string> wallIds = new List<string>();
        public string title;
        public string metadata;
        public Toggle toggle;
        public ToggleVisualState visualState;
    }

    [Header("References")]
    [SerializeField] private RoomAuthoringPanelManager roomAuthoringPanelManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private UiReferenceSettings uiReferenceSettings;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private RectTransform menuRoot;
    [SerializeField] private ScrollRect wallScrollView;
    [SerializeField] private Transform wallToggleContainer;
    [SerializeField] private Toggle wallToggleTemplate;
    [SerializeField] private Text headerText;
    [SerializeField] private Text statusText;
    [SerializeField] private Button autoAssignButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private Button applyButton;

    [Header("Lookup Fallback")]
    [SerializeField] private string menuRootName = "RoomSelectMenu";
    [SerializeField] private string wallScrollViewName = "WallScrollView";
    [SerializeField] private string wallToggleContainerName = "_LayerToggleContainer";
    [SerializeField] private string wallToggleTemplateName = "_WallToggleTemplate";
    [SerializeField] private string headerTextName = "_HeaderText";
    [SerializeField] private string statusTextName = "_StatusText";
    [SerializeField] private string autoAssignButtonName = "_AutoAssignButton";
    [SerializeField] private string resetButtonName = "_ResetButton";
    [SerializeField] private string applyButtonName = "_ApplyButton";

    [Header("Toggle Visual")]
    [SerializeField] private Color toggleNormalBackgroundColor = Color.white;
    [SerializeField] private Color toggleHoverBackgroundColor = new Color(0.2f, 0.46f, 0.38f, 0.94f);
    [SerializeField] private Color toggleSelectedBackgroundColor = new Color(0.35f, 0.29f, 0.08f, 0.98f);
    [SerializeField] private Color toggleNormalTextColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color toggleHoverTextColor = Color.white;
    [SerializeField] private Color toggleSelectedTextColor = new Color(1f, 0.96f, 0.84f, 1f);
    [SerializeField] private Color toggleNormalMetaColor = new Color(0.38f, 0.38f, 0.38f, 1f);
    [SerializeField] private Color toggleHoverMetaColor = new Color(0.9f, 1f, 0.96f, 1f);
    [SerializeField] private Color toggleSelectedMetaColor = new Color(1f, 0.92f, 0.72f, 1f);
    [SerializeField] private Color toggleHoverAccentColor = new Color(0.44f, 1f, 0.78f, 1f);
    [SerializeField] private Color toggleSelectedAccentColor = new Color(1f, 0.84f, 0.22f, 1f);
    [SerializeField] private float toggleNormalHeight = 28f;
    [SerializeField] private float toggleHoverHeight = 34f;
    [SerializeField] private float toggleSelectedHeight = 38f;

    private readonly List<Wall> cachedWalls = new List<Wall>();
    private readonly List<WallListItem> wallItems = new List<WallListItem>();
    private readonly HashSet<string> selectedWallIds = new HashSet<string>();
    private readonly HashSet<string> hoveredWallIds = new HashSet<string>();
    private readonly List<Transform> destroyBuffer = new List<Transform>();

    private Room currentRoom;
    private bool suppressToggleCallbacks;

    public event System.Action HighlightStateChanged;

    private void Awake()
    {
        ResolveReferences();
        ResolveUiReferences();
        BindEvents();
        UpdatePanelState();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ResolveReferences();
            ResolveUiReferences();
        }
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref roomAuthoringPanelManager);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveTransformByName(ref wallRoot, LayerUtility.DefaultWallRootName, true);

        if (menuRoot == null)
        {
            Transform menuTransform = LayerUtility.FindTransformByName(GetMenuRootName(), true);
            menuRoot = menuTransform as RectTransform;
        }
    }

    private void ResolveUiReferences()
    {
        if (menuRoot == null)
        {
            return;
        }

        if (wallScrollView == null)
        {
            Transform scrollTransform = LayerUtility.FindChildByName(menuRoot, GetWallScrollViewName());
            if (scrollTransform != null)
            {
                wallScrollView = scrollTransform.GetComponent<ScrollRect>();
            }
        }

        if (wallToggleContainer == null)
        {
            Transform searchRoot = wallScrollView != null ? wallScrollView.transform : menuRoot;
            wallToggleContainer = LayerUtility.FindChildByName(searchRoot, GetWallToggleContainerName());
        }

        if (wallToggleTemplate == null)
        {
            Transform templateTransform = LayerUtility.FindChildByName(menuRoot, GetWallToggleTemplateName());
            if (templateTransform != null)
            {
                wallToggleTemplate = templateTransform.GetComponent<Toggle>();
            }
        }

        if (headerText == null)
        {
            Transform target = LayerUtility.FindChildByName(menuRoot, GetHeaderTextName());
            if (target != null)
            {
                headerText = target.GetComponent<Text>();
            }
        }

        if (statusText == null)
        {
            Transform target = LayerUtility.FindChildByName(menuRoot, GetStatusTextName());
            if (target != null)
            {
                statusText = target.GetComponent<Text>();
            }
        }

        if (autoAssignButton == null)
        {
            Transform target = LayerUtility.FindChildByName(menuRoot, GetAutoAssignButtonName());
            if (target != null)
            {
                autoAssignButton = target.GetComponent<Button>();
            }
        }

        if (resetButton == null)
        {
            Transform target = LayerUtility.FindChildByName(menuRoot, GetResetButtonName());
            if (target != null)
            {
                resetButton = target.GetComponent<Button>();
            }
        }

        if (applyButton == null)
        {
            Transform target = LayerUtility.FindChildByName(menuRoot, GetApplyButtonName());
            if (target != null)
            {
                applyButton = target.GetComponent<Button>();
            }
        }
    }

    private void BindEvents()
    {
        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged -= HandleSelectedRoomChanged;
            roomAuthoringPanelManager.SelectedRoomChanged += HandleSelectedRoomChanged;
        }

        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleRoomsChanged;
            roomManager.RoomsChanged += HandleRoomsChanged;
        }

        if (autoAssignButton != null)
        {
            autoAssignButton.onClick.RemoveListener(HandleAutoAssignClicked);
            autoAssignButton.onClick.AddListener(HandleAutoAssignClicked);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(HandleResetClicked);
            resetButton.onClick.AddListener(HandleResetClicked);
        }

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(HandleApplyClicked);
            applyButton.onClick.AddListener(HandleApplyClicked);
        }
    }

    private void UnbindEvents()
    {
        if (roomAuthoringPanelManager != null)
        {
            roomAuthoringPanelManager.SelectedRoomChanged -= HandleSelectedRoomChanged;
        }

        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleRoomsChanged;
        }

        if (autoAssignButton != null)
        {
            autoAssignButton.onClick.RemoveListener(HandleAutoAssignClicked);
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(HandleResetClicked);
        }

        if (applyButton != null)
        {
            applyButton.onClick.RemoveListener(HandleApplyClicked);
        }
    }

    private void HandleSelectedRoomChanged(Room room)
    {
        currentRoom = room;
        ReloadSelectionFromRoom();
        UpdatePanelState();
        NotifyHighlightStateChanged();
    }

    private void HandleRoomsChanged()
    {
        ResolveUiReferences();
        if (currentRoom == null)
        {
            UpdatePanelState();
            return;
        }

        ReloadSelectionFromRoom();
        UpdatePanelState();
        NotifyHighlightStateChanged();
    }

    private void HandleAutoAssignClicked()
    {
        if (currentRoom == null)
        {
            return;
        }

        selectedWallIds.Clear();
        IReadOnlyList<string> automaticIds = currentRoom.AutomaticWallIds;
        for (int i = 0; i < automaticIds.Count; i++)
        {
            string wallId = automaticIds[i];
            if (!string.IsNullOrWhiteSpace(wallId))
            {
                selectedWallIds.Add(wallId);
            }
        }

        RefreshToggleStates();
        UpdateHighlights();
        UpdateStatusText();
        NotifyHighlightStateChanged();
    }

    private void HandleResetClicked()
    {
        if (currentRoom == null || roomManager == null)
        {
            return;
        }

        roomManager.UpdateRoomWallSelection(currentRoom, null, false);
        ReloadSelectionFromRoom();
        UpdatePanelState();
        NotifyHighlightStateChanged();
    }

    private void HandleApplyClicked()
    {
        if (currentRoom == null || roomManager == null)
        {
            return;
        }

        roomManager.UpdateRoomWallSelection(currentRoom, selectedWallIds, true);
        ReloadSelectionFromRoom();
        UpdatePanelState();
        NotifyHighlightStateChanged();
    }

    private void ReloadSelectionFromRoom()
    {
        selectedWallIds.Clear();
        hoveredWallIds.Clear();
        if (currentRoom == null)
        {
            RefreshWallList();
            return;
        }

        IReadOnlyList<string> effectiveIds = currentRoom.EffectiveWallIds;
        for (int i = 0; i < effectiveIds.Count; i++)
        {
            string wallId = effectiveIds[i];
            if (!string.IsNullOrWhiteSpace(wallId))
            {
                selectedWallIds.Add(wallId);
            }
        }

        RefreshWallList();
        UpdateHighlights();
        UpdateStatusText();
    }

    private void RefreshWallList()
    {
        ResolveUiReferences();
        hoveredWallIds.Clear();
        ClearWallItems();
        if (wallToggleContainer == null || wallToggleTemplate == null)
        {
            return;
        }

        BuildWallItems();
        suppressToggleCallbacks = true;
        try
        {
            for (int i = 0; i < wallItems.Count; i++)
            {
                WallListItem item = wallItems[i];
                Toggle toggle = Instantiate(wallToggleTemplate, wallToggleContainer);
                toggle.gameObject.SetActive(true);
                toggle.name = $"WallToggle_{item.title}";
                toggle.transform.localScale = Vector3.one;
                item.toggle = toggle;

                LayoutElement layoutElement = toggle.GetComponent<LayoutElement>();
                if (layoutElement == null)
                {
                    layoutElement = toggle.gameObject.AddComponent<LayoutElement>();
                }

                if (layoutElement.preferredHeight <= 0f)
                {
                    layoutElement.preferredHeight = 28f;
                }

                Text[] texts = toggle.GetComponentsInChildren<Text>(true);
                if (texts.Length > 0)
                {
                    texts[0].text = item.title;
                }

                if (texts.Length > 1)
                {
                    texts[1].text = item.metadata;
                }

                bool isSelected = HasAnyIdSelected(item.wallIds);
                toggle.SetIsOnWithoutNotify(isSelected);
                toggle.onValueChanged.AddListener(value => HandleWallToggleChanged(item, value));
                item.visualState = BuildToggleVisualState(toggle, texts);
                if (item.visualState != null)
                {
                    item.visualState.isSelected = isSelected;
                    ApplyToggleVisualState(item.visualState);
                }

                ToggleHoverForwarder hoverForwarder = toggle.GetComponent<ToggleHoverForwarder>();
                if (hoverForwarder == null)
                {
                    hoverForwarder = toggle.gameObject.AddComponent<ToggleHoverForwarder>();
                }

                hoverForwarder.Initialize(this, item);
            }
        }
        finally
        {
            suppressToggleCallbacks = false;
        }

        if (wallToggleContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }

        UpdateStatusText();
    }

    private void BuildWallItems()
    {
        if (wallRoot == null)
        {
            return;
        }

        WallHierarchyUtility.CollectWalls(wallRoot, cachedWalls, true);
        Dictionary<Transform, WallListItem> itemsByRoot = new Dictionary<Transform, WallListItem>();
        for (int i = 0; i < cachedWalls.Count; i++)
        {
            Wall wall = cachedWalls[i];
            if (wall == null)
            {
                continue;
            }

            Transform exportRoot = GetWallExportRoot(wall.transform);
            if (exportRoot == null)
            {
                continue;
            }

            if (!itemsByRoot.TryGetValue(exportRoot, out WallListItem item))
            {
                item = new WallListItem
                {
                    exportRoot = exportRoot,
                };
                itemsByRoot.Add(exportRoot, item);
                wallItems.Add(item);
            }

            if (!item.walls.Contains(wall))
            {
                item.walls.Add(wall);
            }

            if (wall.Data != null && !string.IsNullOrWhiteSpace(wall.Data.id) && !item.wallIds.Contains(wall.Data.id))
            {
                item.wallIds.Add(wall.Data.id);
            }
        }

        wallItems.Sort((left, right) => string.CompareOrdinal(left.exportRoot != null ? left.exportRoot.name : string.Empty, right.exportRoot != null ? right.exportRoot.name : string.Empty));
        for (int i = 0; i < wallItems.Count; i++)
        {
            WallListItem item = wallItems[i];
            item.title = string.IsNullOrWhiteSpace(item.exportRoot != null ? item.exportRoot.name : null)
                ? $"Wall {i + 1}"
                : item.exportRoot.name;
            item.metadata = BuildItemMetadata(item);
        }
    }

    private string BuildItemMetadata(WallListItem item)
    {
        float totalLength = 0f;
        for (int i = 0; i < item.walls.Count; i++)
        {
            Wall wall = item.walls[i];
            if (wall == null || wall.Data == null)
            {
                continue;
            }

            totalLength += wall.Data.GetLength();
        }

        int openingCount = 0;
        if (item.exportRoot != null)
        {
            WallOpening[] openings = item.exportRoot.GetComponentsInChildren<WallOpening>(true);
            openingCount = openings != null ? openings.Length : 0;
        }

        int sharedRoomCount = CountAssignedRooms(item.wallIds, currentRoom);
        StringBuilder builder = new StringBuilder();
        builder.Append("IDs ");
        builder.Append(item.wallIds.Count);
        builder.Append(" | Len ");
        builder.Append(totalLength.ToString("0.#"));
        builder.Append(" | Openings ");
        builder.Append(openingCount);
        if (sharedRoomCount > 0)
        {
            builder.Append(" | Shared ");
            builder.Append(sharedRoomCount);
        }

        return builder.ToString();
    }

    private int CountAssignedRooms(List<string> wallIds, Room ignoredRoom)
    {
        if (roomManager == null || wallIds == null || wallIds.Count == 0)
        {
            return 0;
        }

        int count = 0;
        List<Room> rooms = roomManager.GetAllRooms();
        for (int i = 0; i < rooms.Count; i++)
        {
            Room room = rooms[i];
            if (room == null || room == ignoredRoom)
            {
                continue;
            }

            IReadOnlyList<string> effectiveIds = room.EffectiveWallIds;
            if (effectiveIds == null || effectiveIds.Count == 0)
            {
                continue;
            }

            for (int j = 0; j < wallIds.Count; j++)
            {
                if (ContainsWallId(effectiveIds, wallIds[j]))
                {
                    count++;
                    break;
                }
            }
        }

        return count;
    }

    private void HandleWallToggleChanged(WallListItem item, bool value)
    {
        if (suppressToggleCallbacks || item == null)
        {
            return;
        }

        for (int i = 0; i < item.wallIds.Count; i++)
        {
            string wallId = item.wallIds[i];
            if (string.IsNullOrWhiteSpace(wallId))
            {
                continue;
            }

            if (value)
            {
                selectedWallIds.Add(wallId);
            }
            else
            {
                selectedWallIds.Remove(wallId);
            }
        }

        if (currentRoom != null && roomManager != null)
        {
            roomManager.UpdateRoomWallSelection(currentRoom, selectedWallIds, true);
        }

        UpdateStatusText();

        if (item.visualState != null)
        {
            item.visualState.isSelected = HasAnyIdSelected(item.wallIds);
            ApplyToggleVisualState(item.visualState);
        }

        NotifyHighlightStateChanged();
    }

    private void HandleWallToggleHoverChanged(WallListItem item, bool isHovered)
    {
        if (item == null)
        {
            return;
        }

        for (int i = 0; i < item.walls.Count; i++)
        {
            Wall wall = item.walls[i];
            if (wall == null)
            {
                continue;
            }

            string wallId = wall.Data != null ? wall.Data.id : null;
            if (isHovered)
            {
                if (!string.IsNullOrWhiteSpace(wallId))
                {
                    hoveredWallIds.Add(wallId);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(wallId))
                {
                    hoveredWallIds.Remove(wallId);
                }
            }
        }

        if (item.visualState != null)
        {
            item.visualState.isHovered = isHovered;
            ApplyToggleVisualState(item.visualState);
        }

        NotifyHighlightStateChanged();
    }

    private void RefreshToggleStates()
    {
        suppressToggleCallbacks = true;
        try
        {
            for (int i = 0; i < wallItems.Count; i++)
            {
                WallListItem item = wallItems[i];
                if (item.toggle == null)
                {
                    continue;
                }

                bool isSelected = HasAnyIdSelected(item.wallIds);
                item.toggle.SetIsOnWithoutNotify(isSelected);
                if (item.visualState != null)
                {
                    item.visualState.isSelected = isSelected;
                    ApplyToggleVisualState(item.visualState);
                }
            }
        }
        finally
        {
            suppressToggleCallbacks = false;
        }
    }

    private bool HasAnyIdSelected(List<string> wallIds)
    {
        if (wallIds == null || wallIds.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < wallIds.Count; i++)
        {
            if (selectedWallIds.Contains(wallIds[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateHighlights()
    {
        hoveredWallIds.RemoveWhere(string.IsNullOrWhiteSpace);
    }

    public bool IsWallSelectedForAuthoring(Wall wall)
    {
        return IsWallTrackedByIds(wall, selectedWallIds);
    }

    public bool IsWallHoveredForAuthoring(Wall wall)
    {
        return IsWallTrackedByIds(wall, hoveredWallIds);
    }

    public bool HasSelectedRoomForAuthoring => currentRoom != null;

    public bool TryToggleWallSelectionFromWall(Wall wall)
    {
        if (currentRoom == null || wall == null)
        {
            return false;
        }

        WallListItem item = FindWallItemForWall(wall);
        if (item == null)
        {
            return false;
        }

        bool nextValue = !HasAnyIdSelected(item.wallIds);
        if (item.toggle != null)
        {
            item.toggle.SetIsOnWithoutNotify(nextValue);
        }

        HandleWallToggleChanged(item, nextValue);
        return true;
    }

    private void UpdatePanelState()
    {
        bool hasRoom = currentRoom != null;
        if (headerText != null)
        {
            headerText.text = hasRoom
                ? $"Room Wall Selection - {(string.IsNullOrWhiteSpace(currentRoom.RoomName) ? currentRoom.name : currentRoom.RoomName)}"
                : "Room Wall Selection";
        }

        if (autoAssignButton != null)
        {
            autoAssignButton.interactable = hasRoom;
        }

        if (resetButton != null)
        {
            resetButton.interactable = hasRoom && currentRoom.ManualWallSelectionEnabled;
        }

        if (applyButton != null)
        {
            applyButton.interactable = hasRoom;
        }

        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (statusText == null)
        {
            return;
        }

        if (currentRoom == null)
        {
            statusText.text = "Select a room in RoomCreate mode to author wall ownership.";
            return;
        }

        statusText.text = currentRoom.ManualWallSelectionEnabled
            ? $"Manual selection active. Selected roots: {CountSelectedRoots()} / {wallItems.Count}"
            : $"Automatic selection preview. Selected roots: {CountSelectedRoots()} / {wallItems.Count}";
    }

    private int CountSelectedRoots()
    {
        int count = 0;
        for (int i = 0; i < wallItems.Count; i++)
        {
            if (HasAnyIdSelected(wallItems[i].wallIds))
            {
                count++;
            }
        }

        return count;
    }

    private static bool ContainsWallId(IReadOnlyList<string> ids, string candidate)
    {
        if (ids == null || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private WallListItem FindWallItemForWall(Wall wall)
    {
        if (wall == null)
        {
            return null;
        }

        string wallId = wall.Data != null ? wall.Data.id : null;
        if (string.IsNullOrWhiteSpace(wallId))
        {
            return null;
        }

        for (int i = 0; i < wallItems.Count; i++)
        {
            WallListItem item = wallItems[i];
            if (item == null)
            {
                continue;
            }

            if (ContainsWallId(item.wallIds, wallId))
            {
                return item;
            }
        }

        return null;
    }

    private void ClearWallItems()
    {
        wallItems.Clear();
        if (wallToggleContainer == null)
        {
            return;
        }

        destroyBuffer.Clear();
        for (int i = 0; i < wallToggleContainer.childCount; i++)
        {
            Transform child = wallToggleContainer.GetChild(i);
            if (child == null || child == wallToggleTemplate?.transform)
            {
                continue;
            }

            destroyBuffer.Add(child);
        }

        for (int i = 0; i < destroyBuffer.Count; i++)
        {
            Destroy(destroyBuffer[i].gameObject);
        }
    }

    private Transform GetWallExportRoot(Transform wallTransform)
    {
        if (wallTransform == null)
        {
            return null;
        }

        WallOpeningContainer container = wallTransform.GetComponentInParent<WallOpeningContainer>();
        return container != null ? container.transform : wallTransform;
    }

    private ToggleVisualState BuildToggleVisualState(Toggle toggle, Text[] texts)
    {
        if (toggle == null)
        {
            return null;
        }

        ToggleVisualState state = new ToggleVisualState
        {
            background = toggle.targetGraphic as Image,
            primaryLabel = texts != null && texts.Length > 0 ? texts[0] : null,
            secondaryLabel = texts != null && texts.Length > 1 ? texts[1] : null,
        };

        Transform accentTransform = toggle.transform.Find("AccentBar");
        if (accentTransform == null)
        {
            GameObject accentObject = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
            accentTransform = accentObject.transform;
            accentTransform.SetParent(toggle.transform, false);

            RectTransform accentRect = accentObject.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(6f, 0f);
            accentRect.SetAsFirstSibling();
        }

        state.accentBar = accentTransform.GetComponent<Image>();
        return state;
    }

    private bool IsWallTrackedByIds(Wall wall, HashSet<string> ids)
    {
        if (wall == null || ids == null || ids.Count == 0)
        {
            return false;
        }

        string wallId = wall.Data != null ? wall.Data.id : null;
        if (!string.IsNullOrWhiteSpace(wallId) && ids.Contains(wallId))
        {
            return true;
        }

        WallListItem item = FindWallItemForWall(wall);
        if (item == null || item.wallIds == null)
        {
            return false;
        }

        for (int i = 0; i < item.wallIds.Count; i++)
        {
            string itemWallId = item.wallIds[i];
            if (!string.IsNullOrWhiteSpace(itemWallId) && ids.Contains(itemWallId))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyToggleVisualState(ToggleVisualState state)
    {
        if (state == null)
        {
            return;
        }

        Color backgroundColor = toggleNormalBackgroundColor;
        Color textColor = toggleNormalTextColor;
        Color metaColor = toggleNormalMetaColor;
        Color accentColor = Color.clear;
        float preferredHeight = toggleNormalHeight;

        if (state.isSelected)
        {
            backgroundColor = toggleSelectedBackgroundColor;
            textColor = toggleSelectedTextColor;
            metaColor = toggleSelectedMetaColor;
            accentColor = toggleSelectedAccentColor;
            preferredHeight = toggleSelectedHeight;
        }
        else if (state.isHovered)
        {
            backgroundColor = toggleHoverBackgroundColor;
            textColor = toggleHoverTextColor;
            metaColor = toggleHoverMetaColor;
            accentColor = toggleHoverAccentColor;
            preferredHeight = toggleHoverHeight;
        }

        if (state.background != null)
        {
            state.background.color = backgroundColor;

            LayoutElement layoutElement = state.background.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = state.background.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = preferredHeight;
            layoutElement.minHeight = preferredHeight;
        }

        if (state.primaryLabel != null)
        {
            state.primaryLabel.color = textColor;
            state.primaryLabel.fontStyle = state.isSelected || state.isHovered ? FontStyle.Bold : FontStyle.Normal;
        }

        if (state.secondaryLabel != null)
        {
            state.secondaryLabel.color = metaColor;
            state.secondaryLabel.fontStyle = state.isSelected ? FontStyle.Bold : FontStyle.Normal;
        }

        if (state.accentBar != null)
        {
            state.accentBar.enabled = state.isSelected || state.isHovered;
            state.accentBar.color = accentColor;
        }
    }

    private void NotifyHighlightStateChanged()
    {
        HighlightStateChanged?.Invoke();
    }

    private string GetMenuRootName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallMenuRootName : null, menuRootName);
    private string GetWallScrollViewName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallScrollViewName : null, wallScrollViewName);
    private string GetWallToggleContainerName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallToggleContainerName : null, wallToggleContainerName);
    private string GetWallToggleTemplateName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallToggleTemplateName : null, wallToggleTemplateName);
    private string GetHeaderTextName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallHeaderTextName : null, headerTextName);
    private string GetStatusTextName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallStatusTextName : null, statusTextName);
    private string GetAutoAssignButtonName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallAutoAssignButtonName : null, autoAssignButtonName);
    private string GetResetButtonName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallResetButtonName : null, resetButtonName);
    private string GetApplyButtonName() => GetConfiguredName(uiReferenceSettings != null ? uiReferenceSettings.roomWallApplyButtonName : null, applyButtonName);

    private static string GetConfiguredName(string configuredValue, string fallback)
    {
        return string.IsNullOrWhiteSpace(configuredValue) ? fallback : configuredValue;
    }
}
