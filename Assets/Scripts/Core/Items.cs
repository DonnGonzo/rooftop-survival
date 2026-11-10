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
    public string id, name, desc, category;
    public float weight;       // kilograms each
    public ItemDef(string id, string name, string category, float weight, string desc)
    { this.id = id; this.name = name; this.category = category; this.weight = weight; this.desc = desc; }
}

/// <summary>
/// One thing that can be made. station "" = by hand, on the spot. Otherwise it is ordered at that station
/// ("campfire", "workbench" or "forge") and built by holding Interact there for workSeconds.
/// level is the player level that unlocks it.
/// </summary>
public class Recipe
{
    public string output, station, category;
    public int count, level;
    public float workSeconds;
    public string[] inputs;   // "itemId:amount"

    public Recipe(string output, int count, string station, int level, float workSeconds, string category, params string[] inputs)
    {
        this.output = output; this.count = count; this.station = station; this.level = level;
        this.workSeconds = workSeconds; this.category = category; this.inputs = inputs;
    }

    public string StationName => station == "" ? "By hand" : station == "campfire" ? "Campfire" : station == "workbench" ? "Workbench" : "Forge";
}

/// <summary>A melee weapon or tool: damage, reach, speed, how it gathers, and how its model sits in the hand.</summary>
public class WeaponDef
{
    public string id, model;
    public float damage, reach, speed;   // speed: 1 = club pace; higher is faster
    public bool thrust;                  // spear: stabs forward instead of swinging
    public bool light;                   // torch: carries a flame
    public float chop = 1f, mine = 1f;   // multipliers for wood from trees, stone and scrap from rock and wrecks
    public float length;                 // model is fitted to this length in metres
    public float grip;                   // how far up the shaft the hand holds it, in metres
    public Vector3 euler;                // extra turn once the model stands upright
    public bool flip;                    // the model's head is at its low end: turn it over
}

/// <summary>
/// Everything about items: what exists, what it weighs, what you carry and what is in the roof stash,
/// recipes, weapons, and worn gear (packs and armour, which work just by being carried).
/// Food, water, wood and scrap are plain numbers in RunData; the rest are ItemStacks. All of it saves.
/// </summary>
public static class Items
{
    public static event Action OnChanged;

    public const string Mat = "Materials", Eat = "Food & medicine", Tool = "Tools & weapons", Gear = "Gear", Base = "Base";

