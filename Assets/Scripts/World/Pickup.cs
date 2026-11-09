using UnityEngine;

/// <summary>A supply container on the street, in a room or on the roof. Stand close and press Interact to empty it.</summary>
public class Pickup : Interactable
{
    public string itemId;
    public int count;
    /// <summary>Called once when the player takes it.</summary>
    public System.Action onTaken;
    float baseY, phase;

    public override string Prompt
    {
        get
        {
            var def = Items.Def(itemId);
            return "Take " + count + " " + (def != null ? def.name : itemId);
        }
    }

    public static Pickup Create(string modelKey, string itemId, int count, Transform parent, Vector3 worldPos, float size = 0.9f)
    {
        var go = ModelLibrary.Spawn(modelKey, parent, Vector3.zero, Random.Range(0f, 360f), fitFootprint: size, keepAnimations: false);
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * 0.5f;
            Destroy(go.GetComponent<Collider>());
            Look.Paint(go, "planks", new Color(0.7f, 0.55f, 0.3f), 1f);
        }
        go.transform.position = worldPos;
        go.name = "Pickup_" + itemId;

        var p = go.AddComponent<Pickup>();
        p.itemId = itemId;
        p.count = count;
        p.range = 2.6f;
        return p;
    }

    void Start()
    {
        baseY = transform.position.y;
        phase = Random.Range(0f, 6f);
    }

    void Update()
    {
        // A slow bob so supplies catch the eye without spinning like arcade coins.
        var p = transform.position;
        p.y = baseY + 0.04f + Mathf.Sin(Time.time * 2f + phase) * 0.04f;
        transform.position = p;
    }

    public override void Use()
    {
        Items.Add(itemId, count);
        var def = Items.Def(itemId);
        Hud.Toast("+" + count + " " + (def != null ? def.name : itemId));
        Tutorial.Notify("pickup");
        var pc = PlayerController.Instance;
        if (pc != null && pc.OnStreet) Tutorial.Notify("loot");
        onTaken?.Invoke();
        Destroy(gameObject);
    }
}
