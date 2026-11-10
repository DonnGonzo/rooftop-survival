using UnityEngine;

/// <summary>
/// The double doors of a street building. Closed they are solid, for you and for anything hunting you;
/// the Interact key swings both leaves out toward the street, and shuts them again.
/// </summary>
public class Door : Interactable
{
    Transform left, right;
    Collider[] solids;
    float angle, target;      // degrees each leaf is swung outward
    const float OpenAngle = 100f, Speed = 260f;

    public bool Open => target > 1f;

    public override string Prompt => Open ? "Close the doors" : "Open the doors";
    public override Vector3 Point => transform.position + Vector3.up * 1.2f;

    public override void Use()
    {
        target = Open ? 0f : OpenAngle;
        Sfx.PlayAt(Open ? "door_open" : "door_close", Point, 0.8f, 30f);
        // Open leaves are not solid, so one swinging past you can never trap you.
        if (Open) foreach (var c in solids) c.enabled = false;
    }

    void Update()
    {
        if (Mathf.Approximately(angle, target)) return;
        // Ease out: fast at first, settling at the end of the swing.
        float step = Mathf.Max(40f, Mathf.Abs(target - angle) * 4.5f);
        angle = Mathf.MoveTowards(angle, target, Mathf.Min(step, Speed) * Time.deltaTime);
        left.localRotation = Quaternion.Euler(0f, -angle, 0f);
        right.localRotation = Quaternion.Euler(0f, angle, 0f);
        if (angle <= 0.01f) foreach (var c in solids) c.enabled = true;
    }

    /// <summary>Builds a pair of doors filling an opening w x h, centred on localPos, facing +z of the parent.</summary>
    public static Door Build(Transform parent, Vector3 localPos, float w, float h, bool steel, System.Random rng)
    {
        var go = new GameObject("Doors");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var d = go.AddComponent<Door>();
        d.range = 2.6f;

        Color paint = steel ? new Color(0.36f, 0.4f, 0.42f)
                    : Color.Lerp(new Color(0.62f, 0.3f, 0.2f), new Color(0.25f, 0.45f, 0.48f), (float)rng.NextDouble());
        var body = Look.Mat(steel ? "plain" : "planks", paint, 1.4f, steel ? 0.35f : 0.12f);
        var metal = Look.Mat("plain", new Color(0.62f, 0.6f, 0.55f), 2f, 0.5f);
        var dark = Look.Mat("plain", new Color(0.12f, 0.12f, 0.13f), 2f, 0.3f);
        var glass = Look.Mat("plain", new Color(0.2f, 0.27f, 0.3f), 2f, 0.85f);

        d.left = Leaf(go.transform, -1, w / 2f, h, steel, body, metal, dark, glass);
        d.right = Leaf(go.transform, 1, w / 2f, h, steel, body, metal, dark, glass);
        // Only the two panels are solid (the trim's colliders are already marked for removal).
        d.solids = new Collider[] { d.left.Find("Panel").GetComponent<Collider>(), d.right.Find("Panel").GetComponent<Collider>() };
        return d;
    }

    /// <summary>One leaf, hinged on the jamb at side (-1 left, +1 right) and reaching to the middle.</summary>
    static Transform Leaf(Transform doors, int side, float lw, float h, bool steel, Material body, Material metal, Material dark, Material glass)
    {
        var hinge = new GameObject(side < 0 ? "LeafLeft" : "LeafRight").transform;
        hinge.SetParent(doors, false);
        hinge.localPosition = new Vector3(side * lw, 0f, 0f);
        float cx = -side * lw / 2f;                 // leaf centre, measured from the hinge
        float pw = lw - 0.03f, ph = h - 0.05f, t = 0.07f;
        Look.Box("Panel", hinge, new Vector3(cx, h / 2f, 0f), new Vector3(pw, ph, t), body);
        // Trim that sits proud of the panel on both faces (no colliders: the panel is the solid part).
        foreach (float z in new[] { t / 2f + 0.006f, -t / 2f - 0.006f })
        {
            Look.Box("Kick", hinge, new Vector3(cx, 0.2f, z), new Vector3(pw - 0.04f, 0.3f, 0.012f), metal, false);
            if (steel)
            {
                Look.Box("Rib", hinge, new Vector3(cx, h * 0.5f, z), new Vector3(pw - 0.04f, 0.06f, 0.012f), dark, false);
                Look.Box("Rib", hinge, new Vector3(cx, h * 0.78f, z), new Vector3(pw - 0.04f, 0.06f, 0.012f), dark, false);
            }
            else
            {
                Look.Box("Glass", hinge, new Vector3(cx, h * 0.66f, z), new Vector3(pw * 0.62f, h * 0.36f, 0.012f), glass, false);
                Look.Box("Bar", hinge, new Vector3(cx, h * 0.66f, z + Mathf.Sign(z) * 0.004f), new Vector3(0.03f, h * 0.36f, 0.012f), body, false);
            }
            // Push bar near the meeting edge.
            float hx = -side * (lw - 0.16f);
            Look.Box("Handle", hinge, new Vector3(hx, 1.05f, z + Mathf.Sign(z) * 0.03f), new Vector3(0.035f, 0.42f, 0.035f), metal, false);
            Look.Box("HandleTop", hinge, new Vector3(hx, 1.25f, z + Mathf.Sign(z) * 0.015f), new Vector3(0.035f, 0.035f, 0.05f), metal, false);
            Look.Box("HandleLow", hinge, new Vector3(hx, 0.85f, z + Mathf.Sign(z) * 0.015f), new Vector3(0.035f, 0.035f, 0.05f), metal, false);
        }
        for (int i = 0; i < 3; i++)
            Look.Box("Hinge", hinge, new Vector3(-side * 0.015f, h * (0.15f + 0.35f * i), 0f), new Vector3(0.04f, 0.14f, t + 0.03f), dark, false);
        return hinge;
    }
}
