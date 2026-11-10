using System.Text;
using UnityEngine;

/// <summary>
/// The in-game overlay: day and clock, supplies, health, weapon, what the Interact key will do,
/// the selected kit, tutorial hints, raid status, short messages (toasts) and big messages (banners).
/// </summary>
public class Hud : MonoBehaviour
{
    static string toast = "", banner = "";
    static float toastUntil, bannerUntil, hurtAt = -10f;

    public static void Toast(string msg) { if (!string.IsNullOrEmpty(msg)) { toast = msg; toastUntil = Time.unscaledTime + 3.5f; } }
    public static void Banner(string msg, float seconds = 5f) { banner = msg; bannerUntil = Time.unscaledTime + seconds; }
    /// <summary>Red flash at the screen edges when the player is hit.</summary>
    public static void Hurt() { hurtAt = Time.unscaledTime; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { toast = ""; banner = ""; toastUntil = 0f; bannerUntil = 0f; hurtAt = -10f; }

    GUIStyle big, mid, small, centre, centreBig, key, tiny;
    Texture2D white;
    float startedAt = -1f;

    void OnEnable() { NightDirector.OnNightResolved += OnNight; GameClock.OnDuskWarning += OnDusk; }
    void OnDisable() { NightDirector.OnNightResolved -= OnNight; GameClock.OnDuskWarning -= OnDusk; }

    void OnNight(NightReport r)
    {
        if (!r.attacked) Toast("Night " + r.night + " is quiet.");
    }

    void OnDusk(int day)
    {
        if (!RoofZone.PlayerOnRoof) Banner("Dusk is coming. Get back to the rope.", 6f);
    }

    void Update()
    {
        GameState.Refresh();
        if (GameClock.Instance != null)
        {
            if (GameClock.Sleeping && NightDirector.RaidActive) GameClock.WakeUp();
            float scale = GameState.Frozen ? 0f : GameClock.Sleeping ? 60f : Inp.FastForward() ? 15f : 1f;
            GameClock.Instance.timeScale = scale;
        }
        if (startedAt < 0f && GameState.Playing && !GameState.MenuOpen) startedAt = Time.unscaledTime;
    }

    void Styles()
    {
        if (big != null) return;
        white = Texture2D.whiteTexture;
        big = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        mid = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        centre = new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        centreBig = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
        key = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        foreach (var s in new[] { big, mid, small, centre, centreBig }) s.normal.textColor = Color.white;
        key.normal.textColor = new Color(0.1f, 0.1f, 0.1f);
        tiny = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        tiny.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
    }

    void Panel(Rect r, float alpha = 0.55f)
    {
        GUI.color = new Color(0.04f, 0.05f, 0.06f, alpha);
        GUI.DrawTexture(r, white);
        GUI.color = Color.white;
    }

    void Bar(Rect r, float frac, Color fill)
    {
        Panel(r, 0.7f);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(frac), r.height - 4), white);
        GUI.color = Color.white;
    }

    void Need(float y, string name, float frac, Color c)
    {
        GUI.Label(new Rect(24, y - 3, 70, 18), name, tiny);
        Bar(new Rect(84, y + 2, 184, 10), frac, c);
    }

    /// <summary>A key cap followed by what it does, centred on x. Returns the width used.</summary>
    void KeyHint(float cx, float y, string keyName, string action)
    {
        float kw = Mathf.Max(30f, key.CalcSize(new GUIContent(keyName)).x + 14f);
        float tw = mid.CalcSize(new GUIContent(action)).x;
        float total = kw + 8f + tw + 20f;
        float x = cx - total / 2f;
        Panel(new Rect(x, y - 4, total, 34), 0.6f);
        GUI.color = new Color(0.95f, 0.9f, 0.75f);
        GUI.DrawTexture(new Rect(x + 8, y, kw, 26), white);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 8, y, kw, 26), keyName, key);
        GUI.Label(new Rect(x + 8 + kw + 8, y + 2, tw + 10, 24), action, mid);
    }

    void OnGUI()
    {
        var clock = GameClock.Instance;
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        var player = PlayerController.Instance;
        if (clock == null || run == null || !GameState.Playing || GameState.MenuOpen || MapView.Showing) return;
        Styles();
        float W = Screen.width, H = Screen.height;

        // With the inventory window up, only short messages show; the rest would sit on top of it.
        if (GameState.UiOpen)
        {
            if (Time.unscaledTime < toastUntil)
            {
                float tw0 = Mathf.Min(W - 80f, centre.CalcSize(new GUIContent(toast)).x + 40f);
                float wh0 = Mathf.Min(640f, H - 40f), ww0 = Mathf.Min(1060f, W - 40f);
                var tr0 = new Rect((W + ww0) / 2f - tw0 - 16f, (H + wh0) / 2f - 50f, tw0, 30f);
                Panel(tr0, 0.85f);
                GUI.Label(tr0, toast, centre);
            }
            return;
        }

        // Hurt flash
        float hurt = 1f - Mathf.Clamp01((Time.unscaledTime - hurtAt) / 0.5f);
        if (hurt > 0f)
        {
            GUI.color = new Color(0.8f, 0.05f, 0.05f, hurt * 0.28f);
            GUI.DrawTexture(new Rect(0, 0, W, H), white);
            GUI.color = Color.white;
        }

        // Sleeping
        if (GameClock.Sleeping)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(new Rect(0, 0, W, H), white);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, H / 2f - 30, W, 60), "Sleeping...", centreBig);
        }

        // ---- top left: day, time, place
        int h = Mathf.FloorToInt(clock.Hour);
        int m = Mathf.FloorToInt((clock.Hour % 1f) * 60f);
        Panel(new Rect(12, 12, 300, 74));
        GUI.Label(new Rect(24, 16, 290, 30), "Day " + clock.Day + "    " + h.ToString("00") + ":" + m.ToString("00"), big);
        string where = player != null && player.OnStreet ? "On the street" : "On your roof";
        string phase = clock.IsNight ? "Night" : clock.Hour >= clock.warningHour ? "Dusk soon" : "Daylight";
        GUI.Label(new Rect(24, 44, 290, 22), phase + "  -  " + where, small);
        int safe = Mathf.Max(0, 11 - clock.Day);
        GUI.color = safe > 0 ? new Color(0.7f, 0.9f, 0.7f) : new Color(1f, 0.6f, 0.5f);
        GUI.Label(new Rect(24, 62, 290, 22), safe > 0 ? safe + " safe night" + (safe == 1 ? "" : "s") + " left" : "Raids can come any night", small);
        GUI.color = Color.white;

        // ---- top right: supplies
        var sb = new StringBuilder();
        sb.Append("Food ").Append(run.food).Append("    Water ").Append(run.water)
          .Append("    Wood ").Append(run.wood).Append("    Scrap ").Append(run.scrap)
          .Append("    Pack ").Append(Items.CarriedWeight.ToString("0")).Append(" / ").Append(Items.Capacity.ToString("0")).Append(" kg");
        string supplies = sb.ToString();
        float sw = Mathf.Min(W - 340f, mid.CalcSize(new GUIContent(supplies)).x + 28f);
        Panel(new Rect(W - sw - 12, 12, sw, 54));
        GUI.Label(new Rect(W - sw, 16, sw - 12, 24), supplies, mid);
        GUI.Label(new Rect(W - sw, 38, sw - 12, 22), "Base defense " + BaseStructure.TotalDefense().ToString("F0")
                  + "      Drifters: " + Faction.Standing, small);

        // ---- level
        Panel(new Rect(12, 90, 300, 26));
        GUI.Label(new Rect(24, 92, 90, 22), "Level " + Progress.Level, small);
        Bar(new Rect(100, 97, 200, 12), Progress.Fraction, new Color(0.45f, 0.7f, 1f));

        // ---- tutorial
        if (Tutorial.Active)
        {
            Panel(new Rect(12, 122, 470, 58), 0.6f);
            GUI.color = new Color(1f, 0.85f, 0.4f);
            GUI.Label(new Rect(24, 124, 450, 20), "NEXT STEP  (" + Tutorial.Progress + ")", small);
            GUI.color = Color.white;
            var wrap = new GUIStyle(small) { wordWrap = true };
            GUI.Label(new Rect(24, 142, 450, 38), Tutorial.Current, wrap);
        }

        // ---- raid
        if (NightDirector.RaidActive)
        {
            Panel(new Rect(W / 2f - 170, 74, 340, 40), 0.7f);
            GUI.color = new Color(1f, 0.45f, 0.35f);
            GUI.Label(new Rect(W / 2f - 170, 74, 340, 40), "RAID  -  " + NightDirector.RaidersLeft + " on the roof", centreBig);
            GUI.color = Color.white;
        }
        else if (!clock.IsNight && clock.Hour >= clock.warningHour && player != null && player.OnStreet)
        {
            Panel(new Rect(W / 2f - 230, 74, 460, 36), 0.7f);
            GUI.color = new Color(1f, 0.8f, 0.4f);
            GUI.Label(new Rect(W / 2f - 230, 74, 460, 36), "Night is coming. Get back up the rope.", centre);
            GUI.color = Color.white;
        }

        // ---- banner
        if (Time.unscaledTime < bannerUntil)
        {
            float bw = Mathf.Min(W - 80f, 760f);
            float bh = centre.CalcHeight(new GUIContent(banner), bw - 24) + 22f;
            Panel(new Rect(W / 2f - bw / 2f, H * 0.2f, bw, bh), 0.72f);
            GUI.Label(new Rect(W / 2f - bw / 2f + 12, H * 0.2f, bw - 24, bh), banner, centre);
        }

        // ---- bottom left: health and weapon
        if (player != null)
        {
            float frac = player.health / player.maxHealth;
            Bar(new Rect(16, H - 46, 260, 24), frac, Color.Lerp(new Color(0.8f, 0.15f, 0.12f), new Color(0.3f, 0.75f, 0.3f), frac));
            GUI.Label(new Rect(24, H - 46, 250, 24), "Health " + Mathf.CeilToInt(player.health) + " / " + player.maxHealth.ToString("F0"), small);

            // Needs: stamina, hunger, thirst, and how loaded you are.
            var needs = Needs.Instance;
            if (needs != null)
            {
                Panel(new Rect(16, H - 148, 260, 66));
                Need(H - 144, "Stamina", needs.Stamina / needs.MaxStamina, new Color(0.85f, 0.85f, 0.4f));
                Need(H - 128, "Hunger", needs.Hunger / 100f, needs.Hunger < 20f ? new Color(0.95f, 0.35f, 0.3f) : new Color(0.9f, 0.65f, 0.3f));
                Need(H - 112, "Thirst", needs.Thirst / 100f, needs.Thirst < 20f ? new Color(0.95f, 0.35f, 0.3f) : new Color(0.4f, 0.7f, 0.95f));
                float load = Items.Load;
                Need(H - 96, "Load", load / 1.3f, load > 1f ? new Color(0.95f, 0.35f, 0.3f) : new Color(0.7f, 0.7f, 0.72f));
            }

            var wp = Items.Equipped;
            string weapon = wp != null ? Items.Def(wp.id).name : "Fists";
            Panel(new Rect(16, H - 78, 260, 28));
            GUI.Label(new Rect(24, H - 76, 170, 24), weapon, mid);
            for (int i = 0; i < 3; i++)
            {
                GUI.color = i < MeleeCombat.ComboStep ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 1f, 1f, 0.2f);
                GUI.DrawTexture(new Rect(210 + i * 20, H - 70, 14, 12), white);
            }
            GUI.color = Color.white;
        }

        // ---- crosshair
        GUI.color = new Color(1f, 1f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(W / 2f - 2, H / 2f - 2, 4, 4), white);
        GUI.color = Color.white;

        // ---- what the keys do right here
        float py = H - 190f;
        if (player != null && !GameClock.Sleeping)
        {
            if (!string.IsNullOrEmpty(player.Prompt))
            {
                KeyHint(W / 2f, py, Keybinds.Describe(GameAction.Interact), player.Prompt);
                if (player.HoldProgress >= 0f)
                    Bar(new Rect(W / 2f - 130, py + 34, 260, 12), player.HoldProgress, new Color(1f, 0.82f, 0.35f));
                py -= 46f;
            }
            if (!string.IsNullOrEmpty(player.RopePrompt))
            {
                KeyHint(W / 2f, py, Keybinds.Describe(GameAction.Grapple), player.RopePrompt);
                py -= 40f;
            }
        }

        // ---- selected kit (only when you carry any)
        int kits = 0;
        foreach (var k in Items.BuildKits) kits += Items.Count(k);
        if (kits > 0 && player != null && !player.OnStreet)
        {
            string kit = Items.SelectedKit;
            string label = Items.Def(kit).name + " x" + Items.Count(kit);
            float kw = 330f;
            Panel(new Rect(W - kw - 12, H - 78, kw, 62));
            GUI.Label(new Rect(W - kw, H - 76, kw - 20, 24), "Kit: " + label, mid);
            GUI.Label(new Rect(W - kw, H - 52, kw - 20, 22),
                Keybinds.Describe(GameAction.Build) + " place     " + Keybinds.Describe(GameAction.CycleBuild) + " next kit", small);
        }

        // ---- toast
        if (Time.unscaledTime < toastUntil)
        {
            float tw = Mathf.Min(W - 80f, centre.CalcSize(new GUIContent(toast)).x + 40f);
            Panel(new Rect(W / 2f - tw / 2f, H - 104, tw, 34), 0.6f);
            GUI.Label(new Rect(W / 2f - tw / 2f, H - 104, tw, 34), toast, centre);
        }

        // ---- controls reminder for the first minute, then out of the way
        if (startedAt >= 0f && Time.unscaledTime - startedAt < 60f)
        {
            string K(GameAction a) => Keybinds.Describe(a);
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            GUI.Label(new Rect(300, H - 30, W - 320, 22),
                K(GameAction.Interact) + " use    " + K(GameAction.Inventory) + " craft    " + K(GameAction.Attack) + " attack    "
                + K(GameAction.Heavy) + " heavy    " + K(GameAction.SwapWeapon) + " weapon    " + K(GameAction.Grapple) + " rope    "
                + K(GameAction.Use) + " heal    " + K(GameAction.Map) + " map    " + K(GameAction.ToggleView) + " view    " + K(GameAction.Pause) + " menu", small);
            GUI.color = Color.white;
        }
    }
}
