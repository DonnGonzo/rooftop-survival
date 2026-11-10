using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sky and the light that comes from it: a moon, stars, drifting clouds, and through the day the
/// colour of the ambient light, the fog and the skybox. The sun disc itself is drawn by the skybox from
/// the directional light. Everything hangs off a root that follows the player, so it always looks far away.
/// </summary>
public class SkyController : MonoBehaviour
{
    const float SkyDistance = 900f;
    const float CloudWrap = 800f;
    const int StarCount = 140;
    const int CloudCount = 22;
    const float CloudBaseY = 260f;

    Light moonLight;
    Transform root, moonDisc;
    readonly List<Renderer> stars = new List<Renderer>();
    readonly List<Transform> clouds = new List<Transform>();
    Material skyMat, cloudMat, starMat;

    static readonly Color DaySky = new Color(0.62f, 0.72f, 0.85f), DayEquator = new Color(0.55f, 0.58f, 0.56f), DayGround = new Color(0.3f, 0.3f, 0.26f);
    static readonly Color NightSky = new Color(0.07f, 0.09f, 0.16f), NightEquator = new Color(0.05f, 0.06f, 0.1f), NightGround = new Color(0.03f, 0.03f, 0.04f);
    static readonly Color DuskTint = new Color(1f, 0.62f, 0.4f);
    static readonly Color DayFog = new Color(0.66f, 0.74f, 0.8f), NightFog = new Color(0.03f, 0.04f, 0.07f);

    void Start()
    {
        root = new GameObject("SkyRoot").transform;
        root.SetParent(transform, false);
        var rng = new System.Random(12345);

        var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moon.name = "Moon";
        Destroy(moon.GetComponent<Collider>());
        moon.transform.SetParent(root, false);
        moon.transform.localScale = Vector3.one * 34f;
        Look.Glow(moon, new Color(0.9f, 0.93f, 1f));
        moonDisc = moon.transform;

        var moonGo = new GameObject("MoonLight");
        moonGo.transform.SetParent(transform, false);
        moonLight = moonGo.AddComponent<Light>();
        moonLight.type = LightType.Directional;
        moonLight.color = new Color(0.55f, 0.65f, 1f);
        moonLight.shadows = LightShadows.None;
        moonLight.intensity = 0f;

        for (int i = 0; i < StarCount; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Quad);
            s.name = "Star";
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(root, false);
            Vector3 d = new Vector3((float)rng.NextDouble() * 2f - 1f, (float)rng.NextDouble() * 0.95f + 0.05f, (float)rng.NextDouble() * 2f - 1f).normalized;
            s.transform.localPosition = d * SkyDistance;
            s.transform.localRotation = Quaternion.LookRotation(d);
            s.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 4.2f, (float)rng.NextDouble() * (float)rng.NextDouble());
            var r = s.GetComponent<Renderer>();
            if (starMat == null) starMat = Look.Glow(s, Color.white); else { r.sharedMaterial = starMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            r.enabled = false;
            stars.Add(r);
        }

        for (int i = 0; i < CloudCount; i++)
        {
            var c = new GameObject("Cloud").transform;
            c.SetParent(root, false);
            int puffs = 4 + rng.Next(4);
            float spread = 30f + (float)rng.NextDouble() * 40f;
            for (int p = 0; p < puffs; p++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "CloudPuff";
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(c, false);
                float r = 22f + (float)rng.NextDouble() * 26f;
                s.transform.localScale = new Vector3(r * 1.3f, r * 0.5f, r);
                s.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 2f * spread, ((float)rng.NextDouble() - 0.5f) * 6f, ((float)rng.NextDouble() - 0.5f) * spread);
                var rend = s.GetComponent<Renderer>();
                if (cloudMat == null) cloudMat = Look.Glow(s, Color.white); else { rend.sharedMaterial = cloudMat; rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            }
            c.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 2f * CloudWrap, CloudBaseY + ((float)rng.NextDouble() - 0.4f) * 60f, ((float)rng.NextDouble() - 0.5f) * 2f * CloudWrap);
            clouds.Add(c);
        }

        // Dimmable skybox: copy the default so the project asset is never changed.
        if (RenderSettings.skybox != null)
        {
            skyMat = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyMat;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0032f;
    }

    void LateUpdate()
    {
        var clock = GameClock.Instance;
        if (clock == null || clock.sun == null || root == null) return;

        var cam = Camera.main;
        root.position = cam != null ? cam.transform.position : Vector3.zero;

        Vector3 sunDir = -clock.sun.transform.forward;
        moonLight.transform.rotation = Quaternion.LookRotation(sunDir);   // the moon sits opposite the sun
        Vector3 moonDir = -sunDir;

        float day = Mathf.Clamp01(sunDir.y * 4f + 0.15f);      // 0 at night, 1 in daylight
        float night = 1f - day;
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(sunDir.y) * 5f) * Mathf.Clamp01(sunDir.y * 6f + 0.6f);   // sun near the horizon

        moonDisc.localPosition = moonDir * SkyDistance;
        moonDisc.gameObject.SetActive(moonDir.y > -0.05f);
        moonLight.intensity = night * Mathf.Clamp01(moonDir.y * 3f) * 0.3f;

        bool starsOn = night > 0.55f;
        if (stars.Count > 0 && stars[0].enabled != starsOn) foreach (var r in stars) r.enabled = starsOn;

        RenderSettings.ambientSkyColor = Color.Lerp(Color.Lerp(NightSky, DaySky, day), DuskTint * 0.6f, dusk * 0.5f);
        RenderSettings.ambientEquatorColor = Color.Lerp(Color.Lerp(NightEquator, DayEquator, day), DuskTint * 0.5f, dusk * 0.5f);
        RenderSettings.ambientGroundColor = Color.Lerp(NightGround, DayGround, day);
        RenderSettings.fogColor = Color.Lerp(Color.Lerp(NightFog, DayFog, day), DuskTint * 0.8f, dusk * 0.6f);

        if (cloudMat != null)
            cloudMat.SetColor("_Color", Color.Lerp(Color.Lerp(new Color(0.1f, 0.12f, 0.18f), new Color(0.97f, 0.97f, 0.98f), day), new Color(1f, 0.72f, 0.55f), dusk * 0.7f));

        if (skyMat != null)
        {
            if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", Mathf.Lerp(0.08f, 1.25f, day));
            if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", Color.Lerp(new Color(0.05f, 0.07f, 0.14f), new Color(0.5f, 0.6f, 0.72f), day));
            if (skyMat.HasProperty("_AtmosphereThickness")) skyMat.SetFloat("_AtmosphereThickness", Mathf.Lerp(0.6f, 1.05f, day) + dusk * 0.5f);
        }

        // Clouds drift with the wind and wrap around the sky.
        Vector2 w = WindField.Direction;
        float speed = 3f + WindField.Strength * 4f;
        foreach (var c in clouds)
        {
            Vector3 lp = c.localPosition + new Vector3(w.x, 0f, w.y) * speed * Time.deltaTime;
            if (lp.x > CloudWrap) lp.x -= CloudWrap * 2f;
            if (lp.x < -CloudWrap) lp.x += CloudWrap * 2f;
            if (lp.z > CloudWrap) lp.z -= CloudWrap * 2f;
            if (lp.z < -CloudWrap) lp.z += CloudWrap * 2f;
            c.localPosition = lp;
        }
    }
}
