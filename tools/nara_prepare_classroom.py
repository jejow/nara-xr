"""Fit four classroom rows and an oak lectern into the tevy room."""

import argparse
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
import numpy as np


PRESENTER = (.18, 3.38, 1.69)
ROWS = (.46, -.80, -2.06, -3.32)
COLUMNS = (2.96, 2.00, 1.04, -1.04, -2.00, -2.96)
DESK_OFFSET = .58
DESK_SCALE = (.84, .72, 1)
DESK_TOP = "Table white trapezoidal top"
MONITOR_CENTER = (.18, 2.73, 1.151)
MONITOR_NORMAL = Vector((0, .24, .971)).normalized()
MONITOR_WIDTH = .422
MONITOR_HEIGHT = MONITOR_WIDTH * 9 / 16
SCREEN_WIDTH = 2.78
SCREEN_HEIGHT = SCREEN_WIDTH * 9 / 16

# The projection wall is at +Y. The presenter looks toward -Y.
MARKERS = {
    "NaraPresenter": PRESENTER,
    "NaraSightline": (0, -.35, 1.20),
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


def append_furniture(path, *root_names):
    collection = bpy.data.collections.new("Meja dan kursi Blender tim")
    bpy.context.scene.collection.children.link(collection)
    with bpy.data.libraries.load(str(path.resolve()), link=False) as (source, target):
        target.objects = [
            name for name in source.objects
            if any(name == root or name.startswith(root + " ") for root in root_names)
        ]
    for obj in target.objects:
        collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    for root_name in root_names:
        root = collection.objects.get(root_name)
        if not root or not root.children:
            raise RuntimeError(f"The team furniture source is missing {root_name}.")
        for obj in list(root.children_recursive):
            if root_name == "Near chair" and ("edge piping" in obj.name or "rubber foot" in obj.name):
                bpy.data.objects.remove(obj, do_unlink=True)
            else:
                obj.select_set(True)
    bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
    bpy.ops.object.convert(target="MESH")
    for obj in collection.objects:
        if obj.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = obj
        decimate = obj.modifiers.new("Classroom mesh density", "DECIMATE")
        if obj.name.startswith("Table "):
            decimate.ratio = .62 if obj.name == DESK_TOP else .24
        else:
            decimate.ratio = .50 if "padded" in obj.name or "upholstered" in obj.name else .34
        bpy.ops.object.modifier_apply(modifier=decimate.name)
        if obj.name == DESK_TOP:
            # The original top uses a flat color. Give its detail normal metric UVs.
            uv = obj.data.uv_layers.active or obj.data.uv_layers.new(name="Laminate UV")
            for face in obj.data.polygons:
                normal = max(range(3), key=lambda axis: abs(face.normal[axis]))
                axes = [axis for axis in range(3) if axis != normal]
                for index in face.loop_indices:
                    point = obj.data.vertices[obj.data.loops[index].vertex_index].co
                    uv.data[index].uv = (point[axes[0]], point[axes[1]])
    materials = {mat for obj in collection.objects if obj.type == "MESH" for mat in obj.data.materials}
    for mat in materials:
        if mat.name in FURNITURE_MATERIALS:
            mat.name = FURNITURE_MATERIALS[mat.name]
        elif "piping" in mat.name.lower():
            mat.name = "Nara furniture piping"
        else:
            raise RuntimeError(f"Unexpected material in team furniture: {mat.name}")
    return collection


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


def place_team_desk(source, collection, label, x, y):
    desk = duplicate_furniture(source, collection, "Classroom desk " + label)
    desk.location = (x, y, 0)
    desk.scale = DESK_SCALE
    return desk


def check_desk_fit(desks, rows, columns, offset, chair_back):
    bpy.context.view_layer.update()
    low, high = mesh_bounds(desks[0].children_recursive)
    width, depth = high.x - low.x, high.y - low.y
    if not .91 < width < .94 or not .50 < depth < .54 or not .75 < high.z < .77:
        raise RuntimeError(f"Unexpected team desk size: {width:.3f} x {depth:.3f} x {high.z:.3f} m.")
    aisle = min(x for x in columns if x > 0) - max(x for x in columns if x < 0) - width
    pitch = min(abs(a-b) for a, b in zip(columns, columns[1:]))
    gap = min(abs(a-b) for a, b in zip(rows, rows[1:])) - offset - depth/2 - chair_back
    if aisle < 1.10 or pitch - width < .025 or gap < .065:
        raise RuntimeError(f"Insufficient desk spacing: aisle {aisle:.3f} m, row clearance {gap:.3f} m.")
    print(f"NARA_TEAM_DESKS source=Meja-Kursi.blend count={len(desks)} "
          f"size={width:.3f}x{depth:.3f}x{high.z:.3f} m aisle={aisle:.3f} m row_gap={gap:.3f} m",
          flush=True)


def fit_furniture(room, furniture, upholstery):
    for name in ("chairs", "desks", "teacher_desk", "bags_bin"):
        bpy.data.objects.remove(room.objects[name], do_unlink=True)

    collection = append_furniture(furniture, "Near chair", "Table")
    source_chair = collection.objects["Near chair"]
    source_table = collection.objects["Table"]
    fabric = bpy.data.materials["Nara furniture upholstery"]
    image = next(node.image for node in fabric.node_tree.nodes if node.type == "TEX_IMAGE" and node.image)
    if not image.packed_file:
        raise RuntimeError("The team chair texture must be packed in the source Blender file.")
    markers = dict(MARKERS)
    bpy.context.view_layer.update()
    cushion = next(obj for obj in source_chair.children_recursive if "padded seat" in obj.name)
    _, seat_top = mesh_bounds([cushion])
    if not .46 < seat_top.z < .48:
        raise RuntimeError("The team chair cushion does not match the seated avatar height.")
    desks = []
    for row, y in enumerate(ROWS, 1):
        for column, x in enumerate(COLUMNS, 1):
            label = f"R{row:02d}_C{column:02d}"
            chair = duplicate_furniture(source_chair, collection, "Classroom chair " + label)
            chair.location, chair.rotation_euler.z = (x, y, 0), math.pi
            desks.append(place_team_desk(source_table, collection, label, x, y + DESK_OFFSET))
            markers["NaraAudienceSeat_" + label] = (x, y, seat_top.z - .481)
    for source in (source_chair, source_table):
        for item in list(source.children_recursive) + [source]:
            bpy.data.objects.remove(item, do_unlink=True)

    check_desk_fit(desks, ROWS, COLUMNS, DESK_OFFSET, .31)
    upholstery.parent.mkdir(parents=True, exist_ok=True)
    upholstery.write_bytes(image.packed_file.data)
    return markers


def lectern_oak(texture_path):
    grid = np.linspace(0, 1, 512, endpoint=False, dtype=np.float32)
    u, v = np.meshgrid(grid, grid)
    grain = (5 * np.sin(math.tau * (19*u + .23*np.sin(math.tau*v))) +
             3 * np.cos(math.tau * (44*u + .8*v)) +
             2 * np.sin(math.tau * (116*u + 1.7*v)))
    rng = np.random.default_rng(17)
    grain += .8 * rng.normal(0, 1, grain.shape)
    pixels = np.empty((512, 512, 4), dtype=np.float32)
    pixels[:, :, :3] = (np.array((171, 121, 77)) + grain[..., None]) / 255
    pixels[:, :, 3] = 1
    image = bpy.data.images.new("LecternOak.png", width=512, height=512)
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(texture_path)
    image.file_format = "PNG"
    image.save()
    image.pack()
    material = color("NARA lectern oak", (.41, .25, .12, 1), .46)
    nodes = material.node_tree.nodes
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    material.node_tree.links.new(texture.outputs["Color"], nodes.get("Principled BSDF").inputs["Base Color"])
    return material


def fit_lectern(texture_path):
    x, y, floor = MARKERS["NaraLectern"]
    oak = lectern_oak(texture_path)
    body = color("NARA lectern graphite", (.043, .055, .057, 1), .44)
    deck = color("NARA reading surface", (.045, .064, .064, 1), .58)
    hardware = color("NARA lectern hardware", (.038, .041, .046, 1), .32)
    angle = -math.atan2(MONITOR_NORMAL.y, MONITOR_NORMAL.z)
    parts = (
        ("Lectern plinth", (x, y, floor+.025), (.708, .491, .05), body, .014),
        ("Lectern support", (x, y-.042, floor+.552), (.406, .300, .97), body, .022),
        ("Lectern oak audience panel", (x, y-.207, floor+.566), (.592, .042, .865), oak, .014),
        ("Lectern oak left return", (x-.292, y-.043, floor+.585), (.055, .337, .885), oak, .014),
        ("Lectern oak right return", (x+.292, y-.043, floor+.585), (.055, .337, .885), oak, .014),
        ("Lectern service cover", (x, y+.116, floor+.625), (.302, .016, .620), body, .007),
        ("Lectern rear shelf", (x, y+.125, floor+.884), (.405, .177, .032), body, .010),
    )
    for name, point, size, material, bevel in parts:
        rounded_box(name, point, size, material, bevel)
    for label, point, size, material, bevel in (
        ("Presenter lectern", (x, y, floor+1.066), (.790, .525, .058), oak, .018),
        ("Inclined writing surface", (x, y+.005, floor+1.095), (.725, .470, .015), deck, .007),
        ("Oak paper rest", (x, y+.222, floor+1.063), (.655, .025, .027), oak, .007),
    ):
        item = rounded_box(label, point, size, material, bevel)
        item.rotation_euler.x = angle
    trim = color("Nara confidence bezel", (.047, .058, .063, 1), .43)
    bezel = rounded_box("Lectern confidence monitor", Vector(MONITOR_CENTER) - MONITOR_NORMAL*.019,
                        (MONITOR_WIDTH+.030, MONITOR_HEIGHT+.030, .031), trim, .008)
    bezel.rotation_euler.x = angle
    stem_x, stem_y = x + .293, y - .152
    rounded_box("Lectern microphone mount", (stem_x, stem_y, 1.143), (.054, .058, .027), hardware, .008)
    cable = bpy.data.curves.new("Lectern microphone stem", "CURVE")
    cable.dimensions, cable.bevel_depth, cable.bevel_resolution = "3D", .007, 1
    spline = cable.splines.new("BEZIER")
    spline.bezier_points.add(2)
    for point, coordinate in zip(spline.bezier_points, (
        (stem_x, stem_y, 1.153), (stem_x, stem_y+.022, 1.297), (stem_x-.047, stem_y+.086, 1.359),
    )):
        point.co = coordinate
        point.handle_left_type = point.handle_right_type = "AUTO"
    cable.resolution_u = 6
    cable.materials.append(hardware)
    stem = bpy.data.objects.new("Lectern microphone stem", cable)
    bpy.context.collection.objects.link(stem)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = stem
    stem.select_set(True)
    bpy.ops.object.convert(target="MESH")
    capsule = rounded_box("Lectern microphone capsule", (stem_x-.058, stem_y+.108, 1.366),
                          (.027, .067, .027), hardware, .012)
    capsule.rotation_euler.z = .48


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


def rounded_box(name, point, dimensions, material, bevel):
    item = box(name, point, dimensions, material)
    if bevel:
        edges = item.modifiers.new("Rounded edges", "BEVEL")
        edges.width, edges.segments = bevel, 2
        item.modifiers.new("Face-weighted normals", "WEIGHTED_NORMAL")
    return item


def check_sightlines(scene, markers):
    graph = bpy.context.evaluated_depsgraph_get()
    def blocker(origin, endpoint):
        ray = Vector(endpoint) - Vector(origin)
        hit, _, _, _, obj, _ = scene.ray_cast(graph, origin, ray.normalized(), distance=ray.length - .024)
        return obj.name if hit else None

    count = 0
    screen = Vector(MARKERS["NaraScreen"])
    for name, point in markers.items():
        if not name.startswith("NaraAudienceSeat_"):
            continue
        for height in (1.14, 1.24):
            eye = Vector(point) + Vector((0, 0, height))
            for x, z in ((0, 0), (-1, -1), (0, -1), (1, -1), (-1, 1), (1, 1)):
                target = screen + Vector((x * .47 * SCREEN_WIDTH, 0, z * .47 * SCREEN_HEIGHT))
                blocked = blocker(eye, target)
                if blocked:
                    raise RuntimeError(f"Screen obscured by {blocked} from {name} at eye height {height} m.")
                count += 1
        blocked = blocker(Vector(PRESENTER), Vector(point) + Vector((0, 0, 1.19)))
        if blocked:
            raise RuntimeError(f"Presenter's view of {name} is blocked by {blocked}.")
    blocked = blocker(Vector(PRESENTER), Vector(MONITOR_CENTER))
    if blocked:
        raise RuntimeError(f"Presenter's monitor is blocked by {blocked}.")
    print(f"NARA_CLASSROOM_SIGHTLINES passed={count} room_to_presenter=24 monitor=clear")


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
    camera_data.lens = 20
    aim(camera, MARKERS["NaraSightline"])
    scene.camera = camera
    overview_data = bpy.data.cameras.new("Sudut ruangan")
    overview = bpy.data.objects.new("Sudut ruangan", overview_data)
    bpy.context.collection.objects.link(overview)
    overview.location = (3.43, -3.73, 2.28)
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
    options.fbx.parent.mkdir(parents=True, exist_ok=True)
    fit_lectern(options.fbx.resolve().parent / "LecternOak.png")
    frame = color("Nara screen frame", (.072, .093, .102, 1))
    projector = color("Nara projector body", (.76, .78, .76, 1))
    x, y, z = MARKERS["NaraScreen"]
    box("Pull-down projection screen", (x, y+.032, z), (SCREEN_WIDTH+.10, .055, SCREEN_HEIGHT+.09), frame)
    box("Projection screen cassette", (x, y+.029, z+SCREEN_HEIGHT/2+.059), (SCREEN_WIDTH+.20, .116, .082), projector)
    for name, point in markers.items():
        item = bpy.data.objects.new(name, None)
        item.empty_display_type = "PLAIN_AXES"
        item.empty_display_size = .18
        item.location = point
        bpy.context.collection.objects.link(item)
    bpy.context.view_layer.update()
    check_sightlines(scene, markers)
    mesh_faces = sum(len(item.data.polygons) for item in scene.objects if item.type == "MESH")
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
    for image in bpy.data.images:
        if image.source == "FILE":
            if not image.packed_files:
                image.pack()
            image.filepath = "//" + Path(image.filepath.replace("\\", "/")).name
            for packed in image.packed_files:
                packed.filepath = image.filepath
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(options.blend.resolve()), compress=True, relative_remap=False)
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
