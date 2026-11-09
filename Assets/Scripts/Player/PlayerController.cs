using System.Collections;
using UnityEngine;

/// <summary>
/// First-person controller and roof actions.
/// WASD move, mouse look, Shift sprint, Space jump, E = pick up / plant / water / harvest,
/// T = place a spike trap (needs a trap kit), G = rappel down or climb up, Q = use a bandage or food.
/// Melee is in MeleeCombat (left click, right click).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public Transform cam;
    public float walkSpeed = 4.5f, sprintSpeed = 7f, jumpSpeed = 5f, gravity = -18f;
    public float reach = 3.5f;
    public float maxHealth = 100f;
    public float health = 100f;
    public float roofRegenPerSecond = 3f;
    [Range(0f, 1f)] public float deathSupplyLoss = 0.25f;

    public bool OnStreet => transform.position.y < StreetBand;

    // Read by CameraFeel for head bob, landing and sprint effects.
    public float Pitch => pitch;
    public float VerticalSpeed => velY;
    public bool Grounded => cc != null && cc.isGrounded;
    public float HorizontalSpeed { get; private set; }
    public bool Sprinting { get; private set; }
    static float StreetBand => StreetBuilder.StreetY + 8f;

    CharacterController cc;
    float pitch, velY;
    bool busy;   // rappelling

    void Awake()
    {
        Instance = this;
        cc = GetComponent<CharacterController>();
        var c = Characters.Current;
        maxHealth = c.maxHealth;
        health = maxHealth;
        walkSpeed *= c.moveMult;
        sprintSpeed *= c.moveMult;
    }

    void Update()
    {
        if (GameState.Frozen || busy) return;

        var look = Inp.Look() * GameSettings.Sensitivity;
        transform.Rotate(0f, look.x, 0f);
        float dy = GameSettings.InvertY ? -look.y : look.y;
        pitch = Mathf.Clamp(pitch - dy, -85f, 85f);
        if (cam != null) cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        var m = Inp.Move();
        if (m.magnitude > 0.5f) Tutorial.Notify("move");
        Vector3 dir = transform.right * m.x + transform.forward * m.y;
        float speed = Inp.Sprint() ? sprintSpeed : walkSpeed;

        if (cc.isGrounded && velY < 0f) velY = -2f;
        if (cc.isGrounded && Inp.Jump()) velY = jumpSpeed;
        velY += gravity * Time.deltaTime;
        cc.Move((dir * speed + Vector3.up * velY) * Time.deltaTime);
        HorizontalSpeed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        Sprinting = m.magnitude > 0.5f && Inp.Sprint();

        float y = transform.position.y;
        if (y < -12f && y > StreetBand) Die("You fell off the roof!");
        else if (y < StreetBuilder.StreetY - 10f) Die("You fell out of the world.");

        if (RoofZone.PlayerOnRoof && health < maxHealth)
            health = Mathf.Min(maxHealth, health + roofRegenPerSecond * Time.deltaTime);

        if (Inp.Interact()) Interact();
        if (Inp.Build()) PlaceBuild();
        if (Inp.CycleBuild()) Items.CycleBuild();
        if (Inp.Grapple()) Grapple();
        if (Inp.Use()) Hud.Toast(Items.UseBest());
    }

    // ---------------------------------------------------------------- health

    public void Heal(float amount) { health = Mathf.Min(maxHealth, health + amount); }

    public void TakeDamage(float amount, string source)
    {
        if (GameState.Frozen || health <= 0f) return;
        health -= amount;
        CameraFeel.Shake(Mathf.Clamp01(amount / 25f));
        Hud.Toast("Hit by " + source.Replace("(Clone)", "") + " (-" + amount.ToString("F0") + ")");
        if (health <= 0f) Die("You were mauled on the street.");
    }

    void Die(string why)
    {
        StopAllCoroutines();
        busy = false;
        health = maxHealth;

        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run != null && deathSupplyLoss > 0f)
        {
            run.food = Mathf.FloorToInt(run.food * (1f - deathSupplyLoss));
            run.water = Mathf.FloorToInt(run.water * (1f - deathSupplyLoss));
            run.wood = Mathf.FloorToInt(run.wood * (1f - deathSupplyLoss));
            run.scrap = Mathf.FloorToInt(run.scrap * (1f - deathSupplyLoss));
        }

        cc.enabled = false;
        transform.position = new Vector3(0f, 0.3f, 0f);
        cc.enabled = true;
        velY = 0f;
        Hud.Toast(why + " You wake on the roof and lost some supplies.");
    }

    // ---------------------------------------------------------------- rappel

    void Grapple()
    {
        Vector2 size = RoofGenerator.Size;
        if (size == Vector2.zero) return;
        Vector3 p = transform.position;
        float hx = size.x / 2f, hz = size.y / 2f;

        if (!OnStreet)
        {
            if (!RoofZone.PlayerOnRoof) return;
            float dx = hx - Mathf.Abs(p.x), dz = hz - Mathf.Abs(p.z);
            if (Mathf.Min(dx, dz) > 3.5f) { Hud.Toast("Walk to the roof edge to rappel down."); return; }

            bool sideX = dx < dz;
            Vector3 outward = sideX ? new Vector3(Mathf.Sign(p.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(p.z));
            Vector3 hook = sideX
                ? new Vector3(Mathf.Sign(p.x) * (hx + 1.2f), 0.3f, p.z)
                : new Vector3(p.x, 0.3f, Mathf.Sign(p.z) * (hz + 1.2f));
            Vector3 ground = new Vector3(hook.x, StreetBuilder.StreetY + 0.3f, hook.z) + outward * 3f;

            Tutorial.Notify("rappel");
            StartCoroutine(Travel(
                new[] { hook, new Vector3(hook.x, StreetBuilder.StreetY + 0.3f, hook.z), ground },
                new[] { 0.6f, 4f, 0.6f }, "Rappelling down. Be back before dark!"));
        }
        else
        {
            Vector2 flat = new Vector2(p.x, p.z);
            Vector2 wall = new Vector2(Mathf.Clamp(flat.x, -hx, hx), Mathf.Clamp(flat.y, -hz, hz));
            if ((flat - wall).magnitude > 8f) { Hud.Toast("Get to the base of your building to climb up."); return; }

            Vector2 n = flat - wall;
            n = n.sqrMagnitude < 0.0001f ? Vector2.up : n.normalized;
            Vector2 baseP = wall + n * 1.2f, inward = wall - n * 2.5f;
            StartCoroutine(Travel(new[]
            {
                new Vector3(baseP.x, StreetBuilder.StreetY + 0.3f, baseP.y),
                new Vector3(baseP.x, 0.3f, baseP.y),
                new Vector3(inward.x, 0.3f, inward.y)
            }, new[] { 0.6f, 5f, 0.6f }, "Climbing back up..."));
        }
    }

    IEnumerator Travel(Vector3[] points, float[] durations, string message)
    {
        busy = true;
        cc.enabled = false;
        Hud.Toast(message);
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 a = transform.position, b = points[i];
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / durations[i];
                transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f)));
                yield return null;
            }
        }
        cc.enabled = true;
        velY = 0f;
        busy = false;
    }

    // ---------------------------------------------------------------- roof and street actions

    void Interact()
    {
        var pk = Pickup.Nearest(transform.position, 2.6f);
        if (pk != null)
        {
            pk.Take();
            return;
        }

        FarmPlot best = null;
        float bestDist = reach + 1.5f;   // planters are long, so measure from their middle
        foreach (var p in FindObjectsByType<FarmPlot>())
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < bestDist) { best = p; bestDist = d; }
        }
        if (best == null) { Hud.Toast("Nothing to interact with."); return; }

        if (best.Stage == -1)
        {
            best.Plant();
            Hud.Toast("Planted.");
            Tutorial.Notify("plant");
        }
        else if (best.Ready)
        {
            int bonus = Characters.Current.planterBonus;
            int got = best.Harvest() + bonus;   // Harvest() already adds the crop yield
            if (bonus > 0) Items.Add("food", bonus);
            Hud.Toast("Harvested +" + got + " food.");
        }
        else if (best.Watered) Hud.Toast("Already watered today.");
        else if (best.Water())
        {
            Hud.Toast("Watered (-1 water).");
            Tutorial.Notify("water");
        }
        else Hud.Toast("No water left.");
    }

    /// <summary>T: place the selected crafted kit (trap, snare, barricade or planter) in front of the player.</summary>
    void PlaceBuild()
    {
        if (!RoofZone.PlayerOnRoof) { Hud.Toast("Place buildings on the roof."); return; }
        string kit = Items.SelectedKit;
        if (Items.Count(kit) <= 0)
        {
            Hud.Toast(Items.Def(kit).name + " needed. Craft one with Tab, or press R to pick another kit.");
            return;
        }

        Vector3 pos = transform.position + transform.forward * 2.2f;
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 6f)) pos = hit.point;

        if (Buildables.Place(kit, pos, transform.eulerAngles.y)) Items.Remove(kit, 1);
    }
}
