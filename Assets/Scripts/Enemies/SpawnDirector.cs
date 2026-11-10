using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A place animals come from: a cave, a nest, a subway mouth, a meadow. Each has its own species, and each
/// species only starts appearing from a given day.
/// </summary>
public class Den
{
    public Vector3 pos;
    public string label;
    public bool danger;        // predators (red on the map) or game (green)
    public bool nightOnly;     // only spawns after dark; what it spawned leaves at dawn
    public bool discovered;    // shown on the map once the player has been near
    public string[] species;
    public int[] fromDay;      // first day each species can appear
    public int cap = 3;        // most alive at once from this den
    public readonly List<AnimalAI> live = new List<AnimalAI>();
}

/// <summary>
/// Paces the wildlife. Nothing is spawned up front. Every few seconds the director looks at how many
/// predators and how much game the world should hold right now, and if there are fewer it lets ONE den that
/// is out of the player's sight produce ONE animal. The predator budget starts small on day 1 and grows each
/// day; nights raise it and wake the night-only dens.
/// </summary>
public class SpawnDirector : MonoBehaviour
{
    public static readonly List<Den> Dens = new List<Den>();

    [Tooltip("Seconds between spawn checks.")]
    public float interval = 5f;
    [Tooltip("Predators allowed on day 1, and how many more each later day adds.")]
    public int predatorsDayOne = 2;
    public float predatorsPerDay = 2.5f;
    public int predatorsMax = 34;
    public int gameAnimals = 16;
    [Tooltip("Dens closer to the player than this stay quiet, so nothing appears in front of you.")]
    public float quietRadius = 45f;

    float next;
    bool filled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Dens.Clear(); }

    void OnEnable() { GameClock.OnDawn += Dawn; }
    void OnDisable() { GameClock.OnDawn -= Dawn; }
    void OnDestroy() { Dens.Clear(); }

    public static Den Add(Vector3 pos, string label, bool danger, int cap, bool nightOnly, params object[] speciesThenDay)
    {
        var d = new Den { pos = pos, label = label, danger = danger, cap = cap, nightOnly = nightOnly };
        int n = speciesThenDay.Length / 2;
        d.species = new string[n];
        d.fromDay = new int[n];
        for (int i = 0; i < n; i++)
        {
            d.species[i] = (string)speciesThenDay[i * 2];
            d.fromDay[i] = (int)speciesThenDay[i * 2 + 1];
        }
        Dens.Add(d);
        return d;
    }

    /// <summary>How many predators the world should hold on a given day and time.</summary>
    public int PredatorBudget(int day, bool night)
    {
        float b = Mathf.Min(predatorsMax, predatorsDayOne + (day - 1) * predatorsPerDay);
        return Mathf.RoundToInt(night ? b * 1.5f : b);
    }

    void Update()
    {
        if (GameState.Frozen || Time.time < next) return;
        next = Time.time + interval;

        var clock = GameClock.Instance;
        var player = PlayerController.Instance;
        if (clock == null || player == null) return;
        Vector3 pp = player.transform.position;

        int predators = 0, game = 0;
        foreach (var d in Dens)
        {
            d.live.RemoveAll(a => a == null || a.Hp == null || a.Hp.Dead);
            if (d.danger) predators += d.live.Count; else game += d.live.Count;
            if (!d.discovered && player.OnStreet && Vector3.Distance(pp, d.pos) < 40f)
            {
                d.discovered = true;
                Hud.Toast("Found: " + d.label + (d.danger ? " (danger)" : ""));
            }
        }

        bool night = clock.IsNight;
        int budget = PredatorBudget(clock.Day, night);

        // Loading into a world that should already be lived in: bring it most of the way up at once, out of sight.
        if (!filled)
        {
            filled = true;
            for (int i = predators; i < budget * 0.6f; i++) SpawnFrom(true, clock.Day, night, pp);
            for (int i = game; i < gameAnimals * 0.7f; i++) SpawnFrom(false, clock.Day, night, pp);
            return;
        }

        if (predators < budget) SpawnFrom(true, clock.Day, night, pp);
        if (game < gameAnimals) SpawnFrom(false, clock.Day, night, pp);
    }

    void SpawnFrom(bool danger, int day, bool night, Vector3 playerPos)
    {
        // Every den that could produce something right now.
        var ready = new List<Den>();
        foreach (var d in Dens)
        {
            if (d.danger != danger || d.live.Count >= d.cap) continue;
            if (d.nightOnly && !night) continue;
            if (Vector3.Distance(playerPos, d.pos) < quietRadius) continue;
            bool any = false;
            foreach (int from in d.fromDay) if (from <= day) { any = true; break; }
            if (any) ready.Add(d);
        }
        if (ready.Count == 0) return;
        var den = ready[Random.Range(0, ready.Count)];

        var choices = new List<string>();
        for (int i = 0; i < den.species.Length; i++) if (den.fromDay[i] <= day) choices.Add(den.species[i]);
        var sp = Wildlife.Get(choices[Random.Range(0, choices.Count)]);
        if (sp == null) return;

        Vector2 off = Random.insideUnitCircle.normalized * Random.Range(3f, 7f);
        var ai = Wildlife.Spawn(sp, transform, den.pos + new Vector3(off.x, 0f, off.y), StreetBuilder.StreetY);
        if (ai == null) return;
        ai.roamRadius = danger ? 34f : 22f;
        den.live.Add(ai);
    }

    /// <summary>Daylight sends the night creatures back where they came from.</summary>
    void Dawn(int day)
    {
        foreach (var d in Dens)
        {
            if (!d.nightOnly) continue;
            foreach (var a in d.live) if (a != null) Destroy(a.gameObject);
            d.live.Clear();
        }
    }
}
