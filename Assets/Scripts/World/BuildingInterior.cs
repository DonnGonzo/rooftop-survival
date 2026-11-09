using UnityEngine;

/// <summary>
/// Gives each skyline building a walkable ground-floor room with a doorway, sized from its footprint.
/// The building models have no collider, so you can walk through the facade. The room's walls and
/// doorway then stop you inside, and the doorway is the way in. The room sits inside the closed model,
/// so it is hidden from outside and visible from inside.
/// Limitation: the facade has no visible door. Real building models with entrances would fix that.
/// </summary>
public static class BuildingInterior
{
    const float Wall = 0.3f;
    const float Height = 4f;     // room height
    const float DoorW = 2.4f;
    const float DoorH = 2.7f;

    public static void Build(Transform parent, Vector2 pos, float radius, System.Random rng, float groundY)
    {
        float hx = Mathf.Max(3f, radius * 0.75f);
        float hz = hx;
        float ax = hx + Wall / 2f;   // centre of the X-facing walls
        float az = hz + Wall / 2f;   // centre of the Z-facing walls

        var root = new GameObject("Interior").transform;
        root.SetParent(parent, false);
        root.localPosition = new Vector3(pos.x, groundY, pos.y);

        Color wallCol = Color.Lerp(new Color(0.55f, 0.52f, 0.48f), new Color(0.35f, 0.4f, 0.36f), (float)rng.NextDouble());
        Color floorCol = new Color(0.3f, 0.27f, 0.24f);

        // Floor top sits 5 cm above the street plane to avoid z-fighting with it.
        Box(root, new Vector3(0f, 0f, 0f), new Vector3(ax * 2f, 0.1f, az * 2f), floorCol);
        Box(root, new Vector3(0f, Height + 0.05f, 0f), new Vector3(ax * 2f, 0.1f, az * 2f), wallCol);

        // The door faces the city centre, which is where the roof and the player's start are.
        Vector2 toCentre = -pos.normalized;
        bool doorOnX = Mathf.Abs(toCentre.x) > Mathf.Abs(toCentre.y);
        float sign = doorOnX ? (toCentre.x >= 0f ? 1f : -1f) : (toCentre.y >= 0f ? 1f : -1f);

        float wallH = Height - 0.05f;
        float wallY = 0.05f + wallH / 2f;
        float lintelH = Height - DoorH;
        float lintelY = DoorH + lintelH / 2f;

        if (doorOnX)
        {
            float fullZ = 2f * (hz + Wall);          // length of the X-facing walls
            float side = (fullZ - DoorW) / 2f;
            float off = DoorW / 2f + side / 2f;

            Box(root, new Vector3(-sign * ax, wallY, 0f), new Vector3(Wall, wallH, fullZ), wallCol);   // back wall
            Box(root, new Vector3(sign * ax, wallY, -off), new Vector3(Wall, wallH, side), wallCol);   // door wall, left
            Box(root, new Vector3(sign * ax, wallY, off), new Vector3(Wall, wallH, side), wallCol);    // door wall, right
            Box(root, new Vector3(sign * ax, lintelY, 0f), new Vector3(Wall, lintelH, DoorW), wallCol); // lintel
            Box(root, new Vector3(0f, wallY, az), new Vector3(2f * hx, wallH, Wall), wallCol);         // side +z
            Box(root, new Vector3(0f, wallY, -az), new Vector3(2f * hx, wallH, Wall), wallCol);        // side -z
        }
        else
        {
            float fullX = 2f * hx;                   // length of the Z-facing walls, between the X walls
            float side = (fullX - DoorW) / 2f;
            float off = DoorW / 2f + side / 2f;

            Box(root, new Vector3(ax, wallY, 0f), new Vector3(Wall, wallH, 2f * (hz + Wall)), wallCol);   // side +x
            Box(root, new Vector3(-ax, wallY, 0f), new Vector3(Wall, wallH, 2f * (hz + Wall)), wallCol);  // side -x
            Box(root, new Vector3(0f, wallY, -sign * az), new Vector3(fullX, wallH, Wall), wallCol);      // back wall
            Box(root, new Vector3(-off, wallY, sign * az), new Vector3(side, wallH, Wall), wallCol);      // door wall, left
            Box(root, new Vector3(off, wallY, sign * az), new Vector3(side, wallH, Wall), wallCol);       // door wall, right
            Box(root, new Vector3(0f, lintelY, sign * az), new Vector3(DoorW, lintelH, Wall), wallCol);   // lintel
        }

        // A lamp that switches on only near the room. Many always-on point lights would be too heavy.
        var lampGo = new GameObject("RoomLamp");
        lampGo.transform.SetParent(root, false);
        lampGo.transform.localPosition = new Vector3(0f, Height - 0.4f, 0f);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 12f;
        lamp.intensity = 1.4f;
        lamp.color = new Color(1f, 0.85f, 0.6f);
        lamp.shadows = LightShadows.None;
        lamp.enabled = false;
        var sw = root.gameObject.AddComponent<InteriorLightSwitch>();
        sw.lamp = lamp;

        // Some rooms hold a little loot.
        if (rng.NextDouble() < 0.6)
        {
            string[] ids = { "scrap", "cloth", "food", "water", "wood" };
            string id = ids[rng.Next(ids.Length)];
            Vector3 at = new Vector3((rng.NextDouble() < 0.5 ? -1f : 1f) * hx * 0.4f, 0.1f, 0f);
            Pickup.Create("loot-crate", id, rng.Next(1, 4), root, root.TransformPoint(at));
        }
    }

    static void Box(Transform parent, Vector3 localCentre, Vector3 size, Color col)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "RoomPart";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCentre;
        go.transform.localScale = size;   // keeps the default BoxCollider, so walls and floors block you
        RoofGenerator.Tint(go, col);
    }
}

/// <summary>Turns a room lamp on only while the player is close.</summary>
public class InteriorLightSwitch : MonoBehaviour
{
    public Light lamp;
    const float Range = 22f;

    void Update()
    {
        var p = PlayerController.Instance;
        if (p == null || lamp == null) return;
        bool near = (p.transform.position - transform.position).sqrMagnitude < Range * Range;
        if (lamp.enabled != near) lamp.enabled = near;
    }
}
