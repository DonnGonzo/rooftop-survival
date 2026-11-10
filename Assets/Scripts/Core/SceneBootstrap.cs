using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the whole game in code every time the scene loads (at launch, on New Run, after an overrun):
/// managers, sun and sky, the roof, the city and forest, the player and the UI. The start menu opens on top.
/// </summary>
public static class SceneBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindAnyObjectByType<GameClock>() != null) return;
        Build();
    }

    static void Build()
    {
        GameSettings.ApplyVolume();
        QualitySettings.shadowDistance = 140f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.antiAliasing = 4;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.pixelLightCount = 6;
        Time.timeScale = 1f;

        // Managers
        var mgr = new GameObject("Managers");
        mgr.AddComponent<RunManager>();          // loads or creates the run
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
        }
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        RenderSettings.sun = sun;

        var clock = mgr.AddComponent<GameClock>();
        clock.sun = sun;
        clock.sunColor = MakeSunGradient();
        clock.maxSunIntensity = 1.15f;
        mgr.AddComponent<NightDirector>();
        mgr.AddComponent<Survival>();
        mgr.AddComponent<SpawnDirector>();
        mgr.AddComponent<Hud>();
        mgr.AddComponent<InventoryUI>();
        mgr.AddComponent<MapView>();
        mgr.AddComponent<MainMenu>();
        new GameObject("Sky").AddComponent<SkyController>();

        // Roof and its zone
        var roof = new GameObject("Roof");
        var gen = roof.AddComponent<RoofGenerator>();
        gen.roofRoot = roof.transform;
        var zoneGo = new GameObject("RoofZone");
        zoneGo.transform.SetParent(roof.transform, false);
        var box = zoneGo.AddComponent<BoxCollider>();
        zoneGo.AddComponent<RoofZone>();
        gen.zoneCollider = box;

        // The world below
        StreetBuilder.Build(run.seed);

        // Player
        foreach (var c in Object.FindObjectsByType<Camera>()) c.gameObject.SetActive(false);

        var p = new GameObject("Player");
        p.tag = "Player";
        p.transform.position = new Vector3(0f, 0.3f, 0f);
        var cc = p.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.35f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.4f;

        var camGo = new GameObject("PlayerCam");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(p.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 1400f;   // the moon, stars and clouds sit far out
        cam.fieldOfView = 68f;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<CameraFeel>();

        var pc = p.AddComponent<PlayerController>();
        pc.cam = camGo.transform;
        var melee = p.AddComponent<MeleeCombat>();
        melee.cam = camGo.transform;
        var body = p.AddComponent<PlayerBody>();
        body.cam = camGo.transform;

        // Start frozen behind the menu, unless New Run / a reload asked to skip it
        GameState.Playing = false;
        GameState.MenuOpen = true;
        GameState.UiOpen = false;
        if (GameState.AutoStart)
        {
            GameState.AutoStart = false;
            GameState.MenuOpen = false;
            GameState.Playing = true;
            GameState.HasStarted = true;
        }
    }

    static Gradient MakeSunGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.5f, 0.28f), 0f),
                new GradientColorKey(new Color(1f, 0.9f, 0.76f), 0.35f),
                new GradientColorKey(new Color(1f, 0.97f, 0.92f), 1f)
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }
}
