using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a random rooftop from the run seed. The same seed always gives the same roof,
/// so reloading a save restores it. Roof quality (0..1) drives size, planter count,
/// clutter and starting supplies, so a new run can be easier or harder than the last.
/// </summary>
public class RoofGenerator : MonoBehaviour
{
    [Header("Scene refs")]
    public Transform roofRoot;
    public Transform playerSpawn;
    [Tooltip("Optional trigger box; resized to fit the generated roof.")]
    public BoxCollider zoneCollider;

    [Header("Prefabs (drag imported GLB prefabs here; blank = placeholder cubes)")]
    public GameObject planterPrefab;      // should have a FarmPlot component
    public GameObject[] clutterPrefabs;   // AC units, water tanks, debris...

    [Header("Size range (metres)")]
    public Vector2 widthRange = new Vector2(8f, 24f);
    public Vector2 depthRange = new Vector2(8f, 24f);

    public Vector2 RoofSize { get; private set; }

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
        RoofSize = new Vector2(w, d);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "RoofFloor";
        floor.transform.SetParent(roofRoot, false);
        floor.transform.localScale = new Vector3(w, 1f, d);
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        Tint(floor, new Color(0.35f, 0.35f, 0.37f));

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
            zoneCollider.size = new Vector3(w + 1f, 14f, d + 1f);
            zoneCollider.center = new Vector3(0f, 6f, 0f);
        }

        if (playerSpawn != null) playerSpawn.localPosition = new Vector3(0f, 0.2f, 0f);

        var used = new List<Vector2>();
        used.Add(Vector2.zero); // keep spawn clear

        int planters = Mathf.RoundToInt(Mathf.Lerp(1f, 8f, q));
        for (int i = 0; i < planters; i++)
            Place(planterPrefab, "Planter", rng, w, d, used, 2.4f);

        int clutter = Mathf.RoundToInt(Mathf.Lerp(14f, 3f, q)); // worse roof = more junk to clear
        for (int i = 0; i < clutter; i++)
        {
            GameObject prefab = (clutterPrefabs != null && clutterPrefabs.Length > 0)
                ? clutterPrefabs[rng.Next(clutterPrefabs.Length)] : null;
            Place(prefab, "Clutter", rng, w, d, used, 1.8f);
        }
    }

    public static void Tint(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r != null) r.material.color = c;
    }

    void Place(GameObject prefab, string label, System.Random rng, float w, float d, List<Vector2> used, float minDist)
    {
        const float margin = 1.5f;
        for (int attempt = 0; attempt < 25; attempt++)
        {
            float x = Mathf.Lerp(-w / 2f + margin, w / 2f - margin, (float)rng.NextDouble());
            float z = Mathf.Lerp(-d / 2f + margin, d / 2f - margin, (float)rng.NextDouble());
            var p = new Vector2(x, z);

            bool clear = true;
            foreach (var u in used) if ((u - p).sqrMagnitude < minDist * minDist) { clear = false; break; }
            if (!clear) continue;

            used.Add(p);
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, roofRoot);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(roofRoot, false);
                go.transform.localScale = new Vector3(1.2f, 0.6f, 1.2f);
                if (label == "Planter")
                {
                    Tint(go, new Color(0.40f, 0.26f, 0.15f));
                    var plot = go.AddComponent<FarmPlot>();
                    var crop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(crop.GetComponent<Collider>());
                    crop.name = "Crop";
                    crop.transform.SetParent(go.transform, false);
                    crop.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                    crop.transform.localScale = Vector3.one * 0.6f;
                    Tint(crop, new Color(0.25f, 0.65f, 0.2f));
                    plot.cropVisual = crop.transform;
                    plot.UpdateVisual();
                }
                else
                {
                    go.transform.localScale = new Vector3(1.2f, 0.4f + (float)rng.NextDouble() * 1.2f, 0.9f);
                    Tint(go, new Color(0.3f, 0.3f, 0.28f));
                }
            }
            go.name = label;
            go.transform.localPosition = new Vector3(x, 0f, z);
            go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            return;
        }
    }
}
