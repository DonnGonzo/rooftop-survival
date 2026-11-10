using UnityEngine;

/// <summary>
/// Warm, flickering light for a campfire. The flame shape is already animated by the model's own 'tick' clip.
/// This adds the light it throws on the ground and nearby walls. Brighter at night.
/// </summary>
public class FireFlicker : MonoBehaviour
{
    public float baseRange = 9f;
    public float baseIntensity = 1.6f;

    Light fireLight;
    float seed;

    void Start()
    {
        seed = Random.Range(0f, 100f);
        var go = new GameObject("FireLight");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        fireLight = go.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.55f, 0.22f);
        fireLight.shadows = LightShadows.None;
        fireLight.range = baseRange;
    }

    void Update()
    {
        if (fireLight == null) return;

        var clock = GameClock.Instance;
        float night = (clock != null && clock.IsNight) ? 1f : 0.45f;

        // Two octaves of noise: a fast flicker plus a slower sway.
        float flicker = Mathf.PerlinNoise(Time.time * 7f, seed) * 0.6f + Mathf.PerlinNoise(Time.time * 17f, seed + 5f) * 0.4f;
        fireLight.intensity = baseIntensity * night * (0.7f + 0.6f * flicker);
        fireLight.range = baseRange * (0.9f + 0.2f * flicker);
    }
}

/// <summary>Makes an existing light waver like a flame (the hand torch).</summary>
public class FireFlickerLight : MonoBehaviour
{
    Light l;
    float baseIntensity, seed;

    void Start()
    {
        l = GetComponent<Light>();
        baseIntensity = l != null ? l.intensity : 1f;
        seed = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (l == null) return;
        float f = Mathf.PerlinNoise(Time.time * 8f, seed) * 0.6f + Mathf.PerlinNoise(Time.time * 19f, seed + 5f) * 0.4f;
        l.intensity = baseIntensity * (0.75f + 0.5f * f);
    }
}
