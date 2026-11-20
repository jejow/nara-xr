"""Model the NARA rehearsal room from the team's IF 101 photo and video references."""

import argparse
import math
from pathlib import Path
import struct
import sys
import zlib

import bpy
from mathutils import Matrix, Vector
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from nara_prepare_classroom import aim, color, light, mesh_bounds, preview_slide


ROOT = Path(__file__).resolve().parents[1]
ENVIRONMENT = ROOT / "NARA/Assets/FinalProject/Environment"
TILE_SIZE = .30
HALF_WIDTH = 4.45
HALF_DEPTH = 3.50
CEILING = 3.42
BEAMS = (-2.73, -1.18, .37, 1.92)
ROWS = (.72, -.46, -1.64, -2.82)
COLUMNS = (-3.13, -2.05, -.97, 1.10, 2.18, 3.26)
WINDOWS = ((-1.72, 2.36), (1.30, 2.52))
ENTRY = 1.64
ENTRY_WIDTH = 2.58
PRESENTER = (.12, 3.26, 1.64)
PROJECTION_WIDTH = 2.42
PROJECTION_HEIGHT = PROJECTION_WIDTH * 9 / 16
PROJECTION_CENTER = (-.176, HALF_DEPTH-.120, 2.057)
SEAT_HEIGHT = .461


def collection(name):
    item = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(item)
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection.children[name]


