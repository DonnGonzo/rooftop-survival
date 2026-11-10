using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The forest around the city, generated from the run seed: trees in thick and thin stands, undergrowth,
/// rocks and ore, a lake, old cabins and camps, and the dens the wildlife comes from. Dirt tracks lead out
/// from the city's roads. Trees give wood, rocks stone, ore scrap; bushes, ferns and reeds can be picked.
/// </summary>
public static class ForestBuilder
{
    static readonly string[] Trees = { "tall-pine-tree", "tall-pine-tree", "young-pine", "oak-tree", "broadleaf-oak", "birch-tree", "maple-tree", "front-yard-fir-tree" };
    static readonly float[] TreeMin = { 11f, 11f, 5f, 9f, 9f, 8f, 8f, 6f };
    static readonly float[] TreeMax = { 17f, 17f, 8f, 13f, 12f, 11f, 11f, 10f };
    static readonly string[] Bushes = { "leafy-bush", "round-bush", "bush" };
    static readonly string[] Rocks = { "medium-rock", "large-boulder", "rock-outcrop", "fern-covered-boulder", "boulder-field-cluster" };

    struct Clearing { public Vector3 pos; public float r; }
    static readonly List<Clearing> clearings = new List<Clearing>();
    static Transform root;
    static float Y => StreetBuilder.StreetY;
    static float Inner => StreetBuilder.CityHalf + 8f;
    static float Outer => StreetBuilder.WorldHalf - 12f;

    public static void Build(int seed, Transform parent)
    {
        root = parent;
        clearings.Clear();
        var rng = new System.Random(seed + 77);
        float off = (seed % 1000) * 0.37f;

        Tracks();
        Lake(rng);
        Places(rng);

        // Trees: more where the noise is high, none on tracks, in clearings or in the lake.
        int placed = 0;
        for (int n = 0; n < 14000 && placed < 2300; n++)
        {
            Vector3 p = RandomPoint(rng);
            float density = Mathf.PerlinNoise(p.x * 0.012f + off, p.z * 0.012f + off);
            if (rng.NextDouble() > Mathf.SmoothStep(0.22f, 0.62f, density) + 0.1f) continue;
            if (!Free(p, 1.5f)) continue;
            int k = rng.Next(Trees.Length);
            // Pines on the high noise, broadleaves on the low, so stands look like stands.
            if (density > 0.6f && rng.NextDouble() < 0.6) k = rng.Next(3);
            StreetBuilder.Tree(Trees[k], p, Mathf.Lerp(TreeMin[k], TreeMax[k], (float)rng.NextDouble()), rng, root, false);
            placed++;
        }

        // Undergrowth.
        for (int n = 0; n < 420; n++)
        {
            Vector3 p = RandomPoint(rng);
            if (!Free(p, 1.5f)) continue;
            StreetBuilder.Prop(Bushes[rng.Next(Bushes.Length)], p, (float)rng.NextDouble() * 360f, Mathf.Lerp(0.8f, 1.7f, (float)rng.NextDouble()), 0f, false, root);
        }
        for (int n = 0; n < 60; n++)
        {
            Vector3 p = RandomPoint(rng);
            if (Free(p, 1.5f)) StreetBuilder.BerryBush(p, rng, root);
        }
        for (int n = 0; n < 190; n++)
        {
            Vector3 p = RandomPoint(rng);
            if (!Free(p, 1f)) continue;
            string key = n % 3 == 0 ? "fern-clump" : n % 3 == 1 ? "fern-plant" : "grass-tuft";
            StreetBuilder.FiberPlant(key, p, Mathf.Lerp(0.5f, 1.0f, (float)rng.NextDouble()), rng, root);
        }
        for (int n = 0; n < 45; n++)
        {
            Vector3 p = RandomPoint(rng);
            if (!Free(p, 1f)) continue;
            var m = StreetBuilder.Prop("mushroom-cluster", p, (float)rng.NextDouble() * 360f, Mathf.Lerp(0.3f, 0.5f, (float)rng.NextDouble()), 0f, false, root);
            if (m != null) { var c = Container.Add(m, "Pick mushrooms", 1.2f, 3, new LootRoll("food", 1, 1)); c.range = 2.2f; c.xp = 3; }
        }
        for (int n = 0; n < 70; n++)
        {
            Vector3 p = RandomPoint(rng);
            if (Free(p, 1f)) StreetBuilder.Prop("wildflower-plant", p, (float)rng.NextDouble() * 360f, Mathf.Lerp(0.3f, 0.6f, (float)rng.NextDouble()), 0f, false, root);
        }

        // Stone, ore, and fallen wood.
        for (int n = 0; n < 150; n++)
        {
            Vector3 p = RandomPoint(rng);
            if (!Free(p, 3f)) continue;
            StreetBuilder.Rock(Rocks[rng.Next(Rocks.Length)], p, Mathf.Lerp(1.4f, 4.5f, (float)(rng.NextDouble() * rng.NextDouble()) + 0.1f), rng, root, ResourceNode.Kind.Rock);
        }
        for (int n = 0; n < 26; n++)
        {
            Vector3 p = RandomPoint(rng);
            if (!Free(p, 3f)) continue;
            StreetBuilder.Rock(n % 2 == 0 ? "iron-ore-deposit" : "harvestable-flint-seam", p, Mathf.Lerp(1.8f, 2.8f, (float)rng.NextDouble()), rng, root, ResourceNode.Kind.Ore);
        }
        for (int n = 0; n < 70; n++)
        {
            Vector3 p = RandomPoint(rng);
            if (!Free(p, 2f)) continue;
            string key = n % 4 == 0 ? "bracket-fungus-log" : n % 4 == 1 ? "driftwood-log" : n % 4 == 2 ? "tree-stump" : "moss-covered-stump";
            var log = StreetBuilder.Prop(key, p, (float)rng.NextDouble() * 360f, 0f, Mathf.Lerp(0.9f, 2.4f, (float)rng.NextDouble()), true, root);
            if (log != null) ResourceNode.Add(log, ResourceNode.Kind.Tree, 2, 4);
        }

        Hills(rng);
    }

