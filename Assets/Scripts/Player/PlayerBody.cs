using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The player's character body. Spawns the model chosen for the character and plays its clips from movement
/// (idle, walk, run, jump, strafe, turn). Arms are then bent toward the hand targets from MeleeCombat, so
/// punches and weapon swings sit on top of the locomotion clips.
///
/// First person: the body sits a little behind the eyes and the head is shrunk away, so you see your own
/// arms, chest and legs from outside the mesh (never the inside of the torso).
/// Third person (ToggleView key): the head is back and the camera pulls behind the shoulder.
/// </summary>
public class PlayerBody : MonoBehaviour
{
    [Tooltip("The camera under this player. Set by SceneBootstrap.")]
    public Transform cam;
    [Tooltip("Height of the body in metres. Matches the collider (1.8 m).")]
    public float height = 1.8f;
    [Tooltip("Turn the model if it faces backwards.")]
    public float yawOffset = 0f;
    [Tooltip("First person: how far the body sits behind the eyes, in metres.")]
    public float firstPersonSetback = 0.13f;
    [Tooltip("Third-person camera position relative to the first-person camera: right, up, back.")]
    public Vector3 thirdPersonOffset = new Vector3(0.55f, 0.2f, -3.0f);
    [Tooltip("Degrees the chest turns into a strike.")]
    public float twistDegrees = 28f;

    public bool ThirdPerson => thirdPerson;

    GameObject model;
    Animation anim;
    Transform headBone, chest;
    Transform rArm, rFore, rHand, lArm, lFore, lHand;
    Hand rightHand, leftHand;
    readonly System.Collections.Generic.List<Renderer> headParts = new System.Collections.Generic.List<Renderer>();
    MeleeCombat melee;
    bool thirdPerson;

    /// <summary>The finger bones of one hand, found by name, so the hand can close into a fist on any rig.</summary>
    class Hand
    {
        public Transform hand, index, pinky, middle;
        public bool right;
        public readonly System.Collections.Generic.List<Transform> fingers = new System.Collections.Generic.List<Transform>();
        public readonly System.Collections.Generic.List<Transform> thumb = new System.Collections.Generic.List<Transform>();

        public Hand(Transform hand, bool right)
        {
            this.hand = hand;
            this.right = right;
            foreach (var t in hand.GetComponentsInChildren<Transform>(true))
            {
                if (t == hand || t.childCount == 0 && t.parent == hand) continue;
                string n = t.name.ToLowerInvariant();
                bool isThumb = n.Contains("thumb");
                if (t.parent == hand)
                {
                    if (n.Contains("index")) index = t;
                    else if (n.Contains("pinky")) pinky = t;
                    else if (n.Contains("middle")) middle = t;
                }
                if (t.childCount == 0) continue;   // fingertip end bones have nothing to bend
                (isThumb ? thumb : fingers).Add(t);
            }
        }

        public bool Valid => index != null && pinky != null;

        /// <summary>Unit vector out of the palm, worked out from where the index and pinky knuckles sit.</summary>
        public Vector3 PalmDir()
        {
            Vector3 n = Vector3.Cross(index.position - hand.position, pinky.position - hand.position).normalized;
            return right ? -n : n;
        }

        public Vector3 FingerDir()
        {
            Transform m = middle != null ? middle : index;
            return (m.position - hand.position).normalized;
        }

        /// <summary>Closes the fingers by amount (0 open as animated, 1 fist). Parents come first in the list, so the curl adds up along each finger.</summary>
        public void Curl(float amount)
        {
            if (!Valid || amount <= 0.001f) return;
            Vector3 palm = PalmDir();
            Vector3 axis = Vector3.zero;
            foreach (var j in fingers)
            {
                // One bend axis per finger, taken at its knuckle, so every joint curls the same way.
                if (j.parent == hand) axis = Vector3.Cross(j.GetChild(0).position - j.position, palm).normalized;
                if (axis.sqrMagnitude < 0.5f) continue;
                j.rotation = Quaternion.AngleAxis((j.parent == hand ? 70f : 85f) * amount, axis) * j.rotation;
            }
            foreach (var j in thumb)
            {
                if (j.parent == hand) axis = Vector3.Cross(j.GetChild(0).position - j.position, palm).normalized;
                if (axis.sqrMagnitude < 0.5f) continue;
                j.rotation = Quaternion.AngleAxis(48f * amount, axis) * j.rotation;
            }
        }

        /// <summary>Turns the wrist so the fingers point along forward with the palm facing palmDir.</summary>
        public void Orient(Vector3 forward, Vector3 palmDir, float weight)
        {
            if (!Valid || weight <= 0.001f) return;
            Quaternion now = Quaternion.LookRotation(FingerDir(), -PalmDir());
            Quaternion want = Quaternion.LookRotation(forward, -palmDir);
            Quaternion full = want * Quaternion.Inverse(now) * hand.rotation;
            hand.rotation = Quaternion.Slerp(hand.rotation, full, weight);
        }
    }
    string current = "";
    float turnUntil;