def tint(name, rgb, roughness=.55):
    values = [int(rgb[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    linear = tuple(level / 12.92 if level < .04045 else ((level + .055) / 1.055) ** 2.4 for level in values)
    return color(name, (*linear, 1), roughness)


def write_png(path, pixels):
    def chunk(kind, contents):
        return (struct.pack('!I', len(contents)) + kind + contents
                + struct.pack('!I', zlib.crc32(kind + contents)))

    rgb = np.clip(pixels, 0, 255).astype(np.uint8)
    height, width, _ = rgb.shape
    scanlines = b''.join(b'\x00' + row.tobytes() for row in rgb)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('!2I5B', width, height, 8, 2, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(scanlines, 6)) + chunk(b'IEND', b''))


def room_textures(directory):
    """Recreate the ceramic, varnished timber and dotted curtains without photo pixels."""
    coordinates = (np.arange(768, dtype=np.float32) + .5) / 768
    u, v = np.meshgrid(coordinates, coordinates)
    rng = np.random.default_rng(101)
    grain = rng.normal(0, 1, u.shape)
    tu, tv = (u * 4) % 1, (v * 4) % 1
    seam = np.minimum(np.minimum(tu, 1 - tu), np.minimum(tv, 1 - tv))
    tile = np.clip((seam - .006) / .009, 0, 1)[..., None]
    variations = rng.normal(0, 2.1, (4, 4))[np.int32(v * 4), np.int32(u * 4)]
    mottling = 1.45 * np.sin(math.tau * (5 * u + 4 * v)) + 1.2 * np.cos(math.tau * (9 * u - 7 * v))
    ceramic = np.array((205, 194, 164)) + (variations + mottling + grain * 1.20)[..., None]
    grout = np.array((67, 66, 56)) + (grain * 2.8)[..., None]
    write_png(directory / "IF101Floor.png", ceramic * tile + grout * (1 - tile))

    bend = .105 * np.sin(math.tau * v) + .029 * np.cos(math.tau * 3 * v)
    flowing = u + bend * (.13 + .09 * np.sin(math.tau * 4 * u))
    wavy = (7 * np.sin(math.tau * (7 * flowing + v)) + 5 * np.sin(math.tau * 27 * flowing)
            + 2 * np.cos(math.tau * 73 * flowing) + 1.4 * np.sin(math.tau * 119 * flowing) + grain * 1.55)
    wear = np.maximum(0, .19 * np.sin(2 * math.tau*u) + .13 * np.cos(3 * math.tau*v)
                      + .075 * mottling + .095 * grain - .23)
    timber = np.array((111, 71, 39)) + wavy[..., None] * np.array((1.04, .92, .71)) + 76 * wear[..., None]
    write_png(directory / "IF101Wood.png", timber)

    blinds = np.zeros(u.shape)
    for j in range(8):
        for i in range(5):
            x = (i + .45 + .29 * (j % 2)) / 5
            y = (j + .4) / 8
            distance = np.hypot((u - x) * .80, v - y)
            radius = .024 if (i+j) % 3 else .041
            fill = np.clip((radius - distance) * 570, 0, 1)
            if (i + 2*j) % 4 == 0:
                fill = np.clip((.0029 - np.abs(radius - distance)) * 340, 0, 1) * .42
            blinds = np.maximum(blinds, fill * (.56 + .12 * ((i+j) % 3)))
    weave = .9 * np.sin(math.tau * 171 * u) * np.sin(math.tau * 173 * v) + grain * .67
    louver = -24 * np.exp(-(((v * 10.3) % 1 - .5) / .065) ** 2)
    cloth = np.array((219, 223, 225)) * (1 - blinds[..., None]) + np.array((173, 146, 76)) * blinds[..., None]
    write_png(directory / "IF101Curtain.png", cloth + (weave + louver)[..., None])

    wall_wear = 19 * v**17 * (.51 + .49*np.sin(math.tau * 6 * u + .3*np.cos(33*v))**2)
    wall_wear += np.maximum(0, mottling + .68*grain - .2) * 1.6
    write_png(directory / "IF101Paint.png", np.array((209, 211, 192)) - wall_wear[..., None])
    chalk = 2.2 * np.sin(math.tau * (3*u + 7*v)) + .48 * grain
    scratches = np.maximum(0, grain - 2.25) * 8.5 * np.sin(math.tau * u)**2
    write_png(directory / "IF101Board.png", np.array((200, 206, 207)) + (chalk - scratches)[..., None])


def image_surface(material, path, emission=0):
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = nodes.get("Principled BSDF")
    image = nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(path.resolve()))
    image.image.pack()
    image.image.filepath = "//" + path.name
    for packed in image.image.packed_files:
        packed.filepath = image.image.filepath
    links.new(image.outputs["Color"], shader.inputs["Base Color"])
    if emission:
        links.new(image.outputs["Color"], shader.inputs["Emission Color"])
        shader.inputs["Emission Strength"].default_value = emission


def materials(directory):
    definitions = {
        "paint": ("wall paint", "D1D3C0", .68),
        "ceiling": ("ceiling paint", "DADCCD", .83),
        "tile": ("ceramic tile", "CDC2A4", .26),
        "wood": ("varnished wood", "6F4727", .31),
        "curtain": ("patterned curtain", "E4E2D7", .83),
        "metal": ("aluminum", "A7A79D", .43),
        "rail": ("cable tray steel", "484E4B", .58),
        "white": ("whiteboard", "C8CECF", .39),
        "orange": ("entry orange", "AD4A28", .45),
        "glass": ("frosted glass", "B5C3CF", .32),
        "fanlight": ("high window glass", "C9D7CF", .39),
        "plastic": ("fixture housing", "D0CFC1", .67),
        "tube": ("fluorescent tubes", "FAF9F1", .42),
        "black": ("equipment black", "24282A", .61),
        "clock": ("clock red", "E33932", .48),
        "cabinet": ("storage cabinet", "636D64", .63),
        "felt": ("olive platform", "323D22", .94),
        "roof": ("courtyard clay roof", "9B5039", .76),
        "walk": ("courtyard paving", "AD9280", .80),
        "foliage": ("courtyard foliage", "7A8C51", .91),
    }
    result = {key: tint("IF101 " + name, rgb, roughness) for key, (name, rgb, roughness) in definitions.items()}
    for key, source, emission in (('tile', 'Floor', 0), ('wood', 'Wood', 0), ('curtain', 'Curtain', .28),
                                  ('paint', 'Paint', 0), ('white', 'Board', 0)):
        image_surface(result[key], directory / f"IF101{source}.png", emission)
    for key, power in (('tube', 1.8), ('fanlight', .14), ('clock', .72)):
        mat = result[key]
        emitter = mat.node_tree.nodes.get("Principled BSDF")
        emitter.inputs["Emission Color"].default_value = mat.diffuse_color
        emitter.inputs["Emission Strength"].default_value = power
    return result


def surface(name, vertices, faces, material, uv=None):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.materials.append(material)
    if uv is not None:
        uv_layer = mesh.uv_layers.new()
        for polygon in mesh.polygons:
            for index in polygon.loop_indices:
                uv_layer.data[index].uv = uv[mesh.loops[index].vertex_index]
    item = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(item)
    return item


def box(name, point, size, material):
    x, y, z = (extent / 2 for extent in size)
    vertices = [(-x, -y, -z), (x, -y, -z), (x, y, -z), (-x, y, -z),
                (-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)]
    faces = [(3, 2, 1, 0), (4, 5, 6, 7), (0, 1, 5, 4),
             (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    item = surface(name, vertices, faces, material)
    item.location = point
    return item


def rounded_box(name, point, size, material, amount, segments=1):
    item = box(name, point, size, material)
    bevel = item.modifiers.new("Rounded edges", 'BEVEL')
    bevel.width, bevel.segments = amount, segments
    return item


def wall_block(name, point, size, paint):
    wall = box(name, point, size, paint)
    uv = wall.data.uv_layers.new().data
    for face in wall.data.polygons:
        horizontal = 1 if abs(face.normal.x) > .5 else 0
        for index in face.loop_indices:
            vertex = wall.data.vertices[wall.data.loops[index].vertex_index].co
            uv[index].uv = ((point[horizontal] + vertex[horizontal]) / 3.75,
                            (point[2] + vertex.z) / CEILING)
    return wall


def wall_span(side, lo, hi, bottom, top, material):
    if hi - lo > .001 and top - bottom > .001:
        wall_block(f"IF101 wall {side}", (side * (HALF_WIDTH + .058), (lo + hi) / 2, (bottom + top) / 2),
                   (.116, hi - lo, top - bottom), material)


def side_wall(side, openings, paint):
    cursor = -HALF_DEPTH
    for center, width, bottom, top in sorted(openings):
        lo, hi = center - width / 2, center + width / 2
        wall_span(side, cursor, lo, 0, CEILING, paint)
        wall_span(side, lo, hi, 0, bottom, paint)
        wall_span(side, lo, hi, top, CEILING, paint)
        cursor = hi
    wall_span(side, cursor, HALF_DEPTH, 0, CEILING, paint)


def room_shell(mat):
    collection("01 Ruang dan plafon")
    x, y = HALF_WIDTH, HALF_DEPTH
    u, v = 2*x / (4*TILE_SIZE), 2*y / (4*TILE_SIZE)
    surface("IF101 ceramic floor", [(-x, -y, 0), (x, -y, 0), (x, y, 0), (-x, y, 0)], [(0, 1, 2, 3)],
            mat['tile'], [(.043, .065), (u + .043, .065), (u + .043, v + .065), (.043, v + .065)])
    box("IF101 ceiling", (0, 0, CEILING + .075), (2*x + .232, 2*y + .232, .15), mat['ceiling'])
    for side in (-1, 1):
        wall_block("IF101 wall end", (0, side * (y + .058), CEILING/2),
                   (2*x + .232, .116, CEILING), mat['paint'])
        box("IF101 ceiling edge beam", (0, side * (y - .092), CEILING-.13), (2*x, .184, .26), mat['ceiling'])
        for corner in (-1, 1):
            wall_block("IF101 wall corner pilaster", (corner*(x-.106), side*(y-.127), CEILING/2),
                       (.212, .254, CEILING), mat['paint'])
    side_wall(1, [(center, width, .89, 2.66) for center, width in WINDOWS], mat['paint'])
    side_wall(-1, [(-1.455, 3.39, 1.94, 2.62), (ENTRY, ENTRY_WIDTH, 0, 2.63)], mat['paint'])
    box("Conduit across teaching wall", (0, y-.031, 3.126), (2*x-.41, .025, .023), mat['metal'])
    for x_clip in np.linspace(-3.95, 3.95, 13):
        box("Wall conduit clip", (x_clip, y-.031, 3.126), (.017, .035, .03), mat['plastic'])
    for y_beam in BEAMS:
        box("IF101 ceiling crossbeam", (0, y_beam, CEILING-.13), (2*x, .30, .26), mat['ceiling'])
        for x_lamp in (-2.35, 2.33):
            box("Galvanized fluorescent reflector", (x_lamp, y_beam, 3.14), (1.36, .192, .044), mat['metal'])
            for offset in (-.086, .086):
                reflector = box("Angled reflector flange", (x_lamp, y_beam+offset, 3.111),
                                (1.36, .041, .027), mat['metal'])
                reflector.rotation_euler.x = math.copysign(.47, offset)
            rounded_box("Fluorescent T8 tube", (x_lamp, y_beam, 3.085), (1.22, .027, .027), mat['tube'], .012, 3)
            for end in (-.622, .622):
                box("Fluorescent socket", (x_lamp + end, y_beam, 3.103), (.054, .048, .056), mat['plastic'])
    for side in (-1, 1):
        tray_x = side * (x - .367)
        for offset in (-.119, .119):
            box("Galvanized cable tray lip", (tray_x + offset, 0, 3.112), (.026, 6.85, .055), mat['rail'])
        for offset in (-.086, -.029, .029, .086):
            box("Galvanized cable tray rib", (tray_x + offset, 0, 3.085), (.017, 6.85, .015), mat['rail'])
        for i in range(62):
            box("Cable tray perforation", (tray_x, -3.367 + .11*i, 3.085), (.237, .028, .016), mat['rail'])


def window(x, center, width, lower, upper, mat):
    middle, height = (lower + upper)/2, upper - lower
    inner = -.043 if x > 0 else .043
    box("Milky window glazing", (x, center, middle), (.036, width, height), mat['fanlight'])
    for z in (lower, middle, upper):
        box("Aluminum window sill", (x + inner, center, z), (.093, width + .066, .041), mat['metal'])
    for j in range(5):
        y = center + width * (j / 4 - .5)
        box("Aluminum window mullion", (x + inner, y, middle), (.084, .046, height), mat['metal'])
    return middle


def curtain(center, width, material):
    panel = width/3
    for i in range(3):
        y = center - width/2 + panel*i
        x = HALF_WIDTH - .134 - .006*(i % 2)
        from_y, to_y = y+.013, y+panel-.012
        from_z, to_z = .884 + .023*(i % 2), 2.62
        mid_y = (from_y+to_y) / 2
        vertices = [(x, from_y, from_z), (x-.009, mid_y, from_z-.011), (x, to_y, from_z),
                    (x, from_y, to_z), (x-.005, mid_y, to_z), (x, to_y, to_z)]
        u = i/3
        surface("IF101 roller blind", vertices, [(0, 3, 4, 1), (1, 4, 5, 2)], material,
                [(u, 0), (u+1/6, 0), (u+1/3, 0), (u, 1), (u+1/6, 1), (u+1/3, 1)])


def openings(mat):
    collection("02 Pintu dan jendela")
    wx = HALF_WIDTH
    for center, width in WINDOWS:
        window(wx, center, width, .89, 2.66, mat)
        curtain(center, width + .025, mat['curtain'])
        box("Window blind mounting rail", (wx-.147, center, 2.647), (.083, width+.095, .036), mat['metal'])
        for index in (-1, 0, 1):
            box("Blind bottom rail", (wx-.145, center + index*width/3, .882 + .023*(index == 0)),
                (.033, width/3-.019, .022), mat['plastic'])
        rounded_box("Split air conditioner", (wx-.118, center, 2.896), (.242, 1.006, .234), mat['plastic'], .033, 3)
        box("Air conditioner dark outlet", (wx-.243, center, 2.837), (.014, .861, .044), mat['black'])
        for z in (2.828, 2.844):
            box("Air conditioner vane", (wx-.253, center, z), (.017, .846, .005), mat['metal'])

    middle = window(-wx, -1.455, 3.39, 1.94, 2.62, mat)
    for j in range(28):
        box("High window security grille", (-wx+.105, -3.127 + .124*j, middle), (.024, .016, .648), mat['metal'])
    for z in (2.02, 2.19, 2.36, 2.54):
        box("High window grille rail", (-wx+.117, -1.455, z), (.022, 3.39, .019), mat['metal'])

    portal_x = -wx-.182
    for lo, hi in ((.643, .843), (2.181, 2.550)):
        box("Orange portal jamb", (portal_x, (lo+hi)/2, .967), (.185, hi-lo, 1.934), mat['orange'])
    box("Orange portal lintel", (portal_x, 1.5965, 2.06), (.185, 1.907, .252), mat['orange'])
    for y in (ENTRY-ENTRY_WIDTH/2+.019, ENTRY+ENTRY_WIDTH/2-.019):
        box("Outer aluminum door jamb", (-wx-.054, y, 1.314), (.073, .037, 2.628), mat['metal'])
    for z in (2.191, 2.611):
        box("Entry transom rail", (-wx+.014, ENTRY, z), (.077, ENTRY_WIDTH, .032), mat['metal'])
    for i in range(6):
        louver = box("Entry transom angled louver", (-wx-.019, ENTRY, 2.227+.059*i),
                     (.056, ENTRY_WIDTH-.075, .018), mat['metal'])
        louver.rotation_euler.y = -.39
    box("Entry transom center strut", (-wx+.018, ENTRY, 2.407), (.074, .038, .403), mat['metal'])

    for side in (-1, 1):
        centerline = 1.512
        leaf = centerline + side*.355
        glass_x = -wx-.108 if side < 0 else -wx-.166
        box("Satin window film on door", (glass_x, leaf, .989), (.013, .622, 1.071), mat['glass'])
        for z in (.398, .432, 1.560, 1.603):
            box("Narrow frosted door stripe", (glass_x, leaf, z), (.013, .622, .017), mat['glass'])
        for z in (.059, 1.906):
            box("Glass door patch fitting", (glass_x, centerline + side*.601, z), (.052, .073, .037), mat['metal'])
        rounded_box("Stainless door pull", (glass_x+.070, centerline+side*.102, 1.030),
                    (.030, .027, .538), mat['metal'], .010, 2)
        for z in (.790, 1.269):
            box("Stainless pull standoff", (glass_x+.042, centerline+side*.102, z),
                (.066, .018, .021), mat['metal'])
    box("Wall-mounted brown equipment panel", (-wx+.12, ENTRY-1.05, 1.391),
        (.078, .510, .567), mat['wood'])
    box("Dark entrance mat", (-wx+.31, ENTRY, .005), (.62, 1.20, .010), mat['rail'])
    courtyard(mat)


def courtyard(mat):
    collection("06 Cuplikan selasar dari pintu")
    box("IF101 exterior terracotta paving", (-5.30, ENTRY, -.048), (1.7, 4.8, .094), mat['walk'])
    box("IF101 exterior cream wall", (-6.32, 2.10, 1.31), (.088, 4.72, 2.61), mat['fanlight'])
    for i in range(8):
        roof = box("IF101 exterior terracotta canopy", (-6.26 + .135*i, 2.35, 2.24-.063*i),
                   (.29, 3.32, .059), mat['roof'])
        roof.rotation_euler.y = .433
    for i, (y, height) in enumerate(((.31, 1.88), (.62, 1.58), (.84, 1.72), (1.22, 1.33), (3.05, 1.82))):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=.31,
                                              location=(-5.72-.12*(i % 2), y, height))
        item = bpy.context.object
        item.name = "IF101 exterior foliage"
        item.scale = (.93, 1.09, 1.63)
        item.data.materials.append(mat['foliage'])


def clock_display(mat):
    x, y, z = 3.61, HALF_DEPTH-.046, 2.822
    rounded_box("Digital classroom clock", (x, y, z), (.489, .052, .173), mat['black'], .010, 2)
    segment_positions = ((0, .050), (.025, .026), (.025, -.026), (0, -.051),
                         (-.025, -.026), (-.025, .026), (0, 0))
    digits = {'1': (1, 2), '3': (0, 1, 2, 3, 6), '6': (0, 2, 3, 4, 5, 6)}
    dx = -.197
    for char in "16:36:13":
        if char == ':':
            for dot in (.019, -.019):
                box("LED clock colon", (x+dx-.004, y-.028, z+.012+dot), (.011, .006, .011), mat['clock'])
            dx += .027
            continue
        for segment in digits[char]:
            offset, height = segment_positions[segment]
            size = (.041, .006, .009) if segment in (0, 3, 6) else (.009, .006, .039)
            box("Seven-segment clock LED", (x+dx+offset, y-.029, z+.012+height), size, mat['clock'])
        dx += .067
    for i in range(12):
        box("Clock date indicator", (x-.192+.018*i, y-.03, z-.060), (.008, .005, .005), mat['clock'])


def fitted_platform(mat):
    outline = [(2.51, 2.43), (3.82, 1.19), (4.41, 1.60), (4.41, 3.486), (2.51, 3.486)]
    count = len(outline)
    verts = [(x, y, z) for z in (.001, .142) for x, y in outline]
    faces = [tuple(range(count, 2*count))]
    faces.extend((i, (i + 1) % count, count + (i + 1) % count, count + i) for i in range(count))
    surface("Green teacher platform with clipped corners", verts, faces, mat['felt'], [(0, 0)] * len(verts))
    box("Supply bin under board", (2.688, 3.243, .272), (.281, .337, .257), mat['cabinet'])
    box("Supply bin light lid", (2.688, 3.243, .412), (.303, .353, .022), mat['plastic'])


def teaching_wall(mat):
    collection("03 Area pengajar dan proyeksi")
    x, y, z = PROJECTION_CENTER
    box("Projection screen backing", (x, y+.024, z), (PROJECTION_WIDTH+.040, .022, PROJECTION_HEIGHT+.040), mat['white'])
    box("Projection screen cassette", (x, y+.032, z+PROJECTION_HEIGHT/2+.049), (PROJECTION_WIDTH+.088, .070, .060), mat['plastic'])
    box("Projection screen bottom rail", (x, y+.016, z-PROJECTION_HEIGHT/2-.013), (PROJECTION_WIDTH+.057, .037, .025), mat['black'])
    for x, z in ((-2.655, 1.39), (2.295, 1.37)):
        width, height = 2.40, 1.20
        y = HALF_DEPTH-.030
        rounded_box("Whiteboard aluminum perimeter", (x, y+.007, z), (width+.022, .041, height+.022),
                    mat['metal'], .005)
        surface("Dry wipe board face",
                [(x-width/2, y-.016, z-height/2), (x+width/2, y-.016, z-height/2),
                 (x+width/2, y-.016, z+height/2), (x-width/2, y-.016, z+height/2)],
                [(0, 1, 2, 3)], mat['white'], [(0, 0), (1, 0), (1, 1), (0, 1)])
        box("Narrow whiteboard pen rail", (x, y-.033, z-height/2), (width-.023, .050, .018), mat['metal'])
        box("Whiteboard eraser", (x + width*.17, y-.043, z-height/2+.021), (.091, .040, .023), mat['rail'])
    clock_display(mat)
    rounded_box("Black lecture speaker", (-3.973, 3.245, .334), (.363, .294, .668), mat['black'], .034, 3)
    box("Speaker horn recess", (-3.973, 3.093, .56), (.18, .027, .111), mat['rail'])
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=.135, depth=.015,
                                       location=(-3.973, 3.092, .239), rotation=(math.pi/2, 0, 0))
    bpy.context.object.name = "Speaker woofer grille"
    bpy.context.object.data.materials.append(mat['rail'])
    fitted_platform(mat)
    rounded_box("Ceiling projector", (-.25, .79, 2.777), (.366, .286, .111), mat['plastic'], .015, 2)
    box("Projector ceiling plate", (-.25, .79, 3.159), (.118, .129, .033), mat['metal'])
    box("Projector drop mount", (-.25, .79, 2.991), (.035, .041, .317), mat['plastic'])
    for i in range(12):
        box("Projector side grille slot", (-.434, .685+.018*i, 2.779), (.004, .009, .050), mat['rail'])
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=.033, depth=.025, rotation=(math.pi/2, 0, 0),
                                       location=(-.338, .936, 2.78))
    bpy.context.object.name = "Projector lens"
    bpy.context.object.data.materials.append(mat['black'])


