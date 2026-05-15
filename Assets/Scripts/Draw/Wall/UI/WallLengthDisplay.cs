using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class WallLengthDisplay : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Style")]
    [SerializeField] private Font labelFont;

    [SerializeField] private float labelHeightOffset = 0.25f;
    [SerializeField] private float labelScale = 0.2f;
    [SerializeField] private Color textColor = Color.black;
    [SerializeField] private Color previewTextColor = new Color(0f, 0f, 0f, 0.75f);
    [SerializeField] private int fontSize = 18;
    [SerializeField] private Vector2 labelSize = new Vector2(220f, 40f);

    private const string CanvasName = "WallLengthCanvas";

    private Camera targetCamera;

    private class LabelEntry
    {
        public Transform wallTransform;
        public RectTransform labelRect;
        public Text labelText;
        public float wallHeight;
    }

    private readonly Dictionary<int, LabelEntry> labelEntries = new Dictionary<int, LabelEntry>();
    private readonly List<int> removedLabelKeys = new List<int>();
    private bool labelPositionsDirty = true;
    private Vector3 lastCameraPosition;
    private Quaternion lastCameraRotation;
    private float lastCameraOrthoSize;

    private void OnValidate()
    {
        labelScale = Mathf.Max(0.01f, labelScale);
        fontSize = Mathf.Max(8, fontSize);
        labelSize.x = Mathf.Max(40f, labelSize.x);
        labelSize.y = Mathf.Max(20f, labelSize.y);
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
        entry.labelText.text = FormatLength(wallLengthUnits);
        entry.labelText.color = isPreview ? previewTextColor : textColor;
        entry.labelText.fontSize = fontSize;
        entry.labelRect.sizeDelta = labelSize;
        entry.labelRect.localScale = Vector3.one * labelScale;
        entry.labelText.enabled = true;
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
            Destroy(entry.labelRect.gameObject);
        }

        labelEntries.Remove(key);
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

        bool cameraChanged = HasCameraStateChanged();
        if (!labelPositionsDirty && !cameraChanged)
        {
            return;
        }

        removedLabelKeys.Clear();

        foreach (KeyValuePair<int, LabelEntry> pair in labelEntries)
        {
            LabelEntry entry = pair.Value;
            if (entry == null || entry.wallTransform == null || entry.labelRect == null || entry.labelText == null)
            {
                removedLabelKeys.Add(pair.Key);
                continue;
            }

            Vector3 worldLabelPosition = entry.wallTransform.position + Vector3.up * (entry.wallHeight * 0.5f + labelHeightOffset);
            Vector3 screenPosition = targetCamera.WorldToScreenPoint(worldLabelPosition);
            bool isVisible = screenPosition.z > 0f;

            if (entry.labelText.enabled != isVisible)
            {
                entry.labelText.enabled = isVisible;
            }

            if (!isVisible)
            {
                continue;
            }

            entry.labelRect.position = screenPosition;
        }

        if (removedLabelKeys.Count == 0)
        {
            return;
        }

        for (int i = 0; i < removedLabelKeys.Count; i++)
        {
            labelEntries.Remove(removedLabelKeys[i]);
        }

        labelPositionsDirty = false;
        CacheCameraState();
    }

    private LabelEntry GetOrCreateEntry(Transform wallTransform)
    {
        int key = wallTransform.GetInstanceID();
        if (labelEntries.TryGetValue(key, out LabelEntry existingEntry) && existingEntry != null && existingEntry.labelRect != null && existingEntry.labelText != null)
        {
            existingEntry.wallTransform = wallTransform;
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
                Destroy(existingLabel.gameObject);
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

        lengthText.raycastTarget = false;
        lengthText.horizontalOverflow = HorizontalWrapMode.Overflow;
        lengthText.verticalOverflow = VerticalWrapMode.Overflow;
        lengthText.alignment = TextAnchor.MiddleCenter;
        lengthText.fontSize = fontSize;
        lengthText.color = textColor;

        labelRect.sizeDelta = labelSize;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);

        LabelEntry newEntry = new LabelEntry
        {
            wallTransform = wallTransform,
            labelRect = labelRect,
            labelText = lengthText,
            wallHeight = 0f,
        };

        labelEntries[key] = newEntry;
        labelPositionsDirty = true;
        return newEntry;
    }

    private Canvas GetOrCreateCanvas()
    {
        if (targetCanvas != null)
        {
            return targetCanvas;
        }

        Canvas preferredCanvas = LayerUtility.FindCanvasByNameOrFirst(LayerUtility.DefaultCanvasName);
        if (preferredCanvas != null)
        {
            targetCanvas = preferredCanvas;
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

        if (canvasObject.GetComponent<CanvasScaler>() == null)
        {
            canvasObject.AddComponent<CanvasScaler>();
        }

        if (canvasObject.GetComponent<GraphicRaycaster>() == null)
        {
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        targetCanvas = existingCanvas;
        return existingCanvas;
    }

    private string GetLabelObjectName(Transform wallTransform)
    {
        return $"LengthLabel_{wallTransform.GetInstanceID()}";
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<int, LabelEntry> pair in labelEntries)
        {
            LabelEntry entry = pair.Value;
            if (entry != null && entry.labelRect != null)
            {
                Destroy(entry.labelRect.gameObject);
            }
        }

        labelEntries.Clear();
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

}
