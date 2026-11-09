using System.Collections;
using UnityEngine;

/// <summary>
/// First-person controller + roof actions.
/// WASD move, mouse look, Shift sprint, Space jump,
/// E = plant / water / harvest nearest planter, T = place a trap (2 wood),
/// G = grapple down at the roof edge / climb back up at the base of your building.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    public Transform cam;
    public float walkSpeed = 4.5f, sprintSpeed = 7f, jumpSpeed = 5f, gravity = -18f;
    public float sensitivity = 1.2f, reach = 3.5f;
    public int trapWoodCost = 2;

    [Header("Health")]
    public float maxHealth = 100f;
    public float health = 100f;
    public float roofRegenPerSecond = 3f;
    [Range(0f, 1f)] public float deathSupplyLoss = 0.25f;

    public bool OnStreet { get { return transform.position.y < StreetBand; } }
    static float StreetBand { get { return StreetBuilder.StreetY + 8f; } }

    CharacterController cc;
    float pitch, velY;
    bool busy;   // rappelling

    void Awake()
    {
        Instance = this;
        cc = GetComponent<CharacterController>();
        health = maxHealth;
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        var look = Inp.Look() * sensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
        if (cam != null) cam.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (busy) return;

        var m = Inp.Move();
        Vector3 dir = transform.right * m.x + transform.forward * m.y;
        float speed = Inp.Sprint() ? sprintSpeed : walkSpeed;

        if (cc.isGrounded && velY < 0f) velY = -2f;
        if (cc.isGrounded && Inp.Jump()) velY = jumpSpeed;
        velY += gravity * Time.deltaTime;
        cc.Move((dir * speed + Vector3.up * velY) * Time.deltaTime);

        float y = transform.position.y;
        if (y < -12f && y > StreetBand) Die("You fell off the roof!");
        else if (y < StreetBuilder.StreetY - 10f) Die("You fell out of the world.");

        if (RoofZone.PlayerOnRoof && health < maxHealth)
            health = Mathf.Min(maxHealth, health + roofRegenPerSecond * Time.deltaTime);

        if (Inp.Interact()) Interact();
        if (Inp.Build()) PlaceTrap();
        if (Inp.Grapple()) Grapple();
    }

    // ---------------------------------------------------------------- health

    public void TakeDamage(float amount, string source)
    {
        if (health <= 0f) return;
        health -= amount;
        Hud.Toast("Hit by " + source.Replace("(Clone)", "") + "! (-" + amount.ToString("F0") + ")");
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

    // ---------------------------------------------------------------- grapple

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
            if (Mathf.Min(dx, dz) > 3.5f) { Hud.Toast("Walk to the roof edge to grapple down."); return; }

            Vector3 outward = dx < dz ? new Vector3(Mathf.Sign(p.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(p.z));
            Vector3 hook = dx < dz
                ? new Vector3(Mathf.Sign(p.x) * (hx + 1.2f), 0.3f, p.z)
                : new Vector3(p.x, 0.3f, Mathf.Sign(p.z) * (hz + 1.2f));
            Vector3 ground = new Vector3(hook.x, StreetBuilder.StreetY + 0.3f, hook.z) + outward * 3f;
            StartCoroutine(Travel(new[] { hook, new Vector3(hook.x, StreetBuilder.StreetY + 0.3f, hook.z), ground },
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
                transform.position = Vector3.Lerp(a, b, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
        }
        cc.enabled = true;
        velY = 0f;
        busy = false;
    }

    // ---------------------------------------------------------------- roof actions

    void Interact()
    {
        FarmPlot best = null;
        float bestDist = reach + 1.5f;   // planters are long, measure from the middle
        foreach (var p in FindObjectsByType<FarmPlot>())
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < bestDist) { best = p; bestDist = d; }
        }
        if (best == null) { Hud.Toast("Nothing to interact with."); return; }

        if (best.Stage == -1) { best.Plant(); Hud.Toast("Planted."); }
        else if (best.Ready) { Hud.Toast("Harvested +" + best.Harvest() + " food."); }
        else if (best.Watered) { Hud.Toast("Already watered today."); }
        else if (best.Water()) { Hud.Toast("Watered (-1 water)."); }
        else { Hud.Toast("No water left."); }
    }

    void PlaceTrap()
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (run == null) return;
        if (!RoofZone.PlayerOnRoof) { Hud.Toast("Build on the roof."); return; }
        if (run.wood < trapWoodCost) { Hud.Toast("Need " + trapWoodCost + " wood."); return; }

        Vector3 pos = transform.position + transform.forward * 2.2f;
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 6f)) pos = hit.point;

        run.wood -= trapWoodCost;
        var go = ModelLibrary.Spawn("cattle-grid", null, pos, transform.eulerAngles.y,
                                    fitFootprint: 2.2f, keepAnimations: false);
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position = pos + Vector3.up * 0.08f;
            go.transform.localScale = new Vector3(1f, 0.15f, 1f);
            Destroy(go.GetComponent<Collider>());
            RoofGenerator.Tint(go, new Color(0.75f, 0.15f, 0.1f));
        }
        go.name = "SpikeTrap";
        var s = go.AddComponent<BaseStructure>();
        s.id = "spike_trap";
        s.defense = 10f;
        Hud.Toast("Trap placed (+10 defense).");
    }
}
