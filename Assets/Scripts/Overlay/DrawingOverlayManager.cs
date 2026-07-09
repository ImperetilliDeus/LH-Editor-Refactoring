using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DrawingOverlayManager : MonoBehaviour
{
    private const string DefaultOverlayRootName = "DrawingOverlayRoot";

    [Header("References")]
    [SerializeField] private ModeManager modeManager;
    [SerializeField] private GameObject grid;
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private DrawingOverlayRuntime activeRuntime;
    [SerializeField] private OverlayCalibrationPanelController calibrationPanel;
    [SerializeField] private OverlayCalibrationPanelController calibrationPanelPrefab;

    [Header("UI Fonts")]
    [SerializeField] private TMP_FontAsset tmpFontAsset;
    [SerializeField] private Font legacyFont;

    [Header("Overlay Defaults")]
    [SerializeField] [Range(0f, 1f)] private float defaultOpacity = 0.18f;

    [Header("State")]
    [SerializeField] private DrawingOverlayDocument activeDocument;
    [SerializeField] private bool overlayVisible = true;
    [SerializeField] private bool overlayLocked = true;

    private float drawingPlaneY;
    private Texture2D activeDisplayTexture;

    public DrawingOverlayDocument ActiveDocument => activeDocument;
    public float Opacity => activeDocument != null && activeDocument.calibration != null
        ? Mathf.Clamp01(activeDocument.calibration.opacity)
        : Mathf.Clamp01(defaultOpacity);
    public bool OverlayVisible => overlayVisible;
    public bool OverlayLocked => overlayLocked;
    public TMP_FontAsset UiTmpFont => tmpFontAsset;
    public Font UiLegacyFont => legacyFont;
    public OverlayCalibrationPanelController CalibrationPanelPrefab => calibrationPanelPrefab;
    public Canvas ParentCanvas => parentCanvas;
    public bool IsCalibrating => modeManager != null && modeManager.CurrentMode == EditorMode.DrawingOverlayCalibrate;
    public bool HasAppliedOverlay =>
        activeDocument != null &&
        activeDocument.solved != null &&
        activeDocument.solved.unitPerPixel > 0f &&
        (activeDisplayTexture != null || (activeRuntime != null && activeRuntime.DisplayTexture != null));

    public event Action<DrawingOverlayDocument> ActiveOverlayChanged;

    private void Awake()
    {
        ResolveReferences();
        RefreshDrawingPlane();
    }

    public void BeginCalibration(Texture2D texture, string sourcePath, OverlaySourceType sourceType = OverlaySourceType.Image, int pdfPageIndex = 0)
    {
        if (texture == null)
        {
            return;
        }

        activeDocument = new DrawingOverlayDocument
        {
            id = Guid.NewGuid().ToString("N"),
            source = new DrawingOverlaySource
            {
                sourcePath = sourcePath,
                sourceType = sourceType,
                pdfPageIndex = pdfPageIndex,
                pixelWidth = texture.width,
                pixelHeight = texture.height,
            },
            calibration = new DrawingOverlayCalibration
            {
                opacity = defaultOpacity,
            },
            solved = new DrawingOverlayTransform(),
        };
        activeDisplayTexture = texture;
        overlayVisible = true;
        overlayLocked = true;

        EnsureRuntime();
        if (calibrationPanel != null)
        {
            calibrationPanel.Open(this, activeDocument, texture);
        }

        modeManager?.SetMode(EditorMode.DrawingOverlayCalibrate);
        ActiveOverlayChanged?.Invoke(activeDocument);
    }

    public void Initialize(ModeManager resolvedModeManager, GameObject resolvedGrid, OverlayCalibrationPanelController resolvedPanel)
    {
        modeManager = resolvedModeManager;
        grid = resolvedGrid;
        calibrationPanel = resolvedPanel;
        RefreshDrawingPlane();
    }

    public void SetCalibrationPanel(OverlayCalibrationPanelController panel)
    {
        calibrationPanel = panel;
    }

    public bool ApplyCalibration()
    {
        if (!DrawingOverlayCalibrationService.TrySolve(activeDocument, out DrawingOverlayTransform solved))
        {
            return false;
        }

        activeDocument.solved = solved;
        RefreshDrawingPlane();
        EnsureRuntime();
        Texture2D displayTexture = calibrationPanel != null && calibrationPanel.CurrentTexture != null
            ? calibrationPanel.CurrentTexture
            : activeDisplayTexture;
        activeDisplayTexture = displayTexture;
        activeRuntime.SetDocument(activeDocument, displayTexture, drawingPlaneY);
        ActiveOverlayChanged?.Invoke(activeDocument);
        return true;
    }

    public void ResetCalibration()
    {
        if (activeDocument == null)
        {
            return;
        }

        activeDocument.ResetCalibration();
        activeDocument.calibration.opacity = defaultOpacity;
        if (activeRuntime != null)
        {
            activeRuntime.gameObject.SetActive(false);
        }

        ActiveOverlayChanged?.Invoke(activeDocument);
    }

    public void ClearOverlay()
    {
        activeDocument = null;
        activeDisplayTexture = null;
        if (activeRuntime != null)
        {
            activeRuntime.ClearDocument();
        }

        if (calibrationPanel != null)
        {
            calibrationPanel.Close();
        }

        ActiveOverlayChanged?.Invoke(null);
    }

    public void CompleteCalibration()
    {
        if (modeManager != null && modeManager.CurrentMode == EditorMode.DrawingOverlayCalibrate)
        {
            modeManager.SetMode(EditorMode.Default);
        }

        if (calibrationPanel != null)
        {
            calibrationPanel.Close();
        }

        ActiveOverlayChanged?.Invoke(activeDocument);
    }

    public Vector2 PixelToWorld(Vector2 pixel)
    {
        return DrawingOverlayCalibrationService.PixelToWorldXZ(pixel, activeDocument);
    }

    public Vector2 WorldToPixel(Vector2 worldXZ)
    {
        return DrawingOverlayCalibrationService.WorldXZToPixel(worldXZ, activeDocument);
    }

    public void NotifyDocumentChanged()
    {
        ActiveOverlayChanged?.Invoke(activeDocument);
    }

    public void SetOpacity(float opacity)
    {
        float clampedOpacity = Mathf.Clamp01(opacity);
        if (activeDocument != null && activeDocument.calibration != null)
        {
            activeDocument.calibration.opacity = clampedOpacity;
        }

        if (activeRuntime != null)
        {
            activeRuntime.RefreshOpacity();
        }

        ActiveOverlayChanged?.Invoke(activeDocument);
    }

    public void SetOverlayVisible(bool visible)
    {
        overlayVisible = visible;
        if (activeRuntime != null)
        {
            if (visible)
            {
                activeRuntime.UpdateVisual(drawingPlaneY);
            }
            else
            {
                activeRuntime.gameObject.SetActive(false);
            }
        }

        ActiveOverlayChanged?.Invoke(activeDocument);
    }

    public void SetOverlayLocked(bool locked)
    {
        overlayLocked = locked;
        ActiveOverlayChanged?.Invoke(activeDocument);
    }

    public void ShowStatusOnly(string message)
    {
        if (calibrationPanel != null)
        {
            calibrationPanel.ShowStatusOnly(message);
        }
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref modeManager);

        if (grid == null)
        {
            Transform gridTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultGridName, true);
            if (gridTransform != null)
            {
                grid = gridTransform.gameObject;
            }
        }

        if (calibrationPanel == null)
        {
            LayerUtility.ResolveObject(ref calibrationPanel);
        }

        if (parentCanvas == null)
        {
            parentCanvas = LayerUtility.FindCanvasByNameOrFirst(LayerUtility.DefaultCanvasName);
        }
    }

    private void EnsureRuntime()
    {
        if (activeRuntime != null)
        {
            return;
        }

        Transform existing = LayerUtility.FindTransformByName(DefaultOverlayRootName, true);
        GameObject targetObject = existing != null ? existing.gameObject : new GameObject(DefaultOverlayRootName);
        activeRuntime = targetObject.GetComponent<DrawingOverlayRuntime>();
        if (activeRuntime == null)
        {
            activeRuntime = targetObject.AddComponent<DrawingOverlayRuntime>();
        }
    }

    private void RefreshDrawingPlane()
    {
        drawingPlaneY = 0f;
        if (grid == null)
        {
            return;
        }

        if (grid.TryGetComponent(out Collider gridCollider))
        {
            drawingPlaneY = gridCollider.bounds.center.y;
            return;
        }

        if (grid.TryGetComponent(out Renderer gridRenderer))
        {
            drawingPlaneY = gridRenderer.bounds.center.y;
            return;
        }

        drawingPlaneY = grid.transform.position.y;
    }
}
