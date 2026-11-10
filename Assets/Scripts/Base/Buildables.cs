using UnityEngine;

/// <summary>
/// Builds the things the player crafts as kits: planter, snare, spike trap, barricade.
/// The same code rebuilds them from the save file when a run is loaded.
/// </summary>
public static class Buildables
{
    /// <summary>Maps a kit item to the structure it becomes. Null if the item is not a kit.</summary>
    public static string StructureFor(string kit)
    {
        switch (kit)
        {
            case "planter_kit": return "planter";
            case "workbench_kit": return "workbench";
            case "campfire_kit": return "campfire";
            case "chest_kit": return "chest";
            case "lantern_kit": return "lantern";
            case "rain_kit": return "rain";
            case "forge_kit": return "forge";
            case "snare_kit": return "snare";
            case "trap_kit": return "spike_trap";
            case "barricade_kit": return "barricade";
        }
        return null;
    }

    /// <summary>Builds a structure at a world position. Returns null for an unknown id.</summary>
    public static BaseStructure Spawn(string id, Vector3 pos, float yaw)
    {
        GameObject go;
        float durability, defense;
        switch (id)
        {
            case "planter":
                go = RoofGenerator.SpawnPlanterAt(pos, yaw);
                durability = 40f; defense = 0f;
                break;

            case "workbench":
                go = ModelLibrary.Spawn("field-workbench", null, pos, yaw, fitFootprint: 2.0f, addCollider: true, keepAnimations: false)
                     ?? ModelLibrary.Spawn("upgradeable-assembly-workbench", null, pos, yaw, fitFootprint: 2.0f, addCollider: true, keepAnimations: false)
                     ?? Placeholder(pos, yaw, new Vector3(2f, 0.9f, 0.9f), new Color(0.55f, 0.42f, 0.28f));
                durability = 80f; defense = 0f;
                go.AddComponent<CraftStation>().kind = "workbench";
                break;

            case "campfire":
                go = ModelLibrary.Spawn("open-campfire", null, pos, yaw, fitFootprint: 1.3f, keepAnimations: true)
                     ?? Placeholder(pos, yaw, new Vector3(1f, 0.3f, 1f), new Color(0.3f, 0.2f, 0.15f));
                ModelLibrary.PlayClip(go, "tick");
                durability = 40f; defense = 0f;
                go.AddComponent<FireFlicker>();
                go.AddComponent<CraftStation>().kind = "campfire";
                break;

            case "chest":
                go = ModelLibrary.Spawn("treasure-chest", null, pos, yaw, fitFootprint: 1.3f, addCollider: true, keepAnimations: false)
                     ?? Placeholder(pos, yaw, new Vector3(1.2f, 0.8f, 0.7f), new Color(0.5f, 0.36f, 0.2f));
                durability = 60f; defense = 0f;
                ActionSpot.Add(go, 2.8f, () => "Open storage chest", () => InventoryUI.Show(InventoryUI.Tab.Storage));
                break;

            case "lantern":
            {
                go = ModelLibrary.Spawn("cemetery-lantern-post", null, pos, yaw, fitHeight: 2.6f, addCollider: false, keepAnimations: false)
                     ?? ModelLibrary.Spawn("camping-lantern", null, pos, yaw, fitHeight: 0.5f, keepAnimations: false)
                     ?? Placeholder(pos, yaw, new Vector3(0.2f, 2.2f, 0.2f), new Color(0.3f, 0.3f, 0.3f));
                durability = 20f; defense = 0f;
                var lg = new GameObject("LanternLight");
                lg.transform.SetParent(go.transform, false);
                lg.transform.position = pos + Vector3.up * 2.2f;
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.range = 13f; l.intensity = 1.4f; l.color = new Color(1f, 0.82f, 0.55f);
                break;
            }

            case "rain":
                go = ModelLibrary.Spawn("hide-rain-catchment-funnel", null, pos, yaw, fitFootprint: 1.8f, addCollider: true, keepAnimations: false)
                     ?? Placeholder(pos, yaw, new Vector3(1.2f, 1f, 1.2f), new Color(0.35f, 0.45f, 0.55f));
                durability = 30f; defense = 0f;
                go.AddComponent<RainCollector>().range = 2.8f;
                break;

            case "forge":
                go = ModelLibrary.Spawn("blacksmith-forge", null, pos, yaw, fitFootprint: 2.2f, addCollider: true, keepAnimations: false)
                     ?? ModelLibrary.Spawn("smelting-furnace", null, pos, yaw, fitFootprint: 2.0f, addCollider: true, keepAnimations: false)
                     ?? Placeholder(pos, yaw, new Vector3(1.6f, 1.4f, 1.6f), new Color(0.4f, 0.4f, 0.42f));
                durability = 120f; defense = 0f;
                go.AddComponent<CraftStation>().kind = "forge";
                go.AddComponent<FireFlicker>().baseIntensity = 1.0f;
                break;

            case "snare":
                go = ModelLibrary.Spawn("rs-snare", null, pos, yaw, fitHeight: 1.1f, keepAnimations: false)
                     ?? Placeholder(pos, yaw, new Vector3(1.2f, 0.08f, 1.2f), new Color(0.55f, 0.5f, 0.35f));
                durability = 30f; defense = 3f;
                Trap.Attach(go, radius: 1.3f, hitDamage: 14f, wearPerCatch: 10f, slow: 3f);
                break;

            case "spike_trap":
                go = ModelLibrary.Spawn("rs-spike-trap", null, pos, yaw, fitFootprint: 1.9f, keepAnimations: false)
                     ?? Placeholder(pos, yaw, new Vector3(1.6f, 0.3f, 1.6f), new Color(0.4f, 0.26f, 0.15f));
                durability = 70f; defense = 10f;
                Trap.Attach(go, radius: 1.5f, hitDamage: 22f, wearPerCatch: 7f, slow: 0f);
                break;

            case "barricade":
                go = ModelLibrary.Spawn("rs-barricade", null, pos, yaw, fitFootprint: 2.6f, addCollider: true, keepAnimations: false)
                     ?? Placeholder(pos, yaw, new Vector3(2.4f, 1.2f, 0.4f), new Color(0.45f, 0.3f, 0.18f));
                durability = 140f; defense = 18f;
                break;

            default:
                return null;
        }

        go.name = "Built_" + id;
        var hp = go.AddComponent<Health>();
        hp.max = durability;
        hp.current = durability;
        hp.dropCount = 0;
        hp.dropItem = null;
        hp.breakOnDeath = true;
        hp.breakMessage = Label(id) + " broke.";
        hp.barRange = 7f;
        hp.barOnlyWhenHurt = true;

        var s = go.AddComponent<BaseStructure>();
        s.id = id;
        s.defense = defense;
        s.hp = hp;
        return s;
    }

