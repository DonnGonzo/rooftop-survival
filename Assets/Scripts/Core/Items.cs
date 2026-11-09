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
    public string[] inputs;   // "itemId:amount"

    public Recipe(string output, int count, params string[] inputs)
    {
        this.output = output;
        this.count = count;
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

/// <summary>
/// Inventory and crafting. Food, water, wood and scrap live in RunData (they are used by the
/// roof systems); everything else is stored as ItemStacks in RunData.items so it saves with the run.
/// </summary>
public static class Items
{
    public static event Action OnChanged;

    public static readonly List<ItemDef> Defs = new List<ItemDef>
    {
        new ItemDef("wood", "Wood", "Building material for clubs and trap kits."),
        new ItemDef("scrap", "Scrap metal", "Used in most crafted items."),
        new ItemDef("food", "Food", "Press Q to eat and recover 10 health."),
        new ItemDef("water", "Water", "Waters the planters on the roof."),
        new ItemDef("cloth", "Cloth", "Rags from the street. Makes bandages and rope."),
        new ItemDef("rope", "Rope", "Reserved for a reinforced grapple later."),
        new ItemDef("bandage", "Bandage", "Press Q to recover 30 health."),
        new ItemDef("trap_kit", "Trap kit", "Press T on the roof to place a spike trap (+10 defense)."),
        new ItemDef("club", "Club", "Melee weapon. Equipped automatically while you carry it."),
    };

    public static readonly Recipe[] Recipes =
    {
        new Recipe("bandage", 1, "cloth:2"),
        new Recipe("rope", 1, "cloth:2", "scrap:1"),
        new Recipe("trap_kit", 1, "scrap:2", "wood:1"),
        new Recipe("club", 1, "wood:3", "scrap:1"),
    };

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

        if (id == "food") run.food += n;
        else if (id == "water") run.water += n;
        else if (id == "wood") run.wood += n;
        else if (id == "scrap") run.scrap += n;
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
        OnChanged?.Invoke();
    }

    public static bool Remove(string id, int n)
    {
        if (Count(id) < n) return false;
        Add(id, -n);
        return true;
    }

    public static bool CanCraft(Recipe r)
    {
        foreach (var inp in r.inputs)
        {
            var p = inp.Split(':');
            if (Count(p[0]) < int.Parse(p[1])) return false;
        }
        return true;
    }

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

    /// <summary>Q: use a bandage first, then food. Returns a message for the HUD.</summary>
    public static string UseBest()
    {
        var player = PlayerController.Instance;
        if (player == null) return "";
        if (player.health >= player.maxHealth) return "You're already at full health.";
        if (Count("bandage") > 0)
        {
            Remove("bandage", 1);
            player.Heal(30f);
            return "Used a bandage (+30 health).";
        }
        if (Count("food") > 0)
        {
            Remove("food", 1);
            player.Heal(10f);
            return "Ate some food (+10 health).";
        }
        return "Nothing to use. Craft a bandage from cloth (Tab).";
    }
}
