using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SceneHierarchyTreeView : MonoBehaviour
{
    private const string BackgroundName = "_Background";
    private const string ScrollRootName = "HierarchyScroll";
    private const string ViewportName = "Viewport";

    [Header("References")]
    [SerializeField] private RoomManager roomManager;
    [SerializeField] private WallSelectionManager wallSelectionManager;
    [SerializeField] private Transform wallRoot;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform rowTemplate;

    [Header("Text")]
    [SerializeField] private Font rowFont;
    [SerializeField] private Color rowTextColor = Color.black;
    [SerializeField] private int rowFontSize = 13;

    [Header("Layout")]
    [SerializeField] private float rowHeight = 28f;
    [SerializeField] private float childIndent = 18f;
    [SerializeField] private bool autoConfigureContentLayout = true;

    [Header("Scroll")]
    [SerializeField] private float scrollSensitivity = 96f;
    [SerializeField] private bool smoothScrolling = true;
    [SerializeField] private float scrollDecelerationRate = 0.12f;
    [SerializeField] private float scrollSmoothingSpeed = 18f;

    [Header("Resize")]
    [SerializeField] private Button resizeDragHandle;
    [SerializeField] private RectTransform resizableTarget;
    [SerializeField] private float minWidth = 160f;
    [SerializeField] private float maxWidth = 520f;
    [SerializeField] private bool invertResizeDirection;

    private readonly List<Room> cachedRooms = new List<Room>();
    private readonly List<GameObject> spawnedRows = new List<GameObject>();
    private IEnumerable<Room> testRooms;
    private float resizeStartPointerX;
    private float resizeStartWidth;

    private void Awake()
    {
        ResolveReferences();
        BindResizeHandle();
        BindEvents();
        RebuildNow();
    }

    private void OnEnable()
    {
        BindResizeHandle();
        BindEvents();
        RebuildNow();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void OnDestroy()
    {
        UnbindEvents();
    }

    public void RebuildNow()
    {
        ResolveReferences();
        ClearRows();
        if (contentRoot == null)
        {
            return;
        }

        EnsureContentLayout();
        EnsureScrollRect();
        List<SceneHierarchyTreeRow> rows = SceneHierarchyTreeModel.BuildRows(wallRoot, GetRooms());
        for (int i = 0; i < rows.Count; i++)
        {
            CreateRow(rows[i]);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    public static void RefreshAllInstances()
    {
        SceneHierarchyTreeView[] treeViews = FindObjectsByType<SceneHierarchyTreeView>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < treeViews.Length; i++)
        {
            SceneHierarchyTreeView treeView = treeViews[i];
            if (treeView != null)
            {
                treeView.RebuildNow();
            }
        }
    }

    public void SetReferencesForTests(
        Transform testWallRoot,
        IEnumerable<Room> rooms,
        RectTransform testContentRoot,
        WallSelectionManager testSelectionManager)
    {
        wallRoot = testWallRoot;
        testRooms = rooms;
        contentRoot = testContentRoot;
        wallSelectionManager = testSelectionManager;
    }

    public void ConfigureResizeForTests(Button testResizeDragHandle, RectTransform testResizableTarget, float testMinWidth, float testMaxWidth)
    {
        resizeDragHandle = testResizeDragHandle;
        resizableTarget = testResizableTarget;
        minWidth = testMinWidth;
        maxWidth = testMaxWidth;
        BindResizeHandle();
    }

    private IEnumerable<Room> GetRooms()
    {
        if (testRooms != null)
        {
            return testRooms;
        }

        cachedRooms.Clear();
        if (roomManager != null)
        {
            roomManager.GetAllRooms(cachedRooms);
        }

        return cachedRooms;
    }

    private void ResolveReferences()
    {
        LayerUtility.ResolveObject(ref roomManager);
        LayerUtility.ResolveObject(ref wallSelectionManager);
        LayerUtility.ResolveWallRoot(ref wallRoot, true);

        if (scrollRect == null && contentRoot != null)
        {
            scrollRect = contentRoot.GetComponentInParent<ScrollRect>(true);
        }
    }

    private void BindEvents()
    {
        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleHierarchyChanged;
            roomManager.RoomsChanged += HandleHierarchyChanged;
        }

        WallRegistry.RegistryChanged -= HandleHierarchyChanged;
        WallRegistry.RegistryChanged += HandleHierarchyChanged;
    }

    private void UnbindEvents()
    {
        if (roomManager != null)
        {
            roomManager.RoomsChanged -= HandleHierarchyChanged;
        }

        WallRegistry.RegistryChanged -= HandleHierarchyChanged;
    }

    private void HandleHierarchyChanged()
    {
        RebuildNow();
    }

    private void CreateRow(SceneHierarchyTreeRow row)
    {
        RectTransform rowTransform = rowTemplate != null
            ? Instantiate(rowTemplate, contentRoot)
            : CreateFallbackRow(contentRoot);

        rowTransform.gameObject.SetActive(true);
        rowTransform.name = $"{row.Kind}_{row.DisplayName}";
        rowTransform.SetParent(contentRoot, false);
        rowTransform.localScale = Vector3.one;
        rowTransform.anchoredPosition = new Vector2(0f, rowTransform.anchoredPosition.y);

        LayoutElement layoutElement = rowTransform.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = rowTransform.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredHeight = rowHeight;

        Text label = rowTransform.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = row.DisplayName;
            ApplyTextStyle(label);
            ApplyLabelIndent(label.rectTransform, row.Depth);
        }

        Button button = rowTransform.GetComponent<Button>();
        if (button != null)
        {
            button.interactable = row.Kind == SceneHierarchyTreeRowKind.Wall && row.RepresentativeWall != null;
            button.onClick.RemoveAllListeners();
            if (button.interactable)
            {
                Wall wall = row.RepresentativeWall;
                button.onClick.AddListener(() => SelectWall(wall));
            }
        }

        spawnedRows.Add(rowTransform.gameObject);
    }

    private void SelectWall(Wall wall)
    {
        if (wallSelectionManager == null || wall == null)
        {
            return;
        }

        wallSelectionManager.SetSelectedWall(wall.gameObject);
    }

    private RectTransform CreateFallbackRow(RectTransform parent)
    {
        GameObject rowObject = new GameObject("HierarchyRow", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.SetParent(parent, false);
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        Image image = rowObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.04f);

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rowRect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 0f);
        textRect.offsetMax = new Vector2(-8f, 0f);

        Text text = textObject.GetComponent<Text>();
        text.alignment = TextAnchor.MiddleLeft;
        ApplyTextStyle(text);

        return rowRect;
    }

    private void ApplyTextStyle(Text text)
    {
        if (text == null)
        {
            return;
        }

        if (rowFont != null)
        {
            text.font = rowFont;
        }

        text.color = rowTextColor;
        text.fontSize = Mathf.Max(1, rowFontSize);
    }

    private void EnsureContentLayout()
    {
        if (!autoConfigureContentLayout || contentRoot == null || contentRoot.GetComponent<LayoutGroup>() != null)
        {
            return;
        }

        VerticalLayoutGroup layoutGroup = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.spacing = 0f;
        layoutGroup.padding = new RectOffset(0, 0, 0, 0);
    }

    private void EnsureScrollRect()
    {
        if (contentRoot == null)
        {
            return;
        }

        RectTransform panelRoot = GetHierarchyPanelRoot();
        if (panelRoot == null)
        {
            return;
        }

        EnsureBackground(panelRoot);
        RemoveScrollRectIfPresent(contentRoot);

        ContentSizeFitter sizeFitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        }

        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform scrollRoot = EnsureScrollRoot(panelRoot);
        RectTransform viewport = EnsureScrollViewport(scrollRoot, contentRoot);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.offsetMin = new Vector2(0f, contentRoot.offsetMin.y);
        contentRoot.offsetMax = new Vector2(0f, contentRoot.offsetMax.y);

        scrollRect.content = contentRoot;
        scrollRect.viewport = viewport;

        scrollRect.vertical = true;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = Mathf.Max(1f, scrollSensitivity);
        scrollRect.inertia = smoothScrolling;
        scrollRect.decelerationRate = Mathf.Clamp(scrollDecelerationRate, 0.01f, 1f);
        EnsureScrollHitTarget(viewport);
        EnsureSmoothScrollHandler(viewport, scrollRect);
    }

    private RectTransform GetHierarchyPanelRoot()
    {
        RectTransform target = GetResizableTarget();
        if (target != null)
        {
            return target;
        }

        return contentRoot != null ? contentRoot.parent as RectTransform : null;
    }

    private static void EnsureBackground(RectTransform panelRoot)
    {
        if (panelRoot == null)
        {
            return;
        }

        RectTransform background = panelRoot.Find(BackgroundName) as RectTransform;
        if (background == null)
        {
            GameObject backgroundObject = new GameObject(BackgroundName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background = backgroundObject.GetComponent<RectTransform>();
            background.SetParent(panelRoot, false);
            Image image = backgroundObject.GetComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        }

        background.anchorMin = Vector2.zero;
        background.anchorMax = Vector2.one;
        background.pivot = new Vector2(0.5f, 0.5f);
        background.offsetMin = Vector2.zero;
        background.offsetMax = Vector2.zero;
        background.SetAsFirstSibling();
    }

    private RectTransform EnsureScrollRoot(RectTransform panelRoot)
    {
        RectTransform existingScrollRoot = scrollRect != null && scrollRect.transform != contentRoot
            ? scrollRect.transform as RectTransform
            : null;
        if (existingScrollRoot != null)
        {
            ConfigureScrollRoot(existingScrollRoot);
            return existingScrollRoot;
        }

        Transform existing = panelRoot.Find(ScrollRootName);
        RectTransform scrollRoot = existing as RectTransform;
        if (scrollRoot == null)
        {
            GameObject scrollObject = new GameObject(ScrollRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
            scrollRoot = scrollObject.GetComponent<RectTransform>();
            scrollRoot.SetParent(panelRoot, false);
        }

        ConfigureScrollRoot(scrollRoot);
        if (scrollRoot.parent == panelRoot && panelRoot.childCount > 1)
        {
            scrollRoot.SetSiblingIndex(1);
        }

        scrollRect = scrollRoot.GetComponent<ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
        }

        return scrollRoot;
    }

    private static void ConfigureScrollRoot(RectTransform scrollRoot)
    {
        scrollRoot.anchorMin = Vector2.zero;
        scrollRoot.anchorMax = Vector2.one;
        scrollRoot.pivot = new Vector2(0.5f, 0.5f);
        scrollRoot.offsetMin = Vector2.zero;
        scrollRoot.offsetMax = Vector2.zero;

        Graphic rootGraphic = scrollRoot.GetComponent<Graphic>();
        if (rootGraphic != null)
        {
            rootGraphic.color = Color.clear;
            rootGraphic.raycastTarget = false;
        }
    }

    private static RectTransform EnsureScrollViewport(RectTransform scrollRoot, RectTransform targetContent)
    {
        Transform existing = scrollRoot.Find(ViewportName);
        RectTransform viewport = existing as RectTransform;
        if (viewport == null)
        {
            GameObject viewportObject = new GameObject(ViewportName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            viewport = viewportObject.GetComponent<RectTransform>();
            viewport.SetParent(scrollRoot, false);
        }

        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        if (viewport.GetComponent<RectMask2D>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        if (targetContent.parent != viewport)
        {
            targetContent.SetParent(viewport, false);
        }

        return viewport;
    }

    private static void RemoveScrollRectIfPresent(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        ScrollRect misplacedScrollRect = target.GetComponent<ScrollRect>();
        if (misplacedScrollRect == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(misplacedScrollRect);
        }
        else
        {
            DestroyImmediate(misplacedScrollRect);
        }
    }
    private static void EnsureScrollHitTarget(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        Graphic hitTarget = target.GetComponent<Graphic>();
        if (hitTarget == null)
        {
            Image image = target.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            hitTarget = image;
        }

        hitTarget.color = Color.clear;
        hitTarget.raycastTarget = true;
    }

    private void EnsureSmoothScrollHandler(RectTransform viewport, ScrollRect targetScrollRect)
    {
        if (viewport == null || targetScrollRect == null)
        {
            return;
        }

        SceneHierarchySmoothScrollHandler handler = viewport.GetComponent<SceneHierarchySmoothScrollHandler>();
        if (handler == null)
        {
            handler = viewport.gameObject.AddComponent<SceneHierarchySmoothScrollHandler>();
        }

        handler.Initialize(
            targetScrollRect,
            Mathf.Max(1f, scrollSensitivity),
            Mathf.Max(1f, scrollSmoothingSpeed),
            smoothScrolling);
    }

    private static void RemoveMisplacedScrollRect(ScrollRect misplacedScrollRect)
    {
        if (misplacedScrollRect == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(misplacedScrollRect);
        }
        else
        {
            DestroyImmediate(misplacedScrollRect);
        }
    }

    private void ApplyLabelIndent(RectTransform labelRect, int depth)
    {
        if (labelRect == null)
        {
            return;
        }

        Vector2 offsetMin = labelRect.offsetMin;
        offsetMin.x += Mathf.Max(0, depth) * childIndent;
        labelRect.offsetMin = offsetMin;
    }

    private void BindResizeHandle()
    {
        if (resizeDragHandle == null)
        {
            return;
        }

        SceneHierarchyTreeResizeHandle dragHandle = resizeDragHandle.GetComponent<SceneHierarchyTreeResizeHandle>();
        if (dragHandle == null)
        {
            dragHandle = resizeDragHandle.gameObject.AddComponent<SceneHierarchyTreeResizeHandle>();
        }

        dragHandle.Initialize(this);
    }

    internal void BeginResizeDrag(PointerEventData eventData)
    {
        RectTransform target = GetResizableTarget();
        if (target == null || eventData == null)
        {
            return;
        }

        resizeStartPointerX = eventData.position.x;
        resizeStartWidth = GetCurrentWidth(target);
    }

    internal void HandleResizeDrag(PointerEventData eventData)
    {
        RectTransform target = GetResizableTarget();
        if (target == null || eventData == null)
        {
            return;
        }

        float pointerDelta = eventData.position.x - resizeStartPointerX;
        if (invertResizeDirection)
        {
            pointerDelta = -pointerDelta;
        }

        float lowerWidth = Mathf.Min(minWidth, maxWidth);
        float upperWidth = Mathf.Max(minWidth, maxWidth);
        float width = Mathf.Clamp(resizeStartWidth + pointerDelta, lowerWidth, upperWidth);
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    internal void EndResizeDrag()
    {
        resizeStartPointerX = 0f;
        resizeStartWidth = 0f;
    }

    private RectTransform GetResizableTarget()
    {
        if (resizableTarget != null)
        {
            return resizableTarget;
        }

        return transform as RectTransform;
    }

    private static float GetCurrentWidth(RectTransform target)
    {
        if (target == null)
        {
            return 0f;
        }

        float width = target.rect.width;
        return width > 0f ? width : target.sizeDelta.x;
    }

    private void ClearRows()
    {
        for (int i = spawnedRows.Count - 1; i >= 0; i--)
        {
            if (spawnedRows[i] != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(spawnedRows[i]);
                }
                else
                {
                    DestroyImmediate(spawnedRows[i]);
                }
            }
        }

        spawnedRows.Clear();
    }
}

public sealed class SceneHierarchyTreeResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private SceneHierarchyTreeView treeView;

    public void Initialize(SceneHierarchyTreeView owner)
    {
        treeView = owner;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (treeView != null)
        {
            treeView.BeginResizeDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (treeView != null)
        {
            treeView.HandleResizeDrag(eventData);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (treeView != null)
        {
            treeView.EndResizeDrag();
        }
    }
}

public sealed class SceneHierarchySmoothScrollHandler : MonoBehaviour, IScrollHandler
{
    private ScrollRect scrollRect;
    private float scrollSensitivity = 96f;
    private float smoothingSpeed = 18f;
    private bool smoothScrolling = true;
    private float targetVerticalNormalizedPosition = 1f;
    private bool hasTarget;

    public void Initialize(ScrollRect targetScrollRect, float sensitivity, float speed, bool smooth)
    {
        scrollRect = targetScrollRect;
        scrollSensitivity = Mathf.Max(1f, sensitivity);
        smoothingSpeed = Mathf.Max(1f, speed);
        smoothScrolling = smooth;
        if (scrollRect != null)
        {
            targetVerticalNormalizedPosition = scrollRect.verticalNormalizedPosition;
            hasTarget = true;
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null || eventData == null)
        {
            return;
        }

        float hiddenHeight = scrollRect.content.rect.height - scrollRect.viewport.rect.height;
        if (hiddenHeight <= 0f)
        {
            return;
        }

        if (!hasTarget)
        {
            targetVerticalNormalizedPosition = scrollRect.verticalNormalizedPosition;
            hasTarget = true;
        }

        targetVerticalNormalizedPosition = Mathf.Clamp01(
            targetVerticalNormalizedPosition + (eventData.scrollDelta.y * scrollSensitivity / hiddenHeight));
        if (!smoothScrolling)
        {
            scrollRect.verticalNormalizedPosition = targetVerticalNormalizedPosition;
        }

        eventData.Use();
    }

    private void Update()
    {
        if (!smoothScrolling || scrollRect == null || !hasTarget)
        {
            return;
        }

        float currentPosition = scrollRect.verticalNormalizedPosition;
        float t = 1f - Mathf.Exp(-smoothingSpeed * Time.unscaledDeltaTime);
        float nextPosition = Mathf.Lerp(currentPosition, targetVerticalNormalizedPosition, t);
        if (Mathf.Abs(nextPosition - targetVerticalNormalizedPosition) < 0.001f)
        {
            nextPosition = targetVerticalNormalizedPosition;
        }

        scrollRect.verticalNormalizedPosition = nextPosition;
    }
}
