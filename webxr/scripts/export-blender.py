"""Export the two authored NARA rooms and skinned Rocketbox avatars to GLB."""
import json
from collections import defaultdict
from pathlib import Path
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
MEDIA = ROOT / "webxr/public/media"
FINAL = ROOT / "NARA/Assets/FinalProject"
ROOMS = {"university": "NARA-Kelas-Presentasi.blend", "if110": "NARA-IF-101.blend"}
EXPRESSIONS = ("EyeBlinkLeft", "EyeBlinkRight", "JawOpen", "MouthPucker", "MouthSmileLeft",
               "MouthSmileRight", "BrowInnerUp", "MouthFunnel", "MouthClose")


def web_vector(vector):
    return [round(x, 6) for x in (vector.x, vector.z, -vector.y)]


def export(path, avatars=False):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
                             export_apply=not avatars, export_animations=False,
                             export_morph=avatars, export_morph_normal=False,
                             export_morph_tangent=False, export_cameras=False,
                             export_lights=False, export_extras=False, export_image_format="AUTO")


def room(name, filename):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / "assets/blender" / filename))
    scene = bpy.context.scene
    objects = list(scene.objects)
    markers = {o.name: web_vector(o.matrix_world.translation) for o in objects
               if o.type == "EMPTY" and o.name.startswith("Nara")}
    if len([key for key in markers if key.startswith("NaraAudienceSeat_")]) != 24:
        raise RuntimeError(f"Invalid seat layout: {name}")
    cameras = {o.name: {"position": web_vector(o.matrix_world.translation),
                       "direction": web_vector(o.matrix_world.to_quaternion() @ Vector((0, 0, -1)))}
               for o in objects if o.type == "CAMERA"}
    considered = [o for o in objects if o.type == "MESH" and not o.name.startswith("Slide ")
                  and not o.hide_render and o.visible_get()]
    evaluated = bpy.context.evaluated_depsgraph_get()
    collection = bpy.data.collections.new("WebXR classroom")
    scene.collection.children.link(collection)
    merged = defaultdict(list)
    for obj in considered:
        data = bpy.data.meshes.new_from_object(obj.evaluated_get(evaluated),
                                             preserve_all_data_layers=True, depsgraph=evaluated)
        label = tuple(m.name for m in data.materials if m)
        item = bpy.data.objects.new(obj.name + " WebXR", data)
        collection.objects.link(item)
        item.matrix_world = obj.matrix_world.copy()
        merged[label].append(item)
    for obj in objects:
        bpy.data.objects.remove(obj, do_unlink=True)
    for label, group in merged.items():
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.view_layer.objects.active = group[0]
        for item in group:
            item.select_set(True)
        if len(group) > 1:
            bpy.ops.object.join()
        group[0].name = label[0] if label else "Classroom details"
    for image in bpy.data.images:
        width, height = image.size[:]
        if max(width, height) > 1024:
            factor = 1024 / max(width, height)
            image.scale(round(width * factor), round(height * factor))
            image.pack()
    export(MEDIA / f"rooms/{name}.glb")
    (MEDIA / "rooms" / f"{name}.json").write_text(
        json.dumps({"markers": markers, "cameras": cameras}, ensure_ascii=False), encoding="utf-8")
    print(f"EXPORTED ROOM {name} groups={len(merged)}", flush=True)


def bind_material(material, folder, converted):
    name = material.name.split(".")[0]
    texture = converted / f"{name}_color.jpg"
    opacity = folder / f"{name}_color.png" if name.endswith("opacity") else None
    if not texture.exists() and not (opacity and opacity.exists()):
        return
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    surface = nodes.new("ShaderNodeBsdfPrincipled")
    surface.inputs["Roughness"].default_value = .73
    surface.inputs["Metallic"].default_value = 0
    image = nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(opacity if opacity and opacity.exists() else texture), check_existing=True)
    if opacity and max(image.image.size) > 512:
        width, height = image.image.size[:]
        ratio = 512 / max(width, height)
        image.image.scale(round(width * ratio), round(height * ratio))
        image.image.pack()
    links = material.node_tree.links
    links.new(surface.outputs["BSDF"], output.inputs["Surface"])
    links.new(image.outputs["Color"], surface.inputs["Base Color"])
    if opacity:
        cut = nodes.new("ShaderNodeMath")
        cut.operation = "GREATER_THAN"
        cut.inputs[1].default_value = .42
        links.new(image.outputs["Alpha"], cut.inputs[0])
        links.new(cut.outputs[0], surface.inputs["Alpha"])


def actor(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    from tempfile import TemporaryDirectory
    folder = FINAL / "Characters/Rocketbox" / name
    bpy.ops.import_scene.fbx(filepath=str(folder / "Actor.fbx"), use_anim=False)
    with TemporaryDirectory(prefix="nara-avatar-") as work:
        converted = Path(work)
        for texture in folder.glob("*_color.png"):
            if "opacity" in texture.name:
                continue
            image = bpy.data.images.load(str(texture), check_existing=True)
            width, height = image.size[:]
            if max(width, height) > 512:
                scale = 512 / max(width, height)
                image.scale(round(width * scale), round(height * scale))
            image.filepath_raw = str(converted / (texture.stem + ".jpg"))
            image.file_format = "JPEG"
            image.save(quality=88)
        for material in list(bpy.data.materials):
            if material.users:
                bind_material(material, folder, converted)
        for obj in bpy.context.scene.objects:
            if obj.type != "MESH" or not obj.data.shape_keys:
                continue
            for key in list(obj.data.shape_keys.key_blocks)[1:][::-1]:
                if not key.name.lower().endswith(tuple(s.lower() for s in EXPRESSIONS)):
                    obj.shape_key_remove(key)
        export(MEDIA / "avatars" / f"{name}.glb", avatars=True)
    print(f"EXPORTED AVATAR {name}", flush=True)


args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if not args or "rooms" in args:
    for key, filename in ROOMS.items():
        room(key, filename)
if not args or "avatars" in args:
    for entry in json.loads((FINAL / "Resources/Nara/AvatarCatalog.json").read_text(encoding="utf-8-sig"))["entries"]:
        actor(entry["id"])
else:
    for name in args:
        if name.startswith(("Male_Adult_", "Female_Adult_")):
            actor(name)
