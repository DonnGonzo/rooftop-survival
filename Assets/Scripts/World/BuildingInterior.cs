using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ground floor of a street building: floor, walls, ceiling, a doorway on the street side, lights,
/// and furniture chosen for what the building is (home, shop, office, garage, diner). The furniture that
/// can hold things (fridges, wardrobes, lockers, shelves, crates) can be searched by holding Interact.
/// The room's walls are also what stops you walking through the building's outer face.
/// </summary>
public static class BuildingInterior
{
    public enum Kind { Home, Shop, Office, Garage, Diner }

    const float Height = 3.4f, Wall = 0.25f, DoorW = 2.4f, DoorH = 2.7f;

    /// <summary>One furniture model: how to size it and what a search of it can give.</summary>
    class Item
    {
        public string key, search;
        public bool byHeight;      // size is its height (tall things) rather than its longest side
        public float size, lift, seconds;
        public LootRoll[] loot;
        public Item(string key, bool byHeight, float size, float lift = 0f, string search = null, float seconds = 2f, params LootRoll[] loot)
        { this.key = key; this.byHeight = byHeight; this.size = size; this.lift = lift; this.search = search; this.seconds = seconds; this.loot = loot; }
    }

    static LootRoll L(string item, int min, int max, float chance = 1f) { return new LootRoll(item, min, max, chance); }

