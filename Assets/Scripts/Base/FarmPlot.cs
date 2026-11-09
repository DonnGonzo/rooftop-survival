using UnityEngine;

/// <summary>
/// A planter. Plant -> water daily -> grows one stage per dawn (only if watered) -> harvest food.
/// Call Plant/Water/Harvest from your interaction system.
/// </summary>
public class FarmPlot : MonoBehaviour
{
    public int growDays = 3;
    public int yield = 4;
    [Tooltip("Optional child (e.g. a GLB crop model) that scales up as it grows.")]
    public Transform cropVisual;

    public int Stage { get; private set; } = -1;   // -1 = empty
    public bool Watered { get; private set; }
    public bool Ready => Stage >= growDays;

    void OnEnable() { GameClock.OnDawn += HandleDawn; UpdateVisual(); }
    void OnDisable() { GameClock.OnDawn -= HandleDawn; }

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
        if (RunManager.Instance != null) RunManager.Instance.Current.food += yield;
        return yield;
    }

    public void UpdateVisual()
    {
        if (cropVisual == null) return;
        cropVisual.localScale = Stage < 0
            ? Vector3.zero
            : Vector3.one * Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(Stage / (float)growDays));
    }
}
