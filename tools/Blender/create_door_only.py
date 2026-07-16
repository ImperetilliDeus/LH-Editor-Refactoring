"""
Create one parametric door model for Unity.

Run in Blender:
    blender --background --python create_door_only.py
"""

import sys
import importlib.util
from pathlib import Path

SCRIPT_DIR = Path(r"E:\Unity\LH Editor_Refactoring\tools\Blender")
COMMON_SCRIPT_PATH = SCRIPT_DIR / "parametric_opening_models.py"


def load_common_module():
    module_name = "parametric_opening_models"
    if module_name in sys.modules:
        return sys.modules[module_name]

    spec = importlib.util.spec_from_file_location(module_name, COMMON_SCRIPT_PATH)
    if spec is None or spec.loader is None:
        raise ImportError(f"Cannot load {COMMON_SCRIPT_PATH}")

    module = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = module
    spec.loader.exec_module(module)
    return module


common = load_common_module()
clear_scene = common.clear_scene
create_parametric_door = common.create_parametric_door
ensure_collection = common.ensure_collection
export_fbx = common.export_fbx


def main():
    clear_scene()
    collection = ensure_collection("Parametric_Door")

    root = create_parametric_door(
        target_width=0.9,
        target_height=2.1,
        target_depth=0.06,
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
        collection=collection,
    )

    # Uncomment when you want Blender to export immediately.
    # export_fbx(str(SCRIPT_DIR / "Door_Parametric.fbx"), roots=[root])
    return root


if __name__ == "__main__":
    main()