    static readonly Dictionary<string, Item> Catalog = new Dictionary<string, Item>
    {
        { "fridge", new Item("fridge-freezer", true, 1.85f, 0f, "Search fridge", 2f, L("food", 1, 2, 0.65f), L("water", 1, 2, 0.75f)) },
        { "drinks", new Item("glass-door-drinks-fridge", true, 1.95f, 0f, "Search drinks fridge", 2f, L("water", 1, 3, 0.9f)) },
        { "cupboard", new Item("base-cabinet-run", false, 2.0f, 0f, "Search cupboards", 2.2f, L("food", 1, 3, 0.7f), L("cloth", 1, 1, 0.3f)) },
        { "sink", new Item("kitchen-sink", false, 1.2f, 0f, "Check under the sink", 1.6f, L("water", 1, 1, 0.5f), L("scrap", 1, 1, 0.4f)) },
        { "wardrobe", new Item("wardrobe", true, 2.0f, 0f, "Search wardrobe", 2.2f, L("cloth", 1, 3, 0.9f), L("bandage", 1, 1, 0.15f)) },
        { "bookshelf", new Item("bookshelf", true, 1.9f, 0f, "Search bookshelf", 1.8f, L("fiber", 1, 3, 0.7f)) },
        { "bed", new Item("hotel-king-bed", false, 2.3f, 0f, "Strip the bed", 2f, L("cloth", 1, 2, 0.8f)) },
        { "bedside", new Item("bedside-locker", true, 0.6f, 0f, "Search bedside locker", 1.4f, L("bandage", 1, 1, 0.35f), L("cloth", 1, 1, 0.5f)) },
        { "tv", new Item("flat-screen-television", false, 1.3f, 0.5f, "Pull the TV apart", 2.6f, L("scrap", 1, 2, 0.9f)) },
        { "medicine", new Item("medicine-cabinet", false, 0.6f, 1.35f, "Search medicine cabinet", 1.6f, L("bandage", 1, 2, 0.85f)) },
        { "firstaid", new Item("wall-first-aid-box", false, 0.45f, 1.35f, "Open first aid box", 1.4f, L("bandage", 1, 2, 0.9f)) },
        { "sofa", new Item("sofa", false, 2.1f, 0f, "Search under the cushions", 1.6f, L("cloth", 1, 1, 0.5f), L("scrap", 1, 1, 0.3f)) },
        { "vending", new Item("vending-machine", true, 1.9f, 0f, "Break into vending machine", 3.2f, L("food", 1, 3, 0.9f), L("water", 1, 2, 0.8f), L("scrap", 1, 1, 0.5f)) },
        { "rack", new Item("wire-shelving-rack", true, 1.9f, 0f, "Search shelves", 2f, L("food", 1, 2, 0.5f), L("water", 1, 1, 0.35f), L("cloth", 1, 1, 0.3f)) },
        { "supplies", new Item("supply-shelving-rack", true, 2.0f, 0f, "Search supply rack", 2.2f, L("scrap", 1, 3, 0.8f), L("stone", 1, 2, 0.3f), L("rope", 1, 1, 0.2f)) },
        { "crate", new Item("wooden-crate", false, 0.85f, 0f, "Pry open crate", 2f, L("wood", 2, 3), L("scrap", 1, 2, 0.4f)) },
        { "salvage", new Item("salvage-crate", false, 0.85f, 0f, "Search salvage crate", 2f, L("scrap", 2, 4), L("rope", 1, 1, 0.2f)) },
        { "supplycrate", new Item("supply-crate", false, 0.85f, 0f, "Open supply crate", 2f, L("food", 1, 2, 0.7f), L("bandage", 1, 1, 0.3f), L("water", 1, 2, 0.6f)) },
        { "filing", new Item("filing-cabinet", true, 1.3f, 0f, "Search filing cabinet", 1.8f, L("fiber", 1, 2, 0.6f), L("scrap", 1, 1, 0.3f)) },
        { "lockers", new Item("locker-bank", true, 1.85f, 0f, "Search lockers", 2.6f, L("cloth", 1, 2, 0.7f), L("scrap", 1, 2, 0.4f), L("bandage", 1, 1, 0.25f)) },
        { "barrel", new Item("wooden-barrel", true, 0.95f, 0f, "Check barrel", 1.4f, L("water", 1, 2, 0.5f)) },
        { "tyres", new Item("tyre-stack", true, 0.9f) },
        { "bin", new Item("metal-trash-can", true, 0.75f, 0f, "Dig through the bin", 1.4f, L("scrap", 1, 1, 0.4f), L("fiber", 1, 2, 0.4f)) },
        { "desk", new Item("sit-stand-desk", false, 1.5f, 0f, "Search desk", 1.6f, L("fiber", 1, 1, 0.5f), L("scrap", 1, 1, 0.25f)) },
        { "chair", new Item("task-chair", true, 1.0f) },
        { "reception", new Item("reception-desk", false, 2.6f, 0f, "Search reception desk", 2f, L("cloth", 1, 1, 0.4f), L("bandage", 1, 1, 0.2f)) },
        { "counter", new Item("diner-counter-run", false, 2.6f) },
        { "stool", new Item("counter-stool", true, 0.8f) },
        { "table", new Item("heavy-timber-picnic-table", false, 2.0f) },
        { "rug", new Item("area-rug", false, 3.2f) },
        { "trolley", new Item("abandoned-shopping-trolley", false, 1.0f, 0f, "Check trolley", 1.2f, L("food", 1, 1, 0.5f)) },
        { "backpack", new Item("adventurer-s-backpack", false, 0.5f, 0f, "Search backpack", 1.6f, L("food", 1, 2, 0.6f), L("bandage", 1, 1, 0.4f), L("rope", 1, 1, 0.3f)) },
        { "microwave", new Item("microwave-oven", false, 0.5f, 0.92f) },
        { "lamp", new Item("table-lamp", true, 0.5f) },
    };

    static readonly Dictionary<Kind, string[]> WallSets = new Dictionary<Kind, string[]>
    {
        { Kind.Home, new[] { "fridge", "cupboard", "sink", "wardrobe", "bed", "bedside", "bookshelf", "tv", "medicine", "sofa", "bin" } },
        { Kind.Shop, new[] { "drinks", "vending", "rack", "rack", "supplies", "crate", "supplycrate", "cupboard", "bin" } },
        { Kind.Office, new[] { "filing", "filing", "lockers", "bookshelf", "vending", "sofa", "firstaid", "bin" } },
        { Kind.Garage, new[] { "supplies", "supplies", "lockers", "tyres", "barrel", "salvage", "crate", "firstaid", "bin" } },
        { Kind.Diner, new[] { "fridge", "sink", "cupboard", "drinks", "vending", "bin" } },
    };

    public static Kind KindFor(string buildingKey)
    {
        if (buildingKey.Contains("shop") || buildingKey.Contains("store") || buildingKey.Contains("theater")) return Kind.Shop;
        if (buildingKey.Contains("diner")) return Kind.Diner;
        if (buildingKey.Contains("garage") || buildingKey.Contains("fire-station")) return Kind.Garage;
        if (buildingKey.Contains("office") || buildingKey.Contains("tower") || buildingKey.Contains("loft")) return Kind.Office;
        return Kind.Home;
    }

