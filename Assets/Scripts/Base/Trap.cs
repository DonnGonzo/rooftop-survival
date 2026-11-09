using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A placed trap. Any creature (wildlife or raider) that comes within its radius is hurt, and each catch
/// wears the trap down. The wear comes off the structure's Health, which is the bar shown above it;
/// at 0 the trap breaks. A snare also holds its catch still for a few seconds.
/// </summary>
public class Trap : MonoBehaviour
{
    float radius, hitDamage, wear, slow, nextCheck;
    readonly Dictionary<Health, float> lastCaught = new Dictionary<Health, float>();

    public static Trap Attach(GameObject go, float radius, float hitDamage, float wearPerCatch, float slow)
    {
        var t = go.AddComponent<Trap>();
        t.radius = radius;
        t.hitDamage = hitDamage;
        t.wear = wearPerCatch;
        t.slow = slow;
        return t;
    }

    void Update()
    {
        if (GameState.Frozen || Time.time < nextCheck) return;
        nextCheck = Time.time + 0.2f;

        var own = GetComponent<Health>();
        if (own == null || own.Dead) return;

        foreach (var ai in AnimalAI.All)
        {
            if (ai == null) continue;
            var h = ai.Hp;
            if (h == null || h.Dead) continue;
            Vector3 d = ai.transform.position - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > radius * radius) continue;
            if (Mathf.Abs(ai.transform.position.y - transform.position.y) > 2f) continue;

            // The same creature is caught again only after it has had time to pull free.
            if (lastCaught.TryGetValue(h, out float when) && Time.time - when < 1.5f) continue;
            lastCaught[h] = Time.time;

            h.TakeDamage(hitDamage, "trap");
            if (slow > 0f) ai.HoldFor(slow);
            own.TakeDamage(wear, "wear");
            if (own.Dead) return;
        }
    }
}
