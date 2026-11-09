using UnityEngine;

/// <summary>
/// A planter. Plant -> water daily -> grows one stage per dawn (only if watered) -> harvest food.
/// With the Polyfork trough model, the "planting" child node is scaled up as the crop grows.
/// </summary>
public class FarmPlot : Interactable
{
    public int growDays = 3;
    public int yield = 4;
    [Tooltip("Optional child (e.g. the model's 'planting' node) that scales up as it grows.")]
    public Transform cropVisual;

    Vector3 baseScale = Vector3.one;

    public int Stage { get; private set; } = -1;   // -1 = empty
    public bool Watered { get; private set; }
    public bool Ready => Stage >= growDays;

    protected override void OnEnable() { base.OnEnable(); range = 3.4f; GameClock.OnDawn += HandleDawn; UpdateVisual(); }
    protected override void OnDisable() { base.OnDisable(); GameClock.OnDawn -= HandleDawn; }

    public override string Prompt
    {
        get
        {
            if (Stage == -1) return "Plant seeds";
            if (Ready) return "Harvest crop (+" + (yield + Characters.Current.planterBonus) + " Food)";
            if (Watered) return "";
            return "Water crop (1 Water) - day " + (Stage + 1) + " of " + growDays;
        }
    }

    public override void Use()
    {
        if (Stage == -1)
        {
            Plant();
            Hud.Toast("Planted. Water it each day; it grows at dawn.");
            Tutorial.Notify("plant");
        }
        else if (Ready)
        {
            int bonus = Characters.Current.planterBonus;
            int got = Harvest() + bonus;
            if (bonus > 0) Items.Add("food", bonus);
            Hud.Toast("Harvested +" + got + " Food.");
        }
        else if (Watered) Hud.Toast("Already watered today.");
        else if (Water())
        {
            Hud.Toast("Watered.");
            Tutorial.Notify("water");
        }
        else Hud.Toast("No water. Collect some at the water tower.");
    }

    /// <summary>Used when a saved run is loaded.</summary>
    public void SetState(int stage, bool watered)
    {
        Stage = Mathf.Clamp(stage, -1, growDays);
        Watered = watered;
        UpdateVisual();
    }

    public void SetCropVisual(Transform t)
    {
        cropVisual = t;
        baseScale = t != null ? t.localScale : Vector3.one;
        UpdateVisual();
    }

    void HandleDawn(int day)
    {
        if (Stage >= 0 && Stage < growDays && Watered) Stage++;
        Watered = false;
        UpdateVisual();
    }

    public bool Plant()
    {
        if (Stage != -1) return false;
        Stage = 0;
        UpdateVisual();
        return true;
    }

    public bool Water()
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        if (Stage < 0 || Ready || Watered || run == null || run.water <= 0) return false;
        run.water--;
        Watered = true;
        return true;
    }

    public int Harvest()
    {
        if (!Ready) return 0;
        Stage = -1;
        UpdateVisual();
        Items.Add("food", yield);
        return yield;
    }

    public void UpdateVisual()
    {
        if (cropVisual == null) return;
        cropVisual.localScale = Stage < 0
            ? Vector3.zero
            : baseScale * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(Stage / (float)growDays));
    }
}
