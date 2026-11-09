using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anything the player builds that counts toward base defense: traps, walls, spikes, nets.
/// Add this to each buildable prefab and set its defense value.
/// </summary>
public class BaseStructure : MonoBehaviour
{
    public static readonly List<BaseStructure> All = new List<BaseStructure>();

    public string id = "trap";
    public float defense = 10f;
    public float maxHealth = 100f;
    public float health = 100f;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    /// <summary>Total defense, scaled by each structure's remaining health.</summary>
    public static float TotalDefense()
    {
        float total = 0f;
        foreach (var s in All) total += s.defense * Mathf.Clamp01(s.health / s.maxHealth);
        return total;
    }

    public static void DamageAll(float amount)
    {
        foreach (var s in new List<BaseStructure>(All)) s.Damage(amount);
    }

    public void Damage(float amount)
    {
        health -= amount;
        if (health <= 0f) Destroy(gameObject);
    }
}
