using UnityEngine;

/// <summary>
/// Builds a playable scene in code when you press Play in any scene that has no GameClock:
/// managers, sun, random roof, city, player and HUD.
/// Once you hand-build a real scene (with a GameClock in it) this does nothing.
/// </summary>
public static class SceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        if (Object.FindAnyObjectByType<GameClock>() != null) return;

        // Managers
        var mgr = new GameObject("Managers");
        mgr.AddComponent<RunManager>();      // Awake loads or creates the run
        var run = RunManager.Instance.Current;

        // Sun
        Light sun = null;
        foreach (var l in Object.FindObjectsByType<Light>())
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null)
        {
            var sg = new GameObject("Sun");
            sun = sg.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
        }
        var clock = mgr.AddComponent<GameClock>();
        clock.sun = sun;
        clock.sunColor = MakeSunGradient();
        mgr.AddComponent<NightDirector>();
        mgr.AddComponent<Hud>();

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.003f;
        RenderSettings.fogColor = new Color(0.55f, 0.6f, 0.62f);

        // Roof + zone
        var roof = new GameObject("Roof");
        var gen = roof.AddComponent<RoofGenerator>();
        gen.roofRoot = roof.transform;
        var zoneGo = new GameObject("RoofZone");
        zoneGo.transform.SetParent(roof.transform, false);
        var box = zoneGo.AddComponent<BoxCollider>();
        zoneGo.AddComponent<RoofZone>();
        gen.zoneCollider = box;

        // City: real models if they are imported, otherwise grey boxes
        if (ModelLibrary.Available) StreetBuilder.Build(run.seed);
        else
        {
            BuildGreyboxCity(run.seed);
            Hud.Toast("Models not found - move the .glb files to Assets/Resources/Models (see README).");
        }

        // Player
        var oldCam = Camera.main;
        if (oldCam != null) oldCam.gameObject.SetActive(false);

        var p = new GameObject("Player");
        p.tag = "Player";
        p.transform.position = new Vector3(0f, 0.3f, 0f);
        var cc = p.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f);

        var camGo = new GameObject("PlayerCam");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(p.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 800f;
        camGo.AddComponent<AudioListener>();

        var pc = p.AddComponent<PlayerController>();
        pc.cam = camGo.transform;
    }

    static void BuildGreyboxCity(int seed)
    {
        var rng = new System.Random(seed + 1);
        var city = new GameObject("City");

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "StreetGround";
        ground.transform.SetParent(city.transform, false);
        ground.transform.position = new Vector3(0f, StreetBuilder.StreetY, 0f);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);
        RoofGenerator.Tint(ground, new Color(0.2f, 0.3f, 0.16f));

        for (int i = 0; i < 45; i++)
        {
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = Mathf.Lerp(30f, 120f, (float)rng.NextDouble());
            float w = Mathf.Lerp(8f, 22f, (float)rng.NextDouble());
            float d = Mathf.Lerp(8f, 22f, (float)rng.NextDouble());
            float top = Mathf.Lerp(-35f, 45f, (float)rng.NextDouble());
            float h = top - StreetBuilder.StreetY;

            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = "Building";
            b.transform.SetParent(city.transform, false);
            b.transform.position = new Vector3(Mathf.Cos(ang) * dist, top - h / 2f, Mathf.Sin(ang) * dist);
            b.transform.localScale = new Vector3(w, h, d);

            float moss = (float)rng.NextDouble() * 0.6f;
            RoofGenerator.Tint(b, Color.Lerp(new Color(0.42f, 0.43f, 0.45f), new Color(0.25f, 0.4f, 0.2f), moss));
        }
    }

    static Gradient MakeSunGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.5f, 0.28f), 0f),
                new GradientColorKey(new Color(1f, 0.93f, 0.8f), 0.4f),
                new GradientColorKey(Color.white, 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
