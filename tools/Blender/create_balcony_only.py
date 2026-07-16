"""
Create one balcony glass-window and railing model for Unity.

Run in Blender:
    blender --background --python create_balcony_only.py
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
create_parametric_balcony_window = common.create_parametric_balcony_window
ensure_collection = common.ensure_collection
export_fbx = common.export_fbx


def main():
    clear_scene()
    collection = ensure_collection("Parametric_Balcony")

    root = create_parametric_balcony_window(
        target_width=1.8,
        target_height=2.1,
        target_depth=0.14,
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
        collection=collection,
    )

    # Uncomment when you want Blender to export immediately.
    # export_fbx(str(SCRIPT_DIR / "Balcony_Parametric.fbx"), roots=[root])
    return root


if __name__ == "__main__":
    main()
