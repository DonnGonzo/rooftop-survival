using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct NightReport
{
    public int night;
    public bool attacked;
    public int raiders;
    public float threat;
    public float defense;
}

/// <summary>
/// Decides what each night brings. Nights 1-10 are always quiet. From night 11 a raid may come:
/// creatures climb over the parapet and go for you, or for what you built if you are not there.
/// Traps hurt them, barricades hold them up, and your defense total thins their numbers before they arrive.
/// Kill them all, or last until dawn. If you go down during a raid the base is overrun and the run is lost.
/// </summary>
public class NightDirector : MonoBehaviour
{
    public static NightDirector Instance { get; private set; }
    public static event Action<NightReport> OnNightResolved;

    /// <summary>True while raiders are on the roof.</summary>
    public static bool RaidActive { get; private set; }
    public static int RaidersLeft => live.Count;

    [Header("Attack rules")]
    public int firstAttackNight = 11;
    public float baseAttackChance = 0.45f;
    public float maxAttackChance = 0.9f;
    [Tooltip("Nights after the first attack night over which the chance ramps to max.")]
    public int rampNights = 25;

    [Header("Threat")]
    public float baseThreat = 30f;
    public float threatPerNight = 7f;
    [Range(0f, 0.5f)] public float threatVariance = 0.2f;
    [Tooltip("Threat one raider is worth.")]
    public float threatPerRaider = 12f;
    [Tooltip("How much of your defense total is taken off the threat before raiders are counted.")]
    public float defenseWeight = 0.5f;

    static readonly List<AnimalAI> live = new List<AnimalAI>();
    NightReport current;

    void Awake() { Instance = this; }

    void OnEnable()
    {
        GameClock.OnNightFall += HandleNight;
        GameClock.OnDawn += HandleDawn;
    }

    void OnDisable()
    {
        GameClock.OnNightFall -= HandleNight;
        GameClock.OnDawn -= HandleDawn;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { RaidActive = false; live.Clear(); }

    void HandleNight(int night)
    {
        var report = new NightReport { night = night, defense = BaseStructure.TotalDefense() };

        if (night < firstAttackNight)
        {
            OnNightResolved?.Invoke(report);
            return;
        }

        float ramp = Mathf.InverseLerp(firstAttackNight, firstAttackNight + rampNights, night);
        float chance = Mathf.Lerp(baseAttackChance, maxAttackChance, ramp);
        if (UnityEngine.Random.value > chance)
        {
            OnNightResolved?.Invoke(report);
            return;
        }

        float variance = 1f + UnityEngine.Random.Range(-threatVariance, threatVariance);
        float threat = (baseThreat + (night - firstAttackNight) * threatPerNight) * variance;
        StartRaid(night, threat);
    }

    /// <summary>Starts a raid now. Public so it can be triggered for testing.</summary>
    public void StartRaid(int night, float threat)
    {
        if (RaidActive) return;
        float defense = BaseStructure.TotalDefense();
        int count = Mathf.Clamp(Mathf.RoundToInt((threat - defense * defenseWeight) / threatPerRaider), 2, 16);
        current = new NightReport { night = night, attacked = true, threat = threat, defense = defense, raiders = count };
        RaidActive = true;
        wavesDone = false;
        Hud.Banner("RAID! " + count + " creatures are climbing the building.", 6f);
        Sfx.Play("raid_horn");
        GameClock.WakeUp();
        StartCoroutine(SpawnWaves(night, count));
    }

    IEnumerator SpawnWaves(int night, int count)
    {
        Vector2 size = RoofGenerator.Size;
        float hx = size.x / 2f - 1.2f, hz = size.y / 2f - 1.2f;
        yield return new WaitForSeconds(4f);

        for (int i = 0; i < count; i++)
        {
            while (GameState.Frozen) yield return null;

            // Later nights bring tougher kinds.
            int tier = night >= firstAttackNight + 8 ? 4 : night >= firstAttackNight + 3 ? 3 : 2;
            var sp = Wildlife.Raiders[UnityEngine.Random.Range(0, Mathf.Min(tier, Wildlife.Raiders.Length))];

            // A point on the roof edge.
            float t = UnityEngine.Random.Range(-1f, 1f);
            Vector3 pos;
            switch (UnityEngine.Random.Range(0, 4))
            {
                case 0: pos = new Vector3(t * hx, 0f, hz); break;
                case 1: pos = new Vector3(t * hx, 0f, -hz); break;
                case 2: pos = new Vector3(hx, 0f, t * hz); break;
                default: pos = new Vector3(-hx, 0f, t * hz); break;
            }

            var ai = Wildlife.Spawn(sp, null, pos, 0f);
            if (ai != null)
            {
                ai.stayWithin = new Vector2(hx, hz);
                ai.roamRadius = 8f;
                live.Add(ai);
                StartCoroutine(ClimbIn(ai.transform));
            }
            yield return new WaitForSeconds(UnityEngine.Random.Range(1.5f, 4f));
        }
        wavesDone = true;
    }

    /// <summary>The raider rises over the parapet instead of popping into view.</summary>
    static IEnumerator ClimbIn(Transform t)
    {
        if (t.childCount == 0) yield break;
        var vis = t.GetChild(0);
        Vector3 rest = vis.localPosition;
        Vector3 low = rest + Vector3.down * (2.2f / Mathf.Max(0.05f, t.localScale.y));
        float k = 0f;
        while (k < 1f && vis != null)
        {
            k += Time.deltaTime / 0.9f;
            vis.localPosition = Vector3.Lerp(low, rest, Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }
        if (vis != null) vis.localPosition = rest;
    }

    void Update()
    {
        if (!RaidActive) return;
        live.RemoveAll(a => a == null || a.Hp == null || a.Hp.Dead);
        if (wavesDone && live.Count == 0) EndRaid(true);
    }

    bool wavesDone;

    void HandleDawn(int day)
    {
        if (RaidActive) EndRaid(false);
    }

    void EndRaid(bool allKilled)
    {
        RaidActive = false;
        StopAllCoroutines();
        foreach (var a in live) if (a != null) Destroy(a.gameObject);
        live.Clear();
        wavesDone = false;

        Hud.Banner(allKilled ? "Raid repelled. The roof is yours until tomorrow." : "Dawn. The creatures slink back down the walls.", 6f);
        OnNightResolved?.Invoke(current);
        current = default;
        if (RunManager.Instance != null) RunManager.Instance.Save();
    }

    /// <summary>Called when the player goes down. During a raid that loses the run.</summary>
    public static bool PlayerFell()
    {
        if (!RaidActive) return false;
        RaidActive = false;
        int night = Instance != null ? Instance.current.night : 0;
        Hud.Banner("BASE OVERRUN on night " + night + ". Everything is lost.", 8f);
        if (RunManager.Instance != null) RunManager.Instance.LoseRun(Mathf.Max(0, night - 1));
        return true;
    }
}
