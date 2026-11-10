"""Builds the game's own low-poly props as .glb files (flat colours, no textures).
Usage: python3 make_props.py <out_dir>"""
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix, Euler

OUT = sys.argv[1]
MATS = {}

def mat(name, rgb, rough=0.85, metal=0.0):
    if name in MATS: return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    MATS[name] = m
    return m

WOOD = lambda: mat("wood", (0.36, 0.23, 0.12))
WOOD_D = lambda: mat("wood_dark", (0.22, 0.14, 0.08))
WOOD_L = lambda: mat("wood_light", (0.62, 0.47, 0.28))
METAL = lambda: mat("metal", (0.42, 0.44, 0.46), 0.45, 0.8)
RUST = lambda: mat("rust", (0.38, 0.2, 0.12), 0.8, 0.3)
ROPE = lambda: mat("rope", (0.66, 0.55, 0.36))
CLOTH = lambda: mat("cloth", (0.5, 0.12, 0.1))
CONC = lambda: mat("concrete", (0.5, 0.5, 0.5))
CONC_D = lambda: mat("concrete_dark", (0.32, 0.32, 0.34))
DARK = lambda: mat("dark", (0.04, 0.04, 0.05))
PAINT = lambda: mat("paint_green", (0.2, 0.33, 0.28))
YELLOW = lambda: mat("yellow", (0.85, 0.65, 0.1))
GLOW = lambda: mat("lamp", (1.0, 0.85, 0.5))

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

def add(obj, m):
    obj.data.materials.append(m)
    for p in obj.data.polygons: p.use_smooth = False
    return obj

def box(size, loc=(0,0,0), rot=(0,0,0), m=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object; o.scale = size
    return add(o, m or WOOD())

def cyl(r, h, loc=(0,0,0), rot=(0,0,0), m=None, verts=8, r2=None):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r if r2 is None else r2, depth=h, location=loc, rotation=rot)
    return add(bpy.context.object, m or WOOD())

def cone(r, h, loc=(0,0,0), rot=(0,0,0), m=None, verts=6):
    return cyl(r, h, loc, rot, m, verts, 0.0)

def torus(R, r, loc=(0,0,0), rot=(0,0,0), m=None, seg=12, ring=5):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=seg, minor_segments=ring, location=loc, rotation=rot)
    return add(bpy.context.object, m or ROPE())

