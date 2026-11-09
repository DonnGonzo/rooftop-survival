using UnityEngine;

/// <summary>Player options, saved between sessions with PlayerPrefs.</summary>
public static class GameSettings
{
    public static float Sensitivity
    {
        get => PlayerPrefs.GetFloat("set_sens", 1.2f);
        set => PlayerPrefs.SetFloat("set_sens", value);
    }

    public static float Volume
    {
        get => PlayerPrefs.GetFloat("set_vol", 1f);
        set
        {
            PlayerPrefs.SetFloat("set_vol", value);
            AudioListener.volume = value;
        }
    }

    public static bool InvertY
    {
        get => PlayerPrefs.GetInt("set_invy", 0) == 1;
        set => PlayerPrefs.SetInt("set_invy", value ? 1 : 0);
    }

    public static bool ShowTutorial
    {
        get => PlayerPrefs.GetInt("set_tut", 1) == 1;
        set => PlayerPrefs.SetInt("set_tut", value ? 1 : 0);
    }

    public static int CharacterIndex
    {
        get => PlayerPrefs.GetInt("set_char", 0);
        set => PlayerPrefs.SetInt("set_char", value);
    }

    public static void ApplyVolume() { AudioListener.volume = Volume; }
}