    public static string Label(string id)
    {
        switch (id)
        {
            case "planter": return "Planter";
            case "workbench": return "Workbench";
            case "campfire": return "Campfire";
            case "chest": return "Storage chest";
            case "lantern": return "Lantern";
            case "rain": return "Rain collector";
            case "forge": return "Forge";
            case "snare": return "Snare";
            case "spike_trap": return "Spike trap";
            case "barricade": return "Barricade";
        }
        return id;
    }

    /// <summary>Player action: place one kit in front of them. Returns false if it is not a kit.</summary>
    public static bool Place(string kit, Vector3 pos, float yaw)
    {
        string id = StructureFor(kit);
        if (id == null) return false;
        var s = Spawn(id, pos, yaw);
        if (s == null) return false;
        Tutorial.Notify(id == "planter" ? "planter" : id == "workbench" ? "bench" : "trap");
        Progress.Gain(6);
        Sfx.PlayAt("place", pos, 1f, 20f);
        Hud.Toast(Label(id) + " placed" + (s.defense > 0f ? " (+" + s.defense.ToString("F0") + " defense)." : "."));
        if (RunManager.Instance != null) RunManager.Instance.Save();
        return true;
    }

    /// <summary>Rebuilds everything saved with the run. Called once after the roof is generated.</summary>
    public static void Restore(RunData run)
    {
        if (run == null) return;
        foreach (var saved in run.structures)
        {
            var s = Spawn(saved.id, saved.pos, saved.yRot);
            if (s == null) continue;
            if (saved.health > 0f && s.hp != null) s.hp.current = Mathf.Min(s.hp.max, saved.health);
            var plot = s.GetComponent<FarmPlot>();
            if (plot != null) plot.SetState(saved.stage, saved.watered);
            var station = s.GetComponent<CraftStation>();
            if (station != null) { station.orderId = saved.order ?? ""; station.done = saved.orderDone; station.orderCount = Mathf.Max(1, saved.orderCount); }
            var rain = s.GetComponent<RainCollector>();
            if (rain != null) rain.stored = Mathf.Max(0, saved.stage);
        }
    }

    /// <summary>Stand-in box when a model is missing. It sits on the roof, not inside it.</summary>
    static GameObject Placeholder(Vector3 pos, float yaw, Vector3 size, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = pos + Vector3.up * (size.y / 2f + 0.02f);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = size;
        Look.Paint(go, "planks", c, 1f);
        return go;
    }
}
