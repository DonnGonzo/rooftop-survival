using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the ruined, overgrown city around the player's building: a skyline of Polyfork
/// buildings, the street far below with wrecked vehicles and plants, and the wildlife.
/// Everything is seeded from the run so reloading gives the same city.
/// </summary>
public static class StreetBuilder
{
    public const float StreetY = -90f;

    static readonly string[] BuildingKeys =
    {
        "art-deco-setback-tower", "brick-tenement", "corner-shop", "department-store",
        "postwar-apartment-slab", "prewar-apartment-house", "capsule-residential-tower", "slab-apartment-block"
    };
    static readonly string[] VehicleKeys =
    {
        "compact-hatchback", "fire-engine", "vintage-hearse", "campus-shuttle-bus", "teardrop-trailer", "touring-motorcycle"
    };
    static readonly string[] PlantKeys = { "meadow-grass-plant", "ornamental-grass-tuft" };

    struct Footprint { public Vector2 pos; public float r; }

    /// <summary>Short status line shown in the HUD, handy when debugging a build.</summary>
    public static string Diag = "";
    static int buildingCount;

    public static void Build(int seed)
    {
        buildingCount = 0;
        try
        {
            BuildInternal(seed);
            Diag = "Models OK, buildings: " + buildingCount;
        }
        catch (System.Exception e)
        {
            Diag = "City build failed: " + e.Message;
            Debug.LogError("[StreetBuilder] " + e);
        }
    }

    static void BuildInternal(int seed)
    {
        var rng = new System.Random(seed + 1);
        var city = new GameObject("City");
        var placed = new List<Footprint>();

        // Street ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "StreetGround";
        ground.transform.SetParent(city.transform, false);
        ground.transform.position = new Vector3(0f, StreetY, 0f);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);
        RoofGenerator.Tint(ground, new Color(0.2f, 0.3f, 0.16f));

        // Skyline
        for (int i = 0; i < 60; i++)
        {
            string key = BuildingKeys[rng.Next(BuildingKeys.Length)];
            var b = ModelLibrary.Spawn(key, city.transform, Vector3.zero, rng.Next(4) * 90f,
                                       addCollider: true, keepAnimations: false);
            if (b == null) continue;

            var lb = ModelLibrary.LocalBounds(b.transform);
            float native = Mathf.Max(1f, lb.size.y);
            float topY = Mathf.Lerp(-35f, 45f, (float)rng.NextDouble());   // roof height vs. ours (0)
            float h = topY - StreetY;
            float s = Mathf.Clamp(h / native, 1f, 3f);
            float yStretch = Mathf.Clamp(h / (native * s), 1f, 1.7f);
            float radius = Mathf.Max(lb.size.x, lb.size.z) * s * 0.5f;

            bool ok = false;
            Vector2 pos = Vector2.zero;
            for (int attempt = 0; attempt < 12 && !ok; attempt++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = Mathf.Lerp(22f, 150f, (float)rng.NextDouble()) + radius;
                pos = new Vector2(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist);
                ok = IsClear(placed, pos, radius);
            }
            if (!ok) { Object.Destroy(b); continue; }

            placed.Add(new Footprint { pos = pos, r = radius });
            buildingCount++;
            b.transform.localPosition = new Vector3(pos.x, StreetY, pos.y);
            b.transform.localScale = new Vector3(s, s * yStretch, s);
            Mossify(b, (float)rng.NextDouble());
        }

        // Wrecked vehicles and overgrowth on the street
        for (int i = 0; i < 18; i++)
        {
            var v = ModelLibrary.Spawn(VehicleKeys[rng.Next(VehicleKeys.Length)], city.transform, Vector3.zero,
                                       (float)rng.NextDouble() * 360f, addCollider: true, keepAnimations: false);
            if (v == null) continue;
            Vector2 p = FreeStreetPoint(rng, placed, 4f, 20f, 110f);
            v.transform.localPosition = new Vector3(p.x, StreetY, p.y);
            placed.Add(new Footprint { pos = p, r = 3f });
        }
        for (int i = 0; i < 200; i++)
        {
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = Mathf.Lerp(18f, 130f, (float)rng.NextDouble());
            var g = ModelLibrary.Spawn(PlantKeys[rng.Next(PlantKeys.Length)], city.transform,
                                       new Vector3(Mathf.Cos(ang) * dist, StreetY, Mathf.Sin(ang) * dist),
                                       (float)rng.NextDouble() * 360f, keepAnimations: false);
            if (g != null) g.transform.localScale *= Mathf.Lerp(1f, 2.2f, (float)rng.NextDouble());
        }