def wooden_part(label, point, size, material, radius=.004, tilt=0):
    item = rounded_box(label, point, size, material, radius)
    item.rotation_euler.x = tilt
    along = max(range(3), key=lambda axis: size[axis])
    uv = item.data.uv_layers.new().data
    for face in item.data.polygons:
        normal = max(range(3), key=lambda axis: abs(face.normal[axis]))
        span = [axis for axis in range(3) if axis != normal]
        length = along if along in span else max(span, key=lambda axis: size[axis])
        cross = next(axis for axis in span if axis != length)
        shift = (sum(ord(letter) for letter in label) % 113) / 113
        for index in face.loop_indices:
            vertex = item.data.vertices[item.data.loops[index].vertex_index].co
            uv[index].uv = (shift + vertex[cross] * 2.5, shift + vertex[length] * .78)
    bpy.context.view_layer.objects.active = item
    bpy.ops.object.modifier_apply(modifier=item.modifiers[0].name)
    return item


def chair_template(wood):
    def part(label, point, size, radius=.004, tilt=0):
        return wooden_part(label, point, size, wood, radius, tilt)

    parts = []
    for side in (-1, 1):
        x = side * .239
        parts.extend([
            part(f"Straight front leg {side}", (x, .231, .227), (.043, .052, .454), .0025),
            part(f"Back upright {side}", (x, -.242, .448), (.049, .050, .889), .0025, .052),
            part(f"Deep seat side rail {side}", (x, 0, .405), (.042, .485, .073), .003),
            part(f"Low book shelf edge {side}", (x, -.003, .116), (.035, .455, .054), .0025),
        ])
    for y in (-.208, .212):
        parts.append(part(f"Shelf cross rail {y}", (0, y, .108), (.476, .039, .055)))
    parts.append(part("Deep front seat rail", (0, .228, .407), (.514, .047, .081), .003))
    parts.append(part("Rear seat rail", (0, -.223, .412), (.495, .040, .064)))
    for i in range(5):
        x = (i - 2) * .097
        parts.extend([
            part(f"Seat longitudinal slat {i}", (x, -.001, SEAT_HEIGHT-.012), (.082, .478, .024), .003),
            part(f"Book shelf slat {i}", (x, -.011, .143), (.067, .451, .019), .002),
        ])
    for i in range(4):
        x = (i - 1.5)*.124
        parts.append(part(f"Four broad back slats {i}", (x, -.263, .766),
                          (.113, .022, .246), .0035, .052))
    parts.append(part("Horizontal back support upper", (0, -.289, .835), (.496, .026, .041), .003))
    parts.append(part("Horizontal back support lower", (0, -.282, .659), (.499, .030, .041), .003))
    bpy.ops.object.select_all(action='DESELECT')
    for item in parts:
        item.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    chair = bpy.context.object
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return chair


