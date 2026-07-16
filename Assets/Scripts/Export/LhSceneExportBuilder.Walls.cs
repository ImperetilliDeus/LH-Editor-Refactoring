using System.Collections.Generic;
using LH.Schema;
using UnityEngine;

namespace LH.Export
{
    public static partial class LhSceneExportBuilder
    {
        private static List<LhWallDto> BuildWalls(IEnumerable<Wall> walls, BuildContext context)
        {
            List<LhWallDto> results = new List<LhWallDto>();
            if (walls == null)
            {
                return results;
            }

            HashSet<Transform> exportedRoots = new HashSet<Transform>();
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

                int wallId = ResolveWallExportId(root, context);
                context.wallIdsByRoot[root] = wallId;
                RegisterWallDataIds(root, wallId, context);
                results.Add(BuildWall(root, wallId, context.exportMode == ExportMode.LegacyExact));
            }

            return results;
        }

        private static List<LhLegacyWallDto> BuildLegacyWalls(IEnumerable<Wall> walls, BuildContext context)
        {
            List<LhLegacyWallDto> results = new List<LhLegacyWallDto>();
            List<LhWallDto> builtWalls = BuildWalls(walls, context);
            for (int i = 0; i < builtWalls.Count; i++)
            {
                LhWallDto wall = builtWalls[i];
                results.Add(new LhLegacyWallDto
                {
                    name = wall.name,
                    id = wall.id,
                    position = wall.position,
                    angle = wall.angle,
                    scale = wall.scale,
                    texture = wall.texture,
                    segments = wall.segments,
                });
            }

            return results;
        }

        private static LhWallDto BuildWall(Transform root, int wallId, bool legacyExact)
        {
            List<LhWallSegmentDto> segments = new List<LhWallSegmentDto>();
            WallOpeningContainer container = root.GetComponent<WallOpeningContainer>();
            GetExportRootTransform(root, container, out Vector3 exportRootPosition, out Quaternion exportRootRotation, out Vector3 exportRootScale);

            if (container != null)
            {
                BuildContainerSegments(
                    segments,
                    container,
                    exportRootPosition,
                    exportRootRotation,
                    exportRootScale,
                    legacyExact);
            }
            else if (root.TryGetComponent(out Wall wall))
            {
                segments.Add(BuildStandaloneSegment(wall));
            }

            LhDtoFactory.FillTransform(
                exportRootPosition,
                exportRootRotation.eulerAngles,
                exportRootScale,
                out LhVector3Dto position,
                out LhVector3Dto angle,
                out LhVector3Dto scale);
            return new LhWallDto
            {
                name = root.name,
                id = wallId,
                position = position,
                angle = angle,
                scale = scale,
                texture = ResolveWallTextureCode(root),
                segments = segments,
            };
        }

        private static string ResolveWallTextureCode(Transform root)
        {
            string selectedCode = null;
            if (root != null)
            {
                Wall rootWall = root.GetComponent<Wall>();
                if (rootWall != null && rootWall.Data != null)
                {
                    selectedCode = rootWall.Data.TextureCode;
                }

                if (string.IsNullOrWhiteSpace(selectedCode))
                {
                    Wall[] walls = root.GetComponentsInChildren<Wall>(true);
                    for (int i = 0; i < walls.Length; i++)
                    {
                        Wall wall = walls[i];
                        if (wall == null || wall.Data == null || string.IsNullOrWhiteSpace(wall.Data.TextureCode))
                        {
                            continue;
                        }

                        selectedCode = wall.Data.TextureCode;
                        break;
                    }
                }
            }

            return RoomManager.Instance != null
                ? RoomManager.Instance.GetEffectiveWallTextureCode(selectedCode)
                : selectedCode ?? string.Empty;
        }

