using UnityEngine;

/// <summary>
/// Simple street wildlife. Wanders; chases and bites the player when they are at street level.
/// Aggro range grows at night. "Lurkers" stay near their home spot (the shade at building bases).
/// If the model faces the wrong way when walking, change yawOffset (try 180).
/// </summary>
public class AnimalAI : MonoBehaviour
{
    public float walkSpeed = 1.6f, runSpeed = 5f;
    public float aggroRadius = 14f, biteRange = 1.8f, biteDamage = 12f, biteCooldown = 1.2f;
    public float roamRadius = 50f;
    public bool lurker;
    public float leash = 12f;
    public float yawOffset = 0f;
    public float streetY = -90f;

    const float KeepOutRadius = 18f;   // don't wander into the player's own building

    Vector3 home, target;
    float nextPick, nextBite, idleUntil;
    string anim = "";

    void Start()
    {
        home = transform.position;
        Pick();
        SetAnim("idle");
    }

    void Update()
    {
        var p = PlayerController.Instance;
        bool chasing = false;

        if (p != null && p.OnStreet)
        {
            float night = (GameClock.Instance != null && GameClock.Instance.IsNight) ? 1.5f : 1f;
            Vector3 to = p.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            bool inLeash = !lurker || Vector3.Distance(p.transform.position, home) < leash + aggroRadius;

            if (dist < aggroRadius * night && inLeash)
            {
                chasing = true;
                if (dist > biteRange * 0.8f) Move(to.normalized, runSpeed, "run");
                else SetAnim("idle");

                if (dist < biteRange && Time.time >= nextBite)
                {
                    nextBite = Time.time + biteCooldown;
                    p.TakeDamage(biteDamage, name);
                }
            }
        }

        if (!chasing) Wander();
    }

    void Wander()
    {
        if (Time.time < idleUntil) { SetAnim("idle"); return; }

        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.magnitude < 1f || Time.time > nextPick)
        {
            idleUntil = Time.time + Random.Range(1f, 4f);
            Pick();
            return;
        }
        Move(to.normalized, walkSpeed, "walk");
    }

    void Pick()
    {
        float range = lurker ? leash : roamRadius;
        Vector2 c = Random.insideUnitCircle * range;
        target = new Vector3(home.x + c.x, streetY, home.z + c.y);
        nextPick = Time.time + 12f;
    }

    void Move(Vector3 dir, float speed, string clip)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        Vector3 pos = transform.position + dir * speed * Time.deltaTime;
        pos.y = streetY;

        // keep out of the player's building footprint
        Vector2 flat = new Vector2(pos.x, pos.z);
        if (flat.magnitude < KeepOutRadius)
        {
            flat = flat.normalized * KeepOutRadius;
            pos.x = flat.x; pos.z = flat.y;
        }
        transform.position = pos;

        var look = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, yawOffset, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 8f * Time.deltaTime);
        SetAnim(clip);
    }

    void SetAnim(string clip)
    {
        if (anim == clip) return;
        anim = clip;
        // fall back to "walk" if a model has no "run"
        if (!ModelLibrary.PlayClip(gameObject, clip) && clip == "run") ModelLibrary.PlayClip(gameObject, "walk");
    }
}
