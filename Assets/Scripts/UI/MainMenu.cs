using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Start menu, pause menu, character select, settings and help. Esc opens or closes it during play.</summary>
public class MainMenu : MonoBehaviour
{
    enum Page { Main, Characters, Settings, Help }

    Page page = Page.Main;
    GUIStyle title, button, text;
    float x, y, w;

    void Update()
    {
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

        switch (page)
        {
            case Page.Main: DrawMain(); break;
            case Page.Characters: DrawCharacters(); break;
            case Page.Settings: DrawSettings(); break;
            case Page.Help: DrawHelp(); break;
        }
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
        if (Btn("Back"))
        {
            PlayerPrefs.Save();
            page = Page.Main;
        }
    }

    void DrawHelp()
    {
        Label("WASD move, Shift sprint, Space jump, mouse look.", 24);
        Label("E: pick up, plant, water or harvest.   T: place a spike trap (trap kit).", 24);
        Label("G: rappel down at the roof edge, or climb back up at the base.", 24);
        Label("Tab: inventory and crafting. Q: use a bandage or food.", 24);
        Label("Left click: attack. Keep clicking quickly to chain a combo. Right click: heavy swing.", 40);
        Label("Be back on the roof before dusk. Nights 1-10 are safe. Night 11 is when attacks begin.", 40);
        Label("Esc: pause menu.", 24);
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
