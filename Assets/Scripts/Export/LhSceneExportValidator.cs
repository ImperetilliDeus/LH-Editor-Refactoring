using System.Collections.Generic;
using LH.Schema;

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
            ValidateLegacyRooms(scene.roomData, errors);

            return new LhSceneExportValidationResult(errors);
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
                }
            }
        }

        private static void ValidateLegacyRooms(List<LhLegacyRoomDto> rooms, List<string> errors)
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

                ValidateSurface(room.floor, $"{path}.floor", errors);
                ValidateSurface(room.ceil, $"{path}.ceil", errors);
                ValidateLegacyFurniture(room.furnish, path, errors);
            }
        }

        private static void ValidateSurface(LhSurfaceDto surface, string path, List<string> errors)
        {
            if (surface.meshType != 0 && surface.mesh.vertices == null)
            {
                errors.Add($"{path} custom mesh has no vertices list.");
            }

            if (surface.meshType != 0 && surface.mesh.triangles == null)
            {
                errors.Add($"{path} custom mesh has no triangles list.");
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
    }
}