def between(p1, p2, r, m=None, verts=6, r2=None):
    """Cylinder from p1 to p2."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    o = cyl(r, d.length, (p1 + p2) / 2, m=m, verts=verts, r2=r2)
    o.rotation_euler = d.to_track_quat('Z', 'Y').to_euler()
    return o

def export(name):
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
    bpy.ops.object.join()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.object.name = name
    path = os.path.join(OUT, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', export_yup=True, export_apply=True)
    print("wrote", path, len(bpy.context.object.data.polygons), "faces")

# ---- spike trap: plank frame with sharpened stakes leaning outward ----
def spike_trap():
    reset()
    s = 0.75
    for sx, sy, rz in ((0, s, 0), (0, -s, 0), (s, 0, math.pi/2), (-s, 0, math.pi/2)):
        box((1.7, 0.14, 0.09), (sx, sy, 0.05), (0, 0, rz), WOOD_D())
    box((1.6, 0.12, 0.06), (0, 0, 0.04), (0, 0, math.pi/4), WOOD())
    box((1.6, 0.12, 0.06), (0, 0, 0.04), (0, 0, -math.pi/4), WOOD())
    import random; random.seed(4)
    n = 14
    for i in range(n):
        a = i / n * math.tau
        rr = 0.62 if i % 2 == 0 else 0.34
        base = Vector((math.cos(a) * rr, math.sin(a) * rr, 0.05))
        lean = Vector((math.cos(a), math.sin(a), 0)) * (0.28 if i % 2 == 0 else 0.1)
        h = random.uniform(0.5, 0.68)
        tip = base + lean + Vector((0, 0, h))
        between(base, base + (tip - base) * 0.72, 0.045, WOOD(), 6)
        between(base + (tip - base) * 0.72, tip, 0.045, WOOD_L(), 6, 0.004)
    cone(0.05, 0.55, (0, 0, 0.3), m=WOOD_L())
    export("rs-spike-trap")

# ---- snare: bent sapling, cord and a noose on the ground ----
def snare():
    reset()
    pts = [(-0.45, 0, 0), (-0.42, 0, 0.45), (-0.3, 0, 0.85), (-0.08, 0, 1.08), (0.2, 0, 1.12)]
    for i in range(len(pts) - 1):
        between(pts[i], pts[i+1], 0.035 - i * 0.004, WOOD(), 6, 0.031 - i * 0.004)
    between((0.2, 0, 1.12), (0.25, 0, 0.12), 0.008, ROPE(), 4)
    torus(0.22, 0.014, (0.25, 0, 0.06), (0.25, 0, 0), ROPE(), 14, 4)
    for x, y in ((0.52, 0.12), (0.0, -0.2)):
        between((x, y, -0.02), (x + 0.03, y, 0.2), 0.022, WOOD_D(), 5, 0.012)
    box((0.22, 0.03, 0.03), (0.52, 0.12, 0.17), (0, 0.2, 0.4), WOOD_D())
    for a in range(5):
        ang = a / 5 * math.tau
        box((0.16, 0.05, 0.012), (0.25 + math.cos(ang) * 0.3, math.sin(ang) * 0.3, 0.01), (0, 0, ang), mat("leaf", (0.25, 0.4, 0.16)))
    export("rs-snare")

# ---- club: nail bat. Grip at the origin, head up +Z (exported as +Y) ----
def club():
    reset()
    cyl(0.022, 0.26, (0, 0, 0.1), m=CLOTH(), verts=7)
    cyl(0.03, 0.03, (0, 0, -0.045), m=WOOD_D(), verts=7)
    cyl(0.024, 0.2, (0, 0, 0.33), m=WOOD(), verts=7, r2=0.034)
    cyl(0.034, 0.36, (0, 0, 0.61), m=WOOD(), verts=7, r2=0.052)
    cyl(0.052, 0.04, (0, 0, 0.81), m=WOOD_D(), verts=7, r2=0.04)
    for i in range(9):
        a = i * 2.4
        z = 0.5 + (i % 5) * 0.065
        d = Vector((math.cos(a), math.sin(a), 0))
        between(d * 0.035 + Vector((0, 0, z)), d * 0.1 + Vector((0, 0, z)), 0.006, METAL(), 4, 0.001)
    torus(0.045, 0.008, (0, 0, 0.56), m=RUST(), seg=8, ring=4)
    export("rs-club")

# ---- rope anchor: steel davit bolted to the roof with a coil of rope ----
def rope_anchor():
    reset()
    box((0.5, 0.5, 0.04), (0, 0, 0.02), m=RUST())
    for x in (-0.19, 0.19):
        for y in (-0.19, 0.19):
            cyl(0.025, 0.03, (x, y, 0.05), m=METAL(), verts=6)
    cyl(0.05, 1.05, (0, 0, 0.56), m=YELLOW(), verts=8)
    between((0, 0, 1.08), (0, 0.55, 1.28), 0.045, YELLOW(), 8)
    between((0, 0.06, 0.6), (0, 0.38, 1.18), 0.025, RUST(), 6)
    cyl(0.07, 0.05, (0, 0.55, 1.22), (0, math.pi/2, 0), METAL(), 8)
    between((0, 0.55, 1.2), (0, 0.6, 0.0), 0.014, ROPE(), 5)
    for i in range(5):
        torus(0.2 - i * 0.008, 0.028, (0.42, -0.1, 0.03 + i * 0.05), m=ROPE(), seg=12, ring=5)
    torus(0.06, 0.012, (0, 0.3, 0.95), (math.pi/2, 0, 0), METAL(), 8, 4)
    export("rs-rope-anchor")

# ---- stair hut: roof-access bulkhead with a door ----
def stair_hut():
    reset()
    w, d, h = 3.0, 3.6, 2.7
    box((w, d, h), (0, 0, h/2), m=CONC())
    box((w + 0.25, d + 0.25, 0.14), (0, 0, h + 0.07), m=CONC_D())
    box((w - 0.2, 0.9, 0.5), (0, -d/2 + 0.6, h + 0.3), (0.35, 0, 0), CONC_D())
    # door on the +Y face
    box((1.15, 0.08, 2.1), (0, d/2 + 0.02, 1.05), m=PAINT())
    box((1.3, 0.1, 0.12), (0, d/2 + 0.03, 2.16), m=CONC_D())
    for x in (-0.65, 0.65):
        box((0.1, 0.1, 2.2), (x, d/2 + 0.03, 1.1), m=CONC_D())
    box((0.06, 0.06, 0.2), (0.42, d/2 + 0.08, 1.05), m=METAL())
    box((0.5, 0.03, 0.3), (0, d/2 + 0.07, 1.6), m=mat("glass", (0.1, 0.14, 0.16), 0.2))
    box((0.3, 0.16, 0.12), (0, d/2 + 0.1, 2.38), m=GLOW())
    box((1.5, 0.5, 0.06), (0, d/2 + 0.3, 0.03), m=CONC_D())
    # vent and pipe
    box((0.7, 0.5, 0.5), (0.7, -0.6, h + 0.39), m=METAL())
    box((0.72, 0.52, 0.06), (0.7, -0.6, h + 0.66), m=RUST())
    cyl(0.09, 1.1, (-1.0, 0.9, h + 0.6), m=RUST(), verts=8)
    cone(0.16, 0.14, (-1.0, 0.9, h + 1.2), m=RUST(), verts=8)
    box((0.9, 0.04, 0.5), (w/2 + 0.02, 0, 1.5), (0, 0, math.pi/2), mat("panel", (0.75, 0.7, 0.55)))
    export("rs-stair-hut")

# ---- doorway: an open, dark entrance to stand against a building face (opening faces +Y) ----
def doorway():
    reset()
    w, h, t = 2.6, 3.0, 0.5
    for x in (-w/2 - 0.15, w/2 + 0.15):
        box((0.3, t, h), (x, 0, h/2), m=CONC_D())
    box((w + 0.6, t, 0.35), (0, 0, h + 0.17), m=CONC_D())
    box((w + 1.4, 1.5, 0.1), (0, 0.6, h + 0.5), (-0.12, 0, 0), RUST())
    for x in (-w/2 - 0.5, w/2 + 0.5):
        between((x, 1.25, h + 0.42), (x, 0.2, h - 0.3), 0.03, METAL(), 5)
    box((w + 0.9, 1.2, 0.14), (0, 0.55, 0.07), m=CONC())
    box((w + 0.5, 0.7, 0.14), (0, 0.3, 0.21), m=CONC())
    box((1.4, 0.06, 0.4), (0, 0.27, h + 0.2), m=mat("sign", (0.8, 0.75, 0.6)))
    box((0.5, 0.06, 0.1), (-0.3, 0.3, h + 0.2), m=DARK())
    box((0.3, 0.06, 0.1), (0.35, 0.3, h + 0.2), m=DARK())
    export("rs-doorway")

# ---- barricade: plank wall on braces with spikes on top ----
def barricade():
    reset()
    import random; random.seed(7)
    for i in range(7):
        x = -1.05 + i * 0.35
        hh = random.uniform(1.15, 1.4)
        box((0.3, 0.06, hh), (x, 0, hh/2), (0, random.uniform(-0.04, 0.04), 0), WOOD() if i % 2 else WOOD_D())
        cone(0.1, 0.2, (x, 0, hh + 0.1), m=WOOD_L(), verts=4)
    box((2.5, 0.1, 0.12), (0, -0.07, 0.4), m=WOOD_L())
    box((2.5, 0.1, 0.12), (0, -0.07, 1.0), m=WOOD_L())
    for x in (-0.9, 0.9):
        between((x, -0.1, 0.95), (x, -0.85, 0.0), 0.05, WOOD_D(), 5)
        box((0.5, 0.3, 0.1), (x, -0.85, 0.05), m=CONC_D())
    for i in range(5):
        x = -0.9 + i * 0.45
        between((x, 0.05, 0.75), (x, 0.55, 0.95), 0.03, WOOD_L(), 5, 0.003)
    export("rs-barricade")

# ---- bandage roll / medkit pickup ----
def medkit():
    reset()
    box((0.4, 0.28, 0.22), (0, 0, 0.11), m=mat("white", (0.85, 0.85, 0.82)))
    box((0.42, 0.3, 0.03), (0, 0, 0.13), m=CLOTH())
    box((0.16, 0.02, 0.05), (0, 0.145, 0.13), m=CLOTH())
    box((0.05, 0.02, 0.16), (0, 0.145, 0.13), m=CLOTH())
    box((0.12, 0.04, 0.03), (0, 0, 0.235), m=METAL())
    export("rs-medkit")

for f in (spike_trap, snare, club, rope_anchor, stair_hut, doorway, barricade, medkit): f()
