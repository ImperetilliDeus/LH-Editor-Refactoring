"""
Create a modern entrance door model from the provided reference photo.

Run in Blender:
    blender --background --python create_entrance_door_reference_unity_axis.py

Blender/FBX authoring convention for doors:
    X = opening width
    Y = opening depth / wall thickness direction
    Z = opening height
    1 Unity unit = 100 mm

Unity catalog convention:
    Reference Size / Authored Size = (width, height, depth)
    Uses Blender Local Axes = true
    Fit Width / Fit Height / Fit Depth = false
    Use Parametric Model = true
    Parametric Profile Key = ENTRANCE_DOOR_LOCKSET_V1

Modeling rule:
    - Root is centered on the opening.
    - Root rotation stays identity.
    - Root scale stays (1, 1, 1).
    - Fixed parts keep real dimensions.
    - Stretch parts are separate objects that resize only on intended axes.
    - Lock, handle, hinge, plaque, closer never scale when width/height changes.
"""

from pathlib import Path
import math
import bpy


SCRIPT_DIR = Path(r"E:\Unity\LH Editor_Refactoring\tools\Blender")
EXPORT_PATH = SCRIPT_DIR / "D001_EntranceDoor_BlenderAxes.fbx"
MIN_SIZE = 0.0001


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_material(name, color, alpha=1.0, metallic=0.0, roughness=0.45):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.blend_method = "BLEND" if alpha < 1.0 else "OPAQUE"
    material.show_transparent_back = True

    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], alpha)
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness

    return material


def cube_mesh(name, size):
    sx = size[0] * 0.5
    sy = size[1] * 0.5
    sz = size[2] * 0.5
    vertices = [
        (-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
        (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz),
    ]
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
    ]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    return mesh


def link_object(obj):
    bpy.context.collection.objects.link(obj)
    return obj


def add_box(name, location, size, material, parent, bevel=0.0):
    if min(size) <= MIN_SIZE:
        raise ValueError(f"{name} has invalid size: {size}")

    obj = bpy.data.objects.new(name, cube_mesh(f"{name}_Mesh", size))
    obj.location = location
    obj.parent = parent
    obj.scale = (1.0, 1.0, 1.0)
    if material is not None:
        obj.data.materials.append(material)
    link_object(obj)

    if bevel > 0.0:
        bevel_mod = obj.modifiers.new("Fixed_Bevel", "BEVEL")
        bevel_mod.width = bevel
        bevel_mod.segments = 2
        bevel_mod.affect = "EDGES"
        obj.modifiers.new("Weighted_Normals", "WEIGHTED_NORMAL")

    return obj


def add_cylinder(name, location, radius, depth, material, parent, rotation=(0.0, 0.0, 0.0), vertices=32):
    if radius <= MIN_SIZE or depth <= MIN_SIZE:
        raise ValueError(f"{name} has invalid cylinder size.")

    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    obj.parent = parent
    obj.scale = (1.0, 1.0, 1.0)
    if material is not None:
        obj.data.materials.append(material)

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)
    return obj


def add_root(name, opening_type, profile_key, width, height, depth):
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 1.0
    root.location = (0.0, 0.0, 0.0)
    root.rotation_euler = (0.0, 0.0, 0.0)
    root.scale = (1.0, 1.0, 1.0)

    root["lh_opening_type"] = opening_type
    root["lh_target_width"] = width
    root["lh_target_height"] = height
    root["lh_target_depth"] = depth
    root["lh_parametric_profile_key"] = profile_key
    root["lh_modeling_rule"] = "blender_axes_fixed_parts_plus_stretch_parts"
    link_object(root)
    return root


