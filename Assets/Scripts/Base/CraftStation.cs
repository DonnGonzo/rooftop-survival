using UnityEngine;

/// <summary>
/// A campfire, workbench or forge. Tap Interact to open its recipe list and place an order (the materials
/// are taken then, from your pack and from the roof stash). After that, hold Interact at the station to
/// build it; the bar shows how far along it is. The order and its progress are saved with the run.
/// </summary>
public class CraftStation : Interactable
{
    public string kind = "workbench";   // matches Recipe.station
    public string orderId = "";         // output id of the recipe being built ("" = idle)
    public int orderCount = 1;          // how many of it
    public float done;                  // seconds of work put in so far

    /// <summary>The station whose recipe list is open, or null.</summary>
    public static CraftStation Open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Open = null; }

    public string Label => kind == "forge" ? "Forge" : kind == "campfire" ? "Campfire" : "Workbench";

    Recipe Order
    {
        get
        {
            if (string.IsNullOrEmpty(orderId)) return null;
            foreach (var r in Items.Recipes) if (r.output == orderId && r.station == kind) return r;
            return null;
        }
    }

    float Total { get { var r = Order; return r == null ? 1f : Mathf.Max(0.1f, r.workSeconds * Mathf.Max(1, orderCount)); } }

    public override string Prompt
    {
        get
        {
            var r = Order;
            if (r == null) return kind == "campfire" ? "Cook at the campfire" : "Open " + Label.ToLower();
            string verb = kind == "campfire" ? "cook " : "build ";
            return "Hold to " + verb + Items.Name(r.output) + (orderCount > 1 ? " x" + orderCount : "") + "  (" + Mathf.RoundToInt(WorkProgress * 100f) + "%)";
        }
    }

    public override bool TakesWork => Order != null;
    public override float WorkProgress => Order == null ? 0f : Mathf.Clamp01(done / Total);

    public override void Use()
    {
        Open = this;
        InventoryUI.Show(InventoryUI.Tab.Craft);
    }

    float nextSound;

    public override void Work(float seconds)
    {
        var r = Order;
        if (r == null) return;
        done += seconds;
        if (Time.time > nextSound)
        {
            nextSound = Time.time + 0.55f;
            Sfx.PlayAt(kind == "campfire" ? "sizzle" : kind == "forge" ? "metal" : "craft", transform.position + Vector3.up, 0.7f, 20f);
        }
        if (done < Total) return;
        int n = Mathf.Max(1, orderCount);
        orderId = "";
        done = 0f;
        orderCount = 1;
        for (int i = 0; i < n; i++) Items.Finish(r);
        if (RunManager.Instance != null) RunManager.Instance.Save();
    }

    /// <summary>How many of a recipe the materials in reach would cover (0 if locked or the station is busy).</summary>
    public int Affordable(Recipe r)
    {
        if (Order != null || r.station != kind || !Items.Unlocked(r)) return 0;
        int most = 99;
        foreach (var inp in r.inputs)
        {
            var p = inp.Split(':');
            most = Mathf.Min(most, Items.Have(p[0]) / int.Parse(p[1]));
        }
        return most;
    }

    /// <summary>Places an order for count of a recipe. False if busy, locked, or materials are missing.</summary>
    public bool PlaceOrder(Recipe r, int count = 1)
    {
        count = Mathf.Clamp(count, 1, Affordable(r));
        if (count < 1) return false;
        for (int i = 0; i < count; i++) Items.PayFor(r);
        orderId = r.output;
        orderCount = count;
        done = 0f;
        Sfx.Play("click");
        Hud.Toast("Hold " + Keybinds.Describe(GameAction.Interact) + " at the " + Label.ToLower() + " to " + (kind == "campfire" ? "cook." : "build it."));
        return true;
    }

    public bool Busy => Order != null;
}

/// <summary>Catches rain: 2 clean water each dawn, up to 6 held. Interact to take it.</summary>
public class RainCollector : Interactable
{
    public int stored;
    const int Cap = 6;

    protected override void OnEnable() { base.OnEnable(); GameClock.OnDawn += Dawn; }
    protected override void OnDisable() { base.OnDisable(); GameClock.OnDawn -= Dawn; }

    void Dawn(int day) { stored = Mathf.Min(Cap, stored + 2); }

    public override string Prompt => stored > 0 ? "Collect water (+" + stored + ")" : "Rain collector: empty until dawn";

    public override void Use()
    {
        if (stored <= 0) { Hud.Toast("It fills a little each dawn."); return; }
        Items.Add("water", stored);
        Hud.Toast("+" + stored + " Clean water");
        Sfx.Play("drink");
        stored = 0;
    }
}
