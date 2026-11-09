using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ItemStack
{
    public string id;
    public int count;
}

public class ItemDef
{
    public string id, name, desc;
    public ItemDef(string id, string name, string desc) { this.id = id; this.name = name; this.desc = desc; }
}

public class Recipe
{
    public string output;
    public int count;
    public bool needsBench;   // must stand near the workbench on the roof
    public string[] inputs;   // "itemId:amount"

    public Recipe(string output, int count, bool needsBench, params string[] inputs)
    {
        this.output = output;
        this.count = count;
        this.needsBench = needsBench;
        this.inputs = inputs;
    }

    public string Describe()
    {
        var parts = new List<string>();
        foreach (var i in inputs)
        {
            var p = i.Split(':');
            var def = Items.Def(p[0]);
            parts.Add(p[1] + " " + (def != null ? def.name : p[0]));
        }
        return string.Join(" + ", parts);
    }
}

/// <summary>A melee weapon: how hard and far it hits, how fast it swings, and how its model sits in the hand.</summary>
public class WeaponDef
{
    public string id, model;
    public float damage, reach, speed;   // speed: 1 = club pace; higher is faster
    public bool thrust;                  // spear: stabs forward instead of swinging
    public float length;                 // model is fitted to this height in metres
    public float grip;                   // how far up the shaft the hand holds it, in metres
    public Vector3 euler;                // model rotation so the business end points up
}

/// <summary>
/// Inventory, crafting and weapons. Food, water, wood and scrap live in RunData (the roof systems use them);
/// everything else is stored as ItemStacks in RunData.items so it saves with the run.
/// </summary>
public static class Items
{
    public static event Action OnChanged;

    public static readonly List<ItemDef> Defs = new List<ItemDef>
    {
        new ItemDef("wood", "Wood", "Building material. Hollow logs and crates on the street hold it."),
        new ItemDef("scrap", "Scrap", "Metal for weapons, traps and kits. Garages and vehicles are the best source."),
        new ItemDef("food", "Food", "You eat one each dawn. Use it (Q) to recover 10 health."),
        new ItemDef("water", "Water", "You drink one each dawn, and planters need it. The water tower refills daily."),
        new ItemDef("cloth", "Cloth", "Rags from apartments and shops. Makes bandages and rope."),
        new ItemDef("rope", "Rope", "Needed for spears and snares."),
        new ItemDef("bandage", "Bandage", "Use it (Q) to recover 35 health."),
        new ItemDef("planter_kit", "Planter kit", "Place on the roof to grow food."),
        new ItemDef("snare_kit", "Snare kit", "Place on the roof. Catches what climbs up, a few times."),
        new ItemDef("trap_kit", "Spike trap kit", "Place on the roof. Hurts anything that crosses it and adds defense."),
        new ItemDef("barricade_kit", "Barricade kit", "Place on the roof. Raiders must break it to get past. Adds the most defense."),
        new ItemDef("club", "Nail club", "Sturdy and quick."),
        new ItemDef("spear", "Spear", "Long reach. Keeps teeth away from you."),
        new ItemDef("axe", "Axe", "Slow, heavy, hits hardest."),
    };

    public static readonly Recipe[] Recipes =
    {
        new Recipe("bandage", 1, false, "cloth:2"),
        new Recipe("rope", 1, false, "cloth:2"),
        new Recipe("club", 1, false, "wood:3", "scrap:1"),
        new Recipe("planter_kit", 1, false, "wood:2", "scrap:1"),
        new Recipe("snare_kit", 1, false, "rope:1", "wood:1"),
        new Recipe("trap_kit", 1, true, "wood:2", "scrap:2"),
        new Recipe("barricade_kit", 1, true, "wood:4", "scrap:2"),
        new Recipe("spear", 1, true, "wood:2", "scrap:2", "rope:1"),
        new Recipe("axe", 1, true, "wood:2", "scrap:4"),
    };

    public static readonly WeaponDef[] Weapons =
    {
        new WeaponDef { id = "club", model = "rs-club", damage = 22f, reach = 2.0f, speed = 1f, length = 0.85f, grip = 0.06f, euler = Vector3.zero },
        new WeaponDef { id = "spear", model = "spear", damage = 20f, reach = 3.1f, speed = 1.1f, thrust = true, length = 2.0f, grip = 0.75f, euler = Vector3.zero },
        new WeaponDef { id = "axe", model = "axe", damage = 36f, reach = 2.1f, speed = 0.72f, length = 0.95f, grip = 0.1f, euler = Vector3.zero },
    };

    public static WeaponDef Weapon(string id)
    {
        foreach (var w in Weapons) if (w.id == id) return w;
        return null;
    }

    /// <summary>The weapon in hand, or null for bare fists. Falls back to fists if the weapon is gone.</summary>
    public static WeaponDef Equipped
    {
        get
        {
            var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
            if (run == null || string.IsNullOrEmpty(run.equipped)) return null;
            return Count(run.equipped) > 0 ? Weapon(run.equipped) : null;
        }
    }

