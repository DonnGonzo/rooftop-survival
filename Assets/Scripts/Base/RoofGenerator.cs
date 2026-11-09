using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a random rooftop from the run seed (same seed = same roof). Roof quality (0..1)
/// drives size, planter count, clutter and starting supplies, so each new run can be
/// easier or harder than the last. Uses the Polyfork models when they are imported,
/// otherwise falls back to simple placeholder shapes.
/// </summary>
public class RoofGenerator : MonoBehaviour
{
    public static Vector2 Size { get; private set; }

    [Header("Scene refs")]
    public Transform roofRoot;
    public Transform playerSpawn;
    [Tooltip("Optional trigger box; resized to fit the generated roof.")]
    public BoxCollider zoneCollider;

    [Header("Size range (metres)")]
    public Vector2 widthRange = new Vector2(12f, 26f);
    public Vector2 depthRange = new Vector2(12f, 26f);

    static readonly string[] ClutterKeys =
    {
        "roof-solar-array", "ammo-crate", "loot-crate", "loot-sack", "tool-case", "fuel-jerrycan",
        "scaffold-material-stack", "hollow-log", "boombox", "fire-hydrant", "structural-column-casing",
        "camouflage-net-canopy", "treasure-chest"
    };

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
        run.food = Mathf.RoundToInt(Mathf.Lerp(2f, 14f, q));
        run.water = Mathf.RoundToInt(Mathf.Lerp(3f, 16f, q));
        run.wood = Mathf.RoundToInt(Mathf.Lerp(0f, 12f, q));
        run.scrap = Mathf.RoundToInt(Mathf.Lerp(0f, 8f, q));
    }

    public void Generate(RunData run)
    {
        var rng = new System.Random(run.seed);
        float q = run.roofQuality;

        float w = Mathf.Lerp(widthRange.x, widthRange.y, Mathf.Clamp01(q + ((float)rng.NextDouble() - 0.5f) * 0.5f));
        float d = Mathf.Lerp(depthRange.x, depthRange.y, Mathf.Clamp01(q + ((float)rng.NextDouble() - 0.5f) * 0.5f));
        Size = new Vector2(w, d);

        // Floor, the building body down to the street, and a parapet with one gap (the grapple point).
        Cube("RoofFloor", new Vector3(0f, -0.5f, 0f), new Vector3(w, 1f, d), new Color(0.33f, 0.33f, 0.35f));
        float bodyH = -StreetBuilder.StreetY;
        Cube("BuildingBody", new Vector3(0f, -1f - bodyH / 2f, 0f), new Vector3(w, bodyH, d), new Color(0.36f, 0.4f, 0.34f));

        const float ph = 1.0f, pt = 0.35f, gap = 3f;
        var wall = new Color(0.45f, 0.45f, 0.47f);
        float seg = (w - gap) / 2f;
        Cube("ParapetS", new Vector3(0f, ph / 2f, -d / 2f + pt / 2f), new Vector3(w, ph, pt), wall);
        Cube("ParapetW", new Vector3(-w / 2f + pt / 2f, ph / 2f, 0f), new Vector3(pt, ph, d), wall);
        Cube("ParapetE", new Vector3(w / 2f - pt / 2f, ph / 2f, 0f), new Vector3(pt, ph, d), wall);
        Cube("ParapetN1", new Vector3(-(gap / 2f + seg / 2f), ph / 2f, d / 2f - pt / 2f), new Vector3(seg, ph, pt), wall);
        Cube("ParapetN2", new Vector3((gap / 2f + seg / 2f), ph / 2f, d / 2f - pt / 2f), new Vector3(seg, ph, pt), wall);

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
            zoneCollider.size = new Vector3(w + 1f, 14f, d + 1f);
            zoneCollider.center = new Vector3(0f, 6f, 0f);
        }
        if (playerSpawn != null) playerSpawn.localPosition = new Vector3(0f, 0.2f, 0f);

        // Each entry: x, z, radius. Keeps things from overlapping.
        var used = new List<Vector3>();
        used.Add(new Vector3(0f, 0f, 2.2f));            // spawn point
        used.Add(new Vector3(0f, d / 2f - 1.5f, 2f));   // keep the grapple gap clear

        // Grapple anchor at the gap, campfire as the base hub.
        var anchor = ModelLibrary.Spawn("tendon-harness", roofRoot, new Vector3(0f, 0f, d / 2f - 1.2f), 180f, fitHeight: 1.1f);
        if (anchor != null) anchor.name = "GrappleAnchor";

        float fx = -w / 2f + 3f, fz = -d / 2f + 3f;
        var fire = ModelLibrary.Spawn("open-campfire", roofRoot, new Vector3(fx, 0f, fz));
        if (fire != null) ModelLibrary.PlayClip(fire, "tick");
        used.Add(new Vector3(fx, fz, 1.4f));

        int planters = Mathf.RoundToInt(Mathf.Lerp(1f, 6f, q));
        for (int i = 0; i < planters; i++) SpawnPlanter(rng, w, d, used);

        int clutter = Mathf.RoundToInt(Mathf.Lerp(10f, 2f, q)); // worse roof = more junk
        for (int i = 0; i < clutter; i++) SpawnClutter(rng, w, d, used);
    }

    void SpawnPlanter(System.Random rng, float w, float d, List<Vector3> used)
    {
        float yaw = rng.Next(2) * 90f;
        GameObject go = ModelLibrary.Spawn("modular-planter-trough", roofRoot, Vector3.zero, yaw,
                                           addCollider: true, keepAnimations: false);
        float r = 2.2f;
        FarmPlot plot;
        if (go != null)
        {
            plot = go.AddComponent<FarmPlot>();
            var crop = FindDeep(go.transform, "planting");
            if (crop != null) plot.SetCropVisual(crop);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(roofRoot, false);
            go.transform.localScale = new Vector3(2.5f, 0.6f, 1f);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Tint(go, new Color(0.4f, 0.26f, 0.15f));
            plot = go.AddComponent<FarmPlot>();
            var crop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(crop.GetComponent<Collider>());
            crop.transform.SetParent(go.transform, false);
            crop.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            crop.transform.localScale = Vector3.one * 0.5f;
            Tint(crop, new Color(0.25f, 0.65f, 0.2f));
            plot.SetCropVisual(crop.transform);
            r = 1.6f;
        }
        go.name = "Planter";
        if (!TryPlace(go.transform, r, rng, w, d, used, 2f)) Destroy(go);
    }

    void SpawnClutter(System.Random rng, float w, float d, List<Vector3> used)
    {
        string key = ClutterKeys[rng.Next(ClutterKeys.Length)];
        float yaw = (float)rng.NextDouble() * 360f;
        GameObject go = ModelLibrary.Spawn(key, roofRoot, Vector3.zero, yaw, addCollider: true, keepAnimations: false);
        float r;
        if (go != null)
        {
            var lb = ModelLibrary.LocalBounds(go.transform);
            r = Mathf.Max(lb.size.x, lb.size.z) * 0.5f * go.transform.localScale.x;
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(roofRoot, false);
            go.transform.localScale = new Vector3(1.2f, 0.4f + (float)rng.NextDouble() * 1.2f, 0.9f);
            Tint(go, new Color(0.3f, 0.3f, 0.28f));
            r = 0.8f;
        }
        go.name = "Clutter_" + key;
        if (!TryPlace(go.transform, r, rng, w, d, used, 1.2f)) Destroy(go);
    }

    bool TryPlace(Transform t, float r, System.Random rng, float w, float d, List<Vector3> used, float margin)
    {
        float xr = w / 2f - margin - r, zr = d / 2f - margin - r;
        if (xr < 0f || zr < 0f) return false;

        for (int attempt = 0; attempt < 30; attempt++)
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
            t.localPosition = new Vector3(x, 0f, z);
            return true;
        }
        return false;
    }

    GameObject Cube(string n, Vector3 pos, Vector3 scale, Color c)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n;
        g.transform.SetParent(roofRoot, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        Tint(g, c);
        return g;
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

    public static void Tint(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = c;
    }
}
