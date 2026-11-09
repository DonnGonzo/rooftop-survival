using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Loads the Polyfork .glb models from Assets/Resources/Models and spawns them
/// grounded (feet on y=0, centred on x/z) so pivots never matter.
/// Use short keys without the hash, e.g. "wolf-base" or "modular-planter-trough".
/// </summary>
public static class ModelLibrary
{
    // Every non-kit model in the project folder (file name without .glb).
    static readonly string[] Stems =
    {
        "alien-hive-bug-base","alpine-ibex-base","ammo-crate-ee3ec0","apron-tile-8670c1","arcade-console-ec27c2",
        "arched-black-cat-407cf6","art-deco-setback-tower-b3a788","axe-b545d6","baccarat-table-8b4183","banana-09c206",
        "beelzebufo-base","bellhop-053c4d","black-bear-adbdc7","blackjack-table-6794fe","boombox-d0048f","bottle-ca0f79",
        "bread-loaf-0e0ab5","brick-tenement-7d2014","camouflage-net-canopy-ce6249","campus-shuttle-bus-27dcaa",
        "capsule-residential-tower-80300b","cat-base","cattle-grid-fff844","cherry-pair-71cfe9","chicken-base",
        "cobblestone-street-tile-c1dca2","compact-hatchback-386417","corn-cob-769fd8","corn-maze-wall-run-22b0da",
        "corner-shop-5aec8c","croissant-758d69","crow-base","cruiser-and-longboard-b3d081","cycle-shelter-714b16",
        "department-store-ffbd58","dirt-road-tile-52acff","farm-dog-723730","fire-door-set-5a3cca","fire-engine-7d476b",
        "fire-hydrant-b68b17","firefighter-b13b36","fried-egg-bdcd3f","fuel-jerrycan-6f1d85","goat-base","gold-coin-29afc1",
        "hamburger-060fd6","hammer-fa3e51","highway-tile-3a705e","hollow-log-329fd7","knife-3d7947","lair-stone-stair-4e09d5",
        "lakeside-swing-tree-02c084","lemon-1c574f","lobby-tiered-fountain-354424","loot-crate-23dd3b","loot-sack-4762f5",
        "lounge-drum-table-13d029","meadow-grass-plant-7085e0","meadow-tile-d0d82a","mesh-site-fence-run-a798a0",
        "mine-spoil-gravel-tile-40ae10","modular-planter-trough-f7905e","mountain-hare-base","open-campfire-5b463f",
        "orange-ea3791","ornamental-grass-tuft-e4156b","pistol-42de05","pizza-slice-ddfb9d","plain-wall-segment-143b6d",
        "postwar-apartment-slab-cee5f9","prewar-apartment-house-1081a6","promenade-paving-tile-5656ce",
        "railway-telegraph-pole-run-bb9354","rat-base","rayuela-box-4587d4","red-fox-5c3bc0","roof-solar-array-c4b996",
        "rooftop-helipad-deck-24e53c","roulette-table-75c9b1","roulette-wheel-f92631","scaffold-material-stack-836dd2",
        "schoolkid-da94c1","seedling-tray-116925","shell-scatter-6d36b7","shovel-997c4d","showgirl-5bfa7a",
        "slab-apartment-block-1b02a2","stadium-terrain-a09f73","station-bench-5f56b4","steampunk-farm-terrain-98cb8a",
        "strawberry-57b7d5","structural-column-casing-bc5956","sword-11907e","tank-track-rut-decal-8c32a2",
        "taxi-rank-sign-f5f39d","teardrop-trailer-580e25","tendon-harness-6ef893","tool-case-d92909",
        "touring-motorcycle-3b1ad3","tower-crane-mast-1784a3","trailhead-sign-aa29e1","treasure-chest-58da92",
        "velvet-rope-stanchion-0b761a","village-child-bef09e","vintage-hearse-3b2ec1","warp-gate-b507bf","wolf-base",
        "xoloitzcuintli-1cd7c8"
    };

    static Dictionary<string, string> index;
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
    static bool warned;

    static void BuildIndex()
    {
        index = new Dictionary<string, string>();
        var hash = new Regex("-[0-9a-f]{6}$");
        foreach (var s in Stems) index[hash.Replace(s, "")] = s;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); warned = false; index = null; }

    public static GameObject Load(string key)
    {
        if (index == null) BuildIndex();
        if (cache.TryGetValue(key, out var cached)) return cached;

        string stem;
        if (!index.TryGetValue(key, out stem)) stem = key;
        var go = Resources.Load<GameObject>("Models/" + stem);
        if (go == null && !warned)
        {
            warned = true;
            Debug.LogWarning("[ModelLibrary] Could not load 'Resources/Models/" + stem + "'. " +
                             "Move the .glb files into Assets/Resources/Models (see README). Using placeholders.");
        }
        cache[key] = go;
        return go;
    }

    /// <summary>True when the models have been imported into Resources/Models.</summary>
    public static bool Available { get { return Load("wolf-base") != null; } }

    /// <summary>
    /// Spawn a model grounded on y=0 and centred on x/z, inside a root object.
    /// fitHeight / fitFootprint scale it uniformly (0 = keep native size).
    /// </summary>
    public static GameObject Spawn(string key, Transform parent, Vector3 localPos, float yaw = 0f,
                                   float fitHeight = 0f, float fitFootprint = 0f,
                                   bool addCollider = false, bool keepAnimations = true)
    {
        var prefab = Load(key);
        if (prefab == null) return null;

        // Measure at identity, away from any parent, so rotation can't inflate the bounds.
        var root = new GameObject(key);
        var inst = UnityEngine.Object.Instantiate(prefab, root.transform);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        Bounds b = LocalBounds(root.transform);
        inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        b = LocalBounds(root.transform);

        if (!keepAnimations)
            foreach (var a in inst.GetComponentsInChildren<Animation>()) a.enabled = false;

        if (addCollider)
        {
            var bc = root.AddComponent<BoxCollider>();
            bc.center = b.center;
            bc.size = b.size;
        }

        float s = 1f;
        if (fitHeight > 0f) s = fitHeight / Mathf.Max(0.01f, b.size.y);
        else if (fitFootprint > 0f) s = fitFootprint / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));

        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        root.transform.localScale = Vector3.one * s;
        return root;
    }

    /// <summary>Renderer bounds of everything under root, expressed in root's local space.</summary>
    public static Bounds LocalBounds(Transform root)
    {
        var b = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            Bounds wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 l = root.InverseTransformPoint(corner);
                if (first) { b = new Bounds(l, Vector3.zero); first = false; }
                else b.Encapsulate(l);
            }
        }
        return b;
    }

    /// <summary>Cross-fade to the first clip whose name contains one of the given words (e.g. "walk").</summary>
    public static bool PlayClip(GameObject go, params string[] preferred)
    {
        if (go == null) return false;
        var anim = go.GetComponentInChildren<Animation>();
        if (anim == null) return false;
        anim.enabled = true;
        foreach (string word in preferred)
        {
            foreach (AnimationState st in anim)
            {
                if (st.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    st.wrapMode = WrapMode.Loop;
                    anim.CrossFade(st.name, 0.2f);
                    return true;
                }
            }
        }
        return false;
    }
}
