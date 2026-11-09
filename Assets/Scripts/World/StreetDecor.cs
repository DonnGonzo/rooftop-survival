using UnityEngine;

/// <summary>
/// Street-level ground tiles (roads, meadow, paving, gravel), props and supply crates.
/// Seeded from the run so the street is the same after a reload.
/// </summary>
public static class StreetDecor
{
    const float Tile = 6f;       // every ground tile is fitted to a 6 m square
    const float Extent = 60f;    // tiles cover -60..60 around the building
    const float RoadHalf = 3.5f; // tile centres within this distance of x=0 or z=0 are roads

    static readonly string[] Roads = { "highway-tile", "dirt-road-tile" };
    static readonly string[] Ground = { "meadow-tile", "cobblestone-street-tile", "promenade-paving-tile", "mine-spoil-gravel-tile" };
    static readonly string[] Props =
    {
        "fire-hydrant", "taxi-rank-sign", "station-bench", "railway-telegraph-pole-run",
        "velvet-rope-stanchion", "ornamental-grass-tuft", "shell-scatter"
    };

    struct Loot
    {
        public string model, item;
        public int min, max;
        public Loot(string model, string item, int min, int max)
        {
            this.model = model;
            this.item = item;
            this.min = min;
            this.max = max;
        }
    }

    static readonly Loot[] Table =
    {
        new Loot("loot-crate", "scrap", 2, 4),
        new Loot("loot-crate", "cloth", 1, 3),
        new Loot("loot-sack", "food", 1, 3),
        new Loot("bottle", "water", 1, 2),
        new Loot("tool-case", "scrap", 3, 5),
        new Loot("hollow-log", "wood", 2, 3),
        new Loot("treasure-chest", "wood", 3, 5),
        new Loot("ammo-crate", "cloth", 2, 4),
        new Loot("gold-coin", "scrap", 1, 2),
        new Loot("bread-loaf", "food", 1, 1),
    };

    public static void Build(int seed, Transform parent)
    {
        var rng = new System.Random(seed + 7);
        float noiseOffset = seed * 0.0001f;

        for (float x = -Extent; x < Extent; x += Tile)
        {
            for (float z = -Extent; z < Extent; z += Tile)
            {
                // Lifted a few cm above the StreetBuilder ground plane to avoid z-fighting.
                Vector3 c = new Vector3(x + Tile / 2f, StreetBuilder.StreetY + 0.03f, z + Tile / 2f);
                string key;
                float yaw;

                if (IsRoad(c))
                {
                    key = Roads[rng.Next(Roads.Length)];
                    yaw = Mathf.Abs(c.x) < RoadHalf ? 90f : 0f;
                }
                else
                {
                    float n = Mathf.PerlinNoise(c.x * 0.045f + noiseOffset, c.z * 0.045f + noiseOffset);
                    key = n < 0.35f ? Ground[0] : n < 0.6f ? Ground[1] : n < 0.8f ? Ground[2] : Ground[3];
                    yaw = rng.Next(4) * 90f;
                }

                ModelLibrary.Spawn(key, parent, c, yaw, fitFootprint: Tile, keepAnimations: false);
            }
        }

        for (int i = 0; i < 45; i++)
        {
            Vector3 p = RandomStreetPoint(rng, 14f, 110f);
            if (IsRoad(p) && rng.NextDouble() < 0.6) continue;
            ModelLibrary.Spawn(Props[rng.Next(Props.Length)], parent, p, (float)rng.NextDouble() * 360f, keepAnimations: false);
        }

        for (int i = 0; i < 30; i++)
        {
            Vector3 p = RandomStreetPoint(rng, 16f, 110f);
            var l = Table[rng.Next(Table.Length)];
            Pickup.Create(l.model, l.item, rng.Next(l.min, l.max + 1), parent, p);
        }
    }

    static bool IsRoad(Vector3 p) => Mathf.Abs(p.x) < RoadHalf || Mathf.Abs(p.z) < RoadHalf;

    static Vector3 RandomStreetPoint(System.Random rng, float min, float max)
    {
        float a = (float)rng.NextDouble() * Mathf.PI * 2f;
        float d = Mathf.Lerp(min, max, (float)rng.NextDouble());
        return new Vector3(Mathf.Cos(a) * d, StreetBuilder.StreetY, Mathf.Sin(a) * d);
    }
}
