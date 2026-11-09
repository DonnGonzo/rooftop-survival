using UnityEngine;

/// <summary>First-run tutorial. Each step advances when the player does the thing. Progress is saved.</summary>
public static class Tutorial
{
    static readonly string[] Ids = { "move", "pickup", "planter", "plant", "water", "craft", "attack", "rappel", "loot", "roof", "sleep" };

    static string K(GameAction a) => Keybinds.Describe(a);

    static string Text(int i)
    {
        switch (Ids[i])
        {
            case "move": return "Look around with the mouse and walk with " + K(GameAction.Forward) + K(GameAction.Left) + K(GameAction.Back) + K(GameAction.Right) + ". Hold " + K(GameAction.Sprint) + " to run.";
            case "pickup": return "There are supply caches on your roof. Walk up to one and press " + K(GameAction.Interact) + ".";
            case "planter": return "You carry a planter kit. Face a clear spot and press " + K(GameAction.Build) + " to place it.";
            case "plant": return "Stand at the planter and press " + K(GameAction.Interact) + " to plant seeds.";
            case "water": return "Press " + K(GameAction.Interact) + " again to water it. A watered crop grows one stage each dawn.";
            case "craft": return "Press " + K(GameAction.Inventory) + " and craft a nail club (3 Wood + 1 Scrap).";
            case "attack": return K(GameAction.Attack) + " attacks; three in a row is a combo. " + K(GameAction.Heavy) + " is a heavy strike. Try a swing.";
            case "rappel": return "Go to the yellow rope anchor on the north wall and press " + K(GameAction.Grapple) + " to rappel to the street.";
            case "loot": return "Search the street. Doorways lead into buildings; crates, sacks and cases hold supplies.";
            case "roof": return "Return to the rope and press " + K(GameAction.Grapple) + " to climb home. Be up before night.";
            case "sleep": return "After dark, sleep on the cot (" + K(GameAction.Interact) + "). You eat 1 Food and drink 1 Water each dawn.";
        }
        return "";
    }

    const string SaveKey = "tut_step_v2";
    static int index = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { index = -1; }

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
            Hud.Banner("You know the basics. Nights 1-10 are safe: build traps and barricades before night 11.", 8f);
    }

    public static void Reset()
    {
        index = 0;
        PlayerPrefs.SetInt(SaveKey, 0);
    }
}
