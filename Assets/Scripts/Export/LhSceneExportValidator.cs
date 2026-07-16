using System.Collections.Generic;
using LH.Schema;
using UnityEngine;

namespace LH.Export
{
    public sealed class LhSceneExportValidationResult
    {
        public LhSceneExportValidationResult(List<string> errors)
        {
            Errors = errors ?? new List<string>();
        }

        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; }
    }

    public static class LhSceneExportValidator
    {
        public static LhSceneExportValidationResult ValidateLegacy(LhLegacySceneDto scene)
        {
            List<string> errors = new List<string>();

            ValidateLegacyWalls(scene.wallData, errors);
            ValidateLegacyRooms(scene.roomData, CollectWallIds(scene.wallData), errors);

            return new LhSceneExportValidationResult(errors);
        }

        private static HashSet<int> CollectWallIds(List<LhLegacyWallDto> walls)
        {
            HashSet<int> ids = new HashSet<int>();
            if (walls == null)
            {
                return ids;
            }

            for (int i = 0; i < walls.Count; i++)
            {
                if (walls[i].id > 0)
                {
                    ids.Add(walls[i].id);
                }
            }

            return ids;
        }

        private static void ValidateLegacyWalls(List<LhLegacyWallDto> walls, List<string> errors)
        {
            if (walls == null || walls.Count == 0)
            {
                errors.Add("wallData is empty.");
                return;
            }

            for (int i = 0; i < walls.Count; i++)
            {
                LhLegacyWallDto wall = walls[i];
                string path = $"wallData[{i}]";
                if (wall.id <= 0)
                {
                    errors.Add($"{path} '{wall.name}' has invalid id.");
                }

                if (wall.segments == null || wall.segments.Count == 0)
                {
                    errors.Add($"{path} '{wall.name}' has no segments.");
                    continue;
                }

                for (int segmentIndex = 0; segmentIndex < wall.segments.Count; segmentIndex++)
                {
                    ValidateWallSegment(wall.segments[segmentIndex], $"{path}.segments[{segmentIndex}]", errors);
                }
            }
        }

        private static void ValidateWallSegment(LhWallSegmentDto segment, string path, List<string> errors)
        {
            ValidateNonZeroVector(segment.scale, $"{path}.scale", errors);

            bool hasDoor = segment.door != null && segment.door.isExist;
            bool hasWindow = segment.window != null && segment.window.isExist;
            if (segment.hasInterior && !hasDoor && !hasWindow)
            {
                errors.Add($"{path} hasInterior is true but has no existing door or window.");
            }

            if (hasDoor)
            {
                ValidateOpening(segment.door, $"{path}.door", errors);
            }

            if (hasWindow)
            {
                ValidateOpening(segment.window, $"{path}.window", errors);
            }
        }

        private static void ValidateOpening(LhDoorDto opening, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(opening.code))
            {
                errors.Add($"{path} is missing code.");
            }

            ValidateNonZeroVector(opening.scale, $"{path}.scale", errors);
        }

        private static void ValidateOpening(LhWindowDto opening, string path, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(opening.code))
            {
                errors.Add($"{path} is missing code.");
            }

            ValidateNonZeroVector(opening.scale, $"{path}.scale", errors);
        }

        private static void ValidateLegacyRooms(List<LhLegacyRoomDto> rooms, HashSet<int> wallIds, List<string> errors)
        {
            if (rooms == null || rooms.Count == 0)
            {
                errors.Add("roomData is empty.");
                return;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                LhLegacyRoomDto room = rooms[i];
                string path = $"roomData[{i}]";
                string roomLabel = string.IsNullOrWhiteSpace(room.name) ? "(unnamed)" : room.name;

                if (string.IsNullOrWhiteSpace(room.code))
                {
                    errors.Add($"{path} '{roomLabel}' is missing code.");
                }

                if (room.walls == null || room.walls.Count == 0)
                {
                    errors.Add($"{path} '{roomLabel}' has no wall references.");
                }
                else
                {
                    for (int wallIndex = 0; wallIndex < room.walls.Count; wallIndex++)
                    {
                        int wallId = room.walls[wallIndex];
                        if (wallIds == null || !wallIds.Contains(wallId))
                        {
                            errors.Add($"{path}.walls[{wallIndex}] references missing wall id {wallId}.");
                        }
                    }
                }

                ValidateSurface(room.floor, $"{path}.floor", errors);
                ValidateSurface(room.ceil, $"{path}.ceil", errors);
                ValidateLegacyFurniture(room.furnish, path, errors);
            }
        }

        private static void ValidateSurface(LhSurfaceDto surface, string path, List<string> errors)
        {
            ValidateNonZeroVector(surface.scale, $"{path}.scale", errors);

            if (surface.meshType == 0)
            {
                return;
            }

            if (surface.mesh.vertices == null)
            {
                errors.Add($"{path} custom mesh has no vertices list.");
            }

            if (surface.mesh.triangles == null)
            {
                errors.Add($"{path} custom mesh has no triangles list.");
            }

            if (surface.mesh.normals == null)
            {
                errors.Add($"{path} custom mesh has no normals list.");
            }

            if (surface.mesh.uvs == null)
            {
                errors.Add($"{path} custom mesh has no uvs list.");
            }

            if (surface.mesh.triangles != null && surface.mesh.triangles.Count % 3 != 0)
            {
                errors.Add($"{path} custom mesh triangle list length is not divisible by 3.");
            }

            if (surface.mesh.vertices != null && surface.mesh.normals != null && surface.mesh.normals.Count != 0 && surface.mesh.normals.Count != surface.mesh.vertices.Count)
            {
                errors.Add($"{path} custom mesh normals count does not match vertices count.");
            }

            if (surface.mesh.vertices != null && surface.mesh.uvs != null && surface.mesh.uvs.Count != 0 && surface.mesh.uvs.Count != surface.mesh.vertices.Count)
            {
                errors.Add($"{path} custom mesh uvs count does not match vertices count.");
            }
        }

        private static void ValidateLegacyFurniture(List<LhLegacyFurnitureDto> furniture, string roomPath, List<string> errors)
        {
            if (furniture == null)
            {
                errors.Add($"{roomPath}.furnish is missing.");
                return;
            }

            for (int i = 0; i < furniture.Count; i++)
            {
                LhLegacyFurnitureDto item = furniture[i];
                string path = $"{roomPath}.furnish[{i}]";
                if (string.IsNullOrWhiteSpace(item.code))
                {
                    errors.Add($"{path} is missing code.");
                }

                ValidateNonZeroVector(item.scale, $"{path}.scale", errors);

                if (item.defects == null)
                {
                    errors.Add($"{path}.defects is missing.");
                    continue;
                }

                for (int defectIndex = 0; defectIndex < item.defects.Count; defectIndex++)
                {
                    LhFurnitureDefectDto defect = item.defects[defectIndex];
                    string defectPath = $"{path}.defects[{defectIndex}]";
                    if (string.IsNullOrWhiteSpace(defect.mntnCd))
                    {
                        errors.Add($"{defectPath} is missing mntnCd.");
                    }

                    if (string.IsNullOrWhiteSpace(defect.locCd))
                    {
                        errors.Add($"{defectPath} is missing locCd.");
                    }

                    if (string.IsNullOrWhiteSpace(defect.mtrlCd))
                    {
                        errors.Add($"{defectPath} is missing mtrlCd.");
                    }
                }
            }
        }

        private static void ValidateNonZeroVector(LhVector3Dto value, string path, List<string> errors)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z))
            {
                errors.Add($"{path} contains NaN.");
                return;
            }

            if (Mathf.Approximately(value.x, 0f) ||
                Mathf.Approximately(value.y, 0f) ||
                Mathf.Approximately(value.z, 0f))
            {
                errors.Add($"{path} contains zero.");
            }
        }
    }
}
