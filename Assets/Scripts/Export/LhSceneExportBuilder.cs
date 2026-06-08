using System.Collections.Generic;
using LH.Schema;
using UnityEngine;

namespace LH.Export
{
    public static partial class LhSceneExportBuilder
    {
        public enum ExportMode
        {
            Extended = 0,
            LegacyExact = 1,
        }

        private const int CurrentSchemaVersion = 1;
        private const float DefaultFloorWorldY = 0.1f;
        private const float LegacyFloorWorldY = 0.01f;

        private sealed class BuildContext
        {
            public readonly Dictionary<Transform, int> wallIdsByRoot = new Dictionary<Transform, int>();
            public readonly Dictionary<string, int> wallIdsByDataId = new Dictionary<string, int>();
            public readonly Dictionary<string, WallData> wallDataById = new Dictionary<string, WallData>();
            public readonly Dictionary<Room, List<FurnitureInstance>> furnitureByRoom = new Dictionary<Room, List<FurnitureInstance>>();
            public ExportMode exportMode = ExportMode.Extended;
            public int nextWallId = 1;
        }

        public static LhSceneDto Build(Vector3 startPoint, IEnumerable<Wall> walls, IEnumerable<Room> rooms)
        {
            List<Room> roomList = CollectRooms(rooms);
            BuildContext context = new BuildContext
            {
                exportMode = ExportMode.Extended,
            };

            PrimeFurnitureLookup(roomList, context);
            return new LhSceneDto
            {
                version = CurrentSchemaVersion,
                startPoint = LhVector3Dto.FromVector3(startPoint),
                wallData = BuildWalls(walls, context),
                roomData = BuildRooms(roomList, context),
            };
        }

        public static LhLegacySceneDto BuildLegacy(Vector3 startPoint, IEnumerable<Wall> walls, IEnumerable<Room> rooms)
        {
            List<Room> roomList = CollectRooms(rooms);
            BuildContext context = new BuildContext
            {
                exportMode = ExportMode.LegacyExact,
            };

            PrimeFurnitureLookup(roomList, context);
            return new LhLegacySceneDto
            {
                startPoint = LhVector3Dto.FromVector3(startPoint),
                wallData = BuildLegacyWalls(walls, context),
                roomData = BuildLegacyRooms(roomList, context),
            };
        }

        public static List<string> CollectLegacyWarnings(IEnumerable<Wall> walls)
        {
            List<string> warnings = new List<string>();
            HashSet<Transform> exportedRoots = new HashSet<Transform>();
            if (walls != null)
            {
                foreach (Wall wall in walls)
                {
                    if (wall == null)
                    {
                        continue;
                    }

                    Transform root = GetWallExportRoot(wall.transform);
                    if (root == null || !exportedRoots.Add(root))
                    {
                        continue;
                    }

                    if (!TryParseWallNameId(root, out _))
                    {
                        warnings.Add($"Legacy export warning: wall root '{root.name}' does not contain a numeric suffix, so fallback ids may differ from 55A-style data.");
                    }
                }
            }

            return warnings;
        }

        private static WallOpening FindOpeningForSegment(Transform segmentRoot, Wall segmentWall, WallOpeningContainer container, HashSet<WallOpening> consumedOpenings)
        {
            if (segmentWall == null || container == null)
            {
                return null;
            }

            if (segmentRoot != null)
            {
                WallOpening directOpening = segmentRoot.GetComponentInChildren<WallOpening>(true);
                if (directOpening != null &&
                    (consumedOpenings == null || !consumedOpenings.Contains(directOpening)))
                {
                    consumedOpenings?.Add(directOpening);
                    return directOpening;
                }
            }

            float segmentMin = GetDistanceAlongContainer(container, segmentWall.Data.startPoint);
            float segmentMax = GetDistanceAlongContainer(container, segmentWall.Data.endPoint);
            if (segmentMin > segmentMax)
            {
                float swap = segmentMin;
                segmentMin = segmentMax;
                segmentMax = swap;
            }

            WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
            for (int i = 0; i < openings.Length; i++)
            {
                WallOpening opening = openings[i];
                if (opening == null)
                {
                    continue;
                }

                if (consumedOpenings != null && consumedOpenings.Contains(opening))
                {
                    continue;
                }

                float openingStart = opening.CenterDistance - opening.Width * 0.5f;
                float openingEnd = opening.CenterDistance + opening.Width * 0.5f;
                if (openingEnd + 0.0001f < segmentMin || openingStart - 0.0001f > segmentMax)
                {
                    continue;
                }

                consumedOpenings?.Add(opening);

                return opening;
            }

            return null;
        }

        private static float GetDistanceAlongContainer(WallOpeningContainer container, Vector3 point)
        {
            Vector3 fromStart = point - container.WallStart;
            return Vector3.Dot(fromStart, container.WallDirection);
        }

        private static void NormalizeSegmentRange(ref float start, ref float end)
        {
            if (start <= end)
            {
                return;
            }

            float swap = start;
            start = end;
            end = swap;
        }

