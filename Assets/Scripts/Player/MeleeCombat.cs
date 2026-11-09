using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Melee with a three-hit combo. Left click chains light hits (1, 2, then a finisher).
/// Keep clicking within comboWindow seconds to continue the chain. Right click is a heavy swing.
/// The swing is procedural: the weapon rotates through an arc, so no weapon clips are needed.
/// </summary>
public class MeleeCombat : MonoBehaviour
{
    public Transform cam;
    public float comboWindow = 0.75f;
    public float reach = 1.8f;
    public float fistDamage = 8f;
    public float clubDamage = 25f;
    [Tooltip("Rotation of the club model in the hand. Adjust if it points the wrong way.")]
    public Vector3 weaponEuler = new Vector3(90f, 0f, 0f);

    /// <summary>0 = no chain, 1..3 = hits in the current chain (shown on the HUD).</summary>
    public static int ComboStep { get; private set; }

    static readonly float[] DamageMult = { 1f, 1.2f, 1.7f };
    static readonly float[] Duration = { 0.32f, 0.32f, 0.5f };
    static readonly Vector3[] StartRot =
    {
        new Vector3(-15f, -55f, 0f), new Vector3(-15f, 55f, 0f), new Vector3(-75f, 0f, 0f)
    };
    static readonly Vector3[] EndRot =
    {
        new Vector3(-15f, 55f, 0f), new Vector3(-15f, -55f, 0f), new Vector3(45f, 0f, 0f)
    };

    readonly Vector3 restPos = new Vector3(0.32f, -0.28f, 0.55f);
    readonly Quaternion restRot = Quaternion.Euler(-10f, 0f, 0f);

    Transform pivot;
    GameObject weapon;
    string weaponId;
    float lastHit = -10f, busyUntil;
    bool swinging;
    int combo;

    void Start()
    {
        if (cam == null) return;
        pivot = new GameObject("WeaponPivot").transform;
        pivot.SetParent(cam, false);
        pivot.localPosition = restPos;
        pivot.localRotation = restRot;
        Items.OnChanged += RefreshWeapon;
        RefreshWeapon();
    }

    void OnDestroy() { Items.OnChanged -= RefreshWeapon; }

    void Update()
    {
        if (pivot == null || GameState.Frozen) return;

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
        float bob = Mathf.Sin(Time.time * 9f) * 0.012f * Inp.Move().magnitude;
        pivot.localPosition = Vector3.Lerp(pivot.localPosition, restPos + new Vector3(0f, bob, 0f), 10f * Time.deltaTime);
        pivot.localRotation = Quaternion.Slerp(pivot.localRotation, restRot, 10f * Time.deltaTime);
    }

    void Swing(bool heavy)
    {
        float now = Time.time;
        if (now < busyUntil) return;

        int step = heavy ? 2 : combo;
        float mult = heavy ? 2.5f : DamageMult[step];
        float dur = heavy ? 0.7f : Duration[step];
        float baseDmg = Items.Count("club") > 0 ? clubDamage : fistDamage;
        float dmg = baseDmg * mult * Characters.Current.damageMult;

        busyUntil = now + dur;
        lastHit = now;
        CameraFeel.Kick(heavy ? 6f : 2.5f);   // the view jolts with each swing
        ComboStep = heavy ? 0 : combo + 1;
        combo = heavy ? 0 : (combo + 1) % 3;

        Vector3 from = heavy ? new Vector3(-80f, 0f, 0f) : StartRot[step];
        Vector3 to = heavy ? new Vector3(60f, 0f, 0f) : EndRot[step];
        StartCoroutine(Animate(from, to, dur, heavy || step == 2));
        StartCoroutine(HitAfter(dur * 0.45f, dmg, heavy));
    }

    IEnumerator Animate(Vector3 fromEuler, Vector3 toEuler, float dur, bool lunge)
    {
        swinging = true;
        Quaternion a = Quaternion.Euler(fromEuler);
        Quaternion b = Quaternion.Euler(toEuler);
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Min(1f, t + Time.deltaTime / dur);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            pivot.localRotation = Quaternion.Slerp(a, b, eased);
            float push = lunge ? Mathf.Sin(t * Mathf.PI) * 0.25f : 0f;
            pivot.localPosition = restPos + new Vector3(0f, 0f, push);
            yield return null;
        }
        swinging = false;
    }

    IEnumerator HitAfter(float delay, float dmg, bool heavy)
    {
        yield return new WaitForSeconds(delay);
        if (cam == null) yield break;

        Vector3 center = cam.position + cam.forward * (reach * 0.8f);
        var hit = new HashSet<Health>();
        foreach (var col in Physics.OverlapSphere(center, reach, ~0, QueryTriggerInteraction.Ignore))
        {
            var h = col.GetComponentInParent<Health>();
            if (h == null || h.Dead || hit.Contains(h)) continue;
            hit.Add(h);
            h.TakeDamage(dmg, heavy ? "heavy swing" : "attack");
            Tutorial.Notify("attack");
            Hud.Toast("Hit for " + dmg.ToString("F0") + (heavy ? " (heavy)" : "") + ".");
        }
    }

    void RefreshWeapon()
    {
        if (pivot == null) return;
        string want = Items.Count("club") > 0 ? "club" : null;
        if (want == weaponId) return;

        weaponId = want;
        if (weapon != null) Destroy(weapon);
        weapon = null;

        if (want != null)
        {
            weapon = ModelLibrary.Spawn("hammer", pivot, Vector3.zero, 0f, fitHeight: 0.75f, keepAnimations: false);
            if (weapon != null) weapon.transform.localRotation = Quaternion.Euler(weaponEuler);
        }
    }
}
