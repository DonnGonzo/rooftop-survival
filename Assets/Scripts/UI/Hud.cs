using UnityEngine;

/// <summary>Quick OnGUI heads-up display. Replace with proper UI later.</summary>
public class Hud : MonoBehaviour
{
    static string toast = "";
    static float toastUntil;
    public static void Toast(string msg) { toast = msg; toastUntil = Time.time + 4f; }

    GUIStyle big, small;

    void OnEnable() { NightDirector.OnNightResolved += OnNight; }
    void OnDisable() { NightDirector.OnNightResolved -= OnNight; }

    void OnNight(NightReport r)
    {
        if (!r.attacked) Toast("Night " + r.night + " passed quietly.");
        else if (r.runLost) Toast("BASE OVERRUN on night " + r.night + "! Everything is lost.");
        else if (r.overrun) Toast("Night " + r.night + ": overrun but you held on (threat " + r.threat.ToString("F0") + " vs defense " + r.defense.ToString("F0") + ").");
        else Toast("Night " + r.night + ": attack repelled (threat " + r.threat.ToString("F0") + " vs defense " + r.defense.ToString("F0") + ").");
    }

    void Update()
    {
        if (GameClock.Instance != null)
            GameClock.Instance.timeScale = Inp.FastForward() ? 15f : 1f;
    }

    void OnGUI()
    {
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 15 };
            big.normal.textColor = Color.white;
            small.normal.textColor = Color.white;
        }

        var clock = GameClock.Instance;
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        var player = PlayerController.Instance;
        if (clock == null || run == null) return;

        int h = Mathf.FloorToInt(clock.Hour);
        int m = Mathf.FloorToInt((clock.Hour % 1f) * 60f);
        string phase = clock.IsNight ? "NIGHT" : "DAY";
        string where = player != null && player.OnStreet ? "STREET" : "ROOF";
        GUI.Label(new Rect(16, 12, 600, 30), $"Day {clock.Day}   {h:00}:{m:00}   {phase}   [{where}]", big);

        GUI.Label(new Rect(16, 44, 900, 26),
            $"Food {run.food}   Water {run.water}   Wood {run.wood}   Scrap {run.scrap}   " +
            $"Defense {BaseStructure.TotalDefense():F0}   Roof quality {(run.roofQuality * 100f):F0}%" +
            (player != null ? $"   Health {player.health:F0}/{player.maxHealth:F0}" : ""), small);

        int safe = Mathf.Max(0, 11 - clock.Day);
        string danger = safe > 0 ? $"Safe nights remaining: {safe}" : "Attacks are possible every night!";
        GUI.Label(new Rect(16, 68, 500, 26), danger, small);

        if (!clock.IsNight && clock.Hour >= clock.warningHour && !RoofZone.PlayerOnRoof)
            GUI.Label(new Rect(Screen.width / 2f - 220, 90, 440, 36), "RETURN TO THE ROOF - night is coming!", big);

        if (Time.time < toastUntil)
            GUI.Label(new Rect(Screen.width / 2f - 400, Screen.height - 90, 800, 30), toast, big);

        GUI.Label(new Rect(16, Screen.height - 28, 1100, 24),
            "WASD move  Shift sprint  Space jump  E plant/water/harvest  T trap (2 wood)  G grapple down/up  Hold N fast-forward", small);
    }
}
