using UnityEngine;
using UnityEngine.Serialization;

public class CameraManager_3D : MonoBehaviour, IEditorModeInputHandler
{
    [FormerlySerializedAs("Camera")]
    [SerializeField] private Camera targetCamera;
    [FormerlySerializedAs("Grid")]
    [SerializeField] private GameObject gridObject;

    [Header("Pan")]
    [SerializeField] private float panSensitivity = 1f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 25f;
    [SerializeField] private float minDistance = 10f;
    [SerializeField] private float maxDistance = 2000f;

    [Header("Rotation")]
    [SerializeField] private float rotationSensitivity = 0.2f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    private Plane dragPlane;
    private bool hasDragPlane;
    private bool isPanning;
    private Vector3 lastMouseWorldPoint;
    private float yaw;
    private float pitch;
    private IEditorInputProvider inputProvider;

    private Transform CameraTransform => targetCamera != null ? targetCamera.transform : null;

    private void Reset()
    {
        targetCamera = GetComponent<Camera>();
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            Debug.LogWarning("CameraManager_3D could not find a camera. Disable this component or assign targetCamera.", this);
            return;
        }

        if (gridObject == null)
        {
            Transform gridTransform = LayerUtility.FindTransformByName(LayerUtility.DefaultGridName, true);
            if (gridTransform != null)
            {
                gridObject = gridTransform.gameObject;
            }
        }

        RefreshDragPlane();
        inputProvider = EditorInputManager.Instance.InputProvider;
        Transform cameraTransform = CameraTransform;
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 currentEuler = cameraTransform.eulerAngles;
        yaw = currentEuler.y;
        pitch = NormalizePitch(currentEuler.x);
        ApplyRotation();
        EditorInputManager.Instance.RegisterGlobalHandler(this);
    }

    private void OnValidate()
    {
        panSensitivity = Mathf.Max(0f, panSensitivity);
        zoomSpeed = Mathf.Max(0f, zoomSpeed);
        minDistance = Mathf.Max(0.01f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        rotationSensitivity = Mathf.Max(0f, rotationSensitivity);
        minPitch = Mathf.Clamp(minPitch, -89f, 89f);
        maxPitch = Mathf.Clamp(maxPitch, -89f, 89f);
        if (maxPitch < minPitch)
        {
            maxPitch = minPitch;
        }
    }

    private void OnDestroy()
    {
        if (EditorInputManager.HasInstance)
        {
            EditorInputManager.Instance.UnregisterGlobalHandler(this);
        }
    }

    private void OnDisable()
    {
        isPanning = false;
    }

    public void HandleEditorInput(EditorInputFrame inputFrame)
    {
        if (!isActiveAndEnabled || targetCamera == null || CameraTransform == null || inputProvider == null || !inputFrame.IsPointerAvailable)
        {
            return;
        }

        HandlePan(inputFrame);
        HandleRotate(inputFrame);
        HandleZoom(inputFrame);
    }

    public void SyncRotationFromCameraTransform()
    {
        Transform cameraTransform = CameraTransform;
        if (cameraTransform == null)
        {
            return;
        }

        Vector3 currentEuler = cameraTransform.eulerAngles;
        yaw = currentEuler.y;
        pitch = Mathf.Clamp(NormalizePitch(currentEuler.x), minPitch, maxPitch);
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
        CameraTransform.position += moveDelta;

        if (!TryGetMouseWorldPoint(out lastMouseWorldPoint))
        {
            isPanning = false;
        }
    }

    private void HandleRotate(EditorInputFrame inputFrame)
    {
        if (!inputFrame.RightPressed)
        {
            return;
        }

        Vector2 lookDelta = inputFrame.PointerDelta;
        if (lookDelta.sqrMagnitude <= 0f)
        {
            return;
        }

        yaw += lookDelta.x * rotationSensitivity;
        pitch -= lookDelta.y * rotationSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        ApplyRotation();
    }

    private void HandleZoom(EditorInputFrame inputFrame)
    {
        if (inputFrame.PointerOverUI)
        {
            return;
        }

        float scrollValue = inputFrame.ScrollDeltaY;
        if (Mathf.Approximately(scrollValue, 0f))
        {
            return;
        }

        float zoomDelta = scrollValue * zoomSpeed * Time.unscaledDeltaTime;
        Vector3 pivot = GetZoomPivot(inputFrame.PointerScreenPosition);
        Vector3 toCamera = CameraTransform.position - pivot;
        float currentDistance = toCamera.magnitude;
        if (currentDistance <= 0.0001f)
        {
            return;
        }

        float nextDistance = Mathf.Clamp(currentDistance - zoomDelta, minDistance, maxDistance);
        CameraTransform.position = pivot + toCamera.normalized * nextDistance;
    }

    private void ApplyRotation()
    {
        CameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void RefreshDragPlane()
    {
        float planeY = 0f;
        hasDragPlane = false;

        if (gridObject != null)
        {
            if (gridObject.TryGetComponent(out Collider gridCollider))
            {
                planeY = gridCollider.bounds.center.y;
                hasDragPlane = true;
            }
            else if (gridObject.TryGetComponent(out Renderer gridRenderer))
            {
                planeY = gridRenderer.bounds.center.y;
                hasDragPlane = true;
            }
            else
            {
                planeY = gridObject.transform.position.y;
                hasDragPlane = true;
            }
        }

        if (!hasDragPlane)
        {
            planeY = CameraTransform.position.y;
            hasDragPlane = true;
        }

        dragPlane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
    }

    private bool TryGetMouseWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (!hasDragPlane)
        {
            return false;
        }

        if (!inputProvider.TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
        {
            return false;
        }

        if (!TryCreateScreenRay(pointerScreenPosition, out Ray ray))
        {
            return false;
        }

        if (!dragPlane.Raycast(ray, out float enter))
        {
            return false;
        }

        worldPoint = ray.GetPoint(enter);
        return true;
    }

    private Vector3 GetZoomPivot(Vector2 pointerScreenPosition)
    {
        if (!hasDragPlane)
        {
            return CameraTransform.position + CameraTransform.forward * minDistance;
        }

        if (TryCreateScreenRay(pointerScreenPosition, out Ray pointerRay) &&
            dragPlane.Raycast(pointerRay, out float pointerEnter))
        {
            return pointerRay.GetPoint(pointerEnter);
        }

        Rect pixelRect = targetCamera.pixelRect;
        Vector2 screenCenter = pixelRect.center;
        if (TryCreateScreenRay(screenCenter, out Ray centerRay) &&
            dragPlane.Raycast(centerRay, out float centerEnter))
        {
            return centerRay.GetPoint(centerEnter);
        }

        return CameraTransform.position + CameraTransform.forward * Mathf.Max(minDistance, 1f);
    }

    private float NormalizePitch(float eulerX)
    {
        return eulerX > 180f ? eulerX - 360f : eulerX;
    }

    private bool TryCreateScreenRay(Vector2 screenPosition, out Ray ray)
    {
        ray = default;
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
        {
            return false;
        }

        Rect pixelRect = targetCamera.pixelRect;
        if (pixelRect.width <= 0f || pixelRect.height <= 0f)
        {
            return false;
        }

        float clampedX = Mathf.Clamp(screenPosition.x, pixelRect.xMin, pixelRect.xMax);
        float clampedY = Mathf.Clamp(screenPosition.y, pixelRect.yMin, pixelRect.yMax);
        ray = targetCamera.ScreenPointToRay(new Vector2(clampedX, clampedY));
        return true;
    }

}
