using UnityEngine;

public static class EditorScreenCoordinateUtility
{
    private const float CoordinateTolerance = 0.5f;

    public static Vector2 NormalizePointerScreenPosition(Vector2 rawPointerPosition)
    {
        return NormalizePointerScreenPosition(rawPointerPosition, GetUnityScreenSize(), GetSystemDisplaySize());
    }

    public static Vector2 NormalizePointerScreenPosition(
        Vector2 rawPointerPosition,
        Vector2 unityScreenSize,
        Vector2 systemDisplaySize)
    {
        if (!IsValidSize(unityScreenSize) || !IsValidSize(systemDisplaySize))
        {
            return rawPointerPosition;
        }

        if (ApproximatelySameSize(unityScreenSize, systemDisplaySize))
        {
            return rawPointerPosition;
        }

        bool exceedsUnityScreen =
            rawPointerPosition.x > unityScreenSize.x + CoordinateTolerance ||
            rawPointerPosition.y > unityScreenSize.y + CoordinateTolerance;
        bool fitsSystemDisplay =
            rawPointerPosition.x >= -CoordinateTolerance &&
            rawPointerPosition.y >= -CoordinateTolerance &&
            rawPointerPosition.x <= systemDisplaySize.x + CoordinateTolerance &&
            rawPointerPosition.y <= systemDisplaySize.y + CoordinateTolerance;

        if (!exceedsUnityScreen || !fitsSystemDisplay)
        {
            return rawPointerPosition;
        }

        return new Vector2(
            rawPointerPosition.x * unityScreenSize.x / systemDisplaySize.x,
            rawPointerPosition.y * unityScreenSize.y / systemDisplaySize.y);
    }

    public static Vector2 ToCameraScreenPoint(Camera camera, Vector2 unityScreenPoint)
    {
        return ToCameraScreenPoint(
            unityScreenPoint,
            GetCameraPixelSize(camera),
            GetUnityScreenSize(),
            ShouldScaleCameraScreenPoints(camera));
    }

    public static Vector2 ToCameraScreenPoint(
        Vector2 unityScreenPoint,
        Vector2 cameraPixelSize,
        Vector2 unityScreenSize,
        bool shouldScale)
    {
        if (!shouldScale || !IsValidSize(cameraPixelSize) || !IsValidSize(unityScreenSize))
        {
            return unityScreenPoint;
        }

        return new Vector2(
            unityScreenPoint.x * cameraPixelSize.x / unityScreenSize.x,
            unityScreenPoint.y * cameraPixelSize.y / unityScreenSize.y);
    }

    public static Vector2 ToUnityScreenPoint(Camera camera, Vector2 cameraScreenPoint)
    {
        return ToUnityScreenPoint(
            cameraScreenPoint,
            GetCameraPixelSize(camera),
            GetUnityScreenSize(),
            ShouldScaleCameraScreenPoints(camera));
    }

    public static Vector2 ToUnityScreenPoint(
        Vector2 cameraScreenPoint,
        Vector2 cameraPixelSize,
        Vector2 unityScreenSize,
        bool shouldScale)
    {
        if (!shouldScale || !IsValidSize(cameraPixelSize) || !IsValidSize(unityScreenSize))
        {
            return cameraScreenPoint;
        }

        return new Vector2(
            cameraScreenPoint.x * unityScreenSize.x / cameraPixelSize.x,
            cameraScreenPoint.y * unityScreenSize.y / cameraPixelSize.y);
    }

    public static Vector3 ToUnityScreenPoint(Camera camera, Vector3 cameraScreenPoint)
    {
        Vector2 point = ToUnityScreenPoint(camera, new Vector2(cameraScreenPoint.x, cameraScreenPoint.y));
        return new Vector3(point.x, point.y, cameraScreenPoint.z);
    }

    public static Ray ScreenPointToRay(Camera camera, Vector2 unityScreenPoint)
    {
        return camera.ScreenPointToRay(ToCameraScreenPoint(camera, unityScreenPoint));
    }

    public static Vector2 ScreenPointToAnchoredPosition(
        RectTransform parentRect,
        Canvas canvas,
        Vector2 screenPoint,
        Camera fallbackCamera)
    {
        if (parentRect == null)
        {
            return screenPoint;
        }

        Camera uiCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = canvas.worldCamera != null ? canvas.worldCamera : fallbackCamera;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, uiCamera, out Vector2 localPoint)
            ? localPoint
            : screenPoint;
    }

    public static Vector4 GetViewportSignature(Camera camera)
    {
        return BuildViewportSignature(GetUnityScreenSize(), GetCameraPixelSize(camera));
    }

    public static Vector4 BuildViewportSignature(Vector2 unityScreenSize, Vector2 cameraPixelSize)
    {
        return new Vector4(
            Mathf.Max(0f, unityScreenSize.x),
            Mathf.Max(0f, unityScreenSize.y),
            Mathf.Max(0f, cameraPixelSize.x),
            Mathf.Max(0f, cameraPixelSize.y));
    }

    public static bool ViewportSignatureChanged(Vector4 previous, Vector4 current)
    {
        return Mathf.Abs(previous.x - current.x) > CoordinateTolerance ||
               Mathf.Abs(previous.y - current.y) > CoordinateTolerance ||
               Mathf.Abs(previous.z - current.z) > CoordinateTolerance ||
               Mathf.Abs(previous.w - current.w) > CoordinateTolerance;
    }

    private static Vector2 GetUnityScreenSize()
    {
        return new Vector2(Screen.width, Screen.height);
    }

    private static Vector2 GetSystemDisplaySize()
    {
        Display display = Display.main;
        if (display == null)
        {
            return GetUnityScreenSize();
        }

        return new Vector2(display.systemWidth, display.systemHeight);
    }

    private static Vector2 GetCameraPixelSize(Camera camera)
    {
        if (camera == null)
        {
            return GetUnityScreenSize();
        }

        if (camera.targetTexture != null)
        {
            return new Vector2(camera.targetTexture.width, camera.targetTexture.height);
        }

        return new Vector2(camera.pixelWidth, camera.pixelHeight);
    }

    private static bool ShouldScaleCameraScreenPoints(Camera camera)
    {
        if (camera == null)
        {
            return false;
        }

        Vector2 cameraPixelSize = GetCameraPixelSize(camera);
        Vector2 unityScreenSize = GetUnityScreenSize();
        return camera.targetTexture != null || !ApproximatelySameSize(cameraPixelSize, unityScreenSize);
    }

    private static bool IsValidSize(Vector2 size)
    {
        return size.x > 0f && size.y > 0f;
    }

    private static bool ApproximatelySameSize(Vector2 left, Vector2 right)
    {
        return Mathf.Abs(left.x - right.x) <= CoordinateTolerance &&
               Mathf.Abs(left.y - right.y) <= CoordinateTolerance;
    }
}
