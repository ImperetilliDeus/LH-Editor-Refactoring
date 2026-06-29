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

    private float drawingPlaneY;

    public DrawingOverlayDocument ActiveDocument => activeDocument;
    public TMP_FontAsset UiTmpFont => tmpFontAsset;
    public Font UiLegacyFont => legacyFont;
    public OverlayCalibrationPanelController CalibrationPanelPrefab => calibrationPanelPrefab;
    public Canvas ParentCanvas => parentCanvas;

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
        activeRuntime.SetDocument(activeDocument, calibrationPanel != null ? calibrationPanel.CurrentTexture : null, drawingPlaneY);
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
