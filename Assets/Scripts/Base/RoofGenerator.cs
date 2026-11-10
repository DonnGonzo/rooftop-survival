using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the player's rooftop from the run seed (same seed = same roof). Roof quality (0..1) drives its
/// size, how much the water tower gives and the starting supplies, so each new run is easier or harder.
///
/// The roof starts nearly empty: a campfire, a cot, a workbench, the water tower, the stair hut, the rope
/// anchor and a few supply caches. Everything else (planters, traps, barricades) the player crafts and places.
/// </summary>
public class RoofGenerator : MonoBehaviour
{
    public static Vector2 Size { get; private set; }
    /// <summary>True once the roof exists and saved structures have been rebuilt.</summary>
    public static bool Built { get; private set; }
    /// <summary>Where the rappel rope is fixed on the roof, and where it touches the street.</summary>
    public static Vector3 AnchorPos { get; private set; }
    public static Vector3 RopeFoot { get; private set; }

    [Header("Scene refs")]
    public Transform roofRoot;
    [Tooltip("Optional trigger box; resized to fit the generated roof.")]
    public BoxCollider zoneCollider;

    [Header("Size range (metres)")]
    public Vector2 widthRange = new Vector2(15f, 27f);
    public Vector2 depthRange = new Vector2(15f, 27f);

    static readonly string[] JunkKeys = { "roof-solar-array", "scaffold-material-stack", "fuel-jerrycan", "tyre-stack" };

    struct Cache
    {
        public string model, label, item, extra; public float size; public int min, max;
        public Cache(string model, string label, float size, string item, int min, int max, string extra)
        { this.model = model; this.label = label; this.size = size; this.item = item; this.min = min; this.max = max; this.extra = extra; }
    }
    static readonly Cache[] Caches =
    {
        new Cache("wooden-crate", "crate", 0.8f, "wood", 2, 4, "fiber"),
        new Cache("tool-case", "tool case", 0.55f, "scrap", 1, 3, "stone"),
        new Cache("supply-crate", "supply crate", 0.8f, "food", 1, 3, "water"),
        new Cache("salvage-crate", "salvage crate", 0.8f, "stone", 2, 4, "scrap"),
        new Cache("adventurer-s-backpack", "backpack", 0.5f, "cloth", 1, 3, "food"),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Built = false; Size = Vector2.zero; }

    void OnDestroy() { Built = false; }

    void Start()
    {
        if (RunManager.Instance != null) Generate(RunManager.Instance.Current);
    }

    /// <summary>Deterministic quality from seed. Averaging two rolls makes extremes rarer.</summary>
    public static float RollQuality(int seed)
    {
        var r = new System.Random(seed ^ 0x5bd1e995);
        return (float)((r.NextDouble() + r.NextDouble()) * 0.5);
    }

