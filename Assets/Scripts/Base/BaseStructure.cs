using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anything the player has built on the roof: planters, snares, spike traps, barricades.
/// Its Health component is its durability (the bar above it); when that reaches 0 it breaks.
/// defense counts toward the base's defense while the structure stands, scaled by its condition.
/// Every structure is saved with the run and rebuilt on load.
/// </summary>
public class BaseStructure : MonoBehaviour
{
    public static readonly List<BaseStructure> All = new List<BaseStructure>();

    public string id = "trap";
    public float defense;
    public Health hp;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    public float Condition => hp != null && hp.max > 0f ? Mathf.Clamp01(hp.current / hp.max) : 1f;

    /// <summary>Total defense of everything standing, scaled by each structure's condition.</summary>
    public static float TotalDefense()
    {
        float total = 0f;
        foreach (var s in All) if (s != null) total += s.defense * s.Condition;
        return total;
    }

    public void Damage(float amount, string source = "raid")
    {
        if (hp != null) hp.TakeDamage(amount, source);
    }

    /// <summary>The nearest standing structure to a point, or null.</summary>
    public static BaseStructure Nearest(Vector3 from, float maxDist, string onlyId = null)
    {
        BaseStructure best = null;
        float bestD = maxDist;
        foreach (var s in All)
        {
            if (s == null || (s.hp != null && s.hp.Dead)) continue;
            if (onlyId != null && s.id != onlyId) continue;
            float d = Vector3.Distance(from, s.transform.position);
            if (d < bestD) { best = s; bestD = d; }
        }
        return best;
    }
}
