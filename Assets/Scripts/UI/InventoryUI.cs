using UnityEngine;

/// <summary>
/// The one window for everything you own and can make, in four tabs:
/// Pack (what you carry and its weight), Craft (every recipe, by category), Character (level, stats, needs,
/// standing) and Storage (the roof chest). Opening a campfire, workbench or forge shows the Craft tab with
/// that station active. Time stops while the window is open.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public enum Tab { Pack, Craft, Character, Storage }

    static Tab tab = Tab.Pack;
    static string category = "All";
    static Recipe picked;
    Vector2 scrollA, scrollB;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { tab = Tab.Pack; category = "All"; picked = null; }

    public static void Show(Tab t)
    {
        tab = t;
        GameState.UiOpen = true;
        Sfx.Play("click");
    }

    void Update()
    {
        if (!GameState.Playing || GameState.MenuOpen) return;
        if (Inp.Inventory())
        {
            if (GameState.UiOpen) { GameState.UiOpen = false; CraftStation.Open = null; }
            else { CraftStation.Open = null; Show(tab == Tab.Storage ? Tab.Pack : tab); }
        }
        if (!GameState.UiOpen) CraftStation.Open = null;
    }

    void OnGUI()
    {
        if (!GameState.UiOpen || GameState.MenuOpen) return;
        Skin.Ensure();
        Skin.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

        float w = Mathf.Min(1060f, Screen.width - 40f), h = Mathf.Min(640f, Screen.height - 40f);
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        Skin.Box(r, Skin.Bg);
        Skin.Fill(new Rect(r.x + 1, r.y + 1, w - 2, 3), Skin.Accent);

        // ---- tabs
        float tx = r.x + 16, ty = r.y + 14;
        bool chest = Items.StashInReach;
        Tab[] tabs = chest ? new[] { Tab.Pack, Tab.Craft, Tab.Character, Tab.Storage } : new[] { Tab.Pack, Tab.Craft, Tab.Character };
        if (tab == Tab.Storage && !chest) tab = Tab.Pack;
        foreach (var t in tabs)
        {
            string label = t == Tab.Character && Progress.Points > 0 ? "Character  (" + Progress.Points + ")" : t.ToString();
            if (GUI.Button(new Rect(tx, ty, 150, 34), label, tab == t ? Skin.TabOn : Skin.Tab)) { tab = t; Sfx.Play("click"); }
            tx += 156;
        }
        GUI.Label(new Rect(r.xMax - 330, ty + 8, 314, 20), "Level " + Progress.Level + "      " + Keybinds.Describe(GameAction.Inventory) + " closes", RightAligned());

        // ---- weight, always in view
        float load = Items.Load;
        var wr = new Rect(r.x + 16, r.yMax - 16, w - 32, 8);
        Skin.Bar(wr, load / 1.3f, load > 1f ? Skin.Bad : load > 0.8f ? Skin.Accent : Skin.Good);
        var oldc = GUI.color; if (load > 1f) GUI.color = Skin.Bad;
        GUI.Label(new Rect(wr.x, wr.y - 24, wr.width, 22),
                  "Carrying " + Items.CarriedWeight.ToString("0.0") + " / " + Items.Capacity.ToString("0") + " kg   (" + Items.PackName + ")"
                  + (load > 1.3f ? "    OVERLOADED: you can barely move" : load > 1f ? "    Too heavy: slowed, no sprinting" : ""), Skin.Body);
        GUI.color = oldc;

        var body = new Rect(r.x + 16, r.y + 60, w - 32, h - 112);
        switch (tab)
        {
            case Tab.Pack: DrawPack(body); break;
            case Tab.Craft: DrawCraft(body); break;
            case Tab.Character: DrawCharacter(body); break;
            case Tab.Storage: DrawStorage(body); break;
        }
    }

    static GUIStyle right;
    static GUIStyle RightAligned()
    {
        if (right == null) right = new GUIStyle(Skin.DimText) { alignment = TextAnchor.MiddleRight, wordWrap = false };
        return right;
    }

    // ------------------------------------------------------------------ pack

    void DrawPack(Rect a)
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        GUILayout.BeginArea(a);
        scrollA = GUILayout.BeginScrollView(scrollA);
        string lastCat = "";
        bool any = false;
        foreach (var d in Items.Defs)
        {
            int n = Items.Count(d.id);
            if (n <= 0) continue;
            any = true;
            if (d.category != lastCat) { lastCat = d.category; GUILayout.Space(6); GUILayout.Label(d.category.ToUpper(), Skin.H2); }

            GUILayout.BeginHorizontal(GUILayout.Height(30));
            var sw = GUILayoutUtility.GetRect(6, 26, GUILayout.Width(6));
            Skin.Fill(sw, Skin.CategoryColor(d.category));
            GUILayout.Space(8);
            GUILayout.Label(d.name + "  x" + n, Skin.Body, GUILayout.Width(190));
            GUILayout.Label((d.weight * n).ToString("0.0") + " kg", Skin.DimText, GUILayout.Width(70));
            var wp = Items.Weapon(d.id);
            GUILayout.Label(wp != null ? d.desc + "  Damage " + wp.damage.ToString("F0") + ", reach " + wp.reach.ToString("0.0") + " m." : d.desc, Skin.DimText);
            if (wp != null && run != null)
            {
                bool on = run.equipped == d.id;
                if (GUILayout.Button(on ? "In hand" : "Equip", on ? Skin.TabOn : Skin.Button, GUILayout.Width(96), GUILayout.Height(26)) && !on) Items.Equip(d.id);
            }
            if (Items.Usable(d.id) && GUILayout.Button("Use", Skin.Button, GUILayout.Width(70), GUILayout.Height(26))) Hud.Toast(Items.Consume(d.id));
            if (GUILayout.Button("Drop", Skin.Button, GUILayout.Width(70), GUILayout.Height(26))) Drop(d.id);
            GUILayout.EndHorizontal();
        }
        if (!any) GUILayout.Label("Your hands and pockets are empty.", Skin.Body);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    /// <summary>Drops one stack's worth (up to 5) at your feet as a sack you can pick back up.</summary>
    static void Drop(string id)
    {
        var p = PlayerController.Instance;
        int n = Mathf.Min(5, Items.Count(id));
        if (p == null || n <= 0) return;
        Items.Remove(id, n);
        Pickup.Create("loot-sack", id, n, null, p.transform.position + p.transform.forward * 1.1f + Vector3.up * 0.05f, 0.5f);
        Sfx.Play("place", 0.5f);
    }

    // ------------------------------------------------------------------ craft

    void DrawCraft(Rect a)
    {
        var station = CraftStation.Open;
        string here = station != null ? station.kind : "";

        // Where you are, and what that means.
        GUI.Label(new Rect(a.x, a.y - 4, 600, 34), station != null ? "At the " + station.Label.ToLower() : "By hand", Skin.H1);
        GUI.Label(new Rect(a.x + 260, a.y + 6, a.width - 260, 20),
                  station != null ? (station.Busy ? "An order is under way here. Close this and hold " + Keybinds.Describe(GameAction.Interact) + " to finish it."
                                                  : "Choose a recipe, then hold " + Keybinds.Describe(GameAction.Interact) + " here to make it.")
                                  : "Hand recipes are made at once. Others need a campfire, workbench or forge on your roof.", Skin.DimText);

        // Categories down the left.
        float cx = a.x, cy = a.y + 36;
        foreach (string c in new[] { "All", Items.Mat, Items.Eat, Items.Tool, Items.Gear, Items.Base })
        {
            if (GUI.Button(new Rect(cx, cy, 170, 32), c, category == c ? Skin.TabOn : Skin.Tab)) { category = c; Sfx.Play("click"); }
            cy += 36;
        }

        // Recipe cards in the middle.
        var listRect = new Rect(a.x + 182, a.y + 36, a.width - 182 - 330, a.height - 36);
        GUILayout.BeginArea(listRect);
        scrollB = GUILayout.BeginScrollView(scrollB);
        // The station you are at comes first, then by hand, then the rest.
        var order = new System.Collections.Generic.List<string> { here };
        foreach (string s2 in new[] { "", "campfire", "workbench", "forge" }) if (!order.Contains(s2)) order.Add(s2);
        foreach (string st in order)
        {
            bool headed = false;
            foreach (var rc in Items.Recipes)
            {
                if (rc.station != st) continue;
                if (category != "All" && rc.category != category) continue;
                if (!headed)
                {
                    headed = true;
                    GUILayout.Space(4);
                    GUILayout.Label(rc.StationName.ToUpper() + (st == here ? "" : st == "" ? "" : "   (not here)"), Skin.H2);
                }
                bool unlocked = Items.Unlocked(rc), have = Items.HasInputs(rc);
                string mark = !unlocked ? "<color=#f07060>Level " + rc.level + "</color>" : have ? "<color=#80d880>Ready</color>" : "<color=#a0a4ac>Missing materials</color>";
                string label = "<b>" + Items.Name(rc.output) + (rc.count > 1 ? " x" + rc.count : "") + "</b>     " + mark + "\n<color=#a0a4ac>" + Inputs(rc) + "</color>";
                if (GUILayout.Button(label, picked == rc ? Skin.CardOn : Skin.Card, GUILayout.Height(46))) { picked = rc; Sfx.Play("click"); }
            }
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        // Details on the right.
        var d = new Rect(a.xMax - 318, a.y + 36, 318, a.height - 36);
        Skin.Box(d, Skin.Panel);
        if (picked == null) { GUI.Label(new Rect(d.x + 14, d.y + 14, d.width - 28, 60), "Pick a recipe to see what it needs.", Skin.DimText); return; }

        var def = Items.Def(picked.output);
        float y = d.y + 12;
        Skin.Fill(new Rect(d.x + 14, y + 4, 6, 24), Skin.CategoryColor(picked.category));
        GUI.Label(new Rect(d.x + 28, y, d.width - 40, 30), Items.Name(picked.output), Skin.H1); y += 34;
        GUI.Label(new Rect(d.x + 14, y, d.width - 28, 60), def != null ? def.desc : "", Skin.Body); y += 58;
        var wp = Items.Weapon(picked.output);
        if (wp != null) { GUI.Label(new Rect(d.x + 14, y, d.width - 28, 20), "Damage " + wp.damage.ToString("F0") + "   Reach " + wp.reach.ToString("0.0") + " m   Speed x" + wp.speed.ToString("0.0#"), Skin.DimText); y += 22; }
        if (def != null) { GUI.Label(new Rect(d.x + 14, y, d.width - 28, 20), "Weighs " + def.weight.ToString("0.0") + " kg", Skin.DimText); y += 26; }

        GUI.Label(new Rect(d.x + 14, y, d.width - 28, 20), "NEEDS", Skin.H2); y += 24;
        foreach (var inp in picked.inputs)
        {
            var p = inp.Split(':');
            int need = int.Parse(p[1]), got = Items.Have(p[0]);
            GUI.Label(new Rect(d.x + 14, y, d.width - 28, 20),
                      "<color=" + (got >= need ? "#80d880" : "#f07060") + ">" + got + " / " + need + "</color>   " + Items.Name(p[0]), Skin.Body);
            y += 22;
        }
        y += 6;
        GUI.Label(new Rect(d.x + 14, y, d.width - 28, 20), "Made: " + picked.StationName + (picked.workSeconds > 0f ? ", " + picked.workSeconds.ToString("0.#") + " s of work each" : ", at once"), Skin.DimText); y += 22;
        GUI.Label(new Rect(d.x + 14, y, d.width - 28, 20), "Unlocks at level " + picked.level, Items.Unlocked(picked) ? Skin.DimText : Skin.Body); y += 30;

        var b = new Rect(d.x + 14, d.yMax - 54, d.width - 28, 40);
        if (!Items.Unlocked(picked)) GUI.Label(b, "Reach level " + picked.level + " to make this.", Skin.Centre);
        else if (picked.station == "")
        {
            GUI.enabled = Items.HasInputs(picked);
            if (GUI.Button(b, "Make", Skin.ButtonAccent)) Items.CraftByHand(picked);
            GUI.enabled = true;
        }
        else if (station == null || station.kind != picked.station)
            GUI.Label(b, "Go to a " + picked.StationName.ToLower() + " to make this.", Skin.Centre);
        else if (station.Busy) GUI.Label(b, "The " + station.Label.ToLower() + " is busy.", Skin.Centre);
        else
        {
            int most = station.Affordable(picked);
            GUI.enabled = most >= 1;
            if (GUI.Button(new Rect(b.x, b.y, b.width * 0.48f, b.height), "Make 1", Skin.ButtonAccent) && station.PlaceOrder(picked, 1)) Close();
            GUI.enabled = most >= 2;
            if (GUI.Button(new Rect(b.x + b.width * 0.52f, b.y, b.width * 0.48f, b.height), "Make " + Mathf.Min(most, 10), Skin.ButtonAccent) && station.PlaceOrder(picked, Mathf.Min(most, 10))) Close();
            GUI.enabled = true;
        }
    }

    static void Close() { GameState.UiOpen = false; CraftStation.Open = null; }

    static string Inputs(Recipe r)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var inp in r.inputs)
        {
            var p = inp.Split(':');
            if (sb.Length > 0) sb.Append("  +  ");
            sb.Append(p[1]).Append(' ').Append(Items.Name(p[0]));
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------ character

    void DrawCharacter(Rect a)
    {
        var p = PlayerController.Instance;
        var needs = Needs.Instance;
        float x = a.x, y = a.y;

        GUI.Label(new Rect(x, y, 500, 30), Characters.Current.name + "   -   Level " + Progress.Level, Skin.H1); y += 36;
        Skin.Bar(new Rect(x, y, 420, 16), Progress.Fraction, new Color(0.45f, 0.7f, 1f));
        GUI.Label(new Rect(x + 430, y - 2, 300, 20), Progress.Xp + " / " + Progress.Needed(Progress.Level + 1) + " experience", Skin.DimText); y += 30;

        GUI.Label(new Rect(x, y, 500, 24), Progress.Points > 0 ? "POINTS TO SPEND: " + Progress.Points : "STATS  (one point each level)", Skin.H2); y += 30;
        for (int i = 0; i < Progress.StatIds.Length; i++)
        {
            var row = new Rect(x, y, 520, 44);
            Skin.Box(row, Skin.Panel);
            GUI.Label(new Rect(x + 12, y + 4, 200, 20), Progress.StatNames[i] + "   " + Progress.Stat(Progress.StatIds[i]), Skin.Body);
            GUI.Label(new Rect(x + 12, y + 22, 400, 20), Progress.StatHelp[i], Skin.Small);
            GUI.enabled = Progress.Points > 0;
            if (GUI.Button(new Rect(x + 440, y + 7, 68, 30), "+ 1", Skin.ButtonAccent)) Progress.Spend(Progress.StatIds[i]);
            GUI.enabled = true;
            y += 50;
        }

        // Right column: where you stand.
        float rx = a.x + 560, ry = a.y;
        GUI.Label(new Rect(rx, ry, 400, 24), "CONDITION", Skin.H2); ry += 28;
        if (p != null) { Row(ref ry, rx, "Health", p.health / p.maxHealth, Mathf.CeilToInt(p.health) + " / " + p.maxHealth.ToString("F0"), Skin.Good); }
        if (needs != null)
        {
            Row(ref ry, rx, "Hunger", needs.Hunger / 100f, Mathf.RoundToInt(needs.Hunger) + "%", new Color(0.9f, 0.65f, 0.3f));
            Row(ref ry, rx, "Thirst", needs.Thirst / 100f, Mathf.RoundToInt(needs.Thirst) + "%", new Color(0.4f, 0.7f, 0.95f));
            Row(ref ry, rx, "Stamina", needs.Stamina / needs.MaxStamina, Mathf.RoundToInt(needs.Stamina) + " / " + needs.MaxStamina.ToString("F0"), new Color(0.85f, 0.85f, 0.4f));
        }
        ry += 10;
        GUI.Label(new Rect(rx, ry, 400, 24), "YOU", Skin.H2); ry += 28;
        GUI.Label(new Rect(rx, ry, 440, 20), "Melee damage  x" + (Characters.Current.damageMult * Progress.DamageMult).ToString("0.00"), Skin.Body); ry += 22;
        GUI.Label(new Rect(rx, ry, 440, 20), "Carry limit  " + Items.Capacity.ToString("0") + " kg   (" + Items.PackName + ")", Skin.Body); ry += 22;
        GUI.Label(new Rect(rx, ry, 440, 20), "Armour  " + Items.ArmorName + (Items.Armor > 0f ? "  (-" + Mathf.RoundToInt(Items.Armor * 100f) + "% damage)" : ""), Skin.Body); ry += 22;
        GUI.Label(new Rect(rx, ry, 440, 20), "Base defense  " + BaseStructure.TotalDefense().ToString("F0"), Skin.Body); ry += 32;

        GUI.Label(new Rect(rx, ry, 400, 24), "THE DRIFTERS", Skin.H2); ry += 28;
        var old = GUI.color; GUI.color = Faction.StandingColor;
        GUI.Label(new Rect(rx, ry - 2, 440, 34), Faction.Standing + "   (" + Faction.Reputation + ")", Skin.H1); GUI.color = old; ry += 34;
        Skin.Bar(new Rect(rx, ry, 420, 12), (Faction.Reputation + 100) / 200f, Faction.StandingColor); ry += 20;
        GUI.Label(new Rect(rx, ry, 440, 40), "Help them when they are attacked and share food to earn trust. Hit or rob them and they come for you.", Skin.DimText);
    }

    static void Row(ref float y, float x, string name, float frac, string value, Color c)
    {
        GUI.Label(new Rect(x, y, 90, 20), name, Skin.Body);
        Skin.Bar(new Rect(x + 90, y + 3, 250, 14), frac, c);
        GUI.Label(new Rect(x + 350, y, 120, 20), value, Skin.DimText);
        y += 24;
    }

    // ------------------------------------------------------------------ storage

    void DrawStorage(Rect a)
    {
        float half = (a.width - 20) / 2f;
        GUI.Label(new Rect(a.x, a.y, half, 26), "YOUR PACK", Skin.H2);
        GUI.Label(new Rect(a.x + half + 20, a.y, half - 200, 26), "ROOF STORAGE", Skin.H2);
        if (GUI.Button(new Rect(a.xMax - 220, a.y - 4, 220, 30), "Store all materials", Skin.Button)) Items.StoreAllMaterials();

        var left = new Rect(a.x, a.y + 32, half, a.height - 32);
        var rightR = new Rect(a.x + half + 20, a.y + 32, half, a.height - 32);
        Skin.Box(left, Skin.Panel);
        Skin.Box(rightR, Skin.Panel);

        GUILayout.BeginArea(new Rect(left.x + 8, left.y + 6, left.width - 16, left.height - 12));
        scrollA = GUILayout.BeginScrollView(scrollA);
        foreach (var d in Items.Defs)
        {
            int n = Items.Count(d.id);
            if (n <= 0) continue;
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            GUILayout.Label(d.name + "  x" + n, Skin.Body);
            if (GUILayout.Button("Store 1", Skin.Button, GUILayout.Width(84), GUILayout.Height(26))) { Items.Store(d.id, 1); Sfx.Play("click"); }
            if (GUILayout.Button("All", Skin.Button, GUILayout.Width(60), GUILayout.Height(26))) { Items.Store(d.id, n); Sfx.Play("click"); }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(rightR.x + 8, rightR.y + 6, rightR.width - 16, rightR.height - 12));
        scrollB = GUILayout.BeginScrollView(scrollB);
        bool any = false;
        foreach (var d in Items.Defs)
        {
            int n = Items.Stashed(d.id);
            if (n <= 0) continue;
            any = true;
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            GUILayout.Label(d.name + "  x" + n, Skin.Body);
            if (GUILayout.Button("Take 1", Skin.Button, GUILayout.Width(84), GUILayout.Height(26))) { Items.Store(d.id, -1); Sfx.Play("click"); }
            if (GUILayout.Button("All", Skin.Button, GUILayout.Width(60), GUILayout.Height(26))) { Items.Store(d.id, -n); Sfx.Play("click"); }
            GUILayout.EndHorizontal();
        }
        if (!any) GUILayout.Label("Empty. What is stored here does not weigh you down, and the stations on your roof can use it.", Skin.DimText);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
