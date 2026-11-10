using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Melee with a three-hit combo. Attack chains light hits (1, 2, then a finisher); Heavy is one big strike.
/// Bare hands: jab, cross, hook. With a weapon: two side swings and an overhead blow, or thrusts for a spear.
/// The same strikes chop trees, break rock and strip wrecks (see ResourceNode).
///
/// This script only decides WHERE the hands should be. It moves two targets in front of the player's eyes
/// (RightTarget, LeftTarget); PlayerBody bends the character's real arms to reach them, on top of whatever
/// walk/run/idle clip is playing. The weapon rides on the right target, so the hand is always on the grip.
/// </summary>
public class MeleeCombat : MonoBehaviour
{
    public Transform cam;
    public float comboWindow = 0.85f;
    public float fistDamage = 8f, fistReach = 1.7f;

    /// <summary>0 = no chain, 1..3 = hits in the current chain (shown on the HUD).</summary>
    public static int ComboStep { get; private set; }

    public bool IsSwinging => swinging;
    public bool Armed => weapon != null;

    /// <summary>Eye-height frame that pitches with the view. Hand targets live under it.</summary>
    public Transform Aim { get; private set; }
    public Transform RightTarget { get; private set; }
    public Transform LeftTarget { get; private set; }
    /// <summary>How strongly each arm follows its target (0 = the locomotion clip owns the arm).</summary>
    public float RightWeight { get; private set; }
    public float LeftWeight { get; private set; }
    /// <summary>0 = fist at the guard, 1 = arm fully out. PlayerBody turns the knuckles over as this rises.</summary>
    public float RightExtend { get; private set; }
    public float LeftExtend { get; private set; }
    /// <summary>-1..1: how far the torso turns into the current strike (positive = turning left).</summary>
    public float Twist { get; private set; }
    /// <summary>0..1: how far the upper body leans into the strike.</summary>
    public float Lean { get; private set; }

    class Strike
    {
        public bool left;
        public Vector3 windup, via, hit;   // hand path in the Aim frame; via bends the path (hooks, swings)
        public Vector3 rotWindup, rotHit;  // weapon rotation (ignored for fists)
        public float dur, mult, twist;
        public Strike(bool left, Vector3 windup, Vector3 via, Vector3 hit, Vector3 rotWindup, Vector3 rotHit, float dur, float mult, float twist)
        { this.left = left; this.windup = windup; this.via = via; this.hit = hit; this.rotWindup = rotWindup; this.rotHit = rotHit; this.dur = dur; this.mult = mult; this.twist = twist; }
    }

    static readonly Vector3 FistRestR = new Vector3(0.15f, -0.18f, 0.24f);
    static readonly Vector3 FistRestL = new Vector3(-0.14f, -0.17f, 0.27f);   // lead hand a little further out
    static readonly Vector3 ArmedRest = new Vector3(0.3f, -0.34f, 0.36f);
    static readonly Vector3 ArmedRestRot = new Vector3(38f, -10f, -14f);
    static readonly Vector3 SpearRestRot = new Vector3(72f, -6f, 0f);
    static readonly Vector3 Z = Vector3.zero;

    static readonly Strike[] FistCombo =
    {
        // jab with the lead (left) hand: straight out and straight back
        new Strike(true,  new Vector3(-0.15f, -0.15f, 0.28f), new Vector3(-0.08f, -0.1f, 0.5f), new Vector3(-0.02f, -0.07f, 0.66f), Z, Z, 0.26f, 1f, 0.35f),
        // cross with the rear (right) hand: the hips turn through it
        new Strike(false, new Vector3(0.2f, -0.17f, 0.2f), new Vector3(0.1f, -0.1f, 0.48f), new Vector3(0f, -0.06f, 0.7f), Z, Z, 0.34f, 1.3f, 1f),
        // lead hook: the fist travels in an arc from the side
        new Strike(true,  new Vector3(-0.3f, -0.12f, 0.2f), new Vector3(-0.36f, -0.08f, 0.52f), new Vector3(0.08f, -0.07f, 0.52f), Z, Z, 0.42f, 1.7f, 0.9f),
    };
    static readonly Strike FistHeavy =
        new Strike(false, new Vector3(0.3f, -0.22f, 0.08f), new Vector3(0.34f, -0.12f, 0.42f), new Vector3(-0.04f, -0.04f, 0.7f), Z, Z, 0.62f, 2.5f, 1.2f);

