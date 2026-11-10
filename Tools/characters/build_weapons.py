"""Splits the melee pack into one GLB per weapon, standing upright (handle at the bottom, origin at the
pommel), textures reduced to 1024."""
import bpy, sys, os, math
sys.path.insert(0, "/home/claude/tools")
import preview
from mathutils import Vector, Matrix
SRC = "/home/claude/in/melee-weapons-pack/source/Blades.glb"
SETS = {
    "wp-katana": (["Blade.001", "Habaki.001", "Seppa.001", "Tsuba.002", "Tsuba_Middle", "Tsuka.001"], ["Tsuka.001"]),
    "wp-machete": (["Machete_Blade"], "Machete_Handle"),
    "wp-kukri": (["Kukri_Blade"], "Kukri_Handle"),
    "wp-nail-bat": (["Baseball_Bat_with_Nails", "Barbs", "wire", "Nails*"], "thin"),
    "wp-brick-hammer": (["Brick_Hammer"], "thin"),
    "wp-knife": (["Fisherman_Knife_Handle", "Fishermans_Knife_Blade"], ["Fisherman_Knife_Handle"]),
    "wp-throwing-knife": (["TK_Blade", "TK_Handle"], ["TK_Handle"]),
}
for out, (names, handle) in SETS.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SRC)
    keep = []
    for o in list(bpy.data.objects):
        ok = o.type == 'MESH' and any(o.name == n or (n.endswith("*") and o.name.startswith(n[:-1])) for n in names)
        if ok: keep.append(o)
    hpts = []
    for o in keep:
        mw = o.matrix_world.copy()
        if isinstance(handle, list) and o.name in handle: hpts += [mw @ v.co for v in o.data.vertices]
        elif isinstance(handle, str) and handle != "thin":
            idx = [i for i, m in enumerate(o.data.materials) if m and m.name == handle]
            for p in o.data.polygons:
                if p.material_index in idx: hpts += [mw @ o.data.vertices[i].co for i in p.vertices]
        o.parent = None; o.matrix_world = mw
    for o in list(bpy.data.objects):
        if o not in keep: bpy.data.objects.remove(o, do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in keep: o.select_set(True)
    bpy.context.view_layer.objects.active = keep[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if len(keep) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = out
    pts = [v.co.copy() for v in o.data.vertices]
    mn = Vector((min(p[i] for p in pts) for i in range(3))); mx = Vector((max(p[i] for p in pts) for i in range(3)))
    ext = mx - mn; ax = max(range(3), key=lambda i: ext[i])
    c = (mn + mx) / 2
    if handle == "thin":
        # the handle end is the end whose cross-section is smaller
        lo = [p for p in pts if p[ax] < mn[ax] + ext[ax] * 0.25]; hi = [p for p in pts if p[ax] > mx[ax] - ext[ax] * 0.25]
        def spread(ps): return sum(max(p[i] for p in ps) - min(p[i] for p in ps) for i in range(3) if i != ax)
        handle_low = spread(lo) < spread(hi)
    else:
        hc = sum(hpts, Vector()) / len(hpts); handle_low = hc[ax] < c[ax]
    d = Vector((0, 0, 0)); d[ax] = 1 if handle_low else -1      # direction from handle to tip
    rot = d.rotation_difference(Vector((0, 0, 1))).to_matrix().to_4x4()
    o.data.transform(rot)
    pts = [v.co for v in o.data.vertices]
    mn = Vector((min(p[i] for p in pts) for i in range(3))); mx = Vector((max(p[i] for p in pts) for i in range(3)))
    # origin on the handle's axis at the bottom
    low = [p for p in pts if p.z < mn.z + (mx.z - mn.z) * 0.15]
    cx = sum(p.x for p in low) / len(low); cy = sum(p.y for p in low) / len(low)
    o.data.transform(Matrix.Translation((-cx, -cy, -mn.z)))
    o.data.update()
    for m in o.data.materials:
        for n in m.node_tree.nodes:
            if n.type == 'TEX_IMAGE' and n.image and max(n.image.size) > 1024: n.image.scale(1024, 1024)
        m.blend_method = 'OPAQUE'
    path = "/home/claude/out/%s.glb" % out
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_yup=True, export_image_format='JPEG', export_jpeg_quality=85, export_animations=False)
    print("wrote", out, os.path.getsize(path), "length %.2f" % (mx.z - mn.z), "tris", sum(len(p.vertices) - 2 for p in o.data.polygons))
    preview.render("/home/claude/prev/" + out, (0,), 220)
