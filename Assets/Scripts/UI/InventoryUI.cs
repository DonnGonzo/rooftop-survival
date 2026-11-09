using UnityEngine;

/// <summary>Tab opens the inventory and crafting panel. Time stops while it is open.</summary>
public class InventoryUI : MonoBehaviour
{
    GUIStyle head, body;

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
            head.normal.textColor = Color.white;
            body.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        }

        float w = 680f, h = 460f;
        var r = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
        GUI.color = new Color(0.05f, 0.05f, 0.06f, 0.92f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(r.x + 16, r.y + 12, w - 32, h - 24));
        GUILayout.Label("INVENTORY & CRAFTING   (Tab to close)", head);
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(300));
        GUILayout.Label("Carrying", head);
        bool any = false;
        foreach (var d in Items.Defs)
        {
            int n = Items.Count(d.id);
            if (n <= 0) continue;
            any = true;
            GUILayout.Label(d.name + "  x" + n, body);
            GUILayout.Label(d.desc, body);
            GUILayout.Space(4);
        }
        if (!any) GUILayout.Label("Nothing yet. Plant crops, rappel down, or find supply crates.", body);
        GUILayout.EndVertical();

        GUILayout.BeginVertical();
        GUILayout.Label("Crafting", head);
        foreach (var rc in Items.Recipes)
        {
            GUI.enabled = Items.CanCraft(rc);
            string label = Items.Def(rc.output).name + (rc.count > 1 ? " x" + rc.count : "") + "\n" + rc.Describe();
            if (GUILayout.Button(label, GUILayout.Height(44))) Items.Craft(rc);
            GUI.enabled = true;
        }
        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