def audience_rows(mat):
    collection("04 Kursi kuliah kayu")
    template = chair_template(mat['wood'])
    group = bpy.context.collection
    chairs = []
    sockets = {}
    for row, y in enumerate(ROWS, 1):
        for column, x in enumerate(COLUMNS, 1):
            label = f"R{row:02}_C{column:02}"
            item = template.copy()
            item.data = template.data.copy()
            group.objects.link(item)
            item.name = "IF101 wood chair " + label
            item.location = (x, y, 0)
            sockets["NaraAudienceSeat_" + label] = (x, y, SEAT_HEIGHT-.481)
            uv = item.data.uv_layers.active.data
            values = np.empty(len(uv)*2, dtype=np.float32)
            uv.foreach_get('uv', values)
            values[::2] += .11 * (row + 3*column) % 1
            uv.foreach_set('uv', values)
            chairs.append(item)
    bpy.data.objects.remove(template, do_unlink=True)
    side = -4.218
    box("Steel storage cabinet", (side-.023, -2.850, .952), (.382, .882, 1.899), mat['cabinet'])
    for y in (-3.065, -2.635):
        box("Cabinet smoked glass door", (side+.185, y, 1.285), (.030, .385, 1.10), mat['rail'])
        box("Cabinet lower steel panel", (side+.188, y, .353), (.037, .391, .64), mat['cabinet'])
        for z in (1.035, 1.335, 1.636):
            box("Visible cabinet shelf", (side+.202, y, z), (.012, .368, .016), mat['cabinet'])
            box("Pale supplies inside cabinet", (side+.205, y+.052, z+.089), (.013, .092, .133), mat['plastic'])
        box("Cabinet door handle", (side+.211, y+.105, 1.064), (.044, .025, .144), mat['metal'])
    for y in (-2.98, -2.80):
        bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=.041, depth=.495,
                                           location=(side-.015, y, 1.943), rotation=(0, math.pi/2, 0))
        bpy.context.object.name = "Paper plan roll above cabinet"
        bpy.context.object.data.materials.append(mat['plastic'])
    return chairs, sockets