    public static void ApplyStartingResources(RunData run)
    {
        float q = RollQuality(run.seed);
        run.roofQuality = q;
        run.food = Mathf.RoundToInt(Mathf.Lerp(3f, 8f, q));
        run.water = Mathf.RoundToInt(Mathf.Lerp(3f, 8f, q));
        run.wood = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, q));
        run.scrap = Mathf.RoundToInt(Mathf.Lerp(0f, 3f, q));
        run.items.Add(new ItemStack { id = "planter_kit", count = 1 });   // one planter to get the farm started
    }

    public static int WaterPerDay(RunData run) { return 2 + Mathf.RoundToInt(run.roofQuality * 3f); }

    public void Generate(RunData run)
    {
        var rng = new System.Random(run.seed);
        float q = run.roofQuality;

        float w = Mathf.Lerp(widthRange.x, widthRange.y, Mathf.Clamp01(q + ((float)rng.NextDouble() - 0.5f) * 0.5f));
        float d = Mathf.Lerp(depthRange.x, depthRange.y, Mathf.Clamp01(q + ((float)rng.NextDouble() - 0.5f) * 0.5f));
        Size = new Vector2(w, d);

        var roofMat = Look.Mat("roofing", new Color(0.5f, 0.5f, 0.53f), 5f);
        var wallMat = Look.Mat("concrete", new Color(0.6f, 0.58f, 0.55f), 3f);
        var capMat = Look.Mat("concrete", new Color(0.72f, 0.7f, 0.66f), 2f);
        Color brick = Color.Lerp(new Color(0.62f, 0.4f, 0.32f), new Color(0.55f, 0.55f, 0.5f), (float)rng.NextDouble());
        var bodyMat = Look.Mat("windows", brick, 7f);

        // Floor slab, then the building below it down to the street.
        Look.Box("RoofFloor", roofRoot, new Vector3(0f, -0.5f, 0f), new Vector3(w, 1f, d), roofMat);
        float bodyH = -StreetBuilder.StreetY - 1f;
        Look.Box("BuildingBody", roofRoot, new Vector3(0f, -1f - bodyH / 2f, 0f), new Vector3(w - 0.4f, bodyH, d - 0.4f), bodyMat);
        Look.Box("Cornice", roofRoot, new Vector3(0f, -1.25f, 0f), new Vector3(w + 0.5f, 0.5f, d + 0.5f), capMat, false);

        // Parapet with a coping stone on top, all four sides.
        const float ph = 1.05f, pt = 0.35f;
        Wall("ParapetN", new Vector3(0f, ph / 2f, d / 2f - pt / 2f), new Vector3(w, ph, pt), wallMat, capMat);
        Wall("ParapetS", new Vector3(0f, ph / 2f, -d / 2f + pt / 2f), new Vector3(w, ph, pt), wallMat, capMat);
        Wall("ParapetE", new Vector3(w / 2f - pt / 2f, ph / 2f, 0f), new Vector3(pt, ph, d - pt * 2f), wallMat, capMat);
        Wall("ParapetW", new Vector3(-w / 2f + pt / 2f, ph / 2f, 0f), new Vector3(pt, ph, d - pt * 2f), wallMat, capMat);

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
            zoneCollider.size = new Vector3(w + 1f, 14f, d + 1f);
            zoneCollider.center = new Vector3(0f, 6f, 0f);
        }

        // Each entry: x, z, radius. Keeps things from overlapping.
        var used = new List<Vector3> { new Vector3(0f, 0f, 2.2f) };   // the player's spawn point

        BuildRopeAnchor(w, d, used);
        BuildCamp(run, w, d, used);
        BuildWaterTower(run, w, d, used);
        BuildStairHut(w, d, used);

        // A few supply crates left by whoever held this roof before. Each is searched once per run.
        int caches = 3 + Mathf.RoundToInt(q * 2f);
        for (int i = 0; i < caches; i++)
        {
            var c = Caches[rng.Next(Caches.Length)];
            Vector3 at;
            if (!FindSpot(0.7f, rng, w, d, used, 1.6f, out at)) continue;
            var go = ModelLibrary.Spawn(c.model, roofRoot, at, (float)rng.NextDouble() * 360f, fitFootprint: c.size, addCollider: true, keepAnimations: false);
            if (go == null) continue;
            go.name = "RoofCache";
            var box = Container.Add(go, "Search " + c.label, 1.4f, 0, new LootRoll(c.item, c.min, c.max), new LootRoll(c.extra, 1, 2, 0.5f));
            int index = i;
            if (run.looted.Contains(index)) box.SetEmpty();
            box.onEmptied = () => { run.looted.Add(index); };
        }

        // Rooftop junk along the edges: scenery only.
        int junk = 1 + rng.Next(3);
        for (int i = 0; i < junk; i++)
        {
            string key = JunkKeys[rng.Next(JunkKeys.Length)];
            var go = ModelLibrary.Spawn(key, roofRoot, Vector3.zero, rng.Next(4) * 90f, fitFootprint: key == "fuel-jerrycan" ? 0.5f : 2.2f,
                                        addCollider: true, keepAnimations: false);
            if (go == null) continue;
            Vector3 at;
            if (FindSpot(1.4f, rng, w, d, used, 1.2f, out at)) go.transform.localPosition = at;
            else Destroy(go);
        }

        // Vents and a duct, built from boxes.
        var metal = Look.Mat("concrete", new Color(0.5f, 0.53f, 0.55f), 1.5f, 0.35f);
        for (int i = 0; i < 2; i++)
        {
            Vector3 at;
            if (!FindSpot(0.8f, rng, w, d, used, 1.4f, out at)) continue;
            Look.Box("Vent", roofRoot, at + new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.7f, 0.9f), metal);
            Look.Box("VentCap", roofRoot, at + new Vector3(0f, 0.76f, 0f), new Vector3(1.1f, 0.08f, 1.1f), metal, false);
        }

        Buildables.Restore(run);
        Built = true;
    }

    void Wall(string n, Vector3 pos, Vector3 size, Material wall, Material cap)
    {
        Look.Box(n, roofRoot, pos, size, wall);
        bool alongX = size.x > size.z;
        Vector3 capSize = alongX ? new Vector3(size.x + 0.1f, 0.12f, size.z + 0.16f) : new Vector3(size.x + 0.16f, 0.12f, size.z + 0.1f);
        Look.Box(n + "Cap", roofRoot, pos + new Vector3(0f, size.y / 2f + 0.06f, 0f), capSize, cap, false);
    }

    /// <summary>The yellow davit on the north wall, with a rope hanging all the way to the street.</summary>
    void BuildRopeAnchor(float w, float d, List<Vector3> used)
    {
        Vector3 pos = new Vector3(0f, 0f, d / 2f - 1.0f);
        var go = ModelLibrary.Spawn("rs-rope-anchor", roofRoot, pos, 180f, fitHeight: 1.5f, addCollider: false, keepAnimations: false);
        if (go != null) go.name = "RopeAnchor";
        used.Add(new Vector3(pos.x, pos.z, 1.6f));

        AnchorPos = roofRoot.TransformPoint(pos);
        RopeFoot = new Vector3(AnchorPos.x, StreetBuilder.StreetY, roofRoot.position.z + d / 2f + 0.9f);

        float len = -StreetBuilder.StreetY + 1.2f;
        var rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rope.name = "Rope";
        Destroy(rope.GetComponent<Collider>());
        rope.transform.SetParent(roofRoot, false);
        rope.transform.position = new Vector3(RopeFoot.x, 1.2f - len / 2f, RopeFoot.z - 0.3f);
        rope.transform.localScale = new Vector3(0.07f, len / 2f, 0.07f);
        Look.Paint(rope, "plain", new Color(0.72f, 0.6f, 0.38f));
    }

    /// <summary>Campfire and cot in the south-west corner. The workbench is yours to build.</summary>
    void BuildCamp(RunData run, float w, float d, List<Vector3> used)
    {
        float fx = -w / 2f + 3.4f, fz = -d / 2f + 3.4f;
        var fire = ModelLibrary.Spawn("open-campfire", roofRoot, new Vector3(fx, 0f, fz), 0f, fitFootprint: 1.3f);
        if (fire != null)
        {
            ModelLibrary.PlayClip(fire, "tick");
            fire.AddComponent<FireFlicker>();
        }
        used.Add(new Vector3(fx, fz, 1.5f));

        // Cot: sleep through the night.
        Vector3 cotPos = new Vector3(fx + 2.6f, 0f, fz - 1.2f);
        var cot = ModelLibrary.Spawn("sleeping-bag", roofRoot, cotPos, 90f, fitFootprint: 2.0f, addCollider: false, keepAnimations: false);
        if (cot == null)
        {
            cot = Look.Box("Cot", roofRoot, cotPos + new Vector3(0f, 0.2f, 0f), new Vector3(2f, 0.4f, 0.9f), Look.Mat("planks", new Color(0.5f, 0.4f, 0.3f), 1f), false);
        }
        cot.name = "Cot";
        ActionSpot.Add(cot, 2.6f, () =>
        {
            var c = GameClock.Instance;
            if (c == null || GameClock.Sleeping) return "";
            return (c.Hour >= 18f || c.Hour < 5f) ? "Sleep until dawn" : "";
        }, () =>
        {
            if (NightDirector.RaidActive) { Hud.Toast("Not with those things on the roof."); return; }
            GameClock.Sleep();
            Tutorial.Notify("sleep");
        });
        used.Add(new Vector3(cotPos.x, cotPos.z, 1.4f));
    }

    /// <summary>Water tower in the south-east corner. It refills once a day.</summary>
    void BuildWaterTower(RunData run, float w, float d, List<Vector3> used)
    {
        Vector3 pos = new Vector3(w / 2f - 3.0f, 0f, -d / 2f + 3.0f);
        var go = ModelLibrary.Spawn("water-tower-on-a-dbc730", roofRoot, pos, 0f, fitHeight: 6.5f, addCollider: false, keepAnimations: false);
        if (go == null) return;
        go.name = "WaterTower";
        var lb = ModelLibrary.LocalBounds(go.transform);
        float r = Mathf.Max(lb.size.x, lb.size.z) * 0.5f * go.transform.localScale.x;
        // Only the legs block you: a slim collider at the centre, so you can walk under the tank.
        var col = go.AddComponent<CapsuleCollider>();
        col.radius = 0.5f / go.transform.localScale.x;
        col.height = lb.size.y;
        col.center = new Vector3(0f, lb.size.y / 2f, 0f);
        used.Add(new Vector3(pos.x, pos.z, r + 0.3f));

        ActionSpot.Add(go, r + 2.2f, () =>
        {
            var c = GameClock.Instance;
            if (c == null) return "";
            return run.waterDay == c.Day ? "Water tower: empty until tomorrow" : "Collect water (+" + WaterPerDay(run) + ")";
        }, () =>
        {
            var c = GameClock.Instance;
            if (c == null || run.waterDay == c.Day) { Hud.Toast("The tank refills overnight."); return; }
            run.waterDay = c.Day;
            Items.Add("water", WaterPerDay(run));
            Hud.Toast("+" + WaterPerDay(run) + " Water");
        });
    }

    /// <summary>The roof-access hut in the north-east corner. Scenery, and a windbreak.</summary>
    void BuildStairHut(float w, float d, List<Vector3> used)
    {
        Vector3 pos = new Vector3(w / 2f - 2.6f, 0f, d / 2f - 2.9f);
        var go = ModelLibrary.Spawn("rs-stair-hut", roofRoot, pos, 90f, fitHeight: 3.7f, addCollider: true, keepAnimations: false);
        if (go == null) return;
        go.name = "StairHut";
        used.Add(new Vector3(pos.x, pos.z, 3.0f));
    }

    /// <summary>A crafted planter placed at a spot on the roof.</summary>
    public static GameObject SpawnPlanterAt(Vector3 pos, float yaw)
    {
        GameObject go = ModelLibrary.Spawn("modular-planter-trough", null, pos, yaw, fitFootprint: 2.4f, addCollider: true, keepAnimations: true);
        if (go != null)
        {
            ModelLibrary.PlayClip(go, "tick");   // the plants sway in the wind
            var plot = go.AddComponent<FarmPlot>();
            var crop = FindDeep(go.transform, "planting");
            if (crop != null) plot.SetCropVisual(crop);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = pos + Vector3.up * 0.3f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(2.4f, 0.6f, 1f);
            Look.Paint(go, "planks", new Color(0.5f, 0.36f, 0.22f), 1f);
            var plot = go.AddComponent<FarmPlot>();
            var crop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(crop.GetComponent<Collider>());
            crop.transform.SetParent(go.transform, false);
            crop.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            crop.transform.localScale = Vector3.one * 0.5f;
            Look.Paint(crop, "grass", new Color(0.3f, 0.6f, 0.25f), 0.5f);
            plot.SetCropVisual(crop.transform);
        }
        go.name = "Planter";
        return go;
    }

    /// <summary>Finds a free spot on the roof for something of radius r. Local coordinates.</summary>
    bool FindSpot(float r, System.Random rng, float w, float d, List<Vector3> used, float margin, out Vector3 at)
    {
        at = Vector3.zero;
        float xr = w / 2f - margin - r, zr = d / 2f - margin - r;
        if (xr < 0f || zr < 0f) return false;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            float x = Mathf.Lerp(-xr, xr, (float)rng.NextDouble());
            float z = Mathf.Lerp(-zr, zr, (float)rng.NextDouble());
            bool clear = true;
            foreach (var u in used)
            {
                float dx = u.x - x, dz = u.y - z, min = u.z + r + 0.4f;
                if (dx * dx + dz * dz < min * min) { clear = false; break; }
            }
            if (!clear) continue;
            used.Add(new Vector3(x, z, r));
            at = new Vector3(x, 0f, z);
            return true;
        }
        return false;
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform t in root)
        {
            if (t.name == name) return t;
            var f = FindDeep(t, name);
            if (f != null) return f;
        }
        return null;
    }

    /// <summary>Flat colour on a primitive. Kept for older call sites; new code uses Look.</summary>
    public static void Tint(GameObject go, Color c) { Look.Paint(go, c); }
}
