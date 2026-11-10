using UnityEngine;

/// <summary>
/// The inventory and crafting panel. Opened with the Inventory key it lists what you carry and the recipes
/// made by hand. Opened from a workbench or forge it lists that station's recipes: choosing one places an
/// order, which you then build by holding Interact at the station. Time stops while the panel is open.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    GUIStyle head, body, dim, btn;
    Vector2 scrollLeft, scrollRight;

    void Update()
    {
        if (!GameState.Playing || GameState.MenuOpen) return;
        if (Inp.Inventory())
        {
            GameState.UiOpen = !GameState.UiOpen;
            CraftStation.Open = null;
        }
        if (!GameState.UiOpen) CraftStation.Open = null;
    }

    void OnGUI()
    {
        if (!GameState.UiOpen || GameState.MenuOpen) return;

        if (head == null)
        {
            head = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            dim = new GUIStyle(body);
            btn = new GUIStyle(GUI.skin.button) { fontSize = 14, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 8, 4, 4) };
            head.normal.textColor = Color.white;
            body.normal.textColor = new Color(0.92f, 0.92f, 0.92f);
            dim.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
        }

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        float w = Mathf.Min(900f, Screen.width - 40f), h = Mathf.Min(580f, Screen.height - 40f);
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.color = new Color(0.07f, 0.08f, 0.09f, 0.96f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var station = CraftStation.Open;
        string kind = station != null ? station.kind : "";

        GUILayout.BeginArea(new Rect(r.x + 18, r.y + 14, w - 36, h - 28));
        GUILayout.BeginHorizontal();
        GUILayout.Label(station != null ? station.Label.ToUpper() : "INVENTORY", head);
        GUILayout.FlexibleSpace();
        GUILayout.Label("Level " + Progress.Level + "     " + Keybinds.Describe(GameAction.Inventory) + " to close", dim);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();

        // ---- what you carry
        GUILayout.BeginVertical(GUILayout.Width(w * 0.42f));
        GUILayout.Label("Carrying", head);
        scrollLeft = GUILayout.BeginScrollView(scrollLeft);
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        foreach (var d in Items.Defs)
        {
            int n = Items.Count(d.id);
            if (n <= 0) continue;
            var wp = Items.Weapon(d.id);
            GUILayout.BeginHorizontal();
            GUILayout.Label(d.name + "  x" + n, body, GUILayout.Width(170));
            if (wp != null && run != null)
            {
                bool on = run.equipped == d.id;
                if (GUILayout.Button(on ? "In hand" : "Equip", GUILayout.Width(80)) && !on) Items.Equip(d.id);
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(wp != null ? d.desc + "  Damage " + wp.damage.ToString("F0") + ", reach " + wp.reach.ToString("0.0") + " m." : d.desc, dim);
            GUILayout.Space(6);
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.Space(16);

        // ---- recipes
        GUILayout.BeginVertical();
        GUILayout.Label(station != null ? "Build here" : "Make by hand", head);
        if (station != null && station.Busy)
            GUILayout.Label("An order is under way. Close this and hold " + Keybinds.Describe(GameAction.Interact) + " at the " + station.Label.ToLower() + " to finish it.", body);
        if (station == null)
            GUILayout.Label("Tools, weapons and traps are built at a workbench on your roof.", dim);

        scrollRight = GUILayout.BeginScrollView(scrollRight);
        foreach (var rc in Items.Recipes)
        {
            if (rc.station != kind) continue;
            bool unlocked = Items.Unlocked(rc);
            bool can = unlocked && Items.HasInputs(rc) && (station == null || !station.Busy);
            GUI.enabled = can;
            string label = Items.Name(rc.output) + (rc.count > 1 ? " x" + rc.count : "") + "\n"
                         + (unlocked ? rc.Describe() + (rc.workSeconds > 0f ? "     " + rc.workSeconds.ToString("F0") + " s of work" : "")
                                     : "Unlocks at level " + rc.level);
            if (GUILayout.Button(label, btn, GUILayout.Height(46)))
            {
                if (station == null) Items.CraftByHand(rc);
                else if (station.PlaceOrder(rc)) { GameState.UiOpen = false; CraftStation.Open = null; }
            }
            GUI.enabled = true;
        }

        // What the other stations offer, so the player can see what is ahead.
        foreach (string other in new[] { "", "workbench", "forge" })
        {
            if (other == kind) continue;
            GUILayout.Space(8);
            GUILayout.Label(other == "" ? "By hand (from the inventory)" : other == "workbench" ? "At a workbench" : "At a forge", dim);
            foreach (var rc in Items.Recipes)
                if (rc.station == other)
                    GUILayout.Label("   " + Items.Name(rc.output) + "  -  " + rc.Describe() + (Items.Unlocked(rc) ? "" : "   (level " + rc.level + ")"), dim);
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
