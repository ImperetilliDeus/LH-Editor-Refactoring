using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomAuthoringPanelManager : MonoBehaviour
{
    private const float SquareUnitsToSquareMeters = 0.01f;

    [Header("References")]
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private RoomHandleManager roomHandleManager;
    [SerializeField] private TopViewRenderManager topViewRenderManager;
    [SerializeField] private TMP_Dropdown roomTypeDropdown;
    [SerializeField] private GameObject roomEditMenu;
    [SerializeField] private InputField roomAreaInputField;

    [Header("Label")]
    [SerializeField] private TMP_Text roomTypeLabelPrefab;
    [SerializeField] private Color roomTypeLabelColor = new Color(1f, 1f, 1f, 0.55f);
    [SerializeField] private int roomTypeLabelFontSize = 26;
    [SerializeField] private Color selectedRoomHighlightColor = new Color(0.28f, 0.6f, 1f, 0.42f);

    private readonly Dictionary<Room, TMP_Text> labelsByRoom = new Dictionary<Room, TMP_Text>();
    private readonly List<TMP_Text> pooledLabels = new List<TMP_Text>();
    private readonly List<Room> cachedRooms = new List<Room>();
    private readonly List<Vector2> cachedPolygon = new List<Vector2>();
    private readonly List<Vector3> cachedRoomVertices = new List<Vector3>();
    private readonly List<Room> removeRooms = new List<Room>();

    private Room selectedRoom;
    private bool suppressDropdownEvent;
    private bool labelsDirty = true;
    private Vector3 lastTopViewCameraPosition;
    private Quaternion lastTopViewCameraRotation;
    private float lastTopViewCameraOrthoSize;

    public Room SelectedRoom => selectedRoom;

    private void Awake()
    {
        ResolveReferences();
        BindEvents();
        RefreshDropdownState();
        RefreshAreaField();
    }

    private void OnDestroy()
    {
        SetSelectedRoomInternal(null);
        UnbindEvents();
        ClearAllLabels();
    }

    private void Update()
    {
        SyncSelectionFromFocusedRoom();

        if (labelsDirty || HasTopViewCameraStateChanged())
        {
            RefreshRoomTypeLabels();
            labelsDirty = false;
        }

        CacheTopViewCameraState();

        if (!IsRoomAuthoringMode())
        {
            UpdateRoomEditMenuState();
            return;
        }
        UpdateRoomEditMenuState();
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            modeManager = FindFirstObjectByType<ModeManager>();
        }

        if (roomManager == null)
        {
            roomManager = FindFirstObjectByType<RoomManager>();
        }

        if (roomHandleManager == null)
        {
            roomHandleManager = FindFirstObjectByType<RoomHandleManager>();
        }

        if (topViewRenderManager == null)
        {
            topViewRenderManager = FindFirstObjectByType<TopViewRenderManager>();
        }
    }

    private void BindEvents()
    {
        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
            modeManager.ModeChanged += HandleModeChanged;
        }

        if (roomTypeDropdown != null)
        {
            roomTypeDropdown.onValueChanged.AddListener(HandleRoomTypeDropdownChanged);
        }

        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleRoomsChanged;
            roomManager.RoomsChanged += HandleRoomsChanged;
        }
    }

    private void UnbindEvents()
    {
        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
        }

        if (roomTypeDropdown != null)
        {
            roomTypeDropdown.onValueChanged.RemoveListener(HandleRoomTypeDropdownChanged);
        }

        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleRoomsChanged;
        }
    }

    private void HandleModeChanged(EditorMode mode)
    {
        if (mode != EditorMode.RoomCreate)
        {
            SetSelectedRoomInternal(null);
            roomHandleManager?.ClearFocusedRoom();
        }

        RefreshDropdownState();
        RefreshAreaField();
        labelsDirty = true;
        UpdateRoomEditMenuState();
    }

    private void HandleRoomsChanged()
    {
        SyncSelectionFromFocusedRoom();
        RefreshAreaField();
        labelsDirty = true;
        UpdateRoomEditMenuState();
    }

    private bool IsRoomAuthoringMode()
    {
        return modeManager != null && modeManager.CurrentMode == EditorMode.RoomCreate;
    }

    private void SyncSelectionFromFocusedRoom()
    {
        Room focusedRoom = roomHandleManager != null ? roomHandleManager.FocusedRoom : null;
        if (selectedRoom == focusedRoom)
        {
            return;
        }

        SetSelectedRoomInternal(focusedRoom);
        RefreshDropdownState();
        RefreshAreaField();
        UpdateRoomEditMenuState();
    }

    private bool TryBuildRoomPolygonInCanvas(
        Room room,
        Camera topCamera,
        RectTransform contentRoot,
        Camera uiCamera,
        List<Vector2> results)
    {
        results.Clear();
        if (!room.TryGetOrderedVertices(cachedRoomVertices) || cachedRoomVertices.Count < 3)
        {
            return false;
        }

        for (int i = 0; i < cachedRoomVertices.Count; i++)
        {
            Vector3 screen = topCamera.WorldToScreenPoint(cachedRoomVertices[i]);
            if (screen.z <= 0f)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(contentRoot, screen, uiCamera, out Vector2 localPoint))
            {
                return false;
            }

            results.Add(localPoint);
        }

        return results.Count >= 3;
    }

    private void SetSelectedRoomInternal(Room room)
    {
        if (selectedRoom == room)
        {
            return;
        }

        if (selectedRoom != null)
        {
            selectedRoom.SetSelectionState(false, selectedRoomHighlightColor);
        }

        selectedRoom = room;

        if (selectedRoom != null)
        {
            selectedRoom.SetSelectionState(true, selectedRoomHighlightColor);
        }

        topViewRenderManager?.MarkDirty();
    }

    private void UpdateRoomEditMenuState()
    {
        if (roomEditMenu == null)
        {
            return;
        }

        bool shouldBeActive = IsRoomAuthoringMode() && selectedRoom != null;
        if (roomEditMenu.activeSelf != shouldBeActive)
        {
            roomEditMenu.SetActive(shouldBeActive);
        }
    }

    private void RefreshDropdownState()
    {
        if (roomTypeDropdown == null)
        {
            RefreshAreaField();
            return;
        }

        bool canEdit = IsRoomAuthoringMode() && selectedRoom != null;
        roomTypeDropdown.interactable = canEdit;

        if (!canEdit)
        {
            return;
        }

        int optionIndex = FindOptionIndex(selectedRoom.RoomTypeKey);
        if (optionIndex < 0)
        {
            optionIndex = roomTypeDropdown.options != null && roomTypeDropdown.options.Count > 0 ? 0 : -1;
        }

        suppressDropdownEvent = true;
        if (optionIndex >= 0)
        {
            roomTypeDropdown.SetValueWithoutNotify(optionIndex);
        }

        suppressDropdownEvent = false;
        RefreshAreaField();
    }

    private void RefreshAreaField()
    {
        if (roomAreaInputField == null)
        {
            return;
        }

        bool shouldShowValue = IsRoomAuthoringMode() && selectedRoom != null;
        string nextText = shouldShowValue
            ? $"{selectedRoom.Geometry.Area * SquareUnitsToSquareMeters:0.##} m²"
            : string.Empty;

        if (roomAreaInputField.text != nextText)
        {
            roomAreaInputField.SetTextWithoutNotify(nextText);
        }

        if (roomAreaInputField.interactable)
        {
            roomAreaInputField.interactable = false;
        }

        if (roomAreaInputField.readOnly == false)
        {
            roomAreaInputField.readOnly = true;
        }
    }

    private int FindOptionIndex(string typeKey)
    {
        if (roomTypeDropdown == null || roomTypeDropdown.options == null)
        {
            return -1;
        }

        for (int i = 0; i < roomTypeDropdown.options.Count; i++)
        {
            TMP_Dropdown.OptionData option = roomTypeDropdown.options[i];
            string optionText = option != null ? option.text : string.Empty;
            if (string.Equals(optionText, typeKey, System.StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void HandleRoomTypeDropdownChanged(int optionIndex)
    {
        if (suppressDropdownEvent)
        {
            return;
        }

        ApplySelectedRoomType(optionIndex);
    }

    private void ApplySelectedRoomType(int optionIndex)
    {
        if (selectedRoom == null || roomTypeDropdown == null || roomTypeDropdown.options == null)
        {
            return;
        }

        if (optionIndex < 0 || optionIndex >= roomTypeDropdown.options.Count)
        {
            return;
        }

        TMP_Dropdown.OptionData option = roomTypeDropdown.options[optionIndex];
        string typeKey = option != null ? option.text ?? string.Empty : string.Empty;
        selectedRoom.SetRoomTypeKey(typeKey);
        RefreshLabelForRoom(selectedRoom);
        labelsDirty = true;
    }

    private void RefreshRoomTypeLabels()
    {
        if (!ShouldShowRoomLabels())
        {
            SetLabelsVisible(false);
            return;
        }

        SetLabelsVisible(true);

        cachedRooms.Clear();
        cachedRooms.AddRange(roomManager != null ? roomManager.GetAllRooms() : new List<Room>());
        removeRooms.Clear();

        foreach (KeyValuePair<Room, TMP_Text> pair in labelsByRoom)
        {
            if (pair.Key == null || !cachedRooms.Contains(pair.Key))
            {
                removeRooms.Add(pair.Key);
            }
        }

        for (int i = 0; i < removeRooms.Count; i++)
        {
            Room removeRoom = removeRooms[i];
            if (labelsByRoom.TryGetValue(removeRoom, out TMP_Text removeText) && removeText != null)
            {
                ReleaseLabel(removeText);
            }

            labelsByRoom.Remove(removeRoom);
        }

        for (int i = 0; i < cachedRooms.Count; i++)
        {
            RefreshLabelForRoom(cachedRooms[i]);
        }
    }

    private void RefreshLabelForRoom(Room room)
    {
        if (room == null)
        {
            return;
        }

        TMP_Text label = GetOrCreateLabel(room);
        if (label == null)
        {
            return;
        }

        string typeKey = room.RoomTypeKey;
        label.text = typeKey;
        label.gameObject.SetActive(!string.IsNullOrWhiteSpace(typeKey));

        RectTransform contentRoot = topViewRenderManager != null ? topViewRenderManager.ContentRoot : null;
        Camera topCamera = topViewRenderManager != null ? topViewRenderManager.TopViewCamera : null;
        Canvas targetCanvas = topViewRenderManager != null ? topViewRenderManager.TargetCanvas : null;

        if (contentRoot == null || topCamera == null)
        {
            return;
        }

        Camera uiCamera = targetCanvas != null && targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCanvas != null ? targetCanvas.worldCamera : null;

        RectTransform labelRect = label.rectTransform;
        if (!TryBuildRoomPolygonInCanvas(room, topCamera, contentRoot, uiCamera, cachedPolygon))
        {
            label.gameObject.SetActive(false);
            return;
        }

        if (TryCalculatePolygonCentroid(cachedPolygon, out Vector2 centroid))
        {
            labelRect.anchoredPosition = centroid;
        }
    }

    private bool ShouldShowRoomLabels()
    {
        RectTransform contentRoot = topViewRenderManager != null ? topViewRenderManager.ContentRoot : null;
        return contentRoot != null && contentRoot.gameObject.activeInHierarchy;
    }

    private static bool TryCalculatePolygonCentroid(List<Vector2> polygon, out Vector2 centroid)
    {
        centroid = Vector2.zero;
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        float signedAreaTwice = 0f;
        float centroidX = 0f;
        float centroidY = 0f;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 current = polygon[i];
            Vector2 next = polygon[(i + 1) % polygon.Count];
            float cross = current.x * next.y - next.x * current.y;
            signedAreaTwice += cross;
            centroidX += (current.x + next.x) * cross;
            centroidY += (current.y + next.y) * cross;
        }

        if (Mathf.Abs(signedAreaTwice) <= 0.000001f)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < polygon.Count; i++)
            {
                sum += polygon[i];
            }

            centroid = sum / polygon.Count;
            return true;
        }

        float factor = 1f / (3f * signedAreaTwice);
        centroid = new Vector2(centroidX * factor, centroidY * factor);
        return true;
    }

    private TMP_Text GetOrCreateLabel(Room room)
    {
        if (labelsByRoom.TryGetValue(room, out TMP_Text existing) && existing != null)
        {
            return existing;
        }

        RectTransform contentRoot = topViewRenderManager != null ? topViewRenderManager.ContentRoot : null;
        if (contentRoot == null)
        {
            return null;
        }

        TMP_Text label;
        if (pooledLabels.Count > 0)
        {
            int lastIndex = pooledLabels.Count - 1;
            label = pooledLabels[lastIndex];
            pooledLabels.RemoveAt(lastIndex);
            label.transform.SetParent(contentRoot, false);
            label.gameObject.SetActive(true);
        }
        else if (roomTypeLabelPrefab != null)
        {
            label = Instantiate(roomTypeLabelPrefab, contentRoot);
        }
        else
        {
            GameObject labelObject = new GameObject("RoomTypeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(contentRoot, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }

        if (label == null)
        {
            return null;
        }

        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = roomTypeLabelFontSize;
        label.color = roomTypeLabelColor;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(220f, 48f);

        labelsByRoom[room] = label;
        return label;
    }

    private void SetLabelsVisible(bool visible)
    {
        foreach (KeyValuePair<Room, TMP_Text> pair in labelsByRoom)
        {
            if (pair.Value != null)
            {
                pair.Value.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(pair.Key != null ? pair.Key.RoomTypeKey : string.Empty));
            }
        }
    }

    private void ClearAllLabels()
    {
        foreach (KeyValuePair<Room, TMP_Text> pair in labelsByRoom)
        {
            if (pair.Value != null)
            {
                ReleaseLabel(pair.Value);
            }
        }

        labelsByRoom.Clear();

        for (int i = 0; i < pooledLabels.Count; i++)
        {
            if (pooledLabels[i] != null)
            {
                Destroy(pooledLabels[i].gameObject);
            }
        }

        pooledLabels.Clear();
    }

    private void ReleaseLabel(TMP_Text label)
    {
        if (label == null)
        {
            return;
        }

        label.text = string.Empty;
        label.gameObject.SetActive(false);
        pooledLabels.Add(label);
    }

    private bool HasTopViewCameraStateChanged()
    {
        Camera topCamera = topViewRenderManager != null ? topViewRenderManager.TopViewCamera : null;
        if (topCamera == null)
        {
            return false;
        }

        Transform cameraTransform = topCamera.transform;
        return cameraTransform.position != lastTopViewCameraPosition ||
               cameraTransform.rotation != lastTopViewCameraRotation ||
               !Mathf.Approximately(topCamera.orthographicSize, lastTopViewCameraOrthoSize);
    }

    private void CacheTopViewCameraState()
    {
        Camera topCamera = topViewRenderManager != null ? topViewRenderManager.TopViewCamera : null;
        if (topCamera == null)
        {
            return;
        }

        Transform cameraTransform = topCamera.transform;
        lastTopViewCameraPosition = cameraTransform.position;
        lastTopViewCameraRotation = cameraTransform.rotation;
        lastTopViewCameraOrthoSize = topCamera.orthographicSize;
    }
}

