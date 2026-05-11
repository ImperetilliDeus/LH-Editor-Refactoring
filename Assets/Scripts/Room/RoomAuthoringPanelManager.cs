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
    [SerializeField] private RoomTypePreset roomTypePreset;
    [SerializeField] private string roomTypeJsonFileName = RoomTypeCatalog.DefaultJsonFileName;
    [SerializeField] private TMP_Dropdown roomTypeDropdown;
    [SerializeField] private GameObject roomEditMenu;
    [SerializeField] private InputField roomNameInputField;
    [SerializeField] private InputField roomCodeInputField;
    [SerializeField] private InputField roomNativeCodeInputField;
    [SerializeField] private InputField floorTextureCodeInputField;
    [SerializeField] private InputField ceilingTextureCodeInputField;
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
    private bool isRoomAuthoringModeActive;

    public Room SelectedRoom => selectedRoom;
    public event System.Action<Room> SelectedRoomChanged;

    private void Awake()
    {
        ResolveReferences();
        PopulateRoomTypeDropdown();
        BindEvents();
        SyncModeState();
        RefreshDropdownState();
        RefreshNameField();
        RefreshMetadataFields();
        RefreshAreaField();
        ValidateConfiguration();
    }

    private void OnDestroy()
    {
        SetSelectedRoomInternal(null);
        UnbindEvents();
        ClearAllLabels();
    }

    private void Update()
    {
        if (labelsDirty || HasTopViewCameraStateChanged())
        {
            RefreshRoomTypeLabels();
            labelsDirty = false;
        }

        CacheTopViewCameraState();
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref modeManager);
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref roomHandleManager);
        LayerUtility.ResolveObject(ref topViewRenderManager);
    }

    private void BindEvents()
    {
        if (modeManager != null)
        {
            modeManager.ModeChanged -= HandleModeChanged;
            modeManager.ModeChanged += HandleModeChanged;
        }

        if (roomHandleManager != null)
        {
            roomHandleManager.FocusedRoomChanged -= HandleFocusedRoomChanged;
            roomHandleManager.FocusedRoomChanged += HandleFocusedRoomChanged;
        }

        if (roomTypeDropdown != null)
        {
            roomTypeDropdown.onValueChanged.AddListener(HandleRoomTypeDropdownChanged);
        }

        if (roomNameInputField != null)
        {
            roomNameInputField.onValueChanged.AddListener(HandleRoomNameChanged);
        }

        if (roomCodeInputField != null)
        {
            roomCodeInputField.onValueChanged.AddListener(HandleRoomCodeChanged);
        }

        if (roomNativeCodeInputField != null)
        {
            roomNativeCodeInputField.onValueChanged.AddListener(HandleRoomNativeCodeChanged);
        }

        if (floorTextureCodeInputField != null)
        {
            floorTextureCodeInputField.onValueChanged.AddListener(HandleFloorTextureCodeChanged);
        }

        if (ceilingTextureCodeInputField != null)
        {
            ceilingTextureCodeInputField.onValueChanged.AddListener(HandleCeilingTextureCodeChanged);
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

        if (roomHandleManager != null)
        {
            roomHandleManager.FocusedRoomChanged -= HandleFocusedRoomChanged;
        }

        if (roomTypeDropdown != null)
        {
            roomTypeDropdown.onValueChanged.RemoveListener(HandleRoomTypeDropdownChanged);
        }

        if (roomNameInputField != null)
        {
            roomNameInputField.onValueChanged.RemoveListener(HandleRoomNameChanged);
        }

        if (roomCodeInputField != null)
        {
            roomCodeInputField.onValueChanged.RemoveListener(HandleRoomCodeChanged);
        }

        if (roomNativeCodeInputField != null)
        {
            roomNativeCodeInputField.onValueChanged.RemoveListener(HandleRoomNativeCodeChanged);
        }

        if (floorTextureCodeInputField != null)
        {
            floorTextureCodeInputField.onValueChanged.RemoveListener(HandleFloorTextureCodeChanged);
        }

        if (ceilingTextureCodeInputField != null)
        {
            ceilingTextureCodeInputField.onValueChanged.RemoveListener(HandleCeilingTextureCodeChanged);
        }

        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleRoomsChanged;
        }
    }

    private void HandleModeChanged(EditorMode mode)
    {
        isRoomAuthoringModeActive = mode == EditorMode.RoomCreate;
        if (!isRoomAuthoringModeActive)
        {
            SetSelectedRoomInternal(null);
            roomHandleManager?.ClearFocusedRoom();
        }

        RefreshDropdownState();
        RefreshNameField();
        RefreshMetadataFields();
        RefreshAreaField();
        labelsDirty = true;
        UpdateRoomEditMenuState();
    }

    private void HandleFocusedRoomChanged(Room room)
    {
        if (!isRoomAuthoringModeActive && room != null)
        {
            return;
        }

        if (selectedRoom == room)
        {
            return;
        }

        SetSelectedRoomInternal(room);
        RefreshDropdownState();
        RefreshNameField();
        RefreshMetadataFields();
        RefreshAreaField();
        UpdateRoomEditMenuState();
    }

    private void HandleRoomsChanged()
    {
        RefreshNameField();
        RefreshMetadataFields();
        RefreshAreaField();
        labelsDirty = true;
        UpdateRoomEditMenuState();
    }

    private bool IsRoomAuthoringMode()
    {
        return isRoomAuthoringModeActive;
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
        SelectedRoomChanged?.Invoke(selectedRoom);
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

    private void PopulateRoomTypeDropdown()
    {
        if (roomTypeDropdown == null)
        {
            return;
        }

        IReadOnlyList<RoomTypeCatalog.Entry> roomTypes = RoomTypeCatalog.LoadEntries(roomTypePreset, roomTypeJsonFileName);
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>(roomTypes.Count);

        for (int i = 0; i < roomTypes.Count; i++)
        {
            RoomTypeCatalog.Entry roomType = roomTypes[i];
            if (string.IsNullOrWhiteSpace(roomType.Name))
            {
                continue;
            }

            options.Add(new TMP_Dropdown.OptionData(roomType.Name));
        }

        roomTypeDropdown.ClearOptions();
        roomTypeDropdown.AddOptions(options);
        roomTypeDropdown.RefreshShownValue();
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

    private void RefreshNameField()
    {
        if (roomNameInputField == null)
        {
            return;
        }

        bool canEdit = IsRoomAuthoringMode() && selectedRoom != null;
        string nextText = canEdit ? selectedRoom.RoomName : string.Empty;

        if (!roomNameInputField.isFocused && roomNameInputField.text != nextText)
        {
            roomNameInputField.SetTextWithoutNotify(nextText);
        }

        if (roomNameInputField.interactable != canEdit)
        {
            roomNameInputField.interactable = canEdit;
        }

        if (roomNameInputField.readOnly == canEdit)
        {
            roomNameInputField.readOnly = !canEdit;
        }
    }

    private void RefreshMetadataFields()
    {
        RefreshMetadataField(roomCodeInputField, selectedRoom != null ? selectedRoom.RoomCode : string.Empty);
        RefreshMetadataField(roomNativeCodeInputField, selectedRoom != null ? selectedRoom.RoomNativeCode : string.Empty);
        RefreshMetadataField(
            floorTextureCodeInputField,
            selectedRoom != null && roomManager != null
                ? roomManager.GetEffectiveFloorTextureCode(selectedRoom)
                : string.Empty);
        RefreshMetadataField(
            ceilingTextureCodeInputField,
            selectedRoom != null && roomManager != null
                ? roomManager.GetEffectiveCeilingTextureCode(selectedRoom)
                : string.Empty);
    }

    private void RefreshMetadataField(InputField inputField, string value)
    {
        if (inputField == null)
        {
            return;
        }

        bool canEdit = IsRoomAuthoringMode() && selectedRoom != null;
        string nextText = canEdit ? value ?? string.Empty : string.Empty;

        if (!inputField.isFocused && inputField.text != nextText)
        {
            inputField.SetTextWithoutNotify(nextText);
        }

        if (inputField.interactable != canEdit)
        {
            inputField.interactable = canEdit;
        }

        if (inputField.readOnly == canEdit)
        {
            inputField.readOnly = !canEdit;
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
        string roomCode = selectedRoom.RoomCode;
        if (RoomTypeCatalog.TryGetCode(typeKey, out int resolvedCode, roomTypePreset, roomTypeJsonFileName))
        {
            roomCode = resolvedCode.ToString();
        }

        if (roomManager != null)
        {
            roomManager.UpdateRoomMetadata(
                selectedRoom,
                selectedRoom.RoomName,
                typeKey,
                roomCode,
                selectedRoom.RoomNativeCode,
                selectedRoom.FloorTextureCode,
                selectedRoom.CeilingTextureCode);
        }
    }

    private void HandleRoomNameChanged(string roomName)
    {
        if (selectedRoom == null || !IsRoomAuthoringMode())
        {
            return;
        }

        if (roomManager != null)
        {
            roomManager.UpdateRoomMetadata(
                selectedRoom,
                roomName,
                selectedRoom.RoomTypeKey,
                selectedRoom.RoomCode,
                selectedRoom.RoomNativeCode,
                selectedRoom.FloorTextureCode,
                selectedRoom.CeilingTextureCode);
        }
    }

    private void HandleRoomCodeChanged(string roomCode)
    {
        ApplyMetadataChanges(roomCode, selectedRoom != null ? selectedRoom.RoomNativeCode : string.Empty, selectedRoom != null ? selectedRoom.FloorTextureCode : string.Empty, selectedRoom != null ? selectedRoom.CeilingTextureCode : string.Empty);
    }

    private void HandleRoomNativeCodeChanged(string roomNativeCode)
    {
        ApplyMetadataChanges(selectedRoom != null ? selectedRoom.RoomCode : string.Empty, roomNativeCode, selectedRoom != null ? selectedRoom.FloorTextureCode : string.Empty, selectedRoom != null ? selectedRoom.CeilingTextureCode : string.Empty);
    }

    private void HandleFloorTextureCodeChanged(string floorTextureCode)
    {
        ApplyMetadataChanges(selectedRoom != null ? selectedRoom.RoomCode : string.Empty, selectedRoom != null ? selectedRoom.RoomNativeCode : string.Empty, floorTextureCode, selectedRoom != null ? selectedRoom.CeilingTextureCode : string.Empty);
    }

    private void HandleCeilingTextureCodeChanged(string ceilingTextureCode)
    {
        ApplyMetadataChanges(selectedRoom != null ? selectedRoom.RoomCode : string.Empty, selectedRoom != null ? selectedRoom.RoomNativeCode : string.Empty, selectedRoom != null ? selectedRoom.FloorTextureCode : string.Empty, ceilingTextureCode);
    }

    private void ApplyMetadataChanges(string roomCode, string roomNativeCode, string floorTextureCode, string ceilingTextureCode)
    {
        if (selectedRoom == null || !IsRoomAuthoringMode() || roomManager == null)
        {
            return;
        }

        roomManager.UpdateRoomMetadata(
            selectedRoom,
            selectedRoom.RoomName,
            selectedRoom.RoomTypeKey,
            roomCode,
            roomNativeCode,
            floorTextureCode,
            ceilingTextureCode);
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
        roomManager?.GetAllRooms(cachedRooms);
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

        string labelText = BuildRoomLabelText(room);
        label.text = labelText;
        label.gameObject.SetActive(!string.IsNullOrWhiteSpace(labelText));

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

    private static string BuildRoomLabelText(Room room)
    {
        if (room == null)
        {
            return string.Empty;
        }

        string roomName = room.RoomName;
        string typeKey = room.RoomTypeKey;
        bool hasName = !string.IsNullOrWhiteSpace(roomName);
        bool hasType = !string.IsNullOrWhiteSpace(typeKey);

        if (hasName && hasType)
        {
            return $"{roomName}\n({typeKey})";
        }

        if (hasName)
        {
            return roomName;
        }

        return hasType ? typeKey : string.Empty;
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
                pair.Value.gameObject.SetActive(visible && !string.IsNullOrWhiteSpace(BuildRoomLabelText(pair.Key)));
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

    private void SyncModeState()
    {
        HandleModeChanged(modeManager != null ? modeManager.CurrentMode : EditorMode.Default);
        HandleFocusedRoomChanged(roomHandleManager != null ? roomHandleManager.FocusedRoom : null);
    }

    private void ValidateConfiguration()
    {
        Debug.Assert(modeManager != null, $"{nameof(RoomAuthoringPanelManager)} requires {nameof(modeManager)}.", this);
        Debug.Assert(roomManager != null, $"{nameof(RoomAuthoringPanelManager)} requires {nameof(roomManager)}.", this);
        Debug.Assert(roomHandleManager != null, $"{nameof(RoomAuthoringPanelManager)} requires {nameof(roomHandleManager)}.", this);
    }
}

