"""
Create E003 single entrance door following the current parametric door rules.

Run in Blender:
    blender --background --python create_single_entrance_door_e003.py

Profile:
    SINGLE_ENTRANCE_DOOR_V1

Authoring convention:
    Blender local X = opening width
    Blender local Y = depth
    Blender local Z = opening height
    1 Unity unit = 100mm

Unity catalog:
    typeKey = E003
    referenceSize/authoredSize = (9.5, 21, 1)
    minSize = (9, 20, 0.5)
    maxSize = (11, 22, 1.5)
    usesBlenderLocalAxes = true
    fitWidth/fitHeight/fitDepth = false

Structure:
    - no side panel
    - frame thickness is fixed
    - lock, handle, hinge, closer are fixed
    - only the slab center, top/bottom frame, seals, threshold stretch
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
    create_materials,
    export_fbx,
)


EXPORT_PATH = SCRIPT_DIR / "E003_SingleEntranceDoor_BlenderAxes.fbx"


def create_single_entrance_door_e003(
    target_width=9.5,
    target_height=21.0,
    target_depth=1.0,
    frame_thickness=0.55,
    frame_depth=1.15,
    slab_depth=0.42,
    trim_width=0.16,
    lock_width=0.72,
    lock_height=2.3,
    handle_length=1.15,
    handle_height_ratio=0.46,
    hinge_count=3,
    bevel=0.045,
    material_prefix="E003_SingleEntranceDoor",
):
    if target_width < frame_thickness * 2.0 + 6.5:
        raise ValueError("target_width is too small for a single entrance door.")

    mat = create_materials(material_prefix)
    root = add_root(
        "E003_SingleEntranceDoor_Parametric_ROOT",
        "Door",
        "SINGLE_ENTRANCE_DOOR_V1",
        target_width,
        target_height,
        target_depth,
    )

    half_w = target_width * 0.5
    half_h = target_height * 0.5
    frame_y = 0.0
    door_face_y = -target_depth * 0.5 - slab_depth * 0.25

    inner_width = target_width - frame_thickness * 2.0
    inner_height = target_height - frame_thickness * 2.0
    inner_left = -half_w + frame_thickness
    inner_right = half_w - frame_thickness
    inner_bottom = -half_h + frame_thickness
    inner_top = half_h - frame_thickness

    add_box("Fixed_Frame_Left", (-half_w + frame_thickness * 0.5, frame_y, 0.0), (frame_thickness, frame_depth, target_height), mat["outer_frame"], root, bevel)
    add_box("Fixed_Frame_Right", (half_w - frame_thickness * 0.5, frame_y, 0.0), (frame_thickness, frame_depth, target_height), mat["outer_frame"], root, bevel)
    add_box("Stretch_Frame_Top", (0.0, frame_y, half_h - frame_thickness * 0.5), (target_width, frame_depth, frame_thickness), mat["outer_frame"], root, bevel)
    add_box("Stretch_Frame_Bottom", (0.0, frame_y, -half_h + frame_thickness * 0.5), (target_width, frame_depth, frame_thickness), mat["outer_frame"], root, bevel)

    seal = trim_width * 0.55
    add_box("Fixed_Seal_Left", (inner_left + seal * 0.5, door_face_y - 0.03, 0.0), (seal, seal, inner_height), mat["rubber"], root, bevel * 0.4)
    add_box("Fixed_Seal_Right", (inner_right - seal * 0.5, door_face_y - 0.03, 0.0), (seal, seal, inner_height), mat["rubber"], root, bevel * 0.4)
    add_box("Stretch_Seal_Top", (0.0, door_face_y - 0.03, inner_top - seal * 0.5), (inner_width, seal, seal), mat["rubber"], root, bevel * 0.4)
    add_box("Stretch_Seal_Bottom", (0.0, door_face_y - 0.03, inner_bottom + seal * 0.5), (inner_width, seal, seal), mat["rubber"], root, bevel * 0.4)

    add_box("Stretch_DoorLeaf_Center", (0.0, door_face_y, 0.0), (inner_width, slab_depth, inner_height), mat["dark_slab"], root, bevel * 0.55)

    edge_trim_y = door_face_y - slab_depth * 0.55
    add_box("Fixed_EdgeTrim_Left", (inner_left + trim_width * 0.5, edge_trim_y, 0.0), (trim_width, trim_width, inner_height), mat["trim"], root, bevel * 0.35)
    add_box("Fixed_EdgeTrim_Right", (inner_right - trim_width * 0.5, edge_trim_y, 0.0), (trim_width, trim_width, inner_height), mat["trim"], root, bevel * 0.35)

    seam_z = -half_h + target_height * 0.52
    add_box("Stretch_RecessLine_Horizontal_Center", (0.0, door_face_y - slab_depth * 0.62, seam_z), (inner_width * 0.78, 0.035, 0.035), mat["line"], root, bevel * 0.2)

    plaque_z = -half_h + target_height * 0.68
    add_box("Fixed_DoorPlaque_Upper", (inner_right - inner_width * 0.33, door_face_y - slab_depth * 0.9, plaque_z), (1.05, 0.08, 0.42), mat["label"], root, bevel * 0.35)

    lock_x = inner_left + inner_width * 0.24
    lock_z = -half_h + target_height * handle_height_ratio
    lock_y = door_face_y - slab_depth * 1.1
    add_box("Fixed_Lock_BackPlate", (lock_x, lock_y, lock_z), (lock_width, 0.22, lock_height), mat["metal"], root, bevel)
    add_box("Fixed_Lock_Keypad", (lock_x, lock_y - 0.13, lock_z + lock_height * 0.19), (lock_width * 0.72, 0.08, lock_height * 0.55), mat["keypad"], root, bevel * 0.8)
    add_box("Fixed_Lock_LowerCover", (lock_x, lock_y - 0.13, lock_z - lock_height * 0.30), (lock_width * 0.62, 0.06, lock_height * 0.22), mat["label"], root, bevel * 0.5)

    hub_radius = 0.18
    add_cylinder("Fixed_Handle_RoundHub", (lock_x + lock_width * 0.42, lock_y - 0.22, lock_z - lock_height * 0.38), hub_radius, 0.16, mat["metal"], root, rotation=(math.pi / 2.0, 0.0, 0.0), vertices=32)
    add_cylinder("Fixed_Handle_Lever", (lock_x + lock_width * 0.42 + handle_length * 0.5, lock_y - 0.22, lock_z - lock_height * 0.38), 0.075, handle_length, mat["metal"], root, rotation=(0.0, math.pi / 2.0, 0.0), vertices=24)

    hinge_x = inner_right - frame_thickness * 0.35
    hinge_y = target_depth * 0.5 + 0.18
    usable_h = target_height - frame_thickness * 2.0
    for i in range(max(2, int(hinge_count))):
        t = (i + 1) / (hinge_count + 1)
        hinge_z = inner_bottom + usable_h * t
        add_cylinder(f"Fixed_Hinge_Cylinder_{i + 1:02d}", (hinge_x, hinge_y, hinge_z), 0.20, 0.72, mat["metal"], root, rotation=(math.pi / 2.0, 0.0, 0.0), vertices=24)
        add_box(f"Fixed_Hinge_Cap_{i + 1:02d}", (hinge_x, hinge_y - 0.05, hinge_z), (0.52, 0.22, 0.32), mat["metal"], root, bevel * 0.55)

    add_box("Fixed_TopCloser_RightBlock", (inner_right - frame_thickness * 0.35, target_depth * 0.5 + 0.26, half_h - frame_thickness * 0.62), (0.85, 0.32, 0.35), mat["metal"], root, bevel * 0.55)
    add_box("Stretch_Threshold_FrontStrip", (0.0, target_depth * 0.5 + 0.14, -half_h + frame_thickness * 0.22), (target_width, 0.24, 0.13), mat["metal"], root, bevel * 0.35)

    return root


def main():
    clear_scene()
    root = create_single_entrance_door_e003()
    add_preview_light_and_camera()
    export_fbx(EXPORT_PATH, root)
    return root


if __name__ == "__main__":
    main()
