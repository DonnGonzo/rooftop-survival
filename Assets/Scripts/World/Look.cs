using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Surfaces for everything the game builds from boxes: the roof, the streets, rooms, placeholders.
/// Textures are painted in code (concrete, asphalt, brick, grass and so on) and laid out in world metres
/// by the Rooftop/WorldLit shader, so a stretched box never stretches its texture.
/// </summary>
public static class Look
{
    static Shader lit, glow;
    static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { textures.Clear(); materials.Clear(); lit = null; glow = null; }

    static Shader Lit
    {
        get
        {
            if (lit == null) lit = Resources.Load<Shader>("Shaders/WorldLit");
            if (lit == null) lit = Shader.Find("Standard");
            return lit;
        }
    }

    static Shader GlowShader
    {
        get
        {
            if (glow == null) glow = Resources.Load<Shader>("Shaders/Glow");
            if (glow == null) glow = Shader.Find("Unlit/Color");
            return glow;
        }
    }

    /// <summary>A shared lit material: texture name (see Texture), tint, metres per texture tile.</summary>
    public static Material Mat(string texture, Color tint, float metresPerTile = 2f, float smoothness = 0.08f)
    {
        string key = texture + "|" + ColorUtility.ToHtmlStringRGB(tint) + "|" + metresPerTile + "|" + smoothness;
        if (materials.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Lit);
        m.name = "Look_" + texture;
        m.SetColor("_Color", tint);
        m.SetTexture("_MainTex", Texture(texture));
        m.SetFloat("_Scale", metresPerTile);
        m.SetFloat("_Glossiness", smoothness);
        materials[key] = m;
        return m;
    }

    public static void Paint(GameObject go, string texture, Color tint, float metresPerTile = 2f, float smoothness = 0.08f)
    {
        var r = go.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial = Mat(texture, tint, metresPerTile, smoothness);
    }

    /// <summary>Flat colour (no pattern).</summary>
    public static void Paint(GameObject go, Color tint) { Paint(go, "plain", tint); }

    /// <summary>Unlit, fog-free colour for sky objects and glows. Returns the material so it can be re-tinted.</summary>
    public static Material Glow(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return null;
        var m = new Material(GlowShader);
        m.SetColor("_Color", c);
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return m;
    }

