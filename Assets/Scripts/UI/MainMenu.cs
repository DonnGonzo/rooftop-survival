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
        y = Screen.height * 0.12f;

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
            if (GUI.Button(new Rect(x, y, w, 30), Keybinds.Labels[i] + "   [" + shown + "]", button))
                capturing = i;
            y += 34;
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
        Label(K(GameAction.Forward) + K(GameAction.Left) + K(GameAction.Back) + K(GameAction.Right) +
              " move, " + K(GameAction.Sprint) + " sprint, " + K(GameAction.Jump) + " jump, mouse look.", 24);
        Label(K(GameAction.Interact) + ": pick up, plant, water or harvest.   " + K(GameAction.Build) +
              ": place a spike trap (trap kit).", 24);
        Label(K(GameAction.Grapple) + ": rappel down at the roof edge, or climb back up at the base.", 24);
        Label(K(GameAction.Inventory) + ": inventory and crafting.   " + K(GameAction.Use) + ": use a bandage or food.", 24);
        Label(K(GameAction.Attack) + ": attack. Keep clicking quickly to chain a combo.   " + K(GameAction.Heavy) +
              ": heavy swing.   " + K(GameAction.ToggleView) + ": third-person view.", 40);
        Label("Be back on the roof before dusk. Nights 1-10 are safe. Night 11 is when attacks begin.", 40);
        Label(K(GameAction.Pause) + ": pause menu. Keys can be changed in Settings > Controls.", 24);
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