    public static readonly List<ItemDef> Defs = new List<ItemDef>
    {
        new ItemDef("wood", "Wood", Mat, 1f, "Chop trees for it. An axe or hatchet is faster."),
        new ItemDef("stone", "Stone", Mat, 1.5f, "Break rocks and rubble for it. A pickaxe is faster."),
        new ItemDef("scrap", "Scrap", Mat, 1f, "Strip wrecked vehicles, mine ore, search garages."),
        new ItemDef("fiber", "Fiber", Mat, 0.2f, "From grass, ferns and reeds. Makes cloth and rope."),
        new ItemDef("cloth", "Cloth", Mat, 0.3f, "From wardrobes and lockers, or woven from fiber."),
        new ItemDef("rope", "Rope", Mat, 0.5f, "Twisted from fiber."),
        new ItemDef("leather", "Leather", Mat, 0.6f, "Hide from the animals you kill. Packs and armour need it."),
        new ItemDef("ingot", "Iron ingot", Mat, 2f, "Scrap smelted at a forge. The best gear needs it."),
        new ItemDef("herbs", "Herbs", Mat, 0.1f, "Picked from wildflowers. Makes poultices."),

        new ItemDef("food", "Rations", Eat, 0.5f, "Berries, tins, crops. Restores 30 hunger."),
        new ItemDef("cooked_meat", "Cooked meat", Eat, 0.6f, "Restores 50 hunger."),
        new ItemDef("raw_meat", "Raw meat", Eat, 0.8f, "Cook it at a campfire. Eaten raw it restores 15 hunger and hurts."),
        new ItemDef("water", "Clean water", Eat, 0.5f, "Restores 40 thirst. Planters need it too."),
        new ItemDef("dirty_water", "Lake water", Eat, 0.5f, "Boil it at a campfire. Drunk as it is, it may make you ill."),
        new ItemDef("bandage", "Bandage", Eat, 0.2f, "Restores 35 health."),
        new ItemDef("poultice", "Herbal poultice", Eat, 0.2f, "Restores 20 health."),

        new ItemDef("torch", "Torch", Tool, 1f, "Lights the dark. A poor weapon, but animals dislike it."),
        new ItemDef("club", "Nail club", Tool, 2.5f, "Sturdy and quick."),
        new ItemDef("knife", "Knife", Tool, 0.4f, "Very fast, short reach. Light enough to always carry."),
        new ItemDef("hammer", "Brick hammer", Tool, 1.2f, "A quick mining tool: more stone and scrap per hit."),
        new ItemDef("kukri", "Kukri", Tool, 0.9f, "A fast forged blade. Good against animals."),
        new ItemDef("katana", "Katana", Tool, 1.4f, "The finest blade you can forge: fast, long and deadly."),
        new ItemDef("hatchet", "Hatchet", Tool, 2f, "Chops trees twice as fast."),
        new ItemDef("pickaxe", "Pickaxe", Tool, 3f, "Breaks rock, ore and wrecks twice as fast."),
        new ItemDef("spear", "Spear", Tool, 3f, "Long reach."),
        new ItemDef("machete", "Machete", Tool, 2f, "Fast and sharp."),
        new ItemDef("axe", "War axe", Tool, 4f, "Slow and heavy. Fells trees fastest."),
        new ItemDef("sledge", "Sledgehammer", Tool, 6f, "The hardest hit there is. Shatters rock."),

        new ItemDef("sling_bag", "Sling bag", Gear, 0.5f, "Carry 15 kg more. Works while you carry it."),
        new ItemDef("backpack", "Backpack", Gear, 1f, "Carry 35 kg more. The biggest pack you own is the one that counts."),
        new ItemDef("frame_pack", "Frame pack", Gear, 2f, "Carry 60 kg more."),
        new ItemDef("leather_armor", "Leather jerkin", Gear, 3f, "Take 20% less damage. Works while you carry it."),
        new ItemDef("plate_vest", "Plated vest", Gear, 6f, "Take 40% less damage."),

        new ItemDef("planter_kit", "Planter kit", Base, 5f, "Grows food. Water it daily."),
        new ItemDef("campfire_kit", "Campfire kit", Base, 5f, "Cooks meat and boils water."),
        new ItemDef("chest_kit", "Storage chest kit", Base, 6f, "A place to leave what you cannot carry. Stations use what is in it."),
        new ItemDef("workbench_kit", "Workbench kit", Base, 10f, "Tools, packs and traps are built here."),
        new ItemDef("lantern_kit", "Lantern kit", Base, 2f, "A light for the roof at night."),
        new ItemDef("rain_kit", "Rain collector kit", Base, 5f, "Gives 2 clean water each dawn."),
        new ItemDef("snare_kit", "Snare kit", Base, 2f, "Holds and hurts what steps in it."),
        new ItemDef("trap_kit", "Spike trap kit", Base, 5f, "Hurts anything that crosses it."),
        new ItemDef("barricade_kit", "Barricade kit", Base, 8f, "Raiders must break it to get past."),
        new ItemDef("forge_kit", "Forge kit", Base, 14f, "Smelts scrap into iron and makes the heaviest gear."),
    };

