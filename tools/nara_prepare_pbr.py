"""Prepare Unity PBR maps from the classroom GLB and the generated IF 101 floor."""

from io import BytesIO
import json
from pathlib import Path
import struct

import numpy as np
from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
ENVIRONMENT = ROOT / "NARA/Assets/FinalProject/Environment"


def embedded_images(source):
    content = source.read_bytes()
    document_size = struct.unpack_from("<I", content, 12)[0]
    document = json.loads(content[20:20 + document_size])
    binary_start = 28 + document_size
    images = {}
    for image in document["images"]:
        view = document["bufferViews"][image["bufferView"]]
        start = binary_start + view.get("byteOffset", 0)
        images[image["name"]] = content[start:start + view["byteLength"]]
    return images


def normal_map(height, strength, path):
    dx = np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)
    dy = np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)
    normal = np.stack((-strength * dx, strength * dy, np.ones_like(height)), axis=-1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    Image.fromarray(np.uint8(np.rint((normal * .5 + .5) * 255))).save(path)


def main():
    images = embedded_images(ROOT / "assets/blender/tevy-classroom-unpacked.glb")
    (ENVIRONMENT / "ClassroomNormal.png").write_bytes(images["classroom_normal"])
    ao, roughness, metallic = Image.open(BytesIO(images["classroom_orm"])).convert("RGB").split()
    smoothness = roughness.point(lambda value: 255 - value)
    Image.merge("RGBA", (metallic, ao, ao, smoothness)).save(ENVIRONMENT / "ClassroomSurface.png")

    # At the scale of the original tile, the luminance difference isolates the grout.
    floor = np.asarray(Image.open(ENVIRONMENT / "IF101Floor.png").convert("L"), dtype=np.float32)
    tile = np.clip((floor - 70) / 125, 0, 1)
    normal_map(tile, .42, ENVIRONMENT / "IF101FloorNormal.png")
    finish = np.zeros((*floor.shape, 4), dtype=np.uint8)
    finish[..., 3] = np.uint8(43 + 112 * tile)
    Image.fromarray(finish).save(ENVIRONMENT / "IF101FloorSurface.png")

    noise = np.random.default_rng(101).random((128, 128))
    grain = (4 * noise + sum(np.roll(noise, shift, axis) for axis in (0, 1) for shift in (-1, 1))) / 8
    normal_map(grain, .68, ENVIRONMENT / "DeskLaminateNormal.png")


if __name__ == "__main__":
    main()
