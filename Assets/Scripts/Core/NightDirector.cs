using System;
using UnityEngine;

public struct NightReport
{
    public int night;
    public bool attacked;
    public float threat;
    public float defense;
    public bool overrun;
    public bool runLost;
}

/// <summary>
/// Resolves each night. Nights 1-10 are always safe. From night 11 on, an attack may happen;
/// if your defense is below the threat you are overrun and may lose the whole run.
/// Phase 1 resolves this as a number check - later replace with real spawned beasts.
/// </summary>
public class NightDirector : MonoBehaviour
{
    public static event Action<NightReport> OnNightResolved;
    public static event Action OnCaughtOutside;

    [Header("Attack rules")]
    public int firstAttackNight = 11;
    public float baseAttackChance = 0.35f;
    public float maxAttackChance = 0.85f;
    [Tooltip("Nights after the first attack night over which chance ramps to max.")]
    public int rampNights = 30;

    [Header("Threat")]
    public float baseThreat = 20f;
    public float threatPerNight = 6f;
    [Range(0f, 0.5f)] public float threatVariance = 0.25f;

    [Header("Damage to defenses")]
    public float damagePerThreat = 0.1f;

    void OnEnable() { GameClock.OnNightFall += HandleNight; }
    void OnDisable() { GameClock.OnNightFall -= HandleNight; }

    void HandleNight(int night)
    {
        if (!RoofZone.PlayerOnRoof)
        {
            Debug.Log("Caught outside at nightfall!");
            OnCaughtOutside?.Invoke();   // hook your penalty here (drop loot, injury, etc.)
        }

        var report = new NightReport { night = night, defense = BaseStructure.TotalDefense() };

        if (night < firstAttackNight)
        {
            OnNightResolved?.Invoke(report);
            return;
        }

        float ramp = Mathf.InverseLerp(firstAttackNight, firstAttackNight + rampNights, night);
        float chance = Mathf.Lerp(baseAttackChance, maxAttackChance, ramp);
        if (UnityEngine.Random.value > chance)
        {
            OnNightResolved?.Invoke(report);
            return;
        }

        report.attacked = true;
        float variance = 1f + UnityEngine.Random.Range(-threatVariance, threatVariance);
        report.threat = (baseThreat + (night - firstAttackNight) * threatPerNight) * variance;

        if (report.defense >= report.threat)
        {
            BaseStructure.DamageAll(report.threat * damagePerThreat);
        }
        else
        {
            report.overrun = true;
            BaseStructure.DamageAll(report.threat * damagePerThreat * 2f);

            // The bigger the shortfall, the likelier you lose everything.
            float loseChance = 1f - report.defense / report.threat;
            if (UnityEngine.Random.value < loseChance) report.runLost = true;
        }

        Debug.Log($"Night {night}: threat {report.threat:F0} vs defense {report.defense:F0} " +
                  $"overrun={report.overrun} lost={report.runLost}");
        OnNightResolved?.Invoke(report);

        if (report.runLost) RunManager.Instance.LoseRun(night - 1);
    }
}
