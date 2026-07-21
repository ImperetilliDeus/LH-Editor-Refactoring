"""
Create D101 white one-sliding translucent door.

Run in Blender:
    blender --background --python create_d101_white_one_sliding_door.py

Catalog recommendation:
    openingType = Door
    typeKey = D101
    displayName = 화이트 원슬라이딩 도어
    parametricProfileKey = WHITE_ONE_SLIDING_DOOR_V1
    referenceSize/authoredSize = (9, 21, 0.8)
    minSize = (7, 20, 0.5)
    maxSize = (12, 24, 1.2)
    usesBlenderLocalAxes = true
    fitWidth/fitHeight/fitDepth = false

Blender local axes:
    X = width, Y = depth, Z = height
    1 Unity unit = 100mm
"""

from pathlib import Path
import math
import sys

SCRIPT_DIR = Path(r"E:\Unity\LH Editor_Refactoring\tools\Blender")
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from create_entrance_door_reference_unity_axis import (
    add_box,
    add_cylinder,
    add_preview_light_and_camera,
    add_root,
    clear_scene,
    export_fbx,
    make_material,
)


EXPORT_PATH = SCRIPT_DIR / "D101_WhiteOneSlidingDoor_BlenderAxes.fbx"


def create_materials(prefix):
    return {
        "frame": make_material(f"{prefix}_SoftWhiteFrame", (0.92, 0.91, 0.86), 1.0, 0.0, 0.34),
        "glass": make_material(f"{prefix}_FrostedGlass", (0.82, 0.92, 0.96), 0.36, 0.0, 0.08),
        "seal": make_material(f"{prefix}_PaleSeal", (0.74, 0.75, 0.72), 1.0, 0.0, 0.42),
        "metal": make_material(f"{prefix}_SatinMetal", (0.70, 0.72, 0.70), 1.0, 0.25, 0.24),
    }


def create_d101_white_one_sliding_door(
    target_width=9.0,
    target_height=21.0,
    target_depth=0.8,
    frame_thickness=0.42,
    frame_depth=0.75,
    glass_depth=0.12,
    handle_height=8.2,
    bevel=0.04,
    material_prefix="D101_WhiteOneSlidingDoor",
):
    min_width = frame_thickness * 2.0 + 4.2
    min_height = frame_thickness * 2.0 + 12.0
    if target_width < min_width or target_height < min_height or target_depth <= 0:
        raise ValueError("D101 target size is too small.")

    mat = create_materials(material_prefix)
    root = add_root(
        "D101_WhiteOneSlidingDoor_Parametric_ROOT",
        "Door",
        "WHITE_ONE_SLIDING_DOOR_V1",
        target_width,
        target_height,
        target_depth,
    )

    half_w = target_width * 0.5
    half_h = target_height * 0.5
    inner_width = target_width - frame_thickness * 2.0
    inner_height = target_height - frame_thickness * 2.0
    glass_y = -target_depth * 0.5 - glass_depth * 0.15

    add_box("Fixed_Frame_Left", (-half_w + frame_thickness * 0.5, 0.0, 0.0), (frame_thickness, frame_depth, target_height), mat["frame"], root, bevel)
    add_box("Fixed_Frame_Right", (half_w - frame_thickness * 0.5, 0.0, 0.0), (frame_thickness, frame_depth, target_height), mat["frame"], root, bevel)
    add_box("Stretch_Frame_Top", (0.0, 0.0, half_h - frame_thickness * 0.5), (target_width, frame_depth, frame_thickness), mat["frame"], root, bevel)
    add_box("Stretch_Frame_Bottom", (0.0, 0.0, -half_h + frame_thickness * 0.5), (target_width, frame_depth, frame_thickness), mat["frame"], root, bevel)

    add_box("Stretch_DoorPanel_TranslucentGlass", (0.0, glass_y, 0.0), (inner_width, glass_depth, inner_height), mat["glass"], root, bevel * 0.35)

    rail_height = 0.18
    add_box("Stretch_Seal_Top", (0.0, glass_y - 0.08, half_h - frame_thickness - rail_height * 0.5), (inner_width, rail_height, rail_height), mat["seal"], root, bevel * 0.35)
    add_box("Stretch_Seal_Bottom", (0.0, glass_y - 0.08, -half_h + frame_thickness + rail_height * 0.5), (inner_width, rail_height, rail_height), mat["seal"], root, bevel * 0.35)

    handle_x = -half_w + frame_thickness + 0.55
    handle_z = -half_h + target_height * 0.48
    add_cylinder(
        "Fixed_Handle_VerticalBar",
        (handle_x, glass_y - 0.34, handle_z),
        0.085,
        handle_height,
        mat["metal"],
        root,
        rotation=(0.0, 0.0, 0.0),
        vertices=24,
    )
    add_box("Fixed_Handle_TopCap", (handle_x, glass_y - 0.34, handle_z + handle_height * 0.5), (0.34, 0.18, 0.18), mat["metal"], root, bevel * 0.55)
    add_box("Fixed_Handle_BottomCap", (handle_x, glass_y - 0.34, handle_z - handle_height * 0.5), (0.34, 0.18, 0.18), mat["metal"], root, bevel * 0.55)

    # Thin sliding guide visible at the floor and ceiling. Thickness remains fixed.
    add_box("Stretch_Threshold_FrontStrip", (0.0, target_depth * 0.5 + 0.12, -half_h + 0.12), (target_width, 0.20, 0.12), mat["metal"], root, bevel * 0.35)
    add_box("Stretch_TopGuide_Rail", (0.0, target_depth * 0.5 + 0.10, half_h - 0.12), (target_width, 0.18, 0.12), mat["metal"], root, bevel * 0.35)

    return root


def main():
    clear_scene()
    root = create_d101_white_one_sliding_door()
    add_preview_light_and_camera()
    export_fbx(EXPORT_PATH, root)
    return root


if __name__ == "__main__":
    main()
