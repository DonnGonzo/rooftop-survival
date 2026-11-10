using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the world below the roof from the run seed: a small ruined city on a street grid, and (through
/// ForestBuilder) the forest that surrounds it. The same seed always gives the same world.
///
/// City layout: 5 x 5 blocks. The player's building stands in the middle block. Every other block is one
/// of: four buildings, one large building, a park, a car park, or a ruin. Buildings have a doorway on the
/// street side and a furnished ground floor (BuildingInterior).
/// </summary>
public static class StreetBuilder
{
    public const float StreetY = -90f;
    public const float Pitch = 56f;        // block centre to block centre
    public const float Block = 44f;        // block width; the rest of the pitch is road
    public const int Rings = 2;            // blocks out from the centre: 2 gives 5 x 5
    public const float CityHalf = Rings * Pitch + Pitch / 2f;   // 140 m
    public const float WorldHalf = 350f;   // the forest runs out to here

    static readonly string[] Tall =
    {
        "art-deco-setback-tower", "brick-tenement", "postwar-apartment-slab", "prewar-apartment-house",
        "capsule-residential-tower", "slab-apartment-block", "corner-tenement", "cast-iron-loft", "office-tower", "narrow-walk-up"
    };
    static readonly string[] Low =
    {
        "corner-shop", "department-store", "brownstone-row-house", "parking-garage", "fire-station",
        "street-diner", "one-storey-shop-row", "auto-repair-garage", "movie-theater"
    };
    static readonly string[] Cars = { "compact-hatchback", "vintage-hearse", "pickup-truck", "wheeled-city-coupe", "security-patrol-cruiser" };
    static readonly string[] Trucks = { "fire-engine", "campus-shuttle-bus", "tow-truck", "semi-tractor", "food-truck" };
    static readonly string[] DeadTrees = { "twisted-dead-oak", "dead-cottonwood", "dead-snag", "scorched-tree" };

    /// <summary>Short status line, handy when debugging a build.</summary>
    public static string Diag = "";
    static int buildings, subways;
    static Transform city;

    public static void Build(int seed)
    {
        buildings = 0;
        subways = 0;
        try
        {
            var rng = new System.Random(seed + 1);
            city = new GameObject("City").transform;
            Ground();
            for (int i = -Rings; i <= Rings; i++)
                for (int j = -Rings; j <= Rings; j++)
                    BuildBlock(i, j, rng);
            Roads(rng);
            ForestBuilder.Build(seed, new GameObject("Forest").transform);
            Bounds();
            MapView.Capture(WorldHalf);
            Diag = "buildings " + buildings + ", dens " + SpawnDirector.Dens.Count;
        }
        catch (System.Exception e)
        {
            Diag = "World build failed: " + e.Message;
            Debug.LogError("[StreetBuilder] " + e);
        }
    }

    // ------------------------------------------------------------------ ground and roads

    static void Ground()
    {
        float all = WorldHalf * 2f + 700f;
        Look.Box("ForestFloor", city, new Vector3(0f, StreetY - 0.5f, 0f), new Vector3(all, 1f, all), Look.Mat("grass", new Color(0.3f, 0.43f, 0.22f), 9f));
        float c = CityHalf * 2f + 6f;
        Look.Box("Asphalt", city, new Vector3(0f, StreetY - 0.21f, 0f), new Vector3(c, 0.5f, c), Look.Mat("asphalt", new Color(0.42f, 0.42f, 0.44f), 6f));

        // A dashed centre line down every road. Road centres sit on multiples of 4 m, where the texture puts the line.
        var lineZ = Look.Mat("roadline", new Color(0.42f, 0.42f, 0.44f), 4f);
        var lineX = Look.Mat("roadline_x", new Color(0.42f, 0.42f, 0.44f), 4f);
        for (int k = -Rings; k < Rings; k++)
        {
            float at = (k + 0.5f) * Pitch;
            Look.Box("RoadLine", city, new Vector3(at, StreetY + 0.045f, 0f), new Vector3(3.9f, 0.01f, c), lineZ, false);
            Look.Box("RoadLine", city, new Vector3(0f, StreetY + 0.05f, at), new Vector3(c, 0.01f, 3.9f), lineX, false);
        }
    }

    /// <summary>Invisible walls at the edge of the world.</summary>
    static void Bounds()
    {
        float w = WorldHalf + 6f;
        for (int i = 0; i < 4; i++)
        {
            var g = new GameObject("WorldEdge");
            g.transform.SetParent(city, false);
            var b = g.AddComponent<BoxCollider>();
            bool x = i < 2;
            float sign = i % 2 == 0 ? 1f : -1f;
            g.transform.position = new Vector3(x ? sign * w : 0f, StreetY + 20f, x ? 0f : sign * w);
            b.size = x ? new Vector3(2f, 60f, w * 2f) : new Vector3(w * 2f, 60f, 2f);
        }
    }

    /// <summary>Wrecks along the roads, a couple of old checkpoints, and lamp posts.</summary>
    static void Roads(System.Random rng)
    {
        for (int n = 0; n < 26; n++)
        {
            bool alongZ = rng.Next(2) == 0;
            float line = (rng.Next(-Rings, Rings) + 0.5f) * Pitch;
            float t = Mathf.Lerp(-CityHalf + 6f, CityHalf - 6f, (float)rng.NextDouble());
            if (NearRoadCrossing(t)) continue;
            float lane = (rng.Next(2) == 0 ? -1f : 1f) * 2.6f;
            Vector3 pos = alongZ ? new Vector3(line + lane, StreetY, t) : new Vector3(t, StreetY, line + lane);
            if (pos.magnitude < 34f) continue;   // keep the foot of the rope clear
            bool truck = rng.NextDouble() < 0.25;
            float yaw = (alongZ ? 0f : 90f) + (rng.Next(2) * 180f) + ((float)rng.NextDouble() - 0.5f) * (rng.NextDouble() < 0.15 ? 150f : 26f);
            Wreck(truck ? Trucks[rng.Next(Trucks.Length)] : Cars[rng.Next(Cars.Length)], pos, yaw, truck ? 8.5f : 4.4f, rng);
        }

        // Checkpoints: barriers left across a road.
        for (int n = 0; n < 3; n++)
        {
            bool alongZ = rng.Next(2) == 0;
            float line = (rng.Next(-Rings, Rings) + 0.5f) * Pitch;
            float t = (rng.Next(-Rings, Rings + 1)) * Pitch + ((float)rng.NextDouble() - 0.5f) * 20f;
            if (Mathf.Abs(t) < 20f && Mathf.Abs(line) < 40f) continue;
            string[] parts = { "skated-jersey-barrier", "sandbag-wall-section", "czech-hedgehog", "skated-jersey-barrier", "checkpoint-barrier-pole" };
            for (int k = 0; k < parts.Length; k++)
            {
                float across = -5f + k * 2.5f;
                Vector3 pos = alongZ ? new Vector3(line + across, StreetY, t) : new Vector3(t, StreetY, line + across);
                Prop(parts[k], pos, (alongZ ? 0f : 90f) + ((float)rng.NextDouble() - 0.5f) * 20f, 0f, 2.3f, true);
            }
        }
    }

    static bool NearRoadCrossing(float t)
    {
        for (int k = -Rings; k < Rings; k++) if (Mathf.Abs(t - (k + 0.5f) * Pitch) < 9f) return true;
        return false;
    }

    // ------------------------------------------------------------------ blocks

    static void BuildBlock(int i, int j, System.Random rng)
    {
        Vector3 c = new Vector3(i * Pitch, StreetY, j * Pitch);
        Look.Box("Sidewalk", city, c + new Vector3(0f, -0.17f, 0f), new Vector3(Block, 0.66f, Block), Look.Mat("paving", new Color(0.68f, 0.67f, 0.64f), 3f));
        float top = 0.16f;

        // Street furniture on every block: trees and lamps at the kerb, weeds in the cracks.
        for (int e = 0; e < 4; e++)
        {
            Vector3 n = e == 0 ? Vector3.right : e == 1 ? Vector3.left : e == 2 ? Vector3.forward : Vector3.back;
            Vector3 along = Vector3.Cross(Vector3.up, n);
            if (rng.NextDouble() < 0.7)
            {
                Vector3 p = c + n * (Block / 2f - 1.6f) + along * Mathf.Lerp(-15f, 15f, (float)rng.NextDouble()) + Vector3.up * top;
                bool dead = rng.NextDouble() < 0.3;
                Tree(dead ? DeadTrees[rng.Next(DeadTrees.Length)] : "lakeside-swing-tree", p, Mathf.Lerp(6f, 9.5f, (float)rng.NextDouble()), rng, city, !dead);
            }
            if (rng.NextDouble() < 0.45)
                Prop("arc-street-lamp", c + n * (Block / 2f - 0.9f) + along * (rng.Next(2) == 0 ? -19f : 19f) + Vector3.up * top,
                     Mathf.Atan2(-n.x, -n.z) * Mathf.Rad2Deg, 6.5f, 0f, false);
            for (int g = 0; g < 2; g++)
            {
                Vector3 p = c + n * (Block / 2f - 0.5f - (float)rng.NextDouble() * 2f) + along * Mathf.Lerp(-20f, 20f, (float)rng.NextDouble()) + Vector3.up * top;
                FiberPlant(rng.Next(2) == 0 ? "meadow-grass-plant" : "ornamental-grass-tuft", p, Mathf.Lerp(0.45f, 0.9f, (float)rng.NextDouble()), rng, city);
            }
        }

        if (i == 0 && j == 0) { HomeBlock(c + Vector3.up * top, rng); return; }

        double roll = rng.NextDouble();
        bool inner = Mathf.Abs(i) <= 1 && Mathf.Abs(j) <= 1;
        if (roll < 0.46) QuadBlock(c, top, rng);
        else if (roll < 0.64) TowerBlock(c, top, rng);
        else if (roll < 0.8) ParkBlock(c, top, rng);
        else if (roll < 0.9) CarPark(c, top, rng);
        else RuinBlock(c, top, rng, inner);
    }

    /// <summary>The block your building stands on: a quiet yard, so the first trips down are survivable.</summary>
    static void HomeBlock(Vector3 c, System.Random rng)
    {
        Prop("metal-trash-can", c + new Vector3(-16f, 0f, 17f), 0f, 0.8f, 0f, true);
        Prop("station-bench", c + new Vector3(8f, 0f, 19f), 180f, 0f, 2f, true);
        var crate = Prop("wooden-crate", c + new Vector3(4.5f, 0f, 18f), 20f, 0f, 0.85f, true);
        if (crate != null) Container.Add(crate, "Pry open crate", 2f, 0, new LootRoll("wood", 2, 3), new LootRoll("stone", 1, 2));
    }

    static void QuadBlock(Vector3 c, float top, System.Random rng)
    {
        for (int a = -1; a <= 1; a += 2)
            for (int b = -1; b <= 1; b += 2)
            {
                if (rng.NextDouble() < 0.12) continue;   // an empty lot now and then
                bool low = rng.NextDouble() < 0.4;
                string key = low ? Low[rng.Next(Low.Length)] : Tall[rng.Next(Tall.Length)];
                // The door faces the road on the outer side of this lot.
                int side = rng.Next(2) == 0 ? (a > 0 ? 0 : 1) : (b > 0 ? 2 : 3);
                Building(key, c + new Vector3(a * 11f, top, b * 11f), 16.5f, !low, side, rng);
            }
    }

    static void TowerBlock(Vector3 c, float top, System.Random rng)
    {
        string key = Tall[rng.Next(Tall.Length)];
        Building(key, c + Vector3.up * top, 30f, true, rng.Next(4), rng);
    }

    static void ParkBlock(Vector3 c, float top, System.Random rng)
    {
        Look.Box("ParkGrass", city, c + new Vector3(0f, top + 0.01f, 0f), new Vector3(38f, 0.04f, 38f), Look.Mat("grass", new Color(0.4f, 0.56f, 0.3f), 6f), false);
        Vector3 g = c + Vector3.up * (top + 0.03f);
        Prop("lobby-tiered-fountain", g, 0f, 2.6f, 0f, true);
        for (int n = 0; n < 8; n++)
        {
            Vector3 p = g + new Vector3(Mathf.Lerp(-17f, 17f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-17f, 17f, (float)rng.NextDouble()));
            if ((p - g).magnitude < 4f) continue;
            Tree(n % 3 == 0 ? "oak-tree" : n % 3 == 1 ? "maple-tree" : "lakeside-swing-tree", p, Mathf.Lerp(7f, 11f, (float)rng.NextDouble()), rng, city, true);
        }
        for (int n = 0; n < 5; n++)
        {
            Vector3 p = g + new Vector3(Mathf.Lerp(-17f, 17f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-17f, 17f, (float)rng.NextDouble()));
            if (n < 2) BerryBush(p, rng, city); else Prop(n % 2 == 0 ? "leafy-bush" : "round-bush", p, (float)rng.NextDouble() * 360f, Mathf.Lerp(0.9f, 1.5f, (float)rng.NextDouble()), 0f, false);
        }
        Prop("station-bench", g + new Vector3(5f, 0f, 0f), -90f, 0f, 2f, true);
        Prop("station-bench", g + new Vector3(-5f, 0f, 0f), 90f, 0f, 2f, true);
        SpawnDirector.Add(g, "Park", false, 4, false, "rabbit-base", 1, "chicken-base", 1, "sheep-base", 2);
    }

    static void CarPark(Vector3 c, float top, System.Random rng)
    {
        Look.Box("CarParkTarmac", city, c + new Vector3(0f, top + 0.01f, 0f), new Vector3(38f, 0.04f, 38f), Look.Mat("asphalt", new Color(0.36f, 0.36f, 0.38f), 6f), false);
        Vector3 g = c + Vector3.up * (top + 0.03f);
        for (int row = -1; row <= 1; row += 2)
            for (int k = -2; k <= 2; k++)
            {
                if (rng.NextDouble() < 0.4) continue;
                Wreck(Cars[rng.Next(Cars.Length)], g + new Vector3(k * 6f, 0f, row * 9f), (row > 0 ? 0f : 180f) + ((float)rng.NextDouble() - 0.5f) * 14f, 4.4f, rng);
            }
        var skip = Prop("construction-dumpster", g + new Vector3(-15f, 0f, 0f), 90f, 0f, 3.6f, true);
        if (skip != null) Container.Add(skip, "Dig through the skip", 3f, 5, new LootRoll("scrap", 1, 4, 0.9f), new LootRoll("wood", 1, 3, 0.6f), new LootRoll("fiber", 1, 2, 0.5f)).range = 3.6f;
        Prop("tyre-stack", g + new Vector3(15f, 0f, 3f), 0f, 0.9f, 0f, true);
        SpawnDirector.Add(g, "Car park", true, 3, false, "rat-base", 1, "farm-dog", 2);
    }

    static void RuinBlock(Vector3 c, float top, System.Random rng, bool inner)
    {
        Look.Box("RuinDirt", city, c + new Vector3(0f, top + 0.01f, 0f), new Vector3(38f, 0.04f, 38f), Look.Mat("dirt", new Color(0.5f, 0.44f, 0.36f), 5f), false);
        Vector3 g = c + Vector3.up * (top + 0.03f);
        for (int n = 0; n < 6; n++)
        {
            Vector3 p = g + new Vector3(Mathf.Lerp(-15f, 15f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-15f, 15f, (float)rng.NextDouble()));
            Rock(n % 2 == 0 ? "collapse-rubble-mound" : "masonry-rubble-chunk", p, Mathf.Lerp(2.4f, 5f, (float)rng.NextDouble()), rng, city, ResourceNode.Kind.Rock);
        }
        for (int n = 0; n < 4; n++)
        {
            Vector3 p = g + new Vector3(Mathf.Lerp(-16f, 16f, (float)rng.NextDouble()), 0f, Mathf.Lerp(-16f, 16f, (float)rng.NextDouble()));
            if (n < 2) Prop("broken-stone-column", p, (float)rng.NextDouble() * 360f, Mathf.Lerp(2f, 3.6f, (float)rng.NextDouble()), 0f, true);
            else Tree(DeadTrees[rng.Next(DeadTrees.Length)], p, Mathf.Lerp(5f, 8f, (float)rng.NextDouble()), rng, city, false);
        }
        Prop("tower-crane-mast", g + new Vector3(12f, 0f, -12f), 0f, 26f, 0f, true);
        SpawnDirector.Add(g, "Rubble nest", true, 3, false, "rat-base", 1, "stone-centipede-base", 4, "alien-hive-bug-base", 6);
        if (!inner)
            SpawnDirector.Add(g + new Vector3(4f, 0f, 4f), "Ruins after dark", true, 3, true, "flesh-eater-base", 3, "undead-pirate-base", 6, "ashen-assassin-base", 9);
    }

    // ------------------------------------------------------------------ pieces

    /// <summary>
    /// One building on its lot, sized to fit, with a furnished ground floor and a doorway on 'side'
    /// (0 = +x, 1 = -x, 2 = +z, 3 = -z).
    /// </summary>
    static void Building(string key, Vector3 pos, float maxFootprint, bool tall, int side, System.Random rng)
    {
        if (!ModelLibrary.Has(key)) return;
        float yaw = rng.Next(4) * 90f;
        var b = ModelLibrary.Spawn(key, city, pos, yaw, addCollider: false, keepAnimations: false);
        if (b == null) return;

        var lb = ModelLibrary.LocalBounds(b.transform);
        float s = maxFootprint / Mathf.Max(1f, Mathf.Max(lb.size.x, lb.size.z));
        float stretch = 1f;
        if (tall)
        {
            float want = Mathf.Lerp(34f, 125f, (float)(rng.NextDouble() * rng.NextDouble()) + 0.1f);
            stretch = Mathf.Clamp(want / Mathf.Max(1f, lb.size.y * s), 1f, 2.6f);
        }
        b.transform.localScale = new Vector3(s, s * stretch, s);
        Weather(b, (float)rng.NextDouble());
        buildings++;

        bool turned = Mathf.Abs(Mathf.Round(yaw / 90f) % 2) == 1;
        float hx = (turned ? lb.size.z : lb.size.x) * s / 2f;
        float hz = (turned ? lb.size.x : lb.size.z) * s / 2f;
        BuildingInterior.Build(city, pos, hx, hz, side, BuildingInterior.KindFor(key), rng);

        // A subway mouth by some tall buildings: where the night things come up.
        if (tall && subways < 2 && rng.NextDouble() < 0.1)
        {
            Vector3 n = side == 0 ? Vector3.right : side == 1 ? Vector3.left : side == 2 ? Vector3.forward : Vector3.back;
            Vector3 at = pos + n * ((side < 2 ? hx : hz) + 3.2f) + Vector3.Cross(Vector3.up, n) * 5f;
            subways++;
            Prop("subway-entrance", at, Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg, 0f, 4.5f, true);
            SpawnDirector.Add(at, "Subway mouth", true, 2, false, "rat-base", 1);
            SpawnDirector.Add(at, "Subway mouth at night", true, 4, true, "flesh-eater-base", 2, "undead-pirate-base", 5);
        }
    }

    /// <summary>Greys and greens a building so the city looks long abandoned.</summary>
    static void Weather(GameObject go, float amount)
    {
        var moss = new Color(0.28f, 0.4f, 0.24f);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            foreach (var m in r.materials)
            {
                string prop = m.HasProperty("baseColorFactor") ? "baseColorFactor" : m.HasProperty("_Color") ? "_Color" : null;
                if (prop == null) continue;
                Color c = m.GetColor(prop);
                m.SetColor(prop, Color.Lerp(c * 0.82f, moss, amount * 0.35f));
            }
        }
    }

    /// <summary>Plain scenery. Give height OR footprint (the other 0).</summary>
    public static GameObject Prop(string key, Vector3 pos, float yaw, float height, float footprint, bool collider, Transform parent = null)
    {
        if (!ModelLibrary.Has(key)) return null;
        var go = ModelLibrary.Spawn(key, null, pos, yaw, fitHeight: height, fitFootprint: footprint, addCollider: collider, keepAnimations: false);
        if (go != null) go.transform.SetParent(parent != null ? parent : city, true);
        return go;
    }

    /// <summary>A tree you can chop. Its trunk is solid; the crown is not.</summary>
    public static GameObject Tree(string key, Vector3 pos, float height, System.Random rng, Transform parent, bool sway)
    {
        if (!ModelLibrary.Has(key)) return null;
        var go = ModelLibrary.Spawn(key, null, pos, (float)rng.NextDouble() * 360f, fitHeight: height, keepAnimations: false);
        if (go == null) return null;
        go.transform.SetParent(parent, true);
        float s = go.transform.localScale.x;
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.38f / s;
        col.height = height / s;
        col.center = new Vector3(0f, height / s / 2f, 0f);
        ResourceNode.Add(go, ResourceNode.Kind.Tree, 3 + Mathf.RoundToInt(height / 3f), 3);
        return go;
    }

    public static GameObject Rock(string key, Vector3 pos, float size, System.Random rng, Transform parent, ResourceNode.Kind kind)
    {
        if (!ModelLibrary.Has(key)) return null;
        var go = ModelLibrary.Spawn(key, null, pos, (float)rng.NextDouble() * 360f, fitFootprint: size, addCollider: true, keepAnimations: false);
        if (go == null) return null;
        go.transform.SetParent(parent, true);
        ResourceNode.Add(go, kind, kind == ResourceNode.Kind.Ore ? 6 : 3 + Mathf.RoundToInt(size), 4);
        return go;
    }

    /// <summary>A wrecked vehicle: hit it to strip scrap, and search it once.</summary>
    static void Wreck(string key, Vector3 pos, float yaw, float length, System.Random rng)
    {
        if (!ModelLibrary.Has(key)) return;
        var go = ModelLibrary.Spawn(key, null, pos, yaw, fitFootprint: length, addCollider: true, keepAnimations: false);
        if (go == null) return;
        go.transform.SetParent(city, true);
        go.transform.rotation = Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 4f, yaw, ((float)rng.NextDouble() - 0.5f) * 5f);
        ResourceNode.Add(go, ResourceNode.Kind.Wreck, length > 6f ? 9 : 5, 0);
        var c = Container.Add(go, "Search the wreck", 2.4f, 0, new LootRoll("cloth", 1, 2, 0.5f), new LootRoll("scrap", 1, 2, 0.5f),
                              new LootRoll("food", 1, 1, 0.25f), new LootRoll("bandage", 1, 1, 0.15f));
        c.range = length * 0.5f + 1.8f;
    }

    /// <summary>Grass, fern or reeds: hold Interact to gather fiber. Grows back.</summary>
    public static GameObject FiberPlant(string key, Vector3 pos, float height, System.Random rng, Transform parent)
    {
        if (!ModelLibrary.Has(key)) return null;
        var go = ModelLibrary.Spawn(key, null, pos, (float)rng.NextDouble() * 360f, fitHeight: height, keepAnimations: false);
        if (go == null) return null;
        go.transform.SetParent(parent, true);
        var c = Container.Add(go, "Gather fiber", 1.1f, 2, new LootRoll("fiber", 1, 2));
        c.range = 2.2f; c.xp = 2;
        c.fullVisual = go.transform.childCount > 0 ? go.transform.GetChild(0).gameObject : null;
        return go;
    }

    public static GameObject BerryBush(Vector3 pos, System.Random rng, Transform parent)
    {
        string key = ModelLibrary.Has("berry-bush") ? "berry-bush" : "round-bush";
        var go = ModelLibrary.Spawn(key, null, pos, (float)rng.NextDouble() * 360f, fitHeight: Mathf.Lerp(1.0f, 1.4f, (float)rng.NextDouble()), keepAnimations: false);
        if (go == null) return null;
        go.transform.SetParent(parent, true);
        var c = Container.Add(go, "Pick berries", 1.4f, 2, new LootRoll("food", 1, 2));
        c.range = 2.4f; c.xp = 3;
        return go;
    }
}
