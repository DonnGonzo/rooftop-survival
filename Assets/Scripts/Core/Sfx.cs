using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays sounds by name. A sound called "chop" is looked for in Resources/Audio as chop, chop_2 and chop_3
/// (variations are picked at random). If no file exists, a simple synthesized stand-in is used for the
/// common effects, so the game is never silent; drop real files in and they take over.
/// Voices and music use the same folder (see MusicDirector).
/// </summary>
public static class Sfx
{
    static readonly Dictionary<string, AudioClip[]> cache = new Dictionary<string, AudioClip[]>();
    static AudioSource flat;   // 2D one-shots: interface sounds, your own blows

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); flat = null; }

    public static AudioClip[] Clips(string name)
    {
        if (cache.TryGetValue(name, out var found)) return found;
        var list = new List<AudioClip>();
        foreach (string n in new[] { name, name + "_2", name + "_3" })
        {
            var c = Resources.Load<AudioClip>("Audio/" + n);
            if (c != null) list.Add(c);
        }
        if (list.Count == 0)
        {
            var s = Synth.Make(name);
            if (s != null) list.Add(s);
        }
        var arr = list.ToArray();
        cache[name] = arr;
        return arr;
    }

    public static AudioClip Clip(string name)
    {
        var c = Clips(name);
        return c.Length == 0 ? null : c[Random.Range(0, c.Length)];
    }

    public static bool Has(string name) { return Clips(name).Length > 0; }

    /// <summary>Plays a sound with no position (it is "in your head": interface, your own actions).</summary>
    public static void Play(string name, float volume = 1f)
    {
        var clip = Clip(name);
        if (clip == null) return;
        if (flat == null)
        {
            var go = new GameObject("SfxFlat");
            flat = go.AddComponent<AudioSource>();
            flat.spatialBlend = 0f;
            flat.playOnAwake = false;
        }
        flat.pitch = Random.Range(0.94f, 1.06f);
        flat.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    /// <summary>Plays a sound at a place in the world. range is how far it carries, in metres.</summary>
    public static AudioSource PlayAt(string name, Vector3 pos, float volume = 1f, float range = 28f)
    {
        var clip = Clip(name);
        if (clip == null) return null;
        var go = new GameObject("Sfx_" + name);
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = clip;
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 2f;
        a.maxDistance = range;
        a.volume = volume * GameSettings.SfxVolume;
        a.pitch = Random.Range(0.94f, 1.06f);
        a.Play();
        Object.Destroy(go, clip.length / 0.9f + 0.2f);
        return a;
    }
}

/// <summary>Stand-in sounds made from sine waves and noise, used only until real files exist.</summary>
static class Synth
{
    const int Rate = 22050;

    public static AudioClip Make(string name)
    {
        switch (name)
        {
            case "swing": return Noise(name, 0.2f, 0.25f, 0.6f, true);
            case "punch": case "hit_flesh": return Thump(name, 0.16f, 110f, 0.7f);
            case "hurt": return Thump(name, 0.22f, 80f, 0.8f);
            case "place": return Thump(name, 0.25f, 70f, 0.8f);
            case "footstep": return Thump(name, 0.07f, 120f, 0.35f);
            case "chop": return Thump(name, 0.12f, 240f, 0.7f);
            case "rock": return Noise(name, 0.08f, 0.6f, 0.9f, false);
            case "metal": return Tones(name, 0.45f, 0.5f, 930f, 1370f);
            case "loot": return Tones(name, 0.16f, 0.35f, 660f, 880f);
            case "click": return Tones(name, 0.05f, 0.3f, 700f);
            case "craft_done": return Tones(name, 0.32f, 0.4f, 520f, 780f);
            case "levelup": return Arp(name, 0.4f, 523f, 659f, 784f, 1047f);
            case "eat": case "drink": case "cloth": case "search": return Noise(name, 0.3f, 0.12f, 0.35f, false);
            case "break": return Noise(name, 0.3f, 0.3f, 0.8f, false);
        }
        return null;
    }

