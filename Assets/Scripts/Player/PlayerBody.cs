using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The player's character body. Spawns the model chosen for the character, plays its animations from movement
/// (idle, walk, run, jump, strafe, turn), and handles the first/third-person view (ToggleView key).
/// First person: the camera sits at eye height, the head bone is shrunk to nothing so the head blocks no view,
/// and the rest of the body (shoulders, arms, chest, legs) stays drawn so its movement is visible.
/// Third person: the head is restored and the camera pulls back behind the player.
/// </summary>
public class PlayerBody : MonoBehaviour
{
    [Tooltip("The camera under this player. Set by SceneBootstrap.")]
    public Transform cam;

    [Tooltip("Height of the body in metres. Matches the collider (1.8 m).")]
    public float height = 1.8f;

    [Tooltip("Turn the model if it walks backwards. Mixamo-style rigs often need 180.")]
    public float yawOffset = 180f;

    [Tooltip("Third-person camera position relative to the first-person camera: right, up, back.")]
    public Vector3 thirdPersonOffset = new Vector3(0.6f, 0.25f, -3.2f);

    GameObject model;
    Animation anim;
    Renderer[] renderers;
    Transform headBone, rArm, rFore, rHand;
    MeleeCombat melee;
    bool thirdPerson;
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
        renderers = model.GetComponentsInChildren<Renderer>(true);
        headBone = FindHeadBone(model.transform);
        rArm = FindBoneAny(model.transform, "rightarm", "upperarm_r");
        rFore = FindBoneAny(model.transform, "rightforearm", "lowerarm_r");
        rHand = FindBoneAny(model.transform, "righthand", "hand_r");
        melee = GetComponent<MeleeCombat>();
        ApplyView();
    }

    void Update()
    {
        if (GameState.Frozen || GameState.MenuOpen) return;

        if (Inp.ToggleView())
        {
            thirdPerson = !thirdPerson;
            ApplyView();
        }
        if (model != null) Animate();
    }

    void ApplyView()
    {
        CameraFeel.viewOffset = thirdPerson ? thirdPersonOffset : Vector3.zero;
        // First person: the body is not drawn (seen from inside, the mesh shows its inner faces and looks broken).
        // It still casts a shadow, and third person (V) draws the whole character with its animations.
        if (renderers != null)
        {
            foreach (var r in renderers)
                if (r != null) r.shadowCastingMode = thirdPerson ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }
        if (headBone != null) headBone.localScale = Vector3.one;
    }

    /// <summary>
    /// A punch on top of whatever clip is playing: while the melee swing runs, the right arm is aimed
    /// forward along the player's facing, strongest at the middle of the swing. Runs after the Animation component.
    /// </summary>
    void LateUpdate()
    {
        if (model == null || melee == null || !melee.IsSwinging || rArm == null || rHand == null) return;

        float w = Mathf.Sin(melee.SwingPhase * Mathf.PI);
        if (w <= 0.001f) return;
        Vector3 forward = transform.forward;

        // Aim the upper arm: from its current direction toward the forward punch direction, by w.
        Vector3 cur = ((rFore != null ? rFore.position : rHand.position) - rArm.position).normalized;
        Vector3 aim = Vector3.Lerp(cur, forward, w * 0.9f).normalized;
        rArm.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(cur, aim), w) * rArm.rotation;

        // Then the forearm, straightening the punch toward the same direction.
        if (rFore != null)
        {
            Vector3 curF = (rHand.position - rFore.position).normalized;
            Vector3 aimF = Vector3.Lerp(curF, forward, w).normalized;
            rFore.rotation = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(curF, aimF), w) * rFore.rotation;
        }
    }

    /// <summary>Finds a bone whose name ends with one of the given suffixes (Mixamo "mixamorig:RightHand", UE "girl02:hand_r").</summary>
    static Transform FindBoneAny(Transform root, params string[] suffixes)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            foreach (var s in suffixes)
                if (n.EndsWith(s)) return t;
        }
        return null;
    }

    /// <summary>The head bone of a rig. Mixamo names it "mixamorig:Head", the UE-style rig "girl02:head".</summary>
    static Transform FindHeadBone(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLowerInvariant();
            if (n.EndsWith("head") && !n.Contains("top") && !n.Contains("eye")) return t;
        }
        return null;
    }

    void Animate()
    {
        var p = PlayerController.Instance;
        if (p == null) return;

        Vector2 move = Inp.Move();
        Vector2 look = Inp.Look();
        string want;

        if (!p.Grounded)
        {
            want = "jump";
        }
        else if (p.HorizontalSpeed < 0.25f)
        {
            // Standing still: a short turn clip when the view swings sideways quickly, otherwise idle.
            if (Mathf.Abs(look.x) > 3f) turnUntil = Time.time + 0.5f;
            if (Time.time < turnUntil) want = look.x > 0f ? "turn_right" : "turn_left";
            else want = "idle";
        }
        else if (Mathf.Abs(move.x) > Mathf.Abs(move.y) * 1.2f)
        {
            want = move.x > 0f ? "strafe_right" : "strafe_left";
        }
        else
        {
            want = p.Sprinting ? "run" : "walk";
        }

        if (want == current) return;
        current = want;
        Play(want);
    }

    void Play(string state)
    {
        if (anim == null) return;

        // Each state falls back to the closest clip the model has (for example walk, when there is no run).
        string[] names;
        switch (state)
        {
            case "run": names = new[] { "run", "walk" }; break;
            case "walk": names = new[] { "walk", "run", "idle" }; break;
            case "jump": names = new[] { "jump", "idle", "walk" }; break;
            case "strafe_left": names = new[] { "strafe_left", "walk" }; break;
            case "strafe_right": names = new[] { "strafe_right", "walk" }; break;
            case "turn_left": names = new[] { "turn_left", "idle" }; break;
            case "turn_right": names = new[] { "turn_right", "idle" }; break;
            default: names = new[] { "idle" }; break;
        }

        if (state == "idle" && !HasClip("idle"))
        {
            // No idle clip: hold the first frame of the walk so the body stands still instead of walking on the spot.
            ModelLibrary.PlayClip(model, "walk", "run");
            foreach (AnimationState s in anim) s.time = 0f;
            anim.Sample();
            anim.enabled = false;
            return;
        }

        ModelLibrary.PlayClip(model, names);
    }

    bool HasClip(string word)
    {
        foreach (AnimationState s in anim)
            if (s.name.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