def create_materials(prefix):
    return {
        "outer_frame": make_material(f"{prefix}_OuterFrame", (0.10, 0.11, 0.11), 1.0, 0.0, 0.34),
        "dark_slab": make_material(f"{prefix}_DarkDoorSlab", (0.30, 0.30, 0.29), 1.0, 0.0, 0.42),
        "light_panel": make_material(f"{prefix}_LightSidePanel", (0.62, 0.62, 0.59), 1.0, 0.0, 0.38),
        "trim": make_material(f"{prefix}_BlackTrim", (0.05, 0.055, 0.055), 1.0, 0.0, 0.28),
        "metal": make_material(f"{prefix}_Metal", (0.70, 0.72, 0.70), 1.0, 0.35, 0.22),
        "keypad": make_material(f"{prefix}_KeypadBlack", (0.045, 0.05, 0.055), 1.0, 0.05, 0.18),
        "line": make_material(f"{prefix}_PanelLine", (0.18, 0.18, 0.17), 1.0, 0.0, 0.32),
        "label": make_material(f"{prefix}_SmallPlaque", (0.78, 0.80, 0.78), 1.0, 0.2, 0.2),
        "rubber": make_material(f"{prefix}_RubberSeal", (0.025, 0.025, 0.024), 1.0, 0.0, 0.48),
    }


def validate_size(width, height, depth, frame_thickness, lock_width):
    min_width = frame_thickness * 2.0 + lock_width + 2.0
    min_height = frame_thickness * 2.0 + 8.0
    if width < min_width or height < min_height or depth <= 0.0:
        raise ValueError("Door target size is too small for fixed frame, lock set, and panel parts.")


