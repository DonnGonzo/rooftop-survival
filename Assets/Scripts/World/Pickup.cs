using UnityEngine;

/// <summary>An item on the street or roof. Stand close and press E to take it.</summary>
public class Pickup : MonoBehaviour
{
    public string itemId;
    public int count;

    public static Pickup Create(string modelKey, string itemId, int count, Transform parent, Vector3 worldPos)
    {
        var go = ModelLibrary.Spawn(modelKey, parent, Vector3.zero, Random.Range(0f, 360f), fitFootprint: 1f, keepAnimations: false);
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * 0.5f;
            RoofGenerator.Tint(go, new Color(0.9f, 0.8f, 0.2f));
        }
        go.transform.position = worldPos;
        go.name = "Pickup_" + itemId;

        var p = go.AddComponent<Pickup>();
        p.itemId = itemId;
        p.count = count;
        return p;
    }

    void Update()
    {
        transform.Rotate(0f, 45f * Time.deltaTime, 0f);
    }

    public static Pickup Nearest(Vector3 from, float range)
    {
        Pickup best = null;
        float bestD = range;
        foreach (var pk in FindObjectsByType<Pickup>())
        {
            float d = Vector3.Distance(from, pk.transform.position);
            if (d < bestD) { best = pk; bestD = d; }
        }
        return best;
    }

    public void Take()
    {
        Items.Add(itemId, count);
        var def = Items.Def(itemId);
        Hud.Toast("Picked up " + count + " " + (def != null ? def.name : itemId) + ".");
        Tutorial.Notify("pickup");
        Destroy(gameObject);
    }
}