    public static readonly Recipe[] Recipes =
    {
        // by hand
        new Recipe("cloth", 1, "", 1, 0f, Mat, "fiber:3"),
        new Recipe("rope", 1, "", 1, 0f, Mat, "fiber:4"),
        new Recipe("bandage", 1, "", 1, 0f, Eat, "cloth:2"),
        new Recipe("poultice", 1, "", 1, 0f, Eat, "herbs:2", "fiber:1"),
        new Recipe("torch", 1, "", 1, 0f, Tool, "wood:1", "cloth:1"),
        new Recipe("club", 1, "", 1, 0f, Tool, "wood:3", "scrap:1"),
        new Recipe("knife", 1, "", 2, 0f, Tool, "scrap:2", "cloth:1"),
        new Recipe("sling_bag", 1, "", 2, 0f, Gear, "cloth:4", "rope:1"),
        new Recipe("planter_kit", 1, "", 1, 0f, Base, "wood:4"),
        new Recipe("campfire_kit", 1, "", 1, 0f, Base, "wood:4", "stone:4"),
        new Recipe("chest_kit", 1, "", 2, 0f, Base, "wood:6"),
        new Recipe("workbench_kit", 1, "", 1, 0f, Base, "wood:8", "stone:4"),
        // campfire
        new Recipe("cooked_meat", 1, "campfire", 1, 2.5f, Eat, "raw_meat:1"),
        new Recipe("water", 1, "campfire", 1, 2.5f, Eat, "dirty_water:1"),
        // workbench
        new Recipe("hatchet", 1, "workbench", 2, 5f, Tool, "wood:2", "stone:3"),
        new Recipe("snare_kit", 1, "workbench", 2, 4f, Base, "rope:1", "wood:2"),
        new Recipe("lantern_kit", 1, "workbench", 2, 4f, Base, "wood:2", "scrap:1", "cloth:1"),
        new Recipe("pickaxe", 1, "workbench", 3, 6f, Tool, "wood:3", "stone:4", "scrap:1"),
        new Recipe("hammer", 1, "workbench", 3, 6f, Tool, "wood:2", "scrap:3"),
        new Recipe("spear", 1, "workbench", 3, 6f, Tool, "wood:3", "scrap:2", "rope:1"),
        new Recipe("backpack", 1, "workbench", 3, 8f, Gear, "cloth:8", "rope:2", "leather:2"),
        new Recipe("rain_kit", 1, "workbench", 3, 6f, Base, "wood:4", "cloth:3", "scrap:1"),
        new Recipe("trap_kit", 1, "workbench", 4, 8f, Base, "wood:4", "scrap:3"),
        new Recipe("leather_armor", 1, "workbench", 4, 9f, Gear, "leather:5", "rope:2", "cloth:2"),
        new Recipe("barricade_kit", 1, "workbench", 5, 10f, Base, "wood:8", "scrap:2", "rope:1"),
        new Recipe("forge_kit", 1, "workbench", 6, 14f, Base, "stone:12", "scrap:6", "wood:4"),
        // forge
        new Recipe("ingot", 1, "forge", 6, 4f, Mat, "scrap:3"),
        new Recipe("machete", 1, "forge", 6, 8f, Tool, "ingot:2", "wood:1"),
        new Recipe("kukri", 1, "forge", 6, 7f, Tool, "ingot:1", "wood:1", "leather:1"),
        new Recipe("axe", 1, "forge", 7, 10f, Tool, "ingot:3", "wood:2"),
        new Recipe("frame_pack", 1, "forge", 7, 10f, Gear, "leather:4", "ingot:2", "cloth:6"),
        new Recipe("sledge", 1, "forge", 8, 12f, Tool, "ingot:4", "wood:3"),
        new Recipe("katana", 1, "forge", 9, 16f, Tool, "ingot:4", "leather:1", "rope:1"),
        new Recipe("plate_vest", 1, "forge", 9, 14f, Gear, "ingot:5", "leather:3"),
    };

    public static readonly WeaponDef[] Weapons =
    {
        new WeaponDef { id = "torch", model = "reed-fire-torch", damage = 10f, reach = 1.9f, speed = 1.1f, light = true, length = 0.8f, grip = 0.1f },
        new WeaponDef { id = "knife", model = "wp-knife", damage = 13f, reach = 1.7f, speed = 1.6f, length = 0.26f, grip = 0.05f },
        new WeaponDef { id = "club", model = "wp-nail-bat", damage = 20f, reach = 2.0f, speed = 1f, length = 0.85f, grip = 0.08f },
        new WeaponDef { id = "hatchet", model = "hand-axe", damage = 16f, reach = 1.9f, speed = 1.1f, chop = 2f, length = 0.6f, grip = 0.08f },
        new WeaponDef { id = "pickaxe", model = "mining-pickaxe", damage = 18f, reach = 2.0f, speed = 0.9f, mine = 2f, length = 0.9f, grip = 0.1f },
        new WeaponDef { id = "hammer", model = "wp-brick-hammer", damage = 20f, reach = 1.8f, speed = 1.15f, mine = 1.6f, length = 0.38f, grip = 0.06f },
        new WeaponDef { id = "spear", model = "spear", damage = 20f, reach = 3.1f, speed = 1.1f, thrust = true, length = 2.0f, grip = 0.75f },
        new WeaponDef { id = "kukri", model = "wp-kukri", damage = 24f, reach = 1.9f, speed = 1.45f, chop = 1.2f, length = 0.42f, grip = 0.06f },
        new WeaponDef { id = "machete", model = "wp-machete", damage = 28f, reach = 2.1f, speed = 1.25f, chop = 1.5f, length = 0.68f, grip = 0.07f },
        new WeaponDef { id = "axe", model = "axe", damage = 36f, reach = 2.1f, speed = 0.72f, chop = 3f, length = 0.95f, grip = 0.1f },
        new WeaponDef { id = "katana", model = "wp-katana", damage = 42f, reach = 2.4f, speed = 1.3f, length = 1.0f, grip = 0.1f },
        new WeaponDef { id = "sledge", model = "war-hammer", damage = 46f, reach = 2.2f, speed = 0.6f, mine = 3f, length = 1.1f, grip = 0.12f },
    };

