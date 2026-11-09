using System;
using UnityEngine;

/// <summary>
/// Day/night cycle. Day 1 starts at startHour. A new day begins at dawn.
/// "Night N" is the night that follows Day N (so Night 11 follows Day 11).
/// </summary>
public class GameClock : MonoBehaviour
{
    public static GameClock Instance { get; private set; }

    public static event Action<int> OnDawn;         // new day number
    public static event Action<int> OnDuskWarning;  // "get back to the roof!"
    public static event Action<int> OnNightFall;    // night number (== day number it follows)

    [Header("Timing")]
    [Tooltip("Real seconds for one full 24h cycle.")]
    public float dayLengthSeconds = 600f;
    public float timeScale = 1f;
    public float dawnHour = 6f;
    public float warningHour = 17f;
    public float nightHour = 19f;

    [Header("Lighting")]
    public Light sun;
    public Gradient sunColor;
    public float maxSunIntensity = 1.2f;

    /// <summary>True while the player sleeps on the cot: time runs fast until dawn.</summary>
    public static bool Sleeping { get; private set; }

    public static void Sleep() { Sleeping = true; }
    public static void WakeUp() { Sleeping = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Sleeping = false; OnDawn = null; OnDuskWarning = null; OnNightFall = null; }

    /// <summary>0 at night, 1 in full daylight. Used by the sky and the lighting.</summary>
    public float Daylight => Mathf.Clamp01(Mathf.Sin((Hour - 6f) / 24f * Mathf.PI * 2f) * 3f + 0.15f);

    public int Day { get; private set; } = 1;
    public float Hour { get; private set; } = 7f;
    public bool IsNight => Hour >= nightHour || Hour < dawnHour;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run != null) { Day = run.day; Hour = run.hour; }
        ApplyLighting();
    }

    void Update()
    {
        float prev = Hour;
        Hour += 24f * (Time.deltaTime * timeScale) / dayLengthSeconds;
        if (Hour >= 24f) Hour -= 24f;

        if (Crossed(prev, Hour, dawnHour))
        {
            Day++;
            Sleeping = false;
            OnDawn?.Invoke(Day);
            RunManager.Instance?.Save();
        }
        if (Crossed(prev, Hour, warningHour)) OnDuskWarning?.Invoke(Day);
        if (Crossed(prev, Hour, nightHour)) OnNightFall?.Invoke(Day);

        ApplyLighting();
    }

    static bool Crossed(float a, float b, float t)
    {
        return a <= b ? (a < t && b >= t) : (a < t || b >= t);
    }

    void ApplyLighting()
    {
        if (sun == null) return;
        // 6h = sunrise, 12h = overhead, 18h = sunset. The sun also swings a little south so shadows have length at noon.
        float angle = Hour / 24f * 360f - 90f;
        sun.transform.rotation = Quaternion.Euler(angle, 160f, 0f) * Quaternion.Euler(0f, 0f, 22f);
        float s = Mathf.Sin((Hour - 6f) / 24f * Mathf.PI * 2f);
        sun.intensity = Mathf.Clamp01(s * 3f) * maxSunIntensity;
        if (sunColor != null) sun.color = sunColor.Evaluate(Mathf.Clamp01(s));
        sun.shadows = s > 0.02f ? LightShadows.Soft : LightShadows.None;
    }
}
