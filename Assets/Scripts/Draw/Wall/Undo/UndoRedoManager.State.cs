using UnityEngine;

public partial class UndoRedoManager
{
    public struct WallStateSnapshot
    {
        public GameObject wallObject;
        public WallData wallData;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public string name;
        public Material sharedMaterial;
        public Material topMaterial;
        public int startVertexId;
        public int endVertexId;
        public bool suppressStartHandle;
        public bool suppressEndHandle;
        public bool startSplitPoint;
        public bool endSplitPoint;

        public static WallStateSnapshot Capture(GameObject wallObject)
        {
            if (wallObject == null)
            {
                return default;
            }

            Transform wallTransform = wallObject.transform;
            Wall wallComponent = wallObject.GetComponent<Wall>();
            MeshRenderer renderer = wallObject.GetComponent<MeshRenderer>();

            return new WallStateSnapshot
            {
                wallObject = wallObject,
                position = wallTransform.position,
                rotation = wallTransform.rotation,
                scale = wallTransform.localScale,
                name = wallObject.name,
                sharedMaterial = renderer != null ? renderer.sharedMaterial : null,
                topMaterial = wallComponent != null ? wallComponent.GetTopMaterial() : null,
                wallData = wallComponent != null ? wallComponent.Data.Clone() : null,
                startVertexId = wallComponent != null ? wallComponent.StartVertexId : 0,
                endVertexId = wallComponent != null ? wallComponent.EndVertexId : 0,
                suppressStartHandle = wallComponent != null && wallComponent.SuppressStartHandle,
                suppressEndHandle = wallComponent != null && wallComponent.SuppressEndHandle,
                startSplitPoint = wallComponent != null && wallComponent.IsStartSplitPoint,
                endSplitPoint = wallComponent != null && wallComponent.IsEndSplitPoint,
            };
        }

        public static WallStateSnapshot Capture(GameObject wallObject, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            WallStateSnapshot snapshot = Capture(wallObject);
            snapshot.position = position;
            snapshot.rotation = rotation;
            snapshot.scale = scale;

            if (wallObject != null)
            {
                Wall wallComponent = wallObject.GetComponent<Wall>();
                if (wallComponent != null)
                {
                    Transform wallTransform = wallObject.transform;
                    Vector3 originalPosition = wallTransform.position;
                    Quaternion originalRotation = wallTransform.rotation;
                    Vector3 originalScale = wallTransform.localScale;
                    WallData originalWallData = wallComponent.Data.Clone();

                    wallTransform.SetPositionAndRotation(position, rotation);
                    wallTransform.localScale = scale;
                    wallComponent.SyncEndpointsFromTransform(wallComponent.Data.startPoint.y);
                    snapshot.wallData = wallComponent.Data.Clone();

                    wallTransform.SetPositionAndRotation(originalPosition, originalRotation);
                    wallTransform.localScale = originalScale;
                    wallComponent.CopyDataFrom(originalWallData);
                }
            }

            return snapshot;
        }

        public static bool HasMeaningfulDelta(WallStateSnapshot before, WallStateSnapshot after)
        {
            bool moved = (after.position - before.position).sqrMagnitude > PositionEpsilonSqr;
            bool scaled = (after.scale - before.scale).sqrMagnitude > ScaleEpsilonSqr;
            bool endpointsChanged =
                !AreWallDataEquivalent(before.wallData, after.wallData) ||
                after.startVertexId != before.startVertexId ||
                after.endVertexId != before.endVertexId ||
                after.suppressStartHandle != before.suppressStartHandle ||
                after.suppressEndHandle != before.suppressEndHandle ||
                after.startSplitPoint != before.startSplitPoint ||
                after.endSplitPoint != before.endSplitPoint;
            float rotationDot = Mathf.Abs(Quaternion.Dot(before.rotation, after.rotation));
            bool rotated = rotationDot < RotationEpsilonDot;
            return moved || scaled || rotated || endpointsChanged;
        }

