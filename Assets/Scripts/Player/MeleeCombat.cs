using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Melee with a three-hit combo. Attack chains light hits (1, 2, then a finisher); Heavy is one big strike.
/// Bare hands: right jab, left cross, right hook. With a club: two side swings and an overhead smash.
///
/// This script only decides WHERE the hands should be. It moves two targets in front of the player's eyes
/// (RightTarget, LeftTarget); PlayerBody bends the character's real arms to reach them, on top of whatever
/// walk/run/idle clip is playing. The weapon rides on the right target, so the hand is always on the grip.
/// </summary>
public class MeleeCombat : MonoBehaviour
{
    public Transform cam;
    public float comboWindow = 0.8f;
    public float reach = 1.9f;
    public float fistDamage = 8f;
    public float clubDamage = 25f;
    [Tooltip("Rotation of the club model in the hand. Adjust if it points the wrong way.")]
    public Vector3 weaponEuler = new Vector3(90f, 0f, 0f);

    /// <summary>0 = no chain, 1..3 = hits in the current chain (shown on the HUD).</summary>
    public static int ComboStep { get; private set; }

    public bool IsSwinging => swinging;
    /// <summary>0..1 progress through the current strike (0 when idle).</summary>
    public float SwingPhase { get; private set; }
    public bool Armed => weaponId != null;

    /// <summary>Eye-height frame that pitches with the view. Hand targets live under it.</summary>
    public Transform Aim { get; private set; }
    public Transform RightTarget { get; private set; }
    public Transform LeftTarget { get; private set; }
    /// <summary>How strongly each arm follows its target (0 = the locomotion clip owns the arm).</summary>
    public float RightWeight { get; private set; }
    public float LeftWeight { get; private set; }
    /// <summary>-1..1: how far the torso turns into the current strike (positive = turning left).</summary>
    public float Twist { get; private set; }

    class Strike
    {
        public bool left;
        public Vector3 windup, hit;        // hand positions in the Aim frame
        public Vector3 rotWindup, rotHit;  // weapon rotation (ignored for fists)
        public float dur, mult;
        public Strike(bool left, Vector3 windup, Vector3 hit, Vector3 rotWindup, Vector3 rotHit, float dur, float mult)
        { this.left = left; this.windup = windup; this.hit = hit; this.rotWindup = rotWindup; this.rotHit = rotHit; this.dur = dur; this.mult = mult; }
    }

    static readonly Vector3 FistRestR = new Vector3(0.17f, -0.17f, 0.3f);
    static readonly Vector3 FistRestL = new Vector3(-0.17f, -0.17f, 0.28f);
    static readonly Vector3 ClubRest = new Vector3(0.27f, -0.25f, 0.4f);
    static readonly Vector3 ClubRestRot = new Vector3(-12f, 0f, 0f);

    static readonly Strike[] FistCombo =
    {
        new Strike(false, new Vector3(0.22f, -0.2f, 0.26f), new Vector3(0.04f, -0.09f, 0.6f), Vector3.zero, Vector3.zero, 0.3f, 1f),
        new Strike(true,  new Vector3(-0.22f, -0.2f, 0.26f), new Vector3(-0.04f, -0.09f, 0.6f), Vector3.zero, Vector3.zero, 0.3f, 1.2f),
        new Strike(false, new Vector3(0.42f, -0.12f, 0.22f), new Vector3(-0.06f, -0.08f, 0.6f), Vector3.zero, Vector3.zero, 0.45f, 1.7f),
    };
    static readonly Strike FistHeavy =
        new Strike(false, new Vector3(0.36f, -0.02f, 0.12f), new Vector3(0f, -0.1f, 0.72f), Vector3.zero, Vector3.zero, 0.62f, 2.5f);

    static readonly Strike[] ClubCombo =
    {
        new Strike(false, new Vector3(0.44f, -0.14f, 0.36f), new Vector3(-0.14f, -0.2f, 0.56f), new Vector3(-15f, -60f, 0f), new Vector3(-15f, 60f, 0f), 0.38f, 1f),
        new Strike(false, new Vector3(-0.1f, -0.14f, 0.42f), new Vector3(0.42f, -0.2f, 0.5f), new Vector3(-15f, 60f, 0f), new Vector3(-15f, -60f, 0f), 0.38f, 1.2f),
        new Strike(false, new Vector3(0.26f, 0.06f, 0.26f), new Vector3(0.14f, -0.32f, 0.64f), new Vector3(-80f, 0f, 0f), new Vector3(50f, 0f, 0f), 0.55f, 1.7f),
    };
    static readonly Strike ClubHeavy =
        new Strike(false, new Vector3(0.3f, 0.12f, 0.18f), new Vector3(0.08f, -0.36f, 0.68f), new Vector3(-95f, 0f, 0f), new Vector3(65f, 0f, 0f), 0.75f, 2.5f);

    GameObject weapon;
    string weaponId;
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

    void Update()
    {
        if (Aim == null) return;

        // The hands follow the view up and down, but not all the way (so the arms never fold over the head).
        var pc = PlayerController.Instance;
        float pitch = pc != null ? Mathf.Clamp(pc.Pitch, -50f, 50f) : 0f;
        Aim.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (GameState.Frozen) return;

        // Hands come up when armed or for a few seconds after a strike, and drop back to the walk cycle after.
        bool up = Armed || swinging || Time.time - lastHit < 4f;
        guard = Mathf.MoveTowards(guard, up ? 1f : 0f, Time.deltaTime * (up ? 7f : 2.5f));
        RightWeight = guard;
        LeftWeight = Armed ? 0f : guard;

        if (!swinging) Idle();

        if (ComboStep != 0 && Time.time - lastHit > comboWindow)
        {
            combo = 0;
            ComboStep = 0;
        }

        if (Inp.Attack()) Swing(false);
        else if (Inp.Heavy()) Swing(true);
    }