    static readonly Strike[] SwingCombo =
    {
        new Strike(false, new Vector3(0.46f, -0.1f, 0.3f), new Vector3(0.3f, -0.14f, 0.62f), new Vector3(-0.16f, -0.22f, 0.5f), new Vector3(-20f, -70f, -20f), new Vector3(-10f, 70f, 20f), 0.42f, 1f, 0.8f),
        new Strike(false, new Vector3(-0.14f, -0.1f, 0.4f), new Vector3(0.1f, -0.14f, 0.64f), new Vector3(0.44f, -0.22f, 0.42f), new Vector3(-10f, 70f, 20f), new Vector3(-20f, -70f, -20f), 0.42f, 1.2f, -0.6f),
        new Strike(false, new Vector3(0.24f, 0.1f, 0.2f), new Vector3(0.2f, 0.06f, 0.56f), new Vector3(0.12f, -0.34f, 0.62f), new Vector3(-95f, 0f, 0f), new Vector3(55f, 0f, 0f), 0.58f, 1.7f, 0.5f),
    };
    static readonly Strike SwingHeavy =
        new Strike(false, new Vector3(0.3f, 0.16f, 0.12f), new Vector3(0.24f, 0.12f, 0.56f), new Vector3(0.06f, -0.38f, 0.66f), new Vector3(-110f, 0f, 0f), new Vector3(70f, 0f, 0f), 0.8f, 2.5f, 0.7f);

    static readonly Strike[] ThrustCombo =
    {
        new Strike(false, new Vector3(0.3f, -0.24f, 0.14f), new Vector3(0.22f, -0.2f, 0.4f), new Vector3(0.12f, -0.16f, 0.72f), new Vector3(76f, -8f, 0f), new Vector3(84f, -2f, 0f), 0.4f, 1f, 0.6f),
        new Strike(false, new Vector3(0.3f, -0.2f, 0.14f), new Vector3(0.2f, -0.14f, 0.4f), new Vector3(0.08f, -0.08f, 0.74f), new Vector3(70f, -8f, 0f), new Vector3(78f, 0f, 0f), 0.4f, 1.2f, 0.6f),
        new Strike(false, new Vector3(0.34f, -0.12f, 0.06f), new Vector3(0.24f, -0.12f, 0.4f), new Vector3(0.08f, -0.14f, 0.8f), new Vector3(80f, -10f, 0f), new Vector3(88f, 0f, 0f), 0.55f, 1.7f, 0.9f),
    };
    static readonly Strike ThrustHeavy =
        new Strike(false, new Vector3(0.36f, -0.08f, 0f), new Vector3(0.26f, -0.1f, 0.4f), new Vector3(0.06f, -0.12f, 0.84f), new Vector3(78f, -12f, 0f), new Vector3(90f, 0f, 0f), 0.75f, 2.5f, 1.1f);

    GameObject weaponModel;
    WeaponDef weapon;
    float lastHit = -10f, busyUntil, guard;
    bool swinging;
    int combo;

    void Start()
    {
        Aim = new GameObject("Aim").transform;
        Aim.SetParent(transform, false);
        Aim.localPosition = new Vector3(0f, 1.65f, 0f);

        RightTarget = new GameObject("RightHandTarget").transform;
        RightTarget.SetParent(Aim, false);
        RightTarget.localPosition = FistRestR;
        LeftTarget = new GameObject("LeftHandTarget").transform;
        LeftTarget.SetParent(Aim, false);
        LeftTarget.localPosition = FistRestL;

        Items.OnChanged += RefreshWeapon;
        RefreshWeapon();
    }

    void OnDestroy() { Items.OnChanged -= RefreshWeapon; }

    Vector3 RestRot => weapon == null ? Z : weapon.thrust ? SpearRestRot : ArmedRestRot;

    void Update()
    {
        if (Aim == null) return;

        // The hands follow the view up and down, but not all the way (so the arms never fold over the head).
        var pc = PlayerController.Instance;
        float pitch = pc != null ? Mathf.Clamp(pc.Pitch, -50f, 50f) : 0f;
        Aim.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (GameState.Frozen) return;

        // Hands come up when armed or for a few seconds after a strike, and drop back to the walk cycle after.
        bool onRope = pc != null && pc.Climbing;
        bool up = !onRope && (Armed || swinging || Time.time - lastHit < 4f);
        guard = Mathf.MoveTowards(guard, up ? 1f : 0f, Time.deltaTime * (up ? 7f : 2.5f));
        RightWeight = guard;
        LeftWeight = Armed ? 0f : guard;

        if (!swinging) Idle();

        if (ComboStep != 0 && Time.time - lastHit > comboWindow)
        {
            combo = 0;
            ComboStep = 0;
        }

        if (onRope || GameClock.Sleeping) return;
        if (Inp.Attack()) Swing(false);
        else if (Inp.Heavy()) Swing(true);
    }

