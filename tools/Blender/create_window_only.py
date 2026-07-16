"""
Create one parametric window model for Unity.

Run in Blender:
    blender --background --python create_window_only.py
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
create_parametric_window = common.create_parametric_window
ensure_collection = common.ensure_collection
export_fbx = common.export_fbx


def main():
    clear_scene()
    collection = ensure_collection("Parametric_Window")

    root = create_parametric_window(
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
        collection=collection,
    )

    # Uncomment when you want Blender to export immediately.
    # export_fbx(str(SCRIPT_DIR / "Window_Parametric.fbx"), roots=[root])
    return root


if __name__ == "__main__":
    main()
