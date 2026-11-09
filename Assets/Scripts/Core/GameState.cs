using UnityEngine;

/// <summary>Global flags: which screen is showing and whether the world should move.</summary>
public static class GameState
{
    public static bool Playing;          // the player is in the world
    public static bool MenuOpen = true;  // start / pause menu is showing
    public static bool UiOpen;           // inventory and crafting panel is showing
    public static bool HasStarted;       // a run has been started this session
    public static bool AutoStart;        // skip the menu after a scene reload (New Run / overrun)

    /// <summary>True when the world should not move: menu open, panel open, or no run yet.</summary>
    public static bool Frozen => !Playing || MenuOpen || UiOpen;

    /// <summary>Locks the mouse only while the player is free to look around.</summary>
    public static void Refresh()
    {
        bool free = !Frozen;
        Cursor.lockState = free ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !free;
    }
}
