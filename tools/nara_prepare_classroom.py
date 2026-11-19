"""Fit the NARA panel and lectern into the texture-decoded tevy classroom."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector


PRESENTER = (.18, 3.42, 1.64)
SEATS = (
    ("NaraLeftSeat", "Near chair", (-1.20, .46, 0)),
    ("NaraCoachSeat", "Far chair", (.18, .60, 0)),
    ("NaraRightSeat", "Near chair", (1.56, .46, 0)),
)
DESK_OFFSET = .94
REAR_ROWS = (
    (-1.57, (-2.37, -1.19, 1.19, 2.37)),
    (-3.55, (-2.37, -1.19, 1.19)),
)
MONITOR_CENTER = (.18, 2.74, 1.156)
MONITOR_NORMAL = Vector((0, .38, .925)).normalized()
MONITOR_WIDTH = .422
MONITOR_HEIGHT = MONITOR_WIDTH * 9 / 16
SCREEN_WIDTH = 2.78
SCREEN_HEIGHT = SCREEN_WIDTH * 9 / 16

# The projection wall is at +Y. The presenter looks toward -Y.
MARKERS = {
    "NaraPresenter": PRESENTER,
    "NaraSightline": (.18, .60, 1.20),
    "NaraScreen": (.16, 3.865, 2.05),
    "NaraScreenEdge": (.16 + SCREEN_WIDTH / 2, 3.865, 2.05),
    "NaraLectern": (.18, 2.71, 0),
    "NaraConfidenceScreen": MONITOR_CENTER,
    "NaraConfidenceNormal": tuple(Vector(MONITOR_CENTER) + MONITOR_NORMAL),
}

FURNITURE_MATERIALS = {
    "White laminated wood": "Nara furniture laminate",
    "Black powder coated steel": "Nara furniture steel",
    "Black rubber foot caps": "Nara furniture rubber",
    "Dark fabric edge piping": "Nara furniture piping",
    "AI reference spiral fabric - approximate motif": "Nara furniture upholstery",
}


def append_furniture(path):
    root_names = ("Table", "Near chair", "Far chair")
    collection = bpy.data.collections.new("Meja dan kursi Blender tim")
    bpy.context.scene.collection.children.link(collection)
    with bpy.data.libraries.load(str(path.resolve()), link=False) as (source, target):
        target.objects = [
            name for name in source.objects
            if any(name == root or name.startswith(root + " ") for root in root_names)
        ]
    for obj in target.objects:
        collection.objects.link(obj)
    for name in root_names:
        if name not in collection.objects or not collection.objects[name].children:
            raise RuntimeError(f"Furniture source is missing {name}.")
    bpy.ops.object.select_all(action="DESELECT")
    curves = [obj for obj in collection.objects if obj.type == "CURVE"]
    if curves:
        for curve in curves:
            curve.select_set(True)
        bpy.context.view_layer.objects.active = curves[0]
        bpy.ops.object.convert(target="MESH")
    materials = {mat for obj in collection.objects if obj.type == "MESH" for mat in obj.data.materials}
    for mat in materials:
        if mat.name in FURNITURE_MATERIALS:
            mat.name = FURNITURE_MATERIALS[mat.name]
        elif "piping" in mat.name.lower():
            mat.name = "Nara furniture piping"
        else:
            raise RuntimeError(f"Unexpected material in team furniture: {mat.name}")
    fabric = next(mat for mat in materials if mat.name == "Nara furniture upholstery")
    image = next(node.image for node in fabric.node_tree.nodes if node.type == "TEX_IMAGE" and node.image)
    if not image.packed_file:
        raise RuntimeError("The team chair texture must be packed in the source Blender file.")
    return collection, image.packed_file.data


def duplicate_furniture(root, collection, label):
    sources = [root] + list(root.children_recursive)
    copies = {obj: obj.copy() for obj in sources}
    for source, item in copies.items():
        collection.objects.link(item)
        if item.type == "MESH":
            # The FBX exporter needs a unique mesh for each copied material layout.
            item.data = source.data.copy()
        if source.parent in copies:
            item.parent = copies[source.parent]
    copies[root].name = label
    return copies[root]


def mesh_bounds(items):
    points = [obj.matrix_world @ vert.co for obj in items if obj.type == "MESH" for vert in obj.data.vertices]
    return tuple(Vector([op(p[i] for p in points) for i in range(3)]) for op in (min, max))


def place_furniture(collection, name, chair_source, position, forward):
    chair = duplicate_furniture(collection.objects[chair_source], collection, name + " furniture")
    table = duplicate_furniture(collection.objects["Table"], collection, name + " desk")
    yaw = math.atan2(-forward.x, forward.y)
    chair.location, chair.rotation_euler.z = position, math.pi + yaw
    table.location, table.rotation_euler.z = position + forward * DESK_OFFSET, yaw
    chair["source"] = "Team 9 photo-reference Blender furniture"
    table["source"] = chair["source"]
    return table, chair


def fit_furniture(room, furniture, upholstery):
    for name in ("chairs", "desks", "teacher_desk", "bags_bin"):
        bpy.data.objects.remove(room.objects[name], do_unlink=True)

    collection, texture = append_furniture(furniture)
    panel = []
    for marker, source_name, coordinates in SEATS:
        anchor = Vector(coordinates)
        forward = Vector((PRESENTER[0] - anchor.x, PRESENTER[1] - anchor.y, 0)).normalized()
        panel.append((marker, *place_furniture(collection, marker, source_name, anchor, forward)))

    for row, (y, columns) in enumerate(REAR_ROWS, 1):
        for index, x in enumerate(columns, 1):
            place_furniture(collection, f"Rear row {row} seat {index}", "Near chair",
                            Vector((x, y, 0)), Vector((0, 1, 0)))
    for name in ("Table", "Near chair", "Far chair"):
        source = collection.objects[name]
        for item in list(source.children_recursive) + [source]:
            bpy.data.objects.remove(item, do_unlink=True)

    bpy.context.view_layer.update()
    markers = dict(MARKERS)
    tabletop_bounds = []
    for name, table, chair in panel:
        cushion = next(obj for obj in chair.children_recursive if "padded seat" in obj.name)
        back = next(obj for obj in chair.children_recursive if "upholstered back both faces" in obj.name)
        _, top = mesh_bounds([cushion])
        low, high = mesh_bounds(table.children_recursive)
        back_min, back_max = mesh_bounds([back])
        if ((back_min + back_max) / 2 - chair.location).dot(table.location - chair.location) >= 0:
            raise RuntimeError(f"Chair back faces the desk at {name}.")
        tabletop_bounds.append((low.x, high.x))
        if not .46 < top.z < .48 or not .75 < high.z < .77 or abs(low.z) > .005:
            raise RuntimeError(f"Unexpected furniture scale at {name}.")
        # Calibrate the avatar socket to the chair cushion rather than the model origin.
        markers[name] = (chair.location.x, chair.location.y, top.z - .481)
        print(f"NARA_PANEL {name} seat={top.z:.3f} m table={high.z:.3f} m")
    if any(a[1] >= b[0] for a, b in zip(tabletop_bounds, tabletop_bounds[1:])):
        raise RuntimeError(f"Panel desk bounds overlap: {tabletop_bounds}.")
    print(f"NARA_TEAM_FURNITURE panel={len(panel)} rear={sum(len(columns) for _, columns in REAR_ROWS)}")
    upholstery.parent.mkdir(parents=True, exist_ok=True)
    upholstery.write_bytes(texture)
    return markers


def add_lectern_body():
    x, y, floor = MARKERS["NaraLectern"]
    body = color("NARA matte navy body", (.055, .093, .124, 1), .38)
    reading = color("NARA reading surface", (.064, .082, .085, 1), .50)
    detail = color("NARA teal front detail", (.064, .228, .208, 1), .44)
    parts = (
        ("Lectern floor base", (x, y, floor + .028), (.62, .48, .056), body, .014),
        ("Lectern column", (x, y + .042, floor + .545), (.177, .201, 1.03), body, .016),
        ("Lectern front panel", (x, y - .085, floor + .59), (.411, .072, .86), body, .023),
        ("Lectern front inlay", (x, y - .125, floor + .59), (.036, .009, .683), detail, .004),
        ("Presenter lectern", (x, y, floor + 1.069), (.66, .52, .062), reading, .018),
    )
    for name, point, size, material, bevel in parts:
        item = box(name, point, size, material)
        edges = item.modifiers.new("Rounded edges", "BEVEL")
        edges.width, edges.segments = bevel, 3
        item.modifiers.new("Face-weighted normals", "WEIGHTED_NORMAL")
        item["source"] = "NARA Blender lectern"


def fit_lectern():
    add_lectern_body()
    trim = color("Nara confidence bezel", (.047, .058, .063, 1), .43)
    bezel = box("Lectern confidence monitor", Vector(MONITOR_CENTER) - MONITOR_NORMAL * .013,
                (MONITOR_WIDTH + .027, MONITOR_HEIGHT + .027, .022), trim)
    bezel.rotation_euler.x = -math.atan2(MONITOR_NORMAL.y, MONITOR_NORMAL.z)


def color(name, rgba, roughness=.55):
    material = bpy.data.materials.new(name)
    material.diffuse_color = rgba
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = rgba
    shader.inputs["Roughness"].default_value = roughness
    return material


def box(name, point, dimensions, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=point)
    item = bpy.context.object
    item.name = name
    item.dimensions = dimensions
    item.data.materials.append(material)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return item


def preview_slide(path, name, center, right, up, width, height):
    material = color("NARA sample slide preview", (1, 1, 1, 1))
    tree = material.node_tree
    image = tree.nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(path))
    emitter = tree.nodes.new("ShaderNodeEmission")
    emitter.inputs["Strength"].default_value = .70
    tree.links.new(image.outputs["Color"], emitter.inputs["Color"])
    tree.links.new(emitter.outputs["Emission"], tree.nodes.get("Material Output").inputs["Surface"])
    center = Vector(center)
    w = Vector(right) * (width / 2)
    h = Vector(up) * (height / 2)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(
        [center - w - h, center + w - h, center + w + h, center - w + h],
        [], [(0, 1, 2, 3)],
    )
    uv = mesh.uv_layers.new()
    for i, coordinate in enumerate(((0, 0), (1, 0), (1, 1), (0, 1))):
        uv.data[i].uv = coordinate
    item = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(item)
    mesh.materials.append(material)


def aim(item, focus):
    item.rotation_euler = (Vector(focus) - item.location).to_track_quat("-Z", "Y").to_euler()


def light(name, position, focus, watts, size, tint, height=None):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.size, data.color = watts, size, tint
    if height is not None:
        data.shape, data.size_y = "RECTANGLE", height
    item = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(item)
    item.location = position
    aim(item, focus)


def configure_preview(scene):
    camera_data = bpy.data.cameras.new("Sudut pembicara")
    camera = bpy.data.objects.new("Sudut pembicara", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = PRESENTER
    camera_data.lens = 22.5
    aim(camera, MARKERS["NaraSightline"])
    scene.camera = camera
    overview_data = bpy.data.cameras.new("Sudut ruangan")
    overview = bpy.data.objects.new("Sudut ruangan", overview_data)
    bpy.context.collection.objects.link(overview)
    overview.location = (3.11, -3.18, 2.03)
    overview_data.lens = 20
    aim(overview, (.26, 1.78, 1.21))
    light("Window daylight", (-3.72, -.35, 1.77), (.25, -.1, .8), 690, 5.0, (.82, .91, 1), 1.42)
    for y in (-2.25, 0, 2.25):
        light("Ceiling bounce", (0, y, 2.62), (0, y, .8), 125, 3.5, (1, .94, .84), 1.28)
    light("Presentation wall fill", (2.35, 1.37, 2.30), (.10, 3.78, 1.73), 100, 2.1, (1, .96, .89))
    scene.world = bpy.data.worlds.new("Daylight ambient")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .18
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 14
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = .35


def main(options):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(options.source.resolve()))
    scene = bpy.context.scene
    required = {"shell", "wall_front", "ceiling", "chairs", "desks", "teacher_desk"}
    if not required.issubset(set(scene.objects.keys())):
        raise RuntimeError("Expected tevy Classroom v2 geometry is missing.")
    scene.unit_settings.system = "METRIC"
    for obj in scene.objects:
        if obj.type == "MESH":
            obj["source"] = "tevy Classroom v2, CC0 1.0"
    markers = fit_furniture(scene, options.furniture, options.upholstery.resolve())
    fit_lectern()
    frame = color("Nara screen frame", (.072, .093, .102, 1))
    projector = color("Nara projector body", (.76, .78, .76, 1))
    lens = color("Nara projector lens", (.054, .11, .13, 1), .18)
    x, y, z = MARKERS["NaraScreen"]
    box("Pull-down projection screen", (x, y+.032, z), (SCREEN_WIDTH+.10, .055, SCREEN_HEIGHT+.09), frame)
    box("Projection screen cassette", (x, y+.029, z+SCREEN_HEIGHT/2+.059), (SCREEN_WIDTH+.20, .116, .082), projector)
    box("Ceiling projector", (x, 1.10, 2.57), (.398, .277, .102), projector)
    box("Projector ceiling mount", (x, 1.10, 2.72), (.098, .124, .20), frame)
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=.034, depth=.035,
        location=(x-.096, 1.248, 2.565), rotation=(1.5707963, 0, 0))
    bpy.context.object.name = "Projector lens"
    bpy.context.object.data.materials.append(lens)
    for name, point in markers.items():
        item = bpy.data.objects.new(name, None)
        item.empty_display_type = "PLAIN_AXES"
        item.empty_display_size = .18
        item.location = point
        bpy.context.collection.objects.link(item)
    bpy.context.view_layer.update()
    mesh_faces = sum(len(item.data.polygons) for item in scene.objects if item.type == "MESH")
    options.fbx.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=str(options.fbx.resolve()), use_selection=True,
        object_types={"MESH", "EMPTY"}, bake_anim=False, axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", path_mode="STRIP")
    print(f"NARA_ROOM_FBX meshes and anchors exported, {mesh_faces} polygons")

    preview_slide(options.slide.resolve(), "Slide proyeksi contoh", MARKERS["NaraScreen"],
                  (1, 0, 0), (0, 0, 1), SCREEN_WIDTH, SCREEN_HEIGHT)
    preview_slide(options.slide.resolve(), "Slide monitor contoh", MONITOR_CENTER,
                  (-1, 0, 0), MONITOR_NORMAL.cross(Vector((-1, 0, 0))), MONITOR_WIDTH, MONITOR_HEIGHT)
    configure_preview(scene)
    options.blend.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = "//Tata-Ruang.png" if options.skip_render else str(options.preview.resolve())
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(options.blend.resolve()))
    if not options.skip_render:
        bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "fbx", "blend", "preview", "slide"):
        parser.add_argument(name, type=Path)
    for name in ("furniture", "upholstery"):
        parser.add_argument("--" + name, required=True, type=Path)
    parser.add_argument("--skip-render", action="store_true")
    main(parser.parse_args(sys.argv[sys.argv.index("--")+1:]))
