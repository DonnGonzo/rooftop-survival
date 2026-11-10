using UnityEngine;

/// <summary>First-run tutorial. Each step advances when the player does the thing. Progress is saved.</summary>
public static class Tutorial
{
    static readonly string[] Ids = { "move", "search", "planter", "plant", "water", "attack", "rappel", "gather", "craft", "bench", "roof", "sleep" };

    static string K(GameAction a) => Keybinds.Describe(a);

    static string Text(int i)
    {
        switch (Ids[i])
        {
            case "move": return "Look with the mouse, walk with " + K(GameAction.Forward) + K(GameAction.Left) + K(GameAction.Back) + K(GameAction.Right) + ". Hold " + K(GameAction.Sprint) + " to run.";
            case "search": return "Someone left crates on this roof. Stand at one and HOLD " + K(GameAction.Interact) + " to search it.";
            case "planter": return "You carry a planter kit. Face a clear spot and press " + K(GameAction.Build) + " to place it.";
            case "plant": return "Press " + K(GameAction.Interact) + " at the planter to plant seeds.";
            case "water": return "Press " + K(GameAction.Interact) + " again to water it. A watered crop grows one stage each dawn.";
            case "attack": return K(GameAction.Attack) + " strikes; three in a row is a combo. " + K(GameAction.Heavy) + " is a heavy blow. Throw a punch.";
            case "rappel": return "Go to the yellow rope anchor on the north wall and press " + K(GameAction.Grapple) + " to rappel to the street.";
            case "gather": return "Hit a tree for Wood, rubble or rock for Stone, a wrecked car for Scrap. Hold " + K(GameAction.Interact) + " on bushes and grass.";
            case "craft": return "Press " + K(GameAction.Inventory) + " and make a nail club (3 Wood + 1 Scrap).";
            case "bench": return "Make a workbench kit (8 Wood + 4 Stone) and place it on your roof with " + K(GameAction.Build) + ". Better gear is built there.";
            case "roof": return "Return to the rope and press " + K(GameAction.Grapple) + " to climb home. Be up before night. " + K(GameAction.Map) + " shows the map.";
            case "sleep": return "After dark, sleep in the sleeping bag (" + K(GameAction.Interact) + "). You eat 1 Food and drink 1 Water each dawn.";
        }
        return "";
    }

    const string SaveKey = "tut_step_v3";
    static int index = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { index = -1; }

    static void Load()
    {
        if (index >= 0) return;
        index = PlayerPrefs.GetInt(SaveKey, 0);
    }

    public static bool Active
    {
        get
        {
            Load();
            return GameSettings.ShowTutorial && index < Ids.Length;
        }
    }

    public static string Current => Active ? Text(index) : "";
    public static string Progress { get { Load(); return (Mathf.Min(index, Ids.Length - 1) + 1) + " of " + Ids.Length; } }

    public static void Notify(string id)
    {
        Load();
        if (index >= Ids.Length || Ids[index] != id) return;

        index++;
        PlayerPrefs.SetInt(SaveKey, index);
        if (index >= Ids.Length)
            Hud.Banner("You know the basics. Nights 1-10 are safe: level up and build defenses before night 11.", 8f);
    }

    public static void Reset()
    {
        index = 0;
        PlayerPrefs.SetInt(SaveKey, 0);
    }
}
