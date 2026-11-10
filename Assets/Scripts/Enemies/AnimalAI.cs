using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AnimalMode
{
    Predator,   // wanders, chases and bites the player (and Drifters) on its level
    Prey,       // wanders, runs from the player; hunt it for food
    Raider,     // night attacker on the roof: goes for the player, or wrecks what you built
    Survivor    // a Drifter: fights off animals, and treats the player by their standing
}

/// <summary>What one species is like. Used by Wildlife.Spawn.</summary>
public class Species
{
    public string model;
    public AnimalMode mode;
    public float height;          // metres tall the model is fitted to
    public float health, damage, walk, run, aggro;
    public string drop;           // item dropped on death
    public int dropCount;
    public bool lurker;           // stays near where it spawned (shade at building bases)

    public Species(string model, AnimalMode mode, float height, float health, float damage, float walk, float run,
                   float aggro, string drop, int dropCount, bool lurker = false)
    {
        this.model = model; this.mode = mode; this.height = height; this.health = health; this.damage = damage;
        this.walk = walk; this.run = run; this.aggro = aggro; this.drop = drop; this.dropCount = dropCount; this.lurker = lurker;
    }
}

public static class Wildlife
{
    static readonly Species[] All =
    {
        // small and early
        new Species("rat-base", AnimalMode.Predator, 0.3f, 10f, 4f, 1.5f, 4.2f, 7f, "fiber", 1),
        new Species("red-fox", AnimalMode.Predator, 0.6f, 25f, 6f, 1.8f, 6.2f, 9f, "food", 1),
        new Species("farm-dog", AnimalMode.Predator, 0.7f, 30f, 8f, 1.8f, 5.8f, 12f, "food", 1),
        new Species("compsognathus-base", AnimalMode.Predator, 0.5f, 14f, 5f, 1.8f, 6.0f, 10f, "food", 1),
        new Species("frog-base", AnimalMode.Prey, 0.25f, 5f, 0f, 0.8f, 3.5f, 5f, "food", 1),
        // mid
        new Species("wolf-base", AnimalMode.Predator, 0.95f, 45f, 12f, 1.6f, 6.0f, 16f, "food", 2),
        new Species("garden-orb-weaver-base", AnimalMode.Predator, 0.7f, 35f, 10f, 1.5f, 5.0f, 11f, "fiber", 3),
        new Species("stone-centipede-base", AnimalMode.Predator, 0.45f, 30f, 9f, 1.4f, 4.2f, 9f, "scrap", 1),
        new Species("crocodile-base", AnimalMode.Predator, 0.6f, 90f, 20f, 1.0f, 4.0f, 9f, "food", 4),
        new Species("alien-hive-bug-base", AnimalMode.Predator, 1.0f, 40f, 14f, 1.4f, 4.8f, 12f, "scrap", 1),
        new Species("praying-mantis-base", AnimalMode.Predator, 1.5f, 50f, 12f, 1.5f, 4.8f, 11f, "fiber", 3),
        new Species("tarantula-base", AnimalMode.Predator, 0.8f, 60f, 15f, 1.5f, 5.2f, 12f, "fiber", 4),
        // big and late
        new Species("black-bear", AnimalMode.Predator, 1.45f, 110f, 24f, 1.3f, 5.0f, 12f, "food", 5),
        new Species("lion-base", AnimalMode.Predator, 1.25f, 80f, 18f, 1.7f, 6.4f, 16f, "food", 4),
        new Species("velociraptor-base", AnimalMode.Predator, 1.7f, 70f, 18f, 2.0f, 7.0f, 20f, "food", 3),
        new Species("tyrannosaurus-base", AnimalMode.Predator, 5.2f, 400f, 45f, 1.6f, 5.2f, 26f, "food", 12),
        // things that only walk at night
        new Species("flesh-eater-base", AnimalMode.Predator, 1.8f, 55f, 14f, 1.2f, 4.4f, 18f, "cloth", 1),
        new Species("undead-pirate-base", AnimalMode.Predator, 1.8f, 80f, 18f, 1.2f, 4.6f, 18f, "scrap", 2),
        new Species("ashen-assassin-base", AnimalMode.Predator, 1.8f, 70f, 22f, 1.6f, 6.0f, 20f, "cloth", 2),
        // game
        new Species("rabbit-base", AnimalMode.Prey, 0.35f, 8f, 0f, 1.2f, 6.5f, 9f, "food", 1),
        new Species("mountain-hare-base", AnimalMode.Prey, 0.4f, 8f, 0f, 1.2f, 6.8f, 9f, "food", 1),
        new Species("chicken-base", AnimalMode.Prey, 0.45f, 6f, 0f, 1.0f, 4.0f, 6f, "food", 1),
        new Species("forest-deer", AnimalMode.Prey, 1.5f, 35f, 0f, 1.5f, 7.5f, 16f, "food", 4),
        new Species("sheep-base", AnimalMode.Prey, 0.95f, 25f, 0f, 1.2f, 5.0f, 8f, "food", 3),
        new Species("pig-base", AnimalMode.Prey, 0.8f, 30f, 0f, 1.2f, 5.2f, 8f, "food", 3),
        new Species("goat-base", AnimalMode.Prey, 0.95f, 25f, 0f, 1.3f, 5.5f, 9f, "food", 3),
    };

