using System.Collections;
using UnityEngine;

/// <summary>Hit points for anything that can be hurt (wildlife). Drops loot on death and falls over.</summary>
public class Health : MonoBehaviour
{
    public float max = 35f;
    public string dropItem = "food";
    public int dropCount = 2;

    public float current;
    public bool Dead => current <= 0f;

    void Awake() { current = max; }

    public void TakeDamage(float amount, string source)
    {
        if (Dead) return;
        current -= amount;
        if (Dead) Die();
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
