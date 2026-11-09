using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>Every player action that can be bound to a key or mouse button.</summary>
public enum GameAction
{
    Forward, Back, Left, Right, Jump, Sprint, Interact, Build, Grapple, Use,
    Inventory, Pause, FastForward, Attack, Heavy, ToggleView, CycleBuild, SwapWeapon
}

/// <summary>
/// Customisable key bindings. Each action has one key or mouse button, saved in PlayerPrefs.
/// Binding a key that another action uses swaps the two, so no key ever triggers two actions.
/// Works with the new Input System and the legacy Input Manager.
/// </summary>
public static class Keybinds
{
    public static readonly string[] Labels =
    {
        "Move forward", "Move back", "Move left", "Move right", "Jump", "Sprint",
        "Interact", "Place selected kit", "Use the rope", "Use bandage / eat",
        "Inventory and crafting", "Pause menu", "Fast forward time", "Attack (combo)",
        "Heavy strike", "Toggle third-person view", "Next kit", "Switch weapon"
    };

    static readonly KeyCode[] Defaults =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.Space, KeyCode.LeftShift,
        KeyCode.E, KeyCode.T, KeyCode.G, KeyCode.Q,
        KeyCode.Tab, KeyCode.Escape, KeyCode.N, KeyCode.Mouse0,
        KeyCode.Mouse1, KeyCode.V, KeyCode.R, KeyCode.F
    };

    static readonly KeyCode[] binds = new KeyCode[Defaults.Length];
    static bool loaded;
    static readonly Dictionary<KeyCode, string> keyNameCache = new Dictionary<KeyCode, string>();

    public static int Count => Defaults.Length;

    static string PrefKey(int i) => "bind_" + ((GameAction)i);

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        for (int i = 0; i < Defaults.Length; i++)
        {
            string saved = PlayerPrefs.GetString(PrefKey(i), "");
            KeyCode k = KeyCode.None;
            bool ok = saved.Length > 0 && Enum.TryParse(saved, out k) && Supported(k);
            binds[i] = ok ? k : Defaults[i];
        }
    }

    public static KeyCode Get(GameAction a)
    {
        Load();
        return binds[(int)a];
    }

    /// <summary>Bind an action to a key. If another action had that key, it takes this action's old key.</summary>
    public static void Set(GameAction a, KeyCode k)
    {
        Load();
        int idx = (int)a;
        for (int i = 0; i < binds.Length; i++)
            if (i != idx && binds[i] == k) binds[i] = binds[idx];
        binds[idx] = k;
        Save();
    }

    public static void ResetAll()
    {
        Load();
        for (int i = 0; i < Defaults.Length; i++) binds[i] = Defaults[i];
        Save();
    }

    static void Save()
    {
        for (int i = 0; i < Defaults.Length; i++) PlayerPrefs.SetString(PrefKey(i), binds[i].ToString());
        PlayerPrefs.Save();
    }

    /// <summary>True for keys we can bind: mouse buttons, and keyboard keys the active input system knows.</summary>
    public static bool Supported(KeyCode k)
    {
        if (k == KeyCode.None) return false;
        if (k == KeyCode.Mouse0 || k == KeyCode.Mouse1 || k == KeyCode.Mouse2) return true;
#if ENABLE_INPUT_SYSTEM
        return ToKeyControl(k) != Key.None;
#else
        return true;
#endif
    }

    public static string Label(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.Mouse0: return "Left click";
            case KeyCode.Mouse1: return "Right click";
            case KeyCode.Mouse2: return "Middle click";
            default:
                if (!keyNameCache.TryGetValue(k, out var s))
                {
                    s = k.ToString();
                    keyNameCache[k] = s;
                }
                return s;
        }
    }

    public static string Describe(GameAction a) => Label(Get(a));

    // ---- Runtime checks ----

    public static bool Held(GameAction a)
    {
        KeyCode k = Get(a);
#if ENABLE_INPUT_SYSTEM
        if (IsMouse(k)) { var b = MouseButton(k); return b != null && b.isPressed; }
        var key = ToKeyControl(k);
        return key != Key.None && Keyboard.current != null && Keyboard.current[key].isPressed;
#else
        return Input.GetKey(k);
#endif
    }

    public static bool Pressed(GameAction a)
    {
        KeyCode k = Get(a);
#if ENABLE_INPUT_SYSTEM
        if (IsMouse(k)) { var b = MouseButton(k); return b != null && b.wasPressedThisFrame; }
        var key = ToKeyControl(k);
        return key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
#else
        return Input.GetKeyDown(k);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    static readonly Dictionary<KeyCode, Key> keyMap = new Dictionary<KeyCode, Key>();

    static bool IsMouse(KeyCode k) => k == KeyCode.Mouse0 || k == KeyCode.Mouse1 || k == KeyCode.Mouse2;

    static ButtonControl MouseButton(KeyCode k)
    {
        var m = Mouse.current;
        if (m == null) return null;
        return k == KeyCode.Mouse1 ? m.rightButton : k == KeyCode.Mouse2 ? m.middleButton : m.leftButton;
    }

    /// <summary>Maps a legacy KeyCode to the new Input System's Key. Returns Key.None if there is no match.</summary>
    static Key ToKeyControl(KeyCode k)
    {
        if (keyMap.TryGetValue(k, out var key)) return key;
        key = Enum.TryParse(k.ToString(), out Key parsed) ? parsed : Key.None;
        keyMap[k] = key;
        return key;
    }

#endif
}