    public static Species Get(string model)
    {
        foreach (var s in All) if (s.model == model) return s;
        return null;
    }

    public static readonly Species[] Raiders =
    {
        new Species("alien-hive-bug-base", AnimalMode.Raider, 1.0f, 32f, 9f, 1.6f, 4.6f, 40f, "scrap", 1),
        new Species("praying-mantis-base", AnimalMode.Raider, 1.5f, 46f, 12f, 1.6f, 4.8f, 40f, "cloth", 1),
        new Species("stone-centipede-base", AnimalMode.Raider, 0.5f, 26f, 8f, 1.8f, 5.2f, 40f, "scrap", 1),
        new Species("velociraptor-base", AnimalMode.Raider, 1.7f, 70f, 16f, 2.0f, 6.0f, 40f, "food", 2),
    };

    /// <summary>Puts one animal in the world, standing on groundY.</summary>
    public static AnimalAI Spawn(Species sp, Transform parent, Vector3 pos, float groundY, System.Random rng = null)
    {
        float yaw = rng != null ? (float)rng.NextDouble() * 360f : Random.Range(0f, 360f);
        var go = ModelLibrary.Spawn(sp.model, null, new Vector3(pos.x, groundY, pos.z), yaw, fitHeight: sp.height,
                                    addCollider: true, keepAnimations: true);
        if (go == null) return null;
        if (parent != null) go.transform.SetParent(parent, true);

        var hp = go.AddComponent<Health>();
        hp.max = sp.health;
        hp.current = sp.health;
        // Game and predators give raw meat, and the bigger ones a hide as well.
        hp.dropItem = sp.drop == "food" ? "raw_meat" : sp.drop;
        hp.dropCount = sp.dropCount;
        if (sp.drop == "food" && sp.health >= 25f) { hp.dropItem2 = "leather"; hp.dropCount2 = Mathf.Max(1, Mathf.RoundToInt(sp.health / 45f)); }
        hp.barRange = 14f;

        var ai = go.AddComponent<AnimalAI>();
        ai.mode = sp.mode;
        ai.walkSpeed = sp.walk; ai.runSpeed = sp.run; ai.aggroRadius = sp.aggro;
        ai.biteDamage = sp.damage; ai.lurker = sp.lurker; ai.groundY = groundY;
        ai.biteRange = Mathf.Max(1.6f, sp.height * 0.9f);
        return ai;
    }
}

