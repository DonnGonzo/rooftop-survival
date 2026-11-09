using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A placed wildlife trap with durability. Its health bar is the trap's durability: every catch
/// wears it down, and at 0 the trap breaks and is removed. Anything wild (not the player, not another
/// trap) that comes within the radius takes hit damage.
/// </summary>
public class Trap : MonoBehaviour
{
    float radius, hitDamage, wear, nextCheck;
    Health durability;

    /// <summary>Gives a placed object trap behaviour and a durability health bar.</summary>
    public static Trap Attach(GameObject go, float radius, float hitDamage, float wearPerCatch, float durability)
    {
        var t = go.AddComponent<Trap>();
        t.radius = radius;
        t.hitDamage = hitDamage;
        t.wear = wearPerCatch;

        var h = go.AddComponent<Health>();
        h.max = durability;
        h.current = durability;
        h.dropCount = 0;
        h.dropItem = null;
        h.breakOnDeath = true;
        t.durability = h;
        return t;
    }

    void Update()
    {
        if (GameState.Frozen || durability == null || durability.Dead || Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;

        var hit = new HashSet<Health>();
        foreach (var col in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var h = col.GetComponentInParent<Health>();
            if (h == null || h == durability || h.Dead || hit.Contains(h)) continue;
            if (h.GetComponent<Trap>() != null || h.GetComponent<PlayerController>() != null) continue;

            hit.Add(h);
            h.TakeDamage(hitDamage, "trap");
            durability.TakeDamage(wear, "wear");
            if (durability.Dead) return;   // the trap broke; Health removes it
            Hud.Toast("Trap caught something (" + Mathf.CeilToInt(durability.current) + " durability left).");
        }
    }
}
