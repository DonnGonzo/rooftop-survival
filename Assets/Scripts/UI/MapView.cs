using UnityEngine;

/// <summary>
/// The map (Map key). A picture of the whole world is taken from above once, when the world has been built;
/// the overlay then shows it with markers for you, your rope and the dens you have been near.
/// </summary>
public class MapView : MonoBehaviour
{
    public static bool Showing { get; private set; }
    public static void Toggle() { Showing = !Showing; }

    static MapView instance;
    RenderTexture picture;
    float half;      // the picture covers -half..half on x and z
    GUIStyle label, title;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Showing = false; instance = null; }

    void Awake() { instance = this; }

    /// <summary>Photographs the world from above. Call once after the city and forest exist.</summary>
    public static void Capture(float worldHalf)
    {
        if (instance == null) return;
        instance.half = worldHalf;
        instance.StartCoroutine(instance.CaptureSoon());
    }

    System.Collections.IEnumerator CaptureSoon()
    {
        yield return null;   // let everything spawned this frame settle
        yield return null;
        if (picture == null) picture = new RenderTexture(1024, 1024, 24);

        var go = new GameObject("MapCamera");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = half;
        cam.transform.position = new Vector3(0f, StreetBuilder.StreetY + 190f, 0f);
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 260f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.3f, 0.2f);
        cam.targetTexture = picture;
        cam.enabled = false;

        // Flat, fog-free, shadow-free light for a readable map.
        bool fog = RenderSettings.fog;
        var mode = RenderSettings.ambientMode;
        Color amb = RenderSettings.ambientLight;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white * 0.9f;
        var prevShadows = QualitySettings.shadows;
        QualitySettings.shadows = ShadowQuality.Disable;
        cam.Render();
        QualitySettings.shadows = prevShadows;
        RenderSettings.fog = fog;
        RenderSettings.ambientMode = mode;
        RenderSettings.ambientLight = amb;

        cam.targetTexture = null;
        Destroy(go);
    }

    void Update()
    {
        if (Showing && (GameState.MenuOpen || GameState.UiOpen || Inp.Pause())) Showing = false;
    }

    void OnGUI()
    {
        if (!Showing || picture == null || GameState.MenuOpen) return;
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            title = new GUIStyle(label) { fontSize = 20 };
        }

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float size = Mathf.Min(Screen.width, Screen.height) - 90f;
        var r = new Rect((Screen.width - size) / 2f, (Screen.height - size) / 2f + 14f, size, size);
        GUI.Label(new Rect(r.x, r.y - 34, 600, 30), "MAP     north is up     " + Keybinds.Describe(GameAction.Map) + " to close", title);
        GUI.DrawTexture(r, picture);

        Mark(r, RoofGenerator.RopeFoot, new Color(1f, 0.85f, 0.2f), "Your rope", 9f);
        foreach (var d in SpawnDirector.Dens)
            if (d.discovered) Mark(r, d.pos, d.danger ? new Color(1f, 0.35f, 0.3f) : new Color(0.5f, 0.9f, 0.5f), d.label, 7f);

        if (FactionDirector.Current != null)
            Mark(r, FactionDirector.Current.pos, Faction.StandingColor, "Drifter camp" + (FactionDirector.Current.UnderAttack ? " (under attack)" : ""), 9f);

        var p = PlayerController.Instance;
        if (p != null)
        {
            Vector2 c = ToMap(r, p.transform.position);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(p.transform.eulerAngles.y, c);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(c.x - 3, c.y - 12, 6, 16), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(c.x - 7, c.y - 2, 14, 6), Texture2D.whiteTexture);
            GUI.matrix = m;
            GUI.Label(new Rect(c.x + 10, c.y - 8, 100, 20), "You", label);
        }
    }

    Vector2 ToMap(Rect r, Vector3 world)
    {
        float u = Mathf.InverseLerp(-half, half, world.x), v = Mathf.InverseLerp(-half, half, world.z);
        return new Vector2(r.x + u * r.width, r.y + (1f - v) * r.height);
    }

    void Mark(Rect r, Vector3 world, Color c, string text, float s)
    {
        Vector2 at = ToMap(r, world);
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(at.x - s / 2f - 1, at.y - s / 2f - 1, s + 2, s + 2), Texture2D.whiteTexture);
        GUI.color = c;
        GUI.DrawTexture(new Rect(at.x - s / 2f, at.y - s / 2f, s, s), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(at.x + s, at.y - 9, 200, 20), text, label);
    }
}
