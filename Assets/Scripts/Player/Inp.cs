using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Player input. Every key comes from Keybinds, so the player can change any of them in Settings > Controls.
/// Works with the new Input System or the legacy Input Manager.
/// </summary>
public static class Inp
{
    public static Vector2 Move()
    {
        float x = (Keybinds.Held(GameAction.Right) ? 1f : 0f) - (Keybinds.Held(GameAction.Left) ? 1f : 0f);
        float y = (Keybinds.Held(GameAction.Forward) ? 1f : 0f) - (Keybinds.Held(GameAction.Back) ? 1f : 0f);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    public static Vector2 Look()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f;
#endif
    }

    public static bool Interact() => Keybinds.Pressed(GameAction.Interact);
    public static bool Build() => Keybinds.Pressed(GameAction.Build);
    public static bool CycleBuild() => Keybinds.Pressed(GameAction.CycleBuild);
    public static bool Grapple() => Keybinds.Pressed(GameAction.Grapple);
    public static bool Jump() => Keybinds.Pressed(GameAction.Jump);
    public static bool Sprint() => Keybinds.Held(GameAction.Sprint);
    public static bool FastForward() => Keybinds.Held(GameAction.FastForward);
    public static bool Use() => Keybinds.Pressed(GameAction.Use);
    public static bool Inventory() => Keybinds.Pressed(GameAction.Inventory);
    public static bool Pause() => Keybinds.Pressed(GameAction.Pause);
    public static bool Attack() => Keybinds.Pressed(GameAction.Attack);
    public static bool Heavy() => Keybinds.Pressed(GameAction.Heavy);
    public static bool ToggleView() => Keybinds.Pressed(GameAction.ToggleView);
}
