using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tiny input wrapper that works with either Unity's new Input System or the legacy Input Manager,
/// whichever your project has enabled.
/// </summary>
public static class Inp
{
#if ENABLE_INPUT_SYSTEM
    public static Vector2 Move()
    {
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
        float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }
    public static Vector2 Look() { return Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero; }
    public static bool Interact() { return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame; }
    public static bool Build() { return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame; }
    public static bool Jump() { return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame; }
    public static bool Sprint() { return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed; }
    public static bool FastForward() { return Keyboard.current != null && Keyboard.current.nKey.isPressed; }
#else
    public static Vector2 Move() { return Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f); }
    public static Vector2 Look() { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f; }
    public static bool Interact() { return Input.GetKeyDown(KeyCode.E); }
    public static bool Build() { return Input.GetKeyDown(KeyCode.T); }
    public static bool Jump() { return Input.GetKeyDown(KeyCode.Space); }
    public static bool Sprint() { return Input.GetKey(KeyCode.LeftShift); }
    public static bool FastForward() { return Input.GetKey(KeyCode.N); }
#endif
}
