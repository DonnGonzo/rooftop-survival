import bpy, sys, math
from mathutils import Vector
def render(out, yaw_list=(0, 90, 180), res=420):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'; sc.cycles.samples = 12; sc.cycles.device = 'CPU'
    sc.render.resolution_x = res; sc.render.resolution_y = int(res*1.3)
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.8,0.8,0.85,1); w.node_tree.nodes["Background"].inputs[1].default_value = 1.2
    mn = Vector((1e9,)*3); mx = Vector((-1e9,)*3)
    for o in bpy.data.objects:
        if o.type == 'MESH' and not o.hide_render:
            for c in o.bound_box:
                v = o.matrix_world @ Vector(c)
                mn = Vector(map(min, mn, v)); mx = Vector(map(max, mx, v))
    c = (mn+mx)/2; size = max((mx-mn))
    cam = bpy.data.cameras.new("c"); co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
    cam.type = 'ORTHO'; cam.ortho_scale = size*1.1
    for i, yaw in enumerate(yaw_list):
        a = math.radians(yaw)
        co.location = c + Vector((math.sin(a), -math.cos(a), 0.15)) * size*3
        d = c - co.location; co.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = "%s_%d.png" % (out, yaw); bpy.ops.render.render(write_still=True)
if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--")+1:]
    mode, path, out = a[0], a[1], a[2]
    if mode == "blend": bpy.ops.wm.open_mainfile(filepath=path)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        if mode == "fbx": bpy.ops.import_scene.fbx(filepath=path)
        elif mode == "glb": bpy.ops.import_scene.gltf(filepath=path)
        elif mode == "obj": bpy.ops.wm.obj_import(filepath=path)
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and len(o.data.vertices) < 30 and o.parent is None and mode == "blend": bpy.data.objects.remove(o)
        elif o.type in ('LIGHT','CAMERA'): bpy.data.objects.remove(o)
    render(out, tuple(int(x) for x in a[3].split(",")) if len(a) > 3 else (0, 90, 180))