    void Start()
    {
        string key = Characters.Current.modelKey;
        model = ModelLibrary.Spawn(key, transform, Vector3.zero, yawOffset, fitHeight: height, keepAnimations: true);
        if (model == null)
        {
            Debug.LogWarning("[PlayerBody] Model '" + key + "' is not in Resources/Models. The player has no body.");
            return;
        }
        anim = model.GetComponentInChildren<Animation>(true);
        if (anim == null) Debug.LogWarning("[PlayerBody] '" + key + "' has no Animation component. Set its importer Animation to Legacy.");
        foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            r.updateWhenOffscreen = true;   // the body is always right at the camera; never cull it by stale bounds
            r.shadowCastingMode = ShadowCastingMode.On;
        }

        Transform root = model.transform;
        headBone = FindBone(root, "head");
        chest = FindBone(root, "spine2", "spine_03", "spine1", "spine_02");
        rArm = FindBone(root, "rightarm", "upperarm_r");
        rFore = FindBone(root, "rightforearm", "lowerarm_r");
        rHand = FindBone(root, "righthand", "hand_r");
        lArm = FindBone(root, "leftarm", "upperarm_l");
        lFore = FindBone(root, "leftforearm", "lowerarm_l");
        lHand = FindBone(root, "lefthand", "hand_l");
        if (rHand != null) rightHand = new Hand(rHand, true);
        if (lHand != null) leftHand = new Hand(lHand, false);
        // Separate head pieces (hair, hat, beard, glasses): hidden in first person along with the head itself.
        if (headBone != null)
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (r.bounds.min.y > headBone.position.y - 0.14f) headParts.Add(r);
        melee = GetComponent<MeleeCombat>();
        ApplyView();
        Play("idle");
    }

    void Update()
    {
        if (GameState.Frozen) return;

        if (Inp.ToggleView()) SetThirdPerson(!thirdPerson);
        if (model != null) Animate();
    }

    public void SetThirdPerson(bool on)
    {
        thirdPerson = on;
        ApplyView();
    }

    void ApplyView()
    {
        CameraFeel.viewOffset = thirdPerson ? thirdPersonOffset : Vector3.zero;
        foreach (var r in headParts)
            if (r != null) r.shadowCastingMode = thirdPerson ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        if (model != null)
            model.transform.localPosition = thirdPerson ? Vector3.zero : new Vector3(0f, 0f, -firstPersonSetback);
    }

    /// <summary>Runs after the Animation component has posed the skeleton for this frame.</summary>
    void LateUpdate()
    {
        if (model == null) return;

        // First person: no head in front of the eyes (the hat, hair and glasses are skinned to it and go too).
        if (headBone != null) headBone.localScale = thirdPerson ? Vector3.one : Vector3.one * 0.001f;

        if (melee == null || melee.Aim == null) return;

        if (chest != null && Mathf.Abs(melee.Twist) > 0.001f)
            chest.rotation = Quaternion.AngleAxis(-melee.Twist * twistDegrees, Vector3.up) * chest.rotation;

        Vector3 right = transform.right, down = -transform.up, back = -transform.forward;
        // Knuckles lead: fingers along the view, tipped up a little; palms face each other.
        Vector3 fingersTo = (melee.Aim.forward + melee.Aim.up * 0.35f).normalized;
        if (melee.RightWeight > 0.001f && rArm != null && rFore != null && rHand != null)
        {
            ReachFor(rArm, rFore, rHand, melee.RightTarget.position,
                     rArm.position + right * 0.18f + down * 0.6f + back * 0.2f, melee.RightWeight);
            if (rightHand != null)
            {
                rightHand.Orient(fingersTo, (-right + down * 0.3f).normalized, melee.RightWeight);
                rightHand.Curl(melee.RightWeight);
            }
        }
        if (melee.LeftWeight > 0.001f && lArm != null && lFore != null && lHand != null)
        {
            ReachFor(lArm, lFore, lHand, melee.LeftTarget.position,
                     lArm.position - right * 0.18f + down * 0.6f + back * 0.2f, melee.LeftWeight);
            if (leftHand != null)
            {
                leftHand.Orient(fingersTo, (right + down * 0.3f).normalized, melee.LeftWeight);
                leftHand.Curl(melee.LeftWeight);
            }
        }
    }

    /// <summary>
    /// Two-bone IK: bends the elbow and turns the shoulder so the hand lands on the target, with the elbow
    /// pointing toward the hint. weight blends between the clip's pose (0) and the full reach (1).
    /// </summary>
    static void ReachFor(Transform upper, Transform fore, Transform hand, Vector3 target, Vector3 elbowHint, float weight)
    {
        Quaternion upper0 = upper.localRotation, fore0 = fore.localRotation;

        float lenA = Vector3.Distance(upper.position, fore.position);
        float lenB = Vector3.Distance(fore.position, hand.position);
        Vector3 toTarget = target - upper.position;
        float dist = Mathf.Clamp(toTarget.magnitude, 0.05f, (lenA + lenB) * 0.999f);

        // 1. Elbow: the inside angle that makes shoulder-to-hand the right length.
        Vector3 ba = upper.position - fore.position, bc = hand.position - fore.position;
        float angleNow = Vector3.Angle(ba, bc);
        float cos = (lenA * lenA + lenB * lenB - dist * dist) / (2f * lenA * lenB);
        float angleWant = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f)) * Mathf.Rad2Deg;
        Vector3 axis = Vector3.Cross(ba, bc);
        if (axis.sqrMagnitude < 1e-8f) axis = Vector3.Cross(ba, elbowHint - fore.position);
        if (axis.sqrMagnitude > 1e-8f)
            fore.rotation = Quaternion.AngleAxis(angleWant - angleNow, axis.normalized) * fore.rotation;

        // 2. Shoulder: swing the whole arm so the hand points at the target.
        upper.rotation = Quaternion.FromToRotation(hand.position - upper.position, toTarget) * upper.rotation;

        // 3. Roll around the shoulder-to-target line so the elbow sits toward the hint.
        Vector3 line = (hand.position - upper.position).normalized;
        Vector3 elbowDir = Vector3.ProjectOnPlane(fore.position - upper.position, line);
        Vector3 hintDir = Vector3.ProjectOnPlane(elbowHint - upper.position, line);
        if (elbowDir.sqrMagnitude > 1e-6f && hintDir.sqrMagnitude > 1e-6f)
            upper.rotation = Quaternion.FromToRotation(elbowDir, hintDir) * upper.rotation;

        if (weight < 0.999f)
        {
            upper.localRotation = Quaternion.Slerp(upper0, upper.localRotation, weight);
            fore.localRotation = Quaternion.Slerp(fore0, fore.localRotation, weight);
        }
    }

    /// <summary>First bone whose name ends with one of the suffixes (Mixamo "mixamorig:RightHand", UE "girl02:hand_r").</summary>
    static Transform FindBone(Transform root, params string[] suffixes)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        foreach (var s in suffixes)
            foreach (var t in all)
                if (t.name.ToLowerInvariant().EndsWith(s)) return t;
        return null;
    }

    void Animate()
    {
        var p = PlayerController.Instance;
        if (p == null || anim == null) return;

        Vector2 move = Inp.Move();
        Vector2 look = Inp.Look();
        string want;
        float speed = 1f;

        if (!p.Grounded && Mathf.Abs(p.VerticalSpeed) > 2.5f)
        {
            want = "jump";
        }
        else if (p.HorizontalSpeed < 0.25f)
        {
            // Standing still: a short turn clip when the view swings sideways quickly, otherwise idle.
            if (Mathf.Abs(look.x) > 3f) turnUntil = Time.time + 0.4f;
            if (Time.time < turnUntil) want = look.x > 0f ? "turn_right" : "turn_left";
            else want = "idle";
        }
        else if (Mathf.Abs(move.x) > Mathf.Abs(move.y) * 1.2f)
        {
            want = move.x > 0f ? "strafe_right" : "strafe_left";
        }
        else
        {
            want = p.Sprinting && move.y > 0f ? "run" : "walk";
            if (move.y < 0f) speed = -1f;   // walking backwards plays the walk in reverse
        }

        if (want != current)
        {
            current = want;
            Play(want);
        }
        var st = State(want);
        if (st != null) st.speed = speed;
    }

    AnimationState State(string name)
    {
        if (anim == null) return null;
        foreach (AnimationState s in anim)
            if (s.name == name) return s;
        return null;
    }

    void Play(string state)
    {
        if (anim == null) return;
        string[] names;
        switch (state)
        {
            case "run": names = new[] { "run", "walk" }; break;
            case "walk": names = new[] { "walk", "run", "idle" }; break;
            case "jump": names = new[] { "jump", "idle" }; break;
            case "strafe_left": names = new[] { "strafe_left", "walk" }; break;
            case "strafe_right": names = new[] { "strafe_right", "walk" }; break;
            case "turn_left": names = new[] { "turn_left", "idle" }; break;
            case "turn_right": names = new[] { "turn_right", "idle" }; break;
            default: names = new[] { "idle", "walk" }; break;
        }
        foreach (string n in names)
        {
            var st = State(n);
            if (st == null) continue;
            st.wrapMode = n == "jump" ? WrapMode.ClampForever : WrapMode.Loop;
            anim.enabled = true;
            anim.CrossFade(n, 0.18f);
            return;
        }
    }
}
