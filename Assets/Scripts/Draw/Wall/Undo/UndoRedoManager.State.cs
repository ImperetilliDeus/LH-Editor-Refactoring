using System.Collections.Generic;
using UnityEngine;

public partial class UndoRedoManager
{
    public struct RoomPolygonSnapshot
    {
        public Room room;
        public List<Vector3> vertices;

        public static RoomPolygonSnapshot Capture(Room room, IReadOnlyList<Vector3> sourceVertices)
        {
            return new RoomPolygonSnapshot
            {
                room = room,
                vertices = sourceVertices != null ? Room.CreateSanitizedPolygonCopy(sourceVertices) : new List<Vector3>(),
            };
        }

        public static bool HasMeaningfulDelta(RoomPolygonSnapshot before, RoomPolygonSnapshot after)
        {
            int beforeCount = before.vertices != null ? before.vertices.Count : 0;
            int afterCount = after.vertices != null ? after.vertices.Count : 0;
            if (beforeCount != afterCount)
            {
                return true;
            }

            for (int i = 0; i < beforeCount; i++)
            {
                if ((before.vertices[i] - after.vertices[i]).sqrMagnitude > PositionEpsilonSqr)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public struct WallStateSnapshot
    {
        public GameObject wallObject;
        public WallData wallData;
        public string name;
        public WallVisualState visualState;
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

            Wall wallComponent = wallObject.GetComponent<Wall>();

            return new WallStateSnapshot
            {
                wallObject = wallObject,
                name = wallObject.name,
                visualState = WallVisualState.Capture(wallObject),
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
            if (wallObject != null)
            {
                Wall wallComponent = wallObject.GetComponent<Wall>();
                if (wallComponent != null)
                {
                    float planeY = wallComponent.Data.startPoint.y;
                    float halfLength = scale.z * 0.5f;
                    Vector3 direction = rotation * Vector3.forward;

                    Vector3 start = position - direction * halfLength;
                    Vector3 end = position + direction * halfLength;
                    start.y = planeY;
                    end.y = planeY;

                    snapshot.wallData = new WallData
                    {
                        id = wallComponent.Data.id,
                        startPoint = start,
                        endPoint = end,
                        thickness = scale.x,
                        height = scale.y,
                        centerY = position.y,
                    };
                }
            }

            return snapshot;
        }

        public static bool HasMeaningfulDelta(WallStateSnapshot before, WallStateSnapshot after)
        {
            bool endpointsChanged =
                !AreWallDataEquivalent(before.wallData, after.wallData) ||
                after.startVertexId != before.startVertexId ||
                after.endVertexId != before.endVertexId ||
                after.suppressStartHandle != before.suppressStartHandle ||
                after.suppressEndHandle != before.suppressEndHandle ||
                after.startSplitPoint != before.startSplitPoint ||
                after.endSplitPoint != before.endSplitPoint;
            return endpointsChanged;
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

    public struct OpeningLayoutChangeRecord
    {
        public OpeningLayoutSnapshot before;
        public OpeningLayoutSnapshot after;
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
        public WallVisualState visualState;
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
                before.visualState.wallMaterial != after.visualState.wallMaterial ||
                before.visualState.topMaterial != after.visualState.topMaterial ||
                Mathf.Abs(before.visualState.topFaceOffset - after.visualState.topFaceOffset) > 0.0001f ||
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
