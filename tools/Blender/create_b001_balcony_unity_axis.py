"""
Create B001 balcony window using the final Unity opening convention.

Run in Blender:
    blender --background --python create_b001_balcony_unity_axis.py

Final Unity convention:
    X = opening width
    Y = opening height
    Z = opening depth
    1 Unity unit = 100 mm

This script intentionally bakes B001 at 1800 x 2100 x 140 mm as
18 x 21 x 1.4 Unity units. The exported prefab must not be non-uniformly
scaled in Unity.
"""

from pathlib import Path
import bpy


SCRIPT_DIR = Path(r"E:\Unity\LH Editor_Refactoring\tools\Blender")
EXPORT_PATH = SCRIPT_DIR / "B001_Balcony_UnityAxis.fbx"
MIN_SIZE = 0.0001


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def make_material(name, color, alpha=1.0, roughness=0.45):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.blend_method = "BLEND" if alpha < 1.0 else "OPAQUE"
    material.show_transparent_back = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], alpha)
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Roughness"].default_value = roughness
    return material


def cube_mesh(name, size):
    sx, sy, sz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
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


def add_box(name, location, size, material, parent, bevel=0.0):
    if min(size) <= MIN_SIZE:
        raise ValueError(f"{name} has invalid size: {size}")

    obj = bpy.data.objects.new(name, cube_mesh(f"{name}_Mesh", size))
    obj.location = location
    obj.parent = parent
    if material is not None:
        obj.data.materials.append(material)
    bpy.context.collection.objects.link(obj)

    if bevel > 0.0:
        bevel_mod = obj.modifiers.new("Fixed_Bevel", "BEVEL")
        bevel_mod.width = bevel
        bevel_mod.segments = 1
        bevel_mod.affect = "EDGES"
        obj.modifiers.new("Weighted_Normals", "WEIGHTED_NORMAL")
    return obj


def add_root(width, height, depth):
    root = bpy.data.objects.new("B001_1", None)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 1.0
    root.location = (0, 0, 0)
    root["lh_opening_type"] = "Window"
    root["lh_target_width"] = width
    root["lh_target_height"] = height
    root["lh_target_depth"] = depth
    root["lh_parametric_profile_key"] = "BALCONY_RAILING_WINDOW_V1"
    root["lh_modeling_rule"] = "unity_axis_fixed_parts_plus_stretch_parts"
    bpy.context.collection.objects.link(root)
    return root


def create_b001_balcony(
    target_width=18.0,
    target_height=21.0,
    target_depth=1.4,
    frame_thickness=0.65,
    mullion_thickness=0.45,
    glass_thickness=0.08,
    railing_height=7.2,
    railing_depth_offset=2.4,
    railing_post_width=0.45,
    railing_bar_width=0.22,
    railing_bar_spacing=1.6,
    bevel=0.06,
):
    min_width = frame_thickness * 2.0 + mullion_thickness + 2.0
    min_height = frame_thickness * 2.0 + railing_height + 2.0
    if target_width < min_width or target_height < min_height or target_depth <= 0:
        raise ValueError("B001 target size is too small.")

    frame_mat = make_material("Balcony_Frame", (0.78, 0.8, 0.82), 1.0, 0.32)
    rail_mat = make_material("Balcony_Rail", (0.86, 0.86, 0.82), 1.0, 0.3)
    glass_mat = make_material("Balcony_Glass", (0.62, 0.82, 0.95), 0.34, 0.05)

    root = add_root(target_width, target_height, target_depth)
    half_w = target_width * 0.5
    half_h = target_height * 0.5
    frame_z = 0.0
    rail_z = -(target_depth * 0.5 + railing_depth_offset)

    add_box("Fixed_BalconyFrame_Left", (-half_w + frame_thickness * 0.5, 0, frame_z), (frame_thickness, target_height, target_depth), frame_mat, root, bevel)
    add_box("Fixed_BalconyFrame_Right", (half_w - frame_thickness * 0.5, 0, frame_z), (frame_thickness, target_height, target_depth), frame_mat, root, bevel)
    add_box("Stretch_BalconyFrame_Top", (0, half_h - frame_thickness * 0.5, frame_z), (target_width, frame_thickness, target_depth), frame_mat, root, bevel)
    add_box("Stretch_BalconyFrame_BottomRail", (0, -half_h + frame_thickness * 0.5, frame_z), (target_width, frame_thickness, target_depth), frame_mat, root, bevel)

    inner_width = target_width - frame_thickness * 2.0
    inner_height = target_height - frame_thickness * 2.0
    add_box("Fixed_Center_Mullion", (0, 0, frame_z), (mullion_thickness, inner_height, target_depth * 1.05), frame_mat, root, bevel)

    pane_width = (inner_width - mullion_thickness) * 0.5
    left_x = -mullion_thickness * 0.5 - pane_width * 0.5
    right_x = mullion_thickness * 0.5 + pane_width * 0.5
    add_box("Stretch_Glass_Left_SlidingPanel", (left_x, 0, -0.1), (pane_width, inner_height, glass_thickness), glass_mat, root, bevel * 0.25)
    add_box("Stretch_Glass_Right_SlidingPanel", (right_x, 0, 0.15), (pane_width, inner_height, glass_thickness), glass_mat, root, bevel * 0.25)

    rail_bottom_y = -half_h + frame_thickness
    rail_top_y = rail_bottom_y + railing_height
    rail_mid_y = rail_bottom_y + railing_height * 0.52
    rail_center_y = rail_bottom_y + railing_height * 0.5

    add_box("Fixed_Railing_Left_Post", (-half_w + railing_post_width * 0.5, rail_center_y, rail_z), (railing_post_width, railing_height, railing_post_width), rail_mat, root, bevel)
    add_box("Fixed_Railing_Right_Post", (half_w - railing_post_width * 0.5, rail_center_y, rail_z), (railing_post_width, railing_height, railing_post_width), rail_mat, root, bevel)
    add_box("Stretch_Railing_Top_Rail", (0, rail_top_y, rail_z), (target_width, railing_post_width, railing_post_width), rail_mat, root, bevel)
    add_box("Stretch_Railing_Mid_Rail", (0, rail_mid_y, rail_z), (target_width, railing_bar_width, railing_bar_width), rail_mat, root, bevel * 0.6)
    add_box("Stretch_Railing_Bottom_Rail", (0, rail_bottom_y, rail_z), (target_width, railing_post_width, railing_post_width), rail_mat, root, bevel)

    usable_width = target_width - railing_post_width * 2.0
    bar_count = max(3, round(usable_width / railing_bar_spacing))
    actual_spacing = usable_width / (bar_count + 1)
    bar_height = railing_height - railing_post_width * 1.2
    for i in range(bar_count):
        x = -half_w + railing_post_width + actual_spacing * (i + 1)
        add_box(
            f"Fixed_Railing_Vertical_Bar_{i + 1:02d}",
            (x, rail_center_y, rail_z),
            (railing_bar_width, bar_height, railing_bar_width),
            rail_mat,
            root,
            bevel * 0.45,
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


def main():
    clear_scene()
    root = create_b001_balcony()
    # Uncomment to export from Blender.
    # export_fbx(EXPORT_PATH, root)
    return root


if __name__ == "__main__":
    main()
