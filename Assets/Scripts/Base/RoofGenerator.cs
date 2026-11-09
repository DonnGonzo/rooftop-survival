using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
        "camouflage-net-canopy", "treasure-chest",
        // Polyfork roof base modules (CC BY 4.0, see CREDITS.md)
        "survival-roof-assembly-module-20c655", "hide-rain-catchment-funnel-e41d94", "water-tower-on-a-dbc730",
        "portable-device-charging-dock-b175a5", "layered-leaf-sleeping-cot-7ddad4", "gravity-water-filter-stack-4a54f5"
    };

    // Barriers that stand along the parapet as cover (Polyfork, CC BY 4.0, see CREDITS.md)
    static readonly string[] EdgeKeys =
    {
        "sandbag-wall-section-7075ee", "palisade-wall-621e29", "czech-hedgehog-d77ae5",
        "barbed-wire-fence-section-a01906", "skated-jersey-barrier-7f0a04"
    };

    // Loot containers scattered on the roof. Everything else is for the player to craft and place.
    static readonly string[] LootKeys =
    {
        "loot-crate", "ammo-crate", "loot-sack", "tool-case", "treasure-chest"
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
        if (fire != null)
        {
            ModelLibrary.PlayClip(fire, "tick");
            fire.AddComponent<FireFlicker>();
        }
        used.Add(new Vector3(fx, fz, 1.4f));

        // Sparse roof: the landmark, a few loot containers and a little junk. Planters, cover and traps are crafted.
        SpawnLandmark(rng, w, d, used);

        int loot = Mathf.RoundToInt(Mathf.Lerp(2f, 4f, q));
        for (int i = 0; i < loot; i++) SpawnClutter(LootKeys, rng, w, d, used);

        int junk = Mathf.RoundToInt(Mathf.Lerp(2f, 0f, q));   // worse roof = a bit more junk
        for (int i = 0; i < junk; i++) SpawnClutter(ClutterKeys, rng, w, d, used);
    }

    /// <summary>A crafted planter placed at a spot on the roof. Parented to the roof root when one exists.</summary>
    public static GameObject SpawnPlanterAt(Vector3 pos, float yaw)
    {
        GameObject go = ModelLibrary.Spawn("modular-planter-trough", null, pos, yaw, addCollider: true, keepAnimations: true);
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
            go.transform.localScale = new Vector3(2.5f, 0.6f, 1f);
            Tint(go, new Color(0.4f, 0.26f, 0.15f));
            var plot = go.AddComponent<FarmPlot>();
            var crop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(crop.GetComponent<Collider>());
            crop.transform.SetParent(go.transform, false);
            crop.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            crop.transform.localScale = Vector3.one * 0.5f;
            Tint(crop, new Color(0.25f, 0.65f, 0.2f));
            plot.SetCropVisual(crop.transform);
        }
        go.name = "Planter";
        return go;
    }

    void SpawnPlanter(System.Random rng, float w, float d, List<Vector3> used)
    {
        float yaw = rng.Next(2) * 90f;
        GameObject go = ModelLibrary.Spawn("modular-planter-trough", roofRoot, Vector3.zero, yaw,
                                           addCollider: true, keepAnimations: true);
        float r = 2.2f;
        FarmPlot plot;
        if (go != null)
        {
            ModelLibrary.PlayClip(go, "tick");   // the plants on the planter sway in the wind
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

    void SpawnClutter(string[] keys, System.Random rng, float w, float d, List<Vector3> used)
    {
        string key = keys[rng.Next(keys.Length)];
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

    /// <summary>A water tower on the corner away from the campfire, so the roof has a landmark to find your way by.</summary>
    void SpawnLandmark(System.Random rng, float w, float d, List<Vector3> used)
    {
        var pos = new Vector3(w / 2f - 3.2f, 0f, -d / 2f + 3.2f);
        var go = ModelLibrary.Spawn("water-tower-on-a-dbc730", roofRoot, pos, (float)rng.NextDouble() * 360f,
                                    addCollider: true, keepAnimations: false);
        if (go == null) return;
        go.name = "Landmark_WaterTower";
        var lb = ModelLibrary.LocalBounds(go.transform);
        float r = Mathf.Max(lb.size.x, lb.size.z) * 0.5f * go.transform.localScale.x;
        used.Add(new Vector3(pos.x, pos.z, r));
    }

    /// <summary>One barrier against the parapet, turned to face into the roof. Keeps the grapple gap clear.</summary>
    void SpawnEdgeCover(System.Random rng, float w, float d, List<Vector3> used)
    {
        const float inset = 1.8f;
        int side = rng.Next(4);   // 0 north, 1 south, 2 east, 3 west
        float t = (float)(rng.NextDouble() * 2.0 - 1.0);
        Vector3 pos;
        float yaw;
        switch (side)
        {
            case 0: pos = new Vector3(t * (w / 2f - 2f), 0f, d / 2f - inset); yaw = 180f; break;
            case 1: pos = new Vector3(t * (w / 2f - 2f), 0f, -d / 2f + inset); yaw = 0f; break;
            case 2: pos = new Vector3(w / 2f - inset, 0f, t * (d / 2f - 2f)); yaw = 270f; break;
            default: pos = new Vector3(-w / 2f + inset, 0f, t * (d / 2f - 2f)); yaw = 90f; break;
        }
        if (side == 0 && Mathf.Abs(pos.x) < 3.5f) return;   // the grapple gap in the north wall

        foreach (var u in used)
        {
            float dx = u.x - pos.x, dz = u.y - pos.z, min = u.z + 1.6f;
            if (dx * dx + dz * dz < min * min) return;
        }

        string key = EdgeKeys[rng.Next(EdgeKeys.Length)];
        var go = ModelLibrary.Spawn(key, roofRoot, pos, yaw, fitFootprint: 2.6f, addCollider: true, keepAnimations: false);
        if (go == null) return;
        go.name = "EdgeCover_" + key;
        used.Add(new Vector3(pos.x, pos.z, 1.4f));
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

    static Shader litShader;

    /// <summary>Finds a shader that works in the current render pipeline (URP, HDRP or built-in).</summary>
    static Shader GetLitShader()
    {
        if (litShader != null) return litShader;
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp != null && rp.defaultShader != null) litShader = rp.defaultShader;
        if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("HDRP/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");
        return litShader;
    }

    static Material templateMat;
    static Material plainMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetShader() { litShader = null; templateMat = null; plainMat = null; }

    /// <summary>
    /// A material known to render correctly in this project: the one a Polyfork model already uses
    /// (glTFast's own shader). Falls back to the pipeline default shader if the models are missing.
    /// </summary>
    static Material GetTemplate()
    {
        if (templateMat != null) return templateMat;
        var prefab = ModelLibrary.Load("ammo-crate");
        if (prefab != null)
        {
            var r = prefab.GetComponentInChildren<Renderer>(true);
            if (r != null && r.sharedMaterial != null) templateMat = r.sharedMaterial;
        }
        return templateMat;
    }

    /// <summary>True when a shader is missing or Unity has replaced it with its magenta error shader.</summary>
    static bool IsBroken(Shader s)
    {
        return s == null || !s.isSupported || s.name.IndexOf("InternalError", StringComparison.OrdinalIgnoreCase) >= 0
            || s.name.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Makes a fresh material from the first candidate whose shader actually works in this build.
    /// Order: a Polyfork glTF material (proven to render here), the Resources Plain asset, then the pipeline default.
    /// A broken candidate is skipped instead of producing magenta surfaces.
    /// </summary>
    static Material NewWorkingMaterial()
    {
        var t = GetTemplate();
        if (t != null && !IsBroken(t.shader)) return new Material(t);

        if (plainMat == null) plainMat = Resources.Load<Material>("Materials/Plain");
        if (plainMat != null && !IsBroken(plainMat.shader)) return new Material(plainMat);

        var lit = GetLitShader();
        if (!IsBroken(lit)) return new Material(lit);

        return null;
    }

    static bool warnedNoMaterial;

    public static void Tint(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;

        Material m = NewWorkingMaterial();
        if (m == null)
        {
            if (!warnedNoMaterial)
            {
                warnedNoMaterial = true;
                Debug.LogWarning("[RoofGenerator] No working shader found. Check Graphics settings and that URP is the active pipeline.");
            }
            return;
        }

        string prop = m.HasProperty("_BaseColor") ? "_BaseColor"
                    : m.HasProperty("baseColorFactor") ? "baseColorFactor"
                    : m.HasProperty("_Color") ? "_Color" : null;
        if (prop != null) m.SetColor(prop, c);

        if (m.HasProperty("baseColorTexture")) m.SetTexture("baseColorTexture", Texture2D.whiteTexture);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Texture2D.whiteTexture);

        r.sharedMaterial = m;
    }
}
