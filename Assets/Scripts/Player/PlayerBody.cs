using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The player's character body. Spawns the model chosen for the character, plays its animations from movement
/// (idle, walk, run, jump, strafe, turn), and handles the first/third-person view (ToggleView key).
/// First person: the body casts a shadow but is not drawn, and the first-person arms show.
/// Third person: the whole body is drawn and the camera pulls back behind it; the arms are hidden.
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
    Transform armRight, armLeft;
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
        if (cam != null)
        {
            armRight = cam.Find("ArmRight");
            armLeft = cam.Find("ArmLeft");
        }
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
        if (renderers != null)
        {
            foreach (var r in renderers)
                if (r != null) r.shadowCastingMode = thirdPerson ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }
        // FirstPersonArms builds its arms in its own Start, so look them up here rather than only once.
        if (cam != null)
        {
            if (armRight == null) armRight = cam.Find("ArmRight");
            if (armLeft == null) armLeft = cam.Find("ArmLeft");
        }
        if (armRight != null) armRight.gameObject.SetActive(!thirdPerson);
        if (armLeft != null) armLeft.gameObject.SetActive(!thirdPerson);
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
