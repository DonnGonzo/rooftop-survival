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
    public string dropItem2 = "";
    public int dropCount2;
    /// <summary>Optional name drawn above the bar (used for the Drifters).</summary>
    public System.Func<string> label;

    [Tooltip("Show the bar when the player is within this many metres, even if nothing has hit it.")]
    public float barRange = 22f;
    [Tooltip("Seconds the bar stays up after a hit.")]
    public float barAfterHit = 5f;
    [Tooltip("Traps: on reaching 0 the object is removed instead of dropping loot and falling over.")]
    public bool breakOnDeath;
    [Tooltip("Shown when a structure breaks.")]
    public string breakMessage = "";
    [Tooltip("Structures: hide the bar until it has taken damage.")]
    public bool barOnlyWhenHurt;

    /// <summary>Raised when this dies or breaks.</summary>
    public event System.Action OnDeath;
    /// <summary>Raised on every hit, with what caused it.</summary>
    public event System.Action<string> OnHurt;

    public float current;
    public bool Dead => current <= 0f;

    float lastHitTime = -999f;

    void Awake() { current = max; }

    public void TakeDamage(float amount, string source)
    {
        if (Dead) return;
        current -= amount;
        lastHitTime = Time.time;
        OnHurt?.Invoke(source);
        if (Dead) Die();
    }

    void OnGUI()
    {
        if (Dead) return;
        var cam = Camera.main;
        if (cam == null || GameState.MenuOpen || !BarVisible()) return;

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
        if (label != null)
        {
            if (tag == null) { tag = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold }; tag.normal.textColor = Color.white; }
            GUI.Label(new Rect(x - 120f, y - 20f, 240f, 18f), label(), tag);
        }
    }

    static GUIStyle tag;

    bool BarVisible()
    {
        if (Time.time - lastHitTime < barAfterHit) return true;
        if (barOnlyWhenHurt && current >= max) return false;
        var p = PlayerController.Instance;
        return p != null && Vector3.Distance(p.transform.position, transform.position) < barRange;
    }

    void Die()
    {
        OnDeath?.Invoke();
        if (breakOnDeath)
        {
            if (!string.IsNullOrEmpty(breakMessage)) Hud.Toast(breakMessage);
            Destroy(gameObject);
            return;
        }
        if (dropCount > 0 && !string.IsNullOrEmpty(dropItem))
            Pickup.Create("loot-sack", dropItem, dropCount, null, transform.position + Vector3.up * 0.05f, 0.5f);
        if (dropCount2 > 0 && !string.IsNullOrEmpty(dropItem2))
            Pickup.Create("loot-sack", dropItem2, dropCount2, null, transform.position + new Vector3(0.6f, 0.05f, 0.3f), 0.45f);
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
