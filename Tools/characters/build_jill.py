import bpy, sys, math
import numpy as np
sys.path.insert(0, "/home/claude/tools")
import charlib as C, preview
from mathutils import Vector
H = 1.68
# ---- donor skeleton: Lara's Mixamo rig, cleaned the same way as in build_lara
bpy.ops.wm.open_mainfile(filepath="/home/claude/in/4d899826-lara-croft-just-dont-stare-into-the-eyes/source/LaraFortnite.blend")
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
lbody = next(o for o in bpy.data.objects if o.type == 'MESH' and o.parent == arm)
for o in list(bpy.data.objects):
    if o not in (arm, lbody): bpy.data.objects.remove(o, do_unlink=True)
if arm.animation_data: arm.animation_data.action = None
for a in list(bpy.data.actions): bpy.data.actions.remove(a)
for pb in arm.pose.bones:
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
arm.scale.x = abs(arm.scale.x)
for b in arm.data.bones:
    if b.name.startswith("rig:"): b.name = "mixamorig:" + b.name[4:]
C.normalise(arm, [lbody], H)
bpy.data.objects.remove(lbody, do_unlink=True)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)

# ---- Jill's mesh
bpy.ops.wm.obj_import(filepath="/home/claude/in/jill_obj/G4HAH8Y9ZO6GOGI3LULSYK992.obj")
jill = bpy.context.selected_objects[0]; jill.name = "jill"
jill.rotation_euler.z += math.radians(90)
bpy.context.view_layer.objects.active = jill
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.remove_doubles(threshold=1e-5); bpy.ops.object.mode_set(mode='OBJECT')
mn, mx = C.world_bounds([jill]); s = H / (mx.z - mn.z)
jill.scale = (s, s, s); jill.location = Vector((-(mn.x + mx.x) / 2 * s, 0, -mn.z * s))
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
C.decimate(jill, 32000)
bpy.ops.object.shade_smooth()
for m in jill.data.materials:
    for n in m.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and max(n.image.size) > 1024: n.image.scale(1024, int(1024 * n.image.size[1] / n.image.size[0]))
    b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b:
        b.inputs["Metallic"].default_value = 0; b.inputs["Roughness"].default_value = 0.85
        for l in list(b.inputs["Alpha"].links): m.node_tree.links.remove(l)
    m.blend_method = 'OPAQUE'
print("jill tris", sum(len(p.vertices) - 2 for p in jill.data.polygons), [(m.name) for m in jill.data.materials])

# ---- landmarks (fractions of height; +x is her left)
v = np.array([x.co[:] for x in jill.data.vertices]) / H
def arm_marks(side):
    a = v[v[:, 0] * side > 0.118]
    sh = np.array([0.095 * side, 0.072, 0.795])
    tip = a[a[:, 0] * side > 0.9 * (a[:, 0] * side).max()].mean(0)
    wrist = sh + (tip - sh) * 0.84
    ax = (wrist - sh); L = np.linalg.norm(ax); ax /= L
    t = (a - sh) @ ax
    best = (sh + wrist) / 2; bd = 0
    for k in np.arange(0.3, 0.75, 0.05):
        sl = a[(t > (k - 0.04) * L) & (t < (k + 0.04) * L)]
        if len(sl) < 10: continue
        c = sl.mean(0); d = np.linalg.norm((c - sh) - ((c - sh) @ ax) * ax)
        if d > bd: bd = d; best = c
    return sh, best, wrist, tip
def slice_c(side, z, xmax=0.2):
    s = v[(abs(v[:, 2] - z) < 0.012) & (v[:, 0] * side > 0.008) & (abs(v[:, 0]) < xmax)]
    return s.mean(0)
P = {"Hips": (0, 0.065, 0.53), "Spine": (0, 0.068, 0.585), "Spine1": (0, 0.07, 0.65), "Spine2": (0, 0.072, 0.72),
     "Neck": (0, 0.072, 0.845), "Head": (0, 0.062, 0.885), "HeadEnd": (0, 0.055, 1.0)}
