using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anything the Interact key can use: supply crates, planters, the cot, the water tower, the workbench.
/// The HUD shows Prompt for the one the player is closest to and facing.
/// </summary>
public abstract class Interactable : MonoBehaviour
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [Tooltip("How close the player must be, in metres.")]
    public float range = 2.8f;

    /// <summary>Short text for the HUD, for example "Take 3 Scrap". Empty hides the prompt.</summary>
    public abstract string Prompt { get; }
    public abstract void Use();

    /// <summary>Where the range is measured from. Long objects can override this.</summary>
    public virtual Vector3 Point => transform.position;

    protected virtual void OnEnable() { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); }

    /// <summary>The usable thing nearest to the player, preferring what is in front of them.</summary>
    public static Interactable Best(Vector3 from, Vector3 forward)
    {
        Interactable best = null;
        float bestScore = float.MaxValue;
        foreach (var it in All)
        {
            if (it == null || !it.isActiveAndEnabled) continue;
            Vector3 to = it.Point - from;
            to.y *= 0.5f;   // height matters less than distance along the ground
            float d = to.magnitude;
            if (d > it.range) continue;
            if (string.IsNullOrEmpty(it.Prompt)) continue;
            float facing = d > 0.3f ? Vector3.Dot(forward, to / d) : 1f;
            float score = d - facing * 1.2f;
            if (score < bestScore) { best = it; bestScore = score; }
        }
        return best;
    }
}

/// <summary>A simple interactable driven by two delegates. Used for the cot, water tower and workbench.</summary>
public class ActionSpot : Interactable
{
    public System.Func<string> prompt;
    public System.Action use;

    public override string Prompt => prompt != null ? prompt() : "";
    public override void Use() { use?.Invoke(); }

    public static ActionSpot Add(GameObject go, float range, System.Func<string> prompt, System.Action use)
    {
        var a = go.AddComponent<ActionSpot>();
        a.range = range;
        a.prompt = prompt;
        a.use = use;
        return a;
    }
}
