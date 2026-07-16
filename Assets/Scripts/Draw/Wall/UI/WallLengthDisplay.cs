using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

public class WallLengthDisplay : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private ModeManager modeManager;

    [Header("Style")]
    [SerializeField] private Font labelFont;

    [SerializeField] private float labelHeightOffset = 28f;
    [SerializeField] private float labelScale = 0.2f;
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private Color previewTextColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private int fontSize = 18;
    [SerializeField] private Vector2 labelSize = new Vector2(220f, 40f);
    [SerializeField] private float labelScreenPadding = 14f;

    private const string CanvasName = "WallLengthCanvas";

    private Camera targetCamera;

    public event Action<Transform> LengthLabelClicked;

    private class LabelEntry
    {
        public Transform wallTransform;
        public RectTransform labelRect;
        public Text labelText;
        public WallLengthLabelClickHandler clickHandler;
        public float wallHeight;
        public bool isPreview;
    }

    private readonly Dictionary<int, LabelEntry> labelEntries = new Dictionary<int, LabelEntry>();
    private readonly List<int> removedLabelKeys = new List<int>();
    private bool labelPositionsDirty = true;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private float lastCameraOrthoSize;
    private Vector4 lastViewportSignature;

    private void OnValidate()
    {
        labelHeightOffset = Mathf.Max(0f, labelHeightOffset);
        labelScale = Mathf.Max(0.01f, labelScale);
        fontSize = Mathf.Max(8, fontSize);
        labelSize.x = Mathf.Max(40f, labelSize.x);
        labelSize.y = Mathf.Max(20f, labelSize.y);
        labelScreenPadding = Mathf.Max(0f, labelScreenPadding);
    }

    private void Awake()
    {
        ResolveReferences();
    }

    public void SetWallLength(Transform wallTransform, float wallLengthUnits, float wallHeight, bool isPreview)
    {
        if (wallTransform == null)
        {
            return;
        }

        Wall wallComponent = wallTransform.GetComponent<Wall>();
        if (wallComponent != null && WallHierarchyUtility.IsHiddenOpeningBaseSegment(wallComponent))
        {
            RemoveWallLabel(wallTransform);
            return;
        }

        if (!wallTransform.gameObject.activeInHierarchy)
        {
            RemoveWallLabel(wallTransform);
            return;
        }

        LabelEntry entry = GetOrCreateEntry(wallTransform);
        if (entry == null)
        {
            return;
        }

        entry.wallHeight = wallHeight;
        entry.isPreview = isPreview;
        entry.labelText.text = FormatLength(wallLengthUnits);
        entry.labelText.color = isPreview ? previewTextColor : textColor;
        entry.labelText.fontSize = fontSize;
        entry.labelRect.sizeDelta = labelSize;
        entry.labelRect.localScale = Vector3.one * labelScale;
        entry.labelText.enabled = true;
        ApplyLabelInteractionState(entry);

        labelPositionsDirty = true;
    }

    public void RemoveWallLabel(Transform wallTransform)
    {
        if (wallTransform == null)
        {
            return;
        }

        int key = wallTransform.GetInstanceID();
        if (!labelEntries.TryGetValue(key, out LabelEntry entry))
        {
            return;
        }

        if (entry.labelRect != null)
        {
            DestroyLabelObject(entry.labelRect.gameObject);
        }

        labelEntries.Remove(key);
        labelPositionsDirty = true;
    }

    public void ClearAllLabels()
    {
        foreach (KeyValuePair<int, LabelEntry> pair in labelEntries)
        {
            LabelEntry entry = pair.Value;
            if (entry != null && entry.labelRect != null)
            {
                DestroyLabelObject(entry.labelRect.gameObject);
            }
        }

        labelEntries.Clear();
        labelPositionsDirty = true;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }
        }

        if (labelEntries.Count == 0)
        {
            CacheCameraState();
            return;
        }

        RefreshLabelInteractionStates();

        bool cameraChanged = HasCameraStateChanged();
        bool viewportChanged = HasViewportChanged();
        if (!labelPositionsDirty && !cameraChanged && !viewportChanged)
        {
            return;
        }

        removedLabelKeys.Clear();

        foreach (KeyValuePair<int, LabelEntry> pair in labelEntries)
        {
            LabelEntry entry = pair.Value;
            if (entry == null || entry.wallTransform == null || entry.labelRect == null || entry.labelText == null)
            {
                if (entry != null && entry.labelRect != null)
                {
                    DestroyLabelObject(entry.labelRect.gameObject);
                }

                removedLabelKeys.Add(pair.Key);
                continue;
            }

            bool isVisible = TryGetLabelScreenPosition(entry, out Vector3 screenPosition);

            if (entry.labelText.enabled != isVisible)
            {
                entry.labelText.enabled = isVisible;
            }

            if (!isVisible)
            {
                continue;
            }

            SetLabelRectPosition(entry.labelRect, screenPosition);
        }

        for (int i = 0; i < removedLabelKeys.Count; i++)
        {
            labelEntries.Remove(removedLabelKeys[i]);
        }

        labelPositionsDirty = false;
        CacheCameraState();
        CacheViewportState();
    }

    private LabelEntry GetOrCreateEntry(Transform wallTransform)
    {
        int key = wallTransform.GetInstanceID();
        if (labelEntries.TryGetValue(key, out LabelEntry existingEntry) && existingEntry != null && existingEntry.labelRect != null && existingEntry.labelText != null)
        {
            existingEntry.wallTransform = wallTransform;
            if (existingEntry.clickHandler != null)
            {
                existingEntry.clickHandler.Initialize(this, wallTransform);
            }

            return existingEntry;
        }

        Canvas canvas = GetOrCreateCanvas();
        if (canvas == null)
        {
            return null;
        }

        string labelName = GetLabelObjectName(wallTransform);
        Transform existingLabel = canvas.transform.Find(labelName);
        if (existingLabel == null)
        {
            GameObject labelObject = new GameObject(labelName, typeof(RectTransform));
            existingLabel = labelObject.transform;
            existingLabel.SetParent(canvas.transform, false);
        }

        if (existingLabel == null)
        {
            return null;
        }

        RectTransform labelRect = existingLabel as RectTransform;
        if (labelRect == null)
        {
            GameObject replacement = new GameObject(labelName, typeof(RectTransform));
            replacement.transform.SetParent(canvas.transform, false);
            if (existingLabel != null)
            {
                DestroyLabelObject(existingLabel.gameObject);
            }

            labelRect = replacement.GetComponent<RectTransform>();
            existingLabel = replacement.transform;
        }

        Text lengthText = existingLabel.GetComponent<Text>();
        if (lengthText == null)
        {
            lengthText = existingLabel.gameObject.AddComponent<Text>();
        }

        if (labelFont == null)
        {
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (labelFont != null)
        {
            lengthText.font = labelFont;
        }

        lengthText.raycastTarget = true;
        lengthText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lengthText.verticalOverflow = VerticalWrapMode.Overflow;
        lengthText.alignment = TextAnchor.MiddleCenter;
        lengthText.fontSize = fontSize;
        lengthText.color = textColor;

        WallLengthLabelClickHandler clickHandler = existingLabel.GetComponent<WallLengthLabelClickHandler>();
        if (clickHandler == null)
        {
            clickHandler = existingLabel.gameObject.AddComponent<WallLengthLabelClickHandler>();
        }

        clickHandler.Initialize(this, wallTransform);

        labelRect.sizeDelta = labelSize;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);

        LabelEntry newEntry = new LabelEntry
        {
            wallTransform = wallTransform,
            labelRect = labelRect,
            labelText = lengthText,
            clickHandler = clickHandler,
            wallHeight = 0f,
            isPreview = false,
        };

        labelEntries[key] = newEntry;
        labelPositionsDirty = true;
        return newEntry;
    }

    private Canvas GetOrCreateCanvas()
    {
        if (targetCanvas != null)
        {
            EnsureCanvasInteractionComponents(targetCanvas);
            return targetCanvas;
        }

        Canvas preferredCanvas = LayerUtility.FindCanvasByNameOrFirst(LayerUtility.DefaultCanvasName);
        if (preferredCanvas != null)
        {
            targetCanvas = preferredCanvas;
            EnsureCanvasInteractionComponents(preferredCanvas);
            return preferredCanvas;
        }

        Transform canvasTransform = LayerUtility.FindTransformByName(CanvasName, true);
        GameObject canvasObject = canvasTransform != null ? canvasTransform.gameObject : null;
        if (canvasObject == null)
        {
            canvasObject = new GameObject(CanvasName);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
            targetCanvas = canvas;
            return canvas;
        }

        Canvas existingCanvas = canvasObject.GetComponent<Canvas>();
        if (existingCanvas == null)
        {
            existingCanvas = canvasObject.AddComponent<Canvas>();
            existingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        targetCanvas = existingCanvas;
        EnsureCanvasInteractionComponents(existingCanvas);
        return existingCanvas;
    }

    private static void EnsureCanvasInteractionComponents(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        GameObject canvasObject = canvas.gameObject;
        if (canvasObject.GetComponent<CanvasScaler>() == null)
        {
            canvasObject.AddComponent<CanvasScaler>();
        }

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }
    }

    private string GetLabelObjectName(Transform wallTransform)
    {
        return $"LengthLabel_{wallTransform.GetInstanceID()}";
    }

    private bool TryGetLabelScreenPosition(LabelEntry entry, out Vector3 screenPosition)
    {
        screenPosition = Vector3.zero;
        if (entry == null || entry.wallTransform == null || targetCamera == null)
        {
            return false;
        }

        Wall wall = entry.wallTransform.GetComponent<Wall>();
        if (wall == null)
        {
            Vector3 fallbackWorldPosition = entry.wallTransform.position;
            screenPosition = EditorScreenCoordinateUtility.ToUnityScreenPoint(
                targetCamera,
                targetCamera.WorldToScreenPoint(fallbackWorldPosition));
            return screenPosition.z > 0f;
        }

        Vector3 startWorld = wall.Data.startPoint;
        Vector3 endWorld = wall.Data.endPoint;
        float labelY = entry.wallTransform.position.y + entry.wallHeight * 0.5f;
        startWorld.y = labelY;
        endWorld.y = labelY;

        Vector3 startScreen = EditorScreenCoordinateUtility.ToUnityScreenPoint(
            targetCamera,
            targetCamera.WorldToScreenPoint(startWorld));
        Vector3 endScreen = EditorScreenCoordinateUtility.ToUnityScreenPoint(
            targetCamera,
            targetCamera.WorldToScreenPoint(endWorld));
        if (startScreen.z <= 0f || endScreen.z <= 0f)
        {
            return false;
        }

        Vector2 startPoint = startScreen;
        Vector2 endPoint = endScreen;
        Vector2 wallDirection = endPoint - startPoint;
        if (wallDirection.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        Vector2 midpoint = (startPoint + endPoint) * 0.5f;
        Vector2 normal = new Vector2(-wallDirection.y, wallDirection.x).normalized;
        Vector2 awayFromViewportCenter = midpoint - GetViewportCenter();
        if (awayFromViewportCenter.sqrMagnitude > 0.0001f && Vector2.Dot(normal, awayFromViewportCenter) < 0f)
        {
            normal = -normal;
        }

        float screenOffset = Mathf.Max(labelHeightOffset, GetProjectedLabelHalfExtent(entry, normal) + labelScreenPadding);
        Vector2 labelPoint = midpoint + normal * screenOffset;
        screenPosition = new Vector3(labelPoint.x, labelPoint.y, Mathf.Min(startScreen.z, endScreen.z));
        return true;
    }

    private float GetProjectedLabelHalfExtent(LabelEntry entry, Vector2 direction)
    {
        float scale = Mathf.Max(0.01f, labelScale);
        float width = labelSize.x;
        float height = labelSize.y;
        if (entry != null && entry.labelText != null)
        {
            width = Mathf.Max(entry.labelText.preferredWidth, entry.labelText.fontSize);
            height = Mathf.Max(entry.labelText.preferredHeight, entry.labelText.fontSize);
        }

        float halfWidth = width * scale * 0.5f;
        float halfHeight = height * scale * 0.5f;
        return Mathf.Abs(direction.x) * halfWidth + Mathf.Abs(direction.y) * halfHeight;
    }

    private Vector2 GetViewportCenter()
    {
        Vector4 viewportSignature = EditorScreenCoordinateUtility.GetViewportSignature(targetCamera);
        float width = viewportSignature.x > 0f ? viewportSignature.x : Screen.width;
        float height = viewportSignature.y > 0f ? viewportSignature.y : Screen.height;
        return new Vector2(width * 0.5f, height * 0.5f);
    }

    private void SetLabelRectPosition(RectTransform labelRect, Vector3 screenPosition)
    {
        if (labelRect == null)
        {
            return;
        }

        Canvas canvas = targetCanvas != null ? targetCanvas : labelRect.GetComponentInParent<Canvas>();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect == null)
        {
            labelRect.position = screenPosition;
            return;
        }

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera != null ? canvas.worldCamera : targetCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 anchoredPosition))
        {
            labelRect.anchoredPosition = anchoredPosition;
            return;
        }

        labelRect.position = screenPosition;
    }

    internal void NotifyLengthLabelClicked(Transform wallTransform)
    {
        if (wallTransform == null)
        {
            return;
        }

        LengthLabelClicked?.Invoke(wallTransform);
    }

    private void OnDestroy()
    {
        ClearAllLabels();
    }

    private string FormatLength(float wallLengthUnits)
    {
        float millimeters = MeasurementUnits.UnitsToMillimeters(wallLengthUnits);
        float centimeters = MeasurementUnits.MillimetersToCentimeters(millimeters);
        if (centimeters >= MeasurementUnits.MillimetersToCentimeters(MeasurementUnits.MillimetersPerMeter))
        {
            return $"{MeasurementUnits.MillimetersToMeters(millimeters):0.##} m";
        }

        return $"{centimeters:0.#} cm";
    }

    private void CacheCameraState()
    {
        if (targetCamera == null)
        {
            return;
        }

        Transform cameraTransform = targetCamera.transform;
        lastCameraPosition = cameraTransform.position;
        lastCameraRotation = cameraTransform.rotation;
        lastCameraOrthoSize = targetCamera.orthographicSize;
    }

    private bool HasCameraStateChanged()
    {
        if (targetCamera == null)
        {
            return false;
        }

        Transform cameraTransform = targetCamera.transform;
        return cameraTransform.position != lastCameraPosition ||
               cameraTransform.rotation != lastCameraRotation ||
               !Mathf.Approximately(targetCamera.orthographicSize, lastCameraOrthoSize);
    }

    private void CacheViewportState()
    {
        lastViewportSignature = EditorScreenCoordinateUtility.GetViewportSignature(targetCamera);
    }

    private bool HasViewportChanged()
    {
        return EditorScreenCoordinateUtility.ViewportSignatureChanged(
            lastViewportSignature,
            EditorScreenCoordinateUtility.GetViewportSignature(targetCamera));
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref modeManager);
    }

    private void RefreshLabelInteractionStates()
    {
        foreach (KeyValuePair<int, LabelEntry> pair in labelEntries)
        {
            ApplyLabelInteractionState(pair.Value);
        }
    }

    private void ApplyLabelInteractionState(LabelEntry entry)
    {
        if (entry == null)
        {
            return;
        }

        bool interactable = !entry.isPreview && IsLengthLabelInteractionEnabled();
        if (entry.labelText != null)
        {
            entry.labelText.raycastTarget = interactable;
        }

        if (entry.clickHandler != null)
        {
            entry.clickHandler.SetInteractable(interactable);
        }
    }

    private bool IsLengthLabelInteractionEnabled()
    {
        ResolveReferences();
        return modeManager == null ||
               (modeManager.CurrentMode != EditorMode.RoomCreate &&
                modeManager.CurrentMode != EditorMode.FurniturePlace);
    }

    private static void DestroyLabelObject(GameObject labelObject)
    {
        if (labelObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(labelObject);
        }
        else
        {
            DestroyImmediate(labelObject);
        }
    }

}

internal sealed class WallLengthLabelClickHandler : MonoBehaviour, IPointerClickHandler
{
    private WallLengthDisplay owner;
    private Transform wallTransform;
    private bool interactable = true;

    public void Initialize(WallLengthDisplay owner, Transform wallTransform)
    {
        this.owner = owner;
        this.wallTransform = wallTransform;
    }

    public void SetInteractable(bool interactable)
    {
        this.interactable = interactable;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!interactable || owner == null || wallTransform == null)
        {
            return;
        }

        owner.NotifyLengthLabelClicked(wallTransform);
    }
}
