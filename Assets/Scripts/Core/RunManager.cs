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
}

[Serializable]
public class RunData
{
    public int seed;
    public int day = 1;
    public float hour = 7f;
    public int food, water, wood, scrap;
    public float roofQuality;               // 0 (terrible) .. 1 (great)
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

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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
    }

    public void StartNewRun()
    {
        Current = new RunData { seed = Guid.NewGuid().GetHashCode() };
        RoofGenerator.ApplyStartingResources(Current);
        Save();
    }

    public void Save()
    {
        if (Current == null) return;
        if (GameClock.Instance != null)
        {
            Current.day = GameClock.Instance.Day;
            Current.hour = GameClock.Instance.Hour;
        }
        Current.structures.Clear();
        foreach (var s in BaseStructure.All)
        {
            Current.structures.Add(new PlacedStructure
            {
                id = s.id,
                pos = s.transform.position,
                yRot = s.transform.eulerAngles.y,
                health = s.health
            });
        }
        File.WriteAllText(SavePath, JsonUtility.ToJson(Current));
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void OnApplicationQuit() { Save(); }
}