        private static void BuildContainerSegments(
            List<LhWallSegmentDto> results,
            WallOpeningContainer container,
            Vector3 exportRootPosition,
            Quaternion exportRootRotation,
            Vector3 exportRootScale,
            bool legacyExact)
        {
            if (results == null || container == null)
            {
                return;
            }

            WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
            if (openings == null || openings.Length == 0)
            {
                AddContainerSolidSegment(
                    results,
                    container,
                    null,
                    0f,
                    container.WallLength,
                    exportRootPosition,
                    exportRootRotation,
                    exportRootScale,
                    legacyExact);
                return;
            }

            List<WallOpening> orderedOpenings = new List<WallOpening>(openings.Length);
            for (int i = 0; i < openings.Length; i++)
            {
                if (openings[i] != null)
                {
                    orderedOpenings.Add(openings[i]);
                }
            }

            orderedOpenings.Sort((left, right) => left.CenterDistance.CompareTo(right.CenterDistance));

            float currentDistance = 0f;
            for (int i = 0; i < orderedOpenings.Count; i++)
            {
                WallOpening opening = orderedOpenings[i];
                float openingHalfWidth = opening.Width * 0.5f;
                float openingStart = Mathf.Clamp(opening.CenterDistance - openingHalfWidth, 0f, container.WallLength);
                float openingEnd = Mathf.Clamp(opening.CenterDistance + openingHalfWidth, 0f, container.WallLength);

                AddContainerSolidSegment(
                    results,
                    container,
                    null,
                    currentDistance,
                    openingStart,
                    exportRootPosition,
                    exportRootRotation,
                    exportRootScale,
                    legacyExact);

                AddContainerSolidSegment(
                    results,
                    container,
                    opening,
                    openingStart,
                    openingEnd,
                    exportRootPosition,
                    exportRootRotation,
                    exportRootScale,
                    legacyExact);

                currentDistance = Mathf.Max(currentDistance, openingEnd);
            }

            AddContainerSolidSegment(
                results,
                container,
                null,
                currentDistance,
                container.WallLength,
                exportRootPosition,
                exportRootRotation,
                exportRootScale,
                legacyExact);
        }

        private static void AddContainerSolidSegment(
            List<LhWallSegmentDto> results,
            WallOpeningContainer container,
            WallOpening attachedOpening,
            float startDistance,
            float endDistance,
            Vector3 exportRootPosition,
            Quaternion exportRootRotation,
            Vector3 exportRootScale,
            bool legacyExact)
        {
            if (results == null || container == null)
            {
                return;
            }

            float segmentLength = endDistance - startDistance;
            if (segmentLength <= 0.0001f)
            {
                return;
            }

            float midpointDistance = (startDistance + endDistance) * 0.5f;
            Vector3 segmentCenter = container.WallStart + container.WallDirection * midpointDistance;
            segmentCenter.y = container.CenterY;
            Quaternion segmentRotation = Quaternion.FromToRotation(Vector3.right, container.WallDirection);
            Vector3 segmentScale = new Vector3(segmentLength, container.WallHeight, container.WallThickness);

            LhVector3Dto position;
            LhVector3Dto angle;
            LhVector3Dto scale;
            if (legacyExact)
            {
                BuildLegacyContainerSegmentTransform(
                    container,
                    attachedOpening,
                    midpointDistance,
                    segmentLength,
                    out position,
                    out angle,
                    out scale);
            }
            else
            {
                FillRelativeTransform(
                    exportRootPosition,
                    exportRootRotation,
                    exportRootScale,
                    segmentCenter,
                    segmentRotation,
                    segmentScale,
                    out position,
                    out angle,
                    out scale);
            }

            results.Add(new LhWallSegmentDto
            {
                position = position,
                angle = angle,
                scale = scale,
                hasInterior = attachedOpening != null,
                door = attachedOpening != null && attachedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door
                    ? BuildDoor(attachedOpening, exportRootPosition, exportRootRotation, exportRootScale, legacyExact)
                    : null,
                window = attachedOpening != null && attachedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Window
                    ? BuildWindow(attachedOpening, exportRootPosition, exportRootRotation, exportRootScale, legacyExact)
                    : null,
            });
        }

