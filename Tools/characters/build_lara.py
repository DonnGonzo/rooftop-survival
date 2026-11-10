import bpy, sys
sys.path.insert(0, "/home/claude/tools")
import charlib as C, preview
bpy.ops.wm.open_mainfile(filepath="/home/claude/in/4d899826-lara-croft-just-dont-stare-into-the-eyes/source/LaraFortnite.blend")
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm)
for o in list(bpy.data.objects):
    if o not in (arm, body): bpy.data.objects.remove(o, do_unlink=True)
if arm.animation_data: arm.animation_data.action = None
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for pb in arm.pose.bones:
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
arm.scale.x = abs(arm.scale.x)
for b in arm.data.bones:
    if b.name.startswith("rig:"): b.name = "mixamorig:" + b.name[4:]
bpy.context.view_layer.update()
print("mesh parent/transform", body.parent_type, body.location[:], body.scale[:], [m.type for m in body.modifiers])
C.normalise(arm, [body], 1.72)
# textures: repack at 1024
for m in body.data.materials:
    for n in m.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and max(n.image.size) > 1024:
            n.image.scale(1024, int(1024 * n.image.size[1] / n.image.size[0]))
    b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b: b.inputs["Metallic"].default_value = 0; b.inputs["Roughness"].default_value = 0.85; print("alpha", m.name, m.blend_method, b.inputs["Alpha"].is_linked)
    m.blend_method = 'OPAQUE'
import bmesh
print("facing check: nose y", min((body.matrix_world @ v.co).y for v in body.data.vertices if (body.matrix_world @ v.co).z > 1.5))
C.add_clips(arm)
C.export("/home/claude/out/drifter-lara.glb", arm, [body])
arm.animation_data.action = bpy.data.actions["walk"]; bpy.context.scene.frame_set(8)
preview.render("/home/claude/prev/lara_walk", (0, 60))
arm.animation_data.action = bpy.data.actions["idle"]; bpy.context.scene.frame_set(8)
preview.render("/home/claude/prev/lara_idle", (0,))