        StreetDecor.Build(seed, city.transform);
        SpawnWildlife(rng, city.transform, placed);
    }

    static void SpawnWildlife(System.Random rng, Transform parent, List<Footprint> buildings)
    {
        // Roamers: open streets
        Animals(rng, parent, "wolf-base",       5, 1.6f, 5.5f, 16f, 14f, false, buildings);
        Animals(rng, parent, "red-fox",         3, 1.8f, 6.0f, 12f,  8f, false, buildings);
        Animals(rng, parent, "black-bear",      2, 1.3f, 4.5f, 12f, 26f, false, buildings);
        Animals(rng, parent, "farm-dog",        2, 1.8f, 5.5f, 12f, 10f, false, buildings);
        // Shade dwellers: lurk at building bases, where it's dark
        Animals(rng, parent, "alien-hive-bug-base", 4, 1.4f, 4.5f, 12f, 18f, true, buildings);
        Animals(rng, parent, "rat-base",            6, 1.5f, 4.0f, 9f,   6f, true, buildings);
    }

    static void Animals(System.Random rng, Transform parent, string key, int count, float walk, float run,
                        float aggro, float damage, bool lurker, List<Footprint> buildings)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 pos;
            if (lurker && buildings.Count > 0)
            {
                var bf = buildings[rng.Next(buildings.Count)];
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                pos = new Vector3(bf.pos.x + Mathf.Cos(a) * (bf.r + 3f), StreetY, bf.pos.y + Mathf.Sin(a) * (bf.r + 3f));
            }
            else
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = Mathf.Lerp(25f, 100f, (float)rng.NextDouble());
                pos = new Vector3(Mathf.Cos(a) * dist, StreetY, Mathf.Sin(a) * dist);
            }

            var go = ModelLibrary.Spawn(key, parent, pos, (float)rng.NextDouble() * 360f, keepAnimations: true);
            if (go == null) return; // model missing, skip this species
            var ai = go.AddComponent<AnimalAI>();
            ai.walkSpeed = walk; ai.runSpeed = run; ai.aggroRadius = aggro;
            ai.biteDamage = damage; ai.lurker = lurker; ai.streetY = StreetY;
        }
    }

    static bool IsClear(List<Footprint> list, Vector2 p, float r)
    {
        foreach (var f in list)
        {
            float min = f.r + r + 3f;
            if ((f.pos - p).sqrMagnitude < min * min) return false;
        }
        return true;
    }

    static Vector2 FreeStreetPoint(System.Random rng, List<Footprint> list, float r, float minD, float maxD)
    {
        Vector2 p = Vector2.zero;
        for (int i = 0; i < 20; i++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float d = Mathf.Lerp(minD, maxD, (float)rng.NextDouble());
            p = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
            if (IsClear(list, p, r)) break;
        }
        return p;
    }

    /// <summary>Darken/green-tint building materials so the city looks long abandoned.</summary>
    static void Mossify(GameObject go, float amount)
    {
        var moss = new Color(0.25f, 0.4f, 0.2f);
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            foreach (var m in r.materials)
            {
                // URP Lit uses _BaseColor; glTFast's shader graph uses baseColorFactor.
                string prop = m.HasProperty("_BaseColor") ? "_BaseColor"
                            : m.HasProperty("baseColorFactor") ? "baseColorFactor" : null;
                if (prop == null) continue;
                Color c = m.GetColor(prop);
                m.SetColor(prop, Color.Lerp(c * 0.7f, moss, amount * 0.45f));
            }
        }
    }
}
