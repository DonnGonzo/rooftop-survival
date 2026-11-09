using UnityEngine;

/// <summary>
/// Daily needs. Each dawn you eat 1 Food and drink 1 Water. Miss either and you start the day hungry:
/// you lose health and the roof no longer heals you until you have eaten and drunk at a dawn again.
/// </summary>
public class Survival : MonoBehaviour
{
    public float hungerDamage = 25f;

    void OnEnable() { GameClock.OnDawn += Dawn; }
    void OnDisable() { GameClock.OnDawn -= Dawn; }

    void Dawn(int day)
    {
        var run = RunManager.Instance != null ? RunManager.Instance.Current : null;
        var p = PlayerController.Instance;
        if (run == null || p == null) return;

        bool ate = Items.Remove("food", 1);
        bool drank = Items.Remove("water", 1);
        run.hungry = !(ate && drank);

        if (!run.hungry)
        {
            Hud.Banner("Day " + day + ". You ate and drank (-1 Food, -1 Water).", 5f);
        }
        else
        {
            string missing = !ate && !drank ? "food or water" : !ate ? "food" : "water";
            p.health = Mathf.Max(5f, p.health - hungerDamage);
            Hud.Banner("Day " + day + ". No " + missing + ": you are weak and will not heal today.", 7f);
        }
        RunManager.Instance.Save();
    }
}
