using UnityEngine;

/// <summary>The inventory and crafting panel (Inventory key, or the workbench). Time stops while it is open.</summary>
public class InventoryUI : MonoBehaviour
{
    GUIStyle head, body, dim, btn;
    Vector2 scrollLeft, scrollRight;

    void Update()
    {
        if (!GameState.Playing || GameState.MenuOpen) return;
        if (Inp.Inventory()) GameState.UiOpen = !GameState.UiOpen;
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

        float w = Mathf.Min(860f, Screen.width - 40f), h = Mathf.Min(560f, Screen.height - 40f);
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.color = new Color(0.07f, 0.08f, 0.09f, 0.96f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(r.x + 18, r.y + 14, w - 36, h - 28));
        GUILayout.BeginHorizontal();
        GUILayout.Label("INVENTORY AND CRAFTING", head);
        GUILayout.FlexibleSpace();
        GUILayout.Label(Keybinds.Describe(GameAction.Inventory) + " to close", dim);
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        GUILayout.BeginHorizontal();

        // ---- what you carry
        GUILayout.BeginVertical(GUILayout.Width(w * 0.44f));
        GUILayout.Label("Carrying", head);
        scrollLeft = GUILayout.BeginScrollView(scrollLeft);
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        foreach (var d in Items.Defs)
        {
            int n = Items.Count(d.id);
            if (n <= 0) continue;
            var wp = Items.Weapon(d.id);
            GUILayout.BeginHorizontal();
            GUILayout.Label(d.name + "  x" + n, body, GUILayout.Width(150));
            if (wp != null && run != null)
            {
                bool on = run.equipped == d.id;
                if (GUILayout.Button(on ? "In hand" : "Equip", GUILayout.Width(80)) && !on)
                {
                    Items.Equip(d.id);
                }
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
        GUILayout.BeginHorizontal();
        GUILayout.Label("Crafting", head);
        GUILayout.FlexibleSpace();
        GUI.color = Workbench.PlayerNear ? new Color(0.6f, 0.95f, 0.6f) : new Color(1f, 0.75f, 0.5f);
        GUILayout.Label(Workbench.PlayerNear ? "At the workbench" : "Not at the workbench", body);
        GUI.color = Color.white;
        GUILayout.EndHorizontal();
        scrollRight = GUILayout.BeginScrollView(scrollRight);
        foreach (var rc in Items.Recipes)
        {
            bool has = Items.HasInputs(rc);
            bool benchOk = !rc.needsBench || Workbench.PlayerNear;
            GUI.enabled = has && benchOk;
            string label = Items.Def(rc.output).name + (rc.count > 1 ? " x" + rc.count : "") + "\n" + rc.Describe()
                         + (rc.needsBench ? (benchOk ? "   (workbench)" : "   NEEDS THE WORKBENCH") : "");
            if (GUILayout.Button(label, btn, GUILayout.Height(46))) Items.Craft(rc);
            GUI.enabled = true;
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
