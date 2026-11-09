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
    static Dictionary<string, string> index;
    static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();
    static bool warned;

    /// <summary>
    /// Reads Resources/model-index.txt (written by the Editor whenever a model is added). Each model can be
    /// asked for by its full file name or by the name without the 6-character hash at the end.
    /// </summary>
    static void BuildIndex()
    {
        index = new Dictionary<string, string>();
        var list = Resources.Load<TextAsset>("model-index");
        if (list == null) return;
        var hash = new Regex("-[0-9a-f]{6}$");
        foreach (string line in list.text.Split('\n'))
        {
            string stem = line.Trim();
            if (stem.Length == 0) continue;
            index[stem] = stem;
            string shortKey = hash.Replace(stem, "");
            if (!index.ContainsKey(shortKey)) index[shortKey] = stem;
        }
    }

    /// <summary>True when a model with this name is in the project.</summary>
    public static bool Has(string key)
    {
        if (index == null) BuildIndex();
        return index.ContainsKey(key);
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
            Debug.LogWarning("[ModelLibrary] Could not load 'Resources/Models/" + stem + "'. Using a placeholder.");
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