    /// <summary>
    /// centre: the middle of the building at street level. hx, hz: half its footprint.
    /// side: which face has the door (0 = +x, 1 = -x, 2 = +z, 3 = -z).
    /// </summary>
    public static void Build(Transform parent, Vector3 centre, float hx, float hz, int side, Kind kind, System.Random rng)
    {
        var outer = new GameObject("Interior_" + kind).transform;
        outer.SetParent(parent, false);
        outer.position = centre;

        // Everything is laid out in "door space": the door is on the +z face. Turning the root maps that onto the real side.
        float yaw = side == 0 ? 90f : side == 1 ? -90f : side == 2 ? 0f : 180f;
        outer.rotation = Quaternion.Euler(0f, yaw, 0f);
        float fx = side < 2 ? hz : hx;   // half the building's width, as you face the door
        float fz = side < 2 ? hx : hz;   // half its depth

        // The building's outer faces are solid except at the door.
        float ph = 5f, pt = 0.4f;
        Solid(outer, new Vector3(0f, ph / 2f, -fz + pt / 2f), new Vector3(fx * 2f, ph, pt));
        Solid(outer, new Vector3(-fx + pt / 2f, ph / 2f, 0f), new Vector3(pt, ph, fz * 2f));
        Solid(outer, new Vector3(fx - pt / 2f, ph / 2f, 0f), new Vector3(pt, ph, fz * 2f));
        float flank = fx - DoorW / 2f;
        Solid(outer, new Vector3(-(DoorW / 2f + flank / 2f), ph / 2f, fz - pt / 2f), new Vector3(flank, ph, pt));
        Solid(outer, new Vector3(DoorW / 2f + flank / 2f, ph / 2f, fz - pt / 2f), new Vector3(flank, ph, pt));
        Solid(outer, new Vector3(0f, DoorH + (ph - DoorH) / 2f, fz - pt / 2f), new Vector3(DoorW, ph - DoorH, pt));

        // The room itself: just inside the door, and no bigger than a room should be.
        const float inset = 0.7f;
        float ix = Mathf.Min(fx - inset, 8f), iz = Mathf.Min(fz - inset, 7f);   // inner half sizes
        var root = new GameObject("Room").transform;
        root.SetParent(outer, false);
        root.localPosition = new Vector3(0f, 0f, fz - inset - iz);

        Color wallTint = kind == Kind.Garage ? new Color(0.6f, 0.6f, 0.58f)
                       : Color.Lerp(new Color(0.82f, 0.76f, 0.64f), new Color(0.66f, 0.74f, 0.72f), (float)rng.NextDouble());
        var wallMat = Look.Mat(kind == Kind.Garage ? "concrete" : "plaster", wallTint, 3f);
        var floorMat = kind == Kind.Home ? Look.Mat("planks", new Color(0.62f, 0.46f, 0.3f), 2f)
                     : kind == Kind.Office ? Look.Mat("planks", new Color(0.52f, 0.52f, 0.52f), 2f)
                     : kind == Kind.Garage ? Look.Mat("concrete", new Color(0.45f, 0.45f, 0.45f), 3f)
                     : Look.Mat("paving", new Color(0.78f, 0.75f, 0.68f), 2.4f);
        var ceilMat = Look.Mat("plaster", new Color(0.78f, 0.78f, 0.76f), 4f);
        var trim = Look.Mat("planks", new Color(0.3f, 0.24f, 0.18f), 1.5f);

        // Surfaces face inward only, so nothing of the room shows through the building from the street.
        Face(root, new Vector3(0f, 0.04f, 0f), new Vector2(ix * 2f, iz * 2f), Vector3.up, floorMat);
        Face(root, new Vector3(0f, Height, 0f), new Vector2(ix * 2f, iz * 2f), Vector3.down, ceilMat);
        Face(root, new Vector3(0f, Height / 2f, -iz), new Vector2(ix * 2f, Height), Vector3.forward, wallMat);
        Face(root, new Vector3(-ix, Height / 2f, 0f), new Vector2(iz * 2f, Height), Vector3.right, wallMat);
        Face(root, new Vector3(ix, Height / 2f, 0f), new Vector2(iz * 2f, Height), Vector3.left, wallMat);
        float sideW = ix - DoorW / 2f;
        Face(root, new Vector3(-(DoorW / 2f + sideW / 2f), Height / 2f, iz), new Vector2(sideW, Height), Vector3.back, wallMat);
        Face(root, new Vector3(DoorW / 2f + sideW / 2f, Height / 2f, iz), new Vector2(sideW, Height), Vector3.back, wallMat);
        Face(root, new Vector3(0f, DoorH + (Height - DoorH) / 2f, iz), new Vector2(DoorW, Height - DoorH), Vector3.back, wallMat);
        // Door reveal: the short passage through the wall thickness.
        Face(root, new Vector3(-DoorW / 2f, DoorH / 2f, iz + inset / 2f), new Vector2(inset, DoorH), Vector3.right, wallMat);
        Face(root, new Vector3(DoorW / 2f, DoorH / 2f, iz + inset / 2f), new Vector2(inset, DoorH), Vector3.left, wallMat);
        Face(root, new Vector3(0f, DoorH, iz + inset / 2f), new Vector2(DoorW, inset), Vector3.down, wallMat);
        Face(root, new Vector3(0f, 0.04f, iz + inset / 2f), new Vector2(DoorW, inset), Vector3.up, floorMat);

        // What you stand on and bump into.
        Solid(root, new Vector3(0f, -0.21f, inset / 2f), new Vector3(ix * 2f, 0.5f, iz * 2f + inset));
        Solid(root, new Vector3(0f, Height / 2f, -iz - 0.15f), new Vector3(ix * 2f + 0.6f, Height, 0.3f));
        Solid(root, new Vector3(-ix - 0.15f, Height / 2f, 0f), new Vector3(0.3f, Height, iz * 2f));
        Solid(root, new Vector3(ix + 0.15f, Height / 2f, 0f), new Vector3(0.3f, Height, iz * 2f));
        Solid(root, new Vector3(0f, Height + 0.15f, 0f), new Vector3(ix * 2f, 0.3f, iz * 2f));

        // Skirting board, for a finished look.
        Look.Box("SkirtBack", root, new Vector3(0f, 0.12f, -iz + 0.03f), new Vector3(ix * 2f, 0.14f, 0.05f), trim, false);
        Look.Box("SkirtLeft", root, new Vector3(-ix + 0.03f, 0.12f, 0f), new Vector3(0.05f, 0.14f, iz * 2f), trim, false);
        Look.Box("SkirtRight", root, new Vector3(ix - 0.03f, 0.12f, 0f), new Vector3(0.05f, 0.14f, iz * 2f), trim, false);

        // The way in: a frame against the outer face, and a dark panel so the wall reads as an open doorway.
        ModelLibrary.Spawn("rs-doorway", root, new Vector3(0f, 0f, iz + inset + 0.3f), 0f, fitHeight: 3.9f, keepAnimations: false);
        var voidPanel = GameObject.CreatePrimitive(PrimitiveType.Quad);
        voidPanel.name = "DoorDark";
        Object.Destroy(voidPanel.GetComponent<Collider>());
        voidPanel.transform.SetParent(root, false);
        voidPanel.transform.localPosition = new Vector3(0f, DoorH / 2f, iz + inset + 0.22f);
        voidPanel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // faces the street; unseen from inside
        voidPanel.transform.localScale = new Vector3(DoorW, DoorH, 1f);
        Look.Glow(voidPanel, new Color(0.02f, 0.02f, 0.025f));

        // Light: panels on the ceiling, and one lamp that only burns while you are near (many would be too costly).
        int lx = Mathf.Clamp(Mathf.RoundToInt(ix / 3.5f), 1, 2), lz = Mathf.Clamp(Mathf.RoundToInt(iz / 3.5f), 1, 2);
        for (int a = 0; a < lx; a++)
            for (int b = 0; b < lz; b++)
            {
                var panel = Look.Box("LightPanel", root, new Vector3(Mathf.Lerp(-ix, ix, (a + 0.5f) / lx), Height - 0.04f, Mathf.Lerp(-iz, iz, (b + 0.5f) / lz)),
                                     new Vector3(1.2f, 0.05f, 0.4f), null, false);
                Look.Glow(panel, new Color(1f, 0.93f, 0.75f));
            }
        var lampGo = new GameObject("RoomLamp");
        lampGo.transform.SetParent(root, false);
        lampGo.transform.localPosition = new Vector3(0f, Height - 0.6f, 0f);
        var lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = Mathf.Max(ix, iz) * 1.8f + 5f;
        lamp.intensity = 1.3f;
        lamp.color = new Color(1f, 0.88f, 0.68f);
        lamp.shadows = LightShadows.None;
        lamp.enabled = false;
        var sw = root.gameObject.AddComponent<InteriorLightSwitch>();
        sw.lamp = lamp;
        sw.range = Mathf.Max(ix, iz) + 12f;

        Furnish(root, kind, ix, iz, rng);
    }

