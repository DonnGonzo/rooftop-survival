using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Your standing with the Drifters, the other people still alive out here. It runs from -100 to 100 and is
/// saved with the run. Help them and it rises; hit them, kill them or go through their supplies and it falls.
/// Below -25 they attack on sight. Above 25 they share what they have. A grudge fades a little each dawn.
/// </summary>
public static class Faction
{
    static RunData Run => RunManager.Instance != null ? RunManager.Instance.Current : null;

    public static int Reputation => Run != null ? Run.reputation : 0;
    public static bool Hostile => Reputation <= -25;
    public static bool Friendly => Reputation >= 25;
    public static bool Allied => Reputation >= 60;

    public static string Standing
    {
        get
        {
            int r = Reputation;
            return r <= -60 ? "Hunted" : r <= -25 ? "Hostile" : r < 10 ? "Wary" : r < 25 ? "Neutral" : r < 60 ? "Friendly" : "Allied";
        }
    }

    public static Color StandingColor => Hostile ? new Color(1f, 0.45f, 0.4f) : Friendly ? new Color(0.55f, 0.95f, 0.55f) : new Color(0.95f, 0.9f, 0.6f);

    public static void Change(int amount, string why)
    {
        var run = Run;
        if (run == null || amount == 0) return;
        string before = Standing;
        run.reputation = Mathf.Clamp(run.reputation + amount, -100, 100);
        Hud.Toast("Drifters " + (amount > 0 ? "+" : "") + amount + ": " + why);
        if (Standing != before) Hud.Banner("The Drifters now see you as: " + Standing.ToUpper(), 5f);
    }

    /// <summary>Each dawn a grudge eases a little. Goodwill has to be kept up by deeds, not time.</summary>
    public static void Dawn()
    {
        var run = Run;
        if (run != null && run.reputation < 0) run.reputation = Mathf.Min(0, run.reputation + 4);
    }
}

/// <summary>One Drifter. The AnimalAI on the same object moves and fights; this is the talking part.</summary>
public class Survivor : Interactable
{
    public static readonly List<Survivor> All = new List<Survivor>();

    public Encounter group;
    public bool female;
    public Health Hp { get; private set; }

    string V(string line) { return (female ? "v_f_" : "v_m_") + line; }

    protected override void OnEnable() { base.OnEnable(); All.Add(this); }
    protected override void OnDisable() { base.OnDisable(); All.Remove(this); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    void Awake()
    {
        Hp = GetComponent<Health>();
        range = 3f;
    }

    void Start()
    {
        if (Hp == null) Hp = GetComponent<Health>();
        Hp.label = () => "Drifter  -  " + Faction.Standing;
        Hp.OnHurt += Hurt;
        Hp.OnDeath += () => { if (lastHitByPlayer) Faction.Change(-25, "you killed one of them"); };
    }

    bool lastHitByPlayer;

    void Hurt(string source)
    {
        lastHitByPlayer = source == "strike" || source == "heavy strike";
        if (!lastHitByPlayer) return;
        Faction.Change(-15, "you attacked one of them");
        Say(Faction.Hostile ? "hostile" : "warn");
    }

    public void Say(string line) { Sfx.PlayAt(V(line), transform.position + Vector3.up * 1.6f, 1f, 45f); }

    public override string Prompt
    {
        get
        {
            if (Hp == null || Hp.Dead || Faction.Hostile || group == null) return "";
            if (group.UnderAttack) return "";
            if (Faction.Friendly && !group.gifted) return "Talk to the Drifter (they have something for you)";
            if (group.shares < 2 && Items.Count("food") > 0) return "Share 1 Rations with the Drifters";
            return "Talk to the Drifter";
        }
    }

    public override void Use()
    {
        if (group == null) return;
        if (Faction.Friendly && !group.gifted)
        {
            group.gifted = true;
            Say("gift");
            string[] gifts = Faction.Allied ? new[] { "bandage", "rope", "leather", "cooked_meat", "ingot" } : new[] { "food", "water", "cloth", "fiber", "scrap" };
            var sb = new System.Text.StringBuilder("They hand you: ");
            int kinds = Faction.Allied ? 3 : 2;
            for (int i = 0; i < kinds; i++)
            {
                string id = gifts[Random.Range(0, gifts.Length)];
                int n = Random.Range(1, 4);
                Items.Add(id, n);
                sb.Append(i > 0 ? ", " : "").Append(n).Append(' ').Append(Items.Name(id));
            }
            Hud.Banner(sb.ToString(), 5f);
            Sfx.Play("loot");
            return;
        }
        if (group.shares < 2 && Items.Count("food") > 0)
        {
            Items.Remove("food", 1);
            group.shares++;
            Faction.Change(6, "you shared your food");
            Say("thanks");
            return;
        }
        Say(Faction.Friendly ? "friend" : "greet");
        Hud.Toast(Faction.Friendly ? "\"Good to see a friendly face out here.\"" : "\"Easy. We don't want trouble.\"");
    }
}

/// <summary>One meeting with a group of Drifters: who is in it, what is attacking them, and how it went.</summary>
public class Encounter
{
    public Vector3 pos;
    public readonly List<Survivor> members = new List<Survivor>();
    public readonly List<AnimalAI> attackers = new List<AnimalAI>();
    public readonly List<GameObject> props = new List<GameObject>();
    public bool startedUnderAttack, playerHelped, resolved, gifted, calledOut;
    public int shares;
    public float endsAt;

