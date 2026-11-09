using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Input wrapper that works with the new Input System or the legacy Input Manager.</summary>
public static class Inp
{
#if ENABLE_INPUT_SYSTEM
    static bool Held(Key k) => Keyboard.current != null && Keyboard.current[k].isPressed;
    static bool Pressed(Key k) => Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;

    static bool Click(bool right)
    {
        var m = Mouse.current;
        if (m == null) return false;
        return right ? m.rightButton.wasPressedThisFrame : m.leftButton.wasPressedThisFrame;
    }

    public static Vector2 Move()
    {
        float x = (Held(Key.D) ? 1f : 0f) - (Held(Key.A) ? 1f : 0f);
        float y = (Held(Key.W) ? 1f : 0f) - (Held(Key.S) ? 1f : 0f);
        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    public static Vector2 Look() => Mouse.current != null ? Mouse.current.delta.ReadValue() * 0.1f : Vector2.zero;
    public static bool Interact() => Pressed(Key.E);
    public static bool Build() => Pressed(Key.T);
    public static bool Grapple() => Pressed(Key.G);
    public static bool Jump() => Pressed(Key.Space);
    public static bool Sprint() => Held(Key.LeftShift);
    public static bool FastForward() => Held(Key.N);
    public static bool Use() => Pressed(Key.Q);
    public static bool Inventory() => Pressed(Key.Tab);
    public static bool Pause() => Pressed(Key.Escape);
    public static bool Attack() => Click(false);
    public static bool Heavy() => Click(true);
#else
    public static Vector2 Move() => Vector2.ClampMagnitude(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
    public static Vector2 Look() => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f;
    public static bool Interact() => Input.GetKeyDown(KeyCode.E);
    public static bool Build() => Input.GetKeyDown(KeyCode.T);
    public static bool Grapple() => Input.GetKeyDown(KeyCode.G);
    public static bool Jump() => Input.GetKeyDown(KeyCode.Space);
    public static bool Sprint() => Input.GetKey(KeyCode.LeftShift);
    public static bool FastForward() => Input.GetKey(KeyCode.N);
    public static bool Use() => Input.GetKeyDown(KeyCode.Q);
    public static bool Inventory() => Input.GetKeyDown(KeyCode.Tab);
    public static bool Pause() => Input.GetKeyDown(KeyCode.Escape);
    public static bool Attack() => Input.GetMouseButtonDown(0);
    public static bool Heavy() => Input.GetMouseButtonDown(1);
#endif
}