def create_parametric_entrance_door(
    target_width=9.5,
    target_height=21.0,
    target_depth=1.0,
    frame_thickness=0.55,
    frame_depth=1.15,
    slab_depth=0.42,
    side_panel_width=2.25,
    trim_width=0.16,
    reveal_gap=0.08,
    lock_width=0.72,
    lock_height=2.3,
    handle_length=1.15,
    handle_height_ratio=0.46,
    hinge_count=3,
    bevel=0.045,
    material_prefix="D001_EntranceDoor",
):
    """
    Create a reference-style entrance door.

    Authored dimensions are Unity project units:
        9.5 x 21.0 x 1.0 = 950mm x 2100mm x 100mm.

    Blender local bounds:
        X: -width/2  ~ +width/2
        Y: depth direction
        Z: -height/2 ~ +height/2
    """

    validate_size(target_width, target_height, target_depth, frame_thickness, lock_width)
    mat = create_materials(material_prefix)
    root = add_root(
        "D001_EntranceDoor_Parametric_ROOT",
        "Door",
        "ENTRANCE_DOOR_LOCKSET_V1",
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
    if side_panel_width > inner_width * 0.45:
        side_panel_width = inner_width * 0.36
    main_panel_width = inner_width - side_panel_width - reveal_gap

    inner_left = -half_w + frame_thickness
    inner_right = half_w - frame_thickness
    inner_bottom = -half_h + frame_thickness
    inner_top = half_h - frame_thickness
    side_center_x = inner_left + side_panel_width * 0.5
    main_center_x = inner_right - main_panel_width * 0.5

    # Outer frame. Left/right stretch on height only. Top/bottom stretch on width only.
    add_box(
        "Fixed_Frame_Left",
        (-half_w + frame_thickness * 0.5, frame_y, 0.0),
        (frame_thickness, frame_depth, target_height),
        mat["outer_frame"],
        root,
        bevel,
    )
    add_box(
        "Fixed_Frame_Right",
        (half_w - frame_thickness * 0.5, frame_y, 0.0),
        (frame_thickness, frame_depth, target_height),
        mat["outer_frame"],
        root,
        bevel,
    )
    add_box(
        "Stretch_Frame_Top",
        (0.0, frame_y, half_h - frame_thickness * 0.5),
        (target_width, frame_depth, frame_thickness),
        mat["outer_frame"],
        root,
        bevel,
    )
    add_box(
        "Stretch_Frame_Bottom",
        (0.0, frame_y, -half_h + frame_thickness * 0.5),
        (target_width, frame_depth, frame_thickness),
        mat["outer_frame"],
        root,
        bevel,
    )

    # Thin black rubber/reveal line just inside the frame.
    seal = trim_width * 0.55
    add_box(
        "Fixed_Seal_Left",
        (inner_left + seal * 0.5, door_face_y - 0.03, 0.0),
        (seal, seal, inner_height),
        mat["rubber"],
        root,
        bevel * 0.4,
    )
    add_box(
        "Fixed_Seal_Right",
        (inner_right - seal * 0.5, door_face_y - 0.03, 0.0),
        (seal, seal, inner_height),
        mat["rubber"],
        root,
        bevel * 0.4,
    )
    add_box(
        "Stretch_Seal_Top",
        (0.0, door_face_y - 0.03, inner_top - seal * 0.5),
        (inner_width, seal, seal),
        mat["rubber"],
        root,
        bevel * 0.4,
    )
    add_box(
        "Stretch_Seal_Bottom",
        (0.0, door_face_y - 0.03, inner_bottom + seal * 0.5),
        (inner_width, seal, seal),
        mat["rubber"],
        root,
        bevel * 0.4,
    )

    # Door slab surface split like the reference: light narrow left panel and dark large right panel.
    add_box(
        "Stretch_VerticalAccent_LeftPanel",
        (side_center_x, door_face_y, 0.0),
        (side_panel_width, slab_depth, inner_height),
        mat["light_panel"],
        root,
        bevel * 0.55,
    )
    add_box(
        "Stretch_DoorSlab_MainPanel",
        (main_center_x, door_face_y, 0.0),
        (main_panel_width, slab_depth, inner_height),
        mat["dark_slab"],
        root,
        bevel * 0.55,
    )
    add_box(
        "Fixed_VerticalTrim_BetweenPanels",
        (inner_left + side_panel_width + reveal_gap * 0.5, door_face_y - slab_depth * 0.55, 0.0),
        (trim_width, trim_width, inner_height),
        mat["trim"],
        root,
        bevel * 0.35,
    )

    # Horizontal seams are thin fixed-height details. They move with height but never get thicker.
    seam_z = -half_h + target_height * 0.52
    add_box(
        "Stretch_RecessLine_Horizontal_LeftPanel",
        (side_center_x, door_face_y - slab_depth * 0.62, seam_z),
        (side_panel_width * 0.96, 0.035, 0.035),
        mat["line"],
        root,
        bevel * 0.2,
    )
    add_box(
        "Stretch_RecessLine_Horizontal_MainPanel",
        (main_center_x, door_face_y - slab_depth * 0.62, seam_z),
        (main_panel_width * 0.96, 0.035, 0.035),
        mat["line"],
        root,
        bevel * 0.2,
    )

    # Small rectangular plaque on the upper main panel.
    plaque_w = 1.05
    plaque_h = 0.42
    plaque_z = -half_h + target_height * 0.68
    add_box(
        "Fixed_DoorPlaque_UpperMainPanel",
        (main_center_x - main_panel_width * 0.22, door_face_y - slab_depth * 0.9, plaque_z),
        (plaque_w, 0.08, plaque_h),
        mat["label"],
        root,
        bevel * 0.35,
    )

    # Digital lock body and keypad. Fixed size, visually anchored to the lower-left side.
    lock_x = side_center_x - side_panel_width * 0.08
    lock_z = -half_h + target_height * handle_height_ratio
    lock_y = door_face_y - slab_depth * 1.1
    add_box(
        "Fixed_Lock_BackPlate",
        (lock_x, lock_y, lock_z),
        (lock_width, 0.22, lock_height),
        mat["metal"],
        root,
        bevel,
    )
    add_box(
        "Fixed_Lock_Keypad",
        (lock_x, lock_y - 0.13, lock_z + lock_height * 0.19),
        (lock_width * 0.72, 0.08, lock_height * 0.55),
        mat["keypad"],
        root,
        bevel * 0.8,
    )
    add_box(
        "Fixed_Lock_LowerCover",
        (lock_x, lock_y - 0.13, lock_z - lock_height * 0.30),
        (lock_width * 0.62, 0.06, lock_height * 0.22),
        mat["label"],
        root,
        bevel * 0.5,
    )

    # Lever handle: hub + horizontal lever. Cylinder depth axes are rotated into local Y/X.
    hub_radius = 0.18
    add_cylinder(
        "Fixed_Handle_RoundHub",
        (lock_x + lock_width * 0.42, lock_y - 0.22, lock_z - lock_height * 0.38),
        hub_radius,
        0.16,
        mat["metal"],
        root,
        rotation=(math.pi / 2.0, 0.0, 0.0),
        vertices=32,
    )
    add_cylinder(
        "Fixed_Handle_Lever",
        (lock_x + lock_width * 0.42 + handle_length * 0.5, lock_y - 0.22, lock_z - lock_height * 0.38),
        0.075,
        handle_length,
        mat["metal"],
        root,
        rotation=(0.0, math.pi / 2.0, 0.0),
        vertices=24,
    )

    # Right-side hinge caps visible in the reference. Fixed-size, right-anchored.
    hinge_x = half_w - frame_thickness * 0.25
    hinge_y = target_depth * 0.5 + 0.18
    usable_h = target_height - frame_thickness * 2.0
    hinge_count = max(2, int(hinge_count))
    for i in range(hinge_count):
        t = (i + 1) / (hinge_count + 1)
        hinge_z = inner_bottom + usable_h * t
        add_cylinder(
            f"Fixed_Hinge_Cylinder_{i + 1:02d}",
            (hinge_x, hinge_y, hinge_z),
            0.20,
            0.72,
            mat["metal"],
            root,
            rotation=(math.pi / 2.0, 0.0, 0.0),
            vertices=24,
        )
        add_box(
            f"Fixed_Hinge_Cap_{i + 1:02d}",
            (hinge_x, hinge_y - 0.05, hinge_z),
            (0.52, 0.22, 0.32),
            mat["metal"],
            root,
            bevel * 0.55,
        )

    # Top closer/stopper block from the reference photo.
    closer_z = half_h - frame_thickness * 0.62
    add_box(
        "Fixed_TopCloser_RightBlock",
        (half_w - frame_thickness * 0.35, target_depth * 0.5 + 0.26, closer_z),
        (0.85, 0.32, 0.35),
        mat["metal"],
        root,
        bevel * 0.55,
    )

    # Bottom threshold strip.
    add_box(
        "Stretch_Threshold_FrontStrip",
        (0.0, target_depth * 0.5 + 0.14, -half_h + frame_thickness * 0.22),
        (target_width, 0.24, 0.13),
        mat["metal"],
        root,
        bevel * 0.35,
    )

    return root


def export_fbx(filepath, root):
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for child in root.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(filepath),
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        add_leaf_bones=False,
        mesh_smooth_type="FACE",
    )


def add_preview_light_and_camera():
    bpy.ops.object.light_add(type="AREA", location=(0.0, -12.0, 16.0))
    light = bpy.context.object
    light.name = "Preview_Area_Light"
    light.data.energy = 450
    light.data.size = 7.0

    bpy.ops.object.camera_add(
        location=(6.0, -18.0, 7.5),
        rotation=(math.radians(66.0), 0.0, math.radians(20.0)),
    )
    bpy.context.scene.camera = bpy.context.object


def main():
    clear_scene()
    root = create_parametric_entrance_door()
    add_preview_light_and_camera()

    # Uncomment when you want Blender to export immediately.
    # export_fbx(EXPORT_PATH, root)
    return root


if __name__ == "__main__":
    main()
