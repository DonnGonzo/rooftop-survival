import bpy, sys
sys.path.insert(0, "/home/claude/tools")
import charlib as C, preview
T = "/home/claude/in/kim-kitsuragi-disco-elysium/textures/"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath="/home/claude/in/kim-kitsuragi-disco-elysium/source/kim_posed.fbx")
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
for pb in arm.pose.bones:
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
for o in list(meshes):
    if o.name.startswith("specs"): bpy.data.objects.remove(o, do_unlink=True); meshes.remove(o)
cloth = C.image_material("kim_cloth", T + "kim_Cloth_C.png"); bodym = C.image_material("kim_body", T + "kim_body_C.png")
budget = {"body_low": 9000, "jacket_low": 7000, "pants_low": 4000, "shirt_low": 2500, "gloves_low": 2500, "shoes_low": 3000, "laces_low": 1200, "soles_low": 600, "belt_low": 400, "glasses_low": 500}
for o in meshes:
    o.data.materials.clear(); o.data.materials.append(cloth if o.name.split("_")[0] in ("jacket", "pants", "shirt") else bodym)
    if o.name in budget: C.decimate(o, budget[o.name])
bpy.ops.object.select_all(action='DESELECT')
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.join()
body = bpy.context.view_layer.objects.active; body.name = "kim"
bpy.ops.object.shade_smooth()
print("tris", sum(len(p.vertices) - 2 for p in body.data.polygons))
C.normalise(arm, [body], 1.8)
print("facing: toe y", min(v.co.y for v in body.data.vertices if v.co.z < 0.1), max(v.co.y for v in body.data.vertices if v.co.z < 0.1))
C.add_clips(arm)
C.export("/home/claude/out/drifter-kim.glb", arm, [body])
arm.animation_data.action = bpy.data.actions["walk"]; bpy.context.scene.frame_set(8)
preview.render("/home/claude/prev/kim_walk", (0, 60))
arm.animation_data.action = bpy.data.actions["idle"]; bpy.context.scene.frame_set(8)
preview.render("/home/claude/prev/kim_idle", (0,))
