using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public sealed class VirtualBoundary : MonoBehaviour
{
    public static event Action BoundariesChanged;
    private static readonly List<VirtualBoundary> registeredBoundaries = new List<VirtualBoundary>();

    [Header("Endpoints")]
    [SerializeField] private Transform startAnchor;
    [SerializeField] private Transform endAnchor;
    [SerializeField] private Vector3 localStartPoint = new Vector3(-1f, 0f, 0f);
    [SerializeField] private Vector3 localEndPoint = new Vector3(1f, 0f, 0f);

    [Header("Behavior")]
    [SerializeField] private bool visibleInTopView = true;
    [SerializeField] private bool previewOnly;
    [SerializeField] private int startVertexId;
    [SerializeField] private int endVertexId;

    [Header("Debug")]
    [SerializeField] private Color gizmoColor = new Color(0.12f, 0.85f, 1f, 1f);
    [SerializeField] private bool drawSceneGizmo = true;

    public bool VisibleInTopView => visibleInTopView;
    public static IReadOnlyList<VirtualBoundary> All => registeredBoundaries;
    public bool IncludeInRoomGraph => !previewOnly;
    public bool PreviewOnly => previewOnly;
    public int StartVertexId => startVertexId;
    public int EndVertexId => endVertexId;
    public Vector3 StartPoint => startAnchor != null ? startAnchor.position : transform.TransformPoint(localStartPoint);
    public Vector3 EndPoint => endAnchor != null ? endAnchor.position : transform.TransformPoint(localEndPoint);
    public Transform GroupRoot
    {
        get
        {
            return transform.parent != null ? transform.parent : transform;
        }
    }

    public bool TryGetResolvedEndpoints(out Vector3 startPoint, out Vector3 endPoint)
    {
        startPoint = StartPoint;
        endPoint = EndPoint;
        return (endPoint - startPoint).sqrMagnitude > 0.000001f;
    }

    public void SetEndpoints(Vector3 worldStart, Vector3 worldEnd)
    {
        if (startAnchor != null)
        {
            startAnchor.position = worldStart;
        }
        else
        {
            localStartPoint = transform.InverseTransformPoint(worldStart);
        }

        if (endAnchor != null)
        {
            endAnchor.position = worldEnd;
        }
        else
        {
            localEndPoint = transform.InverseTransformPoint(worldEnd);
        }

        NotifyBoundariesChanged();
    }

    public void SetPreviewOnly(bool value)
    {
        if (previewOnly == value)
        {
            return;
        }

        previewOnly = value;
        NotifyBoundariesChanged();
    }

    private void OnEnable()
    {
        Register(this);
        NotifyBoundariesChanged();
    }

    private void OnDisable()
    {
        Unregister(this);
        NotifyBoundariesChanged();
    }

    private void OnValidate()
    {
        NotifyBoundariesChanged();
    }

    private void OnDrawGizmos()
    {
        if (!drawSceneGizmo || !TryGetResolvedEndpoints(out Vector3 startPoint, out Vector3 endPoint))
        {
            return;
        }

        Gizmos.color = gizmoColor;
        const int dashCount = 12;
        for (int i = 0; i < dashCount; i += 2)
        {
            float t0 = i / (float)dashCount;
            float t1 = (i + 1) / (float)dashCount;
            Vector3 dashStart = Vector3.Lerp(startPoint, endPoint, t0);
            Vector3 dashEnd = Vector3.Lerp(startPoint, endPoint, t1);
            Gizmos.DrawLine(dashStart, dashEnd);
        }
    }

    private static void NotifyBoundariesChanged()
    {
        BoundariesChanged?.Invoke();
    }

    private static void Register(VirtualBoundary boundary)
    {
        if (boundary == null || registeredBoundaries.Contains(boundary))
        {
            return;
        }

        registeredBoundaries.Add(boundary);
    }

    private static void Unregister(VirtualBoundary boundary)
    {
        if (boundary == null)
        {
            return;
        }

        registeredBoundaries.Remove(boundary);
    }
}
