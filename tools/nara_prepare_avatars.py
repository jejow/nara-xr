"""Prepare Rocketbox actors, textures, and portraits for the NARA catalog."""

import argparse
import json
from pathlib import Path
import re
import shutil
import sys


FEMALE_NAMES = ("Nadia", "Maya", "Sari", "Dian", "Kirana", "Alma",
                "Vina", "Rena", "Lina", "Ratri", "Tara", "Dewi", "Laras", "Naya", "Mira")
MALE_NAMES = ("Raka", "Dimas", "Niko", "Arman", "Bima", "Aditya",
              "Galih", "Rizky", "Fajar", "Ari", "Rio", "Bagas", "Satria", "Jati", "Reza")
EXPRESSIONS = {"eyeblinkleft", "eyeblinkright", "jawopen", "mouthpucker",
               "mouthsmileleft", "mouthsmileright", "browinnerup"}


def choices():
    return [
        {"id": f"{kind}_Adult_{number:02}", "label": names[number - 1]}
        for number in range(1, len(FEMALE_NAMES) + 1)
        for kind, names in (("Female", FEMALE_NAMES), ("Male", MALE_NAMES))
    ]


def portrait(path, index):
    from PIL import Image, ImageChops, ImageOps

    with Image.open(path) as picture:
        rgba = picture.convert("RGBA")
    opacity = rgba.getchannel("A")
    if opacity.getextrema() == (255, 255):
        flat = rgba.convert("RGB")
        delta = ImageChops.difference(flat, Image.new("RGB", flat.size, flat.getpixel((0, 0))))
        opacity = delta.convert("L").point(lambda shade: 255 if shade > 15 else 0)
        rgba.putalpha(opacity)
    bounds = opacity.getbbox()
    if not bounds:
        raise RuntimeError(f"No visible model in the source portrait {path}")
    left, top, right, bottom = bounds
    sample_height = int((bottom - top) * .33)
    upper_box = opacity.crop((left, top, right, top + sample_height)).getbbox()
    center = left + (upper_box[0] + upper_box[2]) * .5
    side = int((bottom - top) * .38)
    crop_top = int(top - side * .07)
    head = rgba.crop((round(center - side / 2), crop_top, round(center + side / 2), crop_top + side))
    tile = Image.new("RGB", (192, 192), "#dce8e0" if index % 2 == 0 else "#d9e5ed")
    head = ImageOps.fit(head, tile.size, method=Image.Resampling.LANCZOS)
    tile.paste(head, mask=head.getchannel("A"))
    return tile


def prepare_images(raw, project, gallery):
    from PIL import Image, ImageDraw, ImageFont

    source = json.loads((raw.parent / "asset-sources.json").read_text(encoding="utf-8"))
    asset = project / "Assets/FinalProject"
    models = asset / "Characters/Rocketbox"
    photos = asset / "Resources/Nara"
    entries = json.loads((photos / "AvatarCatalog.json").read_text(encoding="utf-8"))["entries"]
    assert all(item["id"] in source["models"] for item in entries)
    models.mkdir(parents=True, exist_ok=True)
    photos.mkdir(parents=True, exist_ok=True)
    rows = (len(entries) + 5) // 6
    atlas = Image.new("RGB", (6 * 192, rows * 192), "#d9e6de")
    overview = Image.new("RGB", (1440, 104 + rows * 250), "#f4f6ef")
    draw = ImageDraw.Draw(overview)
    font = ImageFont.truetype(photos / "Fonts/Inter-Bold.ttf", 24)
    heading = ImageFont.truetype(photos / "Fonts/Inter-Bold.ttf", 42)
    draw.text((32, 23), f"{len(entries)} avatar panel NARA", fill="#143d37", font=heading)
    for i, actor in enumerate(entries):
        model_id = actor["id"]
        directory = models / model_id
        directory.mkdir(parents=True, exist_ok=True)
        for source_texture in (raw / model_id).glob("*_color.png"):
            shutil.copy2(source_texture, directory / source_texture.name)
        slot = actor["portrait"]
        tile = portrait(raw / model_id / f"{model_id}.png", slot)
        col, row = slot % 6, slot // 6
        atlas.paste(tile, (col * 192, row * 192))
        x, y = 28 + col * 235, 91 + row * 250
        overview.paste(tile.resize((208, 208), Image.Resampling.LANCZOS), (x, y))
        draw.text((x + 9, y + 212), actor["label"], fill="#143d37", font=font)
    atlas.save(photos / "AvatarPortraits.jpg", quality=92, subsampling=0)
    shutil.copy2(raw.parent/"LICENSE-Microsoft-Rocketbox.md", models/"LICENSE-Microsoft-Rocketbox.md")
    (models/"SourceAssets.json").write_text(json.dumps(source,indent=2)+"\n", encoding="utf-8")
    gallery.parent.mkdir(parents=True, exist_ok=True)
    overview.save(gallery, optimize=True)
    print(f"NARA_AVATAR_IMAGES {len(entries)} verified source portraits and texture sets")


def export_rigs(raw, project, ids):
    import bpy

    root = project / "Assets/FinalProject/Characters/Rocketbox"
    for actor in choices():
        model_id = actor["id"]
        if ids and model_id not in ids:
            continue
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(raw / model_id / f"{model_id}_facial.fbx"),
                                 use_anim=False, use_custom_props=False)
        meshes = [item for item in bpy.context.scene.objects if item.type == "MESH"]
        if not meshes:
            raise RuntimeError(f"Missing meshes in {model_id}")
        count, found = 0, set()
        for item in meshes:
            item.data.calc_loop_triangles()
            count += len(item.data.loop_triangles)
            shape_keys = item.data.shape_keys
            if shape_keys:
                for key in list(shape_keys.key_blocks)[1:]:
                    name = re.sub(r"^AK_\d+_", "", key.name).casefold()
                    if name in EXPRESSIONS:
                        found.add(name)
                    else:
                        item.shape_key_remove(key)
        if count < 3000 or count > 20000 or not {"eyeblinkleft", "eyeblinkright", "jawopen"}.issubset(found):
            raise RuntimeError(f"Incomplete face or invalid mesh for {model_id}: {count} triangles, {found}")
        file = root / model_id / "Actor.fbx"
        file.parent.mkdir(parents=True, exist_ok=True)
        bpy.ops.export_scene.fbx(filepath=str(file), object_types={"ARMATURE","MESH"},
            axis_forward="-Z",axis_up="Y", apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_ALL", add_leaf_bones=False,
            bake_anim=False, path_mode="STRIP")
        print(f"NARA_AVATAR_FBX {model_id} triangles={count} expressions={len(found)}", flush=True)


if __name__ == "__main__":
    inputs = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("raw", type=Path)
    parser.add_argument("project", type=Path)
    parser.add_argument("--gallery", type=Path)
    parser.add_argument("--ids", nargs="+", choices=[entry["id"] for entry in choices()])
    args = parser.parse_args(inputs)
    if args.gallery:
        prepare_images(args.raw.resolve(),args.project.resolve(),args.gallery.resolve())
    else:
        export_rigs(args.raw.resolve(), args.project.resolve(), args.ids)
