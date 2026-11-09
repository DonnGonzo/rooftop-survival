using System.Text;
using UnityEngine;

/// <summary>On-screen status, tutorial prompts, toasts and health. Replace with proper UI later.</summary>
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
        GameState.Refresh();
        if (GameClock.Instance != null)
            GameClock.Instance.timeScale = GameState.Frozen ? 0f : (Inp.FastForward() ? 15f : 1f);
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
        if (clock == null || run == null || !GameState.Playing || GameState.MenuOpen) return;

        int h = Mathf.FloorToInt(clock.Hour);
        int m = Mathf.FloorToInt((clock.Hour % 1f) * 60f);
        string phase = clock.IsNight ? "NIGHT" : "DAY";
        string where = player != null && player.OnStreet ? "STREET" : "ROOF";
        GUI.Label(new Rect(16, 12, 600, 30), $"Day {clock.Day}   {h:00}:{m:00}   {phase}   [{where}]", big);

        GUI.Label(new Rect(16, 44, 1000, 26),
            $"Food {run.food}   Water {run.water}   Wood {run.wood}   Scrap {run.scrap}   " +
            $"Defense {BaseStructure.TotalDefense():F0}   Roof quality {(run.roofQuality * 100f):F0}%", small);

        var sb = new StringBuilder();
        foreach (var d in Items.Defs)
        {
            if (d.id == "food" || d.id == "water" || d.id == "wood" || d.id == "scrap") continue;
            int n = Items.Count(d.id);
            if (n > 0) sb.Append(d.name).Append(" x").Append(n).Append("    ");
        }
        string weapon = Items.Count("club") > 0 ? "Club" : "Fists";
        string combo = MeleeCombat.ComboStep > 0 ? "    Combo x" + MeleeCombat.ComboStep : "";
        GUI.Label(new Rect(16, 66, 1100, 26), sb.ToString() + "Weapon: " + weapon + combo, small);

        int safe = Mathf.Max(0, 11 - clock.Day);
        GUI.Label(new Rect(16, 88, 600, 26),
            safe > 0 ? $"Safe nights remaining: {safe}" : "Attacks are possible every night!", small);

        if (Tutorial.Active)
            GUI.Label(new Rect(16, 116, 1100, 30), "TUTORIAL: " + Tutorial.Current, big);

        if (!clock.IsNight && clock.Hour >= clock.warningHour && !RoofZone.PlayerOnRoof)
            GUI.Label(new Rect(Screen.width / 2f - 260, 150, 520, 36), "RETURN TO THE ROOF - night is coming!", big);

        if (player != null) DrawHealthBar(player.health, player.maxHealth);

        GUI.Label(new Rect(Screen.width / 2f - 6, Screen.height / 2f - 12, 20, 24), "+", big);

        if (Time.time < toastUntil)
            GUI.Label(new Rect(Screen.width / 2f - 400, Screen.height - 96, 800, 30), toast, big);

        GUI.Label(new Rect(16, Screen.height - 28, 1200, 24),
            "WASD move  Shift sprint  Space jump  E interact  Tab crafting  LMB attack (chain for combo)  RMB heavy  T trap  G rappel  Q use  Esc menu  Hold N fast-forward", small);
        GUI.Label(new Rect(16, Screen.height - 52, 900, 22), "[debug] " + StreetBuilder.Diag, small);
    }

    void DrawHealthBar(float hp, float max)
    {
        var bg = new Rect(16, Screen.height - 84, 240, 18);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(bg, Texture2D.whiteTexture);
        GUI.color = new Color(0.8f, 0.15f, 0.15f);
        GUI.DrawTexture(new Rect(bg.x, bg.y, bg.width * Mathf.Clamp01(hp / max), bg.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(bg.x + 6, bg.y - 2, 240, 22), "Health " + hp.ToString("F0") + "/" + max.ToString("F0"), small);
    }
}
