"""
Create D103 white glass-frame door/window-style opening, registered as Door.

Run in Blender:
    blender --background --python create_d103_white_glass_frame_door.py

Catalog recommendation:
    openingType = Door
    typeKey = D103
    displayName = 화이트 창호형 도어
    parametricProfileKey = WHITE_GLASS_FRAME_DOOR_V1
    referenceSize/authoredSize = (18, 21, 1)
    minSize = (12, 18, 0.5)
    maxSize = (24, 24, 1.5)
    usesBlenderLocalAxes = true
    fitWidth/fitHeight/fitDepth = false
"""

from pathlib import Path
import sys

SCRIPT_DIR = Path(r"E:\Unity\LH Editor_Refactoring\tools\Blender")
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from create_entrance_door_reference_unity_axis import (
    add_box,
    add_preview_light_and_camera,
    add_root,
    clear_scene,
    export_fbx,
    make_material,
)


EXPORT_PATH = SCRIPT_DIR / "D103_WhiteGlassFrameDoor_BlenderAxes.fbx"


def create_materials(prefix):
    return {
        "frame": make_material(f"{prefix}_WhiteAluminumFrame", (0.86, 0.88, 0.84), 1.0, 0.0, 0.30),
        "glass": make_material(f"{prefix}_ClearSoftGlass", (0.66, 0.84, 0.94), 0.34, 0.0, 0.06),
        "seal": make_material(f"{prefix}_LightGraySeal", (0.60, 0.62, 0.60), 1.0, 0.0, 0.45),
    }


def create_d103_white_glass_frame_door(
    target_width=18.0,
    target_height=21.0,
    target_depth=1.0,
    frame_thickness=0.55,
    frame_depth=0.90,
    mullion_width=0.38,
    glass_depth=0.08,
    bevel=0.045,
    material_prefix="D103_WhiteGlassFrameDoor",
):
    min_width = frame_thickness * 2.0 + mullion_width + 5.0
    min_height = frame_thickness * 2.0 + 10.0
    if target_width < min_width or target_height < min_height or target_depth <= 0:
        raise ValueError("D103 target size is too small.")

    mat = create_materials(material_prefix)
    root = add_root(
        "D103_WhiteGlassFrameDoor_Parametric_ROOT",
        "Door",
        "WHITE_GLASS_FRAME_DOOR_V1",
        target_width,
        target_height,
        target_depth,
    )

    half_w = target_width * 0.5
    half_h = target_height * 0.5
    inner_width = target_width - frame_thickness * 2.0
    inner_height = target_height - frame_thickness * 2.0
    glass_y = -target_depth * 0.5 - glass_depth * 0.12

    add_box("Fixed_Frame_Left", (-half_w + frame_thickness * 0.5, 0.0, 0.0), (frame_thickness, frame_depth, target_height), mat["frame"], root, bevel)
    add_box("Fixed_Frame_Right", (half_w - frame_thickness * 0.5, 0.0, 0.0), (frame_thickness, frame_depth, target_height), mat["frame"], root, bevel)
    add_box("Stretch_Frame_Top", (0.0, 0.0, half_h - frame_thickness * 0.5), (target_width, frame_depth, frame_thickness), mat["frame"], root, bevel)
    add_box("Stretch_Frame_Bottom", (0.0, 0.0, -half_h + frame_thickness * 0.5), (target_width, frame_depth, frame_thickness), mat["frame"], root, bevel)

    pane_width = (inner_width - mullion_width) * 0.5
    left_x = -mullion_width * 0.5 - pane_width * 0.5
    right_x = mullion_width * 0.5 + pane_width * 0.5
    add_box("Stretch_DoorPanel_LeftGlass", (left_x, glass_y, 0.0), (pane_width, glass_depth, inner_height), mat["glass"], root, bevel * 0.25)
    add_box("Stretch_DoorPanel_RightGlass", (right_x, glass_y, 0.02), (pane_width, glass_depth, inner_height), mat["glass"], root, bevel * 0.25)

    add_box("Fixed_VerticalTrim_CenterMullion", (0.0, glass_y - 0.08, 0.0), (mullion_width, frame_depth * 0.65, inner_height), mat["frame"], root, bevel)

    # Inner sash lines. These remain thin while pane area changes.
    sash = 0.15
    for index, x in enumerate((left_x, right_x), start=1):
        add_box(f"Stretch_RecessLine_Horizontal_DoorPanel_{index:02d}_Top", (x, glass_y - 0.12, half_h - frame_thickness - 0.55), (pane_width, sash, sash), mat["seal"], root, bevel * 0.25)
        add_box(f"Stretch_RecessLine_Horizontal_DoorPanel_{index:02d}_Bottom", (x, glass_y - 0.12, -half_h + frame_thickness + 0.55), (pane_width, sash, sash), mat["seal"], root, bevel * 0.25)

    add_box("Fixed_Right_Handle_Slim", (right_x - pane_width * 0.42, glass_y - 0.28, -half_h + target_height * 0.50), (0.14, 0.16, 1.25), mat["seal"], root, bevel * 0.35)
    add_box("Stretch_Threshold_FrontStrip", (0.0, target_depth * 0.5 + 0.12, -half_h + 0.12), (target_width, 0.18, 0.11), mat["seal"], root, bevel * 0.25)

    return root


def main():
    clear_scene()
    root = create_d103_white_glass_frame_door()
    add_preview_light_and_camera()
    export_fbx(EXPORT_PATH, root)
    return root


if __name__ == "__main__":
    main()