    public static WeaponDef Weapon(string id)
    {
        foreach (var w in Weapons) if (w.id == id) return w;
        return null;
    }

    static RunData Run => RunManager.Instance != null ? RunManager.Instance.Current : null;

    // ------------------------------------------------------------------ weight and worn gear

    public const float BaseCapacity = 30f;

    /// <summary>Kilograms of everything carried.</summary>
    public static float CarriedWeight
    {
        get
        {
            float w = 0f;
            foreach (var d in Defs) w += Count(d.id) * d.weight;
            return w;
        }
    }

    /// <summary>The best pack you carry is the one you wear.</summary>
    public static float PackBonus => Count("frame_pack") > 0 ? 60f : Count("backpack") > 0 ? 35f : Count("sling_bag") > 0 ? 15f : 0f;
    public static string PackName => Count("frame_pack") > 0 ? "Frame pack" : Count("backpack") > 0 ? "Backpack" : Count("sling_bag") > 0 ? "Sling bag" : "No pack";

    public static float Capacity => BaseCapacity + PackBonus + Progress.Stat("carry") * 8f;

    /// <summary>1 = at the limit. Above 1 you slow down; above 1.3 you can barely move.</summary>
    public static float Load => CarriedWeight / Mathf.Max(1f, Capacity);

    /// <summary>Share of damage stopped by the best armour carried.</summary>
    public static float Armor => Count("plate_vest") > 0 ? 0.4f : Count("leather_armor") > 0 ? 0.2f : 0f;
    public static string ArmorName => Count("plate_vest") > 0 ? "Plated vest" : Count("leather_armor") > 0 ? "Leather jerkin" : "No armour";

    // ------------------------------------------------------------------ weapons and kits

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
        Hud.Toast(run.equipped == "" ? "Fists" : "In hand: " + Name(run.equipped));
        Sfx.Play("click");
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
    public static readonly string[] BuildKits =
        { "planter_kit", "campfire_kit", "chest_kit", "workbench_kit", "lantern_kit", "rain_kit", "snare_kit", "trap_kit", "barricade_kit", "forge_kit" };
    public static int SelectedIndex;
    public static string SelectedKit => BuildKits[SelectedIndex];

    public static void CycleBuild()
    {
        for (int i = 1; i <= BuildKits.Length; i++)
        {
            int idx = (SelectedIndex + i) % BuildKits.Length;
            if (Count(BuildKits[idx]) > 0) { SelectedIndex = idx; Hud.Toast("Selected: " + Name(SelectedKit)); Sfx.Play("click"); return; }
        }
        Hud.Toast("You carry no kits. Craft one with " + Keybinds.Describe(GameAction.Inventory) + ".");
    }

    // ------------------------------------------------------------------ counts

    public static ItemDef Def(string id) => Defs.Find(d => d.id == id);
    public static string Name(string id) { var d = Def(id); return d != null ? d.name : id; }

    /// <summary>How many you carry.</summary>
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

    public static int Stashed(string id)
    {
        var run = Run;
        if (run == null) return 0;
        var s = run.stash.Find(x => x.id == id);
        return s != null ? s.count : 0;
    }

    /// <summary>The stash is in reach on the roof once a chest has been built.</summary>
    public static bool StashInReach => RoofZone.PlayerOnRoof && BaseStructure.Nearest(Vector3.zero, 200f, "chest") != null;