    static Vector3 RandomPoint(System.Random rng)
    {
        for (int tries = 0; tries < 20; tries++)
        {
            float x = Mathf.Lerp(-Outer, Outer, (float)rng.NextDouble());
            float z = Mathf.Lerp(-Outer, Outer, (float)rng.NextDouble());
            if (Mathf.Abs(x) < Inner && Mathf.Abs(z) < Inner) continue;   // that's the city
            return new Vector3(x, Y, z);
        }
        return new Vector3(Outer, Y, Outer);
    }

    /// <summary>True if nothing claims this spot: no clearing, no track.</summary>
    static bool Free(Vector3 p, float r)
    {
        foreach (var c in clearings)
            if ((c.pos - p).sqrMagnitude < (c.r + r) * (c.r + r)) return false;
        // Tracks run out along the two middle road lines on each axis.
        float half = StreetBuilder.Pitch / 2f;
        if (Mathf.Abs(Mathf.Abs(p.x) - half) < 3.5f + r && Mathf.Abs(p.z) > Inner - 4f) return false;
        if (Mathf.Abs(Mathf.Abs(p.z) - half) < 3.5f + r && Mathf.Abs(p.x) > Inner - 4f) return false;
        return true;
    }

    /// <summary>Dirt tracks continuing the city's middle roads out to the edge of the world.</summary>
    static void Tracks()
    {
        var dirt = Look.Mat("dirt", new Color(0.52f, 0.44f, 0.33f), 5f);
        float half = StreetBuilder.Pitch / 2f;
        float len = Outer - StreetBuilder.CityHalf + 10f;
        float mid = StreetBuilder.CityHalf + len / 2f - 2f;
        foreach (float side in new[] { -1f, 1f })
            foreach (float line in new[] { -half, half })
            {
                Look.Box("Track", root, new Vector3(line, Y - 0.24f, side * mid), new Vector3(5.5f, 0.5f, len), dirt, false);
                Look.Box("Track", root, new Vector3(side * mid, Y - 0.235f, line), new Vector3(len, 0.5f, 5.5f), dirt, false);
            }
    }

    static Vector3 Claim(System.Random rng, float r, float minFromCity, float maxFromCity)
    {
        for (int tries = 0; tries < 60; tries++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float d = StreetBuilder.CityHalf * 1.25f + Mathf.Lerp(minFromCity, maxFromCity, (float)rng.NextDouble()) * 0.62f;
            Vector3 p = new Vector3(Mathf.Cos(a) * d, Y, Mathf.Sin(a) * d);
            if (Mathf.Abs(p.x) > Outer - r || Mathf.Abs(p.z) > Outer - r) continue;
            if (Mathf.Abs(p.x) < Inner + r && Mathf.Abs(p.z) < Inner + r) continue;
            if (!Free(p, r + 8f)) continue;
            clearings.Add(new Clearing { pos = p, r = r });
            return p;
        }
        return Vector3.zero;
    }

