using UnityEngine;

/// <summary>
/// First-person controller + roof actions.
/// WASD move, mouse look, Shift sprint, Space jump,
/// E = plant / water / harvest the nearest planter, T = place a spike trap (costs 2 wood).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform cam;
    public float walkSpeed = 4.5f, sprintSpeed = 7f, jumpSpeed = 5f, gravity = -18f;
    public float sensitivity = 1.2f, reach = 3.5f;
    public int trapWoodCost = 2;

    CharacterController cc;
    float pitch, velY;

    void Awake() { cc = GetComponent<CharacterController>(); }

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

        var m = Inp.Move();
        Vector3 dir = transform.right * m.x + transform.forward * m.y;
        float speed = Inp.Sprint() ? sprintSpeed : walkSpeed;

        if (cc.isGrounded && velY < 0f) velY = -2f;
        if (cc.isGrounded && Inp.Jump()) velY = jumpSpeed;
        velY += gravity * Time.deltaTime;

        cc.Move((dir * speed + Vector3.up * velY) * Time.deltaTime);

        if (transform.position.y < -60f) Respawn();
        if (Inp.Interact()) Interact();
        if (Inp.Build()) PlaceTrap();
    }

    void Respawn()
    {
        cc.enabled = false;
        transform.position = new Vector3(0f, 0.3f, 0f);
        cc.enabled = true;
        velY = 0f;
        Hud.Toast("You fell. Grapple gear comes later - back to the roof.");
    }

    void Interact()
    {
        FarmPlot best = null;
        float bestDist = reach;
        foreach (var p in FindObjectsByType<FarmPlot>(FindObjectsSortMode.None))
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

        Vector3 pos = transform.position + transform.forward * 2f;
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 6f)) pos = hit.point;

        run.wood -= trapWoodCost;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "SpikeTrap";
        go.transform.position = pos + Vector3.up * 0.08f;
        go.transform.localScale = new Vector3(1f, 0.15f, 1f);
        Destroy(go.GetComponent<Collider>());
        RoofGenerator.Tint(go, new Color(0.75f, 0.15f, 0.1f));
        var s = go.AddComponent<BaseStructure>();
        s.id = "spike_trap";
        s.defense = 10f;
        Hud.Toast("Trap placed (+10 defense).");
    }
}
