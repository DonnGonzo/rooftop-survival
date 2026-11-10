using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The look shared by the menus: flat dark panels, an amber accent, and a small set of text and button
/// styles. Everything is built in code (no image files) the first time it is asked for.
/// </summary>
public static class Skin
{
    public static readonly Color Bg = new Color(0.07f, 0.08f, 0.1f, 0.97f);
    public static readonly Color Panel = new Color(0.12f, 0.13f, 0.16f, 1f);
    public static readonly Color PanelLight = new Color(0.17f, 0.19f, 0.23f, 1f);
    public static readonly Color Line = new Color(0.28f, 0.3f, 0.35f, 1f);
    public static readonly Color Accent = new Color(0.93f, 0.66f, 0.24f, 1f);
    public static readonly Color Good = new Color(0.5f, 0.85f, 0.5f, 1f);
    public static readonly Color Bad = new Color(0.95f, 0.45f, 0.4f, 1f);
    public static readonly Color Text = new Color(0.93f, 0.93f, 0.92f, 1f);
    public static readonly Color Dim = new Color(0.62f, 0.64f, 0.68f, 1f);

    public static GUIStyle Title, H1, H2, Body, DimText, Small, Button, ButtonAccent, Tab, TabOn, Card, CardOn, Centre, Tagline;
    static readonly Dictionary<Color, Texture2D> solids = new Dictionary<Color, Texture2D>();
    static bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { built = false; solids.Clear(); }

    public static Texture2D Solid(Color c)
    {
        if (solids.TryGetValue(c, out var t) && t != null) return t;
        t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        t.hideFlags = HideFlags.HideAndDontSave;
        solids[c] = t;
        return t;
    }

    /// <summary>A flat fill with a 1 px border, usable as a stretchable background.</summary>
    static Texture2D Framed(Color fill, Color border)
    {
        var t = new Texture2D(8, 8);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                t.SetPixel(x, y, (x == 0 || y == 0 || x == 7 || y == 7) ? border : fill);
        t.Apply();
        t.filterMode = FilterMode.Point;
        t.hideFlags = HideFlags.HideAndDontSave;
        return t;
    }

    static GUIStyle Text_(int size, Color c, FontStyle fs = FontStyle.Normal, bool wrap = true)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, wordWrap = wrap, richText = true };
        s.normal.textColor = c;
        return s;
    }

    static GUIStyle Btn(Color fill, Color hover, Color border, Color text, int size, TextAnchor anchor)
    {
        var s = new GUIStyle(GUI.skin.button) { fontSize = size, fontStyle = FontStyle.Bold, alignment = anchor, richText = true, wordWrap = true };
        s.border = new RectOffset(2, 2, 2, 2);
        s.padding = new RectOffset(14, 14, 6, 6);
        s.margin = new RectOffset(0, 0, 3, 3);
        s.normal.background = Framed(fill, border);
        s.hover.background = Framed(hover, Accent);
        s.active.background = Framed(Accent, Accent);
        s.focused.background = s.normal.background;
        s.onNormal.background = s.normal.background;
        s.normal.textColor = text; s.hover.textColor = Color.white; s.active.textColor = Color.black; s.focused.textColor = text;
        return s;
    }

    public static void Ensure()
    {
        if (built && Title != null) return;
        built = true;
        Title = Text_(46, Color.white, FontStyle.Bold, false);
        Tagline = Text_(16, Accent, FontStyle.Bold, false);
        H1 = Text_(22, Color.white, FontStyle.Bold, false);
        H2 = Text_(16, Accent, FontStyle.Bold, false);
        Body = Text_(14, Text);
        DimText = Text_(13, Dim);
        Small = Text_(12, Dim, FontStyle.Normal, false);
        Centre = Text_(14, Text, FontStyle.Bold);
        Centre.alignment = TextAnchor.MiddleCenter;
        Button = Btn(PanelLight, new Color(0.24f, 0.26f, 0.31f), Line, Text, 15, TextAnchor.MiddleLeft);
        ButtonAccent = Btn(Accent, new Color(1f, 0.76f, 0.36f), Accent, new Color(0.08f, 0.07f, 0.05f), 16, TextAnchor.MiddleCenter);
        ButtonAccent.hover.textColor = Color.black;
        Tab = Btn(Panel, PanelLight, Line, Dim, 14, TextAnchor.MiddleCenter);
        TabOn = Btn(PanelLight, PanelLight, Accent, Color.white, 14, TextAnchor.MiddleCenter);
        Tab.wordWrap = false; TabOn.wordWrap = false; Tab.padding = new RectOffset(6, 6, 4, 4); TabOn.padding = new RectOffset(6, 6, 4, 4);
        Card = Btn(Panel, PanelLight, Line, Text, 13, TextAnchor.UpperLeft);
        Card.fontStyle = FontStyle.Normal;
        CardOn = Btn(PanelLight, PanelLight, Accent, Color.white, 13, TextAnchor.UpperLeft);
        CardOn.fontStyle = FontStyle.Normal;
    }

    public static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    /// <summary>A panel with a thin border.</summary>
    public static void Box(Rect r, Color fill)
    {
        Fill(r, Line);
        Fill(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), fill);
    }

    public static void Bar(Rect r, float frac, Color fill)
    {
        Fill(r, new Color(0f, 0f, 0f, 0.6f));
        Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(frac), r.height - 2), fill);
    }

    /// <summary>Colour tag for an item category, used as a small swatch on cards.</summary>
    public static Color CategoryColor(string cat)
    {
        switch (cat)
        {
            case Items.Mat: return new Color(0.6f, 0.5f, 0.38f);
            case Items.Eat: return new Color(0.5f, 0.75f, 0.45f);
            case Items.Tool: return new Color(0.75f, 0.45f, 0.4f);
            case Items.Gear: return new Color(0.45f, 0.6f, 0.85f);
            default: return new Color(0.8f, 0.65f, 0.3f);
        }
    }
}