    /// <summary>A one-sided surface. size is its width and height (for floors and ceilings: width and depth).</summary>
    static void Face(Transform root, Vector3 centre, Vector2 size, Vector3 facing, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "RoomFace";
        Object.Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root, false);
        q.transform.localPosition = centre;
        // A quad shows the side its local -Z points at.
        q.transform.localRotation = Mathf.Abs(facing.y) > 0.5f ? Quaternion.LookRotation(-facing, Vector3.forward) : Quaternion.LookRotation(-facing, Vector3.up);
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>An invisible block.</summary>
    static void Solid(Transform root, Vector3 centre, Vector3 size)
    {
        var g = new GameObject("Solid");
        g.transform.SetParent(root, false);
        g.transform.localPosition = centre;
        g.AddComponent<BoxCollider>().size = size;
    }

    // ------------------------------------------------------------------ furniture

    static void Furnish(Transform root, Kind kind, float ix, float iz, System.Random rng)
    {
        string[] set = WallSets[kind];
        int pick = rng.Next(set.Length);

        // Walk the three solid walls, and the door wall either side of the door, dropping furniture in slots.
        for (int wall = 0; wall < 4; wall++)
        {
            float len = (wall == 0 || wall == 3) ? ix : iz;   // half length of this wall
            float skipUntil = -999f;
            for (float t = -len + 1.3f; t <= len - 1.3f; t += 2.3f)
            {
                if (t < skipUntil) continue;
                if (wall == 3 && Mathf.Abs(t) < DoorW / 2f + 1.4f) continue;   // keep the doorway clear
                if (rng.NextDouble() < 0.28) continue;                        // some bare wall
                string id = set[pick++ % set.Length];

                Vector3 at, inward;
                switch (wall)
                {
                    case 0: at = new Vector3(t, 0f, -iz); inward = Vector3.forward; break;   // back
                    case 1: at = new Vector3(-ix, 0f, t); inward = Vector3.right; break;     // left
                    case 2: at = new Vector3(ix, 0f, t); inward = Vector3.left; break;       // right
                    default: at = new Vector3(t, 0f, iz); inward = Vector3.back; break;      // door wall
                }
                float width = Place(root, id, at, inward, true);
                if (width > 2.2f) skipUntil = t + 2.3f + 0.1f;
                if (id == "bed" && rng.NextDouble() < 0.8) Place(root, "bedside", at + Vector3.Cross(Vector3.up, inward) * 1.7f, inward, true);
                if (id == "cupboard" && rng.NextDouble() < 0.5) Place(root, "microwave", at, inward, true);
            }
        }

        // The middle of the room.
        Vector3 back = Vector3.back, right = Vector3.right;
        switch (kind)
        {
            case Kind.Home:
                Place(root, "rug", new Vector3(0f, 0.09f, -iz * 0.25f), Vector3.forward, false);
                Place(root, "sofa", new Vector3(0f, 0f, -iz * 0.25f - 1.2f), Vector3.forward, false);
                if (iz > 4f) Place(root, "table", new Vector3(ix * 0.45f, 0f, iz * 0.35f), Vector3.right, false);
                if (rng.NextDouble() < 0.5) Place(root, "backpack", new Vector3(-ix * 0.4f, 0f, iz * 0.3f), Vector3.forward, false);
                break;

            case Kind.Shop:
            {
                // Aisles of shelving running from the door to the back, with the middle aisle left clear.
                int made = 0;
                for (float x = 2.6f; x < ix - 1.6f && made < 12; x += 2.8f)
                    for (float z = -iz + 2.6f; z < iz - 2.8f && made < 12; z += 2.3f)
                    {
                        Place(root, "rack", new Vector3(x, 0f, z), right, false); made++;
                        Place(root, "rack", new Vector3(-x, 0f, z), -right, false); made++;
                    }
                Place(root, "reception", new Vector3(-Mathf.Min(ix - 2.2f, 3.2f), 0f, iz - 2.4f), back, false);
                Place(root, "trolley", new Vector3(1.2f, 0f, iz * 0.3f), right, false);
                break;
            }

            case Kind.Office:
            {
                Place(root, "reception", new Vector3(0f, 0f, iz - 3.4f), Vector3.forward, false);
                int made = 0;
                for (float x = -ix + 2.4f; x < ix - 2f && made < 9; x += 3.2f)
                    for (float z = -iz + 2.6f; z < iz - 5.5f && made < 9; z += 3.2f)
                    {
                        Place(root, "desk", new Vector3(x, 0f, z), Vector3.forward, false);
                        Place(root, "chair", new Vector3(x, 0f, z - 0.75f), Vector3.forward, false);
                        made++;
                    }
                break;
            }

            case Kind.Garage:
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 c = new Vector3(((float)rng.NextDouble() - 0.5f) * (ix * 1.2f), 0f, -iz * 0.3f + ((float)rng.NextDouble() - 0.5f) * iz);
                    Place(root, i % 2 == 0 ? "crate" : "salvage", c, Vector3.forward, false, (float)rng.NextDouble() * 360f);
                    if (rng.NextDouble() < 0.5) Place(root, "tyres", c + new Vector3(1.1f, 0f, 0.3f), Vector3.forward, false);
                }
                break;
            }