for side, nm in ((1, "Left"), (-1, "Right")):
    sh, el, wr, tip = arm_marks(side)
    print(nm, "shoulder", sh.round(3), "elbow", el.round(3), "wrist", wr.round(3), "tip", tip.round(3))
    P[nm + "Shoulder"] = (0.03 * side, 0.075, 0.80); P[nm + "Arm"] = sh; P[nm + "ForeArm"] = el; P[nm + "Hand"] = wr; P[nm + "HandEnd"] = tip
    kn = slice_c(side, 0.285, 0.11); an = slice_c(side, 0.06, 0.11)
    P[nm + "UpLeg"] = (0.052 * side, 0.068, 0.50); P[nm + "Leg"] = (kn[0], kn[1], 0.285); P[nm + "Foot"] = (an[0], an[1] + 0.005, 0.06)
    P[nm + "ToeBase"] = (an[0], an[1] - 0.075, 0.015); P[nm + "ToeEnd"] = (an[0], an[1] - 0.115, 0.012)
    print(nm, "knee", kn.round(3), "ankle", an.round(3))
TAIL = {"Hips": "Spine", "Spine": "Spine1", "Spine1": "Spine2", "Spine2": "Neck", "Neck": "Head", "Head": "HeadEnd"}
for nm in ("Left", "Right"):
    TAIL.update({nm + "Shoulder": nm + "Arm", nm + "Arm": nm + "ForeArm", nm + "ForeArm": nm + "Hand", nm + "Hand": nm + "HandEnd",
                 nm + "UpLeg": nm + "Leg", nm + "Leg": nm + "Foot", nm + "Foot": nm + "ToeBase", nm + "ToeBase": nm + "ToeEnd"})
bpy.ops.object.select_all(action='DESELECT'); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode='EDIT')
eb = arm.data.edit_bones
for b in list(eb):
    n = C.short(b.name)
    if n not in TAIL: eb.remove(b)
for b in eb:
    n = C.short(b.name); b.use_connect = False
    b.head = Vector(P[n]) * H; b.tail = Vector(P[TAIL[n]]) * H
bpy.ops.object.mode_set(mode='OBJECT')

# ---- skin: weights are solved on a watertight proxy, then copied to the real mesh
bpy.ops.object.select_all(action='DESELECT'); jill.select_set(True); bpy.context.view_layer.objects.active = jill
bpy.ops.object.duplicate(); proxy = bpy.context.view_layer.objects.active; proxy.name = "proxy"
proxy.data.remesh_voxel_size = 0.011; bpy.ops.object.voxel_remesh()
print("proxy verts", len(proxy.data.vertices))
bpy.ops.object.select_all(action='DESELECT'); proxy.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
print("proxy groups", len(proxy.vertex_groups))
bpy.ops.object.select_all(action='DESELECT'); jill.select_set(True); bpy.context.view_layer.objects.active = jill
for g in proxy.vertex_groups: jill.vertex_groups.new(name=g.name)
dt = jill.modifiers.new("dt", 'DATA_TRANSFER'); dt.object = proxy; dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}
dt.vert_mapping = 'POLYINTERP_NEAREST'; dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
bpy.ops.object.modifier_apply(modifier="dt")
bpy.data.objects.remove(proxy, do_unlink=True)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
unw = sum(1 for x in jill.data.vertices if len(x.groups) == 0); print("unweighted verts", unw)
jill.parent = arm; am = jill.modifiers.new("Armature", 'ARMATURE'); am.object = arm

C.add_clips(arm)
C.export("/home/claude/out/drifter-jill.glb", arm, [jill])
arm.animation_data.action = bpy.data.actions["walk"]; bpy.context.scene.frame_set(8)
preview.render("/home/claude/prev/jill_walk", (0, 60, 180))
arm.animation_data.action = bpy.data.actions["idle"]; bpy.context.scene.frame_set(8)
preview.render("/home/claude/prev/jill_idle", (0, 90))
arm.animation_data.action = bpy.data.actions["run"]; bpy.context.scene.frame_set(5)
preview.render("/home/claude/prev/jill_run", (60,))
