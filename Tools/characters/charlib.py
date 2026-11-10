"""Shared helpers: clean up a rigged character and give it idle / walk / run clips retargeted from
Mixamo locomotion FBX files. The retarget works on world-space rotation deltas, so the target's rest pose
(A-pose or T-pose) and bone rolls do not have to match the source's."""
import bpy, os, math
from mathutils import Vector, Quaternion, Matrix

PACK = "/home/claude/in/dbf22064-Male_Locomotion_Pack"
CLIPS = [("idle", "idle.fbx"), ("walk", "walking.fbx"), ("run", "standard run.fbx")]

def short(n): return n.split(":")[-1]

def bones_by_short(arm): return {short(b.name): b for b in arm.data.bones}

def main_dir(arm, bone, names):
    """Direction (armature space, rest) from this bone to its same-chain child, or None."""
    kids = [c for c in bone.children if short(c.name) in names]
    if not kids: return None
    pref = {"Hips": "Spine", "Spine2": "Neck", "Spine": "Spine1", "Spine1": "Spine2", "LeftHand": "LeftHandMiddle1", "RightHand": "RightHandMiddle1"}
    k = next((c for c in kids if short(c.name) == pref.get(short(bone.name))), None)
    if k is None:
        if short(bone.name) in ("LeftHand", "RightHand", "Hips", "Spine2"): return None
        k = kids[0]
    d = k.head_local - bone.head_local
    return d.normalized() if d.length > 1e-6 else None

def retarget(target, fbx_path, clip_name, in_place=True):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx_path, use_anim=True, automatic_bone_orientation=False, ignore_leaf_bones=True)
    new = [o for o in bpy.data.objects if o not in before]
    src = next(o for o in new if o.type == 'ARMATURE')
    act = src.animation_data.action
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    sb = bones_by_short(src); tb = bones_by_short(target)
    common = [n for n in tb if n in sb]
    # armature-space rest rotations, with the source object's rotation folded in so both are in world axes
    sw = src.matrix_world.to_quaternion(); tw = target.matrix_world.to_quaternion()
    s_rest = {n: sw @ sb[n].matrix_local.to_quaternion() for n in common}
    t_rest = {n: tw @ tb[n].matrix_local.to_quaternion() for n in common}
    # alignment: turn the target's rest bone direction onto the source's
    align = {}
    def order(b, out):
        out.append(b)
        for c in b.children: order(c, out)
    chain = []
    for b in target.data.bones:
        if b.parent is None: order(b, chain)
    for b in chain:
        n = short(b.name)
        if n not in common: continue
        limb = n.replace("Left", "").replace("Right", "") in ("Arm", "ForeArm", "UpLeg", "Leg")
        ds = main_dir(src, sb[n], set(common)) if limb else None; dt = main_dir(target, tb[n], set(common)) if limb else None
        if n in ("Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "RightShoulder"):
            align[n] = Quaternion()
        elif ds is not None and dt is not None:
            align[n] = (sw @ ds).rotation_difference(tw @ dt).inverted()
        else:
            p = b.parent
            while p is not None and short(p.name) not in align: p = p.parent
            align[n] = align[short(p.name)].copy() if p is not None else Quaternion()
    hip_s = (src.matrix_world @ sb["Hips"].head_local).z; hip_t = (target.matrix_world @ tb["Hips"].head_local).z
    ratio = hip_t / hip_s if abs(hip_s) > 1e-6 else 1.0
    new_act = bpy.data.actions.new(clip_name); new_act.use_fake_user = True
    if target.animation_data is None: target.animation_data_create()
    target.animation_data.action = new_act
    for pb in target.pose.bones: pb.rotation_mode = 'QUATERNION'
    sc = bpy.context.scene
    twi = target.matrix_world.inverted()
    hips_rest_world = target.matrix_world @ tb["Hips"].head_local
    src_hips_rest_world = src.matrix_world @ sb["Hips"].head_local
    prev = {}
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        bpy.context.view_layer.update()
        for b in chain:
            n = short(b.name)
            pb = target.pose.bones[b.name]
            if n not in common: continue
            s_now = (src.matrix_world @ src.pose.bones[sb[n].name].matrix).to_quaternion()
            delta = s_now @ s_rest[n].inverted()
            want = delta @ align[n] @ t_rest[n]          # world rotation
            m = pb.matrix.copy()
            loc = m.to_translation()
            if n == "Hips":
                sh = (src.matrix_world @ src.pose.bones[sb[n].name].matrix).to_translation()
                off = sh - src_hips_rest_world
                w = hips_rest_world + Vector((0 if in_place else off.x * ratio, 0 if in_place else off.y * ratio, off.z * ratio))
                loc = twi @ w
            rot = (twi.to_quaternion() @ want)
            pb.matrix = Matrix.Translation(loc) @ rot.to_matrix().to_4x4()
            bpy.context.view_layer.update()
            q = pb.rotation_quaternion.copy()
            if n in prev and prev[n].dot(q) < 0: q.negate(); pb.rotation_quaternion = q
            prev[n] = q
            pb.keyframe_insert("rotation_quaternion", frame=f - f0)
            if n == "Hips": pb.keyframe_insert("location", frame=f - f0)
    for o in new: bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.actions.remove(act)
    target.animation_data.action = None
    for pb in target.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0)
    print("clip", clip_name, f1 - f0 + 1, "frames")
    return new_act

def add_clips(target):
    acts = [retarget(target, os.path.join(PACK, f), n) for n, f in CLIPS]
    target.animation_data.action = acts[0]
    return acts

def image_material(name, path, size=1024, alpha=False):
    img = bpy.data.images.load(path)
    if max(img.size) > size: img.scale(size, int(size * img.size[1] / img.size[0]))
    out = "/home/claude/build/%s.%s" % (name, "png" if alpha else "jpg")
    img.filepath_raw = out; img.file_format = 'PNG' if alpha else 'JPEG'; img.save()
    img = bpy.data.images.load(out)
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = img
    m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.85; b.inputs["Metallic"].default_value = 0.0
    if alpha:
        m.node_tree.links.new(t.outputs["Alpha"], b.inputs["Alpha"]); m.blend_method = 'CLIP'; m.alpha_threshold = 0.5
    return m

def world_bounds(objs):
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    return mn, mx

def apply_all(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def normalise(arm, meshes, height=1.75):
    apply_all([arm] + meshes)
    mn, mx = world_bounds(meshes)
    s = height / (mx.z - mn.z)
    arm.scale = (s, s, s)
    arm.location = Vector((-(mn.x + mx.x) / 2 * s, -(mn.y + mx.y) / 2 * s, -mn.z * s))
    bpy.context.view_layer.update()
    apply_all([arm] + meshes)

def decimate(o, tris):
    bpy.context.view_layer.objects.active = o
    cur = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if cur <= tris: return
    m = o.modifiers.new("dec", 'DECIMATE'); m.ratio = tris / cur
    # modifier order: decimate must come before the armature
    while o.modifiers[0] != m: bpy.ops.object.modifier_move_up(modifier=m.name)
    bpy.ops.object.modifier_apply(modifier=m.name)

def export(path, arm, meshes):
    bpy.ops.object.select_all(action='DESELECT')
    for o in [arm] + meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_animations=True,
                              export_animation_mode='ACTIONS', export_apply=False, export_yup=True,
                              export_image_format='JPEG', export_jpeg_quality=85, export_force_sampling=True,
                              export_optimize_animation_size=True)
    print("wrote", path, os.path.getsize(path))
