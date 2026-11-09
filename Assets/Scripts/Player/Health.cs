using System.Collections;
using UnityEngine;

/// <summary>
/// Hit points for anything that can be hurt (wildlife and monsters). Drops loot on death and falls over.
/// Draws a health bar above the creature while it is injured recently or the player is close.
/// </summary>
public class Health : MonoBehaviour
{
    public float max = 35f;
    public string dropItem = "food";
    public int dropCount = 2;

    [Tooltip("Show the bar when the player is within this many metres, even if nothing has hit it.")]
    public float barRange = 22f;
    [Tooltip("Seconds the bar stays up after a hit.")]
    public float barAfterHit = 5f;

    public float current;
    public bool Dead => current <= 0f;

    float lastHitTime = -999f;

    void Awake() { current = max; }

    public void TakeDamage(float amount, string source)
    {
        if (Dead) return;
        current -= amount;
        lastHitTime = Time.time;
        if (Dead) Die();
    }

    void OnGUI()
    {
        if (Dead) return;
        var cam = Camera.main;
        if (cam == null || !BarVisible()) return;

        // Top of the creature's renderers, so the bar sits above it whatever the model height.
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        Vector3 s = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y + 0.3f, b.center.z));
        if (s.z <= 0f) return;   // behind the camera

        float x = s.x, y = Screen.height - s.y;
        const float w = 70f, h = 6f;
        float frac = Mathf.Clamp01(current / max);
        var tex = Texture2D.whiteTexture;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x - w / 2f - 1f, y - 1f, w + 2f, h + 2f), tex);
        GUI.color = Color.Lerp(new Color(0.85f, 0.15f, 0.12f), new Color(0.35f, 0.85f, 0.3f), frac);
        GUI.DrawTexture(new Rect(x - w / 2f, y, w * frac, h), tex);
        GUI.color = Color.white;
    }

    bool BarVisible()
    {
        if (Time.time - lastHitTime < barAfterHit) return true;
        var p = PlayerController.Instance;
        return p != null && Vector3.Distance(p.transform.position, transform.position) < barRange;
    }

    void Die()
    {
        if (dropCount > 0 && !string.IsNullOrEmpty(dropItem))
            Pickup.Create("loot-sack", dropItem, dropCount, null, transform.position + Vector3.up * 0.2f);
        StartCoroutine(FallOver());
    }

    IEnumerator FallOver()
    {
        Quaternion start = transform.rotation;
        Quaternion end = start * Quaternion.Euler(80f, 0f, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 2.5f;
            transform.rotation = Quaternion.Slerp(start, end, Mathf.Min(t, 1f));
            yield return null;
        }
        yield return new WaitForSeconds(6f);
        Destroy(gameObject);
    }
}