    /// <summary>What crafting can draw on: what you carry, plus the stash when it is in reach.</summary>
    public static int Have(string id) { return Count(id) + (StashInReach ? Stashed(id) : 0); }

    public static void Add(string id, int n)
    {
        var run = Run;
        if (run == null || n == 0) return;

        if (id == "food") run.food = Mathf.Max(0, run.food + n);
        else if (id == "water") run.water = Mathf.Max(0, run.water + n);
        else if (id == "wood") run.wood = Mathf.Max(0, run.wood + n);
        else if (id == "scrap") run.scrap = Mathf.Max(0, run.scrap + n);
        else AddTo(run.items, id, n);

        // The first weapon you get goes straight into your hand.
        if (n > 0 && Weapon(id) != null && string.IsNullOrEmpty(run.equipped)) run.equipped = id;
        OnChanged?.Invoke();
    }

    static void AddTo(List<ItemStack> list, string id, int n)
    {
        var s = list.Find(x => x.id == id);
        if (s == null)
        {
            s = new ItemStack { id = id, count = 0 };
            list.Add(s);
        }
        s.count += n;
        if (s.count <= 0) list.Remove(s);
    }

    public static bool Remove(string id, int n)
    {
        if (Count(id) < n) return false;
        Add(id, -n);
        return true;
    }

    /// <summary>Moves items between you and the stash. n below 0 takes them out.</summary>
    public static void Store(string id, int n)
    {
        var run = Run;
        if (run == null || n == 0) return;
        if (n > 0) { n = Mathf.Min(n, Count(id)); if (n <= 0) return; Add(id, -n); AddTo(run.stash, id, n); }
        else { n = Mathf.Min(-n, Stashed(id)); if (n <= 0) return; AddTo(run.stash, id, -n); Add(id, n); }
        OnChanged?.Invoke();
    }

    /// <summary>Puts every raw material you carry into the stash.</summary>
    public static void StoreAllMaterials()
    {
        foreach (var d in Defs) if (d.category == Mat) Store(d.id, Count(d.id));
        Hud.Toast("Materials stored.");
    }

    // ------------------------------------------------------------------ crafting

    public static bool HasInputs(Recipe r)
    {
        foreach (var inp in r.inputs)
        {
            var p = inp.Split(':');
            if (Have(p[0]) < int.Parse(p[1])) return false;
        }
        return true;
    }

    public static bool Unlocked(Recipe r) => Progress.Level >= r.level;