/// <summary>
/// One creature. See AnimalMode for the behaviours. It walks on a flat level (the street or the roof),
/// turns away from walls it would walk into, and plays the model's own Walk / Run / Idle clips.
/// </summary>
public class AnimalAI : MonoBehaviour
{
    public static readonly List<AnimalAI> All = new List<AnimalAI>();

    public AnimalMode mode;
    public float walkSpeed = 1.6f, runSpeed = 5f;
    public float aggroRadius = 14f, biteRange = 1.8f, biteDamage = 12f, biteCooldown = 1.3f;
    public float roamRadius = 30f;
    public bool lurker;
    public float leash = 10f;
    public float groundY = -90f;
    [Tooltip("Raiders: half the roof size, so they stay on it. Zero = no limit.")]
    public Vector2 stayWithin;
    /// <summary>Set on animals sent against a Drifter camp: they go for the Drifters first, from any distance.</summary>
    public bool huntsSurvivors;
    /// <summary>Sound made when it first goes for something.</summary>
    public string cry = "growl";

    public Health Hp { get; private set; }

    Vector3 home, target;
    float nextPick, nextBite, idleUntil, heldUntil, steerUntil, nextCry;
    Vector3 steerDir;
    string anim = "";
    Collider body;
    bool wasHunting;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    void Start()
    {
        home = transform.position;
        Hp = GetComponent<Health>();
        if (Hp == null) Hp = gameObject.AddComponent<Health>();
        Hp.OnDeath += OnDeath;
        body = GetComponent<Collider>();
        Pick();
        SetAnim("idle");
    }

    void OnDeath()
    {
        if (body != null) body.enabled = false;   // don't trip over the body
        var a = GetComponentInChildren<Animation>();
        if (a != null) a.Stop();
        if (mode != AnimalMode.Survivor) Sfx.PlayAt("death_animal", transform.position, 0.8f, 30f);
    }

    /// <summary>Runs the action the first time the player lands a blow on this creature.</summary>
    public void Hp_OnHurtByPlayer(System.Action action)
    {
        var h = GetComponent<Health>();
        if (h == null) return;
        bool done = false;
        h.OnHurt += source =>
        {
            if (done || (source != "strike" && source != "heavy strike")) return;
            done = true;
            action();
        };
    }

    /// <summary>A snare holds the animal still for a while.</summary>
    public void HoldFor(float seconds) { heldUntil = Mathf.Max(heldUntil, Time.time + seconds); }

    void Update()
    {
        if (GameState.Frozen || Hp == null || Hp.Dead) return;
        if (Time.time < heldUntil) { SetAnim("idle"); return; }

        var p = PlayerController.Instance;
        bool playerHere = p != null && !p.Climbing && Mathf.Abs(p.transform.position.y - groundY) < 5f;
        Vector3 toPlayer = p != null ? p.transform.position - transform.position : Vector3.zero;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        bool hunting = false;

        switch (mode)
        {
            case AnimalMode.Prey:
                if (playerHere && dist < aggroRadius) { MoveToward(-toPlayer.normalized, runSpeed, "run"); idleUntil = 0f; }
                else Wander();
                break;

            case AnimalMode.Predator:
            {
                float night = (GameClock.Instance != null && GameClock.Instance.IsNight) ? 1.5f : 1f;
                // A torch in the hand keeps the smaller ones back.
                float reach = aggroRadius * night * (Items.Equipped != null && Items.Equipped.light && biteDamage < 15f ? 0.5f : 1f);
                Health prey = NearestSurvivor(huntsSurvivors ? 200f : aggroRadius);
                bool wantPlayer = playerHere && dist < reach;
                if (prey != null && (huntsSurvivors || !wantPlayer || Vector3.Distance(prey.transform.position, transform.position) < dist))
                {
                    // If the player wades in close, turn on them instead.
                    if (wantPlayer && dist < 5f) HuntPlayer(p, toPlayer, dist);
                    else HuntOther(prey);
                    hunting = true;
                }
                else if (wantPlayer) { HuntPlayer(p, toPlayer, dist); hunting = true; }
                else Wander();
                break;
            }

            case AnimalMode.Raider:
                Raid(p, playerHere, toPlayer, dist);
                break;

            case AnimalMode.Survivor:
            {
                if (Faction.Hostile && playerHere && dist < 20f) { HuntPlayer(p, toPlayer, dist); hunting = true; break; }
                Health threat = NearestThreat(14f);
                if (threat != null) { HuntOther(threat); break; }
                if (playerHere && dist < 5f) { Face(toPlayer); SetAnim("idle"); break; }   // turn to whoever walks up
                Wander();
                break;
            }
        }

        if (hunting && !wasHunting && Time.time > nextCry)
        {
            nextCry = Time.time + 8f;
            if (!string.IsNullOrEmpty(cry)) Sfx.PlayAt(cry, transform.position + Vector3.up * 0.5f, 0.9f, 40f);
        }
        wasHunting = hunting;
    }

