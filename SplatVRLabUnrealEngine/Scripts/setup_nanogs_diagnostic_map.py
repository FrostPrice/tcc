"""Create the generated desktop diagnostic map for the NanoGS viability test.

This is not the final visual-comparison camera conversion. The first Nerfstudio pose
is used only to exercise the renderer while coordinate-system alignment is pending.
"""

import json
import math
from pathlib import Path

import unreal


ASSET_PATH = "/Game/NanoGSViability/exp_ns_poster_baseline_v01_baseline_v01"
MAP_PATH = "/Game/NanoGSViability/NanoGSDiagnosticMap"

project_dir = Path(
    unreal.Paths.convert_relative_path_to_full(unreal.Paths.project_dir())
).resolve()
transforms_path = project_dir.parent / "experiments/baseline_v01/transforms_filtered.json"

asset = unreal.load_asset(ASSET_PATH)
if asset is None:
    raise RuntimeError(f"NanoGS asset not found: {ASSET_PATH}")
if not transforms_path.is_file():
    raise RuntimeError(f"Nerfstudio transforms not found: {transforms_path}")

level_subsystem = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
if unreal.EditorAssetLibrary.does_asset_exist(MAP_PATH):
    if not level_subsystem.load_level(MAP_PATH):
        raise RuntimeError(f"Could not load generated map: {MAP_PATH}")
else:
    if not level_subsystem.new_level(MAP_PATH):
        raise RuntimeError(f"Could not create generated map: {MAP_PATH}")

actor_subsystem = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
for actor in actor_subsystem.get_all_level_actors():
    if actor.get_actor_label() in {"NanoGS_DiagnosticSplat", "NanoGS_DiagnosticCamera"}:
        actor_subsystem.destroy_actor(actor)

splat_actor = actor_subsystem.spawn_actor_from_object(
    asset,
    unreal.Vector(0.0, 0.0, 0.0),
    unreal.Rotator(0.0, 0.0, 0.0),
)
if splat_actor is None:
    raise RuntimeError("Could not create GaussianSplatActor from imported asset")
splat_actor.set_actor_label("NanoGS_DiagnosticSplat")

with transforms_path.open("r", encoding="utf-8") as transforms_file:
    transforms = json.load(transforms_file)

matrix = transforms["frames"][0]["transform_matrix"]
position_ns = [matrix[row][3] for row in range(3)]
camera_back_ns = [matrix[row][2] for row in range(3)]
forward_ns = [-value for value in camera_back_ns]


def preliminary_ns_to_ue(vector, scale=1.0):
    """Mirror NanoGS's documented PLY axis mapping; not yet fidelity-validated."""
    return unreal.Vector(vector[2] * scale, vector[0] * scale, -vector[1] * scale)


camera_location = preliminary_ns_to_ue(position_ns, 100.0)
camera_forward = preliminary_ns_to_ue(forward_ns)
camera_rotation = unreal.MathLibrary.find_look_at_rotation(
    camera_location,
    camera_location + camera_forward * 100.0,
)

camera_actor = actor_subsystem.spawn_actor_from_class(
    unreal.CameraActor,
    camera_location,
    camera_rotation,
)
if camera_actor is None:
    raise RuntimeError("Could not create diagnostic CameraActor")
camera_actor.set_actor_label("NanoGS_DiagnosticCamera")

horizontal_fov = math.degrees(2.0 * math.atan(transforms["w"] / (2.0 * transforms["fl_x"])))
camera_actor.camera_component.set_editor_property("field_of_view", horizontal_fov)
camera_actor.camera_component.set_editor_property("aspect_ratio", transforms["w"] / transforms["h"])
camera_actor.set_editor_property("auto_activate_for_player", unreal.AutoReceiveInput.PLAYER0)

if not level_subsystem.save_current_level():
    raise RuntimeError(f"Could not save generated map: {MAP_PATH}")

unreal.log(f"NANOGS_MAP splat_actor={splat_actor.get_path_name()}")
unreal.log(f"NANOGS_MAP camera_location={camera_location}")
unreal.log(f"NANOGS_MAP camera_rotation={camera_rotation}")
unreal.log(f"NANOGS_MAP horizontal_fov={horizontal_fov:.6f}")
unreal.log_warning(
    "NANOGS_MAP camera conversion is diagnostic only; do not use this map for fidelity metrics."
)
