using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sky: a visible sun disc, a moon (disc and a dim blue light), stars at night, drifting clouds,
/// and a skybox that dims to night. Everything hangs off a root that follows the player, so it always
/// looks far away. Clouds are built from flattened spheres since the model library has none.
/// </summary>
public class SkyController : MonoBehaviour
{
    const float SkyDistance = 650f;
    const float CloudWrap = 620f;
    const int StarCount = 90;
    const int CloudCount = 16;
    const float CloudBaseY = 210f;

    Light moonLight;
    Transform root, sunDisc, moonDisc;
    readonly List<Renderer> stars = new List<Renderer>();
    readonly List<Transform> clouds = new List<Transform>();
    Material skyMat;
    bool hasExposure, hasTint;

    void Start()
    {
        root = new GameObject("SkyRoot").transform;
        root.SetParent(transform, false);

        sunDisc = MakeDisc("SunDisc", 26f, new Color(1f, 0.95f, 0.75f));
        moonDisc = MakeDisc("MoonDisc", 14f, new Color(0.85f, 0.9f, 1f));

        var moonGo = new GameObject("MoonLight");
        moonGo.transform.SetParent(transform, false);
        moonLight = moonGo.AddComponent<Light>();
        moonLight.type = LightType.Directional;
        moonLight.color = new Color(0.55f, 0.65f, 1f);
        moonLight.shadows = LightShadows.None;
        moonLight.intensity = 0f;

        for (int i = 0; i < StarCount; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = "Star";
            Destroy(s.GetComponent<Collider>());
            s.transform.SetParent(root, false);
            s.transform.localScale = Vector3.one * Random.Range(1.5f, 3.2f);
            Vector3 d = Random.onUnitSphere;
            d.y = Mathf.Abs(d.y);   // upper hemisphere only
            s.transform.localPosition = d.normalized * SkyDistance;
            RoofGenerator.Tint(s, Color.white);
            Emit(s, Color.white * 1.5f);
            NoShadows(s);
            var r = s.GetComponent<Renderer>();
            r.enabled = false;
            stars.Add(r);
        }

        for (int i = 0; i < CloudCount; i++)
        {
            var c = new GameObject("Cloud").transform;
            c.SetParent(root, false);
            int puffs = Random.Range(4, 7);
            for (int p = 0; p < puffs; p++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "CloudPuff";
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(c, false);
                float r = Random.Range(18f, 34f);
                s.transform.localScale = new Vector3(r, r * 0.55f, r);
                s.transform.localPosition = new Vector3(Random.Range(-40f, 40f), Random.Range(-4f, 4f), Random.Range(-25f, 25f));
                RoofGenerator.Tint(s, new Color(0.95f, 0.95f, 0.97f));
                NoShadows(s);
            }
            c.localPosition = new Vector3(Random.Range(-600f, 600f), CloudBaseY + Random.Range(-25f, 35f), Random.Range(-600f, 600f));
            clouds.Add(c);
        }

        // Dimmable skybox: copy the default so we don't change the project asset.
        if (RenderSettings.skybox != null)
        {
            skyMat = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyMat;
            hasExposure = skyMat.HasProperty("_Exposure");
            hasTint = skyMat.HasProperty("_SkyTint");
        }
    }

    void Update()
    {
        var clock = GameClock.Instance;
        if (clock == null || clock.sun == null) return;

        var p = PlayerController.Instance;
        root.position = p != null ? p.transform.position : Vector3.zero;

        // Sun direction (toward the sun) and the moon opposite it.
        Vector3 sunDir = -clock.sun.transform.forward;
        moonLight.transform.rotation = clock.sun.transform.rotation * Quaternion.Euler(180f, 0f, 0f);
        Vector3 moonDir = -moonLight.transform.forward;

        float day = Mathf.Clamp01(sunDir.y * 4f);       // 0 at night, 1 at midday
        float night = Mathf.Clamp01(-sunDir.y * 4f);    // 1 at midnight

        sunDisc.localPosition = sunDir * SkyDistance;
        sunDisc.gameObject.SetActive(sunDir.y > -0.05f);
        moonDisc.localPosition = moonDir * SkyDistance;
        moonDisc.gameObject.SetActive(moonDir.y > -0.05f);

        moonLight.intensity = night * Mathf.Clamp01(moonDir.y * 4f) * 0.35f;

        bool starsOn = night > 0.4f;
        foreach (var r in stars) r.enabled = starsOn;

        if (skyMat != null)
        {
            if (hasExposure) skyMat.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.2f, day));
            if (hasTint) skyMat.SetColor("_SkyTint", Color.Lerp(new Color(0.05f, 0.07f, 0.14f), new Color(0.5f, 0.6f, 0.7f), day));
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

    Transform MakeDisc(string name, float size, Color col)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = name;
        Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(root, false);
        s.transform.localScale = Vector3.one * size;
        RoofGenerator.Tint(s, col);
        Emit(s, col * 2f);
        NoShadows(s);
        return s.transform;
    }

    static void Emit(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        var m = r.material;   // per-instance copy
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c);
    }

    static void NoShadows(GameObject go)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }
}
