using UnityEngine;

/// <summary>
/// The body's needs. Hunger and thirst fall through the day (0..100) and are saved; stamina is spent by
/// sprinting, jumping and striking and comes back with a moment's rest. Run out of food or water and you
/// start losing health; get low on either and the roof stops healing you and stamina returns slowly.
/// </summary>
public class Needs : MonoBehaviour
{
    public static Needs Instance { get; private set; }

    [Tooltip("Points of hunger and thirst lost per game hour.")]
    public float hungerPerHour = 4.2f, thirstPerHour = 6f;
    public float staminaRegen = 20f;

    public float Stamina { get; private set; } = 100f;
    public float MaxStamina => 100f + Progress.Stat("endurance") * 15f;

    float lastSpend = -10f, starveTick;

    static RunData Run => RunManager.Instance != null ? RunManager.Instance.Current : null;

    public float Hunger => Run != null ? Run.hunger : 100f;
    public float Thirst => Run != null ? Run.thirst : 100f;
    /// <summary>Low on food or water: no healing, slow stamina.</summary>
    public bool Weak => Hunger < 20f || Thirst < 20f;

    void Awake() { Instance = this; }
    void OnEnable() { GameClock.OnDawn += Dawn; }
    void OnDisable() { GameClock.OnDawn -= Dawn; }
    void Start() { Stamina = MaxStamina; }

    public void Eat(float amount) { if (Run != null) Run.hunger = Mathf.Min(100f, Run.hunger + amount); }
    public void Drink(float amount) { if (Run != null) Run.thirst = Mathf.Min(100f, Run.thirst + amount); }

    /// <summary>Takes stamina if there is enough. Returns false (and takes what is left) if not.</summary>
    public bool Spend(float amount)
    {
        lastSpend = Time.time;
        if (Stamina >= amount) { Stamina -= amount; return true; }
        Stamina = 0f;
        return false;
    }

    void Update()
    {
        var run = Run;
        var clock = GameClock.Instance;
        var p = PlayerController.Instance;
        if (run == null || clock == null || p == null || GameState.Frozen) return;

        // Needs fall with game time, so sleeping through the night costs something, though less than being up.
        float hours = 24f * Time.deltaTime * clock.timeScale / clock.dayLengthSeconds;
        float ease = (1f - Progress.Stat("endurance") * 0.05f) * (GameClock.Sleeping ? 0.5f : 1f);
        run.hunger = Mathf.Max(0f, run.hunger - hungerPerHour * hours * ease);
        run.thirst = Mathf.Max(0f, run.thirst - thirstPerHour * hours * ease);

        if (run.hunger <= 0f || run.thirst <= 0f)
        {
            starveTick += Time.deltaTime;
            if (starveTick > 3f)
            {
                starveTick = 0f;
                p.health = Mathf.Max(1f, p.health - 2f);
                Hud.Hurt();
                if (GameClock.Sleeping) GameClock.WakeUp();
            }
        }

        if (Time.time - lastSpend > 0.8f)
            Stamina = Mathf.Min(MaxStamina, Stamina + staminaRegen * (Weak ? 0.4f : 1f) * Time.deltaTime);
    }

    void Dawn(int day)
    {
        Progress.Gain(15);
        Sfx.Play("dawn", 0.7f);
        Hud.Banner("Day " + day + (Weak ? ".  You are weak: eat and drink." : "."), 4f);
        if (RunManager.Instance != null) RunManager.Instance.Save();
    }
}