    public static void Equip(string id)
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run == null) return;
        run.equipped = id ?? "";
        Hud.Toast(run.equipped == "" ? "Fists" : "Equipped: " + Def(run.equipped).name);
        OnChanged?.Invoke();
    }

    /// <summary>Fists, then each weapon you carry, then back to fists.</summary>
    public static void CycleWeapon()
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run == null) return;
        int start = -1;
        for (int i = 0; i < Weapons.Length; i++) if (Weapons[i].id == run.equipped) start = i;
        string next = "";
        for (int i = start + 1; i < Weapons.Length; i++)
            if (Count(Weapons[i].id) > 0) { next = Weapons[i].id; break; }
        run.equipped = next;
        Hud.Toast(next == "" ? "Fists" : "Equipped: " + Def(next).name);
        OnChanged?.Invoke();
    }

    /// <summary>Roof kits, in the order the cycle key steps through them. Build places the selected one.</summary>
    public static readonly string[] BuildKits = { "planter_kit", "snare_kit", "trap_kit", "barricade_kit" };
    public static int SelectedIndex;
    public static string SelectedKit => BuildKits[SelectedIndex];

    public static void CycleBuild()
    {
        // Step to the next kit you actually carry; if you carry none, just step.
        for (int i = 1; i <= BuildKits.Length; i++)
        {
            int idx = (SelectedIndex + i) % BuildKits.Length;
            if (Count(BuildKits[idx]) > 0) { SelectedIndex = idx; Hud.Toast("Selected: " + Def(SelectedKit).name); return; }
        }
        SelectedIndex = (SelectedIndex + 1) % BuildKits.Length;
        Hud.Toast("Selected: " + Def(SelectedKit).name + " (none carried, craft one with " + Keybinds.Describe(GameAction.Inventory) + ")");
    }

    public static ItemDef Def(string id) => Defs.Find(d => d.id == id);

    public static int Count(string id)
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run == null) return 0;
        switch (id)
        {
            case "food": return run.food;
            case "water": return run.water;
            case "wood": return run.wood;
            case "scrap": return run.scrap;
        }
        var s = run.items.Find(x => x.id == id);
        return s != null ? s.count : 0;
    }

    public static void Add(string id, int n)
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run == null || n == 0) return;

        if (id == "food") run.food = Mathf.Max(0, run.food + n);
        else if (id == "water") run.water = Mathf.Max(0, run.water + n);
        else if (id == "wood") run.wood = Mathf.Max(0, run.wood + n);
        else if (id == "scrap") run.scrap = Mathf.Max(0, run.scrap + n);
        else
        {
            var s = run.items.Find(x => x.id == id);
            if (s == null)
            {
                s = new ItemStack { id = id, count = 0 };
                run.items.Add(s);
            }
            s.count += n;
            if (s.count <= 0) run.items.Remove(s);
        }

        // The first weapon you get goes straight into your hand.
        if (n > 0 && Weapon(id) != null && string.IsNullOrEmpty(run.equipped)) run.equipped = id;
        OnChanged?.Invoke();
    }

    public static bool Remove(string id, int n)
    {
        if (Count(id) < n) return false;
        Add(id, -n);
        return true;
    }

    public static bool HasInputs(Recipe r)
    {
        foreach (var inp in r.inputs)
        {
            var p = inp.Split(':');
            if (Count(p[0]) < int.Parse(p[1])) return false;
        }
        return true;
    }

    public static bool CanCraft(Recipe r) => HasInputs(r) && (!r.needsBench || Workbench.PlayerNear);

    public static bool Craft(Recipe r)
    {
        if (!CanCraft(r)) return false;
        foreach (var inp in r.inputs)
        {
            var p = inp.Split(':');
            Remove(p[0], int.Parse(p[1]));
        }
        Add(r.output, r.count);
        Tutorial.Notify("craft");
        Hud.Toast("Crafted " + Def(r.output).name + ".");
        return true;
    }

    /// <summary>Use key: a bandage first, then food. Returns a message for the HUD.</summary>
    public static string UseBest()
    {
        var player = PlayerController.Instance;
        if (player == null) return "";
        if (player.health >= player.maxHealth) return "You're already at full health.";
        if (Count("bandage") > 0)
        {
            Remove("bandage", 1);
            player.Heal(35f);
            return "Used a bandage (+35 health).";
        }
        if (Count("food") > 0)
        {
            Remove("food", 1);
            player.Heal(10f);
            return "Ate some food (+10 health).";
        }
        return "Nothing to use. Craft a bandage from 2 cloth.";
    }
}

/// <summary>Tracks whether the player is standing at the roof workbench (some recipes need it).</summary>
public static class Workbench
{
    public static Transform Spot;
    public const float Range = 4.5f;

    public static bool PlayerNear
    {
        get
        {
            var p = PlayerController.Instance;
            return Spot != null && p != null && Vector3.Distance(p.transform.position, Spot.position) < Range;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Spot = null; }
}