            case Kind.Diner:
            {
                float z0 = -iz + 2.6f;
                for (float x = -ix + 2.2f; x < ix - 2f; x += 2.7f)
                {
                    Place(root, "counter", new Vector3(x, 0f, z0), Vector3.forward, false);
                    Place(root, "stool", new Vector3(x - 0.6f, 0f, z0 + 0.9f), back, false);
                    Place(root, "stool", new Vector3(x + 0.6f, 0f, z0 + 0.9f), back, false);
                }
                for (float x = -ix + 2.6f; x < ix - 2.4f; x += 3.6f)
                    if (Mathf.Abs(x) > 1.6f) Place(root, "table", new Vector3(x, 0f, iz * 0.4f), right, false);
                break;
            }
        }
    }

    /// <summary>
    /// Puts one catalog item in the room. against = true: its back goes flat against the wall at 'at' and it
    /// faces 'inward'. Returns the width it takes up along the wall (0 if the model is missing).
    /// </summary>
    static float Place(Transform root, string id, Vector3 at, Vector3 inward, bool against, float extraYaw = 0f)
    {
        Item it;
        if (!Catalog.TryGetValue(id, out it) || !ModelLibrary.Has(it.key)) return 0f;

        float yaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg + extraYaw;
        var go = ModelLibrary.Spawn(it.key, root, Vector3.zero, yaw, fitHeight: it.byHeight ? it.size : 0f,
                                    fitFootprint: it.byHeight ? 0f : it.size, addCollider: id != "rug", keepAnimations: false);
        if (go == null) return 0f;

        var lb = ModelLibrary.LocalBounds(go.transform);
        float s = go.transform.localScale.x;
        float depth = lb.size.z * s, width = lb.size.x * s;
        Vector3 pos = at + Vector3.up * it.lift;
        if (against) pos += inward * (depth / 2f + 0.05f);
        go.transform.localPosition = pos;
        go.name = "F_" + id;

        if (it.search != null)
        {
            var c = Container.Add(go, it.search, it.seconds, 5, it.loot);
            c.range = Mathf.Max(2.2f, Mathf.Max(width, depth) * 0.5f + 1.6f);
        }
        return width;
    }
}

/// <summary>Turns a room lamp on only while the player is close.</summary>
public class InteriorLightSwitch : MonoBehaviour
{
    public Light lamp;
    public float range = 22f;

    void Update()
    {
        var p = PlayerController.Instance;
        if (p == null || lamp == null) return;
        bool near = (p.transform.position - transform.position).sqrMagnitude < range * range;
        if (lamp.enabled != near) lamp.enabled = near;
    }
}
