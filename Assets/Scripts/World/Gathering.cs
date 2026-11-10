using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Something you get materials from by hitting it: a tree (wood), a rock or rubble (stone), ore (scrap and
/// stone), a wrecked vehicle (scrap). Each hit that lands gives a little; when it is used up a tree falls and
/// a rock crumbles. Trees and rocks come back after a few days. MeleeCombat calls Hit.
/// </summary>
public class ResourceNode : MonoBehaviour
{
    public enum Kind { Tree, Rock, Ore, Wreck }

    public Kind kind;
    public int hitsLeft = 4;
    public int regrowDays = 3;        // 0 = never comes back (wrecks)
    int fullHits, goneOnDay = -1;
    Vector3 baseScale;
    Collider col;
    Renderer[] rends;

    public static ResourceNode Add(GameObject go, Kind kind, int hits, int regrowDays)
    {
        var n = go.AddComponent<ResourceNode>();
        n.kind = kind; n.hitsLeft = hits; n.fullHits = hits; n.regrowDays = regrowDays;
        return n;
    }

    void Awake()
    {
        baseScale = transform.localScale;
        col = GetComponent<Collider>();
    }

    void OnEnable() { GameClock.OnDawn += Dawn; }
    void OnDisable() { GameClock.OnDawn -= Dawn; }

    public bool Spent => hitsLeft <= 0;

    /// <summary>One blow. Returns a short description of what it gave, or "" if there was nothing left.</summary>
    public string Hit(WeaponDef weapon)
    {
        if (Spent) return "";
        float chop = weapon != null ? weapon.chop : 1f, mine = weapon != null ? weapon.mine : 1f;
        string got;
        switch (kind)
        {
            case Kind.Tree:
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(chop));
                Items.Add("wood", n);
                got = "+" + n + " Wood";
                break;
            }
            case Kind.Rock:
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(mine));
                Items.Add("stone", n);
                got = "+" + n + " Stone";
                break;
            }
            case Kind.Ore:
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(mine));
                Items.Add("scrap", n);
                Items.Add("stone", 1);
                got = "+" + n + " Scrap, +1 Stone";
                break;
            }
            default:
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(mine));
                Items.Add("scrap", n);
                got = "+" + n + " Scrap";
                break;
            }
        }
        hitsLeft--;
        Progress.Gain(2);
        Tutorial.Notify("gather");
        StartCoroutine(Shake());
        if (Spent) StartCoroutine(Finish());
        return got;
    }

    IEnumerator Shake()
    {
        Quaternion rest = transform.localRotation;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float k = (1f - t / 0.25f) * 2.5f;
            transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * 60f) * k, 0f, Mathf.Cos(t * 50f) * k);
            yield return null;
        }
        transform.localRotation = rest;
    }

    IEnumerator Finish()
    {
        if (col != null) col.enabled = false;
        if (kind == Kind.Wreck) { Hud.Toast("Stripped bare."); yield break; }   // the shell stays

        Quaternion a = transform.localRotation;
        Quaternion b = kind == Kind.Tree ? a * Quaternion.Euler(86f, 0f, 0f) : a;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / (kind == Kind.Tree ? 1.1f : 0.5f);
            transform.localRotation = Quaternion.Slerp(a, b, t * t);
            if (kind != Kind.Tree) transform.localScale = baseScale * (1f - t * 0.75f);
            yield return null;
        }
        yield return new WaitForSeconds(1.2f);
        rends = GetComponentsInChildren<Renderer>();
        foreach (var r in rends) r.enabled = false;
        transform.localRotation = a;
        goneOnDay = GameClock.Instance != null ? GameClock.Instance.Day : 0;
    }

    void Dawn(int day)
    {
        if (!Spent || regrowDays <= 0 || goneOnDay < 0 || day - goneOnDay < regrowDays) return;
        hitsLeft = fullHits;
        goneOnDay = -1;
        transform.localScale = baseScale;
        if (rends != null) foreach (var r in rends) if (r != null) r.enabled = true;
        if (col != null) col.enabled = true;
    }
}

/// <summary>What a search can turn up: an item, how many, and how likely.</summary>
public struct LootRoll
{
    public string item;
    public int min, max;
    public float chance;
    public LootRoll(string item, int min, int max, float chance = 1f) { this.item = item; this.min = min; this.max = max; this.chance = chance; }
}

/// <summary>
/// A cupboard, fridge, locker, crate or bush: hold Interact to search or pick it. It is emptied once, and
/// refills after refillDays (0 = never). What it held is decided when it is searched.
/// </summary>
public class Container : Interactable
{
    public string label = "Search crate";
    public float seconds = 1.6f;
    public int refillDays;
    public LootRoll[] loot;
    public int xp = 4;
    /// <summary>Called when it is emptied (used to remember roof caches across saves).</summary>
    public System.Action onEmptied;
    /// <summary>Optional part hidden while empty (berries on a bush).</summary>
    public GameObject fullVisual;

    bool empty;
    int emptiedDay;

    public static Container Add(GameObject go, string label, float seconds, int refillDays, params LootRoll[] loot)
    {
        var c = go.AddComponent<Container>();
        c.label = label; c.seconds = seconds; c.refillDays = refillDays; c.loot = loot;
        c.range = 2.6f;
        return c;
    }

    protected override void OnEnable() { base.OnEnable(); GameClock.OnDawn += Dawn; }
    protected override void OnDisable() { base.OnDisable(); GameClock.OnDawn -= Dawn; }

    public bool Empty => empty;
    public void SetEmpty() { empty = true; emptiedDay = GameClock.Instance != null ? GameClock.Instance.Day : 0; if (fullVisual != null) fullVisual.SetActive(false); }

    public override string Prompt => empty ? "" : label;
    public override float HoldSeconds => seconds;

    public override void Use()
    {
        if (empty) return;
        var sb = new System.Text.StringBuilder();
        if (loot != null)
        {
            foreach (var l in loot)
            {
                if (Random.value > l.chance) continue;
                int n = Random.Range(l.min, l.max + 1);
                if (n <= 0) continue;
                Items.Add(l.item, n);
                if (sb.Length > 0) sb.Append(",  ");
                sb.Append('+').Append(n).Append(' ').Append(Items.Name(l.item));
            }
        }
        Hud.Toast(sb.Length > 0 ? sb.ToString() : "Nothing useful.");
        Progress.Gain(xp);
        Tutorial.Notify("search");
        SetEmpty();
        onEmptied?.Invoke();
    }

    void Dawn(int day)
    {
        if (!empty || refillDays <= 0 || day - emptiedDay < refillDays) return;
        empty = false;
        if (fullVisual != null) fullVisual.SetActive(true);
    }
}