def room_markers(sockets):
    screen = PROJECTION_CENTER
    markers = {
        "NaraPresenter": PRESENTER,
        "NaraSightline": (.07, ROWS[0], 1.24),
        "NaraScreen": screen,
        "NaraScreenEdge": (screen[0] + PROJECTION_WIDTH/2, screen[1], screen[2]),
        **sockets,
    }
    for name, point in markers.items():
        item = bpy.data.objects.new(name, None)
        item.empty_display_type, item.empty_display_size = 'PLAIN_AXES', .15
        item.location = point
        bpy.context.scene.collection.objects.link(item)
    return markers


def audit_room(scene, chairs, markers):
    bpy.context.view_layer.update()
    bounds = [mesh_bounds([chair]) for chair in chairs]
    for i, (low, high) in enumerate(bounds):
        for j, (other_low, other_high) in enumerate(bounds[i + 1:], i + 1):
            if all(min(high[axis], other_high[axis]) > max(low[axis], other_low[axis]) for axis in (0, 1)):
                raise RuntimeError(f"{chairs[i].name} overlaps {chairs[j].name}.")
    aisle = min(low.x for low, _ in bounds if low.x > 0) - max(high.x for _, high in bounds if high.x < 0)
    if aisle < .76:
        raise RuntimeError(f"Audience center aisle is too narrow ({aisle:.3f} m).")
    graph = bpy.context.evaluated_depsgraph_get()
    screen = Vector(markers['NaraScreen'])
    for marker in (name for name in markers if name.startswith("NaraAudienceSeat_")):
        for height in (1.14, 1.24):
            eye = Vector(markers[marker]) + Vector((0, 0, height))
            for dx, dz in ((0, 0), (-1, -1), (0, -1), (1, -1), (-1, 1), (1, 1)):
                ray = screen + Vector((dx * .47 * PROJECTION_WIDTH, 0, dz * .47 * PROJECTION_HEIGHT)) - eye
                hit, _, _, _, obj, _ = scene.ray_cast(graph, eye, ray.normalized(), distance=ray.length - .025)
                if hit:
                    raise RuntimeError(f"{obj.name} blocks the screen from {marker} at {height} m.")
    count = sum(len(face.vertices) - 2 for obj in scene.objects if obj.type == 'MESH' for face in obj.data.polygons)
    print(f"IF110_AUDIT wooden_chairs={len(chairs)} desks=0 central_aisle={aisle:.3f} m "
          f"clear_screen_rays={12 * len(chairs)} base_triangles={count}", flush=True)