        private static bool AreWallDataEquivalent(WallData left, WallData right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return
                (left.startPoint - right.startPoint).sqrMagnitude <= PositionEpsilonSqr &&
                (left.endPoint - right.endPoint).sqrMagnitude <= PositionEpsilonSqr &&
                Mathf.Abs(left.thickness - right.thickness) <= 0.0001f &&
                Mathf.Abs(left.height - right.height) <= 0.0001f &&
                Mathf.Abs(left.centerY - right.centerY) <= 0.0001f;
        }
    }

    private struct WallReference
    {
        public string name;
        public Vector3 startPoint;
        public Vector3 endPoint;
        public int startVertexId;
        public int endVertexId;
    }

    public struct WallTransformRecord
    {
        public GameObject wallObject;
        public Vector3 beforePosition;
        public Quaternion beforeRotation;
        public Vector3 beforeScale;
        public Vector3 afterPosition;
        public Quaternion afterRotation;
        public Vector3 afterScale;
    }

    public struct WallStateChangeRecord
    {
        public WallStateSnapshot before;
        public WallStateSnapshot after;
    }

    public struct OpeningStateSnapshot
    {
        public WallOpeningPlacementManager.OpeningPlacementType type;
        public string doorTypeKey;
        public string windowTypeKey;
        public bool doorOpensRight;
        public bool doorVerticalFlip;
        public float centerDistance;
        public float width;
        public float height;
        public float depth;
        public float bottomY;
    }

    public struct OpeningLayoutSnapshot
    {
        public bool hasContainer;
        public string layoutName;
        public WallStateSnapshot wallSnapshot;
        public Vector3 wallStart;
        public Vector3 wallEnd;
        public float wallThickness;
        public float wallHeight;
        public float centerY;
        public Material wallMaterial;
        public Material wallTopMaterial;
        public int outerStartVertexId;
        public int outerEndVertexId;
        public bool suppressOuterStartHandle;
        public bool suppressOuterEndHandle;
        public OpeningStateSnapshot[] openings;

        public static bool HasMeaningfulDelta(OpeningLayoutSnapshot before, OpeningLayoutSnapshot after)
        {
            if (before.hasContainer != after.hasContainer)
            {
                return true;
            }

            if (!before.hasContainer && !after.hasContainer)
            {
                return WallStateSnapshot.HasMeaningfulDelta(before.wallSnapshot, after.wallSnapshot);
            }

            if (before.layoutName != after.layoutName ||
                (before.wallStart - after.wallStart).sqrMagnitude > PositionEpsilonSqr ||
                (before.wallEnd - after.wallEnd).sqrMagnitude > PositionEpsilonSqr ||
                Mathf.Abs(before.wallThickness - after.wallThickness) > 0.0001f ||
                Mathf.Abs(before.wallHeight - after.wallHeight) > 0.0001f ||
                Mathf.Abs(before.centerY - after.centerY) > 0.0001f ||
                before.wallTopMaterial != after.wallTopMaterial ||
                before.outerStartVertexId != after.outerStartVertexId ||
                before.outerEndVertexId != after.outerEndVertexId ||
                before.suppressOuterStartHandle != after.suppressOuterStartHandle ||
                before.suppressOuterEndHandle != after.suppressOuterEndHandle)
            {
                return true;
            }

            int beforeCount = before.openings != null ? before.openings.Length : 0;
            int afterCount = after.openings != null ? after.openings.Length : 0;
            if (beforeCount != afterCount)
            {
                return true;
            }

            for (int i = 0; i < beforeCount; i++)
            {
                OpeningStateSnapshot left = before.openings[i];
                OpeningStateSnapshot right = after.openings[i];
                if (left.type != right.type ||
                    left.doorTypeKey != right.doorTypeKey ||
                    left.windowTypeKey != right.windowTypeKey ||
                    left.doorOpensRight != right.doorOpensRight ||
                    left.doorVerticalFlip != right.doorVerticalFlip ||
                    Mathf.Abs(left.centerDistance - right.centerDistance) > 0.0001f ||
                    Mathf.Abs(left.width - right.width) > 0.0001f ||
                    Mathf.Abs(left.height - right.height) > 0.0001f ||
                    Mathf.Abs(left.depth - right.depth) > 0.0001f ||
                    Mathf.Abs(left.bottomY - right.bottomY) > 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