    /// <summary>Takes the inputs, from what you carry first and then from the stash. False (nothing taken) if any are missing.</summary>
    public static bool PayFor(Recipe r)
    {
        if (!HasInputs(r)) return false;
        var run = Run;
        foreach (var inp in r.inputs)
        {
            var p = inp.Split(':');
            int need = int.Parse(p[1]);
            int fromPack = Mathf.Min(need, Count(p[0]));
            if (fromPack > 0) Add(p[0], -fromPack);
            if (need > fromPack) AddTo(run.stash, p[0], -(need - fromPack));
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
        Progress.Gain(8 + r.level * 4);
        Tutorial.Notify("craft");
        Sfx.Play("craft_done");
        Hud.Toast("Made " + Name(r.output) + (r.count > 1 ? " x" + r.count : "") + ".");
    }

    // ------------------------------------------------------------------ eating, drinking, healing

    /// <summary>Uses one of an item. Returns what happened, or "" if it is not something you use.</summary>
    public static string Consume(string id)
    {
        var p = PlayerController.Instance;
        var needs = Needs.Instance;
        if (p == null || Count(id) <= 0) return "";
        switch (id)
        {
            case "bandage": Remove(id, 1); p.Heal(35f); Sfx.Play("cloth"); return "Bandaged (+35 health).";
            case "poultice": Remove(id, 1); p.Heal(20f); Sfx.Play("cloth"); return "Poultice applied (+20 health).";
            case "food": Remove(id, 1); needs?.Eat(30f); Sfx.Play("eat"); return "Ate rations (+30 hunger).";
            case "cooked_meat": Remove(id, 1); needs?.Eat(50f); Sfx.Play("eat"); return "Ate cooked meat (+50 hunger).";
            case "raw_meat": Remove(id, 1); needs?.Eat(15f); p.health = Mathf.Max(1f, p.health - 6f); Sfx.Play("eat"); return "Ate raw meat. It sits badly (-6 health).";
            case "water": Remove(id, 1); needs?.Drink(40f); Sfx.Play("drink"); return "Drank clean water (+40 thirst).";
            case "dirty_water":
                Remove(id, 1); needs?.Drink(25f); Sfx.Play("drink");
                if (UnityEngine.Random.value < 0.5f) { p.health = Mathf.Max(1f, p.health - 10f); return "Drank lake water. It made you ill (-10 health)."; }
                return "Drank lake water (+25 thirst).";
        }
        return "";
    }

    public static bool Usable(string id) => id == "bandage" || id == "poultice" || id == "food" || id == "cooked_meat" || id == "raw_meat" || id == "water" || id == "dirty_water";

    /// <summary>Use key: whatever you need most. Wounds first, then the lower of hunger and thirst.</summary>
    public static string UseBest()
    {
        var p = PlayerController.Instance;
        var needs = Needs.Instance;
        if (p == null) return "";
        if (p.health < p.maxHealth * 0.6f)
        {
            if (Count("bandage") > 0) return Consume("bandage");
            if (Count("poultice") > 0) return Consume("poultice");
        }
        if (needs != null)
        {
            bool thirsty = needs.Thirst < 80f, hungry = needs.Hunger < 80f;
            if (thirsty && (!hungry || needs.Thirst <= needs.Hunger))
            {
                if (Count("water") > 0) return Consume("water");
                if (Count("dirty_water") > 0) return Consume("dirty_water");
            }
            if (hungry)
            {
                if (Count("cooked_meat") > 0) return Consume("cooked_meat");
                if (Count("food") > 0) return Consume("food");
                if (Count("raw_meat") > 0) return Consume("raw_meat");
            }
            if (thirsty)
            {
                if (Count("water") > 0) return Consume("water");
                if (Count("dirty_water") > 0) return Consume("dirty_water");
            }
        }
        if (p.health < p.maxHealth)
        {
            if (Count("poultice") > 0) return Consume("poultice");
            if (Count("bandage") > 0) return Consume("bandage");
        }
        return "Nothing you need right now, or nothing to use.";
    }
}

/// <summary>
/// Player level and the points it gives. Experience comes from gathering, searching, crafting, building,
/// fighting and surviving. Each level unlocks recipes and gives one point to put into a stat:
/// vitality (+10 health), strength (+8% damage), carry (+8 kg), endurance (+15 stamina, slower hunger and thirst).
/// </summary>
public static class Progress
{
    static RunData Run => RunManager.Instance != null ? RunManager.Instance.Current : null;

    public static readonly string[] StatIds = { "vitality", "strength", "carry", "endurance" };
    public static readonly string[] StatNames = { "Vitality", "Strength", "Carrying", "Endurance" };
    public static readonly string[] StatHelp = { "+10 health each", "+8% melee damage each", "+8 kg carried each", "+15 stamina, 5% slower hunger and thirst each" };

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

    public static int Stat(string id)
    {
        var run = Run;
        if (run == null) return 0;
        switch (id)
        {
            case "vitality": return run.statVitality;
            case "strength": return run.statStrength;
            case "carry": return run.statCarry;
            case "endurance": return run.statEndurance;
        }
        return 0;
    }

    /// <summary>Points not yet spent: one per level gained.</summary>
    public static int Points
    {
        get
        {
            var run = Run;
            if (run == null) return 0;
            return Level - 1 - (run.statVitality + run.statStrength + run.statCarry + run.statEndurance);
        }
    }

    public static bool Spend(string id)
    {
        var run = Run;
        if (run == null || Points <= 0) return false;
        switch (id)
        {
            case "vitality": run.statVitality++; break;
            case "strength": run.statStrength++; break;
            case "carry": run.statCarry++; break;
            case "endurance": run.statEndurance++; break;
            default: return false;
        }
        var p = PlayerController.Instance;
        if (p != null) p.ApplyStats(true);
        Sfx.Play("levelup");
        return true;
    }

    public static float DamageMult => 1f + Stat("strength") * 0.08f;

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
        Sfx.Play("levelup");
        Hud.Banner("Level " + after + "!  A stat point to spend (" + Keybinds.Describe(GameAction.Inventory) + ")."
                   + (unlocked.Count > 0 ? "\nNew: " + string.Join(", ", unlocked) : ""), 7f);
    }
}
