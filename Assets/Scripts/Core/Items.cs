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

/// <summary>
/// One thing that can be made. station "" = by hand, from the inventory, instantly.
/// "workbench" or "forge" = ordered at that station and then built by holding Interact there for workSeconds.
/// level is the player level that unlocks it.
/// </summary>
public class Recipe
{
    public string output, station;
    public int count, level;
    public float workSeconds;
    public string[] inputs;   // "itemId:amount"

    public Recipe(string output, int count, string station, int level, float workSeconds, params string[] inputs)
    {
        this.output = output; this.count = count; this.station = station; this.level = level;
        this.workSeconds = workSeconds; this.inputs = inputs;
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

/// <summary>A melee weapon or tool: damage, reach, speed, how it gathers, and how its model sits in the hand.</summary>
public class WeaponDef
{
    public string id, model;
    public float damage, reach, speed;   // speed: 1 = club pace; higher is faster
    public bool thrust;                  // spear: stabs forward instead of swinging
    public float chop = 1f, mine = 1f;   // multipliers for wood from trees, stone and scrap from rock and wrecks
    public float length;                 // model is fitted to this height in metres
    public float grip;                   // how far up the shaft the hand holds it, in metres
    public Vector3 euler;                // extra turn once the model stands upright (to face its blade forward)
    public bool flip;                    // the model's head is at its low end: turn it over
}

/// <summary>
/// Inventory, recipes and weapons. Food, water, wood and scrap live in RunData as plain numbers;
/// everything else is stored as ItemStacks in RunData.items. All of it saves with the run.
/// </summary>
public static class Items
{
    public static event Action OnChanged;

    public static readonly List<ItemDef> Defs = new List<ItemDef>
    {
        new ItemDef("wood", "Wood", "Chop trees for it. Any weapon works; an axe or hatchet is faster."),
        new ItemDef("stone", "Stone", "Break rocks and rubble for it. A pickaxe is faster."),
        new ItemDef("scrap", "Scrap", "Strip wrecked vehicles, mine ore, search garages."),
        new ItemDef("fiber", "Fiber", "Gathered from grass, ferns and reeds. Makes cloth and rope."),
        new ItemDef("food", "Food", "You eat one each dawn. Berries, crops, game and kitchen cupboards."),
        new ItemDef("water", "Water", "You drink one each dawn and planters need it. Water tower, lake, fridges."),
        new ItemDef("cloth", "Cloth", "From wardrobes and lockers, or woven from fiber."),
        new ItemDef("rope", "Rope", "Twisted from fiber. Needed for snares and spears."),
        new ItemDef("bandage", "Bandage", "Use it to recover 35 health."),
        new ItemDef("planter_kit", "Planter kit", "Place on the roof to grow food."),
        new ItemDef("workbench_kit", "Workbench kit", "Place on the roof. Tools, weapons and traps are built here."),
        new ItemDef("forge_kit", "Forge kit", "Place on the roof. Heavy weapons are made here."),
        new ItemDef("snare_kit", "Snare kit", "Place on the roof. Holds and hurts what steps in it."),
        new ItemDef("trap_kit", "Spike trap kit", "Place on the roof. Hurts anything that crosses it."),
        new ItemDef("barricade_kit", "Barricade kit", "Place on the roof. Raiders must break it to get past."),
        new ItemDef("club", "Nail club", "Sturdy and quick."),
        new ItemDef("hatchet", "Hatchet", "Light axe. Chops trees twice as fast."),
        new ItemDef("pickaxe", "Pickaxe", "Breaks rock, ore and wrecks twice as fast."),
        new ItemDef("spear", "Spear", "Long reach. Keeps teeth away from you."),
        new ItemDef("axe", "War axe", "Slow and heavy. Hits hardest, and fells trees fastest."),
    };

    public static readonly Recipe[] Recipes =
    {
        // by hand
        new Recipe("cloth", 1, "", 1, 0f, "fiber:3"),
        new Recipe("rope", 1, "", 1, 0f, "fiber:4"),
        new Recipe("bandage", 1, "", 1, 0f, "cloth:2"),
        new Recipe("club", 1, "", 1, 0f, "wood:3", "scrap:1"),
        new Recipe("planter_kit", 1, "", 1, 0f, "wood:4"),
        new Recipe("workbench_kit", 1, "", 1, 0f, "wood:8", "stone:4"),
        // workbench
        new Recipe("hatchet", 1, "workbench", 2, 5f, "wood:2", "stone:3"),
        new Recipe("snare_kit", 1, "workbench", 2, 4f, "rope:1", "wood:2"),
        new Recipe("pickaxe", 1, "workbench", 3, 6f, "wood:3", "stone:4", "scrap:1"),
        new Recipe("spear", 1, "workbench", 3, 6f, "wood:3", "scrap:2", "rope:1"),
        new Recipe("trap_kit", 1, "workbench", 4, 8f, "wood:4", "scrap:3"),
        new Recipe("barricade_kit", 1, "workbench", 5, 10f, "wood:8", "scrap:2", "rope:1"),
        new Recipe("forge_kit", 1, "workbench", 6, 14f, "stone:12", "scrap:6", "wood:4"),
        // forge
        new Recipe("axe", 1, "forge", 6, 12f, "wood:3", "scrap:8"),
    };

    public static readonly WeaponDef[] Weapons =
    {
        new WeaponDef { id = "club", model = "rs-club", damage = 20f, reach = 2.0f, speed = 1f, length = 0.85f, grip = 0.06f },
        new WeaponDef { id = "hatchet", model = "hand-axe", damage = 16f, reach = 1.9f, speed = 1.1f, chop = 2f, length = 0.6f, grip = 0.08f },
        new WeaponDef { id = "pickaxe", model = "mining-pickaxe", damage = 18f, reach = 2.0f, speed = 0.9f, mine = 2f, length = 0.9f, grip = 0.1f },
        new WeaponDef { id = "spear", model = "spear", damage = 20f, reach = 3.1f, speed = 1.1f, thrust = true, length = 2.0f, grip = 0.75f },
        new WeaponDef { id = "axe", model = "axe", damage = 36f, reach = 2.1f, speed = 0.72f, chop = 3f, length = 0.95f, grip = 0.1f },
    };

    public static WeaponDef Weapon(string id)
    {
        foreach (var w in Weapons) if (w.id == id) return w;
        return null;
    }

    static RunData Run => RunManager.Instance != null ? RunManager.Instance.Current : null;

    /// <summary>The weapon in hand, or null for bare fists. Falls back to fists if the weapon is gone.</summary>
    public static WeaponDef Equipped
    {
        get
        {
            var run = Run;
            if (run == null || string.IsNullOrEmpty(run.equipped)) return null;
            return Count(run.equipped) > 0 ? Weapon(run.equipped) : null;
        }
    }

    public static void Equip(string id)
    {
        var run = Run;
        if (run == null) return;
        run.equipped = id ?? "";
        Hud.Toast(run.equipped == "" ? "Fists" : "In hand: " + Def(run.equipped).name);
        OnChanged?.Invoke();
    }

    /// <summary>Fists, then each weapon you carry, then back to fists.</summary>
    public static void CycleWeapon()
    {
        var run = Run;
        if (run == null) return;
        int start = -1;
        for (int i = 0; i < Weapons.Length; i++) if (Weapons[i].id == run.equipped) start = i;
        string next = "";
        for (int i = start + 1; i < Weapons.Length; i++)
            if (Count(Weapons[i].id) > 0) { next = Weapons[i].id; break; }
        Equip(next);
    }

    /// <summary>Kits, in the order the cycle key steps through them. Build places the selected one.</summary>
    public static readonly string[] BuildKits = { "planter_kit", "workbench_kit", "forge_kit", "snare_kit", "trap_kit", "barricade_kit" };
    public static int SelectedIndex;
    public static string SelectedKit => BuildKits[SelectedIndex];

    public static void CycleBuild()
    {
        for (int i = 1; i <= BuildKits.Length; i++)
        {
            int idx = (SelectedIndex + i) % BuildKits.Length;
            if (Count(BuildKits[idx]) > 0) { SelectedIndex = idx; Hud.Toast("Selected: " + Def(SelectedKit).name); return; }
        }
        Hud.Toast("You carry no kits. Craft one with " + Keybinds.Describe(GameAction.Inventory) + ".");
    }

    public static ItemDef Def(string id) => Defs.Find(d => d.id == id);
    public static string Name(string id) { var d = Def(id); return d != null ? d.name : id; }

    public static int Count(string id)
    {
        var run = Run;
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
        var run = Run;
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

    public static bool Unlocked(Recipe r) => Progress.Level >= r.level;

    /// <summary>Takes the inputs out of the inventory. False (and nothing taken) if any are missing.</summary>
    public static bool PayFor(Recipe r)
    {
        if (!HasInputs(r)) return false;
        foreach (var inp in r.inputs)
        {
            var p = inp.Split(':');
            Remove(p[0], int.Parse(p[1]));
        }
        return true;
    }

    /// <summary>Hand recipes only: made on the spot.</summary>
    public static bool CraftByHand(Recipe r)
    {
        if (r.station != "" || !Unlocked(r) || !PayFor(r)) return false;
        Finish(r);
        return true;
    }

    /// <summary>Puts a finished recipe into the inventory.</summary>
    public static void Finish(Recipe r)
    {
        Add(r.output, r.count);
        Progress.Gain(10 + r.level * 4);
        Tutorial.Notify("craft");
        Hud.Toast("Made " + Name(r.output) + (r.count > 1 ? " x" + r.count : "") + ".");
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
        return "Nothing to use. Make a bandage from 2 Cloth.";
    }
}

/// <summary>
/// Player level. Experience comes from gathering, searching, crafting, building, fighting and surviving
/// each dawn. Each level unlocks more recipes (see Recipe.level). Saved with the run.
/// </summary>
public static class Progress
{
    static RunData Run => RunManager.Instance != null ? RunManager.Instance.Current : null;

    public static int Xp => Run != null ? Run.xp : 0;

    /// <summary>Total experience needed to reach a level.</summary>
    public static int Needed(int level) { return level <= 1 ? 0 : Mathf.RoundToInt(40f * Mathf.Pow(level - 1, 1.6f)); }

    public static int Level
    {
        get
        {
            int xp = Xp, l = 1;
            while (l < 30 && xp >= Needed(l + 1)) l++;
            return l;
        }
    }

    /// <summary>0..1 progress through the current level.</summary>
    public static float Fraction
    {
        get
        {
            int l = Level;
            float a = Needed(l), b = Needed(l + 1);
            return Mathf.Clamp01((Xp - a) / Mathf.Max(1f, b - a));
        }
    }

    public static void Gain(int amount)
    {
        var run = Run;
        if (run == null || amount <= 0) return;
        int before = Level;
        run.xp += amount;
        int after = Level;
        if (after <= before) return;

        var unlocked = new List<string>();
        foreach (var r in Items.Recipes)
            if (r.level > before && r.level <= after) unlocked.Add(Items.Name(r.output));
        Hud.Banner("Level " + after + (unlocked.Count > 0 ? "  -  new: " + string.Join(", ", unlocked) : ""), 6f);
    }
}
