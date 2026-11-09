using UnityEngine;

/// <summary>
/// First-person body feel, layered over the camera: head bob while walking, a wider field of view when sprinting,
/// a dip when landing hard, a shake when hurt, and a small kick when swinging a weapon.
/// Runs in LateUpdate so it sits on top of PlayerController's look rotation.
/// </summary>
public class CameraFeel : MonoBehaviour
{
    static float trauma;   // 0..1, decays. Set by Shake().
    static float kick;     // degrees of upward punch, decays. Set by Kick().

    public static void Shake(float amount) { trauma = Mathf.Clamp01(trauma + amount); }
    public static void Kick(float degrees) { kick += degrees; }

    Camera cam;
    Vector3 baseLocal;
    float baseFov, fovNow;
    float bobPhase, bobAmount, landDip;
    bool wasGrounded = true;
    float lastVelY;

    void Start()
    {
        cam = GetComponent<Camera>();
        baseLocal = transform.localPosition;
        baseFov = cam != null ? cam.fieldOfView : 60f;
        fovNow = baseFov;
    }

    void LateUpdate()
    {
        var p = PlayerController.Instance;
        if (p == null || cam == null) return;
        float dt = Time.deltaTime;

        bool moving = p.Grounded && p.HorizontalSpeed > 0.5f;

        // Head bob
        float targetBob = moving ? (p.Sprinting ? 0.055f : 0.03f) : 0f;
        bobAmount = Mathf.Lerp(bobAmount, targetBob, 8f * dt);
        if (moving) bobPhase += dt * (p.Sprinting ? 12f : 8f);
        float bobY = Mathf.Abs(Mathf.Sin(bobPhase)) * bobAmount;   // bounces twice per stride
        float bobX = Mathf.Cos(bobPhase) * bobAmount * 0.5f;

        // Landing dip
        if (p.Grounded && !wasGrounded && lastVelY < -4f) landDip = Mathf.Min(0.25f, -lastVelY * 0.02f);
        wasGrounded = p.Grounded;
        lastVelY = p.VerticalSpeed;
        landDip = Mathf.Lerp(landDip, 0f, 6f * dt);

        // Sprint field of view
        float targetFov = baseFov + (moving && p.Sprinting ? 8f : 0f);
        fovNow = Mathf.Lerp(fovNow, targetFov, 6f * dt);
        cam.fieldOfView = fovNow;

        // Shake and kick
        trauma = Mathf.Max(0f, trauma - dt * 1.6f);
        kick = Mathf.Lerp(kick, 0f, 10f * dt);
        float shake = trauma * trauma;
        float sx = (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 2f * shake * 4f;
        float sy = (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * 2f * shake * 4f;
        float sz = (Mathf.PerlinNoise(Time.time * 18f, 9f) - 0.5f) * 2f * shake * 2f;

        transform.localPosition = baseLocal + new Vector3(bobX, bobY - landDip, 0f);
        // Pitch comes from PlayerController (positive is down). Kick subtracts so the view rises.
        transform.localRotation = Quaternion.Euler(p.Pitch - kick + sx, sy, sz);
    }
}