    void Idle()
    {
        float sway = Mathf.Sin(Time.time * 2.2f) * 0.008f;
        float bob = Mathf.Sin(Time.time * 9f) * 0.012f * Inp.Move().magnitude;
        float k = 12f * Time.deltaTime;
        Vector3 r = (Armed ? ClubRest : FistRestR) + new Vector3(0f, bob + sway, 0f);
        Vector3 l = FistRestL + new Vector3(0f, -bob + sway, 0f);
        RightTarget.localPosition = Vector3.Lerp(RightTarget.localPosition, r, k);
        LeftTarget.localPosition = Vector3.Lerp(LeftTarget.localPosition, l, k);
        RightTarget.localRotation = Quaternion.Slerp(RightTarget.localRotation, Quaternion.Euler(Armed ? ClubRestRot : Vector3.zero), k);
        Twist = Mathf.Lerp(Twist, 0f, k);
    }

    /// <summary>Starts a strike if the last one has finished. Public so tests and AI can trigger it.</summary>
    public void Swing(bool heavy)
    {
        float now = Time.time;
        if (now < busyUntil || Aim == null) return;

        int step = heavy ? 2 : combo;
        Strike s = Armed ? (heavy ? ClubHeavy : ClubCombo[step]) : (heavy ? FistHeavy : FistCombo[step]);
        float baseDmg = Armed ? clubDamage : fistDamage;
        float dmg = baseDmg * s.mult * Characters.Current.damageMult;

        busyUntil = now + s.dur;
        lastHit = now;
        ComboStep = heavy ? 0 : combo + 1;
        combo = heavy ? 0 : (combo + 1) % 3;

        StartCoroutine(Animate(s, heavy));
        StartCoroutine(HitAfter(s.dur * 0.5f, dmg, heavy));
    }

    IEnumerator Animate(Strike s, bool heavy)
    {
        swinging = true;
        guard = Mathf.Max(guard, 0.6f);
        Transform hand = s.left ? LeftTarget : RightTarget;
        Vector3 rest = s.left ? FistRestL : (Armed ? ClubRest : FistRestR);
        Quaternion restRot = Quaternion.Euler(Armed ? ClubRestRot : Vector3.zero);
        Quaternion rotA = Quaternion.Euler(s.rotWindup), rotB = Quaternion.Euler(s.rotHit);
        Vector3 startPos = hand.localPosition;
        Quaternion startRot = hand.localRotation;
        bool kicked = false;
        float side = s.left ? -1f : 1f;

        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / s.dur);
            SwingPhase = t;

            if (t < 0.28f)
            {
                // wind up: pull the hand back
                float u = Mathf.SmoothStep(0f, 1f, t / 0.28f);
                hand.localPosition = Vector3.Lerp(startPos, s.windup, u);
                if (Armed) hand.localRotation = Quaternion.Slerp(startRot, rotA, u);
                Twist = -side * u * 0.5f;
            }
            else if (t < 0.58f)
            {
                // strike: fast out
                float u = (t - 0.28f) / 0.3f;
                u = 1f - Mathf.Pow(1f - u, 3f);
                hand.localPosition = Vector3.Lerp(s.windup, s.hit, u);
                if (Armed) hand.localRotation = Quaternion.Slerp(rotA, rotB, u);
                Twist = side * Mathf.Lerp(-0.5f, 1f, u);
                if (!kicked && u > 0.6f) { kicked = true; CameraFeel.Kick(heavy ? 5f : 2f); }
            }
            else
            {
                // recover to the guard
                float u = Mathf.SmoothStep(0f, 1f, (t - 0.58f) / 0.42f);
                hand.localPosition = Vector3.Lerp(s.hit, rest, u);
                if (Armed) hand.localRotation = Quaternion.Slerp(rotB, restRot, u);
                Twist = side * (1f - u);
            }
            yield return null;
        }
        swinging = false;
        SwingPhase = 0f;
    }

    IEnumerator HitAfter(float delay, float dmg, bool heavy)
    {
        yield return new WaitForSeconds(delay);
        if (Aim == null) yield break;

        Vector3 center = Aim.position + Aim.forward * (reach * 0.8f);
        var hit = new HashSet<Health>();
        foreach (var col in Physics.OverlapSphere(center, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            var h = col.GetComponentInParent<Health>();
            if (h == null || h.Dead || hit.Contains(h)) continue;
            if (h.GetComponent<Trap>() != null) continue;   // don't smash your own traps
            hit.Add(h);
            h.TakeDamage(dmg, heavy ? "heavy swing" : "attack");
            Tutorial.Notify("attack");
            CameraFeel.Shake(heavy ? 0.35f : 0.15f);
            Hud.Toast("Hit for " + dmg.ToString("F0") + (heavy ? " (heavy)" : "") + ".");
        }
    }

    void RefreshWeapon()
    {
        if (RightTarget == null) return;
        string want = Items.Count("club") > 0 ? "club" : null;
        if (want == weaponId) return;

        weaponId = want;
        if (weapon != null) Destroy(weapon);
        weapon = null;

        if (want != null)
        {
            weapon = ModelLibrary.Spawn("hammer", RightTarget, Vector3.zero, 0f, fitHeight: 0.75f, keepAnimations: false);
            if (weapon != null)
            {
                weapon.transform.localRotation = Quaternion.Euler(weaponEuler);
                // hold it near the bottom of the handle, not the middle
                weapon.transform.localPosition = weapon.transform.localRotation * new Vector3(0f, -0.12f, 0f);
            }
        }
    }
}
