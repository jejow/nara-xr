"""Bake Overte's seated upper-body FBXs into NARA's arm-length coordinates.

Run with Blender --background --factory-startup --python this_file -- CACHE OUTPUT.
The frame windows for the hand raises come from Overte's avatar-animation.json.
"""
import math
import struct
import sys
from pathlib import Path
from urllib.request import urlopen

import bpy
from mathutils import Vector

REVISION = "4a289354634237b265f6be106da2df121cd67c54"
SOURCE = f"https://raw.githubusercontent.com/overte-org/overte/{REVISION}/interface/resources/avatar/animations/"
SOURCE_RATE = 30
EXPORT_RATE = 24
# Order follows OverteTake in Unity. Hold boundaries are in frames relative to the take.
TAKES = (
    ("sitting_emote_raisehand_all.fbx", 32, 345),
    ("sitting_emote_raisehand02_all.fbx", 18, 378),
    ("sitting_emote_raisehand03_all.fbx", 15, 233),
    ("sitting_talk02.fbx", 0, 0),
    ("sitting_talk03.fbx", 0, 0),
    ("sitting_talk04.fbx", 0, 0),
    ("sitting_idle_once_leanforward.fbx", 0, 0),
    ("sitting_idle_once_lookaround.fbx", 0, 0),
    ("sitting_emote_agree_headnod.fbx", 0, 0),
    ("sitting_idle_once_shiftweight.fbx", 0, 0),
    ("sitting_idle_once_fidget.fbx", 0, 0),
    ("sitting_emote_agree_acknowledge.fbx", 0, 0),
    ("sitting_emote_agree_headnodyes.fbx", 0, 0),
    ("sitting_emote_agree_longheadnod.fbx", 0, 0),
    ("sitting_idle_once_lookleftright.fbx", 0, 0),
    ("sitting_idle_once_lookfidget.fbx", 0, 0),
    ("sitting_idle_once_shifting.fbx", 0, 0),
    ("sitting_emote_point_all.fbx", 0, 0),
)


class Take:
    def __init__(self, path):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(path), use_custom_props=False)
        self.rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
        self.scene = bpy.context.scene
        self.bones = {bone.name.split(":")[-1].lower().replace("mixamorig", ""): bone
                      for bone in self.rig.pose.bones}
        self.start, self.end = self.rig.animation_data.action.frame_range
        self.fps = self.scene.render.fps / self.scene.render.fps_base
        self.duration = (self.end - self.start) / self.fps
        self.at(0)

    def at(self, seconds):
        frame = self.start + seconds * self.fps
        self.scene.frame_set(math.floor(frame), subframe=frame % 1)

    def point(self, name):
        return self.rig.matrix_world @ self.bones[name].head

    def rotation(self, name):
        return (self.rig.matrix_world @ self.bones[name].matrix).to_quaternion()


class SeatAxes:
    def __init__(self, neutral):
        self.up = Vector((0, 0, 1))
        self.side = neutral.point("rightupleg") - neutral.point("leftupleg")
        self.side.z = 0
        self.side.normalize()
        self.forward = self.up.cross(self.side).normalized()
        knees = (neutral.point("rightleg") + neutral.point("leftleg")
                 - neutral.point("rightupleg") - neutral.point("leftupleg"))
        if self.forward.dot(knees) < 0:
            self.forward.negate()
        self.reference_torso = self.torso(neutral)
        self.reference_spine = neutral.rotation("spine2")
        self.inverse_head = neutral.rotation("head").inverted()

    def vector(self, direction, sign=1):
        return Vector((sign * self.side.dot(direction), self.up.dot(direction),
                       self.forward.dot(direction)))

    def torso(self, take):
        spine = self.vector(take.point("neck") - take.point("spine"))
        shoulders = self.vector(take.point("rightarm") - take.point("leftarm"))
        return Vector((angle(spine.z, spine.y), angle(shoulders.z, shoulders.x),
                       angle(spine.x, spine.y)))

    def head(self, take):
        rotation = (self.reference_spine @ take.rotation("spine2").inverted()
                    @ take.rotation("head") @ self.inverse_head)
        facing = self.vector(rotation @ self.forward)
        up = self.vector(rotation @ self.up)
        return Vector((angle(facing.y, max(.01, facing.z)), angle(facing.x, max(.01, facing.z)),
                       angle(up.x, max(.01, up.y))))


def angle(a, b):
    return math.degrees(math.atan2(a, b))


def sample_pose(take, axes):
    values = list(axes.torso(take) - axes.reference_torso)
    for side, sign in (("right", 1), ("left", -1)):
        shoulder, elbow, wrist = (take.point(side + joint) for joint in ("arm", "forearm", "hand"))
        length = (elbow - shoulder).length + (wrist - elbow).length
        if not .32 < length < .90:
            raise ValueError(f"Unexpected arm length {length:.3f} m")
        middle, index, little = (take.point(side + "hand" + finger + "1")
                                 for finger in ("middle", "index", "pinky"))
        for v in ((wrist - shoulder) / length, (elbow - shoulder) / length, (middle - wrist).normalized(),
                  (index - little).normalized()):
            values.extend(axes.vector(v, sign))
    values.extend(axes.head(take))
    if not all(map(math.isfinite, values)):
        raise ValueError("Non-finite pose")
    return values


def export_take(take, axes, name, hold, lower, output):
    frames = math.ceil(take.duration * EXPORT_RATE) + 1
    expected = (hold == lower == 0) or (0 < hold < lower < take.duration * SOURCE_RATE)
    if not expected or not 2 <= frames <= 16000:
        raise ValueError(f"Invalid Overte take or phase boundaries: {name}")

    output.write(struct.pack("<ifff", frames, take.duration, hold / SOURCE_RATE, lower / SOURCE_RATE))
    for i in range(frames):
        take.at(take.duration * i / (frames - 1))
        try:
            output.write(struct.pack("<30f", *sample_pose(take, axes)))
        except ValueError as error:
            raise ValueError(f"{name}, sample {i}: {error}") from error
    print(f"NARA_OVERTE {name}: {frames} samples, {take.duration:.2f}s", flush=True)


def main(cache, destination):
    cache = cache / REVISION
    cache.mkdir(parents=True, exist_ok=True)

    def source(filename):
        path = cache / filename
        if not path.exists():
            with urlopen(SOURCE + filename, timeout=30) as response:
                data = response.read(2_097_152)
            if not data.startswith(b"Kaydara FBX Binary") or len(data) == 2_097_152:
                raise ValueError(f"Invalid source FBX {filename}")
            path.write_bytes(data)
        return path

    axes = SeatAxes(Take(source("sitting_idle.fbx")))
    pending = destination.with_suffix(".tmp")
    with pending.open("wb") as binary:
        binary.write(struct.pack("<4si", b"NOV1", len(TAKES)))
        for filename, hold, lower in TAKES:
            take = Take(source(filename))
            export_take(take, axes, filename, hold, lower, binary)
    pending.replace(destination)


if __name__ == "__main__":
    main(*(Path(arg).resolve() for arg in sys.argv[sys.argv.index("--") + 1:]))