        private static void RemoveOverlappingOpeningBaseSegments(List<(Transform root, Wall wall)> orderedSegments, WallOpeningContainer container)
        {
            if (orderedSegments == null || orderedSegments.Count < 2 || container == null)
            {
                return;
            }

            WallOpening[] openings = container.GetComponentsInChildren<WallOpening>(true);
            if (openings == null || openings.Length == 0)
            {
                return;
            }

            for (int i = orderedSegments.Count - 1; i >= 0; i--)
            {
                (Transform root, Wall wall) candidate = orderedSegments[i];
                if (candidate.wall == null || candidate.wall.Data == null)
                {
                    continue;
                }

                if (candidate.root != null && candidate.root.GetComponentInChildren<WallOpening>(true) != null)
                {
                    continue;
                }

                float candidateStart = GetDistanceAlongContainer(container, candidate.wall.Data.startPoint);
                float candidateEnd = GetDistanceAlongContainer(container, candidate.wall.Data.endPoint);
                NormalizeSegmentRange(ref candidateStart, ref candidateEnd);

                bool removedForOpeningOverlap = false;
                for (int openingIndex = 0; openingIndex < openings.Length; openingIndex++)
                {
                    WallOpening opening = openings[openingIndex];
                    if (opening == null)
                    {
                        continue;
                    }

                    float openingStart = opening.CenterDistance - opening.Width * 0.5f;
                    float openingEnd = opening.CenterDistance + opening.Width * 0.5f;
                    float overlap = GetSegmentOverlap(candidateStart, candidateEnd, openingStart, openingEnd);
                    if (overlap <= 0.0001f)
                    {
                        continue;
                    }

                    float candidateLength = Mathf.Max(candidateEnd - candidateStart, 0f);
                    float overlapRatio = candidateLength > 0.0001f ? overlap / candidateLength : 0f;
                    if (overlapRatio < 0.5f)
                    {
                        continue;
                    }

                    orderedSegments.RemoveAt(i);
                    removedForOpeningOverlap = true;
                    break;
                }

                if (removedForOpeningOverlap)
                {
                    continue;
                }

                for (int j = 0; j < orderedSegments.Count; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }

                    (Transform root, Wall wall) reference = orderedSegments[j];
                    if (reference.wall == null || reference.wall.Data == null || reference.root == null)
                    {
                        continue;
                    }

                    if (reference.root.GetComponentInChildren<WallOpening>(true) == null)
                    {
                        continue;
                    }

                    float referenceStart = GetDistanceAlongContainer(container, reference.wall.Data.startPoint);
                    float referenceEnd = GetDistanceAlongContainer(container, reference.wall.Data.endPoint);
                    NormalizeSegmentRange(ref referenceStart, ref referenceEnd);

                    if (!ApproximatelyEqual(candidateStart, referenceStart, 0.0001f) ||
                        !ApproximatelyEqual(candidateEnd, referenceEnd, 0.0001f))
                    {
                        continue;
                    }

                    orderedSegments.RemoveAt(i);
                    break;
                }
            }
        }

        private static LhWallSegmentDto BuildStandaloneSegment(Wall wall)
        {
            LhDtoFactory.FillTransform(Vector3.zero, Vector3.zero, Vector3.one, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);
            return new LhWallSegmentDto
            {
                position = position,
                angle = angle,
                scale = scale,
                hasInterior = false,
                door = null,
                window = null,
            };
        }

        private static LhWallSegmentDto BuildSegmentForContainer(
            Transform segmentRoot,
            Wall segmentWall,
            WallOpeningContainer container,
            Vector3 exportRootPosition,
            Quaternion exportRootRotation,
            Vector3 exportRootScale,
            bool legacyExact,
            HashSet<WallOpening> consumedOpenings)
        {
            WallOpening attachedOpening = FindOpeningForSegment(segmentRoot, segmentWall, container, consumedOpenings);
            bool hasInterior = attachedOpening != null;
            LhVector3Dto position;
            LhVector3Dto angle;
            LhVector3Dto scale;
            if (legacyExact)
            {
                BuildLegacySegmentTransform(
                    segmentWall,
                    container,
                    attachedOpening,
                    out position,
                    out angle,
                    out scale);
            }
            else
            {
                FillRelativeTransform(
                    exportRootPosition,
                    exportRootRotation,
                    exportRootScale,
                    segmentWall.transform.position,
                    segmentWall.transform.rotation,
                    segmentWall.transform.lossyScale,
                    out position,
                    out angle,
                    out scale);
            }

            return new LhWallSegmentDto
            {
                position = position,
                angle = angle,
                scale = scale,
                hasInterior = hasInterior,
                door = attachedOpening != null && attachedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door
                    ? BuildDoor(attachedOpening, exportRootPosition, exportRootRotation, exportRootScale, legacyExact)
                    : null,
                window = attachedOpening != null && attachedOpening.Type == WallOpeningPlacementManager.OpeningPlacementType.Window
                    ? BuildWindow(attachedOpening, exportRootPosition, exportRootRotation, exportRootScale, legacyExact)
                    : null,
                };
        }

        private static void BuildLegacyContainerSegmentTransform(
            WallOpeningContainer container,
            WallOpening attachedOpening,
            float midpointDistance,
            float segmentLength,
            out LhVector3Dto position,
            out LhVector3Dto angle,
            out LhVector3Dto scale)
        {
            if (container == null)
            {
                LhDtoFactory.FillTransform(Vector3.zero, Vector3.zero, Vector3.one, out position, out angle, out scale);
                return;
            }

            float wallLength = Mathf.Max(container.WallLength, 0.000001f);
            float wallThickness = Mathf.Max(container.WallThickness, 0.000001f);
            float legacySpan = Mathf.Max(wallLength - wallThickness, 0.000001f);
            float halfLengthOffset = midpointDistance - wallLength * 0.5f;
            float depthRatio = attachedOpening != null
                ? SafeDivide(attachedOpening.Depth, wallThickness)
                : 1f;

            LhDtoFactory.FillTransform(
                new Vector3(SafeDivide(halfLengthOffset, legacySpan), 0f, 0f),
                Vector3.zero,
                new Vector3(SafeDivide(segmentLength, legacySpan), 1f, depthRatio),
                out position,
                out angle,
                out scale);
        }

        private static void BuildLegacySegmentTransform(
            Wall segmentWall,
            WallOpeningContainer container,
            WallOpening attachedOpening,
            out LhVector3Dto position,
            out LhVector3Dto angle,
            out LhVector3Dto scale)
        {
            if (segmentWall == null || segmentWall.Data == null || container == null)
            {
                LhDtoFactory.FillTransform(Vector3.zero, Vector3.zero, Vector3.one, out position, out angle, out scale);
                return;
            }

            float wallLength = Mathf.Max(container.WallLength, 0.000001f);
            float wallThickness = Mathf.Max(container.WallThickness, 0.000001f);
            float legacySpan = Mathf.Max(wallLength - wallThickness, 0.000001f);
            Vector3 direction = container.WallDirection;

            Vector3 start = segmentWall.Data.startPoint;
            Vector3 end = segmentWall.Data.endPoint;
            Vector3 midpoint = (start + end) * 0.5f;
            float halfLengthOffset = Vector3.Dot(midpoint - ((container.WallStart + container.WallEnd) * 0.5f), direction);
            float segmentLength = Vector3.Distance(
                new Vector3(start.x, 0f, start.z),
                new Vector3(end.x, 0f, end.z));
            if (attachedOpening != null)
            {
                Vector3 openingCenter = container.WallStart + direction * attachedOpening.CenterDistance;
                halfLengthOffset = Vector3.Dot(openingCenter - ((container.WallStart + container.WallEnd) * 0.5f), direction);
                segmentLength = attachedOpening.Width;
            }

            float depthRatio = attachedOpening != null
                ? SafeDivide(attachedOpening.Depth, wallThickness)
                : 1f;

            LhDtoFactory.FillTransform(
                new Vector3(SafeDivide(halfLengthOffset, legacySpan), 0f, 0f),
                Vector3.zero,
                new Vector3(SafeDivide(segmentLength, legacySpan), 1f, depthRatio),
                out position,
                out angle,
                out scale);
        }

        private static LhDoorDto BuildDoor(
            WallOpening opening,
            Vector3 exportRootPosition,
            Quaternion exportRootRotation,
            Vector3 exportRootScale,
            bool legacyExact)
        {
            if (legacyExact)
            {
                LhDtoFactory.FillTransform(Vector3.zero, Vector3.zero, Vector3.one, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);
                return new LhDoorDto
                {
                    isExist = true,
                    code = string.IsNullOrWhiteSpace(opening.DoorTypeKey) ? "Pass" : opening.DoorTypeKey,
                    position = position,
                    angle = angle,
                    scale = scale,
                    parametricProfileKey = ResolveOpeningParametricProfile(opening),
                    authoredSize = LhVector3Dto.FromVector3(ResolveOpeningAuthoredSize(opening)),
                    width = opening.Width,
                    height = opening.Height,
                    depth = opening.Depth,
                    bottomY = opening.BottomY,
                };
            }

            FillRelativeTransform(
                exportRootPosition,
                exportRootRotation,
                exportRootScale,
                opening.transform.position,
                opening.transform.rotation,
                opening.transform.lossyScale,
                out LhVector3Dto relativePosition,
                out LhVector3Dto relativeAngle,
                out LhVector3Dto relativeScale);
            return new LhDoorDto
            {
                isExist = true,
                code = string.IsNullOrWhiteSpace(opening.DoorTypeKey) ? "Door" : opening.DoorTypeKey,
                position = relativePosition,
                angle = relativeAngle,
                scale = relativeScale,
                parametricProfileKey = ResolveOpeningParametricProfile(opening),
                authoredSize = LhVector3Dto.FromVector3(ResolveOpeningAuthoredSize(opening)),
                width = opening.Width,
                height = opening.Height,
                depth = opening.Depth,
                bottomY = opening.BottomY,
            };
        }

        private static LhWindowDto BuildWindow(
            WallOpening opening,
            Vector3 exportRootPosition,
            Quaternion exportRootRotation,
            Vector3 exportRootScale,
            bool legacyExact)
        {
            string windowCode = ResolveWindowExportCode(opening.WindowTypeKey);
            if (legacyExact)
            {
                LhDtoFactory.FillTransform(Vector3.zero, Vector3.zero, Vector3.one, out LhVector3Dto position, out LhVector3Dto angle, out LhVector3Dto scale);
                return new LhWindowDto
                {
                    isExist = true,
                    code = windowCode,
                    position = position,
                    angle = angle,
                    scale = scale,
                    parametricProfileKey = ResolveOpeningParametricProfile(opening),
                    authoredSize = LhVector3Dto.FromVector3(ResolveOpeningAuthoredSize(opening)),
                    width = opening.Width,
                    height = opening.Height,
                    depth = opening.Depth,
                    bottomY = opening.BottomY,
                };
            }

            FillRelativeTransform(
                exportRootPosition,
                exportRootRotation,
                exportRootScale,
                opening.transform.position,
                opening.transform.rotation,
                opening.transform.lossyScale,
                out LhVector3Dto relativePosition,
                out LhVector3Dto relativeAngle,
                out LhVector3Dto relativeScale);
            return new LhWindowDto
            {
                isExist = true,
                code = windowCode,
                position = relativePosition,
                angle = relativeAngle,
                scale = relativeScale,
                parametricProfileKey = ResolveOpeningParametricProfile(opening),
                authoredSize = LhVector3Dto.FromVector3(ResolveOpeningAuthoredSize(opening)),
                width = opening.Width,
                height = opening.Height,
                depth = opening.Depth,
                bottomY = opening.BottomY,
            };
        }

        private static string ResolveOpeningParametricProfile(WallOpening opening)
        {
            if (!TryResolveOpeningCatalogItem(opening, out OpeningTypeCatalogItem item) ||
                item == null ||
                !item.UseParametricModel)
            {
                return string.Empty;
            }

            return item.ParametricProfileKey ?? string.Empty;
        }

        private static Vector3 ResolveOpeningAuthoredSize(WallOpening opening)
        {
            if (TryResolveOpeningCatalogItem(opening, out OpeningTypeCatalogItem item) &&
                item != null &&
                item.UseParametricModel &&
                item.AuthoredSize.x > 0f &&
                item.AuthoredSize.y > 0f &&
                item.AuthoredSize.z > 0f)
            {
                return item.AuthoredSize;
            }

            return Vector3.zero;
        }

        private static bool TryResolveOpeningCatalogItem(WallOpening opening, out OpeningTypeCatalogItem item)
        {
            item = null;
            if (opening == null)
            {
                return false;
            }

            OpeningTypeCatalog catalog = Resources.Load<OpeningTypeCatalog>("OpeningTypeCatalog");
            if (catalog == null || catalog.Items == null)
            {
                return false;
            }

            string key = opening.Type == WallOpeningPlacementManager.OpeningPlacementType.Door
                ? opening.DoorTypeKey
                : opening.WindowTypeKey;
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            IReadOnlyList<OpeningTypeCatalogItem> items = catalog.Items;
            for (int i = 0; i < items.Count; i++)
            {
                OpeningTypeCatalogItem candidate = items[i];
                if (candidate == null ||
                    candidate.OpeningType != opening.Type ||
                    !string.Equals(candidate.TypeKey, key, System.StringComparison.Ordinal))
                {
                    continue;
                }

                item = candidate;
                return true;
            }

            return false;
        }

        private static string ResolveWindowExportCode(string windowTypeKey)
        {
            if (string.IsNullOrWhiteSpace(windowTypeKey) ||
                string.Equals(windowTypeKey, "Window", System.StringComparison.Ordinal) ||
                string.Equals(windowTypeKey, "\uCC3D\uBB38", System.StringComparison.Ordinal))
            {
                return "W001";
            }

            return windowTypeKey;
        }
    }
}