        private static float GetSegmentOverlap(float leftStart, float leftEnd, float rightStart, float rightEnd)
        {
            float overlapStart = Mathf.Max(leftStart, rightStart);
            float overlapEnd = Mathf.Min(leftEnd, rightEnd);
            return Mathf.Max(0f, overlapEnd - overlapStart);
        }

        private static bool ApproximatelyEqual(float left, float right, float tolerance)
        {
            return Mathf.Abs(left - right) <= tolerance;
        }

        private static float GetSegmentSortDistance(WallOpeningContainer container, Wall segmentWall)
        {
            if (container == null || segmentWall == null || segmentWall.Data == null)
            {
                return 0f;
            }

            float startDistance = GetDistanceAlongContainer(container, segmentWall.Data.startPoint);
            float endDistance = GetDistanceAlongContainer(container, segmentWall.Data.endPoint);
            return Mathf.Min(startDistance, endDistance);
        }

        private static Vector3 CalculateSurfaceScale(IReadOnlyList<Vector3> boundaryVertices)
        {
            if (boundaryVertices == null || boundaryVertices.Count == 0)
            {
                return Vector3.one;
            }

            Vector3 min = boundaryVertices[0];
            Vector3 max = boundaryVertices[0];
            for (int i = 1; i < boundaryVertices.Count; i++)
            {
                Vector3 vertex = boundaryVertices[i];
                min = Vector3.Min(min, vertex);
                max = Vector3.Max(max, vertex);
            }

            Vector3 size = max - min;
            if (size.x <= 0.0001f)
            {
                size.x = 1f;
            }

            if (size.z <= 0.0001f)
            {
                size.z = 1f;
            }

            return new Vector3(size.x, 1f, size.z);
        }

        private static void FillRelativeTransform(Transform root, Transform target, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale)
        {
            if (root == null || target == null)
            {
                position = default;
                angle = default;
                scale = default;
                return;
            }

            LhDtoFactory.FillTransform(
                root.InverseTransformPoint(target.position),
                (Quaternion.Inverse(root.rotation) * target.rotation).eulerAngles,
                target.localScale,
                out position,
                out angle,
                out scale);
        }

        private static void FillRelativeTransform(
            Vector3 rootPosition,
            Quaternion rootRotation,
            Vector3 rootScale,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Vector3 targetScale,
            out LhVector3Dto position,
            out LhVector3Dto angle,
            out LhVector3Dto scale)
        {
            Vector3 offset = Quaternion.Inverse(rootRotation) * (targetPosition - rootPosition);
            Vector3 relativePosition = new Vector3(
                SafeDivide(offset.x, rootScale.x),
                SafeDivide(offset.y, rootScale.y),
                SafeDivide(offset.z, rootScale.z));
            Vector3 relativeScale = new Vector3(
                SafeDivide(targetScale.x, rootScale.x),
                SafeDivide(targetScale.y, rootScale.y),
                SafeDivide(targetScale.z, rootScale.z));

            LhDtoFactory.FillTransform(
                relativePosition,
                (Quaternion.Inverse(rootRotation) * targetRotation).eulerAngles,
                relativeScale,
                out position,
                out angle,
                out scale);
        }

        private static void GetExportRootTransform(
            Transform root,
            WallOpeningContainer container,
            out Vector3 position,
            out Quaternion rotation,
            out Vector3 scale)
        {
            if (container == null)
            {
                if (root != null && root.TryGetComponent(out Wall wall) && wall.Data != null)
                {
                    Vector3 wallStart = wall.Data.startPoint;
                    Vector3 wallEnd = wall.Data.endPoint;
                    Vector3 wallDirection = wallEnd - wallStart;
                    wallDirection.y = 0f;
                    float wallLength = wallDirection.magnitude;
                    if (wallLength <= 0.000001f)
                    {
                        wallDirection = Vector3.right;
                        wallLength = 0f;
                    }
                    else
                    {
                        wallDirection /= wallLength;
                    }

                    position = (wallStart + wallEnd) * 0.5f;
                    position.y = wall.Data.centerY;
                    rotation = Quaternion.FromToRotation(Vector3.right, wallDirection);
                    scale = new Vector3(wallLength, wall.Data.height, wall.Data.thickness);
                    return;
                }

                position = root != null ? root.position : Vector3.zero;
                rotation = root != null ? root.rotation : Quaternion.identity;
                scale = root != null ? root.lossyScale : Vector3.one;
                return;
            }

            Vector3 start = container.WallStart;
            Vector3 end = container.WallEnd;
            Vector3 direction = end - start;
            direction.y = 0f;
            float length = direction.magnitude;
            if (length <= 0.000001f)
            {
                direction = Vector3.forward;
                length = 0f;
            }
            else
            {
                direction /= length;
            }

            position = (start + end) * 0.5f;
            position.y = container.CenterY;
            rotation = Quaternion.FromToRotation(Vector3.right, direction);
            scale = new Vector3(length, container.WallHeight, container.WallThickness);
        }