    static AudioClip Finish(string name, float[] d)
    {
        var c = AudioClip.Create("synth_" + name, d.Length, 1, Rate, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip Thump(string name, float len, float hz, float vol)
    {
        int n = (int)(len * Rate);
        var d = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n, env = Mathf.Pow(1f - t, 3f);
            phase += (hz * (1f - t * 0.5f)) / Rate;
            d[i] = (Mathf.Sin(phase * Mathf.PI * 2f) * 0.8f + ((float)rng.NextDouble() - 0.5f) * 0.5f * (1f - t)) * env * vol;
        }
        return Finish(name, d);
    }

    static AudioClip Noise(string name, float len, float brightness, float vol, bool sweep)
    {
        int n = (int)(len * Rate);
        var d = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float low = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float env = sweep ? Mathf.Sin(t * Mathf.PI) : Mathf.Pow(1f - t, 2f);
            float k = sweep ? Mathf.Lerp(0.1f, brightness, Mathf.Sin(t * Mathf.PI)) : brightness;
            low += (((float)rng.NextDouble() - 0.5f) * 2f - low) * k;
            d[i] = low * env * vol;
        }
        return Finish(name, d);
    }

    static AudioClip Tones(string name, float len, float vol, params float[] hz)
    {
        int n = (int)(len * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, env = Mathf.Pow(1f - i / (float)n, 2f);
            float v = 0f;
            foreach (float f in hz) v += Mathf.Sin(t * f * Mathf.PI * 2f);
            d[i] = v / hz.Length * env * vol;
        }
        return Finish(name, d);
    }

    static AudioClip Arp(string name, float vol, params float[] hz)
    {
        int per = (int)(0.11f * Rate), n = per * hz.Length + (int)(0.3f * Rate);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            int k = Mathf.Min(hz.Length - 1, i / per);
            float t = i / (float)Rate, local = (i - k * per) / (float)Rate;
            d[i] = Mathf.Sin(t * hz[k] * Mathf.PI * 2f) * Mathf.Exp(-local * 5f) * vol;
        }
        return Finish(name, d);
    }
}

/// <summary>
/// Background music and ambience. Music follows the moment (menu, day, night, raid) and cross-fades between
/// tracks. Ambience follows where you are (roof wind, city, forest, night). Files: Resources/Audio/music_menu,
/// music_day, music_night, music_raid, amb_roof, amb_city, amb_forest, amb_night. Missing files are silent.
/// </summary>
public class MusicDirector : MonoBehaviour
{
    AudioSource a, b, amb;
    string track = "", ambTrack = "";
    bool aActive;

    void Awake()
    {
        a = Make("MusicA");
        b = Make("MusicB");
        amb = Make("Ambience");
    }

    AudioSource Make(string n)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.loop = true;
        s.spatialBlend = 0f;
        s.playOnAwake = false;
        s.volume = 0f;
        s.ignoreListenerPause = true;
        return s;
    }

    void Update()
    {
        var clock = GameClock.Instance;
        var p = PlayerController.Instance;
        bool night = clock != null && clock.IsNight;

        string want = (GameState.MenuOpen && !GameState.HasStarted) ? "music_menu"
                    : NightDirector.RaidActive ? "music_raid"
                    : night ? "music_night" : "music_day";
        if (want != track)
        {
            track = want;
            aActive = !aActive;
            var next = aActive ? a : b;
            next.clip = Sfx.Clip(want);
            if (next.clip != null) next.Play(); else next.Stop();
        }
        float dt = Time.unscaledDeltaTime;
        float mv = GameSettings.MusicVolume * 0.55f * (GameState.MenuOpen && GameState.HasStarted ? 0.5f : 1f);
        Fade(aActive ? a : b, mv, dt);
        Fade(aActive ? b : a, 0f, dt);

        string wantAmb = "amb_roof";
        if (p != null && p.OnStreet)
        {
            Vector3 pos = p.transform.position;
            bool forest = Mathf.Abs(pos.x) > StreetBuilder.CityHalf || Mathf.Abs(pos.z) > StreetBuilder.CityHalf;
            wantAmb = forest ? "amb_forest" : "amb_city";
        }
        if (night) wantAmb = "amb_night";
        if (wantAmb != ambTrack)
        {
            ambTrack = wantAmb;
            amb.clip = Sfx.Clip(wantAmb);
            amb.volume = 0f;
            if (amb.clip != null) amb.Play(); else amb.Stop();
        }
        Fade(amb, GameState.MenuOpen ? 0.15f * GameSettings.SfxVolume : 0.5f * GameSettings.SfxVolume, dt);
    }

    static void Fade(AudioSource s, float target, float dt)
    {
        s.volume = Mathf.MoveTowards(s.volume, target, dt * 0.4f);
        if (target <= 0f && s.volume <= 0.001f && s.isPlaying) s.Stop();
    }
}
