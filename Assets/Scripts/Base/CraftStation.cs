using UnityEngine;

/// <summary>
/// A workbench or forge on the roof. Tap Interact to open its recipe list and place an order (the materials
/// are taken then). After that, hold Interact at the station to build it; the bar shows how far along it is.
/// The order and its progress are saved with the run.
/// </summary>
public class CraftStation : Interactable
{
    public string kind = "workbench";   // matches Recipe.station
    public string orderId = "";         // output id of the recipe being built ("" = idle)
    public float done;                  // seconds of work put in so far

    /// <summary>The station whose recipe list is open, or null when the panel shows only hand recipes.</summary>
    public static CraftStation Open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Open = null; }

    public string Label => kind == "forge" ? "Forge" : "Workbench";

    Recipe Order
    {
        get
        {
            if (string.IsNullOrEmpty(orderId)) return null;
            foreach (var r in Items.Recipes) if (r.output == orderId && r.station == kind) return r;
            return null;
        }
    }

    public override string Prompt
    {
        get
        {
            var r = Order;
            if (r == null) return "Open " + Label.ToLower();
            return "Hold to build " + Items.Name(r.output) + "  (" + Mathf.RoundToInt(WorkProgress * 100f) + "%)";
        }
    }

    public override bool TakesWork => Order != null;
    public override float WorkProgress { get { var r = Order; return r == null ? 0f : Mathf.Clamp01(done / Mathf.Max(0.1f, r.workSeconds)); } }

    public override void Use()
    {
        Open = this;
        GameState.UiOpen = true;
    }

    public override void Work(float seconds)
    {
        var r = Order;
        if (r == null) return;
        done += seconds;
        if (done < r.workSeconds) return;
        orderId = "";
        done = 0f;
        Items.Finish(r);
        if (RunManager.Instance != null) RunManager.Instance.Save();
    }

    /// <summary>Places an order. False if one is already under way, it is locked, or materials are missing.</summary>
    public bool PlaceOrder(Recipe r)
    {
        if (Order != null || r.station != kind || !Items.Unlocked(r) || !Items.PayFor(r)) return false;
        orderId = r.output;
        done = 0f;
        Hud.Toast("Ordered: " + Items.Name(r.output) + ". Hold " + Keybinds.Describe(GameAction.Interact) + " at the " + Label.ToLower() + " to build it.");
        return true;
    }

    public bool Busy => Order != null;
}