def cameras_and_lighting(scene):
    collection("05 Kamera dan pencahayaan render")
    for name, position, focus, lens in (
        ("IF101 dari belakang", (.15, -3.26, 1.95), (-.02, 2.67, 1.44), 18.7),
        ("Sudut pembicara", PRESENTER, (.07, ROWS[0], 1.24), 22.5),
    ):
        data = bpy.data.cameras.new(name)
        data.lens = lens
        item = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(item)
        item.location = position
        aim(item, focus)
    # Reproject the floor-grid pose from the 28 mm-equivalent reference photos.
    for view, x, distance, z, direction, up in (
        ("Foto 6660 - dinding depan", .168, 4.703, 1.595, (-.10089, .99385, -.04553), (-.02235, .04349, .9988)),
        ("Foto 6661 - sudut pintu", -.491, 4.709, 1.532, (-.57132, .81689, -.07926), (-.05782, .05627, .99674)),
    ):
        data = bpy.data.cameras.new(view)
        data.lens = 29.0625
        item = bpy.data.objects.new(view, data)
        bpy.context.collection.objects.link(item)
        item.location = (x, HALF_DEPTH-distance, z)
        forward, vertical = Vector(direction), Vector(up)
        right = forward.cross(vertical).normalized()
        item.rotation_euler = Matrix((right, vertical, -forward)).transposed().to_euler()
    scene.camera = bpy.data.objects["IF101 dari belakang"]
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == 'VIEW_3D':
                space = area.spaces.active
                space.region_3d.view_perspective = 'CAMERA'
                space.shading.color_type = 'MATERIAL'
                space.overlay.show_overlays = False
    for center, width in WINDOWS:
        light("Daylight across roller blinds", (4.13, center, 1.77), (-.6, center-.33, .69),
              230, width-.16, (.80, .90, 1.0), 1.53)
    light("Entry daylight", (-4.21, ENTRY, 1.61), (-.3, .35, .83), 122, 1.58, (.94, .99, 1), 1.82)
    light("Light outside entry", (-5.43, 1.3, 3.55), (-5.69, 1.9, .80), 165, 3.7, (1, .94, .87))
    for y in BEAMS:
        for x in (-2.35, 2.33):
            light("Fluorescent diffuse light", (x, y, 3.045), (x, y, .1), 31, 1.21, (.91, .95, 1), .12)
    world = bpy.data.worlds.new("IF101 ambient")
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .22
    scene.world = world
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 14
    scene.cycles.adaptive_threshold = .035
    scene.cycles.max_bounces = 6
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = 1280, 800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.exposure = .27


