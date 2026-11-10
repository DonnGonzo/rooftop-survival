using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class PlacedStructure
{
    public string id;
    public Vector3 pos;
    public float yRot;
    public float health;
    public int stage = -1;    // planters: crop stage (-1 = empty)
    public bool watered;      // planters: watered today
    public string order = ""; // stations: what is being built
    public float orderDone;   // stations: seconds of work done on it
    public int orderCount = 1;// stations: how many are being made
}

[Serializable]
public class RunData
{
    public int seed;
    public int day = 1;
    public float hour = 7f;
    public int food, water, wood, scrap;
    public float roofQuality;               // 0 (terrible) .. 1 (great)
    public string equipped = "";            // weapon in hand ("" = fists)
    public int xp;                          // experience; the level is worked out from it
    public int statVitality, statStrength, statCarry, statEndurance;   // points spent
    public float hunger = 100f, thirst = 100f;
    public int reputation;                  // standing with the Drifters, -100..100
    public List<ItemStack> stash = new List<ItemStack>();   // what is stored in the roof chests
    public int waterDay;                    // last day the water tower was emptied
    public bool hungry;                     // missed food or water at the last dawn
    public List<int> looted = new List<int>();   // roof supply caches already taken
    public List<ItemStack> items = new List<ItemStack>();   // crafted and found items (not food/water/wood/scrap)
    public List<PlacedStructure> structures = new List<PlacedStructure>();
}

/// <summary>
/// Owns the current run. Saves to disk; if the base is overrun the save is wiped
/// and a brand-new random roof (new seed) is generated.
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }
    public RunData Current { get; private set; }

    string SavePath => Path.Combine(Application.persistentDataPath, "run.json");
    public bool HasSave => File.Exists(SavePath);

    // Not DontDestroyOnLoad: each scene load rebuilds the managers and reloads the run from disk,
    // which keeps a single instance and avoids two RunManagers fighting after a reload.
    void Awake()
    {
        Instance = this;
        LoadOrStart();
    }

    public void LoadOrStart()
    {
        if (File.Exists(SavePath))
        {
            try { Current = JsonUtility.FromJson<RunData>(File.ReadAllText(SavePath)); }
            catch (Exception e) { Debug.LogWarning("Save unreadable, starting fresh: " + e.Message); }
        }
        if (Current == null) StartNewRun();
        if (Current.equipped == null) Current.equipped = "";
        if (Current.looted == null) Current.looted = new List<int>();
        if (Current.stash == null) Current.stash = new List<ItemStack>();
    }

    public void StartNewRun()
    {
        Current = new RunData { seed = Guid.NewGuid().GetHashCode() };
        RoofGenerator.ApplyStartingResources(Current);
        Save(false);
    }

    /// <summary>keepWorld = false writes a brand-new run without copying the clock or the structures of the old one.</summary>
    public void Save(bool keepWorld = true)
    {
        if (Current == null) return;
        if (keepWorld && GameClock.Instance != null)
        {
            Current.day = GameClock.Instance.Day;
            Current.hour = GameClock.Instance.Hour;
        }
        // Structures exist only once the roof has been built; before that, keep what the file had.
        if (keepWorld && RoofGenerator.Built)
        {
            Current.structures.Clear();
            foreach (var s in BaseStructure.All)
            {
                if (s == null || (s.hp != null && s.hp.Dead)) continue;
                var plot = s.GetComponent<FarmPlot>();
                var station = s.GetComponent<CraftStation>();
                var rain = s.GetComponent<RainCollector>();
                Current.structures.Add(new PlacedStructure
                {
                    id = s.id,
                    pos = s.transform.position,
                    yRot = s.transform.eulerAngles.y,
                    health = s.hp != null ? s.hp.current : 0f,
                    stage = plot != null ? plot.Stage : rain != null ? rain.stored : -1,
                    watered = plot != null && plot.Watered,
                    order = station != null ? station.orderId : "",
                    orderDone = station != null ? station.done : 0f,
                    orderCount = station != null ? station.orderCount : 1
                });
            }
        }
        try { File.WriteAllText(SavePath, JsonUtility.ToJson(Current)); }
        catch (Exception e) { Debug.LogWarning("Could not save the run: " + e.Message); }
    }

    /// <summary>Base overrun: wipe everything, roll a new random roof, reload.</summary>
    public void LoseRun(int nightsSurvived)
    {
        int best = Mathf.Max(PlayerPrefs.GetInt("BestNight", 0), nightsSurvived);
        PlayerPrefs.SetInt("BestNight", best);
        PlayerPrefs.Save();

        if (File.Exists(SavePath)) File.Delete(SavePath);
        Current = null;
        StartNewRun();
        GameState.AutoStart = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnApplicationQuit() { Save(); }
}
