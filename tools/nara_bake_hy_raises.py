"""Fit seated HY-Motion FBXs to NARA's existing permission-to-speak poses.

Run with Blender --background --python tools/nara_bake_hy_raises.py -- \
    OVERTE_BANK LOCAL_BANK HY_RAISE.fbx [OTHER_HY_RAISE.fbx ...]
"""

import math
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from nara_bake_overte_motion import EXPORT_RATE, SeatAxes, Take, sample_pose


def lerp(a, b, t):
    return a + (b - a) * t


def limit(value, lo, hi):
    return min(hi, max(lo, value))


def sample(frames, phase):
    offset = limit(phase, 0, 1) * (len(frames) - 1)
    index = min(int(offset), len(frames) - 2)
    return [lerp(a, b, offset - index) for a, b in zip(frames[index], frames[index + 1])]


def phase_curve(poses):
    heights = [pose[4] for pose in poses]
    displacement = heights[-1] - heights[0]
    if abs(displacement) < .15:
        raise ValueError("The source has too little wrist travel for a hand raise.")
    curve = [(value - heights[0]) / displacement for value in heights]
    for index in range(1, len(curve)):
        curve[index] = max(curve[index - 1], limit(curve[index], 0, 1))
    return [(value,) for value in curve]


def source_poses(path):
    take = Take(path)
    mapping = {"spine": "spine1", "spine2": "spine3", "neck": "neck", "head": "head"}
    for side in ("right", "left"):
        prefix = side[0] + "_"
        mapping.update({side + to: prefix + origin for to, origin in (
            ("upleg", "hip"), ("leg", "knee"), ("foot", "ankle"),
            ("arm", "shoulder"), ("forearm", "elbow"), ("hand", "wrist"),
            ("handmiddle1", "middle1"), ("handindex1", "index1"), ("handpinky1", "pinky1"),
        )})
    take.bones = {alias: take.bones[origin] for alias, origin in mapping.items()}
    count = math.ceil(take.duration * EXPORT_RATE) + 1
    if not 2 <= take.duration <= 12:
        raise ValueError("Use a seated hand raise approximately 2 to 12 seconds long.")
    axes = SeatAxes(take)
    poses = []
    for index in range(count):
        take.at(take.duration * index / (count - 1))
        for side in ("right", "left"):
            thigh = take.point(side + "leg") - take.point(side + "upleg")
            if abs(thigh.z) > .45 * thigh.length:
                raise ValueError(f"{path.name} leaves the seated pose at sample {index}.")
        poses.append(sample_pose(take, axes))
    return poses


def read_templates(path):
    with path.open("rb") as stream:
        magic, count = struct.unpack("<4si", stream.read(8))
        if magic != b"NOV1" or count < 3:
            raise ValueError("Invalid Overte bank.")
        tracks = []
        for _ in range(2):
            frames, duration, hold, lower = struct.unpack("<ifff", stream.read(16))
            if not 2 <= frames <= 16000 or not 0 < hold < lower < duration:
                raise ValueError("Invalid right-hand template.")
            poses = list(struct.iter_unpack("<30f", stream.read(frames * 120)))
            if len(poses) != frames:
                raise ValueError("Truncated right-hand template.")
            tracks.append((duration, hold, lower, poses))
        return tracks


def bake(poses, template, rise, wait, release, influence, output):
    duration, template_hold, template_lower, neutral = template
    apex = max(range(len(poses)), key=lambda index: poses[index][4])
    if apex < 2 or apex >= len(poses) - 2:
        raise ValueError("The model must raise and then lower its wrist within the clip.")
    incoming = poses[:apex + 1]
    outgoing = poses[apex:]
    up, down = phase_curve(incoming), phase_curve(outgoing)
    center = poses[apex]
    hold_start, hold_end = max(0, apex - 4), min(len(poses) - 1, apex + 4)
    close = poses[hold_start:hold_end + 1]
    clip_time = rise + wait + release
    count = math.ceil(clip_time * EXPORT_RATE) + 1
    output.write(struct.pack("<ifff", count, clip_time, rise, rise + wait))
    for index in range(count):
        time = clip_time * index / (count - 1)
        if time < rise:
            phase = time / rise
            source = sample(incoming, phase)
            reference = sample(neutral, template_hold / duration * lerp(phase, sample(up, phase)[0], influence))
        elif time < rise + wait:
            phase = (time - rise) / wait
            source = sample(close, phase)
            window = min(template_lower - template_hold, wait)
            reference = sample(neutral, (template_hold + window * phase) / duration)
        else:
            phase = limit((time - rise - wait) / release, 0, 1)
            source = sample(outgoing, phase)
            progress = lerp(phase, sample(down, phase)[0], influence)
            reference = sample(neutral, lerp(template_lower, duration, progress) / duration)
        # Let the capture vary the arm's path within the range of the fitted chair pose.
        # The permission state still controls how long the raised pose holds.
        envelope = limit(min(time, clip_time - time) / .28, 0, 1)
        for axis in (3, 5, 6, 7, 8):
            reference[axis] += limit((source[axis] - center[axis]) * influence, -.036, .036) * envelope
        for axis in range(3):
            reference[axis] += limit(source[axis] * influence, -3.5, 3.5) * envelope
        output.write(struct.pack("<30f", *reference))


def main(bank, destination, *fbxs):
    if not 1 <= len(fbxs) <= 4:
        raise ValueError("Provide between one and four seated right-hand FBXs.")
    templates = read_templates(bank)
    timings = ((.56, 1.18, .47, .40), (.72, 1.45, .52, .61))
    sources = [source_poses(fbx) for fbx in fbxs]
    pending = destination.with_suffix(".tmp")
    with pending.open("wb") as output:
        output.write(struct.pack("<4si", b"NHY1", 2 * len(fbxs)))
        for poses in sources:
            for template, timing in zip(templates, timings):
                bake(poses, template, *timing, output)
    pending.replace(destination)
    print(f"NARA_HY_BAKE {len(fbxs)} generated source(s), {2 * len(fbxs)} retimed takes")


if __name__ == "__main__":
    main(*(Path(arg).resolve() for arg in sys.argv[sys.argv.index("--") + 1:]))
