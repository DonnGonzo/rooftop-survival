using UnityEngine;

/// <summary>
/// Places the roof kits the player has crafted. Traps (spike, snare) have durability and catch wildlife;
/// the barricade adds defense to the base; the planter is a crop bed.
/// </summary>
public static class Buildables
{
    /// <summary>Places one kit. Returns false if the kit is not a buildable.</summary>
    public static bool Place(string kit, Vector3 pos, float yaw)
    {
        switch (kit)
        {
            case "trap_kit": PlaceSpike(pos, yaw); break;
            case "snare_kit": PlaceSnare(pos, yaw); break;
            case "barricade_kit": PlaceBarricade(pos, yaw); break;
            case "planter_kit": RoofGenerator.SpawnPlanterAt(pos, yaw); break;
            default: return false;
        }
        Tutorial.Notify("trap");
        return true;
    }

    static void PlaceSpike(Vector3 pos, float yaw)
    {
        var go = ModelLibrary.Spawn("cattle-grid", null, pos, yaw, fitFootprint: 2.2f, keepAnimations: false)
                 ?? CubeAt(pos, yaw, new Vector3(1f, 0.15f, 1f), new Color(0.75f, 0.15f, 0.1f));
        go.name = "SpikeTrap";
        var s = go.AddComponent<BaseStructure>();
        s.id = "spike_trap";
        s.defense = 10f;
        Trap.Attach(go, radius: 1.6f, hitDamage: 20f, wearPerCatch: 15f, durability: 60f);
        Hud.Toast("Spike trap set (+10 defense).");
    }

    static void PlaceSnare(Vector3 pos, float yaw)
    {
        var go = CubeAt(pos, yaw, new Vector3(1.2f, 0.05f, 1.2f), new Color(0.55f, 0.5f, 0.35f));
        go.name = "Snare";
        Trap.Attach(go, radius: 1.2f, hitDamage: 12f, wearPerCatch: 10f, durability: 25f);
        Hud.Toast("Snare set.");
    }

    static void PlaceBarricade(Vector3 pos, float yaw)
    {
        var go = ModelLibrary.Spawn("sandbag-wall-section-7075ee", null, pos, yaw, fitFootprint: 2.6f,
                                    addCollider: true, keepAnimations: false)
                 ?? CubeAt(pos, yaw, new Vector3(2.4f, 1.1f, 0.5f), new Color(0.5f, 0.42f, 0.3f));
        go.name = "Barricade";
        var s = go.AddComponent<BaseStructure>();
        s.id = "barricade";
        s.defense = 15f;
        Hud.Toast("Barricade placed (+15 defense).");
    }

    /// <summary>Placeholder box when a model is missing. It sits on the roof, not inside it.</summary>
    static GameObject CubeAt(Vector3 pos, float yaw, Vector3 scale, Color c)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = pos + Vector3.up * (scale.y / 2f + 0.02f);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        go.transform.localScale = scale;
        Object.Destroy(go.GetComponent<Collider>());
        RoofGenerator.Tint(go, c);
        return go;
    }
}
