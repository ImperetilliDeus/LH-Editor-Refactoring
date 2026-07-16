"""
Parametric door/window/balcony opening models for Unity.

Run in Blender:
    blender --background --python parametric_opening_models.py

Coordinate convention:
    X = opening width
    Y = opening depth / wall thickness direction
    Z = opening height

The script bakes dimensions into each part and applies transforms so object
scales stay at (1, 1, 1). Do not non-uniformly scale the root object in Unity.
"""

import math
from pathlib import Path

import bpy


MIN_SIZE = 0.0001


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def ensure_collection(name):
    collection = bpy.data.collections.get(name)
    if collection is None:
        collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(collection)
    return collection


def make_material(name, color, alpha=1.0, metallic=0.0, roughness=0.45):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    material.blend_method = "BLEND" if alpha < 1.0 else "OPAQUE"
    material.use_screen_refraction = alpha < 1.0
    material.show_transparent_back = True

    bsdf = material.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], alpha)
        bsdf.inputs["Alpha"].default_value = alpha
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness

    return material


def unlink_from_other_collections(obj, target_collection):
    for collection in list(obj.users_collection):
        if collection != target_collection:
            collection.objects.unlink(obj)
    if obj.name not in target_collection.objects:
        target_collection.objects.link(obj)


def apply_object_transform(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.select_set(False)


def add_cube_part(
    name,
    dimensions,
    location,
    material,
    parent=None,
    collection=None,
    bevel=0.0,
):
    if min(dimensions) <= MIN_SIZE:
        raise ValueError(f"{name} has invalid dimensions: {dimensions}")

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    apply_object_transform(obj)

    if parent is not None:
        obj.parent = parent
    if material is not None:
        obj.data.materials.append(material)
    if collection is not None:
        unlink_from_other_collections(obj, collection)

    if bevel > 0.0:
        modifier = obj.modifiers.new(name="Fixed_Bevel", type="BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.affect = "EDGES"
        obj.modifiers.new(name="Weighted_Normals", type="WEIGHTED_NORMAL")

    return obj


def add_cylinder_part(
    name,
    radius,
    depth,
    location,
    material,
    parent=None,
    collection=None,
    rotation=(0.0, 0.0, 0.0),
    vertices=24,
):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=rotation,
    )
    obj = bpy.context.object
    obj.name = name
    apply_object_transform(obj)

    if parent is not None:
        obj.parent = parent
    if material is not None:
        obj.data.materials.append(material)
    if collection is not None:
        unlink_from_other_collections(obj, collection)

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.shade_smooth()
    obj.select_set(False)
    return obj


def add_root(name, location, collection, opening_type, width, height, depth):
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "CUBE"
    root.empty_display_size = 0.25
    root.location = location
    collection.objects.link(root)

    root["lh_opening_type"] = opening_type
    root["lh_target_width"] = width
    root["lh_target_height"] = height
    root["lh_target_depth"] = depth
    root["lh_modeling_rule"] = "fixed_parts_plus_stretch_parts"
    return root


def create_material_set(prefix):
    return {
        "frame": make_material(f"{prefix}_Frame", (0.78, 0.80, 0.80), 1.0, 0.0, 0.32),
        "slab": make_material(f"{prefix}_Slab", (0.62, 0.52, 0.42), 1.0, 0.0, 0.55),
        "metal": make_material(f"{prefix}_Metal", (0.55, 0.57, 0.58), 1.0, 0.2, 0.25),
        "glass": make_material(f"{prefix}_Glass", (0.62, 0.82, 0.95), 0.34, 0.0, 0.05),
        "rail": make_material(f"{prefix}_Rail", (0.86, 0.86, 0.82), 1.0, 0.0, 0.3),
        "floor": make_material(f"{prefix}_Balcony_Floor", (0.54, 0.51, 0.46), 1.0, 0.0, 0.65),
    }


def validate_opening_size(target_width, target_height, target_depth):
    if target_width <= MIN_SIZE or target_height <= MIN_SIZE or target_depth <= MIN_SIZE:
        raise ValueError("target_width, target_height, and target_depth must be positive.")


def create_parametric_door(
    target_width=0.9,
    target_height=2.1,
    target_depth=0.05,
    frame_thickness=0.07,
    frame_depth=0.08,
    slab_thickness=0.035,
    bevel=0.01,
    handle_radius=0.025,
    handle_length=0.12,
    handle_height_ratio=0.48,
    hinge_count=3,
    material_prefix="Door",
    location=(0.0, 0.0, 0.0),
    collection=None,
):
    validate_opening_size(target_width, target_height, target_depth)
    min_width = frame_thickness * 2.0 + handle_length + 0.18
    min_height = frame_thickness * 2.0 + 0.3
    if target_width < min_width or target_height < min_height:
        raise ValueError("Door target size is too small for fixed frame and handle parts.")

    collection = collection or ensure_collection("Parametric_Openings")
    mat = create_material_set(material_prefix)
    root = add_root(
        f"{material_prefix}_Parametric_ROOT",
        location,
        collection,
        "Door",
        target_width,
        target_height,
        target_depth,
    )

    x0 = location[0]
    y0 = location[1]
    z0 = location[2]
    half_w = target_width * 0.5
    half_h = target_height * 0.5
    inner_w = target_width - frame_thickness * 2.0
    inner_h = target_height - frame_thickness * 2.0
    frame_y = max(frame_depth, target_depth)

    add_cube_part("Fixed_Frame_Left", (frame_thickness, frame_y, target_height), (x0 - half_w + frame_thickness * 0.5, y0, z0), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_Frame_Right", (frame_thickness, frame_y, target_height), (x0 + half_w - frame_thickness * 0.5, y0, z0), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_Frame_Top", (target_width, frame_y, frame_thickness), (x0, y0, z0 + half_h - frame_thickness * 0.5), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_Threshold_Bottom", (target_width, frame_y, frame_thickness), (x0, y0, z0 - half_h + frame_thickness * 0.5), mat["frame"], root, collection, bevel)
    add_cube_part("Stretch_Slab_Center", (inner_w, slab_thickness, inner_h), (x0, y0, z0), mat["slab"], root, collection, bevel * 0.5)

    panel_margin = frame_thickness * 0.8
    add_cube_part(
        "Stretch_Recessed_Panel_Center",
        (max(inner_w - panel_margin * 2.0, MIN_SIZE), slab_thickness * 0.25, max(inner_h * 0.58, MIN_SIZE)),
        (x0, y0 - slab_thickness * 0.52, z0 + inner_h * 0.03),
        mat["frame"],
        root,
        collection,
        bevel * 0.35,
    )

    handle_x = x0 + half_w - frame_thickness - 0.12
    handle_z = z0 - half_h + target_height * handle_height_ratio
    handle_y = y0 - slab_thickness * 0.85
    add_cylinder_part("Fixed_Handle_Knob", handle_radius, handle_radius * 0.8, (handle_x, handle_y, handle_z), mat["metal"], root, collection, rotation=(math.pi / 2.0, 0.0, 0.0))
    add_cylinder_part("Fixed_Handle_Lever", handle_radius * 0.45, handle_length, (handle_x - handle_length * 0.5, handle_y - handle_radius * 0.2, handle_z), mat["metal"], root, collection, rotation=(0.0, math.pi / 2.0, 0.0))

    hinge_x = x0 - half_w + frame_thickness * 0.52
    hinge_y = y0 - frame_y * 0.52
    usable_h = target_height - frame_thickness * 2.0
    for i in range(max(1, hinge_count)):
        t = (i + 1) / (hinge_count + 1)
        hinge_z = z0 - half_h + frame_thickness + usable_h * t
        add_cylinder_part(
            f"Fixed_Hinge_{i + 1:02d}",
            frame_thickness * 0.22,
            frame_thickness * 0.85,
            (hinge_x, hinge_y, hinge_z),
            mat["metal"],
            root,
            collection,
            rotation=(0.0, 0.0, 0.0),
            vertices=18,
        )

    corner_size = frame_thickness * 0.45
    for sx in (-1, 1):
        for sz in (-1, 1):
            add_cube_part(
                f"Fixed_Corner_Block_{'L' if sx < 0 else 'R'}_{'B' if sz < 0 else 'T'}",
                (corner_size, frame_y * 1.04, corner_size),
                (x0 + sx * (half_w - corner_size * 0.5), y0, z0 + sz * (half_h - corner_size * 0.5)),
                mat["frame"],
                root,
                collection,
                bevel * 0.5,
            )

    return root


def create_parametric_window(
    target_width=1.2,
    target_height=1.2,
    target_depth=0.08,
    frame_thickness=0.06,
    sash_thickness=0.045,
    mullion_thickness=0.045,
    glass_thickness=0.008,
    pane_columns=2,
    pane_rows=1,
    bevel=0.008,
    material_prefix="Window",
    location=(0.0, 0.0, 0.0),
    collection=None,
):
    validate_opening_size(target_width, target_height, target_depth)
    pane_columns = max(1, int(pane_columns))
    pane_rows = max(1, int(pane_rows))
    min_glass = 0.08
    min_width = frame_thickness * 2.0 + mullion_thickness * (pane_columns - 1) + min_glass * pane_columns
    min_height = frame_thickness * 2.0 + mullion_thickness * (pane_rows - 1) + min_glass * pane_rows
    if target_width < min_width or target_height < min_height:
        raise ValueError("Window target size is too small for fixed frame, mullion, and glass parts.")

    collection = collection or ensure_collection("Parametric_Openings")
    mat = create_material_set(material_prefix)
    root = add_root(
        f"{material_prefix}_Parametric_ROOT",
        location,
        collection,
        "Window",
        target_width,
        target_height,
        target_depth,
    )

    x0 = location[0]
    y0 = location[1]
    z0 = location[2]
    half_w = target_width * 0.5
    half_h = target_height * 0.5
    frame_y = target_depth

    add_cube_part("Fixed_OuterFrame_Left", (frame_thickness, frame_y, target_height), (x0 - half_w + frame_thickness * 0.5, y0, z0), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_OuterFrame_Right", (frame_thickness, frame_y, target_height), (x0 + half_w - frame_thickness * 0.5, y0, z0), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_OuterFrame_Top", (target_width, frame_y, frame_thickness), (x0, y0, z0 + half_h - frame_thickness * 0.5), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_OuterFrame_Bottom", (target_width, frame_y, frame_thickness), (x0, y0, z0 - half_h + frame_thickness * 0.5), mat["frame"], root, collection, bevel)

    inner_w = target_width - frame_thickness * 2.0
    inner_h = target_height - frame_thickness * 2.0
    pane_w = (inner_w - mullion_thickness * (pane_columns - 1)) / pane_columns
    pane_h = (inner_h - mullion_thickness * (pane_rows - 1)) / pane_rows
    left_inner = x0 - half_w + frame_thickness
    bottom_inner = z0 - half_h + frame_thickness

    for c in range(1, pane_columns):
        mx = left_inner + pane_w * c + mullion_thickness * (c - 0.5)
        add_cube_part(f"Fixed_Mullion_Vertical_{c:02d}", (mullion_thickness, frame_y * 0.95, inner_h), (mx, y0, z0), mat["frame"], root, collection, bevel)

    for r in range(1, pane_rows):
        mz = bottom_inner + pane_h * r + mullion_thickness * (r - 0.5)
        add_cube_part(f"Fixed_Mullion_Horizontal_{r:02d}", (inner_w, frame_y * 0.95, mullion_thickness), (x0, y0, mz), mat["frame"], root, collection, bevel)

    pane_index = 1
    for r in range(pane_rows):
        for c in range(pane_columns):
            px = left_inner + pane_w * (c + 0.5) + mullion_thickness * c
            pz = bottom_inner + pane_h * (r + 0.5) + mullion_thickness * r
            add_cube_part(
                f"Stretch_Glass_Pane_{pane_index:02d}",
                (max(pane_w - sash_thickness * 0.45, MIN_SIZE), glass_thickness, max(pane_h - sash_thickness * 0.45, MIN_SIZE)),
                (px, y0 - glass_thickness * 0.25, pz),
                mat["glass"],
                root,
                collection,
                bevel * 0.3,
            )
            add_cube_part(
                f"Fixed_Sash_Frame_Pane_{pane_index:02d}",
                (pane_w, frame_y * 0.25, sash_thickness),
                (px, y0 - frame_y * 0.28, pz + pane_h * 0.5 - sash_thickness * 0.5),
                mat["frame"],
                root,
                collection,
                bevel * 0.5,
            )
            add_cube_part(
                f"Fixed_Sash_Frame_Pane_{pane_index:02d}_Bottom",
                (pane_w, frame_y * 0.25, sash_thickness),
                (px, y0 - frame_y * 0.28, pz - pane_h * 0.5 + sash_thickness * 0.5),
                mat["frame"],
                root,
                collection,
                bevel * 0.5,
            )
            pane_index += 1

    lock_w = sash_thickness * 0.7
    add_cube_part("Fixed_Window_Lock", (lock_w, frame_y * 0.35, lock_w * 1.8), (x0, y0 - frame_y * 0.58, z0), mat["metal"], root, collection, bevel * 0.4)

    return root


def create_parametric_balcony_window(
    target_width=1.8,
    target_height=2.1,
    target_depth=0.18,
    frame_thickness=0.065,
    mullion_thickness=0.045,
    glass_thickness=0.008,
    railing_height=0.72,
    railing_depth_offset=0.24,
    railing_post_width=0.045,
    railing_bar_width=0.022,
    railing_bar_spacing=0.16,
    floor_depth=0.75,
    floor_thickness=0.045,
    bevel=0.008,
    material_prefix="Balcony",
    location=(0.0, 0.0, 0.0),
    collection=None,
):
    """Create a balcony-style glass opening with a front railing like the reference image."""

    validate_opening_size(target_width, target_height, target_depth)
    min_width = frame_thickness * 2.0 + mullion_thickness + 0.2
    min_height = max(frame_thickness * 2.0 + 0.4, railing_height + 0.3)
    if target_width < min_width or target_height < min_height:
        raise ValueError("Balcony target size is too small for fixed frame and railing parts.")

    collection = collection or ensure_collection("Parametric_Openings")
    mat = create_material_set(material_prefix)
    root = add_root(
        f"{material_prefix}_Parametric_ROOT",
        location,
        collection,
        "BalconyWindow",
        target_width,
        target_height,
        target_depth,
    )

    x0 = location[0]
    y0 = location[1]
    z0 = location[2]
    half_w = target_width * 0.5
    half_h = target_height * 0.5
    frame_y = target_depth

    # Sliding glass window wall behind the railing.
    add_cube_part("Fixed_BalconyFrame_Left", (frame_thickness, frame_y, target_height), (x0 - half_w + frame_thickness * 0.5, y0, z0), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_BalconyFrame_Right", (frame_thickness, frame_y, target_height), (x0 + half_w - frame_thickness * 0.5, y0, z0), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_BalconyFrame_Top", (target_width, frame_y, frame_thickness), (x0, y0, z0 + half_h - frame_thickness * 0.5), mat["frame"], root, collection, bevel)
    add_cube_part("Fixed_BalconyFrame_BottomRail", (target_width, frame_y, frame_thickness), (x0, y0, z0 - half_h + frame_thickness * 0.5), mat["frame"], root, collection, bevel)

    inner_w = target_width - frame_thickness * 2.0
    inner_h = target_height - frame_thickness * 2.0
    center_mullion_x = x0
    add_cube_part("Fixed_Center_Mullion", (mullion_thickness, frame_y * 1.05, inner_h), (center_mullion_x, y0, z0), mat["frame"], root, collection, bevel)

    pane_w = (inner_w - mullion_thickness) * 0.5
    left_pane_x = x0 - mullion_thickness * 0.5 - pane_w * 0.5
    right_pane_x = x0 + mullion_thickness * 0.5 + pane_w * 0.5
    add_cube_part("Stretch_Glass_Left_SlidingPanel", (pane_w, glass_thickness, inner_h), (left_pane_x, y0 - frame_y * 0.2, z0), mat["glass"], root, collection, bevel * 0.25)
    add_cube_part("Stretch_Glass_Right_SlidingPanel", (pane_w, glass_thickness, inner_h), (right_pane_x, y0 + frame_y * 0.05, z0), mat["glass"], root, collection, bevel * 0.25)

    rail_y = y0 - target_depth * 0.5 - railing_depth_offset
    rail_bottom_z = z0 - half_h + frame_thickness
    rail_top_z = rail_bottom_z + railing_height
    rail_mid_z = rail_bottom_z + railing_height * 0.52
    rail_w = target_width

    # Balcony floor slab, placed outside the glass line.
    floor_y = rail_y - floor_depth * 0.5 + railing_depth_offset * 0.15
    floor_z = z0 - half_h - floor_thickness * 0.5
    add_cube_part("Stretch_Balcony_Floor_Slab", (target_width, floor_depth, floor_thickness), (x0, floor_y, floor_z), mat["floor"], root, collection, bevel * 0.5)

    # Railing: fixed rail thickness and bars, variable count by width.
    add_cube_part("Fixed_Railing_Left_Post", (railing_post_width, railing_post_width, railing_height), (x0 - half_w + railing_post_width * 0.5, rail_y, rail_bottom_z + railing_height * 0.5), mat["rail"], root, collection, bevel)
    add_cube_part("Fixed_Railing_Right_Post", (railing_post_width, railing_post_width, railing_height), (x0 + half_w - railing_post_width * 0.5, rail_y, rail_bottom_z + railing_height * 0.5), mat["rail"], root, collection, bevel)
    add_cube_part("Stretch_Railing_Top_Rail", (rail_w, railing_post_width, railing_post_width), (x0, rail_y, rail_top_z), mat["rail"], root, collection, bevel)
    add_cube_part("Stretch_Railing_Mid_Rail", (rail_w, railing_bar_width, railing_bar_width), (x0, rail_y, rail_mid_z), mat["rail"], root, collection, bevel * 0.6)
    add_cube_part("Stretch_Railing_Bottom_Rail", (rail_w, railing_post_width, railing_post_width), (x0, rail_y, rail_bottom_z), mat["rail"], root, collection, bevel)

    usable_bar_width = target_width - railing_post_width * 2.0
    bar_count = max(3, int(usable_bar_width / railing_bar_spacing))
    actual_spacing = usable_bar_width / (bar_count + 1)
    for i in range(bar_count):
        bx = x0 - half_w + railing_post_width + actual_spacing * (i + 1)
        add_cube_part(
            f"Fixed_Railing_Vertical_Bar_{i + 1:02d}",
            (railing_bar_width, railing_bar_width, railing_height - railing_post_width * 1.2),
            (bx, rail_y, rail_bottom_z + railing_height * 0.5),
            mat["rail"],
            root,
            collection,
            bevel * 0.45,
        )

    return root


def export_fbx(filepath, roots=None):
    filepath = str(Path(filepath))
    bpy.ops.object.select_all(action="DESELECT")

    if roots:
        for root in roots:
            root.select_set(True)
            for child in root.children_recursive:
                child.select_set(True)
        bpy.context.view_layer.objects.active = roots[0]
        use_selection = True
    else:
        use_selection = False

    bpy.ops.export_scene.fbx(
        filepath=filepath,
        use_selection=use_selection,
        apply_scale_options="FBX_SCALE_NONE",
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        add_leaf_bones=False,
    )


def build_examples():
    clear_scene()
    collection = ensure_collection("Parametric_Openings")

    roots = []
    x = 0.0
    for width, height in [(0.8, 2.05), (0.95, 2.1), (1.15, 2.25)]:
        roots.append(create_parametric_door(width, height, 0.06, material_prefix=f"Door_{width:.2f}", location=(x, 0.0, 0.0), collection=collection))
        x += width + 0.55

    x += 0.4
    for width, height, cols in [(1.0, 1.0, 2), (1.4, 1.2, 2), (1.8, 1.35, 3)]:
        roots.append(create_parametric_window(width, height, 0.08, pane_columns=cols, material_prefix=f"Window_{width:.2f}", location=(x, 0.0, 0.0), collection=collection))
        x += width + 0.55

    x += 0.5
    for width, height in [(1.4, 2.0), (1.8, 2.1), (2.3, 2.2)]:
        roots.append(create_parametric_balcony_window(width, height, 0.14, material_prefix=f"Balcony_{width:.2f}", location=(x, 0.0, 0.0), collection=collection))
        x += width + 0.7

    bpy.ops.object.light_add(type="AREA", location=(4.0, -4.0, 5.0))
    light = bpy.context.object
    light.name = "Preview_Area_Light"
    light.data.energy = 450
    light.data.size = 5.0

    bpy.ops.object.camera_add(location=(4.0, -6.0, 3.0), rotation=(math.radians(62.0), 0.0, math.radians(36.0)))
    bpy.context.scene.camera = bpy.context.object

    return roots


if __name__ == "__main__":
    build_examples()
