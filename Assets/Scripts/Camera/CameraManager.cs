using UnityEngine;
public class CameraManager : MonoBehaviour, IEditorModeInputHandler
{
    [SerializeField] private Camera MainCamera;
    [SerializeField] private GameObject Grid;

    [SerializeField] private float panSensitivity = 1f;
    [SerializeField] private float zoomSpeed = 50f;
    [SerializeField] private float minZoomSize = 100f;
    [SerializeField] private float maxZoomSize = 1000f;

    private Bounds gridBounds;
    private Plane gridPlane;
    private bool hasGridBounds;
    private bool isPanning;
    private Vector3 lastMouseWorldPoint;
    private IEditorInputProvider inputProvider;

    private void Reset()
    {
        MainCamera = Camera.main;
    }

    private void Awake()
    {
        if (MainCamera == null)
        {
            MainCamera = Camera.main;
        }

        if (MainCamera == null)
        {
            Debug.LogWarning("CameraManager could not find a camera. Disable this component or assign MainCamera.", this);
            return;
        }

        inputProvider = EditorInputManager.Instance.InputProvider;
        RefreshGridBounds();
        ClampZoom();
        ClampCameraPosition();
        EditorInputManager.Instance.RegisterGlobalHandler(this);
    }

    private void OnValidate()
    {
        panSensitivity = Mathf.Max(0f, panSensitivity);
        zoomSpeed = Mathf.Max(0f, zoomSpeed);
        minZoomSize = Mathf.Max(0.01f, minZoomSize);
        maxZoomSize = Mathf.Max(minZoomSize, maxZoomSize);
    }

    private void OnDestroy()
    {
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterGlobalHandler(this);
        }
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        if (MainCamera == null || inputProvider == null || !inputFrame.IsPointerAvailable)
        {
            return;
        }

        HandleZoom(inputFrame);
        HandlePan(inputFrame);
        ClampZoom();
        ClampCameraPosition();
    }

    private void HandlePan(EditorInputFrame inputFrame)
    {
        if (inputFrame.MiddlePressedThisFrame)
        {
            isPanning = TryGetMouseWorldPoint(out lastMouseWorldPoint);
            return;
        }

        if (inputFrame.MiddleReleasedThisFrame)
        {
            isPanning = false;
            return;
        }

        if (!isPanning || !inputFrame.MiddlePressed)
        {
            return;
        }

        if (!TryGetMouseWorldPoint(out Vector3 currentMouseWorldPoint))
        {
            return;
        }

        Vector3 moveDelta = (lastMouseWorldPoint - currentMouseWorldPoint) * panSensitivity;
        moveDelta.y = 0f;

        MainCamera.transform.position += moveDelta;
        ClampCameraPosition();

        if (!TryGetMouseWorldPoint(out lastMouseWorldPoint))
        {
            isPanning = false;
        }
    }

    private void HandleZoom(EditorInputFrame inputFrame)
    {
        if (!MainCamera.orthographic)
        {
            return;
        }

        if (inputFrame.PointerOverUI)
        {
            return;
        }

        float scrollInput = inputFrame.ScrollDeltaY;
        if (Mathf.Approximately(scrollInput, 0f))
        {
            return;
        }

        MainCamera.orthographicSize -= scrollInput * zoomSpeed * Time.unscaledDeltaTime;
    }

    private void RefreshGridBounds()
    {
        hasGridBounds = false;

        if (Grid == null)
        {
            return;
        }

        if (Grid.TryGetComponent(out Collider gridCollider))
        {
            gridBounds = gridCollider.bounds;
            hasGridBounds = true;
        }
        else if (Grid.TryGetComponent(out Renderer gridRenderer))
        {
            gridBounds = gridRenderer.bounds;
            hasGridBounds = true;
        }

        if (!hasGridBounds)
        {
            return;
        }

        gridPlane = new Plane(Vector3.up, new Vector3(0f, gridBounds.center.y, 0f));
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;

        if (!hasGridBounds)
        {
            return false;
        }

        if (!inputProvider.TryGetPointerScreenPosition(out Vector2 mousePosition))
        {
            return false;
        }

        Ray mouseRay = MainCamera.ScreenPointToRay(mousePosition);
        if (!gridPlane.Raycast(mouseRay, out float enter))
        {
            return false;
        }

        worldPoint = mouseRay.GetPoint(enter);
        return true;
    }

    private void ClampZoom()
    {
        if (!MainCamera.orthographic)
        {
            return;
        }

        float effectiveMaxZoomSize = GetEffectiveMaxZoomSize();
        float effectiveMinZoomSize = Mathf.Min(minZoomSize, effectiveMaxZoomSize);
        MainCamera.orthographicSize = Mathf.Clamp(MainCamera.orthographicSize, effectiveMinZoomSize, effectiveMaxZoomSize);
    }

    private float GetEffectiveMaxZoomSize()
    {
        if (!hasGridBounds)
        {
            return Mathf.Max(minZoomSize, maxZoomSize);
        }

        float maxZoomByGridHeight = gridBounds.extents.z;
        float maxZoomByGridWidth = gridBounds.extents.x / Mathf.Max(MainCamera.aspect, 0.0001f);
        float maxZoomAllowedByGrid = Mathf.Min(maxZoomByGridHeight, maxZoomByGridWidth);
        float inspectorMaxZoom = Mathf.Max(minZoomSize, maxZoomSize);

        return Mathf.Max(0.01f, Mathf.Min(inspectorMaxZoom, maxZoomAllowedByGrid));
    }

    private void ClampCameraPosition()
    {
        if (!hasGridBounds)
        {
            return;
        }

        Vector3 cameraPosition = MainCamera.transform.position;
        float halfHeight = MainCamera.orthographic ? MainCamera.orthographicSize : 0f;
        float halfWidth = MainCamera.orthographic ? halfHeight * MainCamera.aspect : 0f;

        float minX = gridBounds.min.x + halfWidth;
        float maxX = gridBounds.max.x - halfWidth;
        float minZ = gridBounds.min.z + halfHeight;
        float maxZ = gridBounds.max.z - halfHeight;

        cameraPosition.x = minX > maxX ? gridBounds.center.x : Mathf.Clamp(cameraPosition.x, minX, maxX);
        cameraPosition.z = minZ > maxZ ? gridBounds.center.z : Mathf.Clamp(cameraPosition.z, minZ, maxZ);

        MainCamera.transform.position = cameraPosition;
    }
}