        private static float SafeDivide(float numerator, float denominator)
        {
            return Mathf.Abs(denominator) > 0.000001f ? numerator / denominator : 0f;
        }

        private static string SanitizeIdToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "item";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (char.IsLetterOrDigit(current))
                {
                    builder.Append(char.ToLowerInvariant(current));
                    continue;
                }

                if (builder.Length == 0 || builder[builder.Length - 1] == '_')
                {
                    continue;
                }

                builder.Append('_');
            }

            return builder.Length > 0 ? builder.ToString().Trim('_') : "item";
        }

        private static Transform GetWallExportRoot(Transform wallTransform)
        {
            if (wallTransform == null)
            {
                return null;
            }

            WallOpeningContainer container = wallTransform.GetComponentInParent<WallOpeningContainer>();
            return container != null ? container.transform : wallTransform;
        }

        private static int ResolveWallExportId(Transform root, BuildContext context)
        {
            if (context != null && context.exportMode == ExportMode.LegacyExact && TryParseWallNameId(root, out int parsedId))
            {
                return parsedId;
            }

            int nextWallId = context != null ? context.nextWallId : 1;
            if (context != null)
            {
                context.nextWallId++;
            }

            return nextWallId;
        }

        private static bool TryParseWallNameId(Transform root, out int wallId)
        {
            wallId = 0;
            if (root == null || string.IsNullOrWhiteSpace(root.name))
            {
                return false;
            }

            string name = root.name;
            int digitStart = -1;
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsDigit(name[i]))
                {
                    digitStart = i;
                    break;
                }
            }

            return digitStart >= 0 && int.TryParse(name.Substring(digitStart), out wallId) && wallId > 0;
        }

        private static void RegisterWallDataIds(Transform root, int wallId, BuildContext context)
        {
            if (root == null)
            {
                return;
            }

            Wall rootWall = root.GetComponent<Wall>();
            if (rootWall != null)
            {
                RegisterWallDataId(rootWall.Data, wallId, context);
                return;
            }

            Wall[] childWalls = root.GetComponentsInChildren<Wall>(true);
            for (int i = 0; i < childWalls.Length; i++)
            {
                Wall childWall = childWalls[i];
                if (childWall == null)
                {
                    continue;
                }

                RegisterWallDataId(childWall.Data, wallId, context);
            }
        }

        private static void RegisterWallDataId(WallData wallData, int wallId, BuildContext context)
        {
            if (wallData == null || string.IsNullOrWhiteSpace(wallData.id))
            {
                return;
            }

            context.wallIdsByDataId[wallData.id] = wallId;
            context.wallDataById[wallData.id] = wallData;
        }

        private static float GetCeilingWorldY(Room room, RoomData roomData, BuildContext context)
        {
            float bestY = roomData != null ? roomData.Geometry.Center.y : 0f;
            if (roomData == null)
            {
                return bestY;
            }

            bool foundWallData = false;
            IReadOnlyList<string> wallIds = room != null ? room.EffectiveWallIds : roomData.EffectiveWallIds;
            for (int i = 0; i < wallIds.Count; i++)
            {
                string wallId = wallIds[i];
                if (string.IsNullOrWhiteSpace(wallId) || !context.wallDataById.TryGetValue(wallId, out WallData wallData))
                {
                    continue;
                }

                foundWallData = true;
                float wallTopY = wallData.centerY + wallData.height * 0.5f;
                if (wallTopY > bestY)
                {
                    bestY = wallTopY;
                }
            }

            if (foundWallData || room == null || room.WallSet == null)
            {
                return bestY;
            }

            foreach (Wall wall in room.WallSet)
            {
                if (wall == null || wall.Data == null)
                {
                    continue;
                }

                float wallTopY = wall.Data.centerY + wall.Data.height * 0.5f;
                if (wallTopY > bestY)
                {
                    bestY = wallTopY;
                }
            }

            return bestY;
        }

        private static bool IsLegacyRectSurface(IReadOnlyList<Vector3> boundaryVertices)
        {
            if (boundaryVertices == null)
            {
                return false;
            }

            List<Vector3> sanitized = PolygonUtility.CreateSanitizedPolygonCopy(boundaryVertices);
            if (sanitized.Count != 4)
            {
                return false;
            }

            for (int i = 0; i < sanitized.Count; i++)
            {
                Vector3 current = sanitized[i];
                Vector3 next = sanitized[(i + 1) % sanitized.Count];
                Vector3 edge = next - current;
                edge.y = 0f;
                if (edge.sqrMagnitude <= 0.000001f)
                {
                    return false;
                }

                bool axisAlignedX = Mathf.Abs(edge.x) > 0.0001f && Mathf.Abs(edge.z) <= 0.0001f;
                bool axisAlignedZ = Mathf.Abs(edge.z) > 0.0001f && Mathf.Abs(edge.x) <= 0.0001f;
                if (!axisAlignedX && !axisAlignedZ)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