    public bool UnderAttack
    {
        get
        {
            foreach (var a in attackers) if (a != null && a.Hp != null && !a.Hp.Dead) return true;
            return false;
        }
    }

    public int Alive
    {
        get
        {
            int n = 0;
            foreach (var m in members) if (m != null && m.Hp != null && !m.Hp.Dead) n++;
            return n;
        }
    }
}

/// <summary>
/// Brings the Drifters into the world. Now and then, by day, a small group makes camp somewhere out of
/// sight. Sometimes animals are already on them when you find them: drive the animals off while any Drifter
/// still lives and your standing rises sharply. Camps move on after a while.
/// </summary>
public class FactionDirector : MonoBehaviour
{
    public static FactionDirector Instance { get; private set; }
    public static Encounter Current { get; private set; }

    [Tooltip("Real seconds between camps appearing, least and most.")]
    public Vector2 gap = new Vector2(150f, 320f);
    [Range(0f, 1f)] public float attackedChance = 0.45f;
    public float campLasts = 420f;

    // Each camp is drawn from these without repeats. The last two are only stand-ins for when the
    // drifter-* files are not in the project.
    static readonly string[] Models = { "drifter-kim", "drifter-jill", "drifter-lara" };
    static readonly bool[] Female = { false, true, true };
    static readonly string[] Fallback = { "shopper-base", "teen-audience-member-base" };
    static readonly bool[] FallbackFemale = { true, false };

    float nextAt;

    void Awake() { Instance = this; Current = null; }
    void OnEnable() { GameClock.OnDawn += Dawn; }
    void OnDisable() { GameClock.OnDawn -= Dawn; }
    void Start() { nextAt = Time.time + Random.Range(60f, 140f); }

    void Dawn(int day) { Faction.Dawn(); }

    void Update()
    {
        if (GameState.Frozen) return;
        var clock = GameClock.Instance;
        var p = PlayerController.Instance;
        if (clock == null || p == null) return;

        if (Current == null)
        {
            if (Time.time >= nextAt && !clock.IsNight && clock.Hour < 15f) Begin(false);
            return;
        }

        var e = Current;
        float dist = Vector3.Distance(p.transform.position, e.pos);

        // Within earshot of a camp in trouble: they call out once.
        if (e.startedUnderAttack && !e.calledOut && e.UnderAttack && p.OnStreet && dist < 110f)
        {
            e.calledOut = true;
            foreach (var m in e.members) if (m != null && !m.Hp.Dead) { m.Say("help"); break; }
            Hud.Banner("Someone is shouting for help to the " + Compass(e.pos - p.transform.position) + ".", 6f);
        }

        if (e.startedUnderAttack && !e.resolved && !e.UnderAttack)
        {
            e.resolved = true;
            if (e.Alive > 0 && e.playerHelped)
            {
                Faction.Change(20, "you saved their camp");
                Progress.Gain(25);
                foreach (var m in e.members) if (m != null && !m.Hp.Dead) { m.Say("thanks"); break; }
            }
        }

        // Everyone dead, or time to move on (never while you are standing in the camp).
        if (e.Alive == 0 || (Time.time > e.endsAt && dist > 45f && !e.UnderAttack)) End();
    }

    static string Compass(Vector3 d)
    {
        float a = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        if (a < 0f) a += 360f;
        string[] names = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        return names[Mathf.RoundToInt(a / 45f) % 8];
    }

    /// <summary>Starts a camp now. forceAttack is for testing.</summary>
    public void Begin(bool forceAttack)
    {
        if (Current != null) End();
        var p = PlayerController.Instance;
        var clock = GameClock.Instance;
        Vector3 pp = p != null ? p.transform.position : Vector3.zero;

        // A road crossing or a spot on a forest track, not too near and not too far.
        Vector3 pos = Vector3.zero;
        for (int tries = 0; tries < 30; tries++)
        {
            float half = StreetBuilder.Pitch / 2f;
            float x = (Random.Range(-StreetBuilder.Rings, StreetBuilder.Rings) + 0.5f) * StreetBuilder.Pitch;
            float z = Random.value < 0.5f ? (Random.Range(-StreetBuilder.Rings, StreetBuilder.Rings) + 0.5f) * StreetBuilder.Pitch
                                          : (Random.value < 0.5f ? -1f : 1f) * Random.Range(StreetBuilder.CityHalf + 15f, StreetBuilder.CityHalf + 110f);
            pos = Random.value < 0.5f ? new Vector3(x, StreetBuilder.StreetY, z) : new Vector3(z, StreetBuilder.StreetY, x);
            if (Mathf.Abs(Mathf.Abs(pos.x) - half) > 1f && Mathf.Abs(Mathf.Abs(pos.z) - half) > 1f && Mathf.Abs(pos.x) > StreetBuilder.CityHalf) continue;
            float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(pp.x, pp.z));
            if (d > 55f && d < 170f) break;
        }

