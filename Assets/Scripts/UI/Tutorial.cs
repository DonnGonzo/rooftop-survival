using UnityEngine;

/// <summary>First-run tutorial. Each step advances when the player does the thing. Progress is saved.</summary>
public static class Tutorial
{
    static readonly string[] Ids = { "move", "plant", "water", "rappel", "pickup", "craft", "trap", "attack", "roof" };
    static readonly string[] Texts =
    {
        "Walk with WASD. Hold Shift to sprint, Space to jump.",
        "Stand next to a planter and press E to plant a seed.",
        "Press E again to water it. Crops grow one stage each dawn if watered.",
        "Stand at the roof edge and press G to rappel down to the street.",
        "Find a supply crate on the street and press E to take what's inside.",
        "Press Tab to craft. Make a club (3 wood + 1 scrap) or a trap kit (2 scrap + 1 wood).",
        "On the roof, press T to place a spike trap using a trap kit.",
        "Left click to attack. Click again quickly to chain a combo. Right click for a heavy swing.",
        "Get back on the roof before dusk. Night 11 is when the attacks start.",
    };

    const string SaveKey = "tut_step";
    static int index = -1;

    static void Load()
    {
        if (index >= 0) return;
        index = GameSettings.ShowTutorial ? PlayerPrefs.GetInt(SaveKey, 0) : Ids.Length;
    }

    public static bool Active
    {
        get
        {
            Load();
            return index < Ids.Length;
        }
    }

    public static string Current => Active ? Texts[index] : "";

    public static void Notify(string id)
    {
        Load();
        if (index >= Ids.Length || Ids[index] != id) return;

        index++;
        PlayerPrefs.SetInt(SaveKey, index);
        Hud.Toast(index < Ids.Length ? "Tutorial step done!" : "Tutorial complete. You're on your own now.");
    }

    public static void Reset()
    {
        index = 0;
        PlayerPrefs.SetInt(SaveKey, 0);
    }
}