    void Idle()
    {
        float breathe = Mathf.Sin(Time.time * 2.2f) * 0.008f;
        float bob = Mathf.Sin(Time.time * 9f) * 0.012f * Inp.Move().magnitude;
        float k = 12f * Time.deltaTime;
        Vector3 r = (Armed ? ArmedRest : FistRestR) + new Vector3(0f, bob + breathe, 0f);
        Vector3 l = FistRestL + new Vector3(0f, -bob + breathe, 0f);
        RightTarget.localPosition = Vector3.Lerp(RightTarget.localPosition, r, k);
        LeftTarget.localPosition = Vector3.Lerp(LeftTarget.localPosition, l, k);
        RightTarget.localRotation = Quaternion.Slerp(RightTarget.localRotation, Quaternion.Euler(RestRot), k);
        Twist = Mathf.Lerp(Twist, 0f, k);
        Lean = Mathf.Lerp(Lean, 0f, k);
        RightExtend = Mathf.Lerp(RightExtend, 0f, k);
        LeftExtend = Mathf.Lerp(LeftExtend, 0f, k);
    }

    /// <summary>Starts a strike if the last one has finished. Public so tests can trigger it.</summary>
    public void Swing(bool heavy)
    {
        float now = Time.time;
        if (now < busyUntil || Aim == null) return;

        int step = heavy ? 2 : combo;
        Strike s;
        if (weapon == null) s = heavy ? FistHeavy : FistCombo[step];
        else if (weapon.thrust) s = heavy ? ThrustHeavy : ThrustCombo[step];
        else s = heavy ? SwingHeavy : SwingCombo[step];

        float speed = weapon != null ? weapon.speed : 1f;
        float dur = s.dur / speed;
        float dmg = (weapon != null ? weapon.damage : fistDamage) * s.mult * Characters.Current.damageMult;
        float reach = weapon != null ? weapon.reach : fistReach;

        busyUntil = now + dur * 0.92f;   // the next strike may start just before this one has fully settled
        lastHit = now;
        ComboStep = heavy ? 0 : combo + 1;
        combo = heavy ? 0 : (combo + 1) % 3;
        Tutorial.Notify("attack");

        StartCoroutine(Animate(s, dur, heavy));
        StartCoroutine(HitAfter(dur * 0.42f, dmg, reach, heavy));
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    IEnumerator Animate(Strike s, float dur, bool heavy)
    {
        swinging = true;
        guard = Mathf.Max(guard, 0.7f);
        bool armed = weapon != null;
        Transform hand = s.left ? LeftTarget : RightTarget;
        Transform other = s.left ? RightTarget : LeftTarget;
        Vector3 rest = s.left ? FistRestL : (armed ? ArmedRest : FistRestR);
        Vector3 otherRest = s.left ? FistRestR : FistRestL;
        Quaternion restRot = Quaternion.Euler(RestRot);
        Quaternion rotA = Quaternion.Euler(s.rotWindup), rotB = Quaternion.Euler(s.rotHit);
        Vector3 startPos = hand.localPosition;
        Quaternion startRot = hand.localRotation;
        bool kicked = false;
        float side = s.left ? -1f : 1f;

        // Phases, as fractions of the strike: load, snap out, a beat at full reach, then back to the guard.
        const float load = 0.22f, snap = 0.2f, hold = 0.08f;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / dur);
            float ext, twist, lean;

            if (t < load)
            {
                float u = Mathf.SmoothStep(0f, 1f, t / load);
                hand.localPosition = Vector3.Lerp(startPos, s.windup, u);
                if (armed) hand.localRotation = Quaternion.Slerp(startRot, rotA, u);
                ext = 0f; twist = -0.35f * u; lean = -0.15f * u;
            }
            else if (t < load + snap)
            {
                float u = (t - load) / snap;
                u = 1f - (1f - u) * (1f - u);   // fast start, eases into the target
                hand.localPosition = Bezier(s.windup, s.via, s.hit, u);
                if (armed) hand.localRotation = Quaternion.Slerp(rotA, rotB, u);
                ext = u; twist = Mathf.Lerp(-0.35f, 1f, u); lean = Mathf.Lerp(-0.15f, 1f, u);
                if (!kicked && u > 0.7f) { kicked = true; CameraFeel.Kick(heavy ? 4f : 1.5f); }
            }
            else if (t < load + snap + hold)
            {
                hand.localPosition = s.hit;
                if (armed) hand.localRotation = rotB;
                ext = 1f; twist = 1f; lean = 1f;
            }
            else
            {
                float u = Mathf.SmoothStep(0f, 1f, (t - load - snap - hold) / (1f - load - snap - hold));
                hand.localPosition = Vector3.Lerp(s.hit, rest, u);
                if (armed) hand.localRotation = Quaternion.Slerp(rotB, restRot, u);
                ext = 1f - u; twist = 1f - u; lean = 1f - u;
            }

            // The other hand tucks in to cover the chin while this one is out.
            if (!armed) other.localPosition = Vector3.Lerp(otherRest, otherRest + new Vector3(-side * -0.04f, 0.04f, -0.08f), ext);

            if (s.left) LeftExtend = ext; else RightExtend = ext;
            Twist = side * twist * s.twist;
            Lean = Mathf.Max(0f, lean) * (heavy ? 1f : 0.6f);
            yield return null;
        }
        swinging = false;
    }

    IEnumerator HitAfter(float delay, float dmg, float reach, bool heavy)
    {
        yield return new WaitForSeconds(delay);
        if (Aim == null) yield break;

        Vector3 center = Aim.position + Aim.forward * (reach * 0.55f);
        var hitHealth = new HashSet<Health>();
        ResourceNode node = null;
        float nodeDist = float.MaxValue;
        bool landed = false;

        foreach (var col in Physics.OverlapSphere(center, reach * 0.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (col.transform.IsChildOf(transform)) continue;

            var h = col.GetComponentInParent<Health>();
            if (h != null)
            {
                if (h.Dead || hitHealth.Contains(h) || h.GetComponent<BaseStructure>() != null) continue;   // not your own builds
                hitHealth.Add(h);
                h.TakeDamage(dmg, heavy ? "heavy strike" : "strike");
                if (h.Dead) Progress.Gain(Mathf.Max(3, Mathf.RoundToInt(h.max / 5f)));
                landed = true;
                continue;
            }

            var n = col.GetComponentInParent<ResourceNode>();
            if (n != null && !n.Spent)
            {
                float d = Vector3.Distance(col.ClosestPoint(Aim.position), Aim.position);
                if (d < nodeDist) { node = n; nodeDist = d; }
            }
        }

        // Only living targets if any were hit; otherwise the nearest thing that can be harvested.
        if (!landed && node != null)
        {
            string got = node.Hit(weapon);
            if (heavy && !node.Spent) got = Join(got, node.Hit(weapon));
            Hud.Toast(got);
            landed = true;
        }
        if (landed) CameraFeel.Shake(heavy ? 0.3f : 0.14f);
    }

    static string Join(string a, string b) { return string.IsNullOrEmpty(b) ? a : a + "   " + b; }

    void RefreshWeapon()
    {
        if (RightTarget == null) return;
        var want = Items.Equipped;
        if (want == weapon) return;

        weapon = want;
        if (weaponModel != null) Destroy(weaponModel);
        weaponModel = null;
        if (want == null) return;

        // Whatever way the model was built, stand it up along its longest side, size it to the weapon's
        // length, and hold it "grip" metres from the bottom end.
        weaponModel = ModelLibrary.Spawn(want.model, RightTarget, Vector3.zero, 0f, keepAnimations: false);
        if (weaponModel == null) return;
        var lb = ModelLibrary.LocalBounds(weaponModel.transform);
        Quaternion up = Quaternion.identity;
        float longest = lb.size.y;
        Vector3 foot = new Vector3(lb.center.x, lb.min.y, lb.center.z);
        if (lb.size.z > longest && lb.size.z >= lb.size.x)
        {
            longest = lb.size.z;
            up = Quaternion.Euler(-90f, 0f, 0f);   // model +Z becomes up
            foot = new Vector3(lb.center.x, lb.center.y, lb.min.z);
        }
        else if (lb.size.x > longest)
        {
            longest = lb.size.x;
            up = Quaternion.Euler(0f, 0f, 90f);    // model +X becomes up
            foot = new Vector3(lb.min.x, lb.center.y, lb.center.z);
        }
        if (want.flip) { up = Quaternion.Euler(180f, 0f, 0f) * up; foot = lb.center * 2f - foot; }
        float scale = want.length / Mathf.Max(0.01f, longest);
        Quaternion rot = Quaternion.Euler(want.euler) * up;
        weaponModel.transform.localScale = Vector3.one * scale;
        weaponModel.transform.localRotation = rot;
        weaponModel.transform.localPosition = -(rot * (foot * scale)) - Quaternion.Euler(want.euler) * new Vector3(0f, want.grip, 0f);
        foreach (var c in weaponModel.GetComponentsInChildren<Collider>()) Destroy(c);
        RightTarget.localRotation = Quaternion.Euler(RestRot);
    }
}