        var e = new Encounter { pos = pos, endsAt = Time.time + campLasts };
        Current = e;

        // The camp: a tent, a fire, their supplies and a flag.
        Camp(e, "dome-tent", pos + new Vector3(3.5f, 0f, 2f), 3f, true);
        var fire = Camp(e, "stone-ring-campfire", pos, 1.4f, false);
        Camp(e, "flag-pole", pos + new Vector3(-3.5f, 0f, 3f), 0f, false, 4.5f);
        var bag = Camp(e, "kit-bag", pos + new Vector3(2.2f, 0f, -2.4f), 0.9f, true);
        if (bag != null)
        {
            var c = Container.Add(bag, "Steal the Drifters' supplies", 2.5f, 0, new LootRoll("food", 2, 4), new LootRoll("cloth", 1, 3), new LootRoll("scrap", 1, 3), new LootRoll("bandage", 1, 2, 0.6f));
            c.onSearched = () =>
            {
                Faction.Change(-30, "you robbed their camp");
                foreach (var m in e.members) if (m != null && !m.Hp.Dead) { m.Say("hostile"); break; }
            };
        }

        // Two or three of them, never the same person twice.
        var cast = new List<string>(); var castFemale = new List<bool>();
        for (int i = 0; i < Models.Length; i++) if (ModelLibrary.Has(Models[i])) { cast.Add(Models[i]); castFemale.Add(Female[i]); }
        if (cast.Count < 2) for (int i = 0; i < Fallback.Length; i++) { cast.Add(Fallback[i]); castFemale.Add(FallbackFemale[i]); }
        for (int i = cast.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (cast[i], cast[j]) = (cast[j], cast[i]); (castFemale[i], castFemale[j]) = (castFemale[j], castFemale[i]);
        }
        int count = Mathf.Min(cast.Count, Random.Range(2, 4));
        for (int i = 0; i < count; i++)
        {
            int k = i;
            var sp = new Species(cast[k], AnimalMode.Survivor, 1.75f, 70f, 9f, 1.3f, 4.6f, 16f, "cloth", 2);
            Vector2 off = Random.insideUnitCircle.normalized * Random.Range(1.8f, 3.5f);
            var ai = Wildlife.Spawn(sp, transform, pos + new Vector3(off.x, 0f, off.y), StreetBuilder.StreetY);
            if (ai == null) continue;
            ai.roamRadius = 5f;
            ai.cry = "";
            ai.biteRange = 1.9f;
            var s = ai.gameObject.AddComponent<Survivor>();
            s.group = e;
            s.female = castFemale[k];
            e.members.Add(s);
        }

        // Sometimes they are already in trouble.
        int day = clock != null ? clock.Day : 1;
        if (forceAttack || Random.value < attackedChance)
        {
            e.startedUnderAttack = true;
            string kind = day >= 8 ? "lion-base" : day >= 5 ? "alien-hive-bug-base" : day >= 3 ? "wolf-base" : "farm-dog";
            int n = day >= 3 ? Random.Range(2, 4) : Random.Range(1, 3);
            for (int i = 0; i < n; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(9f, 14f);
                var ai = Wildlife.Spawn(Wildlife.Get(kind), transform, pos + new Vector3(off.x, 0f, off.y), StreetBuilder.StreetY);
                if (ai == null) continue;
                ai.huntsSurvivors = true;
                // They are a match for the camp, not a massacre: the fight lasts long enough to be joined.
                ai.biteDamage *= 0.45f;
                ai.Hp_OnHurtByPlayer(() => e.playerHelped = true);
                e.attackers.Add(ai);
            }
        }
    }

    GameObject Camp(Encounter e, string key, Vector3 pos, float footprint, bool collider, float height = 0f)
    {
        var go = StreetBuilder.Prop(key, pos, Random.Range(0f, 360f), height, footprint, collider, transform);
        if (go != null) e.props.Add(go);
        return go;
    }

    void End()
    {
        var e = Current;
        Current = null;
        if (e != null)
        {
            foreach (var m in e.members) if (m != null) Destroy(m.gameObject);
            foreach (var a in e.attackers) if (a != null && a.Hp != null && !a.Hp.Dead) a.huntsSurvivors = false;
            foreach (var g in e.props) if (g != null) Destroy(g);
        }
        nextAt = Time.time + Random.Range(gap.x, gap.y);
    }
}