def main(options):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene['reference'] = 'Ruang IF 110 di NARA, diadaptasi dari model referensi IF 101. Empat baris kursi dan layar PPT.'
    directory = options.environment.resolve()
    directory.mkdir(parents=True, exist_ok=True)
    room_textures(directory)
    mat = materials(directory)
    room_shell(mat)
    openings(mat)
    teaching_wall(mat)
    chairs, sockets = audience_rows(mat)
    markers = room_markers(sockets)
    audit_room(scene, chairs, markers)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(directory / 'IF101Classroom.fbx'), use_selection=True,
                             object_types={'MESH', 'EMPTY'}, bake_anim=False, axis_forward='-Z', axis_up='Y',
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', path_mode='STRIP')

    preview_slide(options.slide.resolve(), "Slide PPT contoh", PROJECTION_CENTER,
                  (1, 0, 0), (0, 0, 1), PROJECTION_WIDTH, PROJECTION_HEIGHT)
    cameras_and_lighting(scene)
    for image in (image for image in bpy.data.images if image.source == 'FILE'):
        if not image.packed_files:
            image.pack()
        image.filepath = '//' + Path(image.filepath.replace('\\', '/')).name
        for packed in image.packed_files:
            packed.filepath = image.filepath
    options.blend.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = '//IF101-preview.png'
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(options.blend.resolve()), compress=True, relative_remap=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--environment', type=Path, default=ENVIRONMENT)
    parser.add_argument('--blend', type=Path, default=ROOT / 'assets/blender/NARA-IF-101.blend')
    parser.add_argument('--slide', type=Path,
                        default=ROOT / 'NARA/Assets/FinalProject/Resources/Nara/Slides/slide_1.png')
    main(parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []))