    /// <summary>A box with a collider. size is in metres; parent may be null.</summary>
    public static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool collider = true)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.Destroy(g.GetComponent<Collider>());
        return g;
    }

    // ------------------------------------------------------------------ painted textures

    const int Size = 256;

    public static Texture2D Texture(string name)
    {
        if (textures.TryGetValue(name, out var t) && t != null) return t;
        var px = new Color[Size * Size];
        var rng = new System.Random(name.GetHashCode());
        switch (name)
        {
            case "concrete": Concrete(px, rng); break;
            case "roofing": Roofing(px, rng); break;
            case "asphalt": Asphalt(px, rng); break;
            case "paving": Paving(px, rng); break;
            case "grass": Grass(px, rng); break;
            case "dirt": Dirt(px, rng); break;
            case "brick": Brick(px, rng); break;
            case "windows": Windows(px, rng); break;
            case "planks": Planks(px, rng); break;
            case "plaster": Plaster(px, rng); break;
            case "roadline": RoadLine(px, rng); break;
            default: for (int i = 0; i < px.Length; i++) px[i] = Color.white; break;
        }
        t = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
        t.name = name;
        t.wrapMode = TextureWrapMode.Repeat;
        t.filterMode = FilterMode.Trilinear;
        t.anisoLevel = 8;
        t.SetPixels(px);
        t.Apply(true);
        textures[name] = t;
        return t;
    }

    /// <summary>Noise that wraps at the texture edge, so tiles join without seams. 0..1.</summary>
    static float Noise(int x, int y, float cells, float seed)
    {
        float u = x / (float)Size, v = y / (float)Size;
        float a = Mathf.PerlinNoise(seed + u * cells, seed + v * cells);
        float b = Mathf.PerlinNoise(seed + (u - 1f) * cells, seed + v * cells);
        float c = Mathf.PerlinNoise(seed + u * cells, seed + (v - 1f) * cells);
        float d = Mathf.PerlinNoise(seed + (u - 1f) * cells, seed + (v - 1f) * cells);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static Color Grey(float v) { return new Color(v, v, v, 1f); }

    static void Concrete(Color[] px, System.Random rng)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float v = 0.78f + (Noise(x, y, 4f, 3f) - 0.5f) * 0.22f + (Noise(x, y, 24f, 9f) - 0.5f) * 0.1f
                        + ((float)rng.NextDouble() - 0.5f) * 0.05f;
                float stain = Mathf.SmoothStep(0.62f, 0.8f, Noise(x, y, 3f, 40f));
                v -= stain * 0.16f;
                px[y * Size + x] = Grey(v);
            }
    }

    /// <summary>Flat-roof membrane: gravelly speckle, seams between the sheets, patched areas.</summary>
    static void Roofing(Color[] px, System.Random rng)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float v = 0.7f + (Noise(x, y, 6f, 5f) - 0.5f) * 0.2f + ((float)rng.NextDouble() - 0.5f) * 0.16f;
                if (x % 128 < 3) v -= 0.22f;                       // sheet seams
                if (y % 256 < 2) v -= 0.18f;
                float patch = Mathf.SmoothStep(0.66f, 0.7f, Noise(x, y, 2.5f, 71f));
                v = Mathf.Lerp(v, v * 0.72f, patch);               // tar patches
                float moss = Mathf.SmoothStep(0.7f, 0.85f, Noise(x, y, 5f, 13f));
                var c = Grey(v);
                c = Color.Lerp(c, new Color(v * 0.7f, v * 0.95f, v * 0.6f), moss * 0.5f);
                px[y * Size + x] = c;
            }
    }

    static void Asphalt(Color[] px, System.Random rng)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float v = 0.62f + (Noise(x, y, 5f, 2f) - 0.5f) * 0.2f + ((float)rng.NextDouble() - 0.5f) * 0.2f;
                float crack = Mathf.Abs(Noise(x, y, 7f, 50f) - 0.5f);
                if (crack < 0.012f) v *= 0.45f;
                float weeds = Mathf.SmoothStep(0.72f, 0.8f, Noise(x, y, 4f, 90f));
                var c = Grey(v);
                c = Color.Lerp(c, new Color(0.5f, 0.75f, 0.35f), weeds * 0.55f);
                px[y * Size + x] = c;
            }
    }

    static void Paving(Color[] px, System.Random rng)
    {
        const int slab = 64;
        var shade = new float[16];
        for (int i = 0; i < 16; i++) shade[i] = 0.78f + (float)rng.NextDouble() * 0.16f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int sx = x / slab, sy = y / slab;
                float v = shade[sy * 4 + sx] + ((float)rng.NextDouble() - 0.5f) * 0.06f + (Noise(x, y, 8f, 4f) - 0.5f) * 0.08f;
                var c = Grey(v);
                if (x % slab < 2 || y % slab < 2)
                    c = Color.Lerp(Grey(0.4f), new Color(0.35f, 0.5f, 0.25f), Noise(x, y, 6f, 31f));   // weedy joints
                px[y * Size + x] = c;
            }
    }

    static void Grass(Color[] px, System.Random rng)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Noise(x, y, 5f, 6f), f = (float)rng.NextDouble();
                var c = Color.Lerp(new Color(0.55f, 0.7f, 0.4f), new Color(0.85f, 1f, 0.65f), n);
                c *= 0.82f + f * 0.3f;
                float dry = Mathf.SmoothStep(0.6f, 0.8f, Noise(x, y, 3f, 77f));
                c = Color.Lerp(c, new Color(0.95f, 0.9f, 0.55f), dry * 0.5f);
                c.a = 1f;
                px[y * Size + x] = c;
            }
    }

    static void Dirt(Color[] px, System.Random rng)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float n = Noise(x, y, 6f, 8f);
                var c = Color.Lerp(new Color(0.75f, 0.62f, 0.48f), new Color(1f, 0.9f, 0.75f), n);
                c *= 0.85f + (float)rng.NextDouble() * 0.25f;
                c.a = 1f;
                px[y * Size + x] = c;
            }
    }

    static void Brick(Color[] px, System.Random rng)
    {
        const int bw = 32, bh = 16;
        for (int y = 0; y < Size; y++)
        {
            int row = y / bh;
            for (int x = 0; x < Size; x++)
            {
                int xs = (x + (row % 2) * (bw / 2)) % Size;
                int col = xs / bw;
                float v = 0.75f + Hash(col, row) * 0.25f;
                var c = new Color(v, v * 0.92f, v * 0.88f, 1f);
                if (xs % bw < 2 || y % bh < 2) c = Grey(0.55f);
                c *= 0.92f + (Noise(x, y, 5f, 12f) - 0.5f) * 0.3f;
                c.a = 1f;
                px[y * Size + x] = c;
            }
        }
    }

    /// <summary>One facade tile: a 2 x 2 block of windows in a wall. Some are dark, a few boarded or broken.</summary>
    static void Windows(Color[] px, System.Random rng)
    {
        const int cell = 128;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int cx = x / cell, cy = y / cell, lx = x % cell, ly = y % cell;
                float wall = 0.82f + (Noise(x, y, 6f, 21f) - 0.5f) * 0.18f + ((float)rng.NextDouble() - 0.5f) * 0.04f;
                float streak = Mathf.SmoothStep(0.55f, 0.9f, Noise(x, 0, 14f, 55f)) * (1f - ly / (float)cell) * 0.12f;
                var c = Grey(wall - streak);
                bool inWin = lx > 30 && lx < 98 && ly > 26 && ly < 100;
                if (inWin)
                {
                    int kind = (cx + cy * 2);
                    if (kind == 0) c = new Color(0.16f, 0.2f, 0.24f);                                     // dark glass
                    else if (kind == 1) c = new Color(0.22f, 0.27f, 0.3f) * (0.8f + ly / (float)cell * 0.5f);
                    else if (kind == 2) c = (lx / 12) % 2 == 0 ? new Color(0.5f, 0.4f, 0.28f) : new Color(0.42f, 0.33f, 0.22f);   // boarded
                    else c = new Color(0.1f, 0.12f, 0.14f);                                                // broken, black
                    if (lx == 64 || ly == 63) c = Grey(0.55f);                                             // glazing bars
                    c.a = 1f;
                }
                else if (lx > 26 && lx < 102 && ly > 20 && ly <= 26) c = Grey(0.95f);                      // sill
                if (ly < 3) c *= 0.8f;                                                                     // floor line
                c.a = 1f;
                px[y * Size + x] = c;
            }
    }

    static void Planks(Color[] px, System.Random rng)
    {
        const int pw = 32;
        var shade = new float[8];
        for (int i = 0; i < 8; i++) shade[i] = 0.7f + (float)rng.NextDouble() * 0.3f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int p = x / pw;
                float grain = Mathf.PerlinNoise(x * 0.3f + p * 13f, y * 0.02f) * 0.22f;
                float v = shade[p] - grain;
                var c = new Color(v, v * 0.9f, v * 0.78f, 1f);
                if (x % pw < 2) c = Grey(0.35f);
                if ((y + p * 70) % 256 < 2) c = Grey(0.4f);
                px[y * Size + x] = c;
            }
    }

    static void Plaster(Color[] px, System.Random rng)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float v = 0.85f + (Noise(x, y, 4f, 17f) - 0.5f) * 0.16f + ((float)rng.NextDouble() - 0.5f) * 0.04f;
                float damp = Mathf.SmoothStep(0.6f, 0.85f, Noise(x, y, 3f, 61f));
                var c = Grey(v);
                c = Color.Lerp(c, new Color(v * 0.75f, v * 0.8f, v * 0.62f), damp * 0.6f);
                float crack = Mathf.Abs(Noise(x, y, 6f, 33f) - 0.5f);
                if (crack < 0.008f) c *= 0.6f;
                c.a = 1f;
                px[y * Size + x] = c;
            }
    }

    /// <summary>Asphalt with one worn dashed centre line running along V.</summary>
    static void RoadLine(Color[] px, System.Random rng)
    {
        Asphalt(px, rng);
        for (int y = 0; y < Size; y++)
            for (int x = 122; x < 134; x++)
            {
                if (y % 128 > 70) continue;
                if (Noise(x, y, 20f, 3f) < 0.35f) continue;   // worn away
                px[y * Size + x] = Color.Lerp(px[y * Size + x], new Color(1.6f, 1.5f, 1.1f), 0.8f);
            }
    }

    static float Hash(int a, int b)
    {
        unchecked
        {
            int h = a * 73856093 ^ b * 19349663;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }
}