    static void Lake(System.Random rng)
    {
        float r = Mathf.Lerp(26f, 40f, (float)rng.NextDouble());
        Vector3 p = Claim(rng, r + 6f, 60f, 190f);
        if (p == Vector3.zero) return;

        var water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        water.name = "Lake";
        Object.Destroy(water.GetComponent<Collider>());
        water.transform.SetParent(root, false);
        water.transform.position = p + Vector3.up * 0.03f;
        water.transform.localScale = new Vector3(r * 2f, 0.02f, r * 1.7f);
        Look.Paint(water, "plain", new Color(0.16f, 0.36f, 0.46f), 2f, 0.92f);
        var shore = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shore.name = "LakeShore";
        Object.Destroy(shore.GetComponent<Collider>());
        shore.transform.SetParent(root, false);
        shore.transform.position = p + Vector3.up * 0.012f;
        shore.transform.localScale = new Vector3(r * 2f + 6f, 0.02f, r * 1.7f + 6f);
        Look.Paint(shore, "dirt", new Color(0.62f, 0.56f, 0.42f), 4f);

        for (int n = 0; n < 26; n++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 at = p + new Vector3(Mathf.Cos(a) * (r + 1f), 0f, Mathf.Sin(a) * (r * 0.85f + 1f));
            StreetBuilder.FiberPlant(n % 2 == 0 ? "cattail-reed" : "river-reed-cluster", at, Mathf.Lerp(1.0f, 1.8f, (float)rng.NextDouble()), rng, root);
        }
        var dock = StreetBuilder.Prop("lake-dock-run", p + new Vector3(r - 2f, 0.05f, 0f), 90f, 0f, 6f, true, root);
        GameObject fill = dock != null ? dock : water;
        var c = Container.Add(fill, "Fill your bottles at the lake", 2.5f, 1, new LootRoll("water", 4, 6));
        c.range = dock != null ? 5f : r + 2f;

        SpawnDirector.Add(p + new Vector3(-r - 3f, 0f, 0f), "Lake shore", false, 4, false, "frog-base", 1, "forest-deer", 1);
        SpawnDirector.Add(p + new Vector3(0f, 0f, r * 0.85f + 3f), "Crocodile bank", true, 2, false, "crocodile-base", 4);
    }

