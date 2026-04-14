using UnityEngine;

public class WallOpening : MonoBehaviour
{
    [SerializeField] private WallOpeningPlacementManager.OpeningPlacementType type;
    [SerializeField] private string doorTypeKey;
    [SerializeField] private string windowTypeKey;
    [SerializeField] private bool doorOpensRight;
    [SerializeField] private bool doorVerticalFlip;
    [SerializeField] private float centerDistance;
    [SerializeField] private float width;
    [SerializeField] private float height;
    [SerializeField] private float depth;
    [SerializeField] private float bottomY;
    [SerializeField] private WallOpeningContainer container;
    [SerializeField] private WallOpeningPlacementManager placementManager;

    private Transform startAnchor;
    private Transform endAnchor;
    private WallOpeningMarkerUI markerUI;

    public WallOpeningPlacementManager.OpeningPlacementType Type => type;
    public string DoorTypeKey => doorTypeKey;
    public string WindowTypeKey => windowTypeKey;
    public bool DoorOpensRight => doorOpensRight;
    public bool DoorVerticalFlip => doorVerticalFlip;
    public float CenterDistance => centerDistance;
    public float Width => width;
    public float Height => height;
    public float Depth => depth;
    public float BottomY => bottomY;
    public WallOpeningContainer Container => container;

    public void Initialize(
        WallOpeningPlacementManager manager,
        WallOpeningContainer ownerContainer,
        WallOpeningPlacementManager.OpeningPlacementType openingType,
        string openingDoorTypeKey,
        string openingWindowTypeKey,
        bool openingDoorOpensRight,
        bool openingDoorVerticalFlip,
        float openingCenterDistance,
        float openingWidth,
        float openingHeight,
        float openingDepth,
        float openingBottomY)
    {
        placementManager = manager;
        container = ownerContainer;
        type = openingType;
        doorTypeKey = openingType == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? openingDoorTypeKey ?? string.Empty
            : string.Empty;
        windowTypeKey = openingType == WallOpeningPlacementManager.OpeningPlacementType.Window
            ? openingWindowTypeKey ?? string.Empty
            : string.Empty;
        doorOpensRight = openingType == WallOpeningPlacementManager.OpeningPlacementType.Door && openingDoorOpensRight;
        doorVerticalFlip = openingType == WallOpeningPlacementManager.OpeningPlacementType.Door && openingDoorVerticalFlip;
        centerDistance = openingCenterDistance;
        width = openingWidth;
        height = openingHeight;
        depth = openingDepth;
        bottomY = openingBottomY;
    }

    public void SetCenterDistance(float value)
    {
        centerDistance = value;
    }

    public void SetWidth(float value)
    {
        width = value;
    }

    public void SetHeight(float value)
    {
        height = value;
    }

    public void SetDepth(float value)
    {
        depth = value;
    }

    public void SetBottomY(float value)
    {
        bottomY = value;
    }

    public void SetDoorTypeKey(string value)
    {
        doorTypeKey = type == WallOpeningPlacementManager.OpeningPlacementType.Door
            ? value ?? string.Empty
            : string.Empty;
    }

    public void SetWindowTypeKey(string value)
    {
        windowTypeKey = type == WallOpeningPlacementManager.OpeningPlacementType.Window
            ? value ?? string.Empty
            : string.Empty;
    }

    public void SetDoorOpensRight(bool value)
    {
        doorOpensRight = type == WallOpeningPlacementManager.OpeningPlacementType.Door && value;
    }

    public void SetDoorVerticalFlip(bool value)
    {
        doorVerticalFlip = type == WallOpeningPlacementManager.OpeningPlacementType.Door && value;
    }

    public void EnsureMarker(Canvas canvas, Camera worldCamera, GameObject markerPrefab, Vector2 scaleMultiplier)
    {
        EnsureAnchors();

        if (markerUI == null)
        {
            markerUI = GetComponent<WallOpeningMarkerUI>();
            if (markerUI == null)
            {
                markerUI = gameObject.AddComponent<WallOpeningMarkerUI>();
            }
        }

        markerUI.Initialize(this, placementManager, canvas, worldCamera, startAnchor, endAnchor, markerPrefab, scaleMultiplier);
    }

    private void EnsureAnchors()
    {
        if (startAnchor == null)
        {
            Transform existing = transform.Find("MarkerStart");
            startAnchor = existing != null ? existing : new GameObject("MarkerStart").transform;
            startAnchor.SetParent(transform, false);
        }

        if (endAnchor == null)
        {
            Transform existing = transform.Find("MarkerEnd");
            endAnchor = existing != null ? existing : new GameObject("MarkerEnd").transform;
            endAnchor.SetParent(transform, false);
        }

        startAnchor.localPosition = new Vector3(0f, 0f, -0.5f);
        endAnchor.localPosition = new Vector3(0f, 0f, 0.5f);
    }
}
