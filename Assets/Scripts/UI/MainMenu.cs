using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The title screen and the pause menu. Before a run starts, the world is shown behind the menu from a
/// camera that drifts around your rooftop. Pages: main, characters, settings, controls, how to play.
/// </summary>
public class MainMenu : MonoBehaviour
{
    enum Page { Main, Characters, Settings, Controls, Help }

    Page page = Page.Main;
    float x, y, w;
    int capturing = -1;   // index of the GameAction waiting for a new key, or -1
    Camera orbit;
    float angle;
    Vector2 scroll;

    void Start()
    {
        // A camera of its own for the title screen, so the player's view is not disturbed.
        var go = new GameObject("MenuCamera");
        orbit = go.AddComponent<Camera>();
        orbit.fieldOfView = 55f;
        orbit.nearClipPlane = 0.3f;
        orbit.farClipPlane = 1400f;
        orbit.depth = 10;
        orbit.enabled = false;
    }

    void Update()
    {
        bool title = GameState.MenuOpen && !GameState.HasStarted;
        if (orbit != null)
        {
            if (orbit.enabled != title) orbit.enabled = title;
            if (title)
            {
                angle += Time.unscaledDeltaTime * 4f;
                float r = Mathf.Max(RoofGenerator.Size.x, RoofGenerator.Size.y) * 0.5f + 22f;
                orbit.transform.position = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad) * r, 11f, Mathf.Sin(angle * Mathf.Deg2Rad) * r);
                orbit.transform.LookAt(new Vector3(0f, 1.5f, 0f));
            }
        }

        if (capturing >= 0) return;   // the next key belongs to the rebind, not to the menu
        if (!Inp.Pause()) return;

        if (GameState.MenuOpen)
        {
            if (page != Page.Main) page = Page.Main;
            else if (GameState.HasStarted) Resume();
        }
        else if (GameState.Playing && !MapView.Showing)
        {
            GameState.UiOpen = false;
            GameState.MenuOpen = true;
            page = Page.Main;
        }
    }

    void OnGUI()
    {
        if (!GameState.MenuOpen) return;
        Skin.Ensure();
        GUI.depth = -10;
        float W = Screen.width, H = Screen.height;

        // Paused: dim the game. Title: let the drifting view show, with a dark band for the menu.
        if (GameState.HasStarted) Skin.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.62f));
        float band = Mathf.Min(560f, W * 0.42f);
        Skin.Fill(new Rect(0, 0, band, H), new Color(0.05f, 0.06f, 0.08f, 0.9f));
        Skin.Fill(new Rect(band, 0, 3, H), Skin.Accent);
        for (int i = 0; i < 6; i++) Skin.Fill(new Rect(band + 3 + i * 14, 0, 14, H), new Color(0.05f, 0.06f, 0.08f, 0.5f - i * 0.085f));

        x = 54f;
        w = band - 108f;
        y = Mathf.Max(30f, H * 0.09f);

        GUI.Label(new Rect(x, y, w + 60, 54), "ROOFTOP", Skin.Title); y += 50;
        GUI.Label(new Rect(x, y, w + 60, 54), "SURVIVAL", Skin.Title); y += 60;
        Skin.Fill(new Rect(x, y, 90, 4), Skin.Accent); y += 14;
        GUI.Label(new Rect(x, y, w, 24), GameState.HasStarted ? "PAUSED" : "Hold the roof. Search the ruins. Be home by dark.", Skin.Tagline);
        y += 40;

        if (capturing >= 0) HandleCapture();

        switch (page)
        {
            case Page.Main: DrawMain(); break;
            case Page.Characters: DrawCharacters(); break;
            case Page.Settings: DrawSettings(); break;
            case Page.Controls: DrawControls(H); break;
            case Page.Help: DrawHelp(); break;
        }

        // The run so far, bottom right.
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run != null && page == Page.Main)
        {
            var card = new Rect(W - 340, H - 150, 316, 126);
            Skin.Box(card, new Color(0.07f, 0.08f, 0.1f, 0.88f));
            GUI.Label(new Rect(card.x + 16, card.y + 10, 290, 22), RunManager.Instance.HasSave ? "YOUR RUN" : "NEW RUN", Skin.H2);
            GUI.Label(new Rect(card.x + 16, card.y + 34, 290, 22), "Day " + run.day + "     Level " + Progress.Level + "     " + Characters.Current.name, Skin.Body);
            GUI.Label(new Rect(card.x + 16, card.y + 56, 290, 22), "Roof quality " + Mathf.RoundToInt(run.roofQuality * 100f) + "%     Drifters: " + Faction.Standing, Skin.DimText);
            GUI.Label(new Rect(card.x + 16, card.y + 78, 290, 22), "Longest held: " + PlayerPrefs.GetInt("BestNight", 0) + " nights", Skin.DimText);
            GUI.Label(new Rect(card.x + 16, card.y + 100, 290, 20), Mathf.Max(0, 11 - run.day) > 0 ? (11 - run.day) + " safe nights left" : "Raids can come any night", Skin.Small);
        }
    }

    /// <summary>Takes the next key or mouse button as the new binding. Esc cancels.</summary>
    void HandleCapture()
    {
        Event e = Event.current;
        KeyCode pressed = KeyCode.None;
        if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None) pressed = e.keyCode;
        else if (e.type == EventType.MouseDown && e.button <= 2)
            pressed = e.button == 0 ? KeyCode.Mouse0 : e.button == 1 ? KeyCode.Mouse1 : KeyCode.Mouse2;
        if (pressed == KeyCode.None) return;

        e.Use();
        if (pressed == KeyCode.Escape)
        {
            capturing = -1;
            return;
        }
        if (!Keybinds.Supported(pressed))
        {
            Hud.Toast("That key can't be bound. Try another.");
            capturing = -1;
            return;
        }
        var action = (GameAction)capturing;
        Keybinds.Set(action, pressed);
        capturing = -1;
    }

    void DrawMain()
    {
        bool hasSave = RunManager.Instance != null && RunManager.Instance.HasSave;

        if (GameState.HasStarted)
        {
            if (Big("Resume")) Resume();
        }
        else
        {
            if (Big(hasSave ? "Continue" : "Start")) Begin();
        }
        y += 6;
        if (Btn(GameState.HasStarted ? "New run  (abandon this roof)" : "New run  (new roof, new world)")) NewRun();
        if (Btn("Character:  " + Characters.Current.name)) page = Page.Characters;
        if (Btn("Settings")) page = Page.Settings;
        if (Btn("How to play")) page = Page.Help;
        if (Btn("Quit")) Application.Quit();
    }

    void DrawCharacters()
    {
        Head("CHARACTER");
        for (int i = 0; i < Characters.All.Length; i++)
        {
            var c = Characters.All[i];
            bool on = GameSettings.CharacterIndex == i;
            string label = "<b>" + c.name + "</b>\n<color=#a0a4ac>Health " + c.maxHealth.ToString("F0") + "    Speed x" + c.moveMult.ToString("0.00")
                         + "    Damage x" + c.damageMult.ToString("0.00") + (c.planterBonus > 0 ? "    +1 food per harvest" : "") + "</color>";
            if (GUI.Button(new Rect(x, y, w, 54), label, on ? Skin.CardOn : Skin.Card)) { GameSettings.CharacterIndex = i; Sfx.Play("click"); }
            y += 60;
        }
        GUI.Label(new Rect(x, y, w, 44), Characters.Current.blurb, Skin.Body); y += 46;
        GUI.Label(new Rect(x, y, w, 40), GameState.HasStarted ? "A change of character takes effect on your next run." : "", Skin.DimText); y += 30;
        if (Btn("Back")) page = Page.Main;
    }

    void DrawSettings()
    {
        Head("SETTINGS");
        GameSettings.Sensitivity = Slider("Mouse sensitivity", GameSettings.Sensitivity, 0.3f, 3f, GameSettings.Sensitivity.ToString("0.0"));
        float vol = Slider("Master volume", GameSettings.Volume, 0f, 1f, Mathf.RoundToInt(GameSettings.Volume * 100f) + "%");
        if (vol != GameSettings.Volume) GameSettings.Volume = vol;
        GameSettings.MusicVolume = Slider("Music", GameSettings.MusicVolume, 0f, 1f, Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%");
        GameSettings.SfxVolume = Slider("Sounds and voices", GameSettings.SfxVolume, 0f, 1f, Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%");
        y += 4;
        if (Btn("Invert Y axis:  " + (GameSettings.InvertY ? "On" : "Off"))) GameSettings.InvertY = !GameSettings.InvertY;
        if (Btn("Tutorial hints:  " + (GameSettings.ShowTutorial ? "On" : "Off"))) GameSettings.ShowTutorial = !GameSettings.ShowTutorial;
        if (Btn("Restart the tutorial"))
        {
            GameSettings.ShowTutorial = true;
            Tutorial.Reset();
        }
        if (Btn("Controls")) page = Page.Controls;
        if (Btn("Back"))
        {
            PlayerPrefs.Save();
            page = Page.Main;
        }
    }

    float Slider(string name, float value, float min, float max, string shown)
    {
        GUI.Label(new Rect(x, y, w - 70, 20), name, Skin.Body);
        GUI.Label(new Rect(x + w - 70, y, 70, 20), shown, Skin.DimText);
        y += 22;
        float v = GUI.HorizontalSlider(new Rect(x, y, w, 18), value, min, max);
        y += 26;
        return v;
    }

    /// <summary>Every action with its current key. Click one, then press the new key. Esc cancels.</summary>
    void DrawControls(float H)
    {
        Head("CONTROLS");
        GUI.Label(new Rect(x, y, w, 36), "Click an action, then press the key or mouse button to use. Esc cancels.", Skin.DimText);
        y += 40;
        float listH = H - y - 120f;
        scroll = GUI.BeginScrollView(new Rect(x, y, w + 16, listH), scroll, new Rect(0, 0, w - 4, Keybinds.Count * 32f));
        for (int i = 0; i < Keybinds.Count; i++)
        {
            string shown = capturing == i ? "press a key..." : Keybinds.Describe((GameAction)i);
            if (GUI.Button(new Rect(0, i * 32f, w - 4, 29), Keybinds.Labels[i] + "     [ " + shown + " ]", capturing == i ? Skin.CardOn : Skin.Button))
                capturing = i;
        }
        GUI.EndScrollView();
        y += listH + 8;
        if (Btn("Reset to defaults")) Keybinds.ResetAll();
        if (Btn("Back"))
        {
            capturing = -1;
            page = Page.Settings;
        }
    }

    void DrawHelp()
    {
        Head("HOW TO PLAY");
        string K(GameAction a) => Keybinds.Describe(a);
        Para("You hold a rooftop above a ruined city ringed by forest. Days are for gathering; be home by dark.");
        Para("<b>" + K(GameAction.Interact) + "</b>  Use what is in front of you. HOLD it to search furniture and crates, pick plants, cook, and build at a workbench.");
        Para("<b>" + K(GameAction.Attack) + " / " + K(GameAction.Heavy) + "</b>  Strike. Trees give Wood, rock Stone, wrecks and ore Scrap. They come back after a few minutes. <b>"
             + K(GameAction.SwapWeapon) + "</b> switches weapon.");
        Para("<b>" + K(GameAction.Inventory) + "</b>  Pack, crafting and your character. You can only carry so much: craft a pack, build a storage chest, spend level points on Carrying.");
        Para("<b>" + K(GameAction.Use) + "</b>  Eat, drink or bandage, whichever you need most. Hunger and thirst fall all day. Cook meat and boil lake water at a campfire.");
        Para("<b>" + K(GameAction.Build) + "</b> places the selected kit on the roof, <b>" + K(GameAction.CycleBuild) + "</b> picks the next. <b>"
             + K(GameAction.Grapple) + "</b> uses the rope at the yellow anchor. <b>" + K(GameAction.Map) + "</b> opens the map.");
        Para("The Drifters camp out there by day. Save them from animals and share food and they become friends; attack or rob them and they hunt you.");
        Para("Animals come from dens and grow in number and kind each day. From night 11, raids climb your building.");
        if (Btn("Back")) page = Page.Main;
    }

    void Begin()
    {
        GameState.HasStarted = true;
        GameState.MenuOpen = false;
        GameState.Playing = true;
        Sfx.Play("click");
    }

    void Resume()
    {
        GameState.MenuOpen = false;
        GameState.Playing = true;
    }

    void NewRun()
    {
        RunManager.Instance.StartNewRun();
        GameState.AutoStart = true;
        GameState.Playing = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Head(string text)
    {
        GUI.Label(new Rect(x, y, w, 26), text, Skin.H1);
        y += 34;
    }

    void Para(string text)
    {
        float h = Skin.Body.CalcHeight(new GUIContent(text), w);
        GUI.Label(new Rect(x, y, w, h), text, Skin.Body);
        y += h + 8;
    }

    bool Big(string label)
    {
        bool clicked = GUI.Button(new Rect(x, y, w, 54), label, Skin.ButtonAccent);
        y += 62;
        if (clicked) Sfx.Play("click");
        return clicked;
    }

    bool Btn(string label)
    {
        bool clicked = GUI.Button(new Rect(x, y, w, 40), label, Skin.Button);
        y += 46;
        if (clicked) Sfx.Play("click");
        return clicked;
    }
}
