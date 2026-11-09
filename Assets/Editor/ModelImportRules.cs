using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the .glb models usable by the game without any manual steps:
/// 1. Every model under Resources/Models imports its clips as Legacy animation (the game plays clips with the
///    Animation component; the importer's default, Mecanim, leaves them unplayable).
/// 2. Resources/model-index.txt lists every model file, so the game can find models by name at runtime.
/// Runs whenever a .glb is added, changed or removed.
/// </summary>
public class ModelImportRules : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Models";
    const string IndexPath = "Assets/Resources/model-index.txt";

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool touched = imported.Concat(deleted).Concat(moved).Any(p => p.StartsWith(Folder) && p.EndsWith(".glb"));
        if (touched) Apply();
    }

    [MenuItem("Tools/Rooftop Survival/Refresh model index")]
    public static void Apply()
    {
        if (!Directory.Exists(Folder)) return;
        var stems = new List<string>();
        var reimport = new List<string>();
        foreach (string path in Directory.GetFiles(Folder, "*.glb"))
        {
            stems.Add(Path.GetFileNameWithoutExtension(path));
            string meta = path + ".meta";
            if (!File.Exists(meta)) continue;
            string text = File.ReadAllText(meta);
            if (text.Contains("animationMethod: 2"))
            {
                File.WriteAllText(meta, text.Replace("animationMethod: 2", "animationMethod: 1"));
                reimport.Add(path.Replace('\\', '/'));
            }
        }
        stems.Sort(System.StringComparer.Ordinal);
        string index = string.Join("\n", stems);
        if (!File.Exists(IndexPath) || File.ReadAllText(IndexPath) != index)
        {
            File.WriteAllText(IndexPath, index);
            AssetDatabase.ImportAsset(IndexPath);
        }
        foreach (string p in reimport) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        if (reimport.Count > 0) Debug.Log("[ModelImportRules] Set Legacy animation on " + reimport.Count + " model(s).");
    }
}
