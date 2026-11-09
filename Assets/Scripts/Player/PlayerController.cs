using System.Collections;
using UnityEngine;

/// <summary>
/// The player: movement, looking, health, and the roof actions (interact, place a kit, rope up and down).
/// Melee is in MeleeCombat and the visible body in PlayerBody. Every key comes from Keybinds.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public Transform cam;
    public float walkSpeed = 4.5f, sprintSpeed = 7f, jumpSpeed = 5f, gravity = -18f;
    public float maxHealth = 100f;
    public float health = 100f;
    public float roofRegenPerSecond = 2f;
    [Range(0f, 1f)] public float deathSupplyLoss = 0.25f;

    public bool OnStreet => transform.position.y < StreetBand;

    // Read by CameraFeel, PlayerBody and the HUD.
    public float Pitch => pitch;
    public float VerticalSpeed => velY;
    public bool Grounded => cc != null && cc.isGrounded;
    public float HorizontalSpeed { get; private set; }
    public bool Sprinting { get; private set; }
    public bool Climbing => busy;
    /// <summary>What the Interact key would do right now ("" if nothing).</summary>
    public string Prompt { get; private set; } = "";
    /// <summary>Hint for the rope key, shown when the player is at either end of the rope.</summary>
    public string RopePrompt { get; private set; } = "";

    static float StreetBand => StreetBuilder.StreetY + 8f;
    const float RopeRange = 4.5f;

    CharacterController cc;
    float pitch, velY;
    bool busy;   // on the rope
    Interactable focus;

    /// <summary>Test hook: point the view up or down.</summary>
    public void SetPitch(float p) { pitch = Mathf.Clamp(p, -85f, 85f); }

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
        if (GameState.Frozen || busy) { Prompt = ""; RopePrompt = ""; return; }

        var look = Inp.Look() * GameSettings.Sensitivity;
        transform.Rotate(0f, look.x, 0f);
        float dy = GameSettings.InvertY ? -look.y : look.y;
        pitch = Mathf.Clamp(pitch - dy, -85f, 85f);
        if (cam != null) cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        var m = Inp.Move();
        if (m.magnitude > 0.5f) Tutorial.Notify("move");
        Vector3 dir = transform.right * m.x + transform.forward * m.y;
        bool sprint = Inp.Sprint() && m.y > 0.1f;
        float speed = sprint ? sprintSpeed : walkSpeed;
        if (m.y < -0.1f) speed *= 0.7f;   // backing up is slower

        if (cc.isGrounded && velY < 0f) velY = -2f;
        if (cc.isGrounded && Inp.Jump()) velY = jumpSpeed;
        velY += gravity * Time.deltaTime;
        cc.Move((dir * speed + Vector3.up * velY) * Time.deltaTime);
        HorizontalSpeed = new Vector2(cc.velocity.x, cc.velocity.z).magnitude;
        Sprinting = sprint && m.magnitude > 0.5f;

        float y = transform.position.y;
        if (y < -12f && y > StreetBand) Die("You fell from the roof.");
        else if (y < StreetBuilder.StreetY - 10f) Die("You fell out of the world.");

        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        bool fed = run == null || !run.hungry;
        if (RoofZone.PlayerOnRoof && fed && health < maxHealth && !NightDirector.RaidActive)
            health = Mathf.Min(maxHealth, health + roofRegenPerSecond * Time.deltaTime);

        // What is in reach?
        focus = Interactable.Best(transform.position + Vector3.up * 0.9f, transform.forward);
        Prompt = focus != null ? focus.Prompt : "";
        RopePrompt = AtRopeTop() ? "Rappel down to the street" : AtRopeFoot() ? "Climb the rope to your roof" : "";

        if (Inp.Interact())
        {
            if (focus != null) focus.Use();
            else Hud.Toast("Nothing to use here.");
        }
        if (Inp.Build()) PlaceBuild();
        if (Inp.CycleBuild()) Items.CycleBuild();
        if (Inp.SwapWeapon()) Items.CycleWeapon();
        if (Inp.Grapple()) UseRope();
        if (Inp.Use()) Hud.Toast(Items.UseBest());
    }

    // ---------------------------------------------------------------- health

    public void Heal(float amount) { health = Mathf.Min(maxHealth, health + amount); }

    public void TakeDamage(float amount, string source)
    {
        if (GameState.Frozen || health <= 0f || busy) return;
        health -= amount;
        CameraFeel.Shake(Mathf.Clamp01(amount / 25f));
        Hud.Hurt();
        if (GameClock.Sleeping) GameClock.WakeUp();
        if (health <= 0f) Die("You were brought down by " + source + ".");
    }

    void Die(string why)
    {
        StopAllCoroutines();
        busy = false;
        health = maxHealth;

        // Going down while raiders hold the roof loses the whole run.
        if (NightDirector.PlayerFell()) return;

        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run != null && deathSupplyLoss > 0f)
        {
            run.food = Mathf.FloorToInt(run.food * (1f - deathSupplyLoss));
            run.water = Mathf.FloorToInt(run.water * (1f - deathSupplyLoss));
            run.wood = Mathf.FloorToInt(run.wood * (1f - deathSupplyLoss));
            run.scrap = Mathf.FloorToInt(run.scrap * (1f - deathSupplyLoss));
        }

        Teleport(new Vector3(0f, 0.3f, 0f));
        health = maxHealth * 0.5f;
        Hud.Banner(why + " You come to on your roof, a quarter of your supplies gone.", 6f);
    }

    public void Teleport(Vector3 pos)
    {
        cc.enabled = false;
        transform.position = pos;
        cc.enabled = true;
        velY = 0f;
    }

    // ---------------------------------------------------------------- the rope

    bool AtRopeTop()
    {
        if (OnStreet || !RoofZone.PlayerOnRoof) return false;
        Vector3 a = RoofGenerator.AnchorPos;
        return Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(a.x, a.z)) < RopeRange;
    }

    bool AtRopeFoot()
    {
        if (!OnStreet) return false;
        Vector3 f = RoofGenerator.RopeFoot;
        return Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(f.x, f.z)) < RopeRange + 1.5f;
    }

    void UseRope()
    {
        if (RoofGenerator.Size == Vector2.zero) return;
        Vector3 foot = RoofGenerator.RopeFoot;
        Vector3 top = new Vector3(foot.x, 0.4f, foot.z);

        if (AtRopeTop())
        {
            Tutorial.Notify("rappel");
            Vector3 land = foot + new Vector3(0f, 0.3f, 2.5f);
            StartCoroutine(Travel(new[] { top + Vector3.up * 1.2f, top, foot + Vector3.up * 0.4f, land },
                                  new[] { 0.5f, 0.4f, 4.5f, 0.5f }, "Rappelling down. Be back before dark."));
        }
        else if (AtRopeFoot())
        {
            Vector3 inside = RoofGenerator.AnchorPos + new Vector3(0f, 0.3f, -2.2f);
            StartCoroutine(Travel(new[] { foot + Vector3.up * 0.4f, top, top + Vector3.up * 1.2f, inside },
                                  new[] { 0.5f, 6f, 0.4f, 0.5f }, "Climbing...", () => Tutorial.Notify("roof")));
        }
        else if (OnStreet) Hud.Toast("The rope hangs on the north face of your building.");
        else Hud.Toast("Go to the yellow rope anchor on the north wall.");
    }

    IEnumerator Travel(Vector3[] points, float[] durations, string message, System.Action done = null)
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
                if (!GameState.Frozen) t += Time.deltaTime / durations[i];
                transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, Mathf.Min(t, 1f)));
                yield return null;
            }
        }
        cc.enabled = true;
        velY = 0f;
        busy = false;
        done?.Invoke();
    }

    // ---------------------------------------------------------------- building

    /// <summary>Build key: place the selected crafted kit in front of the player, on the roof.</summary>
    void PlaceBuild()
    {
        if (!RoofZone.PlayerOnRoof || OnStreet) { Hud.Toast("Kits can only be placed on your roof."); return; }
        string kit = Items.SelectedKit;
        if (Items.Count(kit) <= 0)
        {
            // Nothing of the selected kind: fall through to any kit the player does carry.
            foreach (var k in Items.BuildKits)
                if (Items.Count(k) > 0) { kit = k; Items.SelectedIndex = System.Array.IndexOf(Items.BuildKits, k); break; }
            if (Items.Count(kit) <= 0)
            {
                Hud.Toast("No kits. Craft one with " + Keybinds.Describe(GameAction.Inventory) + ".");
                return;
            }
        }

        Vector3 pos = transform.position + transform.forward * 2.2f;
        Vector2 half = RoofGenerator.Size / 2f - Vector2.one * 1.2f;
        pos.x = Mathf.Clamp(pos.x, -half.x, half.x);
        pos.z = Mathf.Clamp(pos.z, -half.y, half.y);
        pos.y = 0f;

        if (Buildables.Place(kit, pos, transform.eulerAngles.y)) Items.Remove(kit, 1);
    }
}
