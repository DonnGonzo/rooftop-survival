using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

/// <summary>
/// Optional runtime loader for Polyfork .glb files placed in StreamingAssets/Models.
/// Easier path: drag the .glb into Assets/ (glTFast imports it as a prefab in the editor).
/// </summary>
public static class GlbLoader
{
    public static async Task<GameObject> Load(string fileName, Transform parent, Vector3 localPos, float scale = 1f)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Models", fileName);
        var gltf = new GltfImport();
        bool ok = await gltf.Load(new Uri(path).AbsoluteUri);
        if (!ok)
        {
            Debug.LogWarning("Could not load GLB: " + path);
            return null;
        }

        var root = new GameObject(Path.GetFileNameWithoutExtension(fileName));
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPos;
        root.transform.localScale = Vector3.one * scale;
        await gltf.InstantiateMainSceneAsync(root.transform);
        return root;
    }
}
