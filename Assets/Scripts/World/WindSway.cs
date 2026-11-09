using UnityEngine;

/// <summary>Shared wind. A slowly changing direction and strength that anything that sways reads from.</summary>
public static class WindField
{
    public static float Strength => 0.4f + 0.6f * Mathf.PerlinNoise(Time.time * 0.06f, 3.1f);

    public static Vector2 Direction
    {
        get
        {
            float a = Mathf.PerlinNoise(Time.time * 0.02f, 7.7f) * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }
    }
}

/// <summary>
/// Gentle whole-object sway for plants and trees that have no sway clip of their own.
/// Models that do have a 'tick' clip (grass, tree leaves) are animated by PlayClip; this adds the trunk motion.
/// </summary>
public class WindSway : MonoBehaviour
{
    public float amplitude = 2f;    // degrees at full strength wind
    public float speed = 1.2f;

    Quaternion baseLocal;
    float phase;

    void Start()
    {
        baseLocal = transform.localRotation;
        phase = Random.Range(0f, 100f);
    }

    void LateUpdate()
    {
        float strength = WindField.Strength;
        Vector2 dir = WindField.Direction;
        float t = Time.time * speed + phase;
        float flutter = Mathf.Sin(t) * 0.6f + Mathf.Sin(t * 2.3f) * 0.4f;   // irregular, not a clean wave

        float pitch = dir.y * amplitude * strength + flutter * amplitude * 0.5f;
        float roll = -dir.x * amplitude * strength;
        transform.localRotation = baseLocal * Quaternion.Euler(pitch, 0f, roll);
    }
}
