"""Bake Rocketbox seated FBXs into arm-length-normalised tracks used by NARA.
Run with Blender --background --factory-startup --python this_file -- CACHE OUTPUT.
"""
import math
import struct
import sys
from pathlib import Path
from urllib.request import urlopen

import bpy
from mathutils import Vector

REVISION = "0943055db6ec570bcef9f2c8b41c9e5467c808f9"
SOURCE = f"https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/{REVISION}/Assets/Animations/all_animations_max_motextr_static/"
# Track order matches ListeningMotion. Both rooms use the same semantic order.
TAKES = (
    ("m", "idle_neutral_02"), ("f", "idle_relaxed_01"), ("m", "idle_neutral_01"),
    ("f", "gestic_thoughtful"), ("m", "idle_dust"), ("f", "breathe_01"),
    ("f", "idle_look_around"), ("m", "gestic_shrug_01"),
    ("f", "idle_roll_head"), ("m", "idle_waiting_01"),
)
RATE = 15


def export_take(path, output):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_custom_props=False)
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE" and "Bip01 R Hand" in obj.pose.bones)
    scene = bpy.context.scene
    action = rig.animation_data.action
    first, last = action.frame_range
    fps = scene.render.fps / scene.render.fps_base
    duration = (last - first) / fps
    count = max(2, math.ceil(duration * RATE) + 1)

    def point(name):
        return rig.matrix_world @ rig.pose.bones["Bip01 " + name].head

    scene.frame_set(math.floor(first), subframe=first % 1)
    up = Vector((0, 0, 1))
    side = point("R Thigh") - point("L Thigh")
    side.z = 0
    side.normalize()
    forward = up.cross(side).normalized()
    knee_direction = point("R Calf") + point("L Calf") - point("R Thigh") - point("L Thigh")
    if forward.dot(knee_direction) < 0:
        forward.negate()

    def axes(vector, sign=1):
        return Vector((sign * side.dot(vector), up.dot(vector), forward.dot(vector)))

    output.write(struct.pack("<if", count, duration))
    for i in range(count):
        frame = first + (last-first) * i / (count-1)
        scene.frame_set(math.floor(frame), subframe=frame % 1)
        spine = axes(point("Neck") - point("Spine"))
        shoulders = axes(point("R UpperArm") - point("L UpperArm"))
        values = [math.degrees(math.atan2(spine.z, spine.y)), math.degrees(math.atan2(shoulders.z, shoulders.x)),
                  math.degrees(math.atan2(spine.x, spine.y))]
        for name, sign in (("R ", 1), ("L ", -1)):
            shoulder, elbow, wrist = (point(name + joint) for joint in ("UpperArm", "Forearm", "Hand"))
            length = (elbow - shoulder).length + (wrist - elbow).length
            if not .32 < length < .9:
                raise ValueError(f"Unexpected arm length {length:.3f} m in {path.name}")
            for vector in ((wrist - shoulder) / length, (elbow - shoulder) / length,
                           (point(name + "Finger2") - wrist).normalized(),
                           (point(name + "Finger1") - point(name + "Finger4")).normalized()):
                values.extend(axes(vector, sign))
        output.write(struct.pack("<27f", *values))
    print(f"NARA_TAKE {path.name} duration={duration:.2f}s frames={count}", flush=True)


def main(cache, destination):
    cache.mkdir(parents=True, exist_ok=True)
    pending = destination.with_suffix(".tmp")
    with pending.open("wb") as binary:
        binary.write(b"NSM1")
        binary.write(struct.pack("<i", 2*len(TAKES)))
        for room in ("chair", "table"):
            for gender, name in TAKES:
                filename = f"{gender}_sit_{room}_{name}.max.fbx"
                path = cache / filename
                if not path.exists():
                    with urlopen(SOURCE + filename, timeout=30) as response:
                        path.write_bytes(response.read())
                export_take(path, binary)
    pending.replace(destination)


if __name__ == "__main__":
    main(*(Path(arg).resolve() for arg in sys.argv[sys.argv.index("--")+1:]))