    /// <summary>Cabins, camps, meadows and dens, each in its own clearing.</summary>
    static void Places(System.Random rng)
    {
        // Somewhere to loot.
        for (int n = 0; n < 3; n++)
        {
            Vector3 p = Claim(rng, 12f, 30f, 210f);
            if (p == Vector3.zero) continue;
            StreetBuilder.Prop(n % 2 == 0 ? "log-cabin" : "porch-log-cabin", p, (float)rng.NextDouble() * 360f, 0f, 9f, true, root);
            StreetBuilder.Prop("stone-ring-campfire", p + new Vector3(7f, 0f, 2f), 0f, 0f, 1.6f, false, root);
            StreetBuilder.Prop("heavy-timber-picnic-table", p + new Vector3(7f, 0f, -3f), 30f, 0f, 2f, true, root);
            Crate("wooden-barrel", p + new Vector3(-6.5f, 0f, 1f), "Check barrel", new LootRoll("water", 1, 3, 0.8f));
            Crate("supply-crate", p + new Vector3(-6.5f, 0f, -1f), "Open supply crate", new LootRoll("food", 1, 3), new LootRoll("bandage", 1, 1, 0.5f));
            Crate("log-store", p + new Vector3(0f, 0f, 7.5f), "Take firewood", new LootRoll("wood", 3, 6));
        }
        for (int n = 0; n < 4; n++)
        {
            Vector3 p = Claim(rng, 8f, 20f, 220f);
            if (p == Vector3.zero) continue;
            StreetBuilder.Prop(n % 2 == 0 ? "camp-tent" : "canvas-ridge-tent", p, (float)rng.NextDouble() * 360f, 0f, 3.2f, true, root);
            StreetBuilder.Prop("stone-ring-campfire", p + new Vector3(3.5f, 0f, 0f), 0f, 0f, 1.4f, false, root);
            Crate("adventurer-s-backpack", p + new Vector3(2.5f, 0f, 2.5f), "Search backpack", new LootRoll("food", 1, 2, 0.7f), new LootRoll("rope", 1, 1, 0.5f), new LootRoll("bandage", 1, 1, 0.4f));
            Crate("salvage-crate", p + new Vector3(-3f, 0f, 1.5f), "Search salvage crate", new LootRoll("scrap", 2, 4), new LootRoll("cloth", 1, 2, 0.5f));
        }

        // Game.
        for (int n = 0; n < 4; n++)
        {
            Vector3 p = Claim(rng, 14f, 10f, 230f);
            if (p == Vector3.zero) continue;
            if (n == 0) SpawnDirector.Add(p, "Farmstead", false, 5, false, "chicken-base", 1, "pig-base", 1, "goat-base", 2);
            else SpawnDirector.Add(p, "Meadow", false, 5, false, "rabbit-base", 1, "mountain-hare-base", 1, "forest-deer", 1, "sheep-base", 2);
            for (int k = 0; k < 6; k++)
                StreetBuilder.Prop("wildflower-plant", p + new Vector3(Mathf.Lerp(-10f, 10f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-10f, 10f, (float)rng.NextDouble())), 0f, 0.5f, 0f, false, root);
            if (n == 0) StreetBuilder.Prop("split-rail-fence-run", p + new Vector3(0f, 0f, 9f), 0f, 0f, 6f, true, root);
        }

        // Predators, nearest-and-mildest first so the walk out from the city gets worse the further you go.
        Den(rng, 10f, 0f, 70f, "Fox earth", "tree-stump", 1.6f, 3, false, "red-fox", 1);
        Den(rng, 12f, 50f, 140f, "Wolf cave", "smuggler-cave-mouth", 7f, 4, false, "wolf-base", 2, "black-bear", 5);
        Den(rng, 12f, 40f, 150f, "Spider thicket", "bone-scatter", 3f, 4, false, "garden-orb-weaver-base", 3, "tarantula-base", 6);
        Den(rng, 12f, 80f, 190f, "Raptor nest", "dinosaur-nest", 5f, 4, false, "compsognathus-base", 3, "velociraptor-base", 7);
        Den(rng, 12f, 90f, 200f, "Brood nest", "brood-nest", 5f, 4, false, "stone-centipede-base", 4, "alien-hive-bug-base", 5, "praying-mantis-base", 6);
        Den(rng, 14f, 120f, 230f, "Lion rock", "rock-outcrop", 8f, 3, false, "lion-base", 8);
        Den(rng, 16f, 150f, 240f, "Tyrant's ground", "skull-and-bone-scatter", 5f, 1, false, "tyrannosaurus-base", 12);
        Den(rng, 10f, 60f, 200f, "Old graves", "bone-warning-totem", 3f, 4, true, "flesh-eater-base", 3, "undead-pirate-base", 6, "ashen-assassin-base", 9);
    }

    static void Den(System.Random rng, float r, float min, float max, string label, string model, float size, int cap, bool nightOnly, params object[] species)
    {
        Vector3 p = Claim(rng, r, min, max);
        if (p == Vector3.zero) return;
        StreetBuilder.Prop(model, p, (float)rng.NextDouble() * 360f, 0f, size, true, root);
        StreetBuilder.Prop("bone-scatter", p + new Vector3(3f, 0f, 2f), (float)rng.NextDouble() * 360f, 0f, 1.6f, false, root);
        StreetBuilder.Prop("bone-warning-totem", p + new Vector3(-r + 2f, 0f, -r + 2f), 0f, 2.2f, 0f, true, root);
        SpawnDirector.Add(p, label, true, cap, nightOnly, species);
    }

    static void Crate(string key, Vector3 pos, string label, params LootRoll[] loot)
    {
        var go = StreetBuilder.Prop(key, pos, 0f, 0f, key == "log-store" ? 2.4f : 0.85f, true, root);
        if (go != null) Container.Add(go, label, 2f, 4, loot);
    }

    /// <summary>Low wooded hills beyond the edge of the world, so the horizon is not a flat line.</summary>
    static void Hills(System.Random rng)
    {
        var mat = Look.Mat("grass", new Color(0.24f, 0.36f, 0.22f), 30f);
        for (int n = 0; n < 30; n++)
        {
            float a = n / 30f * Mathf.PI * 2f + (float)rng.NextDouble() * 0.2f;
            float d = StreetBuilder.WorldHalf + Mathf.Lerp(90f, 260f, (float)rng.NextDouble());
            var h = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            h.name = "Hill";
            Object.Destroy(h.GetComponent<Collider>());
            h.transform.SetParent(root, false);
            float w = Mathf.Lerp(160f, 320f, (float)rng.NextDouble());
            h.transform.position = new Vector3(Mathf.Cos(a) * d, Y - 10f, Mathf.Sin(a) * d);
            h.transform.localScale = new Vector3(w, Mathf.Lerp(50f, 150f, (float)rng.NextDouble()), w);
            h.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