    Health NearestSurvivor(float range)
    {
        Health best = null;
        float bd = range;
        foreach (var s in Survivor.All)
        {
            if (s == null || s.Hp == null || s.Hp.Dead || Mathf.Abs(s.transform.position.y - groundY) > 5f) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);
            if (d < bd) { bd = d; best = s.Hp; }
        }
        return best;
    }

    Health NearestThreat(float range)
    {
        Health best = null;
        float bd = range;
        foreach (var a in All)
        {
            if (a == null || a == this || a.Hp == null || a.Hp.Dead) continue;
            if (a.mode != AnimalMode.Predator && a.mode != AnimalMode.Raider) continue;
            float d = Vector3.Distance(transform.position, a.transform.position);
            if (d < bd) { bd = d; best = a.Hp; }
        }
        return best;
    }

    void HuntPlayer(PlayerController p, Vector3 toPlayer, float dist)
    {
        if (dist > biteRange * 0.8f) MoveToward(toPlayer.normalized, runSpeed, "run");
        else { Face(toPlayer); SetAnim("idle"); }

        if (dist < biteRange && Time.time >= nextBite)
        {
            nextBite = Time.time + biteCooldown;
            p.TakeDamage(biteDamage, Nice(name));
            Sfx.PlayAt(mode == AnimalMode.Survivor ? "punch" : "bite", transform.position, 1f, 25f);
            StartCoroutine(Lunge());
        }
    }

    void HuntOther(Health prey)
    {
        Vector3 to = prey.transform.position - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist > biteRange * 0.8f) MoveToward(to.normalized, runSpeed, "run");
        else { Face(to); SetAnim("idle"); }

        if (dist < biteRange + 0.4f && Time.time >= nextBite)
        {
            nextBite = Time.time + biteCooldown;
            prey.TakeDamage(biteDamage, name);
            Sfx.PlayAt(mode == AnimalMode.Survivor ? "punch" : "bite", transform.position, 0.8f, 25f);
            StartCoroutine(Lunge());
        }
    }

    void Raid(PlayerController p, bool playerHere, Vector3 toPlayer, float dist)
    {
        // A barricade in the way has to come down first.
        var wall = BaseStructure.Nearest(transform.position, 2.4f, "barricade");
        if (wall != null) { Attack(wall); return; }

        if (playerHere && dist < aggroRadius) { HuntPlayer(p, toPlayer, dist); return; }

        var s = BaseStructure.Nearest(transform.position, 80f);
        if (s == null) { Wander(); return; }
        Vector3 to = s.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude > 1.8f) MoveToward(to.normalized, runSpeed * 0.8f, "run");
        else Attack(s);
    }

    void Attack(BaseStructure s)
    {
        Vector3 to = s.transform.position - transform.position;
        to.y = 0f;
        Face(to);
        SetAnim("idle");
        if (Time.time < nextBite) return;
        nextBite = Time.time + biteCooldown;
        s.Damage(biteDamage, "raid");
        Sfx.PlayAt("break", s.transform.position, 0.6f, 25f);
        StartCoroutine(Lunge());
    }

    /// <summary>Quick forward snap of the model when it bites. Only the visual moves, not the collider.</summary>
    IEnumerator Lunge()
    {
        if (transform.childCount == 0) yield break;
        var vis = transform.GetChild(0);
        Vector3 rest = vis.localPosition;
        Vector3 fwd = rest + Vector3.forward * (0.35f / Mathf.Max(0.05f, transform.localScale.x));

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            vis.localPosition = Vector3.Lerp(rest, fwd, t);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.25f;
            vis.localPosition = Vector3.Lerp(fwd, rest, t);
            yield return null;
        }
        vis.localPosition = rest;
    }

    void Wander()
    {
        if (Time.time < idleUntil)
        {
            SetAnim("idle");
            return;
        }

        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.magnitude < 1f || Time.time > nextPick)
        {
            idleUntil = Time.time + Random.Range(1.5f, 5f);
            Pick();
            return;
        }
        MoveToward(to.normalized, walkSpeed, "walk");
    }

    void Pick()
    {
        float range = lurker ? leash : roamRadius;
        Vector2 c = Random.insideUnitCircle * range;
        target = new Vector3(home.x + c.x, groundY, home.z + c.y);
        if (stayWithin != Vector2.zero)
            target = new Vector3(Random.Range(-stayWithin.x, stayWithin.x), groundY, Random.Range(-stayWithin.y, stayWithin.y));
        nextPick = Time.time + 12f;
    }

    void MoveToward(Vector3 dir, float speed, string clip)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        // Something solid ahead: turn along it for a moment instead of walking into it.
        if (Time.time < steerUntil) dir = steerDir;
        else
        {
            Vector3 chest = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(chest, dir, out var hit, 1.6f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<PlayerController>() == null
                && hit.collider.GetComponentInParent<AnimalAI>() == null)
            {
                Vector3 along = Vector3.Cross(Vector3.up, hit.normal).normalized;
                if (Vector3.Dot(along, dir) < 0f) along = -along;
                steerDir = (along + hit.normal * 0.3f).normalized;
                steerUntil = Time.time + 0.6f;
                dir = steerDir;
                if (mode != AnimalMode.Raider && Random.value < 0.3f) Pick();
            }
        }

        Vector3 pos = transform.position + dir * speed * Time.deltaTime;
        pos.y = groundY;
        if (stayWithin != Vector2.zero)
        {
            pos.x = Mathf.Clamp(pos.x, -stayWithin.x, stayWithin.x);
            pos.z = Mathf.Clamp(pos.z, -stayWithin.y, stayWithin.y);
        }
        transform.position = pos;
        Face(dir);
        SetAnim(clip);
    }

    void Face(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 8f * Time.deltaTime);
    }

    void SetAnim(string clip)
    {
        if (anim == clip) return;
        anim = clip;
        // Clip names differ between models (Walk / Crawl / Fly / Run), so try the close matches in order.
        string[] names = clip == "run" ? new[] { "run", "walk", "crawl", "fly" }
                       : clip == "walk" ? new[] { "walk", "crawl", "fly", "run" }
                       : new[] { "idle", "walk", "crawl" };
        ModelLibrary.PlayClip(gameObject, names);
        var a = GetComponentInChildren<Animation>();
        if (a == null) return;
        // A model with no idle clip stands still rather than walking on the spot.
        bool freeze = clip == "idle" && a.GetClip("Idle") == null && a.GetClip("idle") == null;
        foreach (AnimationState s in a) s.speed = freeze ? 0f : 1f;
    }

    static string Nice(string n)
    {
        n = n.Replace("(Clone)", "").Replace("-base", "");
        int cut = n.LastIndexOf('-');
        if (cut > 0 && n.Length - cut == 7) n = n.Substring(0, cut);   // drop a 6-character hash
        return "a " + n.Replace('-', ' ');
    }
}
