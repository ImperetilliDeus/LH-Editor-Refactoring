"""
Create D102 white hinged swing door.

Run in Blender:
    blender --background --python create_d102_white_swing_door.py

Catalog recommendation:
    openingType = Door
    typeKey = D102
    displayName = 화이트 여닫이문
    parametricProfileKey = WHITE_SWING_DOOR_V1
    referenceSize/authoredSize = (9, 21, 0.8)
    minSize = (7, 20, 0.5)
    maxSize = (11, 23, 1.2)
    usesBlenderLocalAxes = true
    fitWidth/fitHeight/fitDepth = false
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


EXPORT_PATH = SCRIPT_DIR / "D102_WhiteSwingDoor_BlenderAxes.fbx"


def create_materials(prefix):
    return {
        "frame": make_material(f"{prefix}_WhiteFrame", (0.93, 0.92, 0.86), 1.0, 0.0, 0.36),
        "slab": make_material(f"{prefix}_WhiteSlab", (0.88, 0.87, 0.81), 1.0, 0.0, 0.40),
        "line": make_material(f"{prefix}_SubtlePanelLine", (0.74, 0.73, 0.68), 1.0, 0.0, 0.46),
        "metal": make_material(f"{prefix}_SatinMetal", (0.70, 0.70, 0.67), 1.0, 0.35, 0.22),
    }


def create_d102_white_swing_door(
    target_width=9.0,
    target_height=21.0,
    target_depth=0.8,
    frame_thickness=0.48,
    frame_depth=0.9,
    slab_depth=0.36,
    bevel=0.035,
    material_prefix="D102_WhiteSwingDoor",
):
    min_width = frame_thickness * 2.0 + 5.0
    min_height = frame_thickness * 2.0 + 12.0
    if target_width < min_width or target_height < min_height or target_depth <= 0:
        raise ValueError("D102 target size is too small.")

    mat = create_materials(material_prefix)
    root = add_root(
        "D102_WhiteSwingDoor_Parametric_ROOT",
        "Door",
        "WHITE_SWING_DOOR_V1",
        target_width,
        target_height,
        target_depth,
    )

    half_w = target_width * 0.5
    half_h = target_height * 0.5
    inner_width = target_width - frame_thickness * 2.0
    inner_height = target_height - frame_thickness * 2.0
    inner_left = -half_w + frame_thickness
    inner_right = half_w - frame_thickness
    slab_y = -target_depth * 0.5 - slab_depth * 0.12

    add_box("Fixed_Frame_Left", (-half_w + frame_thickness * 0.5, 0.0, 0.0), (frame_thickness, frame_depth, target_height), mat["frame"], root, bevel)
    add_box("Fixed_Frame_Right", (half_w - frame_thickness * 0.5, 0.0, 0.0), (frame_thickness, frame_depth, target_height), mat["frame"], root, bevel)
    add_box("Stretch_Frame_Top", (0.0, 0.0, half_h - frame_thickness * 0.5), (target_width, frame_depth, frame_thickness), mat["frame"], root, bevel)
    add_box("Stretch_Frame_Bottom", (0.0, 0.0, -half_h + frame_thickness * 0.5), (target_width, frame_depth, frame_thickness), mat["frame"], root, bevel)

    add_box("Stretch_DoorPanel_MainPanel", (0.0, slab_y, 0.0), (inner_width, slab_depth, inner_height), mat["slab"], root, bevel)

    # Very subtle inset border, like a clean white apartment interior door.
    inset = 0.42
    add_box("Fixed_Trim_Left", (inner_left + inset, slab_y - slab_depth * 0.58, 0.0), (0.055, 0.035, inner_height - inset * 2.0), mat["line"], root, bevel * 0.25)
    add_box("Fixed_Trim_Right", (inner_right - inset, slab_y - slab_depth * 0.58, 0.0), (0.055, 0.035, inner_height - inset * 2.0), mat["line"], root, bevel * 0.25)
    add_box("Stretch_RecessLine_Horizontal_Top", (0.0, slab_y - slab_depth * 0.58, half_h - frame_thickness - inset), (inner_width - inset * 2.0, 0.035, 0.055), mat["line"], root, bevel * 0.25)
    add_box("Stretch_RecessLine_Horizontal_Bottom", (0.0, slab_y - slab_depth * 0.58, -half_h + frame_thickness + inset), (inner_width - inset * 2.0, 0.035, 0.055), mat["line"], root, bevel * 0.25)

    # Right-side knob/lever, fixed size and right-anchored by name.
    knob_x = inner_right - 0.78
    knob_z = -half_h + target_height * 0.47
    knob_y = slab_y - slab_depth * 0.75
    add_cylinder("Fixed_Right_Knob_Round", (knob_x, knob_y, knob_z), 0.20, 0.18, mat["metal"], root, rotation=(math.pi / 2.0, 0.0, 0.0), vertices=32)
    add_cylinder("Fixed_Right_Handle_Lever", (knob_x - 0.48, knob_y - 0.02, knob_z), 0.055, 0.95, mat["metal"], root, rotation=(0.0, math.pi / 2.0, 0.0), vertices=20)

    hinge_x = inner_left + 0.18
    hinge_y = target_depth * 0.5 + 0.12
    for i, t in enumerate((0.22, 0.50, 0.78), start=1):
        hinge_z = -half_h + target_height * t
        add_cylinder(f"Fixed_Hinge_Cylinder_{i:02d}", (hinge_x, hinge_y, hinge_z), 0.13, 0.54, mat["metal"], root, rotation=(math.pi / 2.0, 0.0, 0.0), vertices=20)

    add_box("Stretch_Threshold_FrontStrip", (0.0, target_depth * 0.5 + 0.10, -half_h + 0.10), (target_width, 0.18, 0.10), mat["metal"], root, bevel * 0.3)

    return root


def main():
    clear_scene()
    root = create_d102_white_swing_door()
    add_preview_light_and_camera()
    export_fbx(EXPORT_PATH, root)
    return root


if __name__ == "__main__":
    main()
