using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Start menu, pause menu, character select, settings and help. Esc opens or closes it during play.</summary>
public class MainMenu : MonoBehaviour
{
    enum Page { Main, Characters, Settings, Controls, Help }

    Page page = Page.Main;
    GUIStyle title, button, text;
    float x, y, w;
    int capturing = -1;   // index of the GameAction waiting for a new key, or -1

    void Update()
    {
        if (capturing >= 0) return;   // the next key belongs to the rebind, not to the menu
        if (!Inp.Pause()) return;

        if (GameState.MenuOpen)
        {
            if (GameState.HasStarted) Resume();
        }
        else if (GameState.Playing)
        {
            GameState.UiOpen = false;
            GameState.MenuOpen = true;
            page = Page.Main;
        }
    }

    void OnGUI()
    {
        if (!GameState.MenuOpen) return;
        Styles();

        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        w = 480f;
        x = (Screen.width - w) / 2f;
        y = Mathf.Max(10f, Screen.height * 0.06f);

        GUI.Label(new Rect(x, y, w, 60), "ROOFTOP SURVIVAL", title);
        y += 64;
        GUI.Label(new Rect(x, y, w, 24), GameState.HasStarted ? "Paused" : "Survive the city above the ruins", text);
        y += 36;

        if (capturing >= 0) HandleCapture();

        switch (page)
        {
            case Page.Main: DrawMain(); break;
            case Page.Characters: DrawCharacters(); break;
            case Page.Settings: DrawSettings(); break;
            case Page.Controls: DrawControls(); break;
            case Page.Help: DrawHelp(); break;
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
        Hud.Toast(Keybinds.Labels[capturing] + " is now " + Keybinds.Label(pressed) + ".");
        capturing = -1;
    }

    void DrawMain()
    {
        bool hasSave = RunManager.Instance != null && RunManager.Instance.HasSave;

        if (GameState.HasStarted)
        {
            if (Btn("Resume")) Resume();
        }
        else if (hasSave)
        {
            if (Btn("Continue")) Begin();
        }

        if (Btn(GameState.HasStarted ? "New Run (abandon this base)" : "New Run")) NewRun();

        y += 10;
        if (Btn("Character: " + Characters.Current.name)) page = Page.Characters;
        if (Btn("Settings")) page = Page.Settings;
        if (Btn("How to play")) page = Page.Help;
        if (Btn("Quit")) Application.Quit();
    }

    void DrawCharacters()
    {
        for (int i = 0; i < Characters.All.Length; i++)
        {
            var c = Characters.All[i];
            string mark = GameSettings.CharacterIndex == i ? "> " : "   ";
            string stats = "  HP " + c.maxHealth.ToString("F0") + "  speed x" + c.moveMult.ToString("0.00") +
                           "  dmg x" + c.damageMult.ToString("0.00");
            if (Btn(mark + c.name + stats)) GameSettings.CharacterIndex = i;
        }
        y += 6;
        Label(Characters.Current.blurb, 48);
        if (Btn("Back")) page = Page.Main;
    }

    void DrawSettings()
    {
        Label("Mouse sensitivity: " + GameSettings.Sensitivity.ToString("0.0"));
        GameSettings.Sensitivity = GUI.HorizontalSlider(new Rect(x, y, w, 20), GameSettings.Sensitivity, 0.3f, 3f);
        y += 30;

        Label("Volume: " + Mathf.RoundToInt(GameSettings.Volume * 100f) + "%");
        float vol = GUI.HorizontalSlider(new Rect(x, y, w, 20), GameSettings.Volume, 0f, 1f);
        if (vol != GameSettings.Volume) GameSettings.Volume = vol;
        y += 30;

        if (Btn("Invert Y axis: " + (GameSettings.InvertY ? "On" : "Off"))) GameSettings.InvertY = !GameSettings.InvertY;
        if (Btn("Tutorial hints: " + (GameSettings.ShowTutorial ? "On" : "Off"))) GameSettings.ShowTutorial = !GameSettings.ShowTutorial;
        if (Btn("Restart tutorial"))
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

    /// <summary>Every action with its current key. Click one, then press the new key. Esc cancels.</summary>
    void DrawControls()
    {
        Label("Click an action, then press the key or mouse button to use. Esc cancels.", 36);
        for (int i = 0; i < Keybinds.Count; i++)
        {
            string shown = capturing == i ? "press a key..." : Keybinds.Describe((GameAction)i);
            if (GUI.Button(new Rect(x, y, w, 26), Keybinds.Labels[i] + "   [" + shown + "]", button))
                capturing = i;
            y += 28;
        }
        y += 8;
        if (Btn("Reset controls to defaults"))
        {
            Keybinds.ResetAll();
            Hud.Toast("Controls reset to defaults.");
        }
        if (Btn("Back"))
        {
            capturing = -1;
            page = Page.Settings;
        }
    }

    void DrawHelp()
    {
        string K(GameAction a) => Keybinds.Describe(a);
        Label("You hold a rooftop above a ruined city ringed by forest. Days are for gathering; be home by dark.", 40);
        Label(K(GameAction.Interact) + ": use what is in front of you. HOLD it to search cupboards and crates, pick bushes, and build at a workbench.", 40);
        Label(K(GameAction.Attack) + " / " + K(GameAction.Heavy) + ": strike. Hit trees for Wood, rock for Stone, wrecks and ore for Scrap. "
              + K(GameAction.SwapWeapon) + " switches weapon.", 40);
        Label(K(GameAction.Inventory) + ": inventory and hand recipes. Tools, traps and weapons are ordered at a workbench, then built by holding "
              + K(GameAction.Interact) + " there. New recipes unlock as you level up.", 56);
        Label(K(GameAction.Build) + ": place the selected kit on the roof. " + K(GameAction.CycleBuild) + ": next kit. "
              + K(GameAction.Grapple) + ": use the rope at the yellow anchor. " + K(GameAction.Map) + ": map.", 40);
        Label("Each dawn you eat 1 Food and drink 1 Water. Animals come from dens and grow in number and kind every day. "
              + "From night 11, raids climb your building: traps, barricades and your own hands decide it.", 56);
        Label(K(GameAction.Use) + ": bandage or eat. " + K(GameAction.ToggleView) + ": third person. " + K(GameAction.Pause)
              + ": menu. Keys can be changed in Settings > Controls.", 40);
        if (Btn("Back")) page = Page.Main;
    }

    void Begin()
    {
        GameState.HasStarted = true;
        GameState.MenuOpen = false;
        GameState.Playing = true;
        if (Tutorial.Active) Hud.Toast("Tutorial: " + Tutorial.Current);
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

    bool Btn(string label)
    {
        bool clicked = GUI.Button(new Rect(x, y, w, 40), label, button);
        y += 48;
        return clicked;
    }

    void Label(string s, float h = 24f)
    {
        GUI.Label(new Rect(x, y, w, h), s, text);
        y += h + 4;
    }

    void Styles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title.normal.textColor = Color.white;
        text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
        text.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
    }
}
